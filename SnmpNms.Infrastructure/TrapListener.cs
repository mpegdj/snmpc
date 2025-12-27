using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using VersionCode = Lextm.SharpSnmpLib.VersionCode;

namespace SnmpNms.Infrastructure;

public class TrapListener : ITrapListener
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listeningTask;
    private int _port;

    public bool IsListening => _udpClient != null && _listeningTask != null && !_listeningTask.IsCompleted;

    public event EventHandler<TrapEvent>? OnTrapReceived;

    public void Start(int port = 162)
    {
        if (IsListening)
        {
            Stop();
        }

        _port = port;
        _cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            _udpClient = new UdpClient(port);
            _listeningTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"Failed to start Trap Listener on port {port}: {ex.Message}", ex);
        }
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        
        try
        {
            _udpClient?.Close();
        }
        catch
        {
            // 무시
        }
        
        _udpClient = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        
        try
        {
            _listeningTask?.Wait(1000);
        }
        catch
        {
            // 무시
        }
        
        _listeningTask = null;
    }

    public (string ipAddress, int port) GetListenerInfo()
    {
        if (!IsListening)
        {
            return (GetLocalNetworkIp(), 162); // Trap Listener가 실행되지 않아도 실제 IP 주소 반환
        }

        return (GetLocalNetworkIp(), _port);
    }

    public string GetLocalNetworkIp()
    {
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

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        if (_udpClient == null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(cancellationToken);
                ProcessTrap(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // 에러 발생 시 이벤트 발생
                OnTrapReceived?.Invoke(this, new TrapEvent(
                    "0.0.0.0",
                    0,
                    SnmpVersion.V2c,
                    errorMessage: $"Trap receive error: {ex.Message}"));
            }
        }
    }

    private void ProcessTrap(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        try
        {
            var messages = MessageFactory.ParseMessages(buffer, 0, buffer.Length, null!);
            if (messages == null || messages.Count == 0)
            {
                throw new InvalidOperationException("No message parsed from trap data");
            }
            
            var message = messages[0];
            
            SnmpVersion version = message.Version switch
            {
                VersionCode.V1 => SnmpVersion.V1,
                VersionCode.V2 => SnmpVersion.V2c,
                VersionCode.V3 => SnmpVersion.V3,
                _ => SnmpVersion.V2c
            };

            string? community = null;
            if (message.Parameters != null)
            {
                community = message.Parameters.UserName?.ToString();
            }

            string? enterpriseOid = null;
            string? genericTrapType = null;
            string? specificTrapType = null;
            List<SnmpVariable> variables = new();

            if (message.Pdu() is TrapV1Pdu trapV1)
            {
                enterpriseOid = trapV1.Enterprise.ToString();
                genericTrapType = trapV1.Generic.ToString();
                specificTrapType = trapV1.Specific.ToString();
                
                foreach (var v in trapV1.Variables)
                {
                    variables.Add(new SnmpVariable(
                        v.Id.ToString(),
                        v.Data.ToString(),
                        v.Data.TypeCode.ToString()));
                }
            }
            else if (message.Pdu() is TrapV2Pdu trapV2)
            {
                // SNMPv2c Trap은 첫 번째 변수가 sysUpTime, 두 번째가 snmpTrapOID
                foreach (var v in trapV2.Variables)
                {
                    variables.Add(new SnmpVariable(
                        v.Id.ToString(),
                        v.Data.ToString(),
                        v.Data.TypeCode.ToString()));
                }
            }
            else if (message.Pdu() is InformRequestPdu inform)
            {
                // Inform도 Trap과 유사하게 처리
                foreach (var v in inform.Variables)
                {
                    variables.Add(new SnmpVariable(
                        v.Id.ToString(),
                        v.Data.ToString(),
                        v.Data.TypeCode.ToString()));
                }
            }
            else
            {
                // 기타 PDU 타입
                var pdu = message.Pdu();
                if (pdu != null)
                {
                    foreach (var v in pdu.Variables)
                    {
                        variables.Add(new SnmpVariable(
                            v.Id.ToString(),
                            v.Data.ToString(),
                            v.Data.TypeCode.ToString()));
                    }
                }
            }

            var trapEvent = new TrapEvent(
                remoteEndPoint.Address.ToString(),
                remoteEndPoint.Port,
                version,
                community,
                enterpriseOid,
                genericTrapType,
                specificTrapType,
                variables);

            OnTrapReceived?.Invoke(this, trapEvent);
        }
        catch (Exception ex)
        {
            OnTrapReceived?.Invoke(this, new TrapEvent(
                remoteEndPoint.Address.ToString(),
                remoteEndPoint.Port,
                SnmpVersion.V2c,
                errorMessage: $"Trap parse error: {ex.Message}"));
        }
    }
}


