using System.Threading;
using SnmpNms.Core.Models;

namespace SnmpNms.Core.Interfaces;

public interface ISnmpClient
{
    Task<SnmpResult> GetAsync(ISnmpTarget target, string oid);
    Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids);
    Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid);
    Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid, CancellationToken cancellationToken = default);
    Task<SnmpResult> SetAsync(ISnmpTarget target, string oid, string value, string type);
}

