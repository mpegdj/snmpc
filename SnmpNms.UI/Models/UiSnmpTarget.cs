using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnmpNms.UI.Models;

public class UiSnmpTarget : ISnmpTarget, INotifyPropertyChanged
{
    private string _ipAddress = "127.0.0.1";
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (_ipAddress == value) return;
            _ipAddress = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndpointKey));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private int _port = 161;
    public int Port
    {
        get => _port;
        set
        {
            if (_port == value) return;
            _port = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndpointKey));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private string _alias = "";
    public string Alias
    {
        get => _alias;
        set
        {
            if (_alias == value) return;
            _alias = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private string _device = "";
    public string Device
    {
        get => _device;
        set
        {
            if (_device == value) return;
            _device = value ?? "";
            OnPropertyChanged();
        }
    }

    public string Community { get; set; } = "public";
    public SnmpVersion Version { get; set; } = SnmpVersion.V2c;
    public int Timeout { get; set; } = 3000;
    public int Retries { get; set; } = 1;
    public PollingProtocol PollingProtocol { get; set; } = PollingProtocol.SNMP;

    // Unique key for correlation (events/polling/status updates)
    public string EndpointKey => $"{IpAddress}:{Port}";

    // UI-friendly name (prefer alias if set, else endpoint)
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? EndpointKey : Alias;

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

