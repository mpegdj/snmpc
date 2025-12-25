using System.Net;
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
    public async Task<SnmpResult> GetAsync(ISnmpTarget target, string oid)
    {
        return await GetAsync(target, new[] { oid });
    }

    public async Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids)
    {
        return await Task.Run(() =>
        {
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
                return SnmpResult.Success(snmpVariables, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return SnmpResult.Fail(ex.Message);
            }
        });
    }

    public async Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid)
    {
         return await Task.Run(() =>
        {
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
                return SnmpResult.Success(snmpVariables, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return SnmpResult.Fail(ex.Message);
            }
        });
    }

    public async Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid)
    {
        return await Task.Run(() =>
        {
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(target.IpAddress), target.Port);
                var community = new OctetString(target.Community);
                var rootParams = new ObjectIdentifier(rootOid);
                var version = MapVersion(target.Version);
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var result = new List<Variable>();
                
                Messenger.Walk(version, endpoint, community, rootParams, result, target.Timeout, WalkMode.WithinSubtree);
                
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

    private SnmpVariable MapVariable(Variable v)
    {
        return new SnmpVariable(v.Id.ToString(), v.Data.ToString(), v.Data.TypeCode.ToString());
    }
}

