﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RogueModLoaderCore;
using AbbLab.FileSystem;
using AbbLab.CLI;
using System.Xml.Serialization;
using System.Xml;
using System.Globalization;
using System.Linq;
using AbbLab.Utilities;
using System.Threading;

namespace RogueModLoader.ConsoleApp
{
	public class Program
	{
		public static void Main(/*string[] args*/)
		{
#if DEBUG
			RMLDirectory = new DirectoryHandle(@"D:\Steam\steamapps\common\Streets of Rogue\RogueModLoader");
#else
			RMLDirectory = new DirectoryHandle(Directory.GetCurrentDirectory());
#endif
			GameDirectory = RMLDirectory.Parent;
			BIXDirectory = new DirectoryHandle(GameDirectory, "BepInEx");
			ConfigFile = new FileHandle(RMLDirectory, "RogueModLoader.Config.rml");

			App = new CLIApp();
			App.Start();

#if DEBUG
			App.WriteLine("<back=cyan>DEBUG MODE");
#endif

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
			Loader.PluginsFolder.Create();
			Loader.DisabledFolder.Create();
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
			string uq = query.ToUpperInvariant();
			mod = Loader.Data.Mods.Find(m => (m.RepoOwner + "/" + m.RepoName).ToUpperInvariant() == uq || m.Title.ToUpperInvariant() == uq)
				?? Loader.Data.Mods.Find(m => (m.RepoOwner + "/" + m.RepoName).ToUpperInvariant().StartsWith(uq) || m.Title.ToUpperInvariant().StartsWith(uq))
				?? Loader.Data.Mods.Find(m => (m.RepoOwner + "/" + m.RepoName).ToUpperInvariant().Contains(uq) || m.Title.ToUpperInvariant().Contains(uq));
			if (mod == null)
			{
				App.WriteLine("<fore=red>Could not find a mod \"" + query + "\"!");
				App.WriteLine("<fore=cyan>Use <back=darkgray>list</back> to show the list of available mods.");
				confirmSent = false;
				return false;
			}
			return true;
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
			if (Loader.Data.Mods.Count == 0)
				App.WriteLine("<fore=red>No mods available! Try <back=darkgray>fetch</back> to fetch mods' metadata from the main repository.");
			else
			{
				App.WriteLine("<fore=cyan>" + Loader.Data.Mods.Count + " mods are available:");
				int minIndex = itemsPerPage * (page - 1);
				int maxIndex = itemsPerPage * page;

				for (int i = minIndex; i < maxIndex && i < Loader.Data.Mods.Count; i++)
				{
					RogueMod mod = Loader.Data.Mods[i];

					string modName = mod.Title ?? mod.RepoOwner + "/" + mod.RepoName;
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

					int totalPages = (int)Math.Ceiling((double)Loader.Data.Mods.Count / itemsPerPage);
					if (Loader.Data.Mods.Count > itemsPerPage || page != 1)
						App.WriteLine("<fore=cyan>--- Page " + page + "/" + totalPages + " (" + (minIndex + 1) + "-" + Math.Min(maxIndex, Loader.Data.Mods.Count) + ")");
				}
			}

		}
		[CLICommand("list", "l")]
		public static void ListModReleases(string modStr, int page = 1)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
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
			}

		}

		[CLICommand("info", "n")]
		public static void InfoMod(string modStr)
		{
			if (FindMod(modStr, out RogueMod mod))
			{
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
			}

		}

		[CLICommand("install", "i")]
		public static void InstallMod(string modStr)
		{
			confirmSent = false;
			if (FindMod(modStr, out RogueMod mod))
			{
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
				if (mod.GetState() == ModState.NotInstalled)
					App.WriteLine("<fore=red>" + mod.Title + " is not installed!");
				else
				{
					mod.Delete();
					App.WriteLine("<fore=cyan>" + mod.Title + " was removed!");
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
					App.WriteLine("<fore=yellow>" + mod.Title + " is already enabled");
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
					App.WriteLine("<fore=yellow>" + mod.Title + " is already disabled");
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
			foreach (RogueMod mod in Loader.Data.Mods)
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
