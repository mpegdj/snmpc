using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Shapes;
using SnmpNms.UI.Converters;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Views.MapView;

public partial class MapViewControl : UserControl
{
    private int _windowSeq = 1;
    private readonly List<Border> _windows = new();
    private readonly Dictionary<string, Border> _subnetWindows = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;
    private readonly DeviceStatusToBrushConverter _statusBrush = new();

    public MapViewControl()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        // DataContext(MainViewModel) 연결 이후 기본 창을 생성
        if (TryGetVm() is { } vm)
        {
            OpenSubnet(vm.DefaultSubnet.Name);
            AddInternalWindow("Object Properties", new TextBlock
            {
                Text = "Selected object properties (Todo).",
                Foreground = Brushes.White,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap
            });
            CascadeWindows();
        }
        else
        {
            AddInternalWindow("Map View", new TextBlock
            {
                Text = "MapViewControl DataContext is not MainViewModel. (Todo: inject VM)",
                Foreground = Brushes.White,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    public void OpenSubnet(string subnetName)
    {
        // 이미 열려 있으면 앞으로 가져오기
        if (_subnetWindows.TryGetValue(subnetName, out var existing))
        {
            BringToFront(existing);
            return;
        }

        var vm = TryGetVm();
        var subnet = vm is null ? null : FindSubnet(vm.RootSubnet, subnetName);

        var title = $"Subnet: {subnetName}";
        var content = subnet is null
            ? new TextBlock
            {
                Text = $"Subnet not found: {subnetName}",
                Foreground = Brushes.White,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap
            }
            : CreateSubnetContent(subnet);

        var w = AddInternalWindow(title, content);
        _subnetWindows[subnetName] = w;
        BringToFront(w);
    }

    public void CascadeWindows()
    {
        const double startX = 20;
        const double startY = 20;
        const double step = 28;

        for (var i = 0; i < _windows.Count; i++)
        {
            Canvas.SetLeft(_windows[i], startX + step * i);
            Canvas.SetTop(_windows[i], startY + step * i);
        }
    }

    private void Cascade_Click(object sender, RoutedEventArgs e) => CascadeWindows();

    private void AddWindow_Click(object sender, RoutedEventArgs e)
    {
        AddInternalWindow($"Window {_windowSeq++}", new TextBlock
        {
            Text = "Todo content",
            Foreground = Brushes.White,
            Margin = new Thickness(10),
            TextWrapping = TextWrapping.Wrap
        });
        CascadeWindows();
    }

    private Border AddInternalWindow(string title, UIElement body)
    {
        var header = new DockPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            LastChildFill = true,
            Height = 28
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var closeBtn = new Button
        {
            Content = "X",
            Width = 26,
            Height = 22,
            Margin = new Thickness(0, 2, 4, 2)
        };

        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);
        header.Children.Add(titleBlock);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(body);

        var windowBorder = new Border
        {
            Width = 360,
            Height = 220,
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 120)),
            CornerRadius = new CornerRadius(3),
            Child = root
        };

        // 드래그 이동(간단 구현)
        Point? dragStart = null;
        double startLeft = 0, startTop = 0;

        header.MouseLeftButtonDown += (_, args) =>
        {
            dragStart = args.GetPosition(MdiCanvas);
            startLeft = Canvas.GetLeft(windowBorder);
            startTop = Canvas.GetTop(windowBorder);
            header.CaptureMouse();
            args.Handled = true;
        };

        header.MouseMove += (_, args) =>
        {
            if (dragStart is null) return;
            var p = args.GetPosition(MdiCanvas);
            Canvas.SetLeft(windowBorder, startLeft + (p.X - dragStart.Value.X));
            Canvas.SetTop(windowBorder, startTop + (p.Y - dragStart.Value.Y));
        };

        header.MouseLeftButtonUp += (_, _) =>
        {
            dragStart = null;
            header.ReleaseMouseCapture();
        };

        closeBtn.Click += (_, _) =>
        {
            MdiCanvas.Children.Remove(windowBorder);
            _windows.Remove(windowBorder);
            var subnetKey = _subnetWindows.FirstOrDefault(kv => ReferenceEquals(kv.Value, windowBorder)).Key;
            if (!string.IsNullOrWhiteSpace(subnetKey)) _subnetWindows.Remove(subnetKey);
        };

        // Z-order: 클릭된 창을 앞으로
        windowBorder.MouseLeftButtonDown += (_, _) =>
        {
            var maxZ = _windows.Count == 0 ? 0 : _windows.Select(Panel.GetZIndex).Max();
            Panel.SetZIndex(windowBorder, maxZ + 1);
        };

        Panel.SetZIndex(windowBorder, _windows.Count + 1);
        _windows.Add(windowBorder);
        MdiCanvas.Children.Add(windowBorder);
        return windowBorder;
    }

