using MenuChanger;
using MenuChanger.Extensions;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using Modding;
using System;
using UnityEngine;

namespace Archipelago.HollowKnight.MC
{
    internal class ArchipelagoModeMenuConstructor : ModeMenuConstructor
    {
        private MenuPage modeConfigPage;

        private readonly static Type _settingsType = typeof(ConnectionDetails);
        private readonly static Font _perpetua = CanvasUtil.GetFont("Perpetua");

        public override void OnEnterMainMenu(MenuPage modeMenu)
        {
            modeConfigPage = new MenuPage("Archipelago Settings", modeMenu);
            ConnectionDetails settings = Archipelago.Instance.GS.MenuConnectionDetails;

            EntryField<string> urlField = CreateUrlField(modeConfigPage, settings);
            NumericEntryField<int> portField = CreatePortField(modeConfigPage, settings);
            EntryField<string> nameField = CreateSlotNameField(modeConfigPage, settings);
            EntryField<string> passwordField = CreatePasswordField(modeConfigPage, settings);

            MenuLabel errorLabel = new(modeConfigPage, "");
            BigButton startButton = new(modeConfigPage, "Start", "May stall after clicking");

            startButton.AddSetResumeKeyEvent("Archipelago");
            startButton.OnClick += () => StartOrResumeGame(true, errorLabel);

            modeConfigPage.AfterHide += () => errorLabel.Text.text = "";

            IMenuElement[] elements =
            [
                urlField,
                portField,
                nameField,
                passwordField,
                startButton,
                errorLabel
            ];
            VerticalItemPanel vip = new(modeConfigPage, SpaceParameters.TOP_CENTER_UNDER_TITLE, 100, false, elements);
            modeConfigPage.AddToNavigationControl(vip);

            AttachResumePage();
        }

        private void AttachResumePage()
        {
            MenuPage resumePage = new("Archipelago Resume");

            MenuLabel slotName = new(resumePage, "");

            EntryField<string> urlField = CreateUrlField(resumePage, null);
            NumericEntryField<int> portField = CreatePortField(resumePage, null);
            EntryField<string> passwordField = CreatePasswordField(resumePage, null);

            SmallButton resumeButton = new(resumePage, "Resume");
            MenuLabel errorLabel = new(resumePage, "");

            void RebindSettings()
            {
                ConnectionDetails settings = Archipelago.Instance.LS.ConnectionDetails;
                if (settings != null)
                {
                    slotName.Text.text = $"Slot Name: {settings.SlotName}";
                    urlField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.ServerUrl)));
                    portField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.ServerPort)));
                    passwordField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.ServerPassword)));
                }
                else
                {
                    slotName.Text.text = "Incompatible save file";
                    errorLabel.Text.text = "To resume, recreate your save or downgrade to an older client version.";
                }
            }

            resumeButton.OnClick += () => StartOrResumeGame(false, errorLabel);

            resumePage.BeforeShow += RebindSettings;
            resumePage.AfterHide += () => errorLabel.Text.text = "";

            IMenuElement[] elements =
            [
                slotName,
                urlField,
                portField,
                passwordField,
                resumeButton,
                errorLabel
            ];

            VerticalItemPanel vip = new(resumePage, SpaceParameters.TOP_CENTER_UNDER_TITLE, 100, true, elements);
            resumePage.AddToNavigationControl(vip);

            ResumeMenu.AddResumePage("Archipelago", resumePage);
        }

        private static EntryField<string> CreateUrlField(MenuPage apPage, ConnectionDetails settings)
        {
            EntryField<string> urlField = new(apPage, "Server URL: ");
            urlField.InputField.characterLimit = 500;
            RectTransform urlRect = urlField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            urlRect.sizeDelta = new Vector2(1500f, 63.2f);
            urlField.InputField.textComponent.font = _perpetua;
            if (settings != null)
            {
                urlField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.ServerUrl)));
            }
            return urlField;
        }

        private static NumericEntryField<int> CreatePortField(MenuPage apPage, ConnectionDetails settings)
        {
            NumericEntryField<int> portField = new(apPage, "Server Port: ");
            portField.SetClamp(0, 65535);
            portField.InputField.textComponent.font = _perpetua;
            if (settings != null)
            {
                portField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.ServerPort)));
            }
            return portField;
        }

        private static EntryField<string> CreateSlotNameField(MenuPage apPage, ConnectionDetails settings)
        {
            EntryField<string> nameField = new(apPage, "Slot Name: ");
            nameField.InputField.characterLimit = 500;
            nameField.InputField.textComponent.font = _perpetua;
            RectTransform nameRect = nameField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(1500f, 63.2f);
            if (settings != null)
            {
                nameField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.SlotName)));
            }
            return nameField;
        }

        private static EntryField<string> CreatePasswordField(MenuPage apPage, ConnectionDetails settings)
        {
            EntryField<string> passwordField = new(apPage, "Password: ");
            passwordField.InputField.characterLimit = 500;
            passwordField.InputField.textComponent.font = _perpetua;
            RectTransform passwordRect = passwordField.InputField.gameObject.transform.Find("Text").GetComponent<RectTransform>();
            passwordRect.sizeDelta = new Vector2(1500f, 63.2f);
            if (settings != null)
            {
                passwordField.Bind(settings, _settingsType.GetProperty(nameof(ConnectionDetails.ServerPassword)));
            }
            return passwordField;
        }

        private static void StartOrResumeGame(bool newGame, MenuLabel errorLabel)
        {
            Archipelago.Instance.ArchipelagoEnabled = true;

            // Cloning some settings onto others depending on what is taking precedence.
            // If it's a save slot we're resuming (newGame == false) then we want the slot settings to overwrite the global ones.
            if (newGame)
            {
                Archipelago.Instance.LS = new APLocalSettings()
                {
                    ConnectionDetails = Archipelago.Instance.GS.MenuConnectionDetails with { },
                    ItemIndex = 0
                };
            }
            else if (Archipelago.Instance.LS.ConnectionDetails != null)
            {
                Archipelago.Instance.GS.MenuConnectionDetails = Archipelago.Instance.LS.ConnectionDetails with { };
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
            catch (LoginValidationException ex)
            {
                Archipelago.Instance.DisconnectArchipelago();
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
            modeConfigPage = null;
        }

        public override bool TryGetModeButton(MenuPage modeMenu, out BigButton button)
        {
            button = new BigButton(modeMenu, Archipelago.Instance.spriteManager.GetSprite("IconColorBig"), "Archipelago");
            button.AddHideAndShowEvent(modeMenu, modeConfigPage);
            return true;
        }
    }
}