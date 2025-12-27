using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Views.Dialogs;

public partial class DiscoveryProgressDialog : Window
{
    public class DiscoveredDevice : INotifyPropertyChanged
    {
        private bool _isSelected = false;  // 기본값: uncheck
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private bool _isSnmpSupported = false;
        public bool IsSnmpSupported
        {
            get => _isSnmpSupported;
            set 
            { 
                _isSnmpSupported = value;
                OnPropertyChanged();
            }
        }

        public string IpAddress { get; set; } = "";
        public string Status { get; set; } = "Unknown";
        public string Community { get; set; } = "public";
        public string Version { get; set; } = "V2c";
        public int Port { get; set; } = 161;
        public string Maker { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string SubnetName { get; set; } = "";  // 서브넷 이름 (예: "192.168.0.0/24")

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    private readonly ISnmpClient _snmpClient;
    private readonly ITrapListener? _trapListener;
    private readonly DiscoveryPollingAgentsDialog.DiscoveryConfig _config;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = new();
    public ObservableCollection<string> AvailableMakers { get; } = new();
    public ObservableCollection<string> AvailableDevices { get; } = new();
    public ObservableCollection<SubnetGroup> SubnetGroups { get; } = new();
    public bool ConfigureTrapDestination { get; set; } = false;
    public bool ConfigureStandardNttTrap { get; set; } = false;
    public bool AddDevicesWithoutSubnet { get; set; } = true;  // 서브넷 없이 기기만 추가 (기본값: 체크)

    public class SubnetGroup : INotifyPropertyChanged
    {
        private bool _isSelected = false;  // 기본값: uncheck
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string SubnetName { get; set; } = "";
        public ObservableCollection<DiscoveredDevice> Devices { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public DiscoveryProgressDialog(ISnmpClient snmpClient, DiscoveryPollingAgentsDialog.DiscoveryConfig config, ITrapListener? trapListener = null)
    {
        InitializeComponent();
        _snmpClient = snmpClient;
        _trapListener = trapListener;
        _config = config;
        DataContext = this;
        dataGridDevices.ItemsSource = DiscoveredDevices;
        
        // DiscoveredDevices 변경 시 드롭다운 업데이트
        DiscoveredDevices.CollectionChanged += (s, e) =>
        {
            UpdateFilterDropdowns();
        };
        
        // Trap Listener가 없거나 실행 중이 아니면 체크박스 비활성화
        if (_trapListener == null || !_trapListener.IsListening)
        {
            chkConfigureTrap.IsEnabled = false;
            chkConfigureTrap.ToolTip = "Trap Listener must be running to configure Trap Destination";
        }
        
        // Discovery 시작
        _ = StartDiscoveryAsync();
    }

    private async Task StartDiscoveryAsync()
    {
        btnStop.IsEnabled = true;
        btnOk.IsEnabled = false;
        btnCancel.IsEnabled = true;

        try
        {
            AddLog("=== Discovery Started ===");
            AddLog($"Seeds: {_config.Seeds.Count}");
            AddLog($"Communities: {_config.Communities.Count}");
            AddLog($"Filters: {_config.Filters.Count}");
            AddLog("");

            var discoveredIps = new HashSet<string>();
            var discoveredIpsLock = new object();

            // 필터 타입 확인 (Maker/DeviceName 필터가 있으면 필터링을 위해 SNMP 값 필요)
            var hasMakerFilter = _config.Filters.Any(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.Maker);
            var hasDeviceNameFilter = _config.Filters.Any(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.DeviceName);
            var needsFilterCheck = hasMakerFilter || hasDeviceNameFilter; // Maker/DeviceName 필터가 있으면 필터링 필요
            // SNMP는 항상 검사 (Address 필터만 있어도 SNMP 검사 필요)

            // Seeds에서 네트워크 범위 생성 (Seed IP와 Netmask로 서브넷 범위 계산)
            foreach (var seed in _config.Seeds)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                AddLog($"Scanning network: {seed.IpAddr}/{seed.Netmask}");
                var ipRange = GenerateIpRange(seed.IpAddr, seed.Netmask);
                AddLog($"  Generated {ipRange.Count} IP addresses");

                // Address 필터 미리 적용
                var addressFilters = _config.Filters.Where(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.Address).ToList();
                var filteredIps = ipRange.Where(ip =>
                {
                    if (addressFilters.Count == 0) return true;
                    
                    bool addressMatch = false;
                    foreach (var filter in addressFilters)
                    {
                        if (filter.Type == "Include" && MatchesAddressPattern(ip, filter.Range))
                        {
                            addressMatch = true;
                            break;
                        }
                        if (filter.Type == "Exclude" && MatchesAddressPattern(ip, filter.Range))
                        {
                            return false;
                        }
                    }
                    return addressMatch;
                }).ToList();

                AddLog($"  After address filter: {filteredIps.Count} IP addresses");

                // 병렬 처리로 Discovery 수행 (최대 50개 동시 실행)
                var semaphore = new SemaphoreSlim(50);
                var tasks = filteredIps.Select(async ip =>
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested) return;
                    
                    await semaphore.WaitAsync(_cancellationTokenSource.Token);
                    try
                    {
                        lock (discoveredIpsLock)
                        {
                            if (discoveredIps.Contains(ip)) return;
                            discoveredIps.Add(ip);
                        }

                        await DiscoverDeviceAsync(ip, _config, needsFilterCheck);
                        UpdateProgress();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                AddLog("");
                AddLog($"=== Discovery Complete ===");
                AddLog($"Total devices found: {DiscoveredDevices.Count}");
                txtStatus.Text = "Discovery completed";
            }
            else
            {
                AddLog("");
                AddLog("=== Discovery Stopped ===");
                txtStatus.Text = "Discovery stopped by user";
            }
        }
        catch (Exception ex)
        {
            AddLog($"ERROR: {ex.Message}");
            txtStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            btnStop.IsEnabled = false;
            btnOk.IsEnabled = true;
            btnCancel.IsEnabled = true;
        }
    }

    /// <summary>
    /// Seed IP와 Netmask를 사용하여 서브넷 범위 내의 모든 호스트 IP 주소를 생성합니다.
    /// Seed IP는 네트워크 내의 어떤 IP든 될 수 있으며, Netmask와 AND 연산하여 네트워크 주소를 계산합니다.
    /// </summary>
    private List<string> GenerateIpRange(string ipAddr, string netmask)
    {
        var ips = new List<string>();
        
        try
        {
            var ip = System.Net.IPAddress.Parse(ipAddr);
            var mask = System.Net.IPAddress.Parse(netmask);
            
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return ips;
            
            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            
            // Seed IP와 Netmask를 AND 연산하여 네트워크 주소 계산
            // 예: 192.168.0.100 & 255.255.255.0 = 192.168.0.0
            var networkBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }
            
            // 호스트 부분이 시작되는 바이트 위치 찾기
            int hostStartByte = 3;
            for (int i = 0; i < 4; i++)
            {
                if (maskBytes[i] != 0xFF)
                {
                    hostStartByte = i;
                    break;
                }
            }
            
            // 전체 마스크를 고려하여 호스트 비트 수 계산
            int totalHostBits = 0;
            
            for (int i = hostStartByte; i < 4; i++)
            {
                byte maskByte = maskBytes[i];
                if (maskByte == 0)
                {
                    // 완전히 호스트 부분인 바이트
                    totalHostBits += 8;
                }
                else if (maskByte != 0xFF)
                {
                    // 부분적으로 호스트 부분인 바이트
                    for (int bit = 7; bit >= 0; bit--)
                    {
                        if ((maskByte & (1 << bit)) == 0)
                        {
                            totalHostBits++;
                        }
                    }
                    // 이 바이트 이후의 모든 바이트는 호스트 부분
                    for (int j = i + 1; j < 4; j++)
                    {
                        totalHostBits += 8;
                    }
                    break;
                }
            }
            
            // 호스트 개수 계산 (2^hostBits)
            // totalHostBits가 0이면 기본값 256 사용
            uint hostCount = totalHostBits > 0 ? (uint)(1 << totalHostBits) : 256;
            
            // 너무 많은 IP는 제한 (최대 512개)
            if (hostCount > 512) hostCount = 512;
            
            // IP 범위 생성 (1부터 hostCount-2까지, 네트워크 주소와 브로드캐스트 제외)
            for (uint hostNum = 1; hostNum < hostCount - 1; hostNum++)
            {
                var finalBytes = new byte[4];
                Array.Copy(networkBytes, finalBytes, 4);
                
                // 호스트 번호를 네트워크 주소에 더하기
                // 호스트 번호를 적절한 바이트에 더함 (마지막 바이트부터)
                int byteIndex = 3;
                uint num = hostNum;
                while (byteIndex >= hostStartByte && num > 0)
                {
                    uint sum = (uint)finalBytes[byteIndex] + num;
                    finalBytes[byteIndex] = (byte)(sum & 0xFF);
                    num = sum >> 8;
                    byteIndex--;
                }
                
                ips.Add(new System.Net.IPAddress(finalBytes).ToString());
            }
        }
        catch
        {
            // 파싱 실패 시 원본 IP만 반환
            ips.Add(ipAddr);
        }
        
        return ips;
    }


    private bool MatchesAddressPattern(string ip, string pattern)
    {
        // 와일드카드 패턴 매칭 (예: 198.*.*.22-88)
        var regexPattern = pattern
            .Replace(".", "\\.")
            .Replace("*", "\\d+")
            .Replace("-", "|");
        
        // 범위 처리 (예: 22-88 -> 22|23|24|...|88)
        if (pattern.Contains("-"))
        {
            var parts = pattern.Split('.');
            var lastPart = parts[parts.Length - 1];
            if (lastPart.Contains("-"))
            {
                var rangeParts = lastPart.Split('-');
                if (int.TryParse(rangeParts[0], out var start) && int.TryParse(rangeParts[1], out var end))
                {
                    var rangePattern = string.Join("|", Enumerable.Range(start, end - start + 1));
                    regexPattern = regexPattern.Replace(lastPart.Replace("-", "|"), rangePattern);
                }
            }
        }
        
        return Regex.IsMatch(ip, $"^{regexPattern}$");
    }

    private bool MatchesNamePattern(string name, string pattern)
    {
        // 와일드카드 패턴 매칭 (대소문자 무시)
        // 예: "ntt*" -> "ntt.*" (대소문자 무시)
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";
        
        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }

    private async Task DiscoverDeviceAsync(string ip, DiscoveryPollingAgentsDialog.DiscoveryConfig config, bool needsFilterCheck = false)
    {
        // Ping 확인
        if (config.FindNonSnmpNodes)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 1000);
                if (reply.Status != IPStatus.Success)
                {
                    AddLog($"  {ip}: Ping failed");
                    return;
                }
            }
            catch
            {
                AddLog($"  {ip}: Ping error");
                return;
            }
        }

