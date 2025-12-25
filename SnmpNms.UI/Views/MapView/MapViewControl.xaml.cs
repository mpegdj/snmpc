using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SnmpNms.UI.Views.MapView;

public partial class MapViewControl : UserControl
{
    private int _windowSeq = 1;
    private readonly List<Border> _windows = new();

    public MapViewControl()
    {
        InitializeComponent();

        // 샘플 내부 창 2개를 기본 배치
        AddInternalWindow("Root Subnet", "Map objects will be shown here (Todo).");
        AddInternalWindow("Object Properties", "Selected object properties (Todo).");
        CascadeWindows();
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
        AddInternalWindow($"Window {_windowSeq++}", "Todo content");
        CascadeWindows();
    }

    private void AddInternalWindow(string title, string body)
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

        var content = new TextBlock
        {
            Text = body,
            Foreground = Brushes.White,
            Margin = new Thickness(10),
            TextWrapping = TextWrapping.Wrap
        };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(content);

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
    }
}


