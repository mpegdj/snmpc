using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SnmpNms.Core.Models;
using SnmpNms.UI.Converters;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Views.MapView;

public partial class MapViewControl : UserControl
{
    private bool _initialized;
    private readonly DeviceStatusToBackgroundConverter _statusBgConverter = new();
    private readonly Dictionary<MapNode, Border> _siteBoxes = new();
    
    // 설정
    private double _zoomLevel = 1.0;
    private const double ZoomStep = 0.1;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 2.0;
    private const int GridSize = 50;
    
    // 드래그 상태
    private Border? _draggingBox;
    private Point _dragStart;
    private double _dragStartLeft;
    private double _dragStartTop;

    public MapViewControl()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        DrawGrid();
        
        if (TryGetVm() is { } vm)
        {
            // Site(Subnet) 노드들을 박스로 표시
            BuildSiteBoxes(vm);
            
            // 컬렉션 변경 구독
            vm.RootSubnet.Children.CollectionChanged += (_, _) => RebuildSiteBoxes();
        }
    }

    private MainViewModel? TryGetVm() => DataContext as MainViewModel;

    #region Grid

    private void DrawGrid()
    {
        // XAML 초기화 중에는 GridCanvas/MapCanvas가 아직 null일 수 있음
        if (GridCanvas == null || MapCanvas == null) return;
        
        GridCanvas.Children.Clear();
        
        if (ShowGridCheck?.IsChecked != true) return;

        var dotBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        dotBrush.Freeze();

        for (var x = GridSize; x < MapCanvas.Width; x += GridSize)
        {
            for (var y = GridSize; y < MapCanvas.Height; y += GridSize)
            {
                var dot = new Ellipse
                {
                    Width = 2,
                    Height = 2,
                    Fill = dotBrush
                };
                Canvas.SetLeft(dot, x - 1);
                Canvas.SetTop(dot, y - 1);
                GridCanvas.Children.Add(dot);
            }
        }
    }

    private void ShowGrid_Changed(object sender, RoutedEventArgs e)
    {
        DrawGrid();
    }

    #endregion

    #region Zoom

    private void UpdateZoom()
    {
        CanvasScale.ScaleX = _zoomLevel;
        CanvasScale.ScaleY = _zoomLevel;
        ZoomLevelText.Text = $"{(int)(_zoomLevel * 100)}%";
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
        UpdateZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
        UpdateZoom();
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        
        if (e.Delta > 0)
            _zoomLevel = Math.Min(MaxZoom, _zoomLevel + ZoomStep);
        else
            _zoomLevel = Math.Max(MinZoom, _zoomLevel - ZoomStep);
        
        UpdateZoom();
        e.Handled = true;
    }

    #endregion

    #region Auto Arrange

    private void AutoArrange_Click(object sender, RoutedEventArgs e)
    {
        AutoArrange();
    }

    /// <summary>
    /// Site 박스들을 자동 정렬 (왼쪽 위부터)
    /// </summary>
    public void AutoArrange()
    {
        const double startX = 20;
        const double startY = 20;
        const double spacingX = 180;
        const double spacingY = 200;
        const int columns = 5;

        var index = 0;
        foreach (var box in _siteBoxes.Values)
        {
            var col = index % columns;
            var row = index / columns;
            
            var x = startX + col * spacingX;
            var y = startY + row * spacingY;
            
            if (SnapToGridCheck?.IsChecked == true)
            {
                x = SnapToGrid(x);
                y = SnapToGrid(y);
            }
            
            Canvas.SetLeft(box, x);
            Canvas.SetTop(box, y);
            
            // MapNode에 위치 저장
            if (box.Tag is MapNode node)
            {
                node.X = x;
                node.Y = y;
            }
            
            index++;
        }
    }

    private double SnapToGrid(double value)
    {
        return Math.Round(value / GridSize) * GridSize;
    }

    #endregion

    #region Site Boxes

    private void BuildSiteBoxes(MainViewModel vm)
    {
        _siteBoxes.Clear();
        
        // Canvas에서 SiteBoxesControl 내부의 ItemsControl 대신 직접 Canvas에 추가
        // (ItemsControl의 Canvas ItemsPanel은 위치 지정이 복잡하므로)
        
        var index = 0;
        foreach (var child in vm.RootSubnet.Children)
        {
            if (child.NodeType is MapNodeType.Subnet or MapNodeType.RootSubnet)
            {
                var box = CreateSiteBox(child);
                
                // 초기 위치 (Auto Arrange 스타일)
                var x = child.X > 0 ? child.X : 20 + (index % 5) * 180;
                var y = child.Y > 0 ? child.Y : 20 + (index / 5) * 200;
                
                Canvas.SetLeft(box, x);
                Canvas.SetTop(box, y);
                
                MapCanvas.Children.Add(box);
                _siteBoxes[child] = box;
                index++;
            }
        }
    }

    private void RebuildSiteBoxes()
    {
        // 기존 박스 제거
        foreach (var box in _siteBoxes.Values)
        {
            MapCanvas.Children.Remove(box);
        }
        _siteBoxes.Clear();
        
        if (TryGetVm() is { } vm)
        {
            BuildSiteBoxes(vm);
        }
    }

    private Border CreateSiteBox(MapNode subnet)
    {
        var box = new Border
        {
            MinWidth = 150,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Tag = subnet,
            Cursor = Cursors.Hand
        };

        var mainStack = new StackPanel();
        box.Child = mainStack;

        // 헤더 (Site 이름 + 톱니바퀴)
        var header = new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(4, 4, 0, 0)
        };
        
        // 헤더 배경색 바인딩 (Site 집계 상태)
        header.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(MapNode.EffectiveStatus))
        {
            Source = subnet,
            Converter = _statusBgConverter
        });

        var headerPanel = new DockPanel { LastChildFill = true };
        
        // 톱니바퀴 버튼
        var configBtn = new Button
        {
            Content = "⚙",
            FontSize = 12,
            Padding = new Thickness(4, 0, 4, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            Cursor = Cursors.Hand,
            ToolTip = "Site Config"
        };
        configBtn.Click += (_, _) => OnSiteConfigClick(subnet);
        DockPanel.SetDock(configBtn, Dock.Right);
        headerPanel.Children.Add(configBtn);
        
        // Site 이름
        var nameText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(MapNode.Name))
        {
            Source = subnet
        });
        headerPanel.Children.Add(nameText);
        
        header.Child = headerPanel;
        mainStack.Children.Add(header);

        // 장비 목록
        var deviceList = new ItemsControl
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
            Padding = new Thickness(0)
        };
        deviceList.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(MapNode.Children))
        {
            Source = subnet
        });

        // 장비 행 템플릿
        deviceList.ItemTemplate = CreateDeviceRowTemplate();
        
        mainStack.Children.Add(deviceList);

        // 드래그 이벤트
        header.MouseLeftButtonDown += (_, args) =>
        {
            _draggingBox = box;
            _dragStart = args.GetPosition(MapCanvas);
            _dragStartLeft = Canvas.GetLeft(box);
            _dragStartTop = Canvas.GetTop(box);
            header.CaptureMouse();
            args.Handled = true;
        };

        header.MouseMove += (_, args) =>
        {
            if (_draggingBox != box) return;
            
            var pos = args.GetPosition(MapCanvas);
            var newX = _dragStartLeft + (pos.X - _dragStart.X);
            var newY = _dragStartTop + (pos.Y - _dragStart.Y);
            
            Canvas.SetLeft(box, newX);
            Canvas.SetTop(box, newY);
        };

        header.MouseLeftButtonUp += (_, _) =>
        {
            if (_draggingBox != box) return;
            
            var finalX = Canvas.GetLeft(box);
            var finalY = Canvas.GetTop(box);
            
            // Snap to Grid
            if (SnapToGridCheck?.IsChecked == true)
            {
                finalX = SnapToGrid(finalX);
                finalY = SnapToGrid(finalY);
                Canvas.SetLeft(box, finalX);
                Canvas.SetTop(box, finalY);
            }
            
            // MapNode에 위치 저장
            subnet.X = finalX;
            subnet.Y = finalY;
            
            _draggingBox = null;
            header.ReleaseMouseCapture();
        };

        // 더블클릭 - Properties
        header.MouseLeftButtonDown += (_, args) =>
        {
            if (args.ClickCount == 2)
            {
                OnSiteConfigClick(subnet);
                args.Handled = true;
            }
        };

        return box;
    }

    private DataTemplate CreateDeviceRowTemplate()
    {
        var template = new DataTemplate(typeof(MapNode));
        
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
        borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)));
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        
        // 배경색 바인딩 (장비 상태)
        borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(MapNode.EffectiveStatus))
        {
            Converter = _statusBgConverter
        });

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(MapNode.DisplayName)));
        
        borderFactory.AppendChild(textFactory);
        template.VisualTree = borderFactory;
        
        return template;
    }

    private void OnSiteConfigClick(MapNode subnet)
    {
        // TODO: Site Config 다이얼로그 또는 패널 표시
        MessageBox.Show($"Site: {subnet.Name}\nDevices: {subnet.Children.Count}\nStatus: {subnet.EffectiveStatus}", 
            "Site Config", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 빈 공간 클릭 시 선택 해제
        if (e.OriginalSource == MapCanvas || e.OriginalSource == GridCanvas)
        {
            if (TryGetVm() is { } vm)
            {
                vm.SelectedMapNodes.Clear();
            }
        }
    }
}
