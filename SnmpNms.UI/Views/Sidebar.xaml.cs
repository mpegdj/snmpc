using System.Windows;
using System.Windows.Controls;

namespace SnmpNms.UI.Views;

public partial class Sidebar : UserControl
{
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(Sidebar), 
            new PropertyMetadata("EXPLORER"));

    public static readonly DependencyProperty CurrentContentProperty =
        DependencyProperty.Register(nameof(CurrentContent), typeof(object), typeof(Sidebar));

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public object? CurrentContent
    {
        get => GetValue(CurrentContentProperty);
        set => SetValue(CurrentContentProperty, value);
    }

    public Sidebar()
    {
        InitializeComponent();
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        // Toggle sidebar visibility (will be handled by parent)
        var parent = Parent as FrameworkElement;
        if (parent != null)
        {
            parent.Visibility = parent.Visibility == Visibility.Visible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }
    }
}

