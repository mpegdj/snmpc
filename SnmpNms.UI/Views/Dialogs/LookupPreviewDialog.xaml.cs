using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SnmpNms.UI.Views.Dialogs;

public partial class LookupPreviewDialog : Window, INotifyPropertyChanged
{
    public LookupPreviewDialog(string targetHost)
    {
        TargetHost = targetHost;
        DataContext = this;
        InitializeComponent();
    }

    public string TargetHost { get; }

    public string TitleText => $"Target: {TargetHost}";

    private string _proposedName = "";
    public string ProposedName
    {
        get => _proposedName;
        set { _proposedName = value; OnPropertyChanged(); }
    }

    private string _proposedDescription = "";
    public string ProposedDescription
    {
        get => _proposedDescription;
        set { _proposedDescription = value; OnPropertyChanged(); }
    }

    private string _logText = "";
    public string LogText
    {
        get => _logText;
        set { _logText = value; OnPropertyChanged(); }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


