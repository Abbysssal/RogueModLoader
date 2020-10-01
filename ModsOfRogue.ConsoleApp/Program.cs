using RogueModLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ModsOfRogue.ConsoleApp
{
	public static class Program
	{
		public static void Main(/*string[] args*/) => MainAsync().Wait();
		public static async Task MainAsync(/*string[] args*/)
		{
			RogueLoader loader = new RogueLoader(RogueModUtilities.GetSteamGameDirectory("Streets of Rogue"), "Abbysssal/RogueModLoader", "RogueModLoader.List.rml");
			//await loader.FetchInformation();

			RogueMod mod = loader.Data.Mods.Find(m => m.RepoName == "RogueLibs");
			RogueRelease rel = mod.Releases.Find(r => r.Tag == "v2.0");
			mod.StartDownload(rel);

			while (loader.CurrentDownloads.Count > 0)
			{
				Console.WriteLine("{0}% ({1}/{2}) [{3}]", loader.DownloadPercentage, loader.BytesReceived, loader.BytesTotal, loader.CurrentDownloads.Count);
				await Task.Delay(1);
			}

			Console.WriteLine("Test Finish 1");
			Console.WriteLine("Test Finish 2");
			Console.WriteLine("Test Finish 3");
		}
	}
}
