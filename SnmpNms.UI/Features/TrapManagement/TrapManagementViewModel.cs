using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Features.TrapManagement;

public class TrapManagementViewModel : INotifyPropertyChanged
{
    private readonly ISnmpClient _snmpClient;
    private readonly MainViewModel _mainViewModel;

    public ObservableCollection<MapNode> Devices { get; } = new();

    private MapNode? _selectedDevice;
    public MapNode? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (_selectedDevice == value) return;
            _selectedDevice = value;
            OnPropertyChanged();
            _ = RefreshTrapTableAsync();
        }
    }

    public ObservableCollection<TrapSlotViewModel> TrapSlots { get; } = new();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "Select a device to manage trap destinations.";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public TrapManagementViewModel(ISnmpClient snmpClient, MainViewModel mainViewModel, IEnumerable<MapNode>? initialDevices = null)
    {
        _snmpClient = snmpClient;
        _mainViewModel = mainViewModel;
        
        if (initialDevices != null)
        {
            foreach (var node in initialDevices) Devices.Add(node);
        }
        else
        {
            SyncInitialDevices();
            // 실시간 동기화: 전체 리프레시 대신 변경된 항목만 반영
            _mainViewModel.DeviceNodes.CollectionChanged += (s, e) => {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => {
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
                    {
                        foreach (MapNode node in e.NewItems) 
                            if (!Devices.Contains(node)) Devices.Add(node);
                    }
                    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
                    {
                        foreach (MapNode node in e.OldItems) Devices.Remove(node);
                    }
                    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                    {
                        Devices.Clear();
                        SyncInitialDevices();
                    }
                    
                    if (Devices.Count > 0 && SelectedDevice == null) SelectedDevice = Devices[0];
                });
            };
        }
        
        if (Devices.Count > 0 && SelectedDevice == null)
        {
            SelectedDevice = Devices[0];
        }
    }

    private void SyncInitialDevices()
    {
        foreach (var node in _mainViewModel.DeviceNodes)
        {
            if (!Devices.Contains(node)) Devices.Add(node);
        }
    }

    // 기존의 무거운 RefreshDevices 제거 (필요시 수동 호출용으로만 남김)
    public void RefreshDevices()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => {
            var current = SelectedDevice;
            Devices.Clear();
            SyncInitialDevices();
            if (current != null && Devices.Contains(current)) SelectedDevice = current;
        });
    }

    public async Task RefreshTrapTableAsync()
    {
        if (SelectedDevice?.Target == null) return;

        TrapSlots.Clear();
        StatusMessage = $"Fetching trap table for {SelectedDevice.Target.IpAddress}...";

        IsBusy = true;
        try
        {
            var target = SelectedDevice.Target;
            // NTT MVE5000: 1.3.6.1.4.1.3930.36.5.2.11.1.5
            // NTT MVD5000: 1.3.6.1.4.1.3930.35.5.2.11.1.5
            // Determine base OID by checking SysObjectId or trying to guess
            string nttBaseOid = "1.3.6.1.4.1.3930.36.5.2.11.1"; 
            if (SelectedDevice.Target.SysObjectId.Contains(".35.")) 
                nttBaseOid = "1.3.6.1.4.1.3930.35.5.2.11.1";

            var ipColumnOid = $"{nttBaseOid}.5"; 
            
            // 조회: 기본 커뮤니티(public) 사용, 타임아웃만 5초로 연장
            var readTarget = new UiSnmpTarget 
            {
                IpAddress = target.IpAddress,
                Port = target.Port,
                Version = target.Version,
                Community = target.Community,
                Timeout = 5000, 
                Retries = 1
            };

            var result = await _snmpClient.WalkAsync(readTarget, ipColumnOid);

            // 1차 통신 자체가 실패했을 때만 'private' 커뮤니티로 재시도 (결과 0건은 정상이므로 제외)
            if (!result.IsSuccess)
            {
                StatusMessage = $"[Retry] 'public' failed. Retrying with 'private'...";
                readTarget.Community = "private";
                result = await _snmpClient.WalkAsync(readTarget, ipColumnOid);
            }

            if (!result.IsSuccess)
            {
                // Try MVD if MVE failed
                if (nttBaseOid.Contains(".36."))
                {
                    nttBaseOid = "1.3.6.1.4.1.3930.35.5.2.11.1";
                    ipColumnOid = $"{nttBaseOid}.5";
                    result = await _snmpClient.WalkAsync(readTarget, ipColumnOid);
                }
            }

            if (result.IsSuccess)
            {
                // 변수를 인덱스별로 매핑 (Oid 끝자리 .1, .2 ... .8)
                var varMap = result.Variables.ToDictionary(
                    v => v.Oid.Substring(v.Oid.LastIndexOf('.') + 1), 
                    v => v.Value);

                for (int i = 1; i <= 8; i++)
                {
                    var slotIp = varMap.TryGetValue(i.ToString(), out var val) ? val : "0.0.0.0";
                    TrapSlots.Add(new TrapSlotViewModel 
                    { 
                        No = i, 
                        DestinationIp = slotIp,
                        Status = (slotIp == "0.0.0.0" || slotIp == "0") ? "Empty" : "Configured"
                    });
                }
                StatusMessage = $"[Success] Trap table loaded for {target.IpAddress}.";
            }
            else
            {
                StatusMessage = $"[Error] Failed to load table. Device may not be MVE/MVD series or SNMP error: {result.ErrorMessage}";
                for (int i = 1; i <= 8; i++) TrapSlots.Add(new TrapSlotViewModel { No = i, Status = "Unknown" });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RegisterNmsAsync()
    {
        if (SelectedDevice?.Target == null) return;

        var nmsIp = GetLocalIp();
        if (string.IsNullOrEmpty(nmsIp))
        {
            StatusMessage = "Cannot determine local IP address.";
            return;
        }

        StatusMessage = $"Registering {nmsIp} to {SelectedDevice.Target.IpAddress}...";

        IsBusy = true;
        try
        {
            var target = SelectedDevice.Target;
            string nttBaseOid = "1.3.6.1.4.1.3930.36.5.2.11.1"; 
            if (SelectedDevice.Target.SysObjectId.Contains(".35.")) 
                nttBaseOid = "1.3.6.1.4.1.3930.35.5.2.11.1";

            // 1. 빈 슬롯 찾기 (0.0.0.0)
            int targetIdx = -1;
            foreach (var slot in TrapSlots)
            {
                if (slot.DestinationIp == "0.0.0.0" || slot.DestinationIp == "0" || string.IsNullOrEmpty(slot.DestinationIp))
                {
                    targetIdx = slot.No;
                    break;
                }
            }

            if (targetIdx == -1)
            {
                StatusMessage = "Warning: All slots are full. Overwriting Slot 1.";
                targetIdx = 1;
            }

            // 쓰기 전용 타겟 생성 (타임아웃 5초)
            var writeTarget = new UiSnmpTarget 
            {
                IpAddress = target.IpAddress,
                Port = target.Port,
                Version = target.Version,
                Community = target.Community,
                Timeout = 5000,
                Retries = 1
            };

            StatusMessage = $"[Progress 1/3] Setting Trap IP {nmsIp} to slot {targetIdx}...";
            var ipRes = await _snmpClient.SetAsync(writeTarget, $"{nttBaseOid}.5.{targetIdx}", nmsIp, "IPADDRESS");
            
            // 실패 시 'private' 으로 재시도
            if (!ipRes.IsSuccess) 
            { 
                 StatusMessage = $"[Retry] First attempt failed. Retrying with community 'private'...";
                 writeTarget.Community = "private";
                 ipRes = await _snmpClient.SetAsync(writeTarget, $"{nttBaseOid}.5.{targetIdx}", nmsIp, "IPADDRESS");
            }

            if (!ipRes.IsSuccess) { StatusMessage = $"[Fail] IP Set failed: {ipRes.ErrorMessage}"; return; }

            StatusMessage = $"[Progress 2/3] Setting Community to 'public' for slot {targetIdx}...";
            var comRes = await _snmpClient.SetAsync(writeTarget, $"{nttBaseOid}.3.{targetIdx}", "public", "OCTETSTRING");
            if (!comRes.IsSuccess) { StatusMessage = $"[Fail] Community failed: {comRes.ErrorMessage}"; return; }

            StatusMessage = $"[Progress 3/3] Enabling trap slot {targetIdx}...";
            // NTT MVE5000 Enable OID: .2.x (0 = enabled)
            // Note: If this fails, the trap might still be sent if IP is set, but better to ensure.
            var enRes = await _snmpClient.SetAsync(writeTarget, $"{nttBaseOid}.2.{targetIdx}", "0", "INTEGER");
            if (!enRes.IsSuccess) { StatusMessage = $"[Fail] Enable failed: {enRes.ErrorMessage}"; return; }

            StatusMessage = $"[Success] Registered {nmsIp} to Slot {targetIdx} successfully. Refreshing table...";
            
            // 장비가 설정을 반영할 시간을 충분히 줌
            await Task.Delay(1500); 
            
            // 테이블 갱신
            await RefreshTrapTableAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"[Critical] Registration error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? GetLocalIp() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
        .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class TrapSlotViewModel : INotifyPropertyChanged
{
    public int No { get; set; }
    private string _destinationIp = "0.0.0.0";
    public string DestinationIp
    {
        get => _destinationIp;
        set { _destinationIp = value; OnPropertyChanged(); }
    }
    private string _status = "Unknown";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
