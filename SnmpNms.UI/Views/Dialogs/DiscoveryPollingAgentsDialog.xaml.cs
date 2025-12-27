using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Views.Dialogs;

public partial class DiscoveryPollingAgentsDialog : Window
{
    public class PollingAgent
    {
        public string Address { get; set; } = "";
        public string Status { get; set; } = "disconnected";
    }

    public class SeedEntry
    {
        public string IpAddr { get; set; } = "";
        public string Netmask { get; set; } = "";
        public override string ToString() => $"{IpAddr}/{Netmask}";
    }

    public class CommunityEntry
    {
        public string Version { get; set; } = "V1";
        public string ReadCommunity { get; set; } = "public";
        public string WriteCommunity { get; set; } = "private";
        public override string ToString() => $"{Version},{Version},{ReadCommunity},{WriteCommunity}...";
    }

    public enum FilterType
    {
        Address,
        Maker,
        DeviceName
    }

    public class FilterEntry
    {
        public string Type { get; set; } = "Include";
        public string Range { get; set; } = "";
        public FilterType FilterCategory { get; set; } = FilterType.Address;
        public override string ToString()
        {
            return FilterCategory switch
            {
                FilterType.Address => $"{Type} Address: {Range}",
                FilterType.Maker => $"{Type} Maker: {Range}",
                FilterType.DeviceName => $"{Type} Device Pattern: {Range}",
                _ => $"{Type} {Range}"
            };
        }
    }

    public class DiscoveryConfig
    {
        public bool EnableDiscovery { get; set; }
        public bool UseSubnetBroadcasts { get; set; } = true;
        public bool PingScanSubnets { get; set; } = true;
        public int AutoRestartTimeHours { get; set; } = 1;
        public bool EnableStatusPolling { get; set; } = true;
        public bool EnableServicePolling { get; set; } = true;
        public bool FindNonSnmpNodes { get; set; } = true;
        public bool FindRmonDevices { get; set; } = true;
        public bool FindSnmpV1 { get; set; } = true;
        public bool FindSnmpV2 { get; set; } = true;
        public bool FindSnmpV3 { get; set; } = true;
        public bool FindWeb { get; set; } = true;
        public bool FindSmtp { get; set; } = true;
        public bool FindTelnet { get; set; }
        public bool FindFtp { get; set; }
        public List<SeedEntry> Seeds { get; set; } = new();
        public List<CommunityEntry> Communities { get; set; } = new();
        public List<FilterEntry> Filters { get; set; } = new();
    }

    private readonly ISnmpClient? _snmpClient;
    private readonly ITrapListener? _trapListener;
    private readonly MainViewModel? _mainViewModel;
    private const string ConfigFileName = "discovery_config.json";

    public ObservableCollection<PollingAgent> Agents { get; } = new();
    public ObservableCollection<SeedEntry> Seeds { get; } = new();
    public ObservableCollection<CommunityEntry> Communities { get; } = new();
    public ObservableCollection<FilterEntry> Filters { get; } = new();

    public DiscoveryConfig Config { get; private set; } = new();

    public DiscoveryPollingAgentsDialog(ISnmpClient? snmpClient = null, MainViewModel? mainViewModel = null, ITrapListener? trapListener = null)
    {
        _snmpClient = snmpClient;
        _trapListener = trapListener;
        _mainViewModel = mainViewModel;
        InitializeComponent();
        DataContext = this;
        
        // 설정 로드
        LoadConfig();
        
        // 기본 에이전트는 추가하지 않음 (SNMP를 지원하지 않을 수 있음)
        lstAgents.ItemsSource = Agents;
        lstSeeds.ItemsSource = Seeds;
        lstCommunities.ItemsSource = Communities;
        lstFilters.ItemsSource = Filters;
        
        // UI에 설정 반영
        ApplyConfigToUI();
    }

