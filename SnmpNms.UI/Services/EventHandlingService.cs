using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Services;

/// <summary>
/// SNMP Trap 및 Polling 결과를 처리하고 ViewModel에 반영하는 서비스
/// </summary>
public class EventHandlingService
{
    private readonly MainViewModel _vm;
    private readonly IPollingService _pollingService;
    private readonly ITrapListener _trapListener;
    private readonly ISnmpClient _snmpClient;
    private readonly IMibService _mibService;
    private readonly Channel<TrapEvent> _trapChannel;

    // UI Throttling
    private readonly ConcurrentQueue<StatusUpdateAction> _uiUpdateQueue = new();
    private readonly DispatcherTimer _uiThrottlingTimer;

    private class StatusUpdateAction
    {
        public MapNode Node { get; set; } = null!;
        public DeviceStatus Status { get; set; }
        public string? Message { get; set; }
    }

    public EventHandlingService(
        MainViewModel vm,
        IPollingService pollingService,
        ITrapListener trapListener,
        ISnmpClient snmpClient,
        IMibService mibService)
    {
        _vm = vm;
        _pollingService = pollingService;
        _trapListener = trapListener;
        _snmpClient = snmpClient;
        _mibService = mibService;

        // 트랩 처리를 위한 채널 생성 (무제한 큐 또는 용량 제한 가능)
        _trapChannel = Channel.CreateUnbounded<TrapEvent>(new UnboundedChannelOptions
        {
            SingleReader = true, // 단일 소비자 스레드 사용
            SingleWriter = false
        });

        // 백그라운드 소비자 시작
        _ = Task.Run(ProcessTrapQueueAsync);

        // UI Throttling 타이머 설정 (150ms 주기)
        _uiThrottlingTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _uiThrottlingTimer.Tick += (s, e) => FlushUpdatesToUi();
        _uiThrottlingTimer.Start();
    }

    public void Subscribe()
    {
        _pollingService.OnPollingResult += OnPollingResult;
        _trapListener.OnTrapReceived += OnTrapReceived;
        _trapListener.OnPacketReceived += (isValid) => _vm.TriggerPort162RxPulse();

        _snmpClient.OnRequestSent += () => _vm.TriggerPort161TxPulse();
        _snmpClient.OnResponseReceived += (isSuccess) => _vm.TriggerPort161RxPulse(isSuccess);
    }

    public void Unsubscribe()
    {
        _pollingService.OnPollingResult -= OnPollingResult;
        _trapListener.OnTrapReceived -= OnTrapReceived;
    }

    private void OnPollingResult(object? sender, PollingResult e)
    {
        // IP 기반 고속 검색 
        var deviceNode = _vm.FindDeviceByIp(e.Target.IpAddress);
        if (deviceNode?.Target == null) return;

        // 즉시 UI 스레드를 쓰지 않고 큐에 적재 (UI Throttling)
        _uiUpdateQueue.Enqueue(new StatusUpdateAction 
        { 
            Node = deviceNode, 
            Status = e.Status, 
            Message = null // Polling 메시지는 LastMessage에 표시 안 함 (기존 정책 준수)
        });
    }

    private void FlushUpdatesToUi()
    {
        if (_uiUpdateQueue.IsEmpty) return;

        bool statusChanged = false;
        var processedNodes = new HashSet<MapNode>();

        while (_uiUpdateQueue.TryDequeue(out var action))
        {
            if (action.Node.Target!.Status != action.Status)
            {
                action.Node.Target.Status = action.Status;
                statusChanged = true;
            }

            if (action.Message != null)
            {
                action.Node.Target.LastMessage = action.Message;
            }
            
            processedNodes.Add(action.Node);
        }

        if (statusChanged)
        {
            _vm.RootSubnet.RecomputeEffectiveStatus();
        }
    }

    private void OnTrapReceived(object? sender, TrapEvent e)
    {
        // 수신 즉시 큐에 삽입하여 리스너가 블로킹되지 않도록 함 (Producer)
        _trapChannel.Writer.TryWrite(e);
    }

