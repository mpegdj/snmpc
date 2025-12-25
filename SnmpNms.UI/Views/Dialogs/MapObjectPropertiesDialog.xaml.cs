using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using SnmpNms.Core.Interfaces;
using System.Windows;
using SnmpNms.Core.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Views.Dialogs;

public partial class MapObjectPropertiesDialog : Window, INotifyPropertyChanged
{
    private readonly ISnmpClient? _snmpClient;
    private CancellationTokenSource? _lookupCts;

    public MapObjectPropertiesDialog(MapObjectType type, ISnmpClient? snmpClient = null)
    {
        ObjectType = type;
        _snmpClient = snmpClient;
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

        DataContext = this;
        InitializeComponent();
        
        // Address 입력창 초기화
        if (ObjectType == MapObjectType.Device)
        {
            SyncAddressToInputs();
        }
    }

    public MapObjectPropertiesDialog(MapObjectType type, UiSnmpTarget target, ISnmpClient? snmpClient = null) : this(type, snmpClient)
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
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


