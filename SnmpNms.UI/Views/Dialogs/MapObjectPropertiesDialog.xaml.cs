using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using SnmpNms.Core.Interfaces;
using System.Windows;
using SnmpNms.Core.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Views.Dialogs;

public partial class MapObjectPropertiesDialog : Window, INotifyPropertyChanged
{
    private readonly ISnmpClient? _snmpClient;
    private readonly ITrapListener? _trapListener;
    private CancellationTokenSource? _lookupCts;

    public MapObjectPropertiesDialog(MapObjectType type, ISnmpClient? snmpClient = null, ITrapListener? trapListener = null)
    {
        ObjectType = type;
        _snmpClient = snmpClient;
        _trapListener = trapListener;
        // sensible defaults (SNMPc 느낌)
        IconName = "auto.ico";
        ReadAccessMode = "SNMP V2c";
        ReadWriteAccessMode = "SNMP V2c";
        ReadCommunity = "public";
        ReadWriteCommunity = "private";
        PollIntervalSec = "3";
        PollTimeoutMs = "3000";
        PollRetries = "1";
        LookupStatus = "Address 입력 후 Enter 또는 Lookup을 누르면 Ping/SNMP로 자동 채움";

        // Trap Listener 정보로 기본값 설정 (항상 실제 네트워크 IP 사용)
        if (_trapListener != null)
        {
            var (ip, port) = _trapListener.GetListenerInfo();
            TrapDestinationIp = ip;
            TrapDestinationPort = port.ToString();
        }
        else
        {
            // Trap Listener가 없어도 실제 네트워크 IP 찾기 시도
            TrapDestinationIp = GetLocalNetworkIp();
            TrapDestinationPort = "162";
        }

        DataContext = this;
        InitializeComponent();
        
        // Trap 탭은 Device 타입일 때만 활성화
        if (tabTrap != null)
        {
            tabTrap.IsEnabled = ObjectType == MapObjectType.Device;
        }
        
        // Address 입력창 초기화
        if (ObjectType == MapObjectType.Device)
        {
            SyncAddressToInputs();
        }
    }

    public MapObjectPropertiesDialog(MapObjectType type, UiSnmpTarget target, ISnmpClient? snmpClient = null, ITrapListener? trapListener = null) : this(type, snmpClient, trapListener)
    {
        // 기존 UiSnmpTarget의 값으로 다이얼로그 초기화
        Alias = target.Alias ?? "";
        Device = target.Device ?? "";
        Address = $"{target.IpAddress}:{target.Port}";
        ReadCommunity = target.Community ?? "public";
        ReadWriteCommunity = target.Community ?? "private";
        ReadAccessMode = target.Version switch
        {
            SnmpVersion.V1 => "SNMP V1",
            SnmpVersion.V3 => "SNMP V3",
            _ => "SNMP V2c"
        };
        ReadWriteAccessMode = ReadAccessMode;
        PollTimeoutMs = target.Timeout.ToString();
        PollRetries = target.Retries.ToString();
        PollIntervalSec = "3"; // UiSnmpTarget에 없으므로 기본값 사용
        
        // PollingProtocol 설정
        var protocolStr = target.PollingProtocol switch
        {
            Core.Models.PollingProtocol.Ping => "Ping",
            Core.Models.PollingProtocol.ARP => "ARP",
            Core.Models.PollingProtocol.None => "None",
            _ => "SNMP"
        };
        PollingProtocol = protocolStr;
        
        // ComboBox 초기화
        if (ObjectType == MapObjectType.Device && cmbPollingProtocol != null)
        {
            for (int i = 0; i < cmbPollingProtocol.Items.Count; i++)
            {
                if (cmbPollingProtocol.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                    item.Content?.ToString() == protocolStr)
                {
                    cmbPollingProtocol.SelectedIndex = i;
                    break;
                }
            }
        }
        
        SyncAddressToInputs();
    }
    