    private async Task ProcessTrapQueueAsync()
    {
        // 큐에서 트랩을 꺼내 처리 (Consumer)
        await foreach (var e in _trapChannel.Reader.ReadAllAsync())
        {
            try
            {
                HandleTrapInternal(e);
            }
            catch (Exception ex)
            {
                _vm.Debug.LogError("EventHandlingService", $"Error processing trap: {ex.Message}");
            }
        }
    }

    private void HandleTrapInternal(TrapEvent e)
    {
        if (e.ErrorMessage != null)
        {
            _vm.AddEvent(EventSeverity.Error, e.SourceIpAddress, $"[Trap] {e.ErrorMessage}");
            return;
        }

        // Trap OID 결정
        string trapOid = "";
        if (e.Variables.Count > 1 && e.Variables[1].Oid == "1.3.6.1.6.3.1.1.4.1.0")
        {
            trapOid = e.Variables[1].Value?.ToString() ?? "";
        }
        
        if (string.IsNullOrEmpty(trapOid))
        {
            if (!string.IsNullOrEmpty(e.EnterpriseOid)) trapOid = e.EnterpriseOid;
            else if (e.Variables.Count > 0) trapOid = e.Variables[0].Oid;
        }

        // Trap 이름 (MIB)
        var trapName = string.IsNullOrEmpty(trapOid) ? "Unknown" : (_mibService.GetOidName(trapOid) ?? trapOid);
        
        // 데이터 파싱 및 Severity 결정
        var displayValues = new List<string>();
        var severity = EventSeverity.Info;

        for (int i = 0; i < e.Variables.Count; i++)
        {
            var val = e.Variables[i].Value?.ToString() ?? "null";
            
            if (i == 2) // Level
            {
                var levelName = NttTrapMappers.GetLevelName(val);
                displayValues.Add(levelName);
                
                severity = levelName.ToLower() switch
                {
                    "error" => EventSeverity.Error,
                    "warning" => EventSeverity.Warning,
                    "notice" => EventSeverity.Notice,
                    _ => EventSeverity.Info
                };
            }
            else if (i == 3) // Category
            {
                displayValues.Add(NttTrapMappers.GetCategoryName(val));
            }
            else
            {
                displayValues.Add(val);
            }
        }

        var mergedValues = string.Join(" / ", displayValues);
        var variablesSummary = e.Variables.Count > 0 ? ": " + mergedValues : "";

        // 디바이스 검색 
        var deviceNode = _vm.FindDeviceByIp(e.SourceIpAddress);
        string deviceDisplayName = e.SourceIpAddress;

        if (deviceNode?.Target != null)
        {
            deviceDisplayName = !string.IsNullOrWhiteSpace(deviceNode.Target.Alias) 
                ? deviceNode.Target.Alias 
                : deviceNode.Target.Device;

            DeviceStatus newStatus = severity switch
            {
                EventSeverity.Error => DeviceStatus.Down,
                EventSeverity.Warning => DeviceStatus.Warning,
                EventSeverity.Notice => DeviceStatus.Notice,
                _ => DeviceStatus.Up
            };
            
            // UI Throttling 큐에 적재
            var trapInfo = string.Join(" ", displayValues);
            _uiUpdateQueue.Enqueue(new StatusUpdateAction
            {
                Node = deviceNode,
                Status = newStatus,
                Message = $"[{severity}] {trapInfo}"
            });

            _vm.TriggerPort162RxPulse();
        }
        else
        {
            // 이름 추론 (미등록 장비인 경우)
            if (trapName.StartsWith("mve", StringComparison.OrdinalIgnoreCase))
            {
                int trapIdx = trapName.IndexOf("Trap", StringComparison.OrdinalIgnoreCase);
                deviceDisplayName = trapIdx > 0 ? trapName.Substring(0, trapIdx) : trapName;
            }
        }

        // Com Log 기록 (Raw 데이터 분석용)
        if (e.RawData != null && e.RawData.Length > 0)
        {
            var rawSummary = string.Join(" / ", e.Variables.Select(v => v.Value?.ToString() ?? "null"));
            _vm.Com.LogReceive(e.RawData, $"{e.SourceIpAddress}:{e.SourcePort}", trapOid, rawSummary);
        }

        _vm.AddEvent(severity, deviceDisplayName, $"{trapName}{variablesSummary}");
    }
}
