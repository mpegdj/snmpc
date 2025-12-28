using System.Net;
using System.Threading;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpResult = SnmpNms.Core.Models.SnmpResult;
using SnmpVariable = SnmpNms.Core.Models.SnmpVariable;
using VersionCode = Lextm.SharpSnmpLib.VersionCode;

namespace SnmpNms.Infrastructure;

public class SnmpClient : ISnmpClient
{
    public event Action? OnRequestSent;
    public event Action<bool>? OnResponseReceived;

    private void FireRequestSent()
    {
        OnRequestSent?.Invoke();
    }

    public async Task<SnmpResult> GetAsync(ISnmpTarget target, string oid)
    {
        return await GetAsync(target, new[] { oid });
    }

    public async Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids)
    {
        return await Task.Run(() =>
        {
            FireRequestSent();
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(target.IpAddress), target.Port);
                var community = new OctetString(target.Community);
                var variables = oids.Select(o => new Variable(new ObjectIdentifier(o))).ToList();
                var version = MapVersion(target.Version);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var result = Messenger.Get(version, endpoint, community, variables, target.Timeout);
                
                stopwatch.Stop();

                var snmpVariables = result.Select(MapVariable).ToList();
                OnResponseReceived?.Invoke(true);
                return SnmpResult.Success(snmpVariables, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                OnResponseReceived?.Invoke(false);
                return SnmpResult.Fail(ex.Message);
            }
        });
    }

    public async Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid)
    {
         return await Task.Run(() =>
        {
            FireRequestSent();
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(target.IpAddress), target.Port);
                var community = new OctetString(target.Community);
                var variables = new List<Variable> { new Variable(new ObjectIdentifier(oid)) };
                var version = MapVersion(target.Version);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // GetNextRequestMessage를 사용하여 직접 요청 (Messenger.GetNext가 구버전에는 없거나 사용법이 다를 수 있으므로 확인 필요하지만, 일반적으로 Messenger 클래스 활용 권장)
                // 하지만 Messenger에는 Get만 있고 GetNext 헬퍼가 명시적이지 않을 수 있음.
                // 안전하게 GetNextRequestMessage를 생성해서 보내는 방식 사용 가능.
                // 또는 Messenger.GetNext 메서드가 있는지 확인. 
                // SharpSnmpLib 10+ 에서는 Messenger.GetNext 메서드가 없을 수 있고 Walk 모드가 주로 쓰임.
                // 여기서는 Walk가 아닌 단일 GetNext가 필요하므로 저수준 API 사용.
                
                // 간단하게 구현하기 위해 Walk를 1개만 요청하는 방식으로 우회하거나 직접 메시지 전송.
                // 여기서는 직접 메시지 전송 방식으로 구현.
                
                GetNextRequestMessage message = new GetNextRequestMessage(
                    0,
                    version,
                    community,
                    variables
                );

                var response = message.GetResponse(target.Timeout, endpoint);
                
                stopwatch.Stop();

                var snmpVariables = response.Pdu().Variables.Select(MapVariable).ToList();
                OnResponseReceived?.Invoke(true);
                return SnmpResult.Success(snmpVariables, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                OnResponseReceived?.Invoke(false);
                return SnmpResult.Fail(ex.Message);
            }
        });
    }

    public async Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            FireRequestSent();
            try
            {
                // 취소 토큰 체크
                cancellationToken.ThrowIfCancellationRequested();

                var endpoint = new IPEndPoint(IPAddress.Parse(target.IpAddress), target.Port);
                var community = new OctetString(target.Community);
                var rootParams = new ObjectIdentifier(rootOid);
                var version = MapVersion(target.Version);
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var result = new List<Variable>();
                
                // WalkMode.WithinSubtree: 지정된 OID의 하위 트리만 순회 (같은 레벨의 다음 OID로 넘어가지 않음)
                // - 1.3.6.1.2.1.1에서 시작하면 1.3.6.1.2.1.1.x만 가져옴 (1.3.6.1.2.1.2로 넘어가지 않음)
                // - 하위 OID가 있으면: 하위로 내려가서 모든 하위 OID를 가져옴
                // - 하위 OID가 없으면: 빈 결과 반환 (리프 노드)
                // 
                // 주의: Messenger.Walk는 동기 메서드이고 CancellationToken을 직접 지원하지 않음
                // 취소는 Walk 완료 후 체크하거나, 별도 스레드에서 실행하여 취소 처리
                Messenger.Walk(version, endpoint, community, rootParams, result, target.Timeout, WalkMode.WithinSubtree);
                
                // Walk 완료 후 취소 체크
                cancellationToken.ThrowIfCancellationRequested();
                
                // WithinSubtree 모드에서는 하위가 없는 경우 빈 결과가 반환될 수 있음
                // 이 경우 현재 OID를 직접 조회하여 스칼라 값 가져오기
                if (result.Count == 0)
                {
                    try
                    {
                        var getResult = Messenger.Get(version, endpoint, community, new[] { new Variable(rootParams) }, target.Timeout);
                        if (getResult != null && getResult.Count > 0)
                        {
                            result.AddRange(getResult);
                        }
                    }
                    catch
                    {
                        // Get 실패 시 무시 (하위가 없는 노드일 수 있음)
                    }
                }
                
                stopwatch.Stop();

                var snmpVariables = result.Select(MapVariable).ToList();
                
                // 디버깅: Walk 결과 확인
                System.Diagnostics.Debug.WriteLine($"WalkAsync: OID={rootOid}, Count={snmpVariables.Count}, Time={stopwatch.ElapsedMilliseconds}ms");
                if (snmpVariables.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  First: {snmpVariables[0].Oid}");
                    System.Diagnostics.Debug.WriteLine($"  Last: {snmpVariables[snmpVariables.Count - 1].Oid}");
                }
                
                return SnmpResult.Success(snmpVariables, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"WalkAsync Cancelled: OID={rootOid}");
                return SnmpResult.Fail("Walk cancelled by user");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WalkAsync Exception: {ex.Message}\n{ex.StackTrace}");
                return SnmpResult.Fail(ex.Message);
            }
        }, cancellationToken);
    }

    private VersionCode MapVersion(SnmpVersion version)
    {
        return version switch
        {
            SnmpVersion.V1 => VersionCode.V1,
            SnmpVersion.V2c => VersionCode.V2,
            SnmpVersion.V3 => VersionCode.V3,
            _ => VersionCode.V2
        };
    }

    public async Task<SnmpResult> SetAsync(ISnmpTarget target, string oid, string value, string type)
    {
        return await Task.Run(() =>
        {
            FireRequestSent();
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(target.IpAddress), target.Port);
                var community = new OctetString(target.Community);
                var version = MapVersion(target.Version);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // 타입에 따라 ISnmpData 생성
                ISnmpData data = type.ToUpper() switch
                {
                    "INTEGER" or "INTEGER32" => new Integer32(int.Parse(value)),
                    "OCTETSTRING" or "STRING" => new OctetString(value),
                    "IPADDRESS" => new IP(IPAddress.Parse(value).GetAddressBytes()),
                    "COUNTER32" => new Counter32(uint.Parse(value)),
                    "COUNTER64" => new Counter64(ulong.Parse(value)),
                    "GAUGE32" => new Gauge32(uint.Parse(value)),
                    "TIMETICKS" => new TimeTicks(uint.Parse(value)),
                    "OBJECTIDENTIFIER" or "OID" => new ObjectIdentifier(value),
                    _ => new OctetString(value) // 기본값: 문자열
                };

                var variable = new Variable(new ObjectIdentifier(oid), data);
                var result = Messenger.Set(version, endpoint, community, new List<Variable> { variable }, target.Timeout);

                stopwatch.Stop();

                var snmpVariables = result.Select(MapVariable).ToList();
                return SnmpResult.Success(snmpVariables, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return SnmpResult.Fail(ex.Message);
            }
        });
    }

    private SnmpVariable MapVariable(Variable v)
    {
        return new SnmpVariable(v.Id.ToString(), v.Data.ToString(), v.Data.TypeCode.ToString());
    }
}