    private void SyncAddressToInputs()
    {
        // Address 속성에서 IP:Port를 파싱하여 각 입력창에 설정
        var (host, port) = ParseHostPort(Address);
        if (!string.IsNullOrWhiteSpace(host))
        {
            var parts = host.Split('.');
            if (parts.Length == 4)
            {
                txtAddress1.Text = parts[0];
                txtAddress2.Text = parts[1];
                txtAddress3.Text = parts[2];
                txtAddress4.Text = parts[3];
            }
        }
        txtPort.Text = port > 0 ? port.ToString() : "161";
    }
    
    private void SyncInputsToAddress()
    {
        // 각 입력창의 값을 조합하여 Address 속성에 설정
        _isSyncingAddress = true;
        try
        {
            var parts = new[] { txtAddress1.Text, txtAddress2.Text, txtAddress3.Text, txtAddress4.Text };
            var ip = string.Join(".", parts);
            if (int.TryParse(txtPort.Text, out var port) && port > 0)
            {
                Address = $"{ip}:{port}";
            }
            else if (!string.IsNullOrWhiteSpace(ip))
            {
                Address = ip;
            }
        }
        finally
        {
            _isSyncingAddress = false;
        }
    }
    
    private void AddressPart_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var txt = sender as System.Windows.Controls.TextBox;
        if (txt is null) return;
        
        // 숫자만 입력되도록 필터링 (이미 PreviewTextInput에서 처리하지만 안전장치)
        var text = txt.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        
        // 3자리 입력 시 자동으로 다음 입력창으로 이동
        if (text.Length >= 3 && txt != txtAddress4)
        {
            var next = txt == txtAddress1 ? txtAddress2 : 
                      txt == txtAddress2 ? txtAddress3 : txtAddress4;
            next?.Focus();
            next?.SelectAll();
        }
        
