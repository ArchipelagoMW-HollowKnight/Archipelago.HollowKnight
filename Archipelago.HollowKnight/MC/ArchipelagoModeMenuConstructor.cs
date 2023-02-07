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
        private MenuPage _apPage;

        private readonly static Type _settingsType = typeof(ConnectionDetails);
        private readonly static Font _perpetua = CanvasUtil.GetFont("Perpetua");

        public override void OnEnterMainMenu(MenuPage modeMenu)
        {
            _apPage = new MenuPage("Archipelago Settings", modeMenu);
            ConnectionDetails settings = Archipelago.Instance.MenuSettings;

            EntryField<string> urlField = CreateUrlField(_apPage, settings);
            NumericEntryField<int> portField = CreatePortField(_apPage, settings);
            EntryField<string> nameField = CreateSlotNameField(_apPage, settings);
            EntryField<string> passwordField = CreatePasswordField(_apPage, settings);

            MenuLabel errorLabel = new(_apPage, "");
            SmallButton startButton = new(_apPage, "Start");

            startButton.AddSetResumeKeyEvent("Archipelago");
            startButton.OnClick += () => StartOrResumeGame(true, errorLabel);

            urlField.SetNeighbor(Neighbor.Down, portField);
            portField.SetNeighbor(Neighbor.Down, nameField);
            nameField.SetNeighbor(Neighbor.Down, passwordField);
            passwordField.SetNeighbor(Neighbor.Down, startButton);
            startButton.SetNeighbor(Neighbor.Down, _apPage.backButton);
            _apPage.backButton.SetNeighbor(Neighbor.Up, startButton);

            var elements = new IMenuElement[]
            {
                urlField,
                portField,
                nameField,
                passwordField,
                startButton,
                errorLabel
            };
            new VerticalItemPanel(_apPage, new Vector2(0, 300), 100, false, elements);

            AttachResumePage();
        }

        private void AttachResumePage()
        {
            MenuPage resumePage = new("Archipelago Resume");
            ConnectionDetails settings = Archipelago.Instance.ApSettings;

            EntryField<string> urlField = CreateUrlField(resumePage, settings);
            NumericEntryField<int> portField = CreatePortField(resumePage, settings);
            EntryField<string> passwordField = CreatePasswordField(resumePage, settings);

            SmallButton resumeButton = new(resumePage, "Resume");
            MenuLabel errorLabel = new(resumePage, "");

            resumeButton.OnClick += () => StartOrResumeGame(false, errorLabel);

            resumePage.AddToNavigationControl(resumeButton);

            IMenuElement[] elements = new IMenuElement[]
            {
                urlField,
                portField,
                passwordField,
                errorLabel
            };

            new VerticalItemPanel(resumePage, new Vector2(0, 300), 100, true, elements);

            MenuChanger.ResumeMenu.AddResumePage("Archipelago", resumePage);
        }

        private static EntryField<string> CreatePasswordField(MenuPage apPage, ConnectionDetails settings)
        {
            var passwordField = new EntryField<string>(apPage, "Password: ");
            passwordField.InputField.characterLimit = 500;
            passwordField.InputField.textComponent.font = _perpetua;
            var passwordRect = passwordField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            passwordRect.sizeDelta = new Vector2(1500f, 63.2f);
            passwordField.Bind(settings, _settingsType.GetProperty("ServerPassword"));
            return passwordField;
        }

        private static EntryField<string> CreateSlotNameField(MenuPage apPage, ConnectionDetails settings)
        {
            var nameField = new EntryField<string>(apPage, "Slot Name: ");
            nameField.InputField.characterLimit = 500;
            nameField.InputField.textComponent.font = _perpetua;
            var nameRect = nameField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(1500f, 63.2f);
            nameField.Bind(settings, _settingsType.GetProperty("SlotName"));
            return nameField;
        }

        private static NumericEntryField<int> CreatePortField(MenuPage apPage, ConnectionDetails settings)
        {
            var portField = new NumericEntryField<int>(apPage, "Server Port: ");
            portField.SetClamp(0, 65535);
            portField.InputField.textComponent.font = _perpetua;
            portField.Bind(settings, _settingsType.GetProperty("ServerPort"));
            return portField;
        }

        private static EntryField<string> CreateUrlField(MenuPage apPage, ConnectionDetails settings)
        {
            var urlField = new EntryField<string>(apPage, "Server URL: ");
            urlField.InputField.characterLimit = 500;
            var urlRect = urlField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            urlRect.sizeDelta = new Vector2(1500f, 63.2f);
            urlField.InputField.textComponent.font = _perpetua;
            urlField.Bind(settings, _settingsType.GetProperty("ServerUrl"));
            return urlField;
        }

        private static void StartOrResumeGame(bool newGame, MenuLabel errorLabel)
        {
            Archipelago.Instance.ArchipelagoEnabled = true;

            // Cloning some settings onto others depending on what is taking precedence.
            // If it's a save slot we're resuming (newGame == false) then we want the slot settings to overwrite the global ones.
            if (newGame)
            {
                Archipelago.Instance.ApSettings = Archipelago.Instance.MenuSettings with { };  // Clone MenuSettings into ApSettings
            }
            else
            {
                Archipelago.Instance.MenuSettings = Archipelago.Instance.ApSettings with { };
            }
            try
            {
                Archipelago.Instance.StartOrResumeGame(newGame);
                MenuChangerMod.HideAllMenuPages();
                if (newGame)
                {
                    UIManager.instance.StartNewGame();
                }
                else
                {
                    UIManager.instance.ContinueGame();
                    GameManager.instance.ContinueGame();
                }
            }
            catch (ArchipelagoConnectionException ex)
            {
                errorLabel.Text.text = ex.Message;
            }
            catch (Exception ex)
            {
                errorLabel.Text.text = "An unknown error occurred when attempting to connect.";
                Archipelago.Instance.LogError(ex);
                Archipelago.Instance.DisconnectArchipelago();
            }
        }

        public override void OnExitMainMenu()
        {
            _apPage = null;
        }

        public override bool TryGetModeButton(MenuPage modeMenu, out BigButton button)
        {
            button = new BigButton(modeMenu, Archipelago.Sprite, "Archipelago");
            button.AddHideAndShowEvent(modeMenu, _apPage);
            return true;
        }
    }
}