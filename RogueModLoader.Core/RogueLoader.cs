using AbbLab.FileSystem;
using Octokit;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.Linq;
using System.Net;

namespace RogueModLoaderCore
{
	public class RogueLoader
	{
		public RogueLoader(DirectoryHandle gameRootDirectory, string repo, string configPath)
		{
			GameRootDirectory = gameRootDirectory;
			BepInExDirectory = new DirectoryHandle(gameRootDirectory, "BepInEx");
			PluginsFolder = new DirectoryHandle(BepInExDirectory, "plugins");
			DisabledFolder = new DirectoryHandle(BepInExDirectory, "disabled-plugins");
			RogueDataFile = new FileHandle(BepInExDirectory, "RogueModLoader.Data.rml");
			string[] split = repo?.Split(new char[] { '/' }, 2);
			if (split == null || split.Length < 2) throw new ArgumentException("Invalid repository format!");
			RepoOwner = split[0];
			RepoName = split[1];
			ConfigPath = configPath;
			GitHub = new GitHubClient(new ProductHeaderValue("RogueModLoader", "0.1"));

		}
		public void Start() => ReadXmlData();

		public DirectoryHandle GameRootDirectory { get; set; }
		public DirectoryHandle BepInExDirectory { get; set; }
		public DirectoryHandle PluginsFolder { get; set; }
		public DirectoryHandle DisabledFolder { get; set; }
		public string RepoOwner { get; }
		public string RepoName { get; }
		public string ConfigPath { get; set; }

		public GitHubClient GitHub { get; set; }
		public bool CanRequest(int requests)
		{
			ApiInfo info = GitHub.GetLastApiInfo();
			return info == null || requests <= info.RateLimit.Remaining;
		}

		public FileHandle RogueDataFile { get; set; }
		public RogueData Data { get; set; } = new RogueData();

		private readonly object writing = new object();
		public void ReadXmlData()
		{
			XmlSerializer ser = new XmlSerializer(typeof(RogueData));
			if (RogueDataFile.Exists())
			{
				try
				{
					using (XmlReader reader = XmlReader.Create(RogueDataFile.FullPath))
						Data = (RogueData)ser.Deserialize(reader);
					foreach (RogueMod mod in Data.Mods)
					{
						mod.Loader = this;
						mod.CheckFile();
					}
				}
				catch
				{
					using (XmlWriter writer = XmlWriter.Create(RogueDataFile.FullPath))
						ser.Serialize(writer, Data);
				}
			}
			else
				using (XmlWriter writer = XmlWriter.Create(RogueDataFile.FullPath))
					ser.Serialize(writer, Data);
			Data.Loader = this;
		}
		public void WriteXmlData()
		{
			lock (writing)
			{
				XmlSerializer ser = new XmlSerializer(typeof(RogueData));
				using (XmlWriter writer = XmlWriter.Create(RogueDataFile.FullPath))
					ser.Serialize(writer, Data);
			}
		}

		public async Task FetchInformation()
		{
			IReadOnlyList<RepositoryContent> contentList = await GitHub.Repository.Content.GetAllContents(RepoOwner, RepoName, ConfigPath);
			if (contentList.Count < 1) throw new ArgumentException("Config file not found!");
			RepositoryContent content = contentList[0];
			if (content.Type.Value != ContentType.File) throw new ArgumentException("An entry at the specified path is not a file!");
			FileHandle file = new FileHandle(BepInExDirectory, "RogueModLoader.List.rml");
			WebClient web = new WebClient();
			await web.DownloadFileTaskAsync(content.DownloadUrl, file.FullPath);

			try
			{
				XmlSerializer ser = new XmlSerializer(typeof(RogueModsList));
				RogueModsList list = null;
				using (XmlReader reader = XmlReader.Create(file.FullPath))
					list = (RogueModsList)ser.Deserialize(reader);

				Dictionary<string, string> mod2ver = new Dictionary<string, string>();
				foreach (RogueMod oldMod in Data.Mods)
					mod2ver.Add(oldMod.RepoOwner + "/" + oldMod.RepoName, oldMod.CurrentTag);
				Data.Mods.Clear();
				foreach ((string, string) repo in list.Repos)
				{
					RogueMod mod = new RogueMod(this)
					{
						RepoOwner = repo.Item1,
						RepoName = repo.Item2
					};
					await mod.FetchInformation();
					if (mod2ver.TryGetValue(repo.Item1 + "/" + repo.Item2, out string currentTag))
						mod.CurrentTag = currentTag;
					if (mod.Releases.Count > 0)
						Data.Mods.Add(mod);
				}
				Data.LastCheck = DateTime.Now;
			}
			finally
			{
				file.Delete();
			}
			WriteXmlData();
		}

