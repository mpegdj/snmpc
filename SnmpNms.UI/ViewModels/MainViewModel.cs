using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<UiSnmpTarget> Devices { get; } = new();

    private UiSnmpTarget? _selectedDevice;
    public UiSnmpTarget? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (ReferenceEquals(_selectedDevice, value)) return;
            _selectedDevice = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


