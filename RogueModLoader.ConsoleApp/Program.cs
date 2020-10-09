using System;
using System.Collections.Generic;
using RogueModLoader.Core;
using AbbLab.FileSystem;
using AbbLab.CLI;
using System.Xml.Serialization;
using System.Xml;
using System.Linq;
using System.Threading;
using System.Data;
using Octokit;
using System.IO;

namespace RogueModLoader.ConsoleApp
{
	public class Program
	{
		private const string CurrentVersion = "v0.3.2";

		public static void Main(/*string[] args*/)
		{
			App = new CLIApp();
			App.Start();
			RMLDirectory =
#if DEBUG
				new DirectoryHandle(@"D:\Steam\steamapps\common\Streets of Rogue\RogueModLoader");
			App.WriteLine("<back=darkcyan><fore=black>THIS IS DEBUG MODE");
#else
				new DirectoryHandle(System.IO.Directory.GetCurrentDirectory());
#endif
			GameDirectory = RMLDirectory.Parent;
			BIXDirectory = new DirectoryHandle(GameDirectory, "BepInEx");
			ConfigFile = new FileHandle(RMLDirectory, "RogueModLoader.Config.rml");

			CLIInputUpdater input = new CLIInputUpdater(":> ");
			App.AddUpdater(input);
			CLICommandParser parser = input.AttachCommandParser();
			parser.AddCommandsFrom(typeof(Program));

			bool errored = false;

			if (!BIXDirectory.Exists())
			{
				App.WriteLine("<back=red>Are you sure that you have BepInEx and RogueModLoader installed properly?");
				App.WriteLine("<back=red>BepInEx's and RogueModLoader's folders must be in the game's root directory.");
				errored = true;
			}
			if (!ConfigFile.Exists())
			{
				App.WriteLine("<back=red>The config file RogueModLoader.Config.rml could not be found!");
				errored = true;
			}
			else
			{
				XmlSerializer ser = new XmlSerializer(typeof(RogueModLoaderConfig));
				try
				{
					using (XmlReader reader = XmlReader.Create(ConfigFile.FullPath))
						Config = (RogueModLoaderConfig)ser.Deserialize(reader);
				}
				catch
				{
					App.WriteLine("<back=red>The config file RogueModLoader.Config.rml could not be read!");
					errored = true;
				}
			}
			if (errored)
			{
				App.Exit();
				return;
			}

			Loader = new RogueLoader(GameDirectory, Config.MainRepository, Config.ListPath)
			{
				RogueDataFile = new FileHandle(RMLDirectory, "RogueModLoader.Data.rml")
			};
			if (!Loader.PluginsFolder.Exists())
				Loader.PluginsFolder.Create();
			if (!Loader.DisabledFolder.Exists())
				Loader.DisabledFolder.Create();

			FileHandle unityNetworking = new FileHandle(GameDirectory, "StreetsOfRogue_Data", "Managed", "UnityEngine.Networking.dll");
			FileHandle hlapiNetworking = new FileHandle(GameDirectory, "StreetsOfRogue_Data", "Managed", "com.unity.multiplayer-hlapi.Runtime.dll");
			if (hlapiNetworking.Exists() && !unityNetworking.Exists())
			{
				using (FileStream inputFile = new FileStream(hlapiNetworking.FullPath, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (FileStream outputFile = new FileStream(unityNetworking.FullPath, System.IO.FileMode.Create))
					{
						byte[] buffer = new byte[0x10000];
						int bytes;
						while ((bytes = inputFile.Read(buffer, 0, buffer.Length)) > 0)
							outputFile.Write(buffer, 0, bytes);
					}
				}
				App.WriteLine("<fore=darkcyan>Created UnityEngine.Networking.dll.");
				App.WriteLine("<fore=darkcyan>It is required by some mods, that were made on the previous version of Unity.");
				App.WriteLine("");
			}

			Loader.ReadXmlData();

			if (Config.FetchOnStart)
			{
				Config.FetchOnStart = false;
				XmlSerializer ser = new XmlSerializer(typeof(RogueModLoaderConfig));
				try
				{
					using (XmlWriter writer = XmlWriter.Create(ConfigFile.FullPath))
						ser.Serialize(writer, Config);
				}
				catch
				{
					App.WriteLine("<back=red>The config file RogueModLoader.Config.rml could not be read!");
					errored = true;
				}
				confirmSent = true;
				FetchMods();
			}
			Console.Title = "RogueModLoader.ConsoleApp " + CurrentVersion;
			App.WriteLine("<fore=darkcyan>RogueModLoader.ConsoleApp " + CurrentVersion);
			App.WriteLine("");
			App.WriteLine("<fore=cyan>Use <back=darkgray>help</back> to see the list of commands.");
			App.WriteLine("");

			App.Wait();
		}

		public static DirectoryHandle RMLDirectory;
		public static DirectoryHandle GameDirectory;
		public static DirectoryHandle BIXDirectory;
		public static FileHandle ConfigFile;

		public static RogueModLoaderConfig Config;

		public static RogueLoader Loader;
		public static CLIApp App;

		public static bool confirmSent;

		public static bool FindMod(string query, out RogueMod mod)
		{
			Loader.CheckLocalFiles();
			string uq = query.ToUpperInvariant();
			List<RogueMod> found = Loader.Data.Mods.FindAll(m => m.Title.ToUpperInvariant() == uq || m.Repository.ToUpperInvariant() == uq);
			if (found.Count == 0) found = Loader.Data.Mods.FindAll(m => m.Title.ToUpperInvariant().StartsWith(uq) || m.Repository.ToUpperInvariant().StartsWith(uq));
			if (found.Count == 0) found = Loader.Data.Mods.FindAll(m => m.Title.ToUpperInvariant().Contains(uq) || m.Repository.ToUpperInvariant().Contains(uq));
			if (found.Count == 0)
			{
				mod = null;
				App.WriteLine("<fore=red>Could not find a mod \"" + query + "\"!");
				App.WriteLine("<fore=cyan>Use <back=darkgray>list</back> to show the list of available mods.");
				return confirmSent = false;
			}
			else if (found.Count == 1)
			{
				mod = found[0];
				return true;
			}
			else
			{
				mod = null;
				App.WriteLine("<fore=red>Found " + found.Count + " mods \"" + query + "\"!");
				App.WriteLine("<fore=red>: " + found[0].Title + ", " + found[1].Title + (found.Count > 2 ? ", " + found[2].Title : "") + (found.Count > 3 ? "..." : ""));
				return confirmSent = false;
			}
		}

		[CLICommand("help", "h")]
		public static void HelpCommands()
		{
			App.WriteLine("- <fore=cyan>h|help</fore> - shows the list of available commands");
			App.WriteLine("");
			App.WriteLine("- <fore=cyan>f|fetch</fore> - gets the list of available mods from the main repository and all mods' metadata");
			App.WriteLine("- <fore=cyan>f|fetch [mod]</fore> - gets the specified mod's metadata from the mod's repository");
			App.WriteLine("");
			App.WriteLine("- <fore=cyan>l|list</fore> - shows the list of available mods");
			App.WriteLine("- <fore=cyan>l|list [mod]</fore> - show the list of the mod's releases");
			App.WriteLine("");
			App.WriteLine("- <fore=cyan>n|info [mod]</fore> - displays information about the specified mod");
			App.WriteLine("");
			App.WriteLine("- <fore=cyan>i|install [mod]</fore> - downloads the specified mod");
			App.WriteLine("- <fore=cyan>i|install [mod] [version]</fore> - downloads the specified mod release");
			App.WriteLine("- <fore=cyan>un|uninstall [mod]</fore> - removes the specified mod from the computer");
			App.WriteLine("");
			App.WriteLine("- <fore=cyan>e|enable [mod]</fore> - enables the specified mod");
			App.WriteLine("- <fore=cyan>d|disable [mod]</fore> - disables the specified mod");
			App.WriteLine("");
			App.WriteLine("- <fore=cyan>u|update</fore> - updates all installed mods");
			App.WriteLine("- <fore=cyan>u|update [mod]</fore> - updates the specified mod");
			App.WriteLine("");
		}

		[CLICommand("fetch", "f")]
		public static void FetchMods()
		{
			TimeSpan span = DateTime.Now - Loader.Data.LastCheck;
			if (span.TotalMinutes < 10 && !confirmSent)
			{
				int minutes = (int)Math.Ceiling(span.TotalMinutes);
				App.WriteLine("<fore=yellow>According to the data file, you fetched mods' metadata less than " + minutes + " minute" + (minutes == 1 ? "" : "s") + " ago.");
				App.WriteLine("<fore=yellow>Are you sure you want to fetch new metadata? Enter the command again to proceed.");
				confirmSent = true;
				return;
			}
			confirmSent = false;
			App.WriteLine("<fore=cyan>Started fetching mods' metadata from " + Config.MainRepository + " (" + Config.ListPath + ")...");

			List<Release> releases = new List<Release>(Loader.GitHub.Repository.Release.GetAll(Loader.RepoOwner, Loader.RepoName).Result);
			Release latestPre = releases[0];
			Release latest = releases.Find(m => !m.Prerelease) ?? latestPre;
			if (CurrentVersion != latest.TagName && CurrentVersion != latestPre.TagName)
			{
				Release appr = CurrentVersion.StartsWith("v0") || CurrentVersion.Contains("-pre") ? latestPre : latest;
				App.WriteLine("");
				App.WriteLine("<fore=black><back=darkyellow>RogueModLoader has a new release \"" + appr.TagName + "\"!");
				App.WriteLine("<fore=black><back=darkyellow>Download the latest version here:");
				App.WriteLine("<fore=darkcyan>" + appr.HtmlUrl);
				App.WriteLine("");
			}

			try
			{
				Loader.FetchInformation().Wait();
				App.WriteLine("<fore=green>Fetched metadata from " + Loader.Data.Mods.Count + " mod repositories.");
			}
			catch (Exception e)
			{
				App.WriteLine("<back=red>" + e.Message);
			}

		}
		[CLICommand("fetch", "f")]
		public static void FetchModReleases(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal)
				{
					App.WriteLine("<fore=red>" + mod.Title + " is local!");
					return;
				}

				TimeSpan span = DateTime.Now - mod.LastCheck;
				if (span.TotalMinutes < 10 && !confirmSent)
				{
					int minutes = (int)Math.Ceiling(span.TotalMinutes);
					App.WriteLine("<fore=yellow>According to the data file, you fetched " + mod.Title + "' metadata less than " + minutes + " minute" + (minutes == 1 ? "" : "s") + " ago.");
					App.WriteLine("<fore=yellow>Are you sure you want to fetch new metadata? Enter the command again to proceed.");
					confirmSent = true;
					return;
				}
				confirmSent = false;

				string modName = mod.Title ?? mod.RepoOwner + "/" + mod.RepoName;
				App.WriteLine("<fore=cyan>Started fetching " + modName + "'s mod metadata...");
				try
				{
					mod.FetchInformation().Wait();
					App.WriteLine("<fore=green>Fetched metadata from " + modName + " (" + mod.Releases.Count + " releases)");
				}
				catch (Exception e)
				{
					App.WriteLine("<back=red>" + e.Message);
				}
			}

		}

