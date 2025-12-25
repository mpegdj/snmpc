using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.Infrastructure;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;
using SnmpNms.UI.Views.Dialogs;

namespace SnmpNms.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISnmpClient _snmpClient;
    private readonly IMibService _mibService;
    private readonly IPollingService _pollingService;
    private readonly MainViewModel _vm;

    private Point _dragStartPoint;
    private MapNode? _selectionAnchor;

    public MainWindow()
    {
        InitializeComponent();
        
        // DI 컨테이너 없이 수동 주입
        _snmpClient = new SnmpClient();
        _mibService = new MibService();
        _pollingService = new PollingService(_snmpClient);
        _vm = new MainViewModel();
        DataContext = _vm;

        // Polling 이벤트 연결
        _pollingService.OnPollingResult += PollingService_OnPollingResult;

        // MIB 파일 로드 (Mib 폴더가 실행 파일 위치 또는 상위에 있다고 가정)
        LoadMibs();
        
        // MIB 트리 초기화
        InitializeMibTree();

        // 기본 디바이스(샘플) 추가
        var defaultDevice = new UiSnmpTarget
        {
            IpAddress = "127.0.0.1",
            Community = "public",
            Version = SnmpVersion.V2c,
            Timeout = 3000
        };
        _vm.AddDeviceToSubnet(defaultDevice);
        _vm.SelectedDevice = defaultDevice;
        _vm.AddSystemInfo("[System] Map Selection Tree ready (Root Subnet/Default).");
    }

    // --- Edit Button Bar: Add Map Objects (SNMPc style) ---
    private void FindMapObjects_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DiscoveryPollingAgentsDialog(_snmpClient, _vm) { Owner = this };
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
        var dlg = new MapObjectPropertiesDialog(type, _snmpClient) { Owner = this };

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
                break;
            }
            case MapObjectType.Subnet:
            {
                _vm.AddSubnet(dlg.Result.Alias, parent);
                _vm.AddSystemInfo($"[Map] Subnet added: {dlg.Result.Alias}");
                break;
            }
            case MapObjectType.Goto:
            {
                _vm.AddGoto(dlg.Result.Alias, dlg.Result.GotoSubnetName, parent);
                _vm.AddSystemInfo($"[Map] Goto added: {dlg.Result.Alias} -> {dlg.Result.GotoSubnetName}");
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
                lblStatus.Content = $"Up - {e.Target.IpAddress} ({DateTime.Now:HH:mm:ss})";
                lblStatus.Foreground = Brushes.Green;
                SetDeviceStatus($"{e.Target.IpAddress}:{e.Target.Port}", DeviceStatus.Up);
                // Polling 로그는 너무 많을 수 있으므로 상태 변경 시에만 찍거나, 별도 로그창 사용 권장
                // 여기서는 간단하게 시간 갱신
                // txtResult.AppendText($"[Poll] {e.Target.IpAddress} is Alive ({e.ResponseTime}ms)\n");
            }
            else
            {
                lblStatus.Content = $"Down - {e.Target.IpAddress} ({DateTime.Now:HH:mm:ss})";
                lblStatus.Foreground = Brushes.Red;
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
        var target = BuildTargetFromInputs();

        _pollingService.AddTarget(target);
        _pollingService.Start();
        _vm.AddEvent(EventSeverity.Info, target.EndpointKey, "[System] Auto Polling Started");
    }

    private void ChkAutoPoll_Unchecked(object sender, RoutedEventArgs e)
    {
        var target = BuildTargetFromInputs(minimal: true);
        _pollingService.RemoveTarget(target);
        _pollingService.Stop(); // 현재는 단순화를 위해 전체 Stop
        
        _vm.AddSystemInfo("[System] Auto Polling Stopped");
        lblStatus.Content = "Unknown";
        lblStatus.Foreground = Brushes.Gray;
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

            var result = await _snmpClient.GetAsync(target, oid);

            if (result.IsSuccess)
            {
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
                _vm.AddEvent(EventSeverity.Info, $"{target.IpAddress}:{target.Port}", sb.ToString().TrimEnd());
            }
            else
            {
                _vm.AddEvent(EventSeverity.Error, $"{target.IpAddress}:{target.Port}", $"Failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _vm.AddEvent(EventSeverity.Error, $"{txtIp.Text}:161", $"Error: {ex.Message}");
        }
        finally
        {
            btnGet.IsEnabled = true;
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
    private void TvDevices_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // SNMPc 매뉴얼: "Single-click on the small box to the left of a subnet icon to open or close"
        // Expander(▶/▼) 클릭은 기본 동작(expand/collapse)을 허용해야 함
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null)
        {
            // TreeViewItem의 ToggleButton(expander)을 클릭한 경우 기본 동작 허용
            if (dep is System.Windows.Controls.Primitives.ToggleButton)
            {
                e.Handled = false;
                return;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }

        _dragStartPoint = e.GetPosition(tvDevices);

        var node = FindNodeFromOriginalSource(e.OriginalSource);
        if (node is null) return;

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
        }

        e.Handled = true; // 기본 TreeView 단일 선택 동작 차단
    }

    private void TvDevices_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(tvDevices);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var selected = _vm.SelectedMapNodes.Where(n => n.NodeType == MapNodeType.Device).ToList();
        if (selected.Count == 0) return;

        DragDrop.DoDragDrop(tvDevices, new DataObject("SnmpNms.MapNodes", selected), DragDropEffects.Move);
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

        // Double-click subnet name -> open subnet as Map View internal window
        if (node.NodeType is MapNodeType.Subnet or MapNodeType.RootSubnet)
        {
            mapViewControl?.OpenSubnet(node.DisplayName);
            mapViewControl?.CascadeWindows();
            _vm.AddSystemInfo($"[Map] Open subnet: {node.DisplayName}");
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
            mapViewControl?.OpenSubnet(node.DisplayName);
            mapViewControl?.CascadeWindows();
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
        node.IsSelected = selected;
        if (selected)
        {
            if (!_vm.SelectedMapNodes.Contains(node))
                _vm.SelectedMapNodes.Add(node);

            if (node.NodeType == MapNodeType.Device && node.Target is not null)
            {
                _vm.SelectedDevice = node.Target;
                _vm.SelectedDeviceNode = node;
                txtIp.Text = node.Target.IpAddress;
                txtCommunity.Text = node.Target.Community;
                
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

        foreach (var node in selected)
        {
            if (node.Parent is null) continue;
            if (node.NodeType is MapNodeType.RootSubnet) continue;
            if (node.NodeType is MapNodeType.Subnet && node.Children.Count > 0) continue; // 비어있을 때만 삭제

            if (node.Target is not null) _pollingService.RemoveTarget(node.Target);
            node.Parent.RemoveChild(node);
            if (node.NodeType == MapNodeType.Device) _vm.RemoveDeviceNode(node);
            _vm.AddSystemInfo($"[Map] Deleted: {node.DisplayName}");
        }

        ClearMapSelection();
        _vm.RootSubnet.RecomputeEffectiveStatus();
    }

    private void StartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDevice is null)
        {
            _vm.AddSystemInfo("[System] StartPoll: no device selected.");
            return;
        }

        _pollingService.AddTarget(_vm.SelectedDevice);
        _pollingService.Start();
        chkAutoPoll.IsChecked = true;
        _vm.AddEvent(EventSeverity.Info, _vm.SelectedDevice.EndpointKey, "[System] Polling started");
    }

    private void StopPoll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDevice is not null)
        {
            _pollingService.RemoveTarget(_vm.SelectedDevice);
        }
        _pollingService.Stop();
        chkAutoPoll.IsChecked = false;
        _vm.AddSystemInfo("[System] Polling stopped");
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
            treeMib.ItemsSource = new[] { rootTree };
            
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
            var treeViewItem = FindTreeViewItem(treeMib, defaultNode);
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
        return treeMib.SelectedItem as MibTreeNode;
    }

    private void treeMib_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var node = e.NewValue as MibTreeNode;
        if (node == null) return;

        txtMibName.Text = node.Name;
        txtMibOid.Text = node.Oid;
        txtMibType.Text = node.NodeType.ToString();
        txtMibDescription.Text = node.Description ?? "-";

        // MIB Table 탭에도 자동으로 정보 업데이트
        if (!string.IsNullOrEmpty(node.Oid))
        {
            txtMibTableOid.Text = $"{node.Name} ({node.Oid})";
            
            // 디바이스가 선택되어 있고, 테이블 노드이거나 리프 노드인 경우 자동으로 데이터 로드
            if (_vm.SelectedDevice != null && tabMain.SelectedIndex == 5) // MIB Table 탭이 활성화되어 있으면
            {
                _ = LoadMibTableData(node.Oid);
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
            var result = await _snmpClient.WalkAsync(_vm.SelectedDevice, tableOid);
            
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
            var result = await _snmpClient.WalkAsync(_vm.SelectedDevice, oid);
            
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
        // SNMPc 스타일: View Window Area 내부 창 정렬(Cascade)
        // 현재는 Map View 탭 내부에서 겹치는 내부 창을 제공한다.
        mapViewControl?.CascadeWindows();
    }
}
