using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Valdorso.UI
{
    /// <summary>
    /// Finestra di accesso: nome utente e password, oppure creazione di un nuovo account.
    /// Invio conferma, Tab passa al campo successivo, Esc chiude.
    /// Ricorda sul PC solo il nome utente, mai la password.
    /// </summary>
    public class LoginPanel : MonoBehaviour
    {
        const string LastUsernameKey = "valdorso.ultimoNomeUtente";

        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_InputField usernameField;
        [SerializeField] TMP_InputField passwordField;
        [SerializeField] TMP_InputField confirmField;
        [SerializeField] GameObject confirmGroup;
        [SerializeField] TMP_Text errorText;
        [SerializeField] Button submitButton;
        [SerializeField] TMP_Text submitLabel;
        [SerializeField] Button backButton;
        [SerializeField] Button switchModeButton;
        [SerializeField] TMP_Text switchModeLabel;

        bool createMode;
        bool listenersAdded;
        Action<string, string, bool> onSubmit;

        /// <summary>Apre la finestra; submit riceve nome utente, password e "crea account".</summary>
        public void Open(Action<string, string, bool> submit, string error = null)
        {
            onSubmit = submit;
            gameObject.SetActive(true);
            AddListeners();

            if (string.IsNullOrEmpty(usernameField.text))
                usernameField.text = PlayerPrefs.GetString(LastUsernameKey, string.Empty);
            passwordField.text = string.Empty;
            confirmField.text = string.Empty;

            SetMode(createMode);
            ShowError(error);
            Select(string.IsNullOrEmpty(usernameField.text) ? usernameField : passwordField);
        }

        public void Close()
        {
            if (passwordField != null) passwordField.text = string.Empty;
            if (confirmField != null) confirmField.text = string.Empty;
            gameObject.SetActive(false);
        }

        public void ShowError(string message)
        {
            if (errorText != null) errorText.text = message ?? string.Empty;
        }

        void AddListeners()
        {
            if (listenersAdded) return;
            listenersAdded = true;
            submitButton.onClick.AddListener(Submit);
            backButton.onClick.AddListener(Close);
            switchModeButton.onClick.AddListener(() => SetMode(!createMode));
        }

        void SetMode(bool create)
        {
            createMode = create;
            confirmGroup.SetActive(create);
            titleText.text = create ? "Crea un account" : "Accedi";
            submitLabel.text = create ? "Crea ed entra" : "Entra";
            switchModeLabel.text = create ? "Hai già un account? Accedi" : "Non hai un account? Creane uno";
            ShowError(null);
        }

        void Submit()
        {
            string username = usernameField.text.Trim();
            string password = passwordField.text;

            if (username.Length == 0)
            {
                ShowError("Scrivi il nome utente.");
                Select(usernameField);
                return;
            }
            if (password.Length == 0)
            {
                ShowError("Scrivi la password.");
                Select(passwordField);
                return;
            }
            if (createMode && password != confirmField.text)
            {
                ShowError("Le due password non coincidono.");
                Select(confirmField);
                return;
            }

            PlayerPrefs.SetString(LastUsernameKey, username);
            PlayerPrefs.Save();

            Action<string, string, bool> callback = onSubmit;
            bool create = createMode;
            Close();
            callback?.Invoke(username, password, create);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Submit();
            else if (keyboard.escapeKey.wasPressedThisFrame) Close();
            else if (keyboard.tabKey.wasPressedThisFrame) SelectNext(keyboard.shiftKey.isPressed);
        }

        void SelectNext(bool backwards)
        {
            var fields = new List<TMP_InputField> { usernameField, passwordField };
            if (createMode) fields.Add(confirmField);
            int current = fields.FindIndex(f => f.isFocused);
            int next = current < 0 ? 0 : (current + (backwards ? fields.Count - 1 : 1)) % fields.Count;
            Select(fields[next]);
        }

        static void Select(TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }
    }
}