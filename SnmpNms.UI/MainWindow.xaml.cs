using System.IO;
using System.Windows;
using System.Windows.Media;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.Infrastructure;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISnmpClient _snmpClient;
    private readonly IMibService _mibService;
    private readonly IPollingService _pollingService;
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        
        // DI 컨테이너 없이 수동 주입
        _snmpClient = new SnmpClient();
        _mibService = new MibService();
        _pollingService = new PollingService(_snmpClient);
        _vm = new MainViewModel();
        DataContext = _vm;

        // Polling 이벤트 연결
        _pollingService.OnPollingResult += PollingService_OnPollingResult;

        // MIB 파일 로드 (Mib 폴더가 실행 파일 위치 또는 상위에 있다고 가정)
        LoadMibs();

        // 기본 디바이스(샘플) 추가
        var defaultDevice = new UiSnmpTarget
        {
            IpAddress = "127.0.0.1",
            Community = "public",
            Version = SnmpVersion.V2c,
            Timeout = 3000
        };
        _vm.Devices.Add(defaultDevice);
        _vm.SelectedDevice = defaultDevice;
    }

    private void PollingService_OnPollingResult(object? sender, PollingResult e)
    {
        // UI 스레드에서 업데이트
        Dispatcher.Invoke(() =>
        {
            if (e.Status == DeviceStatus.Up)
            {
                lblStatus.Content = $"Up - {e.Target.IpAddress} ({DateTime.Now:HH:mm:ss})";
                lblStatus.Foreground = Brushes.Green;
                // Polling 로그는 너무 많을 수 있으므로 상태 변경 시에만 찍거나, 별도 로그창 사용 권장
                // 여기서는 간단하게 시간 갱신
                // txtResult.AppendText($"[Poll] {e.Target.IpAddress} is Alive ({e.ResponseTime}ms)\n");
            }
            else
            {
                lblStatus.Content = $"Down - {e.Target.IpAddress} ({DateTime.Now:HH:mm:ss})";
                lblStatus.Foreground = Brushes.Red;
                txtResult.AppendText($"[Poll] {e.Target.IpAddress} is Down: {e.Message}\n");
                txtResult.ScrollToEnd();
            }
        });
    }

    private void ChkAutoPoll_Checked(object sender, RoutedEventArgs e)
    {
        var target = BuildTargetFromInputs();

        _pollingService.AddTarget(target);
        _pollingService.Start();
        txtResult.AppendText($"[System] Auto Polling Started for {target.IpAddress}\n");
    }

    private void ChkAutoPoll_Unchecked(object sender, RoutedEventArgs e)
    {
        var target = BuildTargetFromInputs(minimal: true);
        _pollingService.RemoveTarget(target);
        _pollingService.Stop(); // 현재는 단순화를 위해 전체 Stop
        
        txtResult.AppendText($"[System] Auto Polling Stopped\n");
        lblStatus.Content = "Unknown";
        lblStatus.Foreground = Brushes.Gray;
    }

    private UiSnmpTarget BuildTargetFromInputs(bool minimal = false)
    {
        var ip = txtIp.Text?.Trim() ?? "";
        var community = txtCommunity.Text?.Trim() ?? "public";

        return new UiSnmpTarget
        {
            IpAddress = ip,
            Community = minimal ? "public" : community,
            Version = SnmpVersion.V2c,
            Timeout = 3000,
            Port = 161
        };
    }

    private void LoadMibs()
    {
        // 개발 환경 경로 하드코딩 (임시)
        // 실제 배포 시에는 AppDomain.CurrentDomain.BaseDirectory 기준 "Mib" 폴더 사용 권장
        var projectRoot = @"D:\git\snmpc\Mib"; 
        
        // 만약 경로가 없으면 실행 파일 기준 "Mib" 폴더 시도
        if (!Directory.Exists(projectRoot))
        {
            projectRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mib");
        }

        if (Directory.Exists(projectRoot))
        {
            try
            {
                _mibService.LoadMibModules(projectRoot);
                txtResult.AppendText($"[System] Loaded MIBs from {projectRoot}\n");
            }
            catch (Exception ex)
            {
                txtResult.AppendText($"[System] Failed to load MIBs: {ex.Message}\n");
            }
        }
        else
        {
            txtResult.AppendText($"[System] MIB directory not found: {projectRoot}\n");
        }
    }

    private async void BtnGet_Click(object sender, RoutedEventArgs e)
    {
        txtResult.AppendText($"\nSending SNMP GET request to {txtIp.Text}...\n");
        btnGet.IsEnabled = false;

        try
        {
            var target = new UiSnmpTarget
            {
                IpAddress = txtIp.Text,
                Community = txtCommunity.Text,
                Version = SnmpVersion.V2c,
                Timeout = 3000
            };

            var oid = txtOid.Text;

            // 이름으로 OID 검색 기능 추가 (예: "sysDescr" 입력 시 변환)
            // 숫자(.)로 시작하지 않으면 이름으로 간주
            if (!string.IsNullOrEmpty(oid) && !oid.StartsWith(".") && !char.IsDigit(oid[0]))
            {
                var convertedOid = _mibService.GetOid(oid);
                if (convertedOid != oid)
                {
                    txtResult.AppendText($"[System] Converted '{oid}' to '{convertedOid}'\n");
                    oid = convertedOid;
                }
            }

            var result = await _snmpClient.GetAsync(target, oid);

            if (result.IsSuccess)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Success! (Time: {result.ResponseTime}ms)");
                foreach (var v in result.Variables)
                {
                    // 결과 출력 시 OID -> 이름 변환 적용
                    var name = _mibService.GetOidName(v.Oid);
                    // 원래 OID가 그대로 나오면 이름 없음
                    var displayName = name == v.Oid ? v.Oid : $"{name} ({v.Oid})";
                    
                    sb.AppendLine($"{displayName} = {v.TypeCode}: {v.Value}");
                }
                txtResult.AppendText(sb.ToString());
                txtResult.ScrollToEnd();
            }
            else
            {
                txtResult.AppendText($"Failed: {result.ErrorMessage}\n");
            }
        }
        catch (Exception ex)
        {
            txtResult.AppendText($"Error: {ex.Message}\n");
        }
        finally
        {
            btnGet.IsEnabled = true;
        }
    }

    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        var ip = (txtAddIp.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            // txtIp에 값이 있으면 그걸로 추가도 허용
            ip = (txtIp.Text ?? "").Trim();
        }

        if (string.IsNullOrWhiteSpace(ip))
        {
            txtResult.AppendText("[System] AddDevice: IP is empty.\n");
            return;
        }

        // 중복 방지(동일 ip:port)
        if (_vm.Devices.Any(d => d.IpAddress == ip && d.Port == 161))
        {
            txtResult.AppendText($"[System] Device already exists: {ip}:161\n");
            return;
        }

        var dev = new UiSnmpTarget
        {
            IpAddress = ip,
            Community = (txtCommunity.Text ?? "public").Trim(),
            Version = SnmpVersion.V2c,
            Timeout = 3000,
            Port = 161
        };

        _vm.Devices.Add(dev);
        _vm.SelectedDevice = dev;
        txtResult.AppendText($"[System] Device added: {dev.DisplayName}\n");
        txtResult.ScrollToEnd();
    }

    private void RemoveDevice_Click(object sender, RoutedEventArgs e)
    {
        if (tvDevices.SelectedItem is not UiSnmpTarget selected)
        {
            txtResult.AppendText("[System] RemoveDevice: no device selected.\n");
            return;
        }

        _pollingService.RemoveTarget(selected);
        _vm.Devices.Remove(selected);
        _vm.SelectedDevice = _vm.Devices.FirstOrDefault();

        txtResult.AppendText($"[System] Device removed: {selected.DisplayName}\n");
        txtResult.ScrollToEnd();
    }

    private void TvDevices_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not UiSnmpTarget dev) return;

        _vm.SelectedDevice = dev;
        txtIp.Text = dev.IpAddress;
        txtCommunity.Text = dev.Community;

        txtResult.AppendText($"[System] Selected device: {dev.DisplayName}\n");
        txtResult.ScrollToEnd();
    }

    private void StartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDevice is null)
        {
            txtResult.AppendText("[System] StartPoll: no device selected.\n");
            return;
        }

        _pollingService.AddTarget(_vm.SelectedDevice);
        _pollingService.Start();
        chkAutoPoll.IsChecked = true;
        txtResult.AppendText($"[System] Polling started for {_vm.SelectedDevice.DisplayName}\n");
        txtResult.ScrollToEnd();
    }

    private void StopPoll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDevice is not null)
        {
            _pollingService.RemoveTarget(_vm.SelectedDevice);
        }
        _pollingService.Stop();
        chkAutoPoll.IsChecked = false;
        txtResult.AppendText("[System] Polling stopped\n");
        txtResult.ScrollToEnd();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        txtResult.Clear();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        txtResult.AppendText("[System] Refresh requested\n");
        txtResult.ScrollToEnd();
    }

    private void MenuSnmpTest_Click(object sender, RoutedEventArgs e)
    {
        tabMain.SelectedIndex = 5; // SNMP Test 탭(현재 순서 기준)
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "SnmpNms (WPF)\nSNMPc 스타일 NMS를 목표로 하는 프로젝트입니다.",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuWindowsCascade_Click(object sender, RoutedEventArgs e)
    {
        // SNMPc 스타일: View Window Area 내부 창 정렬(Cascade)
        // 현재는 Map View 탭 내부에서 겹치는 내부 창을 제공한다.
        mapViewControl?.CascadeWindows();
    }
}
