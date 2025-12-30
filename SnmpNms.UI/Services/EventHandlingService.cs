using System.Windows;
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
        // IP 기반 고속 검색 (MainWindow.FindTargetByKey 대체 가능 여부 확인 필요)
        var deviceNode = _vm.FindDeviceByIp(e.Target.IpAddress);
        if (deviceNode?.Target == null) return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            deviceNode.Target.Status = e.Status;
            _vm.RootSubnet.RecomputeEffectiveStatus();
        });
    }

    private void OnTrapReceived(object? sender, TrapEvent e)
    {
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
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                var statusChanged = deviceNode.Target.Status != newStatus;
                deviceNode.Target.Status = newStatus;

                var trapInfo = string.Join(" ", displayValues); 
                deviceNode.Target.LastMessage = $"[{severity}] {trapInfo}"; 
                
                if (statusChanged)
                {
                    _vm.RootSubnet.RecomputeEffectiveStatus();
                }
                _vm.TriggerPort162RxPulse();
            });
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
