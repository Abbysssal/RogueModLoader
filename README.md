<div align="center">
  <p>
    <a href="https://github.com/Abbysssal/RogueModLoader/releases/latest">
      <img src="https://img.shields.io/github/v/release/Abbysssal/RogueModLoader?label=Latest%20release&style=for-the-badge&logo=github" alt="Latest release"/>
    </a>
    <a href="https://github.com/Abbysssal/RogueModLoader/releases">
      <img src="https://img.shields.io/github/v/release/Abbysssal/RogueModLoader?include_prereleases&label=Latest%20pre-release&style=for-the-badge&logo=github" alt="Latest pre-release"/>
    </a>
    <br/>
    <a href="https://github.com/Abbysssal/RogueModLoader/releases">
      <img src="https://img.shields.io/github/downloads/Abbysssal/RogueModLoader/total?label=Downloads&style=for-the-badge" alt="Downloads"/>
    </a>
    <a href="https://github.com/Abbysssal/RogueModLoader/subscription">
      <img src="https://img.shields.io/github/watchers/Abbysssal/RogueModLoader?color=green&label=Watchers&style=for-the-badge" alt="Watchers"/>
    </a>
    <a href="https://github.com/Abbysssal/RogueModLoader/stargazers">
      <img src="https://img.shields.io/github/stars/Abbysssal/RogueModLoader?color=green&label=Stars&style=for-the-badge" alt="Stars"/>
    </a>
  </p>
</div>

## Links ##
*  [Download RogueModLoader](https://github.com/Abbysssal/RogueModLoader/releases)
*  [RogueLibs on GitHub](https://github.com/Abbysssal/RogueLibs)

## Installation ##
1.  Install BepInEx:
    1.  [Download the latest version of BepInEx](https://github.com/BepInEx/BepInEx/releases/latest);
    2.  Drag all files from the archive into directory /Steam/SteamApps/common/\<game>/;
    3.  Run the game, so BepInEx can create needed files and directories, and close the game;
3.  [Download the latest version of RogueModLoader](https://github.com/Abbysssal/RogueModLoader/releases/latest);
4.  Move the contents of the archive to the game root directory;
5.  Done! Now run RogueModLoader.ConsoleApp.exe in the RogueModLoader folder and enjoy!

## Deinstallation ##
1.  Just remove RogueModLoader folder from the game root directory.

## Usage ##

- `h|help` - shows the list of available commands

- `f|fetch` - gets the list of available mods from the main repository and all mods' metadata
- `f|fetch [mod]` - gets the specified mod's metadata from the mod's repository

- `l|list` - shows the list of available mods
- `l|list [mod]` - show the list of the mod's releases

- `n|info [mod]` - displays information about the specified mod

- `i|install [mod]` - downloads the specified mod
- `i|install [mod] [version]` - downloads the specified mod release
- `un|uninstall [mod]` - removes the specified mod from the computer

- `e|enable [mod]` - enables the specified mod
- `d|disable [mod]` - disables the specified mod

- `u|update` - updates all installed mods
- `u|update [mod]` - updates the specified mod

## Configuration ##

In order to configure RogueModLoader to work with any other game supported by BepInEx, you just need to edit some of the files. Let's look at the RogueModLoader.Config.rml file:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RogueModLoaderConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <FetchOnStart>true</FetchOnStart>
  <MainRepository>Abbysssal/RogueModLoader</MainRepository>
  <ListPath>RogueModLoader.List.rml</ListPath>
</RogueModLoaderConfig>
```
- `<FetchOnStart>` determines whether RogueModLoader should execute `fetch` command on start. After fetching metadata, it is automatically set to `false`. When distributing RogueModLoader.Config.rml, you should make sure that this variable is set to `true`.
- `<MainRepository>` - the repository of your modified RogueModLoader. It's where the RogueModLoader.List.rml should be.
- `<ListPath>` - determines the path to the RogueModLoader.List.rml (name can be different) in the repository.

Now let's look at RogueModLoader.List.rml:
```xml
<repositories>
  <repository>Abbysssal/RogueLibs</repository>
  <repository>Abbysssal/ECTD</repository>
  <repository>Abbysssal/aToM</repository>
  <repository>Abbysssal/aToI</repository>
  <repository>Moonkis/SoRFramerateLimiter</repository>
</repositories>
```
It's just a list of mod repositories, that you want to include in your RogueModLoader.






