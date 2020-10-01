using AbbLab.FileSystem;
using Microsoft.Win32;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace RogueModLoader
{
	public static class RogueModUtilities
	{
		private static bool triedToFind;
		private static DirectoryHandle steamDirectory;
		public static DirectoryHandle SteamDirectory
		{
			get
			{
				if (triedToFind || steamDirectory != null) return steamDirectory;
				try
				{
					string steamPath = (string)Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam").GetValue("InstallPath");
					steamDirectory = new DirectoryHandle(steamPath);
					triedToFind = true;
					return steamDirectory;
				}
				catch
				{
					triedToFind = true;
					return null;
				}
			}
		}

		public static DirectoryHandle GetSteamGameDirectory(string gameName)
			=> new DirectoryHandle(Path.Combine(steamDirectory.FullPath, "steamapps", "common", gameName));

	}
}
