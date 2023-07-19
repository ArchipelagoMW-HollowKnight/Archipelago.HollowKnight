# Archipelago.HollowKnight

A mod which enables Hollow Knight to act as an Archipelago client, enabling multiworld and randomization driven by the [Archipelago multigame multiworld system](https://archipelago.gg).

## Installing Archipelago.HollowKnight
### Installing with Scarab+
1. [Download Scarab+](https://themulhima.github.io/Scarab?download).
2. Place Scarab+ in a folder other than your Downloads folder and run it
   * If it does not detect your HK install directory, lead Scarab to the correct directory.
   * Also, don’t pirate the game. >:(
3. Install and enable Archipelago.
   * There are several mods that are needed to for Archipelago to run. They are installed automatically.
    * Archipelago Map Mod is an in-game tracker for Archipelago. It is optional, but if you choose to install it, make sure that RandoMapMod and MapChanger are disabled or uninstalled if you came from the standalone randomizer.
4. Start the game and ensure **Archipelago** appears in the top left corner of the main menu.

## Joining an Archipelago Session
1. Start the game after installing all necessary mods.
2. Create a **new save game.**
3. Select the **Archipelago** game mode from the mode selection screen.
4. Enter in the correct settings for your Archipelago server.
5. Hit **Start** to begin the game. The game will stall for a few seconds while it does all item placements.
6. The game will immediately drop you into the randomized game. So if you are waiting for a countdown then wait for it to lapse before hitting Start, or hit Start then pause the game once you're in it.

# Known Issues

- Deathlink may occasionally enter a state where incoming Deathlinks will not affect you.  To fix this if it happens, either die intentionally or save and reload your game.
- Starting inventory is displayed twice in RecentItems
- Archipelago icons stop showing when reloading a save.  This is cosmetic and does not affect gameplay.

# Contributing
Contributions are welcome, all code is licensed under the MIT License. Please track your work within the repository if you are taking on a feature. This is done via GitHub Issues. If you are interesting in taking on an issue please comment on the issue to have it assigned to you. If you are looking to contribute something that isn't in the issues list then please submit an issue to describe what work you intend to take on.

Contribution guidelines:
* All issues should be labeled appropriately.
* All in-progress issues should have someone assigned to them.
* Pull Requests must have at least (and preferably exactly) one linked issue which they close out.
* Please use feature branches, especially if working in this repository (not a fork).
* Please match the style of surrounding code. 
    * The only exception to this guideline is that we are in the progress of phasing out the usage of `var`. Please use explicit typing instead.

## Development Setup
Follow the instructions in the csproj file to create a LocalOverrides.targets file with your Hollow Knight installation path. If you use the Hollow Knight Modding Visual Studio extension (recommended), there is an item template to create this file for you automatically.

Post-build events will automatically package the mod for export **as well as install it in your HK installation.** When developing on the mod **do not install Archipelago through Scarab.** If Archipelago is installed through Scarab, uninstall or disable it before testing. Archipelago.HollowKnight will not load if both Scarab and development versions are installed at the same time.

