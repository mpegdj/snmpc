using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnmpNms.UI.Models;

public class UiSnmpTarget : ISnmpTarget, INotifyPropertyChanged
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 161;
    public string Community { get; set; } = "public";
    public SnmpVersion Version { get; set; } = SnmpVersion.V2c;
    public int Timeout { get; set; } = 3000;
    public int Retries { get; set; } = 1;

    public string DisplayName => $"{IpAddress}:{Port}";

    private DeviceStatus _status = DeviceStatus.Unknown;
    public DeviceStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