		public const int itemsPerPage = 15;
		[CLICommand("list", "l")]
		public static void ListMods(int page = 1)
		{
			Loader.CheckLocalFiles();
			if (Loader.Data.Mods.Count == 0)
				App.WriteLine("<fore=red>No mods available! Try <back=darkgray>fetch</back> to fetch mods' metadata from the main repository.");
			else
			{
				App.WriteLine("<fore=cyan>" + Loader.Data.Mods.Count + " mods are available:");
				int minIndex = itemsPerPage * (page - 1);
				int maxIndex = itemsPerPage * page;

				Loader.Data.Mods.Sort((a, b) =>
				{
					if (a.IsLocal) return 1;
					if (a.IsEnabled() && !b.IsEnabled())
						return -1;
					else if (!a.IsEnabled() && b.IsEnabled())
						return 1;
					return a.Title.CompareTo(b.Title);
				});

				for (int i = minIndex; i < maxIndex && i < Loader.Data.Mods.Count; i++)
				{
					RogueMod mod = Loader.Data.Mods[i];

					string modName = mod.Title ?? mod.Repository;
					if (mod.IsLocal)
					{
						if (mod.IsEnabled())
							App.WriteLine("<fore=darkcyan>" + modName + " (local)");
						else
							App.WriteLine("<fore=darkgray>" + modName + " (local, disabled)");
						continue;
					}
					ModState state = mod.GetState();
					string currentStr = mod.Current?.Tag;
					string latestStr = mod.GetLatest(mod.Current?.Prerelease ?? false).Tag;
					if (state == ModState.NotInstalled)
						App.WriteLine(modName + " (" + latestStr + ") - <fore=darkgray>not installed");
					else if (state == ModState.UnknownVersion)
						App.WriteLine(modName + " (unknown version!) - <fore=red>use <back=darkgray>update " + mod.Title + "</back> to fix");
					else if (state == ModState.Disabled)
						App.WriteLine(modName + " (" + currentStr + ") - <fore=darkgray>disabled");
					else if (state == ModState.HasUpdate)
						App.WriteLine(modName + " (" + currentStr + "<" + latestStr + ") - <fore=yellow>has an update!</fore>");
					else
						App.WriteLine(modName + " (" + currentStr + ") - <fore=green>up to date");
				}

				int totalPages = (int)Math.Ceiling((double)Loader.Data.Mods.Count / itemsPerPage);
				if (Loader.Data.Mods.Count > itemsPerPage || page != 1)
					App.WriteLine("<fore=cyan>--- Page " + page + "/" + totalPages + " (" + (minIndex + 1) + "-" + Math.Min(maxIndex, Loader.Data.Mods.Count) + ")");
				App.WriteLine("");
			}

		}
		[CLICommand("list", "l")]
		public static void ListModReleases(string modStr, int page = 1)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal)
				{
					App.WriteLine("<fore=red>" + mod.Title + " is local!");
					return;
				}

				App.WriteLine("<fore=cyan>" + mod.Releases.Count + " " + mod.Title + " releases are available:");
				int minIndex = itemsPerPage * (page - 1);
				int maxIndex = itemsPerPage * page;

				for (int i = minIndex; i < maxIndex && i < mod.Releases.Count; i++)
				{
					RogueRelease release = mod.Releases[i];
					string tagStr = release.Prerelease ? "<fore=yellow>" + release.Tag + "</fore>" : release.Tag;
					App.WriteLine(tagStr + ": " + release.Title);
				}
				int totalPages = (int)Math.Ceiling((double)mod.Releases.Count / itemsPerPage);
				if (mod.Releases.Count > itemsPerPage || page != 1)
					App.WriteLine("<fore=cyan>--- Page " + page + "/" + totalPages + " (" + (minIndex + 1) + "-" + Math.Min(maxIndex, mod.Releases.Count) + ")");
				App.WriteLine("");
			}

		}

		[CLICommand("info", "n")]
		public static void InfoMod(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal)
				{
					App.WriteLine("<fore=red>" + mod.Title + " is local!");
					return;
				}

				bool showRepo = mod.Title != mod.RepoOwner + "/" + mod.RepoName;
				App.WriteLine("<fore=cyan>" + mod.Title + (showRepo ? " (" + mod.RepoOwner + "/" + mod.RepoName + ")" : ""));
				ModState state = mod.GetState();
				RogueRelease latest = mod.GetLatest(mod.Current?.Prerelease ?? false);
				string currentStr = state == ModState.NotInstalled ? "<fore=darkgray>not installed"
					: state == ModState.UnknownVersion ? "<fore=red>unknown"
					: mod.Current == latest ? "<fore=green>" + mod.CurrentTag
					: "<fore=yellow>" + mod.CurrentTag;
				App.WriteLine("<fore=cyan>Description: " + mod.Description);
				App.WriteLine("");
				App.WriteLine("<fore=cyan>Latest version:    " + (latest.Prerelease ? "<fore=yellow>" : "") + latest.Tag);
				App.WriteLine("<fore=cyan>Installed version: " + currentStr);
				App.WriteLine("");
				App.WriteLine("<fore=cyan>Url: https://github.com/" + mod.RepoOwner + "/" + mod.RepoName);
				App.WriteLine("<fore=cyan>Downloads: " + mod.Downloads);
				App.WriteLine("<fore=cyan>Watchers: " + mod.Watchers);
				App.WriteLine("<fore=cyan>Stars: " + mod.Stars);
				App.WriteLine("");
			}

		}

		[CLICommand("install", "i")]
		public static void InstallMod(string modStr)
		{
			confirmSent = false;
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal)
				{
					App.WriteLine("<fore=red>" + mod.Title + " is local!");
					return;
				}

				RogueRelease latest = mod.GetLatest(mod.Current?.Prerelease ?? false);
				InstallMod(mod.RepoOwner + "/" + mod.RepoName, latest.Tag);
			}

		}
		[CLICommand("install", "i")]
		public static void InstallMod(string modStr, string modVersion)
		{
			confirmSent = false;
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal)
				{
					App.WriteLine("<fore=red>" + mod.Title + " is local!");
					return;
				}

				string query = modVersion.ToUpperInvariant();
				RogueRelease found = mod.Releases.Find(r => r.Tag.ToUpperInvariant() == query || r.Title.ToUpperInvariant() == query)
					?? mod.Releases.Find(r => r.Tag.ToUpperInvariant().StartsWith(query) || r.Title.ToUpperInvariant().StartsWith(query))
					?? mod.Releases.Find(r => r.Tag.ToUpperInvariant().Contains(query) || r.Title.ToUpperInvariant().Contains(query));
				if (found == null)
				{
					App.WriteLine("<fore=red>Could not find a release \"" + modVersion + "\"!");
					App.WriteLine("<fore=cyan>Use <back=darkgray>list" + (mod.RepoOwner + "/" + mod.RepoName) + "</back> to show the list of available releases.");
					return;
				}
				string modName = mod.Title + " " + found.Tag;
				App.WriteLine("<fore=cyan>Started downloading " + modName);
				mod.StartDownload(found);

				double percents = -1;
				RogueDownload download = Loader.CurrentDownloads.Find(d => d.Mod == mod);
				while (download != null)
				{
					double newPercents = Math.Round(download.DownloadPercentage, 2);
					if (percents != newPercents)
					{
						App.WriteLine("Downloading <fore=white>" + modName + ": " + newPercents + "%");
						percents = newPercents;
					}
					Thread.Sleep(10);
					download = Loader.CurrentDownloads.Find(d => d.Mod == mod);
				}
				App.WriteLine("<fore=green>" + modName + " finished downloading!");
				if (mod.GetState() == ModState.Disabled) mod.Enable();
			}

		}

		[CLICommand("uninstall", "un", "ui")]
		public static void UninstallMod(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal && !confirmSent)
				{
					App.WriteLine("<fore=yellow>This mod is local. You won't be able to reinstall it using RogueModLoader.");
					App.WriteLine("<fore=yellow>Are you sure you want to delete this local mod? Enter the command again to proceed.");
					confirmSent = true;
					return;
				}
				confirmSent = false;

				if (mod.GetState() == ModState.NotInstalled)
					App.WriteLine("<fore=red>" + mod.Title + " is not installed!");
				else
				{
					mod.Delete();
					App.WriteLine("<fore=cyan>" + mod.Title + " was removed!");
					if (mod.IsLocal) Loader.Data.Mods.Remove(mod);
				}
			}

		}

		[CLICommand("enable", "e")]
		public static void EnableMod(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				ModState state = mod.GetState();
				if (state == ModState.NotInstalled)
					App.WriteLine("<fore=red>" + mod.Title + " is not installed!");
				else if (mod.IsEnabled())
					App.WriteLine("<fore=yellow>" + mod.Title + " is already enabled.");
				else
				{
					mod.Enable();
					App.WriteLine("<fore=cyan>" + mod.Title + " enabled!");
				}
			}

		}
		[CLICommand("disable", "d")]
		public static void DisableMod(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				ModState state = mod.GetState();
				if (state == ModState.NotInstalled)
					App.WriteLine("<fore=red>" + mod.Title + " is not installed!");
				else if (!mod.IsEnabled())
					App.WriteLine("<fore=yellow>" + mod.Title + " is already disabled.");
				else
				{
					mod.Disable();
					App.WriteLine("<fore=cyan>" + mod.Title + " disabled!");
				}
			}

		}

		[CLICommand("update", "u")]
		public static void UpdateMods()
		{
			confirmSent = false;
			List<RogueMod> needUpdating = new List<RogueMod>();
			foreach (RogueMod mod in Loader.Data.Mods.Where(m => !m.IsLocal))
			{
				if (mod.GetState() == ModState.HasUpdate || mod.GetState() == ModState.UnknownVersion)
					needUpdating.Add(mod);
			}
			App.WriteLine("<fore=yellow>Found " + needUpdating.Count + " mod updates.");
			foreach (RogueMod mod in needUpdating)
				InstallMod(mod.RepoOwner + "/" + mod.RepoName, mod.GetLatest(mod.Current?.Prerelease ?? false).Tag);
		}
		[CLICommand("update", "u")]
		public static void UpdateMod(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
				if (mod.IsLocal)
				{
					App.WriteLine("<fore=red>" + mod.Title + " is local!");
					return;
				}

				confirmSent = false;
				if (mod.GetState() == ModState.HasUpdate || mod.GetState() == ModState.UnknownVersion)
					InstallMod(mod.RepoOwner + "/" + mod.RepoName, mod.GetLatest(mod.Current?.Prerelease ?? false).Tag);
				else if (mod.GetState() == ModState.NotInstalled)
					App.WriteLine("<fore=red>" + (mod.Title ?? mod.RepoOwner + "/" + mod.RepoName) + " is not installed!");
				else if (mod.GetState() == ModState.Enabled)
					App.WriteLine("<fore=red>" + (mod.Title ?? mod.RepoOwner + "/" + mod.RepoName) + " doesn't have any updates!");
				else if (mod.GetState() == ModState.Disabled)
					App.WriteLine("<fore=red>" + (mod.Title ?? mod.RepoOwner + "/" + mod.RepoName) + " is disabled!");
			}
		}






	}

	public class RogueModLoaderConfig
	{
		public bool FetchOnStart;
		public string MainRepository;
		public string ListPath;

	}
}
