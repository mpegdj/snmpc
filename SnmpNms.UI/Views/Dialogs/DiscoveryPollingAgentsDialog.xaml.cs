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
                FilterType.DeviceName => $"{Type} Device Name: {Range}",
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
        public bool FindWeb { get; set; } = true;
        public bool FindSmtp { get; set; } = true;
        public bool FindTelnet { get; set; }
        public bool FindFtp { get; set; }
        public List<SeedEntry> Seeds { get; set; } = new();
        public List<CommunityEntry> Communities { get; set; } = new();
        public List<FilterEntry> Filters { get; set; } = new();
    }

    private readonly ISnmpClient? _snmpClient;
    private readonly MainViewModel? _mainViewModel;
    private const string ConfigFileName = "discovery_config.json";

    public ObservableCollection<PollingAgent> Agents { get; } = new();
    public ObservableCollection<SeedEntry> Seeds { get; } = new();
    public ObservableCollection<CommunityEntry> Communities { get; } = new();
    public ObservableCollection<FilterEntry> Filters { get; } = new();

    public DiscoveryConfig Config { get; private set; } = new();

    public DiscoveryPollingAgentsDialog(ISnmpClient? snmpClient = null, MainViewModel? mainViewModel = null)
    {
        _snmpClient = snmpClient;
        _mainViewModel = mainViewModel;
        InitializeComponent();
        DataContext = this;
        
        // 설정 로드
        LoadConfig();
        
        // 기본 에이전트 추가
        Agents.Add(new PollingAgent { Address = "localhost", Status = "connected" });
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
                Config = new DiscoveryConfig();
                Config.Communities.Add(new CommunityEntry { Version = "V1", ReadCommunity = "crclab", WriteCommunity = "crclab" });
                Config.Communities.Add(new CommunityEntry { Version = "V1", ReadCommunity = "public", WriteCommunity = "netman" });
                Config.Filters.Add(new FilterEntry { Type = "Include", Range = "192.168.0.100-101", FilterCategory = FilterType.Address });
                Config.Filters.Add(new FilterEntry { Type = "Include", Range = "192.168.1.100-101", FilterCategory = FilterType.Address });
                Config.Filters.Add(new FilterEntry { Type = "Include", Range = "ntt*", FilterCategory = FilterType.Maker });
                Config.Filters.Add(new FilterEntry { Type = "Include", Range = "hv*", FilterCategory = FilterType.Maker });
                Config.Filters.Add(new FilterEntry { Type = "Include", Range = "mv*", FilterCategory = FilterType.Maker });
                Config.Filters.Add(new FilterEntry { Type = "Include", Range = "hc*", FilterCategory = FilterType.Maker });
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

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
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
        var progressDialog = new DiscoveryProgressDialog(_snmpClient, Config) { Owner = this };
        if (progressDialog.ShowDialog() == true)
        {
            // 선택된 디바이스들을 Map에 등록
            if (_mainViewModel != null)
            {
                var selectedDevices = progressDialog.DiscoveredDevices.Where(d => d.IsSelected && d.Status != "Ping Only").ToList();
                foreach (var device in selectedDevices)
                {
                    var version = device.Version == "V1" ? SnmpVersion.V1 : SnmpVersion.V2c;
                    var target = new UiSnmpTarget
                    {
                        IpAddress = device.IpAddress,
                        Port = device.Port,
                        Community = device.Community,
                        Version = version,
                        Timeout = 3000,
                        Retries = 1
                    };

                    _mainViewModel.AddDeviceToSubnet(target);
                    _mainViewModel.AddEvent(EventSeverity.Info, target.EndpointKey, 
                        $"[Discovery] Device added: {device.IpAddress} ({device.Status})");
                }

                MessageBox.Show($"Added {selectedDevices.Count} device(s) to map.", "Discovery Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void BtnSeedAdd_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSeedIpAddr.Text) || string.IsNullOrWhiteSpace(txtSeedNetmask.Text))
        {
            MessageBox.Show("Please enter both IP address and netmask.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        Seeds.Add(new SeedEntry 
        { 
            IpAddr = txtSeedIpAddr.Text.Trim(), 
            Netmask = txtSeedNetmask.Text.Trim() 
        });
        
        txtSeedIpAddr.Clear();
        txtSeedNetmask.Clear();
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

