using SnmpNms.Core.Models;

namespace SnmpNms.Core.Interfaces;

public interface IPollingService
{
    void Start();
    void Stop();
    void AddTarget(ISnmpTarget target);
    void RemoveTarget(ISnmpTarget target);
    
    // 폴링 주기를 설정 (기본값 3000ms)
    void SetInterval(int intervalMs);

    event EventHandler<PollingResult> OnPollingResult;
}

