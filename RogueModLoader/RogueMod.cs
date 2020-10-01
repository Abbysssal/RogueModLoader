using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using AbbLab.FileSystem;
using Octokit;

namespace RogueModLoader
{
	[XmlRoot("mod")]
	public class RogueMod : IXmlSerializable
	{
		public RogueMod() { }
		public RogueMod(RogueLoader loader) => Loader = loader;

		public RogueLoader Loader { get; set; }

		public string RepoOwner { get; set; }
		public string RepoName { get; set; }

		private string title;
		public string Title
		{
			get => title ?? RepoOwner + "/" + RepoName;
			set => title = value;
		}
		public string Description { get; set; }

		public int Stars { get; set; }
		public int Watchers { get; set; }
		public int Downloads { get; set; }

		public FileHandle File { get; set; }
		public List<RogueRelease> Releases { get; } = new List<RogueRelease>();
		public string CurrentTag { get; set; }
		public RogueRelease Current => Releases.Find(r => r.Tag == CurrentTag);
		public RogueRelease GetLatest(bool includePrereleases)
			=> includePrereleases ? Releases[0]
			: Releases.Find(r => !r.Prerelease) ?? Releases[0];

		public void CheckFile()
		{
			if (File?.Exists() != true)
			{
				RogueRelease release = Current ?? GetLatest(Current?.Prerelease ?? false);
				if (release != null) // find by current/latest release's file
				{
					FileHandle pluginFile = new FileHandle(Path.Combine(Loader.PluginsFolder.FullPath, release.FileName));
					FileHandle disabledFile = new FileHandle(Path.Combine(Loader.DisabledFolder.FullPath, release.FileName));
					bool found = false;
					if (pluginFile.Exists()) { File = pluginFile; found = true; }
					if (disabledFile.Exists())
					{
						if (found) disabledFile.Delete();
						else File = disabledFile;
					}
				}
			}
		}

		public void WriteXml(XmlWriter xml)
		{
			xml.WriteAttributeString("repo", RepoOwner + "/" + RepoName);
			xml.WriteAttributeString("tag", CurrentTag);
			if (title != null) xml.WriteElementString("title", title);
			if (Description != null) xml.WriteElementString("description", Description);

			xml.WriteElementString("stars", Stars.ToString());
			xml.WriteElementString("watchers", Watchers.ToString());
			xml.WriteElementString("downloads", Downloads.ToString());

			CheckFile();
			if (File?.Exists() == true) xml.WriteElementString("path", File.FullPath);

			xml.WriteStartElement("releases");
			XmlSerializer ser = new XmlSerializer(typeof(RogueRelease));
			foreach (RogueRelease release in Releases)
				ser.Serialize(xml, release);
			xml.WriteEndElement();
		}
		public void ReadXml(XmlReader xml)
		{
			string[] repo = xml.GetAttribute("repo")?.Split(new char[] { '/' });
			if (repo == null || repo.Length < 2)
				throw new ArgumentException("Invalid repository format!", nameof(repo));
			RepoOwner = repo[0];
			RepoName = repo[1];

			CurrentTag = xml.GetAttribute("tag");

			if (xml.ReadNonEmptyElement())
			{
				xml.MoveToContent();
				while (xml.NodeType != XmlNodeType.EndElement)
				{
					if (xml.Name == "title")
						title = xml.ReadElementContentAsString();
					else if (xml.Name == "description")
						Description = xml.ReadElementContentAsString();
					else if (xml.Name == "stars")
					{
						if (int.TryParse(xml.ReadElementContentAsString(), out int stars))
							Stars = stars;
					}
					else if (xml.Name == "watchers")
					{
						if (int.TryParse(xml.ReadElementContentAsString(), out int watchers))
							Watchers = watchers;
					}
					else if (xml.Name == "downloads")
					{
						if (int.TryParse(xml.ReadElementContentAsString(), out int downloads))
							Downloads = downloads;
					}
					else if (xml.Name == "path")
					{
						File = new FileHandle(xml.ReadElementContentAsString());
					}
					else if (xml.Name == "releases")
					{
						if (xml.ReadNonEmptyElement())
						{
							xml.MoveToContent();
							XmlSerializer ser = new XmlSerializer(typeof(RogueRelease));
							while (xml.Name == "release")
							{
								RogueRelease release = (RogueRelease)ser.Deserialize(xml);
								release.Mod = this;
								Releases.Add(release);
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

		public void StartDownload(RogueRelease release)
		{
			if (File?.Exists() == true) File.Delete();
			WebClient web = new WebClient();
			string filePath = File?.FullPath ?? Path.Combine(Loader.PluginsFolder.FullPath, release.FileName);
			File = new FileHandle(filePath);
			CurrentTag = release.Tag;
			Loader.CurrentDownloads.Add(new RogueDownload(this, web)
			{
				Task = web.DownloadFileTaskAsync(release.DownloadURL, filePath)
			});
		}
		public void Delete()
		{
			File.Delete();
			File = null;
			CurrentTag = null;
		}

		public ModState GetState() => File?.Exists() != true ? ModState.NotInstalled // "Download" button
			: Current == null ? ModState.UnknownVersion // "Fix/Update" button
			: File.Parent.Name != "plugins" ? ModState.Disabled // "Uninstall" button
			: CurrentTag != GetLatest(Current.Prerelease).Tag ? ModState.HasUpdate // "Update" button
			: ModState.Enabled; // "Up To Date" label

		private bool updating = false;
		private bool[] updateSession = new bool[2];
		public async Task FetchInformation()
		{
			if (!updating)
			{
				updating = true;
				updateSession = new bool[2];
			}
			if (Loader.CanRequest(1) && !updateSession[0])
			{
				Repository repo = await Loader.GitHub.Repository.Get(RepoOwner, RepoName);
				title = repo.FullName;
				Description = repo.Description;
				Stars = repo.StargazersCount;
				Watchers = repo.WatchersCount;
				updateSession[0] = true;
			}
			if (Loader.CanRequest(1) && !updateSession[1])
			{
				Releases.Clear();
				Downloads = 0;
				IReadOnlyList<Release> releases = await Loader.GitHub.Repository.Release.GetAll(RepoOwner, RepoName);
				foreach (Release release in releases)
				{
					if (release.Assets.Count < 1) continue;
					RogueRelease rogue = new RogueRelease(this);
					rogue.From(release);
					Downloads += release.Assets[0].DownloadCount;
					Releases.Add(rogue);
				}

			}
			Loader.WriteXmlData();
		}

		public System.Xml.Schema.XmlSchema GetSchema() => null;

	}
	public enum ModState
	{
		NotInstalled = 1,
		UnknownVersion = 2,
		Disabled = 3,
		HasUpdate = 4,
		Enabled = 5
	}
}
