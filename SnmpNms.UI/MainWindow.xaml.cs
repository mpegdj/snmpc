using System.IO;
using System.Windows;
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

    public MainWindow()
    {
        InitializeComponent();
        
        // DI 컨테이너 없이 수동 주입
        _snmpClient = new SnmpClient();
        _mibService = new MibService();

        // MIB 파일 로드 (Mib 폴더가 실행 파일 위치 또는 상위에 있다고 가정)
        LoadMibs();
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