        SyncInputsToAddress();
    }
    
    private void AddressPart_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 숫자만 허용
        e.Handled = !char.IsDigit(e.Text, 0);
    }
    
    private void AddressPart_KeyDown(object sender, KeyEventArgs e)
    {
        var txt = sender as System.Windows.Controls.TextBox;
        if (txt is null) return;
        
        // Backspace가 첫 번째 입력창에서 눌리면 이전 입력창으로 이동
        if (e.Key == Key.Back && txt.Text.Length == 0)
        {
            var prev = txt == txtAddress2 ? txtAddress1 :
                      txt == txtAddress3 ? txtAddress2 :
                      txt == txtAddress4 ? txtAddress3 : null;
            if (prev != null)
            {
                prev.Focus();
                prev.SelectAll();
                e.Handled = true;
            }
        }
        // Enter 키로 Lookup 실행
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ObjectType == MapObjectType.Device)
            {
                _ = LookupWithPreviewAsync();
            }
        }
        // 점(.) 입력 시 다음 입력창으로 이동
        else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
        {
            var next = txt == txtAddress1 ? txtAddress2 :
                      txt == txtAddress2 ? txtAddress3 :
                      txt == txtAddress3 ? txtAddress4 : null;
            if (next != null)
            {
                next.Focus();
                next.SelectAll();
                e.Handled = true;
            }
        }
    }
    
    private void AddressPart_GotFocus(object sender, RoutedEventArgs e)
    {
        // 포커스 시 전체 선택
        if (sender is System.Windows.Controls.TextBox txt)
        {
            txt.SelectAll();
        }
    }
    
    private void Port_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SyncInputsToAddress();
    }
    
    private void Port_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 숫자만 허용
        e.Handled = !char.IsDigit(e.Text, 0);
    }
    
    private void Port_KeyDown(object sender, KeyEventArgs e)
    {
        // Enter 키로 Lookup 실행
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ObjectType == MapObjectType.Device)
            {
                _ = LookupWithPreviewAsync();
            }
        }
    }

    public MapObjectType ObjectType { get; set; }

    private string _alias = "";
    public string Alias
    {
        get => _alias;
        set { if (_alias == value) return; _alias = value; OnPropertyChanged(); }
    }

    private string _device = "";
    public string Device
    {
        get => _device;
        set { if (_device == value) return; _device = value; OnPropertyChanged(); }
    }

    private string _address = "";
    private bool _isSyncingAddress = false;
    public string Address
    {
        get => _address;
        set 
        { 
            if (_address == value) return; 
            _address = value; 
            OnPropertyChanged();
            // Address 속성이 외부에서 변경되면 입력창 동기화
            if (!_isSyncingAddress && ObjectType == MapObjectType.Device)
            {
                SyncAddressToInputs();
            }
        }
    }

    private string _iconName = "auto.ico";
    public string IconName
    {
        get => _iconName;
        set { if (_iconName == value) return; _iconName = value; OnPropertyChanged(); }
    }

    public string NodeGroup1 { get; set; } = "";
    public string NodeGroup2 { get; set; } = "";

    public string Description { get; set; } = "";

    public string ReadAccessMode { get; set; } = "SNMP V2c";
    public string ReadWriteAccessMode { get; set; } = "SNMP V2c";
    public string ReadCommunity { get; set; } = "public";
    public string ReadWriteCommunity { get; set; } = "private";

    public string PollingProtocol { get; set; } = "SNMP";
    public string PollIntervalSec { get; set; } = "3";
    public string PollTimeoutMs { get; set; } = "3000";
    public string PollRetries { get; set; } = "1";

    private string _trapDestinationIp = "";
    public string TrapDestinationIp
    {
        get => _trapDestinationIp;
        set
        {
            if (_trapDestinationIp != value)
            {
                _trapDestinationIp = value;
                OnPropertyChanged();
            }
        }
    }

    private string _trapDestinationPort = "162";
    public string TrapDestinationPort
    {
        get => _trapDestinationPort;
        set
        {
            if (_trapDestinationPort != value)
            {
                _trapDestinationPort = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsConfigureTrapEnabled => ObjectType == MapObjectType.Device && 
                                          _snmpClient != null && 
                                          !string.IsNullOrWhiteSpace(Address);

    private bool _isLookupBusy;
    public bool IsLookupBusy
    {
        get => _isLookupBusy;
        set { if (_isLookupBusy == value) return; _isLookupBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLookupEnabled)); }
    }

    public bool IsLookupEnabled => !IsLookupBusy && ObjectType == MapObjectType.Device;

    private string _lookupStatus = "";
    public string LookupStatus
    {
        get => _lookupStatus;
        set { if (_lookupStatus == value) return; _lookupStatus = value; OnPropertyChanged(); }
    }

    public ParsedResult Result { get; private set; } = new();

    public sealed class ParsedResult
    {
        public MapObjectType Type { get; init; }
        public string Alias { get; init; } = "";
        public string Device { get; init; } = "";
        public string Icon { get; init; } = "auto.ico";
        public string Description { get; init; } = "";

        // Device
        public string IpOrHost { get; init; } = "";
        public int Port { get; init; } = 161;
        public SnmpVersion SnmpVersion { get; init; } = SnmpVersion.V2c;
        public string ReadCommunity { get; init; } = "public";
        public string ReadWriteCommunity { get; init; } = "private";
        public PollingProtocol PollingProtocol { get; init; } = PollingProtocol.SNMP;
        public int TimeoutMs { get; init; } = 3000;
        public int Retries { get; init; } = 1;
        public int PollIntervalSec { get; init; } = 3;

        // Goto
        public string GotoSubnetName { get; init; } = "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {

        // basic numeric parse
        if (!int.TryParse(PollIntervalSec.Trim(), out var pollIntervalSec) || pollIntervalSec < 0)
        {
            MessageBox.Show(this, "Poll Interval(sec)이 올바르지 않습니다.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(PollTimeoutMs.Trim(), out var timeoutMs) || timeoutMs < 0)
        {
            MessageBox.Show(this, "Poll Timeout(ms)이 올바르지 않습니다.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(PollRetries.Trim(), out var retries) || retries < 0)
        {
            MessageBox.Show(this, "Poll Retries가 올바르지 않습니다.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ObjectType == MapObjectType.Device)
        {
            if (string.IsNullOrWhiteSpace(Address))
            {
                MessageBox.Show(this, "Device의 Address(IP/DNS)는 필수입니다.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (host, port) = ParseHostPort(Address.Trim());
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Address 형식이 올바르지 않습니다. 예: 192.168.0.10:161 또는 192.168.0.10.161", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new ParsedResult
            {
                Type = ObjectType,
                Alias = (Alias ?? "").Trim(),
                Device = (Device ?? "").Trim(),
                Icon = (IconName ?? "auto.ico").Trim(),
                Description = (Description ?? "").Trim(),
                IpOrHost = host,
                Port = port,
                SnmpVersion = ParseSnmpVersion(ReadAccessMode),
                ReadCommunity = (ReadCommunity ?? "public").Trim(),
                ReadWriteCommunity = (ReadWriteCommunity ?? "private").Trim(),
                PollingProtocol = ParsePollingProtocol(cmbPollingProtocol.SelectedItem is System.Windows.Controls.ComboBoxItem item ? item.Content?.ToString() : "SNMP"),
                TimeoutMs = timeoutMs,
                Retries = retries,
                PollIntervalSec = pollIntervalSec,
            };
        }
        else if (ObjectType == MapObjectType.Subnet)
        {
            Result = new ParsedResult
            {
                Type = ObjectType,
                Alias = (Alias ?? "").Trim(),
                Device = "",
                Icon = (IconName ?? "auto.ico").Trim(),
                Description = (Description ?? "").Trim(),
            };
        }
        else // Goto
        {
            Result = new ParsedResult
            {
                Type = ObjectType,
                Alias = (Alias ?? "").Trim(),
                Device = "",
                Icon = (IconName ?? "auto.ico").Trim(),
                Description = (Description ?? "").Trim(),
                GotoSubnetName = (Address ?? "").Trim(), // SNMPc: Address에 점프할 Subnet 이름
            };
        }

        DialogResult = true;
        Close();
    }


    private async void Lookup_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectType != MapObjectType.Device) return;
        await LookupWithPreviewAsync();
    }

    private void Ping_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectType != MapObjectType.Device) return;
        if (string.IsNullOrWhiteSpace(Address))
        {
            LookupStatus = "Address가 비어있습니다.";
            return;
        }

        var (host, _) = ParseHostPort(Address.Trim());
        if (string.IsNullOrWhiteSpace(host))
        {
            LookupStatus = "Address 형식이 올바르지 않습니다.";
            return;
        }

        var win = new PingLogWindow(host) { Owner = this };
        win.Show();
    }

    private async Task LookupWithPreviewAsync()
    {
        if (IsLookupBusy) return;
        if (string.IsNullOrWhiteSpace(Address))
        {
            LookupStatus = "Address가 비어있습니다.";
            return;
        }

        var (host, port) = ParseHostPort(Address.Trim());
        if (string.IsNullOrWhiteSpace(host))
        {
            LookupStatus = "Address 형식이 올바르지 않습니다. 예: 192.168.0.10:161 또는 192.168.0.10.161";
            return;
        }

        _lookupCts?.Cancel();
        _lookupCts = new CancellationTokenSource();
        var token = _lookupCts.Token;

        try
        {
            IsLookupBusy = true;
            LookupStatus = "Lookup 실행 중...";

            var log = new List<string>();
            log.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Lookup start");
            log.Add($"Target: {host}:{port}");
            log.Add("");
            log.Add("== SNMP GET ==");

            var timeoutMs = int.TryParse(PollTimeoutMs.Trim(), out var t) ? t : 3000;
            var retries = int.TryParse(PollRetries.Trim(), out var retryCount) ? retryCount : 1;
            var version = ParseSnmpVersion(ReadAccessMode);
            var community = (ReadCommunity ?? "public").Trim();

            // sysDescr, sysObjectID, sysName
            var oids = new[]
            {
                "1.3.6.1.2.1.1.1.0",
                "1.3.6.1.2.1.1.2.0",
                "1.3.6.1.2.1.1.5.0",
            };

            var target = new Models.UiSnmpTarget
            {
                IpAddress = host,
                Port = port,
                Community = community,
                Version = version,
                Timeout = timeoutMs,
                Retries = retries
            };

            log.Add($"snmp.get host={host}:{port} version={version} community={community} timeout={timeoutMs}ms retries={retries}");
            log.Add($"oids: {string.Join(", ", oids)}");
            log.Add("");

            if (_snmpClient is null)
            {
                log.Add("SNMP client not injected -> cannot SNMP GET");
                log.Add("");
                log.Add("== PING (SNMP failure diagnostics) ==");
                log.Add("NOTE: Ping Fail은 대상/방화벽에서 ICMP 차단이어도 정상일 수 있습니다.");
                await AppendPingDiagnosticsAsync(log, host, token);

                var preview = new LookupPreviewDialog(host) { Owner = this };
                preview.LogText = string.Join(Environment.NewLine, log);
                preview.ProposedName = string.IsNullOrWhiteSpace(Alias) ? host : Alias;
                preview.ProposedDescription = string.IsNullOrWhiteSpace(Description) ? "" : Description;

                if (preview.ShowDialog() == true)
                {
                    Alias = (preview.ProposedName ?? "").Trim();
                    Device = (preview.ProposedName ?? "").Trim();
                    Description = preview.ProposedDescription ?? "";
                    OnPropertyChanged(nameof(Description));
                    LookupStatus = "Lookup OK (사용자 적용됨 / SNMP client 미주입)";
                }
                else
                {
                    LookupStatus = "Lookup 완료 (미적용)";
                }
                return;
            }

            var res = await _snmpClient.GetAsync(target, oids);
            if (token.IsCancellationRequested) return;

            if (!res.IsSuccess)
            {
                log.Add($"SNMP FAIL: {res.ErrorMessage}");
                log.Add("");
                log.Add("== PING (SNMP failure diagnostics) ==");
                log.Add("NOTE: Ping Fail은 대상/방화벽에서 ICMP 차단이어도 정상일 수 있습니다.");
                await AppendPingDiagnosticsAsync(log, host, token);

                var previewFail = new LookupPreviewDialog(host) { Owner = this };
                previewFail.LogText = string.Join(Environment.NewLine, log);
                previewFail.ProposedName = string.IsNullOrWhiteSpace(Alias) ? host : Alias;
                previewFail.ProposedDescription = string.IsNullOrWhiteSpace(Description) ? "" : Description;

                if (previewFail.ShowDialog() == true)
                {
                    Alias = (previewFail.ProposedName ?? "").Trim();
                    Device = (previewFail.ProposedName ?? "").Trim();
                    Description = previewFail.ProposedDescription ?? "";
                    OnPropertyChanged(nameof(Description));
                    LookupStatus = "Lookup OK (사용자 적용됨) / SNMP FAIL";
                }
                else
                {
                    LookupStatus = "Lookup 완료 (미적용) / SNMP FAIL";
                }
                return;
            }

            string? sysDescr = null;
            string? sysObjectId = null;
            string? sysName = null;

            foreach (var v in res.Variables)
            {
                if (v.Oid == "1.3.6.1.2.1.1.1.0") sysDescr = v.Value;
                else if (v.Oid == "1.3.6.1.2.1.1.2.0") sysObjectId = v.Value;
                else if (v.Oid == "1.3.6.1.2.1.1.5.0") sysName = v.Value;
            }

            log.Add("SNMP OK:");
            log.Add($"  sysDescr: {sysDescr}");
            log.Add($"  sysObjectID: {sysObjectId}");
            log.Add($"  sysName: {sysName}");

            // sysName을 Alias와 Device에 채우기
            var deviceName = !string.IsNullOrWhiteSpace(sysName) ? sysName.Trim() : host;
            
            var descLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(sysDescr)) descLines.Add(sysDescr.Trim());
            if (!string.IsNullOrWhiteSpace(sysObjectId)) descLines.Add($"sysObjectID: {sysObjectId.Trim()}");
            if (!string.IsNullOrWhiteSpace(sysName)) descLines.Add($"sysName: {sysName.Trim()}");
            var proposedDesc = descLines.Count > 0 ? string.Join(Environment.NewLine, descLines) : "";

            // Icon은 일단 auto.ico 유지(후속: sysObjectID 기반 자동 아이콘 매핑)
            if (string.IsNullOrWhiteSpace(IconName)) IconName = "auto.ico";

            var previewOk = new LookupPreviewDialog(host) { Owner = this };
            previewOk.LogText = string.Join(Environment.NewLine, log);
            previewOk.ProposedName = deviceName;
            previewOk.ProposedDescription = proposedDesc;

            if (previewOk.ShowDialog() == true)
            {
                // sysName을 Alias와 Device에 채우기
                Alias = deviceName;
                Device = deviceName;
                Description = proposedDesc;
                OnPropertyChanged(nameof(Description));
                LookupStatus = "Lookup OK (사용자 적용됨) / SNMP OK";
            }
            else
            {
                LookupStatus = "Lookup 완료 (미적용) / SNMP OK";
            }
        }
        catch (Exception ex)
        {
            LookupStatus = $"Lookup 오류: {ex.Message}";
        }
        finally
        {
            IsLookupBusy = false;
        }
    }

    private static async Task AppendPingDiagnosticsAsync(List<string> log, string host, CancellationToken token)
    {
        var pingOk = false;
        for (var i = 1; i <= 4; i++)
        {
            log.Add($"ping[{i}] host={host} timeout=1200ms");
            var pingRes = await TryPingDetailAsync(host, timeoutMs: 1200, token);
            if (pingRes.IsSuccess)
            {
                pingOk = true;
                log.Add($"  -> OK (Status={pingRes.Status}, RTT={pingRes.RoundtripMs}ms)");
            }
            else
            {
                var err = string.IsNullOrWhiteSpace(pingRes.Error) ? "" : $" / Error={pingRes.Error}";
                var rtt = pingRes.RoundtripMs.HasValue ? $" / RTT={pingRes.RoundtripMs}ms" : "";
                log.Add($"  -> Fail (Status={pingRes.Status}{rtt}{err})");
            }

            if (i < 4) await Task.Delay(200, token);
        }

        log.Add($"ping.summary: {(pingOk ? "OK" : "Fail")}");
    }

    private readonly record struct PingAttemptResult(bool IsSuccess, IPStatus? Status, long? RoundtripMs, string? Error);

    private static async Task<PingAttemptResult> TryPingDetailAsync(string host, int timeoutMs, CancellationToken token)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            token.ThrowIfCancellationRequested();
            return new PingAttemptResult(reply.Status == IPStatus.Success, reply.Status, reply.RoundtripTime, null);
        }
        catch (Exception ex)
        {
            return new PingAttemptResult(false, null, null, ex.Message);
        }
    }

    private static SnmpVersion ParseSnmpVersion(string? mode)
    {
        var m = (mode ?? "").ToUpperInvariant();
        if (m.Contains("V1")) return SnmpVersion.V1;
        if (m.Contains("V3")) return SnmpVersion.V3;
        return SnmpVersion.V2c;
    }

    private static Core.Models.PollingProtocol ParsePollingProtocol(string? protocolStr)
    {
        var p = (protocolStr ?? "").ToUpperInvariant();
        if (p.Contains("PING")) return Core.Models.PollingProtocol.Ping;
        if (p.Contains("ARP")) return Core.Models.PollingProtocol.ARP;
        if (p.Contains("NONE")) return Core.Models.PollingProtocol.None;
        return Core.Models.PollingProtocol.SNMP;
    }

    private static (string host, int port) ParseHostPort(string input)
    {
        // allow "x.x.x.x:161"
        var colon = input.LastIndexOf(':');
        if (colon > 0 && colon < input.Length - 1)
        {
            var host = input[..colon].Trim();
            if (int.TryParse(input[(colon + 1)..], out var port) && port is > 0 and <= 65535)
                return (host, port);
            return (host, 161);
        }

        // allow "x.x.x.x.Port" where x.x.x.x is a valid IPv4 and Port is 1..65535 (SNMPc 문서 스타일)
        // NOTE: "192.168.0.100" 같은 일반 IPv4는 절대 여기서 포트로 오인하면 안 됨.
        var ipPort = Regex.Match(input.Trim(), @"^(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})\.(?<port>\d{1,5})$");
        if (ipPort.Success)
        {
            if (int.TryParse(ipPort.Groups["a"].Value, out var a) &&
                int.TryParse(ipPort.Groups["b"].Value, out var b) &&
                int.TryParse(ipPort.Groups["c"].Value, out var c) &&
                int.TryParse(ipPort.Groups["d"].Value, out var d) &&
                int.TryParse(ipPort.Groups["port"].Value, out var port) &&
                a is >= 0 and <= 255 &&
                b is >= 0 and <= 255 &&
                c is >= 0 and <= 255 &&
                d is >= 0 and <= 255 &&
                port is > 0 and <= 65535)
            {
                var host = $"{a}.{b}.{c}.{d}";
                return (host, port);
            }
        }

        return (input.Trim(), 161);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    private async void GetTrapInfo_Click(object sender, RoutedEventArgs e)
    {
        if (_snmpClient == null || ObjectType != MapObjectType.Device)
        {
            MessageBox.Show("Trap information retrieval is only available for Device objects.", "Invalid Object Type", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await LoadTrapInfoAsync();
    }

    private async Task LoadTrapInfoAsync()
    {
        if (_snmpClient == null) return;

        var (host, port) = ParseHostPort(Address);
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        try
        {
            var target = new UiSnmpTarget
            {
                IpAddress = host,
                Port = port,
                Community = ReadCommunity, // Read Community 사용
                Version = ParseSnmpVersion(ReadAccessMode),
                Timeout = int.TryParse(PollTimeoutMs, out var timeout) ? timeout : 3000,
                Retries = int.TryParse(PollRetries, out var retries) ? retries : 1
            };

            // 표준 SNMP Trap Destination OID 시도
            // 1.3.6.1.6.3.18.1.3.0 (snmpTrapAddress)
            var trapOid = "1.3.6.1.6.3.18.1.3.0";
            var result = await _snmpClient.GetAsync(target, trapOid);

            if (result.IsSuccess && result.Variables.Count > 0)
            {
                var trapAddress = result.Variables[0].Value;
                if (!string.IsNullOrWhiteSpace(trapAddress) && IPAddress.TryParse(trapAddress, out _))
                {
                    TrapDestinationIp = trapAddress;
                    
                    // Trap 정보 텍스트 업데이트
                    if (txtTrapInfo != null)
                    {
                        txtTrapInfo.Text = $"Trap Destination retrieved successfully!\n\n" +
                                         $"Device: {host}\n" +
                                         $"Trap Destination IP: {trapAddress}\n" +
                                         $"Trap Destination Port: {TrapDestinationPort}\n" +
                                         $"\nRetrieved at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    }
                    
                    MessageBox.Show($"Trap Destination retrieved successfully!\n\nDevice: {host}\nTrap Destination IP: {trapAddress}", 
                        "Trap Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var message = $"Trap Destination retrieved but value is invalid or empty.\n\nDevice: {host}\nValue: {trapAddress}";
                    if (txtTrapInfo != null)
                    {
                        txtTrapInfo.Text = message + $"\n\nRetrieved at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    }
                    MessageBox.Show(message, "Trap Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                var errorMsg = result.ErrorMessage ?? "Unknown error";
                var hint = errorMsg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                    ? "\n\nPossible causes:\n• Device does not support SNMP GET for this OID\n• Read Community is incorrect\n• Firewall is blocking the request"
                    : "\n\nNote: This device may not support the standard SNMP Trap OID (1.3.6.1.6.3.18.1.3.0), or the OID may not be configured.";
                
                var message = $"Failed to retrieve Trap Destination.\n\nError: {errorMsg}{hint}";
                if (txtTrapInfo != null)
                {
                    txtTrapInfo.Text = message + $"\n\nAttempted at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                }
                MessageBox.Show(message, "Trap Information", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error retrieving Trap Destination: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ConfigureTrap_Click(object sender, RoutedEventArgs e)
    {
        if (_snmpClient == null || ObjectType != MapObjectType.Device)
        {
            MessageBox.Show("Trap configuration is only available for Device objects.", "Invalid Object Type", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (host, port) = ParseHostPort(Address);
        if (string.IsNullOrWhiteSpace(host))
        {
            MessageBox.Show("Please enter a valid device address first.", "Invalid Address", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!IPAddress.TryParse(TrapDestinationIp, out _))
        {
            MessageBox.Show("Please enter a valid Trap Destination IP address.", "Invalid IP", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TrapDestinationPort, out int trapPort) || trapPort <= 0 || trapPort > 65535)
        {
            MessageBox.Show("Please enter a valid Trap Destination Port (1-65535).", "Invalid Port", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var target = new UiSnmpTarget
            {
                IpAddress = host,
                Port = port,
                Community = ReadWriteCommunity, // Write Community 사용
                Version = ParseSnmpVersion(ReadWriteAccessMode),
                Timeout = int.TryParse(PollTimeoutMs, out var timeout) ? timeout : 3000,
                Retries = int.TryParse(PollRetries, out var retries) ? retries : 1
            };

            // 표준 SNMP Trap Destination OID 시도
            // MVE5000/MVD5000 전용 설정은 개별 편집에서 처리 (향후 구현)
            // 1.3.6.1.6.3.18.1.3.0 (snmpTrapAddress) - 표준이지만 모든 장비에서 지원하지 않음
            var trapOid = "1.3.6.1.6.3.18.1.3.0";
            var result = await _snmpClient.SetAsync(target, trapOid, TrapDestinationIp, "IPADDRESS");

            if (result.IsSuccess)
            {
                MessageBox.Show($"Trap Destination configured successfully!\n\nDevice: {host}\nTrap Destination: {TrapDestinationIp}:{TrapDestinationPort}", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var errorMsg = result.ErrorMessage ?? "Unknown error";
                var hint = errorMsg.Contains("timed out", StringComparison.OrdinalIgnoreCase) 
                    ? "\n\nPossible causes:\n• Device does not support SNMP SET\n• Write Community is incorrect\n• Firewall is blocking the request"
                    : "\n\nNote: This device may not support the standard SNMP Trap OID (1.3.6.1.6.3.18.1.3.0), or Write Community may be incorrect.";
                
                MessageBox.Show($"Failed to configure Trap Destination.\n\nError: {errorMsg}{hint}", 
                    "Configuration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error configuring Trap Destination: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetLocalNetworkIp()
    {
        // Trap Listener가 있으면 그것을 사용
        if (_trapListener != null)
        {
            return _trapListener.GetLocalNetworkIp();
        }

        // 로컬 네트워크 IP 주소 찾기 (127.0.0.1 제외)
        string localIP = "127.0.0.1";
        try
        {
            // 활성 네트워크 인터페이스에서 IPv4 주소 찾기
            // 우선순위: Ethernet > Wireless > 기타
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .ThenByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            foreach (var ni in networkInterfaces)
            {
                var ipProps = ni.GetIPProperties();
                var ipv4Address = ipProps.UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                                           !IPAddress.IsLoopback(addr.Address));
                
                if (ipv4Address != null)
                {
                    localIP = ipv4Address.Address.ToString();
                    break; // 첫 번째 유효한 IP 주소 사용
                }
            }
        }
        catch
        {
            // 실패 시 기본값 사용 (127.0.0.1)
        }

        return localIP;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        // IsConfigureTrapEnabled 업데이트
        if (propertyName == nameof(Address))
        {
            OnPropertyChanged(nameof(IsConfigureTrapEnabled));
        }
    }
}


