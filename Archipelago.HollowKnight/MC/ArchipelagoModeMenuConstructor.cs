using System;
using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using UnityEngine;

namespace Archipelago.HollowKnight.MC
{
    internal class ArchipelagoModeMenuConstructor : ModeMenuConstructor
    {
        MenuPage ApPage;
        private MenuLabel errorLabel;

        public override void OnEnterMainMenu(MenuPage modeMenu)
        {
            ApPage = new MenuPage("Archipelago Settings", modeMenu);
            var settingsType = typeof(ConnectionDetails);
            var settings = Archipelago.Instance.ApSettings;

            var urlField = new EntryField<string>(ApPage, "Server URL: ");
            urlField.Bind(settings, settingsType.GetProperty("ServerUrl"));
            urlField.InputField.characterLimit = 500;

            var portField = new NumericEntryField<int>(ApPage, "Server Port: ");
            portField.SetClamp(0, 65535);
            portField.Bind(settings, settingsType.GetProperty("ServerPort"));

            var nameField = new EntryField<string>(ApPage, "Slot Name: ");
            nameField.Bind(settings, settingsType.GetProperty("SlotName"));
            nameField.InputField.characterLimit = 500;

            var passwordField = new EntryField<string>(ApPage, "Password: ");
            passwordField.Bind(settings, settingsType.GetProperty("ServerPassword"));
            passwordField.InputField.characterLimit = 500;

            var startButton = new BigButton(ApPage, "Start", "Will stall after clicking.");
            startButton.OnClick += StartNewGame;

            errorLabel = new MenuLabel(ApPage, "");

            urlField.SetNeighbor(Neighbor.Down, portField);
            portField.SetNeighbor(Neighbor.Down, nameField);
            nameField.SetNeighbor(Neighbor.Down, passwordField);
            passwordField.SetNeighbor(Neighbor.Down, startButton);
            startButton.SetNeighbor(Neighbor.Down, ApPage.backButton);
            ApPage.backButton.SetNeighbor(Neighbor.Up, startButton);

            var elements = new IMenuElement[]
            {
                urlField,
                portField,
                nameField,
                passwordField,
                startButton,
                errorLabel
            };
            new VerticalItemPanel(ApPage, new Vector2(0, 300), 100, false, elements);
        }

        private void StartNewGame()
        {
            Archipelago.Instance.ArchipelagoEnabled = true;
            try
            {
                Archipelago.Instance.ConnectAndRandomize();
                MenuChangerMod.HideAllMenuPages();
                UIManager.instance.StartNewGame();
            }
            catch (ArchipelagoConnectionException ex)
            {
                errorLabel.Text.text = ex.Message;
            }
            catch (Exception ex)
            {
                errorLabel.Text.text = "An error occurred when attempting to connect. Please report in Discord to @ijwu.";
                Archipelago.Instance.LogError(ex);
            }
            
        }

        public override void OnExitMainMenu()
        {
            ApPage = null;
        }

        public override bool TryGetModeButton(MenuPage modeMenu, out BigButton button)
        {
            button = new BigButton(modeMenu, Archipelago.Sprite, "Archipelago");
            button.AddHideAndShowEvent(modeMenu, ApPage);
            return true;
        }
    }
}