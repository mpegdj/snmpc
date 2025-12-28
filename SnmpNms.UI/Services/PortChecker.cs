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
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
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
    /// 포트 161과 162 상태 확인 및 권한 요청
    /// </summary>
    public static PortCheckResult CheckPortsAndRequestPermission()
    {
        var result = new PortCheckResult();
        
        // 포트 161 체크 (SNMP 클라이언트 - 일반적으로 서버 측에서 LISTEN)
        result.Port161Listening = IsPortListening(161, ProtocolType.Udp);
        
        // 포트 162 체크 (Trap Listener - LISTEN 상태여야 함)
        result.Port162Listening = IsPortListening(162, ProtocolType.Udp);
        
        result.IsAdmin = IsRunningAsAdministrator();
        
        // 포트 161 또는 162가 LISTEN 상태가 아니고 관리자 권한이 없으면 권한 요청
        if ((!result.Port161Listening || !result.Port162Listening) && !result.IsAdmin)
        {
            var ports = new List<string>();
            if (!result.Port161Listening) ports.Add("161 (SNMP)");
            if (!result.Port162Listening) ports.Add("162 (SNMP Trap)");
            
            var response = MessageBox.Show(
                $"포트 {string.Join(", ", ports)}를 열려면 관리자 권한이 필요합니다.\n\n" +
                "관리자 권한으로 재시작하시겠습니까?",
                "관리자 권한 필요",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (response == MessageBoxResult.Yes)
            {
                RestartAsAdministrator();
                result.RequestedRestart = true;
            }
        }
        
        return result;
    }
}

/// <summary>
/// 포트 체크 결과
/// </summary>
public class PortCheckResult
{
    public bool Port161Listening { get; set; }
    public bool Port162Listening { get; set; }
    public bool IsAdmin { get; set; }
    public bool RequestedRestart { get; set; }
}