    private void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                Config = JsonSerializer.Deserialize<DiscoveryConfig>(json) ?? new DiscoveryConfig();
            }
            else
            {
                // 기본값 설정
                SetDefaultConfig();
            }
        }
        catch
        {
            Config = new DiscoveryConfig();
        }

        // ObservableCollection에 복사
        Seeds.Clear();
        Communities.Clear();
        Filters.Clear();
        foreach (var seed in Config.Seeds) Seeds.Add(seed);
        foreach (var comm in Config.Communities) Communities.Add(comm);
        foreach (var filter in Config.Filters) Filters.Add(filter);
    }
    
    private void SetDefaultConfig()
    {
        Config = new DiscoveryConfig();
        Config.Seeds.Add(new SeedEntry { IpAddr = "192.168.0.0", Netmask = "255.255.254.0" });
        Config.Communities.Add(new CommunityEntry { Version = "V1", ReadCommunity = "crclab", WriteCommunity = "crclab" });
        Config.Communities.Add(new CommunityEntry { Version = "V1", ReadCommunity = "public", WriteCommunity = "netman" });
        Config.Filters.Add(new FilterEntry { Type = "Include", Range = "192.168.0.100-101", FilterCategory = FilterType.Address });
        Config.Filters.Add(new FilterEntry { Type = "Include", Range = "192.168.1.100-101", FilterCategory = FilterType.Address });
        Config.Filters.Add(new FilterEntry { Type = "Include", Range = "ntt*", FilterCategory = FilterType.Maker });
        Config.Filters.Add(new FilterEntry { Type = "Include", Range = "hv*", FilterCategory = FilterType.DeviceName });
        Config.Filters.Add(new FilterEntry { Type = "Include", Range = "mv*", FilterCategory = FilterType.DeviceName });
        Config.Filters.Add(new FilterEntry { Type = "Include", Range = "hc*", FilterCategory = FilterType.DeviceName });
    }

    private void SaveConfig()
    {
        try
        {
            // UI에서 설정 읽기
            Config.EnableDiscovery = chkEnableDiscovery.IsChecked == true;
            Config.UseSubnetBroadcasts = chkUseSubnetBroadcasts.IsChecked == true;
            Config.PingScanSubnets = chkPingScanSubnets.IsChecked == true;
            Config.AutoRestartTimeHours = int.TryParse(txtAutoRestartTime.Text, out var hours) ? hours : 1;
            Config.EnableStatusPolling = chkEnableStatusPolling.IsChecked == true;
            Config.EnableServicePolling = chkEnableServicePolling.IsChecked == true;
            Config.FindNonSnmpNodes = chkFindNonSnmpNodes.IsChecked == true;
            Config.FindRmonDevices = chkFindRmonDevices.IsChecked == true;
            Config.FindSnmpV1 = chkFindSnmpV1.IsChecked == true;
            Config.FindSnmpV2 = chkFindSnmpV2.IsChecked == true;
            Config.FindSnmpV3 = chkFindSnmpV3.IsChecked == true;
            Config.FindWeb = chkFindWeb.IsChecked == true;
            Config.FindSmtp = chkFindSmtp.IsChecked == true;
            Config.FindTelnet = chkFindTelnet.IsChecked == true;
            Config.FindFtp = chkFindFtp.IsChecked == true;
            
            Config.Seeds = Seeds.ToList();
            Config.Communities = Communities.ToList();
            Config.Filters = Filters.ToList();

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyConfigToUI()
    {
        chkEnableDiscovery.IsChecked = Config.EnableDiscovery;
        chkUseSubnetBroadcasts.IsChecked = Config.UseSubnetBroadcasts;
        chkPingScanSubnets.IsChecked = Config.PingScanSubnets;
        txtAutoRestartTime.Text = Config.AutoRestartTimeHours.ToString();
        chkEnableStatusPolling.IsChecked = Config.EnableStatusPolling;
        chkEnableServicePolling.IsChecked = Config.EnableServicePolling;
        chkFindNonSnmpNodes.IsChecked = Config.FindNonSnmpNodes;
        chkFindRmonDevices.IsChecked = Config.FindRmonDevices;
        chkFindSnmpV1.IsChecked = Config.FindSnmpV1;
        chkFindSnmpV2.IsChecked = Config.FindSnmpV2;
        chkFindSnmpV3.IsChecked = Config.FindSnmpV3;
        chkFindWeb.IsChecked = Config.FindWeb;
        chkFindSmtp.IsChecked = Config.FindSmtp;
        chkFindTelnet.IsChecked = Config.FindTelnet;
        chkFindFtp.IsChecked = Config.FindFtp;
    }

    private System.Windows.Controls.Button? _btnDelete;
    
    private void LstAgents_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_btnDelete == null)
        {
            _btnDelete = this.FindName("btnDelete") as System.Windows.Controls.Button;
        }
        if (_btnDelete != null)
        {
            _btnDelete.IsEnabled = lstAgents.SelectedItem != null;
        }
    }

    private async void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        if (_snmpClient == null)
        {
            MessageBox.Show("SNMP Client is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (Seeds.Count == 0)
        {
            MessageBox.Show("Please add at least one seed IP address.", "No Seeds", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Communities.Count == 0)
        {
            MessageBox.Show("Please add at least one community string.", "No Communities", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // UI에서 설정 읽기
        SaveConfig(); // 설정 저장

        // Discovery 진행 다이얼로그 표시
        var progressDialog = new DiscoveryProgressDialog(_snmpClient, Config, _trapListener) { Owner = this };
        if (progressDialog.ShowDialog() == true)
        {
            // 선택된 디바이스들을 Map에 등록
            if (_mainViewModel != null)
            {
                var selectedDevices = progressDialog.DiscoveredDevices.Where(d => d.IsSelected && d.Status != "Ping Only").ToList();
                
                // Standard NTT Trap 설정 (MVE5000/MVD5000 전용)
                bool configureStandardNtt = progressDialog.ConfigureStandardNttTrap && 
                                          _trapListener != null && 
                                          _trapListener.IsListening;
                
                if (configureStandardNtt && selectedDevices.Count > 0 && _trapListener != null)
                {
                    var snmpDevices = selectedDevices.Where(d => d.IsSnmpSupported).ToList();
                    if (snmpDevices.Count > 0)
                    {
                        var (trapIp, trapPort) = _trapListener.GetListenerInfo();
                        await ConfigureStandardNttTrapAsync(snmpDevices, trapIp, "public");
                    }
                }
                
                // 표준 Trap 설정 (기존)
                bool configureTrap = progressDialog.ConfigureTrapDestination && 
                                     _trapListener != null && 
                                     _trapListener.IsListening;
                
                if (configureTrap && selectedDevices.Count > 0 && _trapListener != null)
                {
                    var (trapIp, trapPort) = _trapListener.GetListenerInfo();
                    await ConfigureTrapForDevicesAsync(selectedDevices, trapIp, trapPort);
                }
                
                // 선택된 서브넷 그룹 확인
                var selectedSubnets = progressDialog.SubnetGroups
                    .Where(sg => sg.IsSelected)
                    .Select(sg => sg.SubnetName)
                    .ToHashSet();
                
                // 선택된 서브넷에 속한 디바이스만 필터링
                var devicesToAdd = selectedDevices
                    .Where(d => selectedSubnets.Contains(d.SubnetName) || selectedSubnets.Count == 0)
                    .ToList();
                
                foreach (var device in devicesToAdd)
                {
                    var version = device.Version == "V1" ? SnmpVersion.V1 : SnmpVersion.V2c;
                    
                    // DeviceName이 있으면 Device와 Alias 모두 설정, 없으면 IP 주소 사용
                    var deviceName = !string.IsNullOrWhiteSpace(device.DeviceName) ? device.DeviceName : device.IpAddress;
                    
                    var target = new UiSnmpTarget
                    {
                        IpAddress = device.IpAddress,
                        Port = device.Port,
                        Community = device.Community,
                        Version = version,
                        Timeout = 3000,
                        Retries = 1,
                        PollingProtocol = PollingProtocol.SNMP, // Discovery로 찾은 디바이스는 기본적으로 SNMP
                        Device = deviceName,
                        Alias = deviceName,
                        Maker = device.Maker
                    };

                    // 서브넷 없이 추가 옵션 확인
                    MapNode? subnet = null;
                    if (progressDialog.AddDevicesWithoutSubnet)
                    {
                        // 서브넷 없이 Default에 직접 추가
                        subnet = _mainViewModel.DefaultSubnet;
                    }
                    else
                    {
                        // CIDR 기반 서브넷 찾기 또는 생성
                        subnet = FindOrCreateSubnetForDevice(device.IpAddress);
                    }
                    
                    _mainViewModel.AddDeviceToSubnet(target, subnet);
                    _mainViewModel.AddEvent(EventSeverity.Info, target.EndpointKey, 
                        $"[Discovery] Device added: {deviceName} ({device.IpAddress}, Maker: {device.Maker}) to subnet: {subnet.Name}");
                }

                var trapInfo = configureTrap ? " (Trap configured)" : "";
                var subnetInfo = selectedSubnets.Count > 0 ? $" from {selectedSubnets.Count} subnet(s)" : "";
                MessageBox.Show($"Added {devicesToAdd.Count} device(s){subnetInfo} to map{trapInfo}.", "Discovery Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private async Task ConfigureStandardNttTrapAsync(
        List<DiscoveryProgressDialog.DiscoveredDevice> devices, 
        string trapIp, 
        string community)
    {
        if (_snmpClient == null) return;
        
        int successCount = 0;
        int skipCount = 0;
        int failCount = 0;
        
        // MVE5000/MVD5000 base OID
        string[] baseOids = {
            "1.3.6.1.4.1.3930.36.5.2.11",  // MVE5000
            "1.3.6.1.4.1.3930.35.5.2.11"   // MVD5000
        };
        
        foreach (var device in devices)
        {
            try
            {
                var target = new UiSnmpTarget
                {
                    IpAddress = device.IpAddress,
                    Port = device.Port,
                    Community = device.Community == "N/A" ? "private" : device.Community, // Write Community 시도
                    Version = device.Version == "V1" ? SnmpVersion.V1 : SnmpVersion.V2c,
                    Timeout = 3000,
                    Retries = 1
                };

                bool configured = false;
                
                // MVE5000/MVD5000 Table 확인
                foreach (var baseOid in baseOids)
                {
                    // 1번 Entry 확인 (GET)
                    var enableOid = $"{baseOid}.1.2.1";    // ServiceEnable
                    var ipv4Oid = $"{baseOid}.1.5.1";      // IPv4Address
                    
                    var checkResult = await _snmpClient.GetAsync(target, new[] { enableOid, ipv4Oid });
                    
                    if (checkResult.IsSuccess && checkResult.Variables.Count >= 2)
                    {
                        var enableValue = checkResult.Variables.FirstOrDefault(v => v.Oid == enableOid)?.Value;
                        var ipValue = checkResult.Variables.FirstOrDefault(v => v.Oid == ipv4Oid)?.Value;
                        
                        // 이미 설정되어 있으면 스킵
                        if (enableValue == "0" && !string.IsNullOrWhiteSpace(ipValue) && ipValue != "0.0.0.0")
                        {
                            skipCount++;
                            configured = true;
                            break;
                        }
                        
                        // MVE5000/MVD5000 기기임을 확인했으므로 설정 시도
                        // ServiceEnable = 0 (enabled)
                        var enableResult = await _snmpClient.SetAsync(target, enableOid, "0", "INTEGER");
                        if (!enableResult.IsSuccess) continue;
                        
                        // CommunityName = "public"
                        var communityOid = $"{baseOid}.1.3.1";
                        var communityResult = await _snmpClient.SetAsync(target, communityOid, community, "OCTETSTRING");
                        if (!communityResult.IsSuccess) continue;
                        
                        // Protocol = 0 (IPv4)
                        var protocolOid = $"{baseOid}.1.4.1";
                        var protocolResult = await _snmpClient.SetAsync(target, protocolOid, "0", "INTEGER");
                        if (!protocolResult.IsSuccess) continue;
                        
                        // IPv4Address = trapIp
                        var ipResult = await _snmpClient.SetAsync(target, ipv4Oid, trapIp, "IPADDRESS");
                        if (!ipResult.IsSuccess) continue;
                        
                        successCount++;
                        configured = true;
                        break;
                    }
                }
                
                if (!configured)
                {
                    failCount++;
                }
            }
            catch
            {
                failCount++;
            }
        }
        
        if (successCount > 0 || skipCount > 0 || failCount > 0)
        {
            MessageBox.Show(
                $"Standard NTT Trap configuration completed.\n\n" +
                $"Success: {successCount}\n" +
                $"Skipped (already configured): {skipCount}\n" +
                $"Failed: {failCount}\n\n" +
                "Note: Only MVE5000/MVD5000 devices are configured. Other devices are skipped.",
                "Standard NTT Trap Configuration Result",
                MessageBoxButton.OK,
                successCount > 0 || skipCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }

    private async Task ConfigureTrapForDevicesAsync(List<DiscoveryProgressDialog.DiscoveredDevice> devices, string trapIp, int trapPort)
    {
        if (_snmpClient == null) return;
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (var device in devices)
        {
            try
            {
                // Write Community는 Read Community와 동일하게 시도 (실제로는 CommunityEntry에서 WriteCommunity를 가져와야 함)
                // 현재는 DiscoveryProgressDialog에서 WriteCommunity 정보가 없으므로 Read Community 사용
                var target = new UiSnmpTarget
                {
                    IpAddress = device.IpAddress,
                    Port = device.Port,
                    Community = device.Community == "N/A" ? "private" : device.Community, // Write Community 시도
                    Version = device.Version == "V1" ? SnmpVersion.V1 : SnmpVersion.V2c,
                    Timeout = 3000,
                    Retries = 1
                };

                // 표준 SNMP Trap Destination OID 시도
                var trapOid = "1.3.6.1.6.3.18.1.3.0";
                var result = await _snmpClient.SetAsync(target, trapOid, trapIp, "IPADDRESS");

                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            catch
            {
                failCount++;
            }
        }
        
        if (successCount > 0 || failCount > 0)
        {
            MessageBox.Show(
                $"Trap configuration completed.\n\n" +
                $"Success: {successCount}\n" +
                $"Failed: {failCount}\n\n" +
                "Note: Some devices may not support the standard SNMP Trap OID, or Write Community may be incorrect.",
                "Trap Configuration Result",
                MessageBoxButton.OK,
                successCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }

    private void BtnSeedAdd_Click(object sender, RoutedEventArgs e)
    {
        var seedInput = txtSeedIpAddr.Text.Trim();
        if (string.IsNullOrWhiteSpace(seedInput))
        {
            MessageBox.Show("Please enter IP address (CIDR format: X.X.X.X/YY or separate IP and netmask).", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        string ipAddr;
        string netmask;
        
        // CIDR 표기법 확인 (예: 192.168.0.0/24)
        if (seedInput.Contains('/'))
        {
            var parts = seedInput.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                MessageBox.Show("Invalid CIDR format. Use X.X.X.X/YY (e.g., 192.168.0.0/24)", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            ipAddr = parts[0].Trim();
            var cidrPrefix = parts[1].Trim();
            
            // CIDR prefix를 netmask로 변환
            if (!int.TryParse(cidrPrefix, out int prefixLength) || prefixLength < 0 || prefixLength > 32)
            {
                MessageBox.Show("Invalid CIDR prefix length. Must be between 0 and 32.", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            netmask = CidrToNetmask(prefixLength);
        }
        else
        {
            // 기존 방식: IP와 Netmask를 별도로 입력
            ipAddr = seedInput;
            netmask = txtSeedNetmask.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(netmask))
            {
                MessageBox.Show("Please enter netmask or use CIDR format (X.X.X.X/YY).", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        
        Seeds.Add(new SeedEntry 
        { 
            IpAddr = ipAddr, 
            Netmask = netmask 
        });
        
        txtSeedIpAddr.Clear();
        txtSeedNetmask.Clear();
    }
    
    /// <summary>
    /// IP 주소가 특정 Seed의 서브넷에 속하는지 확인합니다.
    /// </summary>
    private bool IsIpInSubnet(string ipAddress, SeedEntry seed)
    {
        try
        {
            var ip = System.Net.IPAddress.Parse(ipAddress);
            var seedIp = System.Net.IPAddress.Parse(seed.IpAddr);
            var mask = System.Net.IPAddress.Parse(seed.Netmask);
            
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            
            var ipBytes = ip.GetAddressBytes();
            var seedBytes = seedIp.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            
            // Seed IP와 Netmask를 AND 연산하여 네트워크 주소 계산
            var seedNetworkBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                seedNetworkBytes[i] = (byte)(seedBytes[i] & maskBytes[i]);
            }
            
            // Device IP와 Netmask를 AND 연산하여 네트워크 주소 계산
            var deviceNetworkBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                deviceNetworkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }
            
            // 네트워크 주소가 같으면 같은 서브넷
            return seedNetworkBytes.SequenceEqual(deviceNetworkBytes);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Netmask에서 CIDR prefix length를 계산합니다.
    /// 예: 255.255.255.0 -> 24
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
                if (b == 0xFF)
                {
                    prefixLength += 8;
                }
                else
                {
                    // 비트를 세어서 prefix length 계산
                    int bits = 0;
                    byte temp = b;
                    while ((temp & 0x80) != 0)
                    {
                        bits++;
                        temp <<= 1;
                    }
                    prefixLength += bits;
                    break;
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
            
            return $"{networkBytes[0]}.{networkBytes[1]}.{networkBytes[2]}.{networkBytes[3]}";
        }
        catch
        {
            return ipAddress;
        }
    }
    
    /// <summary>
    /// 서브넷 이름을 찾는 헬퍼 메서드 (재귀)
    /// </summary>
    private MapNode? FindSubnetByName(MapNode root, string subnetName)
    {
        if (root.NodeType == MapNodeType.Subnet && root.Name == subnetName)
        {
            return root;
        }
        
        foreach (var child in root.Children)
        {
            var found = FindSubnetByName(child, subnetName);
            if (found != null) return found;
        }
        
        return null;
    }
    
    /// <summary>
    /// 기기의 IP 주소에 맞는 서브넷을 찾거나 생성합니다.
    /// </summary>
    private MapNode FindOrCreateSubnetForDevice(string deviceIpAddress)
    {
        if (_mainViewModel == null) 
        {
            // _mainViewModel이 null이면 null을 반환할 수 없으므로, 
            // 이 경우는 호출 전에 체크해야 하지만 안전을 위해 예외 처리
            throw new InvalidOperationException("MainViewModel is not available");
        }
        
        // Seed 목록에서 해당 IP가 속한 서브넷 찾기
        foreach (var seed in Seeds)
        {
            if (IsIpInSubnet(deviceIpAddress, seed))
            {
                // 네트워크 주소 계산
                var networkAddress = CalculateNetworkAddress(seed.IpAddr, seed.Netmask);
                var cidrPrefix = NetmaskToCidrPrefix(seed.Netmask);
                var subnetName = $"{networkAddress}/{cidrPrefix}";
                
                // 서브넷 찾기
                var existingSubnet = FindSubnetByName(_mainViewModel.RootSubnet, subnetName);
                if (existingSubnet != null)
                {
                    return existingSubnet;
                }
                
                // 서브넷이 없으면 생성 (Default 서브넷 아래에 생성)
                return _mainViewModel.AddSubnet(subnetName, _mainViewModel.DefaultSubnet);
            }
        }
        
        // 매칭되는 Seed가 없으면 Default 서브넷에 추가
        return _mainViewModel.DefaultSubnet;
    }
    
    /// <summary>
    /// CIDR prefix length를 netmask로 변환합니다.
    /// 예: 24 -> 255.255.255.0
    /// </summary>
    private string CidrToNetmask(int prefixLength)
    {
        if (prefixLength == 0) return "0.0.0.0";
        if (prefixLength == 32) return "255.255.255.255";
        
        uint mask = 0xFFFFFFFF << (32 - prefixLength);
        var bytes = BitConverter.GetBytes(mask);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
    }

    private void BtnSeedChange_Click(object sender, RoutedEventArgs e)
    {
        if (lstSeeds.SelectedItem is SeedEntry selected)
        {
            txtSeedIpAddr.Text = selected.IpAddr;
            txtSeedNetmask.Text = selected.Netmask;
            Seeds.Remove(selected);
        }
    }

    private void BtnSeedDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstSeeds.SelectedItem is SeedEntry selected)
        {
            Seeds.Remove(selected);
        }
    }

    private void BtnCommAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CommunityEditDialog();
        if (dlg.ShowDialog() == true)
        {
            Communities.Add(new CommunityEntry 
            { 
                Version = dlg.Version,
                ReadCommunity = dlg.ReadCommunity,
                WriteCommunity = dlg.WriteCommunity
            });
        }
    }

    private void BtnCommEdit_Click(object sender, RoutedEventArgs e)
    {
        if (lstCommunities.SelectedItem is CommunityEntry selected)
        {
            var dlg = new CommunityEditDialog 
            { 
                Version = selected.Version,
                ReadCommunity = selected.ReadCommunity,
                WriteCommunity = selected.WriteCommunity
            };
            if (dlg.ShowDialog() == true)
            {
                selected.Version = dlg.Version;
                selected.ReadCommunity = dlg.ReadCommunity;
                selected.WriteCommunity = dlg.WriteCommunity;
            }
        }
    }

    private void BtnCommDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstCommunities.SelectedItem is CommunityEntry selected)
        {
            Communities.Remove(selected);
        }
    }

    private void BtnFilterAddressAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtAddressRange.Text))
        {
            MessageBox.Show("Please enter an address range.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        Filters.Add(new FilterEntry 
        { 
            Type = "Include", 
            Range = txtAddressRange.Text.Trim(),
            FilterCategory = FilterType.Address
        });
        
        txtAddressRange.Clear();
    }

    private void BtnFilterMakerAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtMakerPattern.Text))
        {
            MessageBox.Show("Please enter a maker pattern.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        Filters.Add(new FilterEntry 
        { 
            Type = "Include", 
            Range = txtMakerPattern.Text.Trim(),
            FilterCategory = FilterType.Maker
        });
        
        txtMakerPattern.Clear();
    }

    private void BtnFilterDeviceNameAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtDeviceNamePattern.Text))
        {
            MessageBox.Show("Please enter a device name pattern.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        Filters.Add(new FilterEntry 
        { 
            Type = "Include", 
            Range = txtDeviceNamePattern.Text.Trim(),
            FilterCategory = FilterType.DeviceName
        });
        
        txtDeviceNamePattern.Clear();
    }

    private void BtnFilterChange_Click(object sender, RoutedEventArgs e)
    {
        if (lstFilters.SelectedItem is FilterEntry selected)
        {
            switch (selected.FilterCategory)
            {
                case FilterType.Address:
                    txtAddressRange.Text = selected.Range;
                    break;
                case FilterType.Maker:
                    txtMakerPattern.Text = selected.Range;
                    break;
                case FilterType.DeviceName:
                    txtDeviceNamePattern.Text = selected.Range;
                    break;
            }
            Filters.Remove(selected);
        }
    }

    private void BtnFilterDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstFilters.SelectedItem is FilterEntry selected)
        {
            Filters.Remove(selected);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstAgents.SelectedItem is PollingAgent selected)
        {
            if (MessageBox.Show($"Delete agent '{selected.Address}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Agents.Remove(selected);
            }
        }
    }

    private void BtnDefault_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "모든 설정을 기본값으로 되돌리시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "기본값으로 복원",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            ResetToDefaults();
            MessageBox.Show("설정이 기본값으로 복원되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ResetToDefaults()
    {
        // 기본값 설정
        SetDefaultConfig();
        
        // ObservableCollection에 복사
        Seeds.Clear();
        Communities.Clear();
        Filters.Clear();
        foreach (var seed in Config.Seeds) Seeds.Add(seed);
        foreach (var comm in Config.Communities) Communities.Add(comm);
        foreach (var filter in Config.Filters) Filters.Add(filter);
        
        // UI에 설정 반영
        ApplyConfigToUI();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // 설정 저장
        SaveConfig();
        DialogResult = true;
        Close();
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Discovery/Polling Agents Help\n\nConfigure discovery and polling agents for network discovery.", "Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

// Community Edit Dialog (간단한 버전)
public class CommunityEditDialog : Window
{
    public string Version { get; set; } = "V1";
    public string ReadCommunity { get; set; } = "public";
    public string WriteCommunity { get; set; } = "private";

    public CommunityEditDialog()
    {
        Title = "Edit Community";
        Width = 400;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        
        var cmbVersion = new System.Windows.Controls.ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        cmbVersion.Items.Add("V1");
        cmbVersion.Items.Add("V2c");
        cmbVersion.SelectedItem = Version;
        cmbVersion.SelectionChanged += (s, e) => Version = cmbVersion.SelectedItem?.ToString() ?? "V1";
        
        var txtRead = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 8), Text = ReadCommunity };
        txtRead.TextChanged += (s, e) => ReadCommunity = txtRead.Text;
        
        var txtWrite = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 8), Text = WriteCommunity };
        txtWrite.TextChanged += (s, e) => WriteCommunity = txtWrite.Text;
        
        var stackPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var btnOk = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var btnCancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, IsCancel = true };
        btnOk.Click += (s, e) => { DialogResult = true; Close(); };
        btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
        stackPanel.Children.Add(btnOk);
        stackPanel.Children.Add(btnCancel);
        
        grid.Children.Add(new System.Windows.Controls.TextBlock { Text = "Version:", Margin = new Thickness(0, 0, 0, 4) });
        System.Windows.Controls.Grid.SetRow(cmbVersion, 1);
        grid.Children.Add(cmbVersion);
        System.Windows.Controls.Grid.SetRow(new System.Windows.Controls.TextBlock { Text = "Read Community:", Margin = new Thickness(0, 8, 0, 4) }, 2);
        System.Windows.Controls.Grid.SetRow(txtRead, 3);
        grid.Children.Add(txtRead);
        System.Windows.Controls.Grid.SetRow(new System.Windows.Controls.TextBlock { Text = "Write Community:", Margin = new Thickness(0, 8, 0, 4) }, 4);
        System.Windows.Controls.Grid.SetRow(txtWrite, 5);
        grid.Children.Add(txtWrite);
        System.Windows.Controls.Grid.SetRow(stackPanel, 6);
        grid.Children.Add(stackPanel);
        
        Content = grid;
    }
}

