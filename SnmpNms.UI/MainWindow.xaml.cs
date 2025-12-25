using System.IO;
using System.Windows;
using System.Windows.Media;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.Infrastructure;
using SnmpNms.UI.Models;

namespace SnmpNms.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISnmpClient _snmpClient;
    private readonly IMibService _mibService;
    private readonly IPollingService _pollingService;

    public MainWindow()
    {
        InitializeComponent();
        
        // DI 컨테이너 없이 수동 주입
        _snmpClient = new SnmpClient();
        _mibService = new MibService();
        _pollingService = new PollingService(_snmpClient);

        // Polling 이벤트 연결
        _pollingService.OnPollingResult += PollingService_OnPollingResult;

        // MIB 파일 로드 (Mib 폴더가 실행 파일 위치 또는 상위에 있다고 가정)
        LoadMibs();
    }

    private void PollingService_OnPollingResult(object? sender, PollingResult e)
    {
        // UI 스레드에서 업데이트
        Dispatcher.Invoke(() =>
        {
            if (e.Status == DeviceStatus.Up)
            {
                lblStatus.Content = $"Up ({DateTime.Now:HH:mm:ss})";
                lblStatus.Foreground = Brushes.Green;
                // Polling 로그는 너무 많을 수 있으므로 상태 변경 시에만 찍거나, 별도 로그창 사용 권장
                // 여기서는 간단하게 시간 갱신
                // txtResult.AppendText($"[Poll] {e.Target.IpAddress} is Alive ({e.ResponseTime}ms)\n");
            }
            else
            {
                lblStatus.Content = $"Down ({DateTime.Now:HH:mm:ss})";
                lblStatus.Foreground = Brushes.Red;
                txtResult.AppendText($"[Poll] {e.Target.IpAddress} is Down: {e.Message}\n");
                txtResult.ScrollToEnd();
            }
        });
    }

    private void ChkAutoPoll_Checked(object sender, RoutedEventArgs e)
    {
        var target = new UiSnmpTarget
        {
            IpAddress = txtIp.Text,
            Community = txtCommunity.Text,
            Version = SnmpVersion.V2c,
            Timeout = 3000
        };

        _pollingService.AddTarget(target);
        _pollingService.Start();
        txtResult.AppendText($"[System] Auto Polling Started for {target.IpAddress}\n");
    }

    private void ChkAutoPoll_Unchecked(object sender, RoutedEventArgs e)
    {
        _pollingService.Stop();
        // 현재는 단일 타겟만 가정하여 전체 중지 후 제거 (또는 IP 기준으로 제거 가능)
        // 여기서는 간단하게 Stop만 호출하거나, 입력된 IP를 제거
        var target = new UiSnmpTarget
        {
            IpAddress = txtIp.Text,
            Port = 161 
        };
        _pollingService.RemoveTarget(target);
        
        txtResult.AppendText($"[System] Auto Polling Stopped\n");
        lblStatus.Content = "Unknown";
        lblStatus.Foreground = Brushes.Gray;
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
}
