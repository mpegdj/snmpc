using System.Collections.Concurrent;
using System.Timers;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using Timer = System.Timers.Timer;

namespace SnmpNms.Infrastructure;

public class PollingService : IPollingService
{
    private readonly ISnmpClient _snmpClient;
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, ISnmpTarget> _targets;
    
    // Alive Check를 위한 OID (sysUpTime)
    private const string SysUpTimeOid = "1.3.6.1.2.1.1.3.0";

    public event EventHandler<PollingResult>? OnPollingResult;

    public PollingService(ISnmpClient snmpClient)
    {
        _snmpClient = snmpClient;
        _targets = new ConcurrentDictionary<string, ISnmpTarget>();
        
        // 기본 3초 주기
        _timer = new Timer(3000);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
    }

    public void Start()
    {
        if (!_timer.Enabled)
        {
            _timer.Start();
        }
    }

    public void Stop()
    {
        if (_timer.Enabled)
        {
            _timer.Stop();
        }
    }

    public void AddTarget(ISnmpTarget target)
    {
        var key = $"{target.IpAddress}:{target.Port}";
        _targets.TryAdd(key, target);
    }

    public void RemoveTarget(ISnmpTarget target)
    {
        var key = $"{target.IpAddress}:{target.Port}";
        _targets.TryRemove(key, out _);
    }

    public void SetInterval(int intervalMs)
    {
        if (intervalMs > 0)
        {
            _timer.Interval = intervalMs;
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Polling 주기마다 등록된 모든 타겟에 대해 비동기 요청
        var tasks = _targets.Values.Select(PollTargetAsync);
        await Task.WhenAll(tasks);
    }

    private async Task PollTargetAsync(ISnmpTarget target)
    {
        try
        {
            // sysUpTime을 조회하여 Alive 체크
            var result = await _snmpClient.GetAsync(target, SysUpTimeOid);
            
            DeviceStatus status;
            string message;
            long responseTime = result.ResponseTime;

            if (result.IsSuccess && result.Variables.Count > 0)
            {
                status = DeviceStatus.Up;
                message = $"Alive ({result.Variables[0].Value})";
            }
            else
            {
                status = DeviceStatus.Down;
                message = result.ErrorMessage ?? "No Response";
                responseTime = 0;
            }

            // 결과 이벤트 발생
            OnPollingResult?.Invoke(this, new PollingResult(target, status, responseTime, message));
        }
        catch (Exception ex)
        {
            OnPollingResult?.Invoke(this, new PollingResult(target, DeviceStatus.Down, 0, ex.Message));
        }
    }
}

