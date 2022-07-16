# Archipelago.HollowKnight

A mod which enables Hollow Knight to act as an Archipelago client, enabling multiworld and randomization driven by the [Archipelago multigame multiworld system](https://archipelago.gg).

## Installing Archipelago.HollowKnight
### Installing with Scarab
1. Download Scarab from the [Scarab releases page](https://github.com/fifty-six/Scarab/releases).
2. Extract Scarab and run it.
	* If it does not detect your HK install directory, lead Scarab to the correct directory.
	* Also, don’t pirate the game. >:(
3. Install and enable Archipelago.
4. Start the game and ensure **Archipelago** appears in the top left corner of the main menu.

### Installing Manually
1. Download Scarab from the [Scarab releases page](https://github.com/fifty-six/Scarab/releases).
2. Extract Scarab and run it.
	* If it does not detect your HK install directory, lead Scarab to the correct directory.
	* Also, don’t pirate the game. >:(
3. Install and enable ItemChanger.
4. Install and enable MenuChanger.
5. Install and enable Benchwarp.
6. Install and enable RecentItemsDisplay.
7. (Optional) Install and enable QoL.
8. Download Archipelago.HollowKnight from the [Archipelago.HollowKnight releases page]().
9. Click the “Open Mods” button near the bottom left of the Scarab UI.
10. In the resulting folder, extract the Archipelago.HollowKnight.zip file you Downloaded.
	* There should now be a new folder called Archipelago.HollowKnight which contains the mod files.
11. Start the game and ensure **Archipelago** appears in the top left corner of the main menu.

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
- Most non-shop locations that cost Geo (like Stags and Cornifer locations) do not charge a cost in Archipelago 0.3.3.  This is fixed in development and should be fixed in the next Archipelago release.

# Contributing
Contributions are welcome, all code is licensed under the MIT License. Please track your work within the repository if you are taking on a feature. This is done via GitHub Issues. If you are taking on an issue please comment on the issue and assign yourself (if possible). If you are looking to contribute something that isn't in the issues list then please submit an issue to describe what work you intend to take on.

There are a few guidelines/rules I personally follow and deeply encourage all contributors to follow:
* All issues must be linked to the [Hollow Knight Archipelago](https://github.com/users/Ijwu/projects/1/views/1) project board.
* All issues must be linked to a release milestone.
* All issues should be labeled appropriately.
* All issues should be in the correct Kanban column to describe their current status.
* If an issue is in `In Progress` status then it must have someone assigned to it.
* Pull Requests must have a linked issue which they close out.

Okay, if you read this far your eyes probably glazed over at the list above. It's... uh, just not very hard to track your work. Just refer to the list everytime you interact with the repository and go through the guidelines in order. It becomes second nature very quickly.

**If you're not going to do any of the above then I at least ask you to create an issue describing any work you intend to take on, at the minimum.** I can handle the rest.

## Development Setup
You will need to make a copy of the `Directory.Build.props.todo` file and rename the copy to `Directory.Build.props`. Then you'll want to alter the `Directory.Build.props` file you made to point to your local HK installation. It will be ignored by the .gitignore file as `Directory.Build.props`. Do not commit any changes to `Directory.Build.props.todo` to prevent leaking your paths to the world.

Post-build events will automatically package the mod for export **as well as install it in your HK installation.** When developing on the mod **do not install Archipelago through Scarab.** If Archipelago is installed through Scarab, uninstall or disable it before testing. Archipelago.HollowKnight will not load if both Scarab and development versions are installed at the same time.

