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

    public MainWindow()
    {
        InitializeComponent();
        // 실제 애플리케이션에서는 DI 컨테이너를 사용하여 주입받는 것이 좋습니다.
        _snmpClient = new SnmpClient();
    }

    private async void BtnGet_Click(object sender, RoutedEventArgs e)
    {
        txtResult.Text = "Sending SNMP GET request...";
        btnGet.IsEnabled = false;

        try
        {
            // 화면의 입력값으로 Target 객체 생성
            var target = new UiSnmpTarget
            {
                IpAddress = txtIp.Text,
                Community = txtCommunity.Text,
                Version = SnmpVersion.V2c, // 간단하게 V2c 고정, 추후 UI 바인딩 필요
                Timeout = 3000
            };

            var oid = txtOid.Text;

            // 비동기 호출
            var result = await _snmpClient.GetAsync(target, oid);

            if (result.IsSuccess)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Success! (Time: {result.ResponseTime}ms)");
                foreach (var v in result.Variables)
                {
                    sb.AppendLine(v.ToString());
                }
                txtResult.Text = sb.ToString();
            }
            else
            {
                txtResult.Text = $"Failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            txtResult.Text = $"Error: {ex.Message}";
        }
        finally
        {
            btnGet.IsEnabled = true;
        }
    }
}
