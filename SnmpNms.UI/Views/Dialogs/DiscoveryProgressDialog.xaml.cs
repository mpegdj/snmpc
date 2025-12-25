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
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Views.Dialogs;

public partial class DiscoveryProgressDialog : Window
{
    public class DiscoveredDevice : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string IpAddress { get; set; } = "";
        public string Status { get; set; } = "Unknown";
        public string Community { get; set; } = "public";
        public string Version { get; set; } = "V2c";
        public int Port { get; set; } = 161;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    private readonly ISnmpClient _snmpClient;
    private readonly DiscoveryPollingAgentsDialog.DiscoveryConfig _config;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isRunning = false;

    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = new();

    public DiscoveryProgressDialog(ISnmpClient snmpClient, DiscoveryPollingAgentsDialog.DiscoveryConfig config)
    {
        InitializeComponent();
        _snmpClient = snmpClient;
        _config = config;
        DataContext = this;
        dataGridDevices.ItemsSource = DiscoveredDevices;
        
        // Discovery 시작
        _ = StartDiscoveryAsync();
    }

    private async Task StartDiscoveryAsync()
    {
        _isRunning = true;
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

            // Seeds에서 IP 범위 생성
            foreach (var seed in _config.Seeds)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                AddLog($"Scanning seed: {seed.IpAddr}/{seed.Netmask}");
                var ipRange = GenerateIpRange(seed.IpAddr, seed.Netmask);
                AddLog($"  Generated {ipRange.Count} IP addresses");

                foreach (var ip in ipRange)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested) break;
                    if (discoveredIps.Contains(ip)) continue;

                    // Address 필터 확인
                    var addressFilters = _config.Filters.Where(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.Address).ToList();
                    if (addressFilters.Count > 0)
                    {
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
                                addressMatch = false;
                                break;
                            }
                        }
                        if (!addressMatch) continue;
                    }

                    discoveredIps.Add(ip);
                    await DiscoverDeviceAsync(ip, _config);
                    
                    UpdateProgress();
                }
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
            _isRunning = false;
            btnStop.IsEnabled = false;
            btnOk.IsEnabled = true;
            btnCancel.IsEnabled = true;
        }
    }

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
            
            // 네트워크 주소 계산
            var networkBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }
            
            // 호스트 부분 마스크 계산
            var hostMask = ~BitConverter.ToUInt32(maskBytes.Reverse().ToArray(), 0);
            var hostCount = hostMask + 1;
            
            // 너무 많은 IP는 제한 (예: /24 = 256개)
            if (hostCount > 256) hostCount = 256;
            
            for (uint i = 1; i < hostCount - 1; i++) // 0과 255 제외
            {
                var hostBytes = BitConverter.GetBytes(i).Take(4).ToArray();
                var finalBytes = new byte[4];
                for (int j = 0; j < 4; j++)
                {
                    finalBytes[j] = (byte)(networkBytes[j] | hostBytes[j]);
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

    private async Task DiscoverDeviceAsync(string ip, DiscoveryPollingAgentsDialog.DiscoveryConfig config)
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

        // SNMP 확인
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

                // sysDescr (Maker), sysName (Device Name)으로 SNMP 확인
                var oids = new[] { "1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.5.0" };
                var result = await _snmpClient.GetAsync(target, oids);
                
                if (result.IsSuccess && result.Variables.Count > 0)
                {
                    string? sysDescr = null;
                    string? sysName = null;
                    
                    foreach (var v in result.Variables)
                    {
                        if (v.Oid == "1.3.6.1.2.1.1.1.0") sysDescr = v.Value;
                        else if (v.Oid == "1.3.6.1.2.1.1.5.0") sysName = v.Value;
                    }
                    
                    // Maker 필터 확인 (sysDescr 사용)
                    var makerFilters = config.Filters.Where(f => f.FilterCategory == DiscoveryPollingAgentsDialog.FilterType.Maker).ToList();
                    if (makerFilters.Count > 0)
                    {
                        var makerName = sysDescr ?? "";
                        bool makerMatch = false;
                        foreach (var filter in makerFilters)
                        {
                            if (filter.Type == "Include" && MatchesNamePattern(makerName, filter.Range))
                            {
                                makerMatch = true;
                                break;
                            }
                            if (filter.Type == "Exclude" && MatchesNamePattern(makerName, filter.Range))
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
                    
                    var device = new DiscoveredDevice
                    {
                        IpAddress = ip,
                        Status = "SNMP OK",
                        Community = comm.ReadCommunity,
                        Version = comm.Version,
                        Port = 161
                    };
                    
                    DiscoveredDevices.Add(device);
                    var makerInfo = !string.IsNullOrWhiteSpace(sysDescr) ? $" Maker: {sysDescr}" : "";
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
            var device = new DiscoveredDevice
            {
                IpAddress = ip,
                Status = "Ping Only",
                Community = "N/A",
                Version = "N/A",
                Port = 161
            };
            DiscoveredDevices.Add(device);
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

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var device in DiscoveredDevices)
        {
            device.IsSelected = true;
        }
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var device in DiscoveredDevices)
        {
            device.IsSelected = false;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        base.OnClosed(e);
    }
}

