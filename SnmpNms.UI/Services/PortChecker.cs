using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;

namespace SnmpNms.UI.Services;

/// <summary>
/// 포트 상태 확인 및 권한 체크 서비스
/// </summary>
public static class PortChecker
{
    /// <summary>
    /// 포트가 사용 가능한지 확인 (LISTEN 상태)
    /// </summary>
    public static bool IsPortAvailable(int port)
    {
        try
        {
            // UDP 포트 체크
            using (var udpClient = new UdpClient(port))
            {
                return true;
            }
        }
        catch (SocketException)
        {
            // 포트가 이미 사용 중이거나 권한이 없음
            return false;
        }
    }
    
    /// <summary>
    /// 포트가 LISTEN 상태인지 확인 (netstat 방식)
    /// </summary>
    public static bool IsPortListening(int port, ProtocolType protocolType = ProtocolType.Udp)
    {
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            
            if (protocolType == ProtocolType.Udp)
            {
                var udpListeners = properties.GetActiveUdpListeners();
                return udpListeners.Any(endpoint => endpoint.Port == port);
            }
            else
            {
                var tcpListeners = properties.GetActiveTcpListeners();
                return tcpListeners.Any(endpoint => endpoint.Port == port);
            }
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 관리자 권한으로 실행 중인지 확인
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 관리자 권한으로 재시작
    /// </summary>
    public static void RestartAsAdministrator()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            }
            
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = exePath,
                Verb = "runas" // UAC 프롬프트 표시
            };
            
            Process.Start(startInfo);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"관리자 권한으로 재시작할 수 없습니다:\n{ex.Message}",
                "권한 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    
    /// <summary>
    /// 포트 161과 162 상태 확인 및 설정 필요 여부 판단
    /// </summary>
    public static PortCheckResult CheckPortsAndSetup()
    {
        var result = new PortCheckResult();
        result.IsAdmin = IsRunningAsAdministrator();
        
        // 1. 방화벽 규칙 확인 (162번 위주)
        result.Firewall161Registered = VerifyFirewallRule(161);
        result.Firewall162Registered = VerifyFirewallRule(162);
        
        // 2. 포트 상태 체크
        result.Port161Listening = IsPortListening(161, ProtocolType.Udp);
        result.Port162Listening = IsPortListening(162, ProtocolType.Udp);
        
        // 3. 관리자 권한이 필요한지 결정
        // 162번 방화벽 규칙이 없고 아직 관리자가 아니라면 권한 요청이 필요함
        if (!result.Firewall162Registered && !result.IsAdmin)
        {
            result.RequestedRestart = true;
        }
        else if (result.IsAdmin)
        {
            // 이미 관리자라면 누락된 규칙 자동 등록 시도
            EnsureFirewallRules();
            // 등록 후 다시 확인
            result.Firewall161Registered = VerifyFirewallRule(161);
            result.Firewall162Registered = VerifyFirewallRule(162);
        }
        
        return result;
    }

    /// <summary>
    /// SNMP 및 Trap 포트를 윈도우 방화벽에 허용 규칙으로 추가
    /// </summary>
    public static void EnsureFirewallRules()
    {
        try
        {
            // netsh를 사용하여 방화벽 규칙 추가 (이미 있으면 무시되거나 업데이트됨)
            // SNMP (UDP 161)
            RunNetshCommand("advfirewall firewall add rule name=\"SnmpNms - SNMP (UDP 161)\" dir=in action=allow protocol=UDP localport=161 profile=any");
            // Trap (UDP 162)
            RunNetshCommand("advfirewall firewall add rule name=\"SnmpNms - SNMP Trap (UDP 162)\" dir=in action=allow protocol=UDP localport=162 profile=any");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"방화벽 규칙 추가 중 오류 발생: {ex.Message}");
        }
    }

    /// <summary>
    /// 특정 포트에 대한 방화벽 규칙이 존재하는지 확인
    /// </summary>
    public static bool VerifyFirewallRule(int port)
    {
        try
        {
            var ruleName = port == 161 ? "SnmpNms - SNMP (UDP 161)" : "SnmpNms - SNMP Trap (UDP 162)";
            
            // netsh advfirewall firewall show rule 은 규칙이 없을 때 "no rules match" 등을 출력하며
            // ExitCode가 1이 되는 경우가 많습니다. 0인 경우라도 "찾을 수 없습니다" 등의 문자열을 체크하는 것이 안전합니다.
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            
            using var process = Process.Start(startInfo);
            if (process == null) return false;
            
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            // 출력 결과에 규칙 이름과 포트 번호가 포함되어 있다면 등록된 것으로 간주
            return output.Contains(ruleName) && output.Contains(port.ToString());
        }
        catch
        {
            return false;
        }
    }

    private static void RunNetshCommand(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            
            // 이미 관리자 권한인 경우 Verb = "runas" 없이 실행 (깜빡임 방지 및 명확성)
            if (!IsRunningAsAdministrator())
            {
                startInfo.Verb = "runas";
            }
            
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }
        catch
        {
            // 로그 기록 또는 무시
        }
    }
}

/// <summary>
/// 포트 체크 결과
/// </summary>
public class PortCheckResult
{
    public bool Port161Listening { get; set; }
    public bool Port162Listening { get; set; }
    public bool Firewall161Registered { get; set; }
    public bool Firewall162Registered { get; set; }
    public bool IsAdmin { get; set; }
    public bool RequestedRestart { get; set; }
}

