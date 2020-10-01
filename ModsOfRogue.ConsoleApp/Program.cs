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
			await loader.FetchInformation();

			Console.WriteLine("Test Finish");
		}
	}
}
