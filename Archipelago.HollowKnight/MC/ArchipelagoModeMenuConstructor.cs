using System;
using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using Modding;
using UnityEngine;

namespace Archipelago.HollowKnight.MC
{
    internal class ArchipelagoModeMenuConstructor : ModeMenuConstructor
    {
        MenuPage ApPage;
        private MenuLabel errorLabel;

        public override void OnEnterMainMenu(MenuPage modeMenu)
        {
            var perpetua = CanvasUtil.GetFont("Perpetua");
            
            ApPage = new MenuPage("Archipelago Settings", modeMenu);
            var settingsType = typeof(ConnectionDetails);
            var settings = Archipelago.Instance.ApSettings;

            var urlField = new EntryField<string>(ApPage, "Server URL: ");
            urlField.InputField.characterLimit = 500;
            var urlRect = urlField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            urlRect.sizeDelta = new Vector2(1500f, 63.2f);
            urlField.InputField.textComponent.font = perpetua;
            urlField.Bind(settings, settingsType.GetProperty("ServerUrl"));
            

            var portField = new NumericEntryField<int>(ApPage, "Server Port: ");
            portField.SetClamp(0, 65535);
            portField.InputField.textComponent.font = perpetua;
            portField.Bind(settings, settingsType.GetProperty("ServerPort"));

            var nameField = new EntryField<string>(ApPage, "Slot Name: ");
            nameField.InputField.characterLimit = 500;
            nameField.InputField.textComponent.font = perpetua;
            var nameRect = nameField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(1500f, 63.2f);
            nameField.Bind(settings, settingsType.GetProperty("SlotName"));
            

            var passwordField = new EntryField<string>(ApPage, "Password: ");
            passwordField.InputField.characterLimit = 500;
            passwordField.InputField.textComponent.font = perpetua;
            var passwordRect = passwordField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            passwordRect.sizeDelta = new Vector2(1500f, 63.2f);
            passwordField.Bind(settings, settingsType.GetProperty("ServerPassword"));
            

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
                // Archipelago.Instance.ConnectAndRandomize();
                Archipelago.Instance.StartOrResumeGame(true);
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
                Archipelago.Instance.DisconnectArchipelago();
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