        // SNMP는 항상 검사 (필터가 없어도 SNMP 검사 필요)
        foreach (var comm in config.Communities)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested) break;

            try
            {
                var version = comm.Version == "V1" ? SnmpVersion.V1 : SnmpVersion.V2c;
                var target = new UiSnmpTarget
                {
                    IpAddress = ip,
                    Port = 161,
                    Community = comm.ReadCommunity,
                    Version = version,
                    Timeout = 2000,
                    Retries = 1
                };

                // sysDescr (Maker), sysObjectID, sysName (Device Name)으로 SNMP 확인
                var oids = new[] { "1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.2.0", "1.3.6.1.2.1.1.5.0" };
                var result = await _snmpClient.GetAsync(target, oids);
                
                if (result.IsSuccess && result.Variables.Count > 0)
                {
                    string? sysDescr = null;
                    string? sysObjectId = null;
                    string? sysName = null;
                    
                    foreach (var v in result.Variables)
                    {
                        if (v.Oid == "1.3.6.1.2.1.1.1.0") sysDescr = v.Value;
                        else if (v.Oid == "1.3.6.1.2.1.1.2.0") sysObjectId = v.Value;
                        else if (v.Oid == "1.3.6.1.2.1.1.5.0") sysName = v.Value;
                    }
                    
                    // 메이커 정보 추출
                    string maker = ExtractMaker(sysObjectId, sysDescr);
                    
                    // Maker 필터 확인 (추출된 메이커 정보 사용)
                    var makerFilters = config.Filters.Where(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.Maker).ToList();
                    if (makerFilters.Count > 0)
                    {
                        bool makerMatch = false;
                        foreach (var filter in makerFilters)
                        {
                            if (filter.Type == "Include" && MatchesNamePattern(maker, filter.Range))
                            {
                                makerMatch = true;
                                break;
                            }
                            if (filter.Type == "Exclude" && MatchesNamePattern(maker, filter.Range))
                            {
                                return; // Exclude는 제외
                            }
                        }
                        if (!makerMatch) return; // Maker 필터에 매칭되지 않으면 제외
                    }
                    
                    // Device Name 필터 확인 (sysName 사용)
                    var deviceNameFilters = config.Filters.Where(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.DeviceName).ToList();
                    if (deviceNameFilters.Count > 0)
                    {
                        var deviceName = sysName ?? "";
                        bool deviceNameMatch = false;
                        foreach (var filter in deviceNameFilters)
                        {
                            if (filter.Type == "Include" && MatchesNamePattern(deviceName, filter.Range))
                            {
                                deviceNameMatch = true;
                                break;
                            }
                            if (filter.Type == "Exclude" && MatchesNamePattern(deviceName, filter.Range))
                            {
                                return; // Exclude는 제외
                            }
                        }
                        if (!deviceNameMatch) return; // Device Name 필터에 매칭되지 않으면 제외
                    }
                    
                    // 서브넷 이름 계산
                    string subnetName = CalculateSubnetName(ip);
                    
                    var device = new DiscoveredDevice
                    {
                        IpAddress = ip,
                        Status = "SNMP OK",
                        Community = comm.ReadCommunity,
                        Version = comm.Version,
                        Port = 161,
                        IsSnmpSupported = true,  // SNMP 지원 기기
                        Maker = maker,
                        DeviceName = sysName ?? "",
                        SubnetName = subnetName
                    };
                    
                    DiscoveredDevices.Add(device);
                    
                    // 메이커 및 디바이스 목록 업데이트, 서브넷 그룹 업데이트
                    Dispatcher.Invoke(() =>
                    {
                        if (!AvailableMakers.Contains(maker))
                        {
                            AvailableMakers.Add(maker);
                        }
                        if (!string.IsNullOrWhiteSpace(sysName) && !AvailableDevices.Contains(sysName))
                        {
                            AvailableDevices.Add(sysName);
                        }
                        UpdateSubnetGroups();
                        UpdateFilterDropdowns();
                    });
                    
                    var makerInfo = !string.IsNullOrWhiteSpace(maker) ? $" Maker: {maker}" : "";
                    var nameInfo = !string.IsNullOrWhiteSpace(sysName) ? $" Name: {sysName}" : "";
                    AddLog($"  {ip}: Found (SNMP {comm.Version}, Community: {comm.ReadCommunity}){makerInfo}{nameInfo}");
                    return; // 첫 번째 성공한 커뮤니티 사용
                }
            }
            catch
            {
                // 무시하고 다음 커뮤니티 시도
            }
        }

        // SNMP 실패했지만 Ping 성공이면 Non-SNMP 노드로 추가
        if (config.FindNonSnmpNodes)
        {
            // 서브넷 이름 계산
            string subnetName = CalculateSubnetName(ip);
            
            var device = new DiscoveredDevice
            {
                IpAddress = ip,
                Status = "Ping Only",
                Community = "N/A",
                Version = "N/A",
                Port = 161,
                IsSnmpSupported = false,
                Maker = "Unknown",
                DeviceName = "",
                SubnetName = subnetName
            };
            DiscoveredDevices.Add(device);
            
            // 서브넷 그룹 업데이트
            Dispatcher.Invoke(() =>
            {
                UpdateSubnetGroups();
            });
            
            AddLog($"  {ip}: Found (Ping only, no SNMP)");
        }
    }

    private void AddLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            txtLog.Text += $"[{timestamp}] {message}\n";
            
            // 스크롤을 맨 아래로
            if (txtLog.Parent is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToEnd();
            }
        });
    }

    private void UpdateProgress()
    {
        Dispatcher.Invoke(() =>
        {
            txtProgress.Text = $"{DiscoveredDevices.Count} devices found";
        });
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource.Cancel();
        AddLog("Stopping discovery...");
    }

    private void BtnAllToggle_Click(object sender, RoutedEventArgs e)
    {
        // 모든 기기 토글
        var allDevices = DiscoveredDevices.ToList();
        if (allDevices.Count == 0) return;

        bool allSelected = allDevices.All(d => d.IsSelected);
        
        foreach (var device in allDevices)
        {
            device.IsSelected = !allSelected;
        }
    }

    private void BtnSnmpToggle_Click(object sender, RoutedEventArgs e)
    {
        // SNMP 지원 기기만 토글
        var snmpDevices = DiscoveredDevices.Where(d => d.IsSnmpSupported).ToList();
        if (snmpDevices.Count == 0) return;

        bool allSelected = snmpDevices.All(d => d.IsSelected);
        
        foreach (var device in snmpDevices)
        {
            device.IsSelected = !allSelected;
        }
    }

    private void BtnPingToggle_Click(object sender, RoutedEventArgs e)
    {
        // Ping Only 기기만 토글
        var pingDevices = DiscoveredDevices.Where(d => d.Status == "Ping Only").ToList();
        if (pingDevices.Count == 0) return;

        bool allSelected = pingDevices.All(d => d.IsSelected);
        
        foreach (var device in pingDevices)
        {
            device.IsSelected = !allSelected;
        }
    }

    private void BtnV1Toggle_Click(object sender, RoutedEventArgs e)
    {
        // SNMP V1 기기만 토글
        var v1Devices = DiscoveredDevices.Where(d => d.Version == "V1").ToList();
        if (v1Devices.Count == 0) return;

        bool allSelected = v1Devices.All(d => d.IsSelected);
        
        foreach (var device in v1Devices)
        {
            device.IsSelected = !allSelected;
        }
    }

    private void BtnV2Toggle_Click(object sender, RoutedEventArgs e)
    {
        // SNMP V2c 기기만 토글
        var v2Devices = DiscoveredDevices.Where(d => d.Version == "V2c").ToList();
        if (v2Devices.Count == 0) return;

        bool allSelected = v2Devices.All(d => d.IsSelected);
        
        foreach (var device in v2Devices)
        {
            device.IsSelected = !allSelected;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // Trap 설정은 DiscoveryPollingAgentsDialog에서 Map 등록 시 함께 수행
        // 여기서는 다이얼로그만 닫고 ConfigureTrapDestination 플래그는 유지
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// sysObjectID와 sysDescr에서 메이커 정보를 추출합니다.
    /// </summary>
    private string ExtractMaker(string? sysObjectId, string? sysDescr)
    {
        // sysObjectID에서 메이커 추출 (더 정확함)
        if (!string.IsNullOrWhiteSpace(sysObjectId))
        {
            // 알려진 Enterprise OID 매핑
            if (sysObjectId.StartsWith("1.3.6.1.4.1.9.")) return "Cisco";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.2636.")) return "Juniper";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.311.")) return "Microsoft";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.8072.")) return "Net-SNMP";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.2011.")) return "Huawei";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.3930.36")) return "NTT (MVE5000)";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.3930.35")) return "NTT (MVD5000)";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.3930.")) return "NTT";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.171.")) return "D-Link";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.5624.")) return "HP";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.232.")) return "HP";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.1991.")) return "Brocade";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.6486.")) return "Alcatel-Lucent";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.6027.")) return "3Com";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.890.")) return "ZTE";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.6527.")) return "Extreme Networks";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.1916.")) return "Enterasys";
            if (sysObjectId.StartsWith("1.3.6.1.4.1.30065.")) return "Ubiquiti";
        }

        // sysDescr에서 메이커 추출 (대체 방법)
        if (!string.IsNullOrWhiteSpace(sysDescr))
        {
            var descr = sysDescr.ToUpper();
            if (descr.Contains("CISCO")) return "Cisco";
            if (descr.Contains("JUNIPER")) return "Juniper";
            if (descr.Contains("MICROSOFT") || descr.Contains("WINDOWS")) return "Microsoft";
            if (descr.Contains("NET-SNMP") || descr.Contains("NETSNMP")) return "Net-SNMP";
            if (descr.Contains("HUAWEI")) return "Huawei";
            if (descr.Contains("MVE5000") || descr.Contains("MVD5000")) return "NTT";
            if (descr.Contains("D-LINK") || descr.Contains("DLINK")) return "D-Link";
            if (descr.Contains("HP ") || descr.Contains("HEWLETT")) return "HP";
            if (descr.Contains("BROCADE")) return "Brocade";
            if (descr.Contains("ALCATEL") || descr.Contains("LUCENT")) return "Alcatel-Lucent";
            if (descr.Contains("3COM")) return "3Com";
            if (descr.Contains("ZTE")) return "ZTE";
            if (descr.Contains("EXTREME")) return "Extreme Networks";
            if (descr.Contains("UBIQUITI")) return "Ubiquiti";
            
            // sysDescr의 첫 부분에서 추출 시도 (일반적으로 형식: "Manufacturer Model ...")
            var parts = sysDescr.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].Length > 2)
            {
                return parts[0];
            }
        }

        return "Unknown";
    }

    private void CmbMakerFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbMakerFilter?.SelectedItem == null) return;
        
        string? selectedMaker = null;
        if (cmbMakerFilter.SelectedItem is ComboBoxItem item)
        {
            selectedMaker = item.Content?.ToString();
        }
        else if (cmbMakerFilter.SelectedItem is string maker)
        {
            selectedMaker = maker;
        }
        
        // "None"이면 아무것도 하지 않음
        if (selectedMaker == null || selectedMaker == "None" || selectedMaker == "")
        {
            return;
        }
        
        // 해당 Maker의 기기들 찾기
        var makerDevices = DiscoveredDevices.Where(d => d.Maker == selectedMaker).ToList();
        if (makerDevices.Count == 0) return;
        
        // 모두 체크되어 있으면 모두 해제, 아니면 모두 체크
        bool allSelected = makerDevices.All(d => d.IsSelected);
        
        foreach (var device in makerDevices)
        {
            device.IsSelected = !allSelected;
        }
        
        // 선택을 "None"으로 리셋
        Dispatcher.Invoke(() =>
        {
            if (cmbMakerFilter.Items.Count > 0)
            {
                cmbMakerFilter.SelectedIndex = 0; // "None" 선택
            }
        });
    }

    private void CmbDeviceFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbDeviceFilter?.SelectedItem == null) return;
        
        string? selectedDevice = null;
        if (cmbDeviceFilter.SelectedItem is ComboBoxItem item)
        {
            selectedDevice = item.Content?.ToString();
        }
        else if (cmbDeviceFilter.SelectedItem is string device)
        {
            selectedDevice = device;
        }
        
        // "None"이면 아무것도 하지 않음
        if (selectedDevice == null || selectedDevice == "None" || selectedDevice == "")
        {
            return;
        }
        
        // 해당 DeviceName의 기기들 찾기
        var deviceMatches = DiscoveredDevices.Where(d => d.DeviceName == selectedDevice).ToList();
        if (deviceMatches.Count == 0) return;
        
        // 모두 체크되어 있으면 모두 해제, 아니면 모두 체크
        bool allSelected = deviceMatches.All(d => d.IsSelected);
        
        foreach (var device in deviceMatches)
        {
            device.IsSelected = !allSelected;
        }
        
        // 선택을 "None"으로 리셋
        Dispatcher.Invoke(() =>
        {
            if (cmbDeviceFilter.Items.Count > 0)
            {
                cmbDeviceFilter.SelectedIndex = 0; // "None" 선택
            }
        });
    }

    private void UpdateFilterDropdowns()
    {
        if (cmbMakerFilter == null || cmbDeviceFilter == null) return;
        
        // 메이커 드롭다운 업데이트
        var currentMaker = cmbMakerFilter.SelectedItem;
        var currentMakerValue = currentMaker is ComboBoxItem cbi ? cbi.Content?.ToString() : currentMaker?.ToString();
        cmbMakerFilter.Items.Clear();
        cmbMakerFilter.Items.Add(new ComboBoxItem { Content = "None", IsSelected = true });
        foreach (var maker in AvailableMakers.OrderBy(m => m))
        {
            cmbMakerFilter.Items.Add(maker);
        }
        // 현재 선택값 유지 또는 "None" 선택
        if (currentMakerValue != null && currentMakerValue != "None")
        {
            var itemToSelect = cmbMakerFilter.Items.Cast<object>().FirstOrDefault(item =>
                (item is ComboBoxItem itemCbi && itemCbi.Content?.ToString() == currentMakerValue) ||
                (item is string str && str == currentMakerValue));
            if (itemToSelect != null)
            {
                cmbMakerFilter.SelectedItem = itemToSelect;
            }
            else
            {
                cmbMakerFilter.SelectedIndex = 0; // "None" 선택
            }
        }
        else if (cmbMakerFilter.Items.Count > 0)
        {
            cmbMakerFilter.SelectedIndex = 0; // "None" 선택
        }
        
        // 디바이스 드롭다운 업데이트
        var currentDevice = cmbDeviceFilter.SelectedItem;
        var currentDeviceValue = currentDevice is ComboBoxItem cdi ? cdi.Content?.ToString() : currentDevice?.ToString();
        cmbDeviceFilter.Items.Clear();
        cmbDeviceFilter.Items.Add(new ComboBoxItem { Content = "None", IsSelected = true });
        foreach (var device in AvailableDevices.OrderBy(d => d))
        {
            cmbDeviceFilter.Items.Add(device);
        }
        // 현재 선택값 유지 또는 "None" 선택
        if (currentDeviceValue != null && currentDeviceValue != "None")
        {
            var itemToSelect = cmbDeviceFilter.Items.Cast<object>().FirstOrDefault(item =>
                (item is ComboBoxItem itemCdi && itemCdi.Content?.ToString() == currentDeviceValue) ||
                (item is string str && str == currentDeviceValue));
            if (itemToSelect != null)
            {
                cmbDeviceFilter.SelectedItem = itemToSelect;
            }
            else
            {
                cmbDeviceFilter.SelectedIndex = 0; // "None" 선택
            }
        }
        else if (cmbDeviceFilter.Items.Count > 0)
        {
            cmbDeviceFilter.SelectedIndex = 0; // "None" 선택
        }
    }

    /// <summary>
    /// IP 주소를 기반으로 서브넷 이름을 계산합니다.
    /// </summary>
    private string CalculateSubnetName(string deviceIpAddress)
    {
        // Seed 목록에서 해당 IP가 속한 서브넷 찾기
        foreach (var seed in _config.Seeds)
        {
            if (IsIpInSubnet(deviceIpAddress, seed))
            {
                // 네트워크 주소 계산
                var networkAddress = CalculateNetworkAddress(seed.IpAddr, seed.Netmask);
                var cidrPrefix = NetmaskToCidrPrefix(seed.Netmask);
                return $"{networkAddress}/{cidrPrefix}";
            }
        }
        
        // 매칭되는 Seed가 없으면 "Default" 반환
        return "Default";
    }

    /// <summary>
    /// IP 주소가 특정 Seed의 서브넷에 속하는지 확인합니다.
    /// </summary>
    private bool IsIpInSubnet(string ipAddress, DiscoveryPollingAgentsDialog.SeedEntry seed)
    {
        try
        {
            var ip = System.Net.IPAddress.Parse(ipAddress);
            var seedIp = System.Net.IPAddress.Parse(seed.IpAddr);
            var mask = System.Net.IPAddress.Parse(seed.Netmask);
            
            var ipBytes = ip.GetAddressBytes();
            var seedBytes = seedIp.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            
            for (int i = 0; i < 4; i++)
            {
                if ((ipBytes[i] & maskBytes[i]) != (seedBytes[i] & maskBytes[i]))
                {
                    return false;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Netmask를 CIDR prefix length로 변환합니다.
    /// </summary>
    private int NetmaskToCidrPrefix(string netmask)
    {
        try
        {
            var mask = System.Net.IPAddress.Parse(netmask);
            var maskBytes = mask.GetAddressBytes();
            int prefixLength = 0;
            
            foreach (var b in maskBytes)
            {
                for (int i = 7; i >= 0; i--)
                {
                    if ((b & (1 << i)) != 0)
                    {
                        prefixLength++;
                    }
                    else
                    {
                        return prefixLength;
                    }
                }
            }
            return prefixLength;
        }
        catch
        {
            return 24; // 기본값
        }
    }

    /// <summary>
    /// IP 주소와 Netmask로 네트워크 주소를 계산합니다.
    /// </summary>
    private string CalculateNetworkAddress(string ipAddress, string netmask)
    {
        try
        {
            var ip = System.Net.IPAddress.Parse(ipAddress);
            var mask = System.Net.IPAddress.Parse(netmask);
            
            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var networkBytes = new byte[4];
            
            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }
            
            return new System.Net.IPAddress(networkBytes).ToString();
        }
        catch
        {
            return ipAddress;
        }
    }

    /// <summary>
    /// 서브넷 그룹을 업데이트합니다.
    /// </summary>
    private void UpdateSubnetGroups()
    {
        var subnetDict = new Dictionary<string, SubnetGroup>();
        
        // 기존 그룹 유지
        foreach (var group in SubnetGroups)
        {
            subnetDict[group.SubnetName] = group;
            group.Devices.Clear();
        }
        
        // 디바이스를 서브넷별로 그룹화
        foreach (var device in DiscoveredDevices)
        {
            if (!subnetDict.ContainsKey(device.SubnetName))
            {
                subnetDict[device.SubnetName] = new SubnetGroup
                {
                    SubnetName = device.SubnetName,
                    IsSelected = false  // 기본값: uncheck
                };
            }
            subnetDict[device.SubnetName].Devices.Add(device);
        }
        
        // 컬렉션 업데이트
        SubnetGroups.Clear();
        foreach (var group in subnetDict.Values.OrderBy(g => g.SubnetName))
        {
            SubnetGroups.Add(group);
        }
    }
}