		public List<RogueDownload> CurrentDownloads { get; } = new List<RogueDownload>();
		public void RemoveCompleted() => CurrentDownloads.RemoveAll(d => d.Complete || d.Task.IsCompleted);
		public bool Complete
		{
			get
			{
				RemoveCompleted();
				return CurrentDownloads.All(d => d.Complete);
			}
		}
		public long BytesReceived
		{
			get
			{
				RemoveCompleted();
				return CurrentDownloads.Sum(d => d.BytesReceived);
			}
		}
		public long BytesTotal
		{
			get
			{
				RemoveCompleted();
				return CurrentDownloads.Sum(d => d.BytesTotal);
			}
		}
		public double DownloadPercentage
		{
			get
			{
				long received = BytesReceived;
				if (received == 0) return 0d;
				return (double)received / BytesTotal * 100d;
			}
		}

	}
	[XmlRoot("data")]
	public class RogueData : IXmlSerializable
	{
		public RogueLoader Loader { get; set; }
		public DateTime LastCheck { get; set; } = DateTime.MinValue;

		public List<RogueMod> Mods { get; } = new List<RogueMod>();

		public void WriteXml(XmlWriter xml)
		{
			xml.WriteAttributeString("lastCheck", LastCheck.ToBinary().ToString());

			xml.WriteStartElement("mods");
			XmlSerializer ser = new XmlSerializer(typeof(RogueMod));
			foreach (RogueMod mod in Mods)
				ser.Serialize(xml, mod);
			xml.WriteEndElement();
		}
		public void ReadXml(XmlReader xml)
		{
			string bin = xml.GetAttribute("lastCheck");
			if (long.TryParse(bin, out long binLong))
				LastCheck = DateTime.FromBinary(binLong);

			if (xml.ReadNonEmptyElement())
			{
				xml.MoveToContent();
				while (xml.NodeType != XmlNodeType.EndElement)
				{
					if (xml.Name == "mods")
					{
						if (xml.ReadNonEmptyElement())
						{
							xml.MoveToContent();
							XmlSerializer ser = new XmlSerializer(typeof(RogueMod));
							while (xml.Name == "mod")
							{
								RogueMod mod = (RogueMod)ser.Deserialize(xml);
								mod.Loader = Loader;
								Mods.Add(mod);
								xml.MoveToContent();
							}
							xml.ReadEndElement();
						}
					}
					else
						xml.Skip();
					xml.MoveToContent();
				}
				xml.ReadEndElement();
			}
		}

		public System.Xml.Schema.XmlSchema GetSchema() => null;

	}
	[XmlRoot("repositories")]
	public class RogueModsList : IXmlSerializable
	{
		public List<(string, string)> Repos { get; } = new List<(string, string)>();

		public void WriteXml(XmlWriter xml)
		{
			foreach ((string, string) repo in Repos)
				xml.WriteElementString("repository", repo.Item1 + "/" + repo.Item2);
		}
		public void ReadXml(XmlReader xml)
		{
			if (xml.ReadNonEmptyElement())
			{
				xml.MoveToContent();
				while (xml.NodeType != XmlNodeType.EndElement)
				{
					if (xml.Name == "repository")
					{
						string[] split = xml.ReadElementContentAsString()?.Split(new char[] { '/' }, 2);
						if (split == null || split.Length < 2) throw new ArgumentException("Invalid repository format!");
						Repos.Add((split[0], split[1]));
					}
					xml.MoveToContent();
				}
			}
		}

		public System.Xml.Schema.XmlSchema GetSchema() => null;

	}
	public class RogueDownload
	{
		public RogueDownload(RogueMod mod, WebClient web)
		{
			Mod = mod;
			WebClient = web;
			web.DownloadProgressChanged += Web_DownloadProgressChanged;
			web.DownloadFileCompleted += Web_DownloadFileCompleted;
		}

		public RogueMod Mod { get; }
		public WebClient WebClient { get; }
		public Task Task { get; set; }

		public bool Complete;
		public long BytesReceived;
		public long BytesTotal;
		public double DownloadPercentage => BytesReceived == 0 ? 0 : (double)BytesReceived / BytesTotal * 100;

		private void Web_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			BytesReceived = e.BytesReceived;
			BytesTotal = e.TotalBytesToReceive;
		}
		private void Web_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			Complete = true;
			WebClient.DownloadProgressChanged -= Web_DownloadProgressChanged;
			WebClient.DownloadFileCompleted -= Web_DownloadFileCompleted;
			Mod.Loader.CurrentDownloads.Remove(this);
			Mod.Loader.WriteXmlData();
		}

	}
}