    private void BringToFront(Border windowBorder)
    {
        var maxZ = _windows.Count == 0 ? 0 : _windows.Select(Panel.GetZIndex).Max();
        Panel.SetZIndex(windowBorder, maxZ + 1);
    }

    private MainViewModel? TryGetVm() => DataContext as MainViewModel;

    private static MapNode? FindSubnet(MapNode root, string subnetName)
    {
        if (root.NodeType is MapNodeType.Subnet or MapNodeType.RootSubnet)
        {
            if (string.Equals(root.Name, subnetName, StringComparison.OrdinalIgnoreCase))
                return root;
        }

        foreach (var c in root.Children)
        {
            var found = FindSubnet(c, subnetName);
            if (found is not null) return found;
        }
        return null;
    }

    private UIElement CreateSubnetContent(MapNode subnet)
    {
        // DataContext = subnet, ItemsSource = Children (ObservableCollection) -> 추가/삭제 즉시 반영
        var items = new ItemsControl
        {
            Margin = new Thickness(10)
        };
        items.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MapNode.Children)));

        // WrapPanel layout
        var panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
        panelFactory.SetValue(WrapPanel.ItemWidthProperty, 150.0);
        panelFactory.SetValue(WrapPanel.ItemHeightProperty, 64.0);
        items.ItemsPanel = new ItemsPanelTemplate(panelFactory);

        // Item template: [status dot] Name / Type
        var rootBorder = new FrameworkElementFactory(typeof(Border));
        rootBorder.SetValue(Border.MarginProperty, new Thickness(6));
        rootBorder.SetValue(Border.PaddingProperty, new Thickness(8));
        rootBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        rootBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
        rootBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)));
        rootBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        rootBorder.AppendChild(stack);

        var header = new FrameworkElementFactory(typeof(StackPanel));
        header.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        stack.AppendChild(header);

        var dot = new FrameworkElementFactory(typeof(Rectangle));
        dot.SetValue(Rectangle.WidthProperty, 10.0);
        dot.SetValue(Rectangle.HeightProperty, 10.0);
        dot.SetValue(Rectangle.MarginProperty, new Thickness(0, 2, 6, 0));
        dot.SetBinding(Shape.FillProperty, new Binding(nameof(MapNode.EffectiveStatus)) { Converter = _statusBrush });
        header.AppendChild(dot);

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(MapNode.DisplayName)));
        header.AppendChild(name);

        var type = new FrameworkElementFactory(typeof(TextBlock));
        type.SetValue(TextBlock.MarginProperty, new Thickness(0, 6, 0, 0));
        type.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));
        type.SetBinding(TextBlock.TextProperty, new Binding(nameof(MapNode.NodeType)));
        stack.AppendChild(type);

        items.ItemTemplate = new DataTemplate { VisualTree = rootBorder };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = items
        };

        var root = new DockPanel { LastChildFill = true };
        var hint = new TextBlock
        {
            Text = "Add Device/Subnet/Goto 하면 이 목록이 즉시 갱신됩니다.",
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            Margin = new Thickness(10, 8, 10, 0)
        };
        DockPanel.SetDock(hint, Dock.Top);
        root.Children.Add(hint);
        root.Children.Add(scroll);

        root.DataContext = subnet;
        return root;
    }
}


