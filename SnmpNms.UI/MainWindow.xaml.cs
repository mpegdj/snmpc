using System.IO;
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
    private void EditAddDeviceObject_Click(object sender, RoutedEventArgs e) => ShowAddMapObjectDialog(MapObjectType.Device);
    private void EditAddSubnet_Click(object sender, RoutedEventArgs e) => ShowAddMapObjectDialog(MapObjectType.Subnet);
    private void EditAddGoto_Click(object sender, RoutedEventArgs e) => ShowAddMapObjectDialog(MapObjectType.Goto);

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
                    Retries = dlg.Result.Retries
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
        _vm.AddSystemInfo($"[Map] Properties (Todo): {node.DisplayName}");
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

    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddSystemInfo("[System] Refresh requested");
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
