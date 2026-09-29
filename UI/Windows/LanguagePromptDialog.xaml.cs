using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SmartNotes.Core.Native;

namespace SmartNotes.UI.Windows;

public partial class LanguagePromptDialog : Window
{
    private readonly CultureInfo _detectedCulture;

    public bool UserSwitchedLanguage { get; private set; } = false;
    public string SelectedLanguageCode { get; private set; } = "en";
    public string SelectedProofingTag { get; private set; } = "en-US";

    public LanguagePromptDialog(CultureInfo detectedCulture)
    {
        _detectedCulture = detectedCulture;
        InitializeComponent();

        Loaded += (s, e) =>
        {
            WindowBlurHelper.ApplyModernWindowStyles(this);
            ConfigureDialogContent();
        };

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                BtnSwitchLanguage_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                BtnKeepEnglish_Click(this, new RoutedEventArgs());
            }
        };
    }

    private void ConfigureDialogContent()
    {
        string twoLetter = _detectedCulture.TwoLetterISOLanguageName.ToLowerInvariant();
        string displayName = _detectedCulture.DisplayName;

        if (twoLetter == "de")
        {
            TxtHeadline.Text = "Sprache auswählen / Choose Language";
            TxtMessage.Text = "SmartNotes wurde auf Version 2.1 aktualisiert. Ihre Windows-Systemsprache wurde als Deutsch erkannt. Möchten Sie die Benutzeroberfläche und die Rechtschreibprüfung auf Deutsch umstellen oder bei Englisch (Standard) bleiben?";
            TxtDetectedLangTitle.Text = $"Deutsch ({_detectedCulture.NativeName})";
            TxtSwitchButtonLabel.Text = "Auf Deutsch umstellen";
        }
        else
        {
            TxtHeadline.Text = "Choose Your Preferred Language";
            TxtMessage.Text = $"SmartNotes detected your Windows system language as {displayName}. SmartNotes defaults to English, but you can choose to switch to your system language right away.";
            TxtDetectedLangTitle.Text = displayName;
            TxtSwitchButtonLabel.Text = $"Switch to {displayName}";
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void BtnSwitchLanguage_Click(object sender, RoutedEventArgs e)
    {
        UserSwitchedLanguage = true;
        SelectedLanguageCode = _detectedCulture.TwoLetterISOLanguageName.ToLowerInvariant();
        SelectedProofingTag = _detectedCulture.Name;
        DialogResult = true;
        Close();
    }

    private void BtnKeepEnglish_Click(object sender, RoutedEventArgs e)
    {
        UserSwitchedLanguage = false;
        SelectedLanguageCode = "en";
        SelectedProofingTag = "en-US";
        DialogResult = false;
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        BtnKeepEnglish_Click(sender, e);
    }
}
