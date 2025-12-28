using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Win32;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.Infrastructure;
using SnmpNms.UI.Models;
using SnmpNms.UI.Services;
using SnmpNms.UI.ViewModels;
using SnmpNms.UI.Views.Dialogs;
using SnmpNms.UI.Views;
using SnmpNms.UI.Views.EventLog;
using VersionCode = Lextm.SharpSnmpLib.VersionCode;

namespace SnmpNms.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    private readonly ISnmpClient _snmpClient;
    private readonly IMibService _mibService;
    private readonly IPollingService _pollingService;
    private readonly ITrapListener _trapListener;
    private readonly MainViewModel _vm;
    private readonly MapDataService _mapDataService;

    private Point _dragStartPoint;
    private MapNode? _selectionAnchor;
    private SidebarMapView? _sidebarMapView;
    private TreeView? _tvDevices;
    private TreeView? _treeMib;
    private CancellationTokenSource? _walkCancellationTokenSource;
    private string? _currentFilePath;
    private bool _isModified;

    public MainWindow()
    {
        InitializeComponent();
        
        // DI 컨테이너 없이 수동 주입
        _snmpClient = new SnmpClient();
        _mibService = new MibService();
        _pollingService = new PollingService(_snmpClient);
        _trapListener = new TrapListener();
        _mapDataService = new MapDataService();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Polling 이벤트 연결
        _pollingService.OnPollingResult += PollingService_OnPollingResult;

        // Trap 이벤트 연결
        _trapListener.OnTrapReceived += TrapListener_OnTrapReceived;

        // Trap Listener 시작
        InitializeTrapListener();

        // MIB 로딩/파싱 로그를 GUI Event Log로 연결
        if (_mibService is MibService mibService)
        {
            mibService.OnLog += msg => _vm.AddSystemInfo(msg);
        }

        // MIB 파일 로드 (Mib 폴더가 실행 파일 위치 또는 상위에 있다고 가정)
        LoadMibs();
        
        // MIB 트리 초기화
        InitializeMibTree();

        // 기본 디바이스 제거 (127.0.0.1은 의미 없음)
        _vm.AddSystemInfo("[System] Map Selection Tree ready (Root Subnet/Default).");

        // VS Code 스타일 UI 초기화
        this.Loaded += (s, e) => InitializeVSCodeUI();
        
        // 시작 메시지를 터미널에 표시 (콘솔 할당)
        AllocConsole();
        Console.WriteLine("SNMPc Start");
    }

    private void InitializeVSCodeUI()
    {
        // Sidebar에 Map 뷰 설정
        _sidebarMapView = new SidebarMapView { DataContext = _vm };
        _tvDevices = _sidebarMapView.TreeView;
        _tvDevices.MouseLeftButtonDown += TvDevices_MouseLeftButtonDown;
        _tvDevices.SelectedItemChanged += TvDevices_SelectedItemChanged;
        _tvDevices.PreviewMouseMove += TvDevices_PreviewMouseMove;
        _tvDevices.Drop += TvDevices_Drop;
        _tvDevices.PreviewKeyDown += TvDevices_PreviewKeyDown;
        _sidebarMapView.MapNodeTextMouseLeftButtonDown += MapNodeText_MouseLeftButtonDown;
        
        // DataContext 확인 및 설정
        System.Diagnostics.Debug.WriteLine($"MapRoots count: {_vm.MapRoots.Count}");
        sidebar.CurrentContent = _sidebarMapView;
        
        // DataContext가 제대로 설정되었는지 확인
        if (_sidebarMapView.DataContext == null)
        {
            _sidebarMapView.DataContext = _vm;
        }

        // BottomPanel에 Event Log DataContext 설정 (여러 탭이 각각 바인딩됨)
        bottomPanel.DataContext = _vm;
        bottomPanel.SetOutputViewModel(_vm.Output);
        bottomPanel.SetMainViewModel(_vm);
        
        // OutputViewModel에 저장 서비스 연결
        _vm.Output.SetSaveService(_vm.OutputSaveService);

        // Sidebar (Activity Bar 포함) 이벤트 연결
        sidebar.ViewChanged += ActivityBar_ViewChanged;
        
        // MapViewControl에 서비스 주입
        mapViewControl.SnmpClient = _snmpClient;
        mapViewControl.TrapListener = _trapListener;
    }

    private void InitializeTrapListener()
    {
        try
        {
            _trapListener.Start(162);
            _vm.IsTrapListening = true;
            _vm.AddEvent(EventSeverity.Info, null, "[System] Trap Listener started on port 162");
        }
        catch (Exception ex)
        {
            _vm.IsTrapListening = false;
            _vm.AddEvent(EventSeverity.Error, null, $"[System] Failed to start Trap Listener: {ex.Message}");
        }
    }

    private void TrapListener_OnTrapReceived(object? sender, TrapEvent e)
    {
        if (e.ErrorMessage != null)
        {
            _vm.AddEvent(EventSeverity.Error, e.SourceIpAddress, $"[Trap] {e.ErrorMessage}");
            return;
        }

        var trapInfo = $"Trap from {e.SourceIpAddress}:{e.SourcePort}";
        if (!string.IsNullOrEmpty(e.EnterpriseOid))
        {
            trapInfo += $" Enterprise: {e.EnterpriseOid}";
        }
        if (!string.IsNullOrEmpty(e.GenericTrapType))
        {
            trapInfo += $" Generic: {e.GenericTrapType}";
        }
        if (e.Variables.Count > 0)
        {
            trapInfo += $" ({e.Variables.Count} variables)";
        }

        _vm.AddEvent(EventSeverity.Info, e.SourceIpAddress, $"[Trap] {trapInfo}");
        
        // 변수들도 로그에 기록 (최대 5개만, MIB 이름 변환 포함)
        foreach (var variable in e.Variables.Take(5))
        {
            var oidName = _mibService?.GetOidName(variable.Oid) ?? variable.Oid;
            _vm.AddEvent(EventSeverity.Info, e.SourceIpAddress, $"  {oidName} = {variable.Value}");
        }
    }

    private void ActivityBar_ViewChanged(object? sender, ActivityBarView view)
    {
        // Activity Bar 뷰 변경 처리
        switch (view)
        {
            case ActivityBarView.Map:
                sidebar.HeaderText = "EXPLORER";
                _sidebarMapView = new SidebarMapView { DataContext = _vm };
                _tvDevices = _sidebarMapView.TreeView;
                _tvDevices.MouseLeftButtonDown += TvDevices_MouseLeftButtonDown;
                _tvDevices.SelectedItemChanged += TvDevices_SelectedItemChanged;
                _tvDevices.PreviewMouseMove += TvDevices_PreviewMouseMove;
                _tvDevices.Drop += TvDevices_Drop;
                _tvDevices.PreviewKeyDown += TvDevices_PreviewKeyDown;
                _sidebarMapView.MapNodeTextMouseLeftButtonDown += MapNodeText_MouseLeftButtonDown;
                sidebar.CurrentContent = _sidebarMapView;
                break;
            case ActivityBarView.Mib:
                sidebar.HeaderText = "MIB";
                var sidebarMibView = new SidebarMibView { DataContext = _mibService.GetMibTree() };
                _treeMib = sidebarMibView.TreeView;
                _treeMib.ItemsSource = new[] { _mibService.GetMibTree() };
                _treeMib.SelectedItemChanged += treeMib_SelectedItemChanged;
                sidebarMibView.MibTreeGetClick += MibTreeGet_Click;
                sidebarMibView.MibTreeGetNextClick += MibTreeGetNext_Click;
                sidebarMibView.MibTreeWalkClick += MibTreeWalk_Click;
                sidebarMibView.MibTreeViewTableClick += MibTreeViewTable_Click;
                sidebarMibView.MibTreeCopyOidClick += MibTreeCopyOid_Click;
                sidebarMibView.MibTreeCopyNameClick += MibTreeCopyName_Click;
                sidebar.CurrentContent = sidebarMibView;
                break;
            case ActivityBarView.Search:
                sidebar.HeaderText = "SEARCH";
                var searchView = new SidebarSearchView();
                searchView.SetViewModel(_vm);
                searchView.SetMibService(_mibService);
                searchView.MapNodeSelected += SearchView_MapNodeSelected;
                searchView.MibNodeSelected += SearchView_MibNodeSelected;
                sidebar.CurrentContent = searchView;
                searchView.FocusSearchBox();
                break;
            case ActivityBarView.EventLog:
                sidebar.HeaderText = "EVENT LOG";
                sidebar.CurrentContent = new TextBlock { Text = "Event Log (Coming soon)", Foreground = System.Windows.Media.Brushes.Gray };
                break;
            case ActivityBarView.Settings:
                sidebar.HeaderText = "SETTINGS";
                sidebar.CurrentContent = new TextBlock { Text = "Settings (Coming soon)", Foreground = System.Windows.Media.Brushes.Gray };
                break;
        }
    }

    // --- Edit Button Bar: Add Map Objects (SNMPc style) ---
    private void FindMapObjects_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DiscoveryPollingAgentsDialog(_snmpClient, _vm, _trapListener) { Owner = this };
        dialog.ShowDialog();
    }

    private void EditAddDeviceObject_Click(object sender, RoutedEventArgs e) => ShowAddMapObjectDialog(MapObjectType.Device);
    private void EditAddSubnet_Click(object sender, RoutedEventArgs e) => ShowAddMapObjectDialog(MapObjectType.Subnet);
    private void EditAddGoto_Click(object sender, RoutedEventArgs e) => ShowAddMapObjectDialog(MapObjectType.Goto);

    // Edit Object Properties 버튼 - Map Node 속성 편집
    private void EditObjectProperties_Click(object sender, RoutedEventArgs e)
    {
        // 선택된 Map Node 찾기
        var selectedNode = _vm.SelectedMapNodes.FirstOrDefault();
        if (selectedNode == null)
        {
            _vm.AddEvent(EventSeverity.Info, null, "Please select a map object to edit properties");
            return;
        }

        ShowEditMapObjectDialog(selectedNode);
    }

    private void ShowAddMapObjectDialog(MapObjectType type)
    {
        var dlg = new MapObjectPropertiesDialog(type, _snmpClient, _trapListener) { Owner = this };

        // 기본값: 현재 선택된 장비/입력값 기반
        if (type == MapObjectType.Device)
        {
            dlg.Alias = string.IsNullOrWhiteSpace(txtIp.Text) ? "" : txtIp.Text.Trim();
            dlg.Device = "";
            dlg.Address = string.IsNullOrWhiteSpace(txtIp.Text) ? "" : $"{txtIp.Text.Trim()}:161";
            dlg.ReadCommunity = (txtCommunity.Text ?? "public").Trim();
        }
        else if (type == MapObjectType.Subnet)
        {
            dlg.Alias = "New Subnet";
            dlg.Device = "";
        }
        else
        {
            dlg.Alias = "Goto";
            dlg.Device = "";
            dlg.Address = ""; // goto 대상 subnet 이름
        }

        if (dlg.ShowDialog() != true) return;

        var parent = GetSelectedSubnetOrDefault();
        switch (dlg.Result.Type)
        {
            case MapObjectType.Device:
            {
                var target = new UiSnmpTarget
                {
                    IpAddress = dlg.Result.IpOrHost,
                    Port = dlg.Result.Port,
                    Alias = dlg.Result.Alias,
                    Device = dlg.Result.Device,
                    Community = dlg.Result.ReadCommunity,
                    Version = dlg.Result.SnmpVersion,
                    Timeout = dlg.Result.TimeoutMs,
                    Retries = dlg.Result.Retries,
                    PollingProtocol = dlg.Result.PollingProtocol
                };
                _vm.AddDeviceToSubnet(target, parent);
                _vm.AddEvent(EventSeverity.Info, target.EndpointKey, $"[Map] Device added: {target.DisplayName} ({target.EndpointKey})");
                MarkAsModified();
                break;
            }
            case MapObjectType.Subnet:
            {
                _vm.AddSubnet(dlg.Result.Alias, parent);
                _vm.AddSystemInfo($"[Map] Subnet added: {dlg.Result.Alias}");
                MarkAsModified();
                break;
            }
            case MapObjectType.Goto:
            {
                _vm.AddGoto(dlg.Result.Alias, dlg.Result.GotoSubnetName, parent);
                _vm.AddSystemInfo($"[Map] Goto added: {dlg.Result.Alias} -> {dlg.Result.GotoSubnetName}");
                MarkAsModified();
                break;
            }
        }

        parent.IsExpanded = true;
        _vm.RootSubnet.RecomputeEffectiveStatus();
    }

    private void ShowEditMapObjectDialog(MapNode node)
    {
        if (node.NodeType == MapNodeType.Device && node.Target != null)
        {
            // Device인 경우 기존 Target 정보로 다이얼로그 열기
            var dlg = new MapObjectPropertiesDialog(MapObjectType.Device, node.Target, _snmpClient) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            // 기존 Target 업데이트
            node.Target.IpAddress = dlg.Result.IpOrHost;
            node.Target.Port = dlg.Result.Port;
            node.Target.Alias = dlg.Result.Alias;
            node.Target.Device = dlg.Result.Device;
            node.Target.Community = dlg.Result.ReadCommunity;
            node.Target.Version = dlg.Result.SnmpVersion;
            node.Target.Timeout = dlg.Result.TimeoutMs;
            node.Target.Retries = dlg.Result.Retries;
            node.Target.PollingProtocol = dlg.Result.PollingProtocol;

            _vm.AddEvent(EventSeverity.Info, node.Target.EndpointKey, $"[Map] Device updated: {node.Target.DisplayName} ({node.Target.EndpointKey})");
        }
        else if (node.NodeType == MapNodeType.Subnet || node.NodeType == MapNodeType.RootSubnet)
        {
            // Subnet인 경우 이름만 편집 가능 (간단한 입력 다이얼로그 또는 기존 다이얼로그 사용)
            var dlg = new MapObjectPropertiesDialog(MapObjectType.Subnet, _snmpClient) { Owner = this };
            dlg.Alias = node.Name;
            if (dlg.ShowDialog() != true) return;

            node.Name = dlg.Result.Alias;
            _vm.AddSystemInfo($"[Map] Subnet updated: {node.Name}");
        }
        else if (node.NodeType == MapNodeType.Goto)
        {
            // Goto인 경우
            var dlg = new MapObjectPropertiesDialog(MapObjectType.Goto, _snmpClient) { Owner = this };
            dlg.Alias = node.Name;
            if (dlg.ShowDialog() != true) return;

            node.Name = dlg.Result.Alias;
            _vm.AddSystemInfo($"[Map] Goto updated: {node.Name}");
        }
    }

    private MapNode GetSelectedSubnetOrDefault()
    {
        // 선택된 노드 중 subnet/root가 있으면 그걸 사용
        var selected = _vm.SelectedMapNodes.FirstOrDefault(n => n.NodeType is MapNodeType.Subnet or MapNodeType.RootSubnet);
        if (selected is not null) return selected;

        // 장비가 선택되어 있으면 부모 subnet으로
        var device = _vm.SelectedMapNodes.FirstOrDefault(n => n.NodeType == MapNodeType.Device);
        if (device?.Parent is not null) return device.Parent;

        return _vm.DefaultSubnet;
    }

    private void PollingService_OnPollingResult(object? sender, PollingResult e)
    {
        // UI 스레드에서 업데이트
        Dispatcher.Invoke(() =>
        {
            if (e.Status == DeviceStatus.Up)
            {
                SetDeviceStatus($"{e.Target.IpAddress}:{e.Target.Port}", DeviceStatus.Up);
                // Polling 로그는 너무 많을 수 있으므로 상태 변경 시에만 찍거나, 별도 로그창 사용 권장
                // 여기서는 간단하게 시간 갱신
                // txtResult.AppendText($"[Poll] {e.Target.IpAddress} is Alive ({e.ResponseTime}ms)\n");
            }
            else
            {
                SetDeviceStatus($"{e.Target.IpAddress}:{e.Target.Port}", DeviceStatus.Down);
                _vm.AddEvent(EventSeverity.Error, $"{e.Target.IpAddress}:{e.Target.Port}", $"[Poll] Down: {e.Message}");
            }
        });
    }

    private void SetDeviceStatus(string deviceKey, DeviceStatus status)
    {
        var target = FindTargetByKey(_vm.RootSubnet, deviceKey);
        if (target is not null) target.Status = status;
        _vm.RootSubnet.RecomputeEffectiveStatus();
    }

    private static UiSnmpTarget? FindTargetByKey(MapNode node, string key)
    {
        if (node.Target is not null && string.Equals(node.Target.EndpointKey, key, StringComparison.OrdinalIgnoreCase))
            return node.Target;

        foreach (var c in node.Children)
        {
            var found = FindTargetByKey(c, key);
            if (found is not null) return found;
        }
        return null;
    }

    
    private void ChkAutoPoll_Checked(object sender, RoutedEventArgs e)
    {
        // 모든 기기를 polling에 추가
        var allDevices = GetAllDevicesFromNode(_vm.RootSubnet);
        
        foreach (var device in allDevices)
        {
            _pollingService.AddTarget(device);
        }
        
        _pollingService.Start();
        _vm.IsPollingRunning = true;
        _vm.AddEvent(EventSeverity.Info, null, "[System] Auto Polling Started");
    }

    private void ChkAutoPoll_Unchecked(object sender, RoutedEventArgs e)
    {
        // 모든 기기를 polling에서 제거
        var allDevices = GetAllDevicesFromNode(_vm.RootSubnet);
        
        foreach (var device in allDevices)
        {
            _pollingService.RemoveTarget(device);
        }
        
        _pollingService.Stop();
        _vm.IsPollingRunning = false;
        _vm.AddEvent(EventSeverity.Info, null, "[System] Auto Polling Stopped");
    }

    private UiSnmpTarget BuildTargetFromInputs(bool minimal = false)
    {
        var ip = txtIp.Text?.Trim() ?? "";
        var community = txtCommunity.Text?.Trim() ?? "public";

        return new UiSnmpTarget
        {
            IpAddress = ip,
            Community = minimal ? "public" : community,
            Version = SnmpVersion.V2c,
            Timeout = 3000,
            Port = 161
        };
    }

    private void LoadMibs()
    {
        // 개발 환경 경로 하드코딩 (임시)
        // 실제 배포 시에는 AppDomain.CurrentDomain.BaseDirectory 기준 "Mib" 폴더 사용 권장
        var projectRoot = @"D:\git\snmpc\Mib"; 
        
        // 만약 경로가 없으면 실행 파일 기준 "Mib" 폴더 시도
        if (!Directory.Exists(projectRoot))
        {
            projectRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mib");
        }

        if (Directory.Exists(projectRoot))
        {
            try
            {
                _mibService.LoadMibModules(projectRoot);
                _vm.AddSystemInfo($"[System] Loaded MIBs from {projectRoot}");
            }
            catch (Exception ex)
            {
                _vm.AddEvent(EventSeverity.Warning, null, $"[System] Failed to load MIBs: {ex.Message}");
            }
        }
        else
        {
            _vm.AddEvent(EventSeverity.Warning, null, $"[System] MIB directory not found: {projectRoot}");
        }
    }

    private async void BtnGet_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddEvent(EventSeverity.Info, $"{txtIp.Text}:161", $"Sending SNMP GET request to {txtIp.Text}...");
        btnGet.IsEnabled = false;

        try
        {
            var target = new UiSnmpTarget
            {
                IpAddress = txtIp.Text,
                Community = txtCommunity.Text,
                Version = SnmpVersion.V2c,
                Timeout = 3000
            };

            var oid = txtOid.Text;

            // 이름으로 OID 검색 기능 추가 (예: "sysDescr" 입력 시 변환)
            // 숫자(.)로 시작하지 않으면 이름으로 간주
            if (!string.IsNullOrEmpty(oid) && !oid.StartsWith(".") && !char.IsDigit(oid[0]))
            {
                var convertedOid = _mibService.GetOid(oid);
                if (convertedOid != oid)
                {
                    _vm.AddSystemInfo($"[System] Converted '{oid}' to '{convertedOid}'");
                    oid = convertedOid;
                }
            }

            // Output 로그: 송신
            _vm.Output.LogSend("SNMP", "GET", $"{target.IpAddress}:{target.Port}", oid);
            
            var result = await _snmpClient.GetAsync(target, oid);

            if (result.IsSuccess)
            {
                // Output 로그: 수신
                foreach (var v in result.Variables)
                {
                    _vm.Output.LogReceive("SNMP", "GET", $"{target.IpAddress}:{target.Port}", v.Oid, $"{v.TypeCode}: {v.Value}");
                }
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Success! (Time: {result.ResponseTime}ms)");
                foreach (var v in result.Variables)
                {
                    // 결과 출력 시 OID -> 이름 변환 적용
                    var name = _mibService.GetOidName(v.Oid);
                    // 원래 OID가 그대로 나오면 이름 없음
                    var displayName = name == v.Oid ? v.Oid : $"{name} ({v.Oid})";
                    
                    sb.AppendLine($"{displayName} = {v.TypeCode}: {v.Value}");
                }
                var resultText = sb.ToString().TrimEnd();
                txtSnmpResult.Text = resultText;
                _vm.AddEvent(EventSeverity.Info, $"{target.IpAddress}:{target.Port}", resultText);
            }
            else
            {
                // Output 로그: 에러
                _vm.Output.LogError("SNMP", "GET", $"{target.IpAddress}:{target.Port}", oid, result.ErrorMessage ?? "Unknown error");
                
                var errorText = $"Failed: {result.ErrorMessage}";
                txtSnmpResult.Text = errorText;
                _vm.AddEvent(EventSeverity.Error, $"{target.IpAddress}:{target.Port}", errorText);
            }
        }
        catch (Exception ex)
        {
            // Output 로그: 예외
            _vm.Output.LogError("SNMP", "GET", $"{txtIp.Text}:161", txtOid.Text, ex.Message);
            
            var errorText = $"Error: {ex.Message}";
            txtSnmpResult.Text = errorText;
            _vm.AddEvent(EventSeverity.Error, $"{txtIp.Text}:161", errorText);
        }
        finally
        {
            btnGet.IsEnabled = true;
        }
    }

    private async void BtnGetNext_Click(object sender, RoutedEventArgs e)
    {
        if (txtIp == null || txtCommunity == null || txtOid == null || txtSnmpResult == null)
        {
            MessageBox.Show("UI 필드가 초기화되지 않았습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _vm.AddEvent(EventSeverity.Info, $"{txtIp.Text}:161", $"Sending SNMP GET-NEXT request to {txtIp.Text}...");
        if (txtSnmpResult != null)
        {
            txtSnmpResult.Text = $"Sending SNMP GET-NEXT request to {txtIp.Text}...";
        }
        btnGetNext.IsEnabled = false;

        try
        {
            var target = new UiSnmpTarget
            {
                IpAddress = txtIp.Text,
                Community = txtCommunity.Text,
                Version = SnmpVersion.V2c,
                Timeout = 3000
            };

            var oid = txtOid.Text;

            // 이름으로 OID 검색 기능 추가
            if (!string.IsNullOrEmpty(oid) && !oid.StartsWith(".") && !char.IsDigit(oid[0]))
            {
                var convertedOid = _mibService.GetOid(oid);
                if (convertedOid != oid)
                {
                    _vm.AddSystemInfo($"[System] Converted '{oid}' to '{convertedOid}'");
                    oid = convertedOid;
                }
            }

            // Output 로그: 송신
            _vm.Output.LogSend("SNMP", "GET-NEXT", $"{target.IpAddress}:{target.Port}", oid);
            
            var result = await _snmpClient.GetNextAsync(target, oid);

            if (result.IsSuccess && result.Variables.Count > 0)
            {
                // Output 로그: 수신
                foreach (var v in result.Variables)
                {
                    _vm.Output.LogReceive("SNMP", "GET-NEXT", $"{target.IpAddress}:{target.Port}", v.Oid, $"{v.TypeCode}: {v.Value}");
                }
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Get-Next Success! (Time: {result.ResponseTime}ms)");
                // Get Next는 단일 결과이므로 정렬 불필요, 순서대로 표시
                foreach (var v in result.Variables)
                {
                    var name = _mibService.GetOidName(v.Oid);
                    var displayName = name == v.Oid ? v.Oid : $"{name} ({v.Oid})";
                    sb.AppendLine($"{displayName} = {v.TypeCode}: {v.Value}");
                    
                    // 다음 Get Next를 위해 OID 필드 자동 업데이트
                    if (txtOid != null && !string.IsNullOrEmpty(v.Oid))
                    {
                        txtOid.Text = v.Oid;
                    }
                }
                var resultText = sb.ToString().TrimEnd();
                if (txtSnmpResult != null)
                {
                    txtSnmpResult.Text = resultText;
                }
                _vm.AddEvent(EventSeverity.Info, $"{target.IpAddress}:{target.Port}", resultText);
            }
            else
            {
                // Output 로그: 에러
                _vm.Output.LogError("SNMP", "GET-NEXT", $"{target.IpAddress}:{target.Port}", oid, result.ErrorMessage ?? "Unknown error");
                
                var errorText = $"Get-Next Failed: {result.ErrorMessage}";
                if (txtSnmpResult != null)
                {
                    txtSnmpResult.Text = errorText;
                }
                _vm.AddEvent(EventSeverity.Error, $"{target.IpAddress}:{target.Port}", errorText);
            }
        }
        catch (Exception ex)
        {
            // Output 로그: 예외
            _vm.Output.LogError("SNMP", "GET-NEXT", $"{txtIp.Text}:161", txtOid.Text, ex.Message);
            
            var errorText = $"Error: {ex.Message}";
            if (txtSnmpResult != null)
            {
                txtSnmpResult.Text = errorText;
            }
            _vm.AddEvent(EventSeverity.Error, $"{txtIp.Text}:161", errorText);
            System.Diagnostics.Debug.WriteLine($"BtnGetNext_Click Exception: {ex}");
        }
        finally
        {
            btnGetNext.IsEnabled = true;
        }
    }

    private async void BtnWalk_Click(object sender, RoutedEventArgs e)
    {
        if (txtIp == null || txtCommunity == null || txtOid == null || txtSnmpResult == null)
        {
            MessageBox.Show("UI 필드가 초기화되지 않았습니다.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 기존 Walk 작업이 있으면 취소
        _walkCancellationTokenSource?.Cancel();
        _walkCancellationTokenSource?.Dispose();
        _walkCancellationTokenSource = new CancellationTokenSource();
        var token = _walkCancellationTokenSource.Token;

        _vm.AddEvent(EventSeverity.Info, $"{txtIp.Text}:161", $"Sending SNMP WALK request to {txtIp.Text}...");
        if (txtSnmpResult != null)
        {
            txtSnmpResult.Text = $"Sending SNMP WALK request to {txtIp.Text}...";
        }
        btnWalk.IsEnabled = false;
        btnStopWalk.IsEnabled = true;
        btnStopWalk.Visibility = Visibility.Visible;

        try
        {
            var target = new UiSnmpTarget
            {
                IpAddress = txtIp.Text,
                Community = txtCommunity.Text,
                Version = SnmpVersion.V2c,
                Timeout = 10000  // Walk는 여러 요청을 보내므로 Timeout을 늘림
            };

            var oid = txtOid.Text;

            // 이름으로 OID 검색 기능 추가
            if (!string.IsNullOrEmpty(oid) && !oid.StartsWith(".") && !char.IsDigit(oid[0]))
            {
                var convertedOid = _mibService.GetOid(oid);
                if (convertedOid != oid)
                {
                    _vm.AddSystemInfo($"[System] Converted '{oid}' to '{convertedOid}'");
                    oid = convertedOid;
                }
            }

            // Walk는 스칼라 OID에 대해 하위 트리를 순회하므로, 인스턴스 OID(.0)는 제거
            // 예: 1.3.6.1.2.1.1.1.0 -> 1.3.6.1.2.1.1.1 (스칼라 OID로 변환)
            // (75aa832: nel child disappear 에서 사용하던 규칙을 포팅)
            if (!string.IsNullOrEmpty(oid) && oid.EndsWith(".0"))
            {
                var scalarOid = oid.Substring(0, oid.Length - 2);
                // 스칼라 OID인지 확인 (MIB 서비스에서 이름을 찾을 수 있으면 스칼라 OID)
                var testName = _mibService.GetOidName(scalarOid);
                if (testName != scalarOid)
                {
                    oid = scalarOid;
                }
            }

            // 취소 토큰 체크
            token.ThrowIfCancellationRequested();

            // Output 로그: 송신
            _vm.Output.LogSend("SNMP", "WALK", $"{target.IpAddress}:{target.Port}", oid);
            
            var result = await _snmpClient.WalkAsync(target, oid, token);

            // 취소되었는지 확인
            if (token.IsCancellationRequested)
            {
                if (txtSnmpResult != null)
                {
                    txtSnmpResult.Text = "Walk cancelled by user.";
                }
                _vm.AddEvent(EventSeverity.Warning, $"{target.IpAddress}:{target.Port}", "Walk cancelled by user");
                return;
            }

            if (result.IsSuccess)
            {
                // Output 로그: 수신 (요약)
                _vm.Output.LogReceive("SNMP", "WALK", $"{target.IpAddress}:{target.Port}", oid, $"{result.Variables.Count} variables, {result.ResponseTime}ms");
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Walk Success! (Time: {result.ResponseTime}ms, {result.Variables.Count} values)");
                sb.AppendLine("");
                // OID 순으로 정렬하여 표시
                var sortedVariables = result.Variables.OrderBy(v => CompareOidForDisplay(v.Oid)).ToList();
                foreach (var v in sortedVariables)
                {
                    var name = _mibService.GetOidName(v.Oid);
                    var displayName = name == v.Oid ? v.Oid : $"{name} ({v.Oid})";
                    sb.AppendLine($"{displayName} = {v.TypeCode}: {v.Value}");
                }
                var resultText = sb.ToString().TrimEnd();
                if (txtSnmpResult != null)
                {
                    txtSnmpResult.Text = resultText;
                }
                _vm.AddEvent(EventSeverity.Info, $"{target.IpAddress}:{target.Port}", $"Walk completed: {result.Variables.Count} values");
            }
            else
            {
                // Output 로그: 에러
                _vm.Output.LogError("SNMP", "WALK", $"{target.IpAddress}:{target.Port}", oid, result.ErrorMessage ?? "Unknown error");
                
                var errorText = $"Walk Failed: {result.ErrorMessage}";
                if (txtSnmpResult != null)
                {
                    txtSnmpResult.Text = errorText;
                }
                _vm.AddEvent(EventSeverity.Error, $"{target.IpAddress}:{target.Port}", errorText);
            }
        }
        catch (OperationCanceledException)
        {
            if (txtSnmpResult != null)
            {
                txtSnmpResult.Text = "Walk cancelled by user.";
            }
            _vm.AddEvent(EventSeverity.Warning, $"{txtIp.Text}:161", "Walk cancelled by user");
        }
        catch (Exception ex)
        {
            // Output 로그: 예외
            _vm.Output.LogError("SNMP", "WALK", $"{txtIp.Text}:161", txtOid.Text, ex.Message);
            
            var errorText = $"Error: {ex.Message}";
            if (txtSnmpResult != null)
            {
                txtSnmpResult.Text = errorText;
            }
            _vm.AddEvent(EventSeverity.Error, $"{txtIp.Text}:161", errorText);
            System.Diagnostics.Debug.WriteLine($"BtnWalk_Click Exception: {ex}");
        }
        finally
        {
            btnWalk.IsEnabled = true;
            btnStopWalk.IsEnabled = false;
            btnStopWalk.Visibility = Visibility.Collapsed;
            _walkCancellationTokenSource?.Dispose();
            _walkCancellationTokenSource = null;
        }
    }

    private void BtnStopWalk_Click(object sender, RoutedEventArgs e)
    {
        _walkCancellationTokenSource?.Cancel();
        if (txtSnmpResult != null)
        {
            txtSnmpResult.Text = "Stopping Walk...";
        }
    }

    private void BtnSendTrap_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var targetIp = txtTrapTarget.Text.Trim();
            if (string.IsNullOrEmpty(targetIp))
            {
                _vm.AddEvent(EventSeverity.Warning, null, "[Trap Test] Please enter Trap Target IP address");
                return;
            }

            if (!int.TryParse(txtTrapPort.Text.Trim(), out int port) || port <= 0 || port > 65535)
            {
                _vm.AddEvent(EventSeverity.Warning, null, "[Trap Test] Please enter valid port number (1-65535)");
                return;
            }

            var trapOid = txtTrapOid.Text.Trim();
            if (string.IsNullOrEmpty(trapOid))
            {
                _vm.AddEvent(EventSeverity.Warning, null, "[Trap Test] Please enter Trap OID");
                return;
            }

            // 이름으로 OID 검색 기능 (예: "sysDescr" 입력 시 변환)
            if (!trapOid.StartsWith(".") && !char.IsDigit(trapOid[0]))
            {
                var convertedOid = _mibService.GetOid(trapOid);
                if (convertedOid != trapOid)
                {
                    _vm.AddSystemInfo($"[Trap Test] Converted '{trapOid}' to '{convertedOid}'");
                    trapOid = convertedOid;
                }
            }

            var target = new IPEndPoint(IPAddress.Parse(targetIp), port);
            var community = new OctetString(txtCommunity.Text.Trim());
            var trapObjectId = new ObjectIdentifier(trapOid);

            _vm.AddEvent(EventSeverity.Info, $"{targetIp}:{port}", $"[Trap Test] Sending SNMPv2c Trap to {targetIp}:{port}...");

            // SNMPv2c Trap 전송
            var variables = new List<Variable>
            {
                new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.3.0"), new TimeTicks(0)), // sysUpTime
                new Variable(trapObjectId, new OctetString($"Test Trap from {DateTime.Now:yyyy-MM-dd HH:mm:ss}"))
            };

            Messenger.SendTrapV2(
                0,
                VersionCode.V2,
                target,
                community,
                trapObjectId,
                0,
                variables);

            _vm.AddEvent(EventSeverity.Info, $"{targetIp}:{port}", $"[Trap Test] Trap sent successfully! OID: {trapOid}");
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, null, $"[Trap Test] Error: {ex.Message}");
        }
    }

    private void RemoveDevice_Click(object sender, RoutedEventArgs e)
    {
        // Map Tree에서 선택된 디바이스 노드 제거
        var selectedDeviceNode = _vm.SelectedMapNodes.FirstOrDefault(n => n.NodeType == MapNodeType.Device);
        if (selectedDeviceNode?.Target is null || selectedDeviceNode.Parent is null)
        {
            _vm.AddSystemInfo("[System] RemoveDevice: no device selected.");
            return;
        }

        _pollingService.RemoveTarget(selectedDeviceNode.Target);
        selectedDeviceNode.Parent.RemoveChild(selectedDeviceNode);
        _vm.RemoveDeviceNode(selectedDeviceNode);
        _vm.SelectedDevice = null;

        _vm.AddEvent(EventSeverity.Info, selectedDeviceNode.Target.EndpointKey, "[System] Device removed");
    }

    // --- Map Selection Tree interactions (SNMPc style) ---
    private void TvDevices_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        Console.WriteLine("[TvDevices_SelectedItemChanged] Event fired");
        
        if (e.NewValue is MapNode node)
        {
            Console.WriteLine($"[TvDevices_SelectedItemChanged] Node selected: {node.Name}, NodeType={node.NodeType}");
            
            var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (!ctrl && !shift)
            {
                ClearMapSelection();
                SelectNode(node, true);
                _selectionAnchor = node;
            }
            else if (ctrl)
            {
                SelectNode(node, !node.IsSelected);
                _selectionAnchor = node;
            }
            else if (shift)
            {
                SelectRange(node);
                UpdateTreeViewItemSelection();
            }
        }
        else
        {
            Console.WriteLine("[TvDevices_SelectedItemChanged] SelectedItem is not MapNode");
        }
    }
    
    private void TvDevices_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Console.WriteLine("[TvDevices_MouseLeftButtonDown] Event fired");
        
        if (_tvDevices == null) return;
        
        // e.Source 또는 e.OriginalSource에서 TreeViewItem 찾기
        var dep = e.Source as DependencyObject ?? e.OriginalSource as DependencyObject;
        TreeViewItem? clickedItem = null;
        
        // TreeViewItem을 찾을 때까지 부모를 따라 올라감
        while (dep is not null)
        {
            if (dep is TreeViewItem tvi)
            {
                clickedItem = tvi;
                break;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }

        // ToggleButton(확장/축소 버튼)을 직접 클릭한 경우 선택 처리하지 않음
        var toggleButton = e.OriginalSource as System.Windows.Controls.Primitives.ToggleButton;
        if (toggleButton != null)
        {
            Console.WriteLine("[TvDevices_MouseLeftButtonDown] ToggleButton clicked, skipping selection");
            return;
        }

        // TreeViewItem을 찾지 못한 경우, SelectedItem을 사용
        if (clickedItem == null)
        {
            Console.WriteLine("[TvDevices_MouseLeftButtonDown] clickedItem is null, trying SelectedItem");
            if (_tvDevices.SelectedItem is MapNode selectedNode)
            {
                Console.WriteLine($"[TvDevices_MouseLeftButtonDown] Using SelectedItem: {selectedNode.Name}, NodeType={selectedNode.NodeType}");
                var isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                var isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                if (!isCtrl && !isShift)
                {
                    ClearMapSelection();
                    SelectNode(selectedNode, true);
                    _selectionAnchor = selectedNode;
                }
                else if (isCtrl)
                {
                    SelectNode(selectedNode, !selectedNode.IsSelected);
                    _selectionAnchor = selectedNode;
                }
                else if (isShift)
                {
                    SelectRange(selectedNode);
                    UpdateTreeViewItemSelection();
                }
            }
            return;
        }

        // 확장/축소 영역(왼쪽 19px)을 클릭한 경우 선택 처리하지 않음
        if (clickedItem.Items.Count > 0)
        {
            var clickPosition = e.GetPosition(clickedItem);
            if (clickPosition.X < 19)
            {
                Console.WriteLine("[TvDevices_MouseLeftButtonDown] Expansion area clicked, skipping selection");
                return;
            }
        }

        _dragStartPoint = e.GetPosition(_tvDevices);

        var node = clickedItem.DataContext as MapNode;
        if (node is null)
        {
            Console.WriteLine("[TvDevices_MouseLeftButtonDown] node is null");
            return;
        }
        
        Console.WriteLine($"[TvDevices_MouseLeftButtonDown] Node clicked: {node.Name}, NodeType={node.NodeType}");

        var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        if (!ctrl && !shift)
        {
            ClearMapSelection();
            SelectNode(node, true);
            clickedItem.IsSelected = true;
            _selectionAnchor = node;
        }
        else if (ctrl)
        {
            SelectNode(node, !node.IsSelected);
            clickedItem.IsSelected = node.IsSelected;
            _selectionAnchor = node;
        }
        else if (shift)
        {
            SelectRange(node);
            UpdateTreeViewItemSelection();
        }
    }
    
    private void UpdateTreeViewItemSelection()
    {
        if (_tvDevices == null) return;
        
        // 모든 TreeViewItem의 선택 상태를 MapNode의 IsSelected와 동기화
        UpdateTreeViewItemSelectionRecursive(_tvDevices.Items, _tvDevices.ItemContainerGenerator);
    }
    
    private void UpdateTreeViewItemSelectionRecursive(ItemCollection items, ItemContainerGenerator generator)
    {
        foreach (var item in items)
        {
            if (item is MapNode node)
            {
                var container = generator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                {
                    container.IsSelected = node.IsSelected;
                    
                    // 자식 항목도 재귀적으로 업데이트
                    if (container.Items.Count > 0)
                    {
                        UpdateTreeViewItemSelectionRecursive(container.Items, container.ItemContainerGenerator);
                    }
                }
            }
        }
    }

    private void TvDevices_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        if (_tvDevices == null) return;
        var pos = e.GetPosition(_tvDevices);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var selected = _vm.SelectedMapNodes.Where(n => n.NodeType == MapNodeType.Device).ToList();
        if (selected.Count == 0) return;

        if (_tvDevices == null) return;
        DragDrop.DoDragDrop(_tvDevices, new DataObject("SnmpNms.MapNodes", selected), DragDropEffects.Move);
    }

    private void TvDevices_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("SnmpNms.MapNodes")) return;
        var dragged = e.Data.GetData("SnmpNms.MapNodes") as List<MapNode>;
        if (dragged is null || dragged.Count == 0) return;

        var targetNode = FindNodeFromOriginalSource(e.OriginalSource);
        if (targetNode is null) return;

        // 드롭 대상은 Root/Subnet만 허용
        if (targetNode.NodeType is not (MapNodeType.RootSubnet or MapNodeType.Subnet))
            return;

        foreach (var d in dragged)
        {
            if (d.Parent is null) continue;
            if (ReferenceEquals(d.Parent, targetNode)) continue;

            d.Parent.RemoveChild(d);
            targetNode.AddChild(d);
            _vm.AddEvent(EventSeverity.Info, d.Target?.EndpointKey, $"[Map] Moved to subnet: {targetNode.DisplayName}");
        }

        targetNode.IsExpanded = true;
        _vm.RootSubnet.RecomputeEffectiveStatus();
    }

    private void TvDevices_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        DeleteSelectedNodes();
        e.Handled = true;
    }

    private void MapNodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        var node = (sender as FrameworkElement)?.DataContext as MapNode;
        if (node is null) return;

        // Double-click subnet name -> select in Map Graph View
        if (node.NodeType is MapNodeType.Subnet or MapNodeType.RootSubnet)
        {
            // 새로운 Map Graph View에서는 Site 박스가 이미 표시되어 있음
            // 해당 Site를 선택하거나 스크롤하는 동작으로 변경 가능
            _vm.AddSystemInfo($"[Map] Selected subnet: {node.DisplayName}");
        }
    }

    // ContextMenu는 XAML 컴파일 시 IComponentConnector.Connect(connectionId)에서
    // MenuItem 이벤트를 상위 컨테이너(TabItem 등)에 AddHandler로 연결하려고 하다가
    // connectionId/target 타입 불일치(InvalidCast: MenuItem -> TabItem)를 유발할 수 있어
    // Click 이벤트 대신 CommandBinding으로 처리한다.

    private void CmdMapDelete_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        // 우클릭 시 선택이 보장되지 않으므로, 파라미터로 받은 노드를 우선 선택시키고 삭제
        if (e.Parameter is MapNode node)
        {
            ClearMapSelection();
            SelectNode(node, true);
        }
        DeleteSelectedNodes();
    }

    private void CmdMapOpen_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not MapNode node) return;
        if (node.NodeType is MapNodeType.Subnet or MapNodeType.RootSubnet)
        {
            // 새로운 Map Graph View에서는 Site 박스가 이미 표시되어 있음
            // Map View 탭으로 전환
            if (tabMain.Items.Cast<TabItem>().FirstOrDefault(t => t.Header?.ToString() == "Map View") is { } mapTab)
            {
                tabMain.SelectedItem = mapTab;
            }
            _vm.AddSystemInfo($"[Map] Open subnet: {node.DisplayName}");
        }
    }

    private void CmdMapProperties_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not MapNode node) return;
        ShowEditMapObjectDialog(node);
    }

    private void CmdMapQuickPoll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not MapNode node) return;
        if (node.Target is null) return;
        _vm.AddSystemInfo($"[Map] Quick Poll (Todo): {node.Target.DisplayName}");
    }

    private void CmdMapMibTable_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not MapNode node) return;
        if (node.Target is null) return;
        _vm.AddSystemInfo($"[Map] MIB Table (Todo): {node.Target.DisplayName}");
    }

    private MapNode? FindNodeFromOriginalSource(object? originalSource)
    {
        var dep = originalSource as DependencyObject;
        while (dep is not null)
        {
            if (dep is TreeViewItem tvi)
                return tvi.DataContext as MapNode;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return null;
    }

    private void ClearMapSelection()
    {
        foreach (var n in _vm.SelectedMapNodes.ToList())
        {
            n.IsSelected = false;
        }
        _vm.SelectedMapNodes.Clear();
    }

    private void SelectNode(MapNode node, bool selected)
    {
        Console.WriteLine($"[SelectNode] Called: node={node.Name}, NodeType={node.NodeType}, selected={selected}");
        node.IsSelected = selected;
        
        // TreeViewItem의 실제 선택 상태도 업데이트
        if (_tvDevices != null)
        {
            var container = FindTreeViewItemForNode(_tvDevices, node);
            if (container != null)
            {
                container.IsSelected = selected;
            }
        }
        
        if (selected)
        {
            if (!_vm.SelectedMapNodes.Contains(node))
                _vm.SelectedMapNodes.Add(node);

            // 모든 노드 타입에 대해 정보 출력
            Console.WriteLine($"[SelectNode] Node selected: {node.Name}");
            Console.WriteLine($"  NodeType: {node.NodeType}");
            Console.WriteLine($"  DisplayName: {node.DisplayName}");
            Console.WriteLine($"  EffectiveStatus: {node.EffectiveStatus}");
            
            if (node.NodeType == MapNodeType.Device && node.Target is not null)
            {
                // Device인 경우 상세 정보 출력
                Console.WriteLine($"[SelectNode] Device details:");
                Console.WriteLine($"  IP Address: {node.Target.IpAddress}:{node.Target.Port}");
                Console.WriteLine($"  Alias: {node.Target.Alias ?? "-"}");
                Console.WriteLine($"  Community: {node.Target.Community ?? "-"}");
                Console.WriteLine($"  Version: {node.Target.Version}");
                Console.WriteLine($"  Status: {node.Target.Status}");
                
                _vm.SelectedDevice = node.Target;
                _vm.SelectedDeviceNode = node;
                
                // SNMP Test 탭으로 자동 전환
                var snmpTestTab = tabMain.Items.Cast<TabItem>().FirstOrDefault(t => t.Header?.ToString() == "SNMP Test");
                if (snmpTestTab != null)
                {
                    tabMain.SelectedItem = snmpTestTab;
                }
                
                // IP Address와 Community 필드 업데이트 (탭 전환과 관계없이 항상 업데이트)
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (txtIp != null)
                    {
                        txtIp.Text = node.Target.IpAddress;
                        System.Diagnostics.Debug.WriteLine($"[SelectNode] Set txtIp.Text to {node.Target.IpAddress}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[SelectNode] txtIp is null!");
                    }
                    
                    if (txtCommunity != null)
                    {
                        txtCommunity.Text = node.Target.Community;
                        System.Diagnostics.Debug.WriteLine($"[SelectNode] Set txtCommunity.Text to {node.Target.Community}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[SelectNode] txtCommunity is null!");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // MIB Table 탭이 활성화되어 있고 MIB 노드가 선택되어 있으면 자동으로 테이블 로드
                if (tabMain.SelectedIndex == 5) // MIB Table 탭
                {
                    var selectedMibNode = GetSelectedMibNode();
                    if (selectedMibNode != null && !string.IsNullOrEmpty(selectedMibNode.Oid))
                    {
                        txtMibTableDevice.Text = node.Target.DisplayName;
                        _ = LoadMibTableData(selectedMibNode.Oid);
                    }
                    else
                    {
                        txtMibTableDevice.Text = node.Target.DisplayName;
                    }
                }
            }
        }
        else
        {
            _vm.SelectedMapNodes.Remove(node);
            if (node.NodeType == MapNodeType.Device && ReferenceEquals(_vm.SelectedDeviceNode, node))
                _vm.SelectedDeviceNode = null;
        }
    }
    
    private TreeViewItem? FindTreeViewItemForNode(ItemsControl parent, MapNode targetNode)
    {
        foreach (var item in parent.Items)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null)
            {
                if (container.DataContext == targetNode)
                    return container;
                
                var found = FindTreeViewItemForNode(container, targetNode);
                if (found != null) return found;
            }
        }
        return null;
    }
    
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                return t;
            
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private void SelectRange(MapNode node)
    {
        if (_selectionAnchor is null || _selectionAnchor.Parent is null || node.Parent is null ||
            !ReferenceEquals(_selectionAnchor.Parent, node.Parent))
        {
            ClearMapSelection();
            SelectNode(node, true);
            _selectionAnchor = node;
            return;
        }

        var siblings = _selectionAnchor.Parent.Children;
        var a = siblings.IndexOf(_selectionAnchor);
        var b = siblings.IndexOf(node);
        if (a < 0 || b < 0) return;

        var start = Math.Min(a, b);
        var end = Math.Max(a, b);

        ClearMapSelection();
        for (var i = start; i <= end; i++)
            SelectNode(siblings[i], true);
    }

    private void DeleteSelectedNodes()
    {
        var selected = _vm.SelectedMapNodes.ToList();
        if (selected.Count == 0) return;

        var deletedAny = false;
        foreach (var node in selected)
        {
            if (node.Parent is null) continue;
            if (node.NodeType is MapNodeType.RootSubnet) continue;
            if (node.NodeType is MapNodeType.Subnet && node.Children.Count > 0) continue; // 비어있을 때만 삭제

            if (node.Target is not null) _pollingService.RemoveTarget(node.Target);
            node.Parent.RemoveChild(node);
            if (node.NodeType == MapNodeType.Device) _vm.RemoveDeviceNode(node);
            _vm.AddSystemInfo($"[Map] Deleted: {node.DisplayName}");
            deletedAny = true;
        }

        if (deletedAny) MarkAsModified();

        ClearMapSelection();
        _vm.RootSubnet.RecomputeEffectiveStatus();
    }

    // MapNode에서 하위 모든 Device를 재귀적으로 찾는 헬퍼 메서드
    private List<UiSnmpTarget> GetAllDevicesFromNode(MapNode node)
    {
        var devices = new List<UiSnmpTarget>();
        
        if (node.NodeType == MapNodeType.Device && node.Target != null)
        {
            devices.Add(node.Target);
        }
        
        foreach (var child in node.Children)
        {
            devices.AddRange(GetAllDevicesFromNode(child));
        }
        
        return devices;
    }

    private void StartPoll_Click(object sender, RoutedEventArgs e)
    {
        // 모든 기기를 polling에 추가 (필터링은 표시에만 관련)
        var allDevices = GetAllDevicesFromNode(_vm.RootSubnet);
        
        if (allDevices.Count == 0)
        {
            _vm.AddSystemInfo("[System] StartPoll: no device to poll.");
            return;
        }
        
        // 모든 장비를 polling에 추가
        foreach (var device in allDevices)
        {
            _pollingService.AddTarget(device);
        }
        
        _pollingService.Start();
        _vm.IsPollingRunning = true;
        
        // Start 시에는 로그 남기지 않음 (필터링은 표시에만 관련)
    }

    private void StopPoll_Click(object sender, RoutedEventArgs e)
    {
        // 모든 기기를 polling에서 제거 (필터링은 표시에만 관련)
        var allDevices = GetAllDevicesFromNode(_vm.RootSubnet);
        
        // 모든 장비를 polling에서 제거
        foreach (var device in allDevices)
        {
            _pollingService.RemoveTarget(device);
        }
        
        _pollingService.Stop();
        _vm.IsPollingRunning = false;
        
        // Stop 시에는 로그 남기지 않음 (필터링은 표시에만 관련)
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearEvents();
    }

    // MIB Tree Methods
    private void InitializeMibTree()
    {
        try
        {
            var rootTree = _mibService.GetMibTree();
            // MIB 트리는 Activity Bar에서 Mib 뷰를 선택할 때 초기화됨
            
            // 디버깅: 트리 노드 개수 확인
            var totalNodes = CountNodes(rootTree);
            _vm.AddSystemInfo($"[System] MIB Tree initialized: {totalNodes} nodes");
            System.Diagnostics.Debug.WriteLine($"InitializeMibTree: Total nodes in tree: {totalNodes}");
            
            // UI가 로드된 후 기본 MIB 노드 선택 (Dispatcher 사용)
            Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SelectDefaultMibNode(rootTree);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Warning, null, $"[System] Failed to initialize MIB tree: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"InitializeMibTree Error: {ex}");
        }
    }

    private void SelectDefaultMibNode(MibTreeNode rootNode)
    {
        // 기본적으로 sysDescr 노드를 찾아서 선택
        var defaultNode = FindMibNodeByName(rootNode, "sysDescr");
        
        // sysDescr가 없으면 mgmt 노드의 첫 번째 자식 선택
        if (defaultNode == null)
        {
            var mgmtNode = rootNode.Children.FirstOrDefault(c => c.Name == "mgmt");
            if (mgmtNode != null && mgmtNode.Children.Count > 0)
            {
                var systemNode = mgmtNode.Children.FirstOrDefault(c => c.Oid == "1.3.6.1.2.1.1");
                if (systemNode != null && systemNode.Children.Count > 0)
                {
                    defaultNode = systemNode.Children.FirstOrDefault(c => !string.IsNullOrEmpty(c.Oid));
                }
            }
        }
        
        // 노드를 찾았으면 선택하고 트리 확장
        if (defaultNode != null)
        {
            // 부모 노드들을 모두 확장
            ExpandParentNodes(defaultNode, rootNode);
            
            // TreeViewItem을 찾아서 선택
            var treeViewItem = _treeMib != null ? FindTreeViewItem(_treeMib, defaultNode) : null;
            if (treeViewItem != null)
            {
                treeViewItem.IsSelected = true;
                treeViewItem.BringIntoView();
            }
            
            // MIB Table 탭도 기본으로 설정하고 데이터 로드
            if (_vm.SelectedDevice != null)
            {
                tabMain.SelectedIndex = 5; // MIB Table 탭
                txtMibTableOid.Text = $"{defaultNode.Name} ({defaultNode.Oid})";
                txtMibTableDevice.Text = _vm.SelectedDevice?.DisplayName ?? "-";
                _ = LoadMibTableData(defaultNode.Oid);
            }
        }
    }

    private TreeViewItem? FindTreeViewItem(ItemsControl parent, MibTreeNode targetNode)
    {
        foreach (var item in parent.Items)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null)
            {
                if (container.DataContext == targetNode)
                    return container;
                
                var found = FindTreeViewItem(container, targetNode);
                if (found != null) return found;
            }
        }
        return null;
    }

    private MibTreeNode? FindMibNodeByName(MibTreeNode node, string name)
    {
        if (node.Name == name) return node;
        
        foreach (var child in node.Children)
        {
            var found = FindMibNodeByName(child, name);
            if (found != null) return found;
        }
        
        return null;
    }

    private void ExpandParentNodes(MibTreeNode targetNode, MibTreeNode rootNode)
    {
        // 루트부터 타겟까지의 경로를 찾아서 확장
        var path = FindPath(rootNode, targetNode);
        foreach (var node in path)
        {
            node.IsExpanded = true;
        }
    }

    private List<MibTreeNode> FindPath(MibTreeNode current, MibTreeNode target)
    {
        var path = new List<MibTreeNode> { current };
        
        if (current == target) return path;
        
        foreach (var child in current.Children)
        {
            var childPath = FindPath(child, target);
            if (childPath.Count > 0 && childPath.Last() == target)
            {
                path.AddRange(childPath);
                return path;
            }
        }
        
        return new List<MibTreeNode>(); // 경로를 찾지 못함
    }

    private int CountNodes(MibTreeNode node)
    {
        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }
        return count;
    }

    private MibTreeNode? GetSelectedMibNode()
    {
        return _treeMib?.SelectedItem as MibTreeNode;
    }

    private void treeMib_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var node = e.NewValue as MibTreeNode;
        if (node == null) return;

        // TODO: MIB 상세 정보를 Sidebar에 추가할 때 활성화
        // if (_txtMibName != null) _txtMibName.Text = node.Name;
        // if (_txtMibOid != null) _txtMibOid.Text = node.Oid;
        // if (_txtMibType != null) _txtMibType.Text = node.NodeType.ToString();
        // if (_txtMibDescription != null) _txtMibDescription.Text = node.Description ?? "-";

        // MIB Table 탭에도 자동으로 정보 업데이트
        if (!string.IsNullOrEmpty(node.Oid))
        {
            txtMibTableOid.Text = $"{node.Name} ({node.Oid})";
            
            // 디바이스가 선택되어 있고, 테이블 노드이거나 리프 노드인 경우 자동으로 데이터 로드
            if (_vm.SelectedDevice != null && tabMain.SelectedIndex == 5) // MIB Table 탭이 활성화되어 있으면
            {
                _ = LoadMibTableData(node.Oid);
            }

            // SNMP Test 탭의 OID 필드에 자동으로 채워주기
            if (txtOid != null && !string.IsNullOrEmpty(node.Oid))
            {
                // OID 또는 이름 중 선택 (이름이 유효한 OID면 이름 우선, 아니면 OID 사용)
                // + 스칼라 OID면 .0을 붙여 인스턴스 OID로 제공 (GET/GET-NEXT UX 일관성)
                // (75aa832: nel child disappear 에서 사용하던 규칙을 포팅)
                bool isNameValidOid =
                    !string.IsNullOrEmpty(node.Name) &&
                    node.Name != node.Oid &&
                    System.Text.RegularExpressions.Regex.IsMatch(node.Name, @"^\d+(\.\d+)+$");

                var oidToUse = isNameValidOid ? node.Name : node.Oid;

                if (!oidToUse.EndsWith(".0"))
                {
                    var testName = _mibService.GetOidName(oidToUse);
                    if (testName != oidToUse && !testName.EndsWith(".0"))
                    {
                        oidToUse = oidToUse + ".0";
                    }
                }

                txtOid.Text = oidToUse;
            }
        }
    }

    // MIB Tree Context Menu Handlers
    private void MibTreeGet_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        // Get 요청 실행
        _ = ExecuteSnmpGet(node.Oid);
    }

    private void MibTreeGetNext_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        // GetNext 요청 실행
        _ = ExecuteSnmpGetNext(node.Oid);
    }

    private void MibTreeWalk_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        // Walk 요청 실행
        _ = ExecuteSnmpWalk(node.Oid);
    }

    private void MibTreeViewTable_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        // MIB Table 탭으로 전환하고 테이블 표시
        ShowMibTable(node.Oid, node.Name);
    }

    private void ShowMibTable(string tableOid, string tableName)
    {
        // MIB Table 탭 활성화
        tabMain.SelectedIndex = 5; // MIB Table 탭 인덱스 (현재 순서 기준)
        
        // 테이블 정보 표시
        txtMibTableOid.Text = $"{tableName} ({tableOid})";
        txtMibTableDevice.Text = _vm.SelectedDevice?.DisplayName ?? "-";
        
        // 테이블 데이터 로드
        _ = LoadMibTableData(tableOid);
    }

    // TabControl의 탭 선택 변경 이벤트 핸들러
    private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SNMP Test 탭이 선택되었고 SelectedDevice가 있으면 IP Address와 Community 업데이트
        var selectedTab = tabMain.SelectedItem as TabItem;
        if (selectedTab != null && selectedTab.Header?.ToString() == "SNMP Test" && _vm.SelectedDevice != null)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (txtIp != null)
                {
                    txtIp.Text = _vm.SelectedDevice.IpAddress;
                    System.Diagnostics.Debug.WriteLine($"[TabMain_SelectionChanged] Set txtIp.Text to {_vm.SelectedDevice.IpAddress}");
                }
                if (txtCommunity != null)
                {
                    txtCommunity.Text = _vm.SelectedDevice.Community;
                    System.Diagnostics.Debug.WriteLine($"[TabMain_SelectionChanged] Set txtCommunity.Text to {_vm.SelectedDevice.Community}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        // MIB Table 탭이 선택되었는지 확인
        if (tabMain.SelectedIndex == 5) // MIB Table 탭 인덱스
        {
            // MIB 트리에서 선택된 노드가 있으면 자동으로 테이블 표시
            var selectedMibNode = GetSelectedMibNode();
            if (selectedMibNode != null && !string.IsNullOrEmpty(selectedMibNode.Oid))
            {
                txtMibTableOid.Text = $"{selectedMibNode.Name} ({selectedMibNode.Oid})";
                txtMibTableDevice.Text = _vm.SelectedDevice?.DisplayName ?? "-";
                
                // 디바이스가 선택되어 있으면 자동으로 데이터 로드
                if (_vm.SelectedDevice != null)
                {
                    _ = LoadMibTableData(selectedMibNode.Oid);
                }
            }
        }
    }

    private async Task LoadMibTableData(string tableOid)
    {
        if (_vm.SelectedDevice == null) return;

        try
        {
            txtMibTableStatus.Text = $"Loading MIB table data from {_vm.SelectedDevice.IpAddress}...";
            dataGridMibTable.ItemsSource = null;
            dataGridMibTable.Columns.Clear();

            // SNMP WALK으로 테이블 데이터 가져오기
            var result = await _snmpClient.WalkAsync(_vm.SelectedDevice, tableOid, CancellationToken.None);
            
            if (!result.IsSuccess)
            {
                txtMibTableStatus.Text = $"Failed to load table data: {result.ErrorMessage ?? "Unknown error"}";
                _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"MIB Table Walk failed: {result.ErrorMessage}");
                return;
            }

            if (result.Variables.Count == 0)
            {
                txtMibTableStatus.Text = "No data returned from device.";
                return;
            }

            // 디버깅: 받은 변수 개수 확인
            System.Diagnostics.Debug.WriteLine($"LoadMibTableData: Received {result.Variables.Count} variables for OID {tableOid}");
            
            // 테이블 데이터 파싱 및 구조화
            var tableData = ParseWalkResultToTable(result.Variables, tableOid);
            
            System.Diagnostics.Debug.WriteLine($"LoadMibTableData: Parsed into {tableData.Count} rows");
            
            if (tableData.Count == 0)
            {
                txtMibTableStatus.Text = $"No table data found. Received {result.Variables.Count} variables but could not parse into table format.";
                // 디버깅: 첫 번째 변수 출력
                if (result.Variables.Count > 0)
                {
                    var firstVar = result.Variables[0];
                    System.Diagnostics.Debug.WriteLine($"First variable: OID={firstVar.Oid}, Value={firstVar.Value}");
                }
                return;
            }

            // DataGrid 컬럼 생성
            CreateMibTableColumns(tableData);

            // DataGrid에 데이터 바인딩
            dataGridMibTable.ItemsSource = tableData;
            
            // 디버깅: 컬럼 개수 확인
            System.Diagnostics.Debug.WriteLine($"LoadMibTableData: Created {dataGridMibTable.Columns.Count} columns");
            
            txtMibTableStatus.Text = $"Loaded {tableData.Count} row(s), {dataGridMibTable.Columns.Count} column(s) from MIB table.";
            _vm.AddSystemInfo($"[MIB Table] Loaded {tableData.Count} row(s) for {tableOid}");
        }
        catch (Exception ex)
        {
            txtMibTableStatus.Text = $"Error loading table data: {ex.Message}";
            _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice?.IpAddress, $"MIB Table error: {ex.Message}");
        }
    }

    private List<Dictionary<string, object>> ParseWalkResultToTable(List<SnmpVariable> variables, string baseOid)
    {
        var tableData = new List<Dictionary<string, object>>();
        
        if (variables.Count == 0) return tableData;
        
        // OID를 분석하여 인스턴스와 컬럼 분리
        // 예: 1.3.6.1.2.1.2.2.1.2.1 -> baseOid: 1.3.6.1.2.1.2.2.1.2, instance: 1
        var baseOidParts = baseOid.Split('.');
        var baseOidLength = baseOidParts.Length;
        
        // 변수들을 그룹화 (같은 인스턴스끼리)
        var instanceGroups = new Dictionary<string, Dictionary<string, string>>();
        
        foreach (var variable in variables)
        {
            var oidParts = variable.Oid.Split('.');
            
            // baseOid보다 긴 경우에만 처리
            if (oidParts.Length <= baseOidLength) continue;
            
            // 컬럼 OID 찾기: baseOid에서 시작하는 가장 긴 매칭 OID
            // 예: baseOid가 1.3.6.1.2.1.2.2.1이고 실제 OID가 1.3.6.1.2.1.2.2.1.2.1이면
            // 컬럼 OID는 1.3.6.1.2.1.2.2.1.2 (baseOid + 다음 숫자)
            string columnOid;
            string instanceKey;
            
            if (oidParts.Length == baseOidLength + 1)
            {
                // 간단한 경우: baseOid.숫자
                columnOid = variable.Oid;
                instanceKey = oidParts[baseOidLength];
            }
            else
            {
                // 복잡한 경우: baseOid.컬럼번호.인스턴스...
                // 컬럼 번호는 baseOid 직후의 숫자
                columnOid = string.Join(".", oidParts.Take(baseOidLength + 1));
                instanceKey = string.Join(".", oidParts.Skip(baseOidLength + 1));
            }
            
            var columnName = _mibService.GetOidName(columnOid);
            if (string.IsNullOrEmpty(columnName) || columnName == columnOid)
            {
                // 이름을 찾지 못한 경우 OID 사용
                columnName = columnOid;
            }
            
            if (!instanceGroups.ContainsKey(instanceKey))
            {
                instanceGroups[instanceKey] = new Dictionary<string, string>();
            }
            
            instanceGroups[instanceKey][columnName] = variable.Value;
        }
        
        // Dictionary를 List로 변환
        foreach (var instance in instanceGroups.OrderBy(i => i.Key))
        {
            var row = new Dictionary<string, object> { ["Instance"] = instance.Key };
            foreach (var column in instance.Value)
            {
                row[column.Key] = column.Value;
            }
            tableData.Add(row);
        }
        
        return tableData;
    }

    private void CreateMibTableColumns(List<Dictionary<string, object>> tableData)
    {
        dataGridMibTable.Columns.Clear();
        
        if (tableData.Count == 0) return;
        
        // 모든 컬럼 이름 수집
        var allColumns = new HashSet<string>();
        foreach (var row in tableData)
        {
            foreach (var key in row.Keys)
            {
                allColumns.Add(key);
            }
        }
        
        // Instance 컬럼을 첫 번째로 추가
        if (allColumns.Contains("Instance"))
        {
            dataGridMibTable.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Instance",
                Binding = new System.Windows.Data.Binding("[Instance]") { Mode = System.Windows.Data.BindingMode.OneWay },
                Width = 150,
                IsReadOnly = true
            });
            allColumns.Remove("Instance");
        }
        
        // 나머지 컬럼 추가
        foreach (var columnName in allColumns.OrderBy(c => c))
        {
            dataGridMibTable.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = columnName,
                Binding = new System.Windows.Data.Binding($"[{columnName}]") { Mode = System.Windows.Data.BindingMode.OneWay },
                Width = 150,
                IsReadOnly = true
            });
        }
    }

    private void BtnMibTableRefresh_Click(object sender, RoutedEventArgs e)
    {
        var currentOid = txtMibTableOid.Text;
        if (string.IsNullOrEmpty(currentOid) || currentOid == "-") return;
        
        // OID 추출 (괄호 안의 OID)
        var match = System.Text.RegularExpressions.Regex.Match(currentOid, @"\(([0-9.]+)\)");
        if (match.Success)
        {
            _ = LoadMibTableData(match.Groups[1].Value);
        }
    }

    private void MibTreeCopyOid_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        Clipboard.SetText(node.Oid);
        _vm.AddSystemInfo($"[System] Copied OID: {node.Oid}");
    }

    private void MibTreeCopyName_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Name)) return;
        
        Clipboard.SetText(node.Name);
        _vm.AddSystemInfo($"[System] Copied Name: {node.Name}");
    }

    // MIB Details Panel Button Handlers
    private void BtnMibGet_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        _ = ExecuteSnmpGet(node.Oid);
    }

    private void BtnMibGetNext_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        _ = ExecuteSnmpGetNext(node.Oid);
    }

    private void BtnMibWalk_Click(object sender, RoutedEventArgs e)
    {
        var node = GetSelectedMibNode();
        if (node == null || string.IsNullOrEmpty(node.Oid)) return;
        
        if (_vm.SelectedDevice == null)
        {
            _vm.AddEvent(EventSeverity.Warning, null, "Please select a device first");
            return;
        }

        _ = ExecuteSnmpWalk(node.Oid);
    }

    private async Task ExecuteSnmpGet(string oid)
    {
        if (_vm.SelectedDevice == null) return;

        try
        {
            _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"GET {oid} ({_mibService.GetOidName(oid)})");
            var result = await _snmpClient.GetAsync(_vm.SelectedDevice, oid);
            
            if (result.IsSuccess && result.Variables.Count > 0)
            {
                foreach (var variable in result.Variables)
                {
                    var displayOid = _mibService.GetOidName(variable.Oid);
                    _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"{displayOid} = {variable.Value}");
                }
            }
            else
            {
                _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"GET failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"GET error: {ex.Message}");
        }
    }

    private async Task ExecuteSnmpGetNext(string oid)
    {
        if (_vm.SelectedDevice == null) return;

        try
        {
            _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"GET-NEXT {oid} ({_mibService.GetOidName(oid)})");
            var result = await _snmpClient.GetNextAsync(_vm.SelectedDevice, oid);
            
            if (result.IsSuccess && result.Variables.Count > 0)
            {
                foreach (var variable in result.Variables)
                {
                    var displayOid = _mibService.GetOidName(variable.Oid);
                    _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"{displayOid} = {variable.Value}");
                }
            }
            else
            {
                _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"GET-NEXT failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"GET-NEXT error: {ex.Message}");
        }
    }

    private async Task ExecuteSnmpWalk(string oid)
    {
        if (_vm.SelectedDevice == null) return;

        try
        {
            _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"WALK {oid} ({_mibService.GetOidName(oid)})");
            var result = await _snmpClient.WalkAsync(_vm.SelectedDevice, oid, CancellationToken.None);
            
            if (result.IsSuccess && result.Variables.Count > 0)
            {
                _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"Walk completed: {result.Variables.Count} values");
                foreach (var variable in result.Variables.Take(20)) // 처음 20개만 표시
                {
                    var displayOid = _mibService.GetOidName(variable.Oid);
                    _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"{displayOid} = {variable.Value}");
                }
                if (result.Variables.Count > 20)
                {
                    _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.IpAddress, $"... and {result.Variables.Count - 20} more values");
                }
            }
            else
            {
                _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"WALK failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, _vm.SelectedDevice.IpAddress, $"WALK error: {ex.Message}");
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    // File Menu Handlers
    private void MenuFileNew_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        // 기존 데이터 초기화
        _vm.RootSubnet.Children.Clear();
        _vm.DeviceNodes.Clear();
        _vm.SelectedMapNodes.Clear();
        _vm.SelectedDevice = null;
        _vm.SelectedDeviceNode = null;

        // Default Subnet 다시 추가
        var defaultSubnet = new MapNode(MapNodeType.Subnet, "Default");
        _vm.RootSubnet.AddChild(defaultSubnet);
        _vm.RootSubnet.IsExpanded = true;
        defaultSubnet.IsExpanded = true;

        _currentFilePath = null;
        _isModified = false;
        UpdateTitle();

        _vm.AddSystemInfo("[File] New map created");
    }

    private void MenuFileOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        var dlg = new OpenFileDialog
        {
            Title = "Open Map File",
            Filter = "SNMP Map Files (*.snmpmap)|*.snmpmap|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".snmpmap"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var loadedRoot = _mapDataService.LoadFromFile(dlg.FileName);
            if (loadedRoot == null)
            {
                MessageBox.Show("Failed to load map file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 기존 데이터 초기화
            _vm.RootSubnet.Children.Clear();
            _vm.DeviceNodes.Clear();
            _vm.SelectedMapNodes.Clear();
            _vm.SelectedDevice = null;
            _vm.SelectedDeviceNode = null;

            // 로드된 데이터 적용
            foreach (var child in loadedRoot.Children)
            {
                _vm.RootSubnet.AddChild(child);
                CollectDeviceNodes(child);
            }

            _vm.RootSubnet.IsExpanded = loadedRoot.IsExpanded;
            _vm.RootSubnet.RecomputeEffectiveStatus();

            _currentFilePath = dlg.FileName;
            _isModified = false;
            UpdateTitle();

            _vm.AddSystemInfo($"[File] Loaded: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load map file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.AddEvent(EventSeverity.Error, null, $"[File] Load error: {ex.Message}");
        }
    }

    private void MenuFileSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            MenuFileSaveAs_Click(sender, e);
            return;
        }

        SaveToFile(_currentFilePath);
    }

    private void MenuFileSaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save Map File",
            Filter = "SNMP Map Files (*.snmpmap)|*.snmpmap|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".snmpmap",
            FileName = string.IsNullOrEmpty(_currentFilePath) ? "map" : Path.GetFileName(_currentFilePath)
        };

        if (dlg.ShowDialog() != true) return;

        SaveToFile(dlg.FileName);
    }

    private void SaveToFile(string filePath)
    {
        try
        {
            _mapDataService.SaveToFile(_vm.RootSubnet, filePath);
            _currentFilePath = filePath;
            _isModified = false;
            UpdateTitle();

            _vm.AddSystemInfo($"[File] Saved: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save map file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.AddEvent(EventSeverity.Error, null, $"[File] Save error: {ex.Message}");
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isModified) return true;

        var result = MessageBox.Show(
            "You have unsaved changes. Do you want to save before continuing?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
        {
            MenuFileSave_Click(this, new RoutedEventArgs());
            return !_isModified; // 저장 성공 시 _isModified가 false가 됨
        }

        return true; // No 선택 시
    }

    private void CollectDeviceNodes(MapNode node)
    {
        if (node.NodeType == MapNodeType.Device)
        {
            if (!_vm.DeviceNodes.Contains(node))
                _vm.DeviceNodes.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectDeviceNodes(child);
        }
    }

    private void UpdateTitle()
    {
        var fileName = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
        var modified = _isModified ? " *" : "";
        Title = $"SnmpNms - {fileName}{modified}";
    }

    public void MarkAsModified()
    {
        if (!_isModified)
        {
            _isModified = true;
            UpdateTitle();
        }
    }

    private void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddSystemInfo("[System] Refresh requested");
    }

    private void MenuConfigMibDatabase_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CompileMibsDialog(_mibService) { Owner = this };
        var result = dlg.ShowDialog();
        
        // 컴파일 후 MIB 트리 새로고침 (다이얼로그가 닫혔을 때 항상 새로고침)
        InitializeMibTree();
        _vm.AddSystemInfo("[System] MIB Database updated. MIB tree refreshed.");
    }

    private void MenuSnmpTest_Click(object sender, RoutedEventArgs e)
    {
        tabMain.SelectedIndex = 5; // SNMP Test 탭(현재 순서 기준)
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "SnmpNms (WPF)\nSNMPc 스타일 NMS를 목표로 하는 프로젝트입니다.",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuWindowsCascade_Click(object sender, RoutedEventArgs e)
    {
        // 새로운 Map Graph View에서는 Auto Arrange 기능으로 대체
        mapViewControl?.AutoArrange();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_trapListener != null && _trapListener.IsListening)
        {
            _trapListener.Stop();
            _vm.IsTrapListening = false;
            _vm.AddEvent(EventSeverity.Info, null, "[System] Trap Listener stopped");
        }
        
        if (_pollingService != null && _vm.IsPollingRunning)
        {
            _pollingService.Stop();
            _vm.IsPollingRunning = false;
            _vm.AddEvent(EventSeverity.Info, null, "[System] Auto Polling Stopped");
        }
        
        // 저장 서비스 파일 닫기
        _vm.LogSaveService.CloseCurrentFile();
        _vm.OutputSaveService.CloseCurrentFile();
        
        base.OnClosed(e);
    }

    private string CompareOidForDisplay(string oid)
    {
        // OID를 숫자 배열로 변환하여 정렬 가능한 문자열 생성
        // 예: "1.3.6.1.2.1.1.1" -> "0001.0003.0006.0001.0002.0001.0001.0001"
        var parts = oid.Split('.');
        var paddedParts = parts.Select(p => int.TryParse(p, out var num) ? num.ToString("D10") : p.PadLeft(10, '0')).ToArray();
        return string.Join(".", paddedParts);
    }

    // Search View 이벤트 핸들러
    private void SearchView_MapNodeSelected(object? sender, MapNode node)
    {
        try
        {
            // Map 뷰로 전환 (이 시점에서 _tvDevices가 생성됨)
            sidebar.CurrentView = ActivityBarView.Map;
            
            // 노드까지 경로 확장
            ExpandToMapNode(node);
            
            // ViewModel에 선택 상태 설정 (SelectNode 대신 직접 처리)
            ClearMapSelection();
            node.IsSelected = true;
            if (!_vm.SelectedMapNodes.Contains(node))
                _vm.SelectedMapNodes.Add(node);
            
            // Device인 경우 SelectedDevice 설정
            if (node.NodeType == MapNodeType.Device && node.Target != null)
            {
                _vm.SelectedDevice = node.Target;
                _vm.SelectedDeviceNode = node;
                
                // Device 탭으로 이동
                var deviceTab = tabMain.Items.Cast<TabItem>().FirstOrDefault(t => t.Header?.ToString() == "Device");
                if (deviceTab != null)
                {
                    tabMain.SelectedItem = deviceTab;
                }
            }
            
            // TreeViewItem 찾아서 포커스 (Dispatcher로 지연 실행)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_tvDevices != null)
                {
                    var container = FindTreeViewItemForNode(_tvDevices, node);
                    if (container != null)
                    {
                        container.IsSelected = true;
                        container.BringIntoView();
                        container.Focus();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            _vm.AddSystemInfo($"[Search] Selected: {node.DisplayName}");
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, null, $"[Search] Error selecting node: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"SearchView_MapNodeSelected error: {ex}");
        }
    }
    
    private void ExpandToMapNode(MapNode node)
    {
        // 부모 체인을 찾아서 모두 확장
        var parents = new List<MapNode>();
        var current = node.Parent;
        while (current != null)
        {
            parents.Insert(0, current);
            current = current.Parent;
        }
        
        foreach (var parent in parents)
        {
            parent.IsExpanded = true;
        }
    }

    private void SearchView_MibNodeSelected(object? sender, MibTreeNode node)
    {
        try
        {
            // MIB 뷰로 전환 (이 시점에서 _treeMib이 생성됨)
            sidebar.CurrentView = ActivityBarView.Mib;
            
            // MIB 트리에서 노드까지 경로 확장
            var rootTree = _mibService.GetMibTree();
            if (rootTree != null)
            {
                ExpandParentNodes(node, rootTree);
            }
            
            // TreeViewItem 찾아서 선택 (Dispatcher로 지연 실행)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_treeMib != null)
                {
                    var treeViewItem = FindTreeViewItem(_treeMib, node);
                    if (treeViewItem != null)
                    {
                        treeViewItem.IsSelected = true;
                        treeViewItem.BringIntoView();
                        treeViewItem.Focus();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            // OID 필드 업데이트
            if (txtOid != null && !string.IsNullOrEmpty(node.Oid))
            {
                txtOid.Text = node.Oid;
            }
            
            _vm.AddSystemInfo($"[Search] Selected MIB: {node.Name} ({node.Oid})");
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, null, $"[Search] Error selecting MIB node: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"SearchView_MibNodeSelected error: {ex}");
        }
    }
}
