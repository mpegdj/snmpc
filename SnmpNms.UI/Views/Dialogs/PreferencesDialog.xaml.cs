using System.Windows;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Views.Dialogs;

public partial class PreferencesDialog : Window
{
    public AppPreferences Preferences { get; private set; }

    public PreferencesDialog(AppPreferences currentPreferences)
    {
        InitializeComponent();
        Preferences = currentPreferences;
        LoadPreferences();
    }

    private void LoadPreferences()
    {
        chkAutoLoadMap.IsChecked = Preferences.AutoLoadLastMap;
        chkAutoStartPolling.IsChecked = Preferences.AutoStartPolling;
        txtDefaultCommunity.Text = Preferences.DefaultCommunity ?? "public";
        txtDefaultTimeout.Text = Preferences.DefaultTimeout.ToString();
        txtPollingInterval.Text = Preferences.PollingInterval.ToString();
        txtTrapPort.Text = Preferences.TrapListenerPort.ToString();
        chkEnableLogSave.IsChecked = Preferences.EnableLogSave;
        txtMaxLogLines.Text = Preferences.MaxLogLines.ToString();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Preferences.AutoLoadLastMap = chkAutoLoadMap.IsChecked ?? true;
            Preferences.AutoStartPolling = chkAutoStartPolling.IsChecked ?? false;
            Preferences.DefaultCommunity = txtDefaultCommunity.Text;
            Preferences.DefaultTimeout = int.Parse(txtDefaultTimeout.Text);
            Preferences.PollingInterval = int.Parse(txtPollingInterval.Text);
            Preferences.TrapListenerPort = int.Parse(txtTrapPort.Text);
            Preferences.EnableLogSave = chkEnableLogSave.IsChecked ?? true;
            Preferences.MaxLogLines = int.Parse(txtMaxLogLines.Text);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Invalid input: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
