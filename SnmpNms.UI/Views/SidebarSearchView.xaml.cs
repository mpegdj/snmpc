using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Views;

/// <summary>
/// 검색 결과 항목을 표시하기 위한 ViewModel
/// </summary>
public class SearchResultItem
{
    public string DisplayName { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Icon { get; set; } = "";
    public Brush IconColor { get; set; } = Brushes.Gray;
    public object? SourceObject { get; set; }
}

public enum SearchScope
{
    All,
    Map,
    Mib
}

public partial class SidebarSearchView : UserControl
{
    private MainViewModel? _viewModel;
    private IMibService? _mibService;
    private SearchScope _currentScope = SearchScope.All;
    private bool _isInitialized = false;
    
    private List<SearchResultItem> _mapResults = new();
    private List<SearchResultItem> _mibResults = new();
    
    /// <summary>
    /// Map 노드가 선택되었을 때 발생하는 이벤트
    /// </summary>
    public event EventHandler<MapNode>? MapNodeSelected;
    
    /// <summary>
    /// MIB 노드가 선택되었을 때 발생하는 이벤트
    /// </summary>
    public event EventHandler<MibTreeNode>? MibNodeSelected;

    public SidebarSearchView()
    {
        InitializeComponent();
        Loaded += SidebarSearchView_Loaded;
    }

    private void SidebarSearchView_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        SearchTextBox?.Focus();
    }

    /// <summary>
    /// MainViewModel 설정
    /// </summary>
    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// MibService 설정
    /// </summary>
    public void SetMibService(IMibService mibService)
    {
        _mibService = mibService;
    }

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchTextBox.Clear();
            e.Handled = true;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 실시간 검색 (디바운싱 없이 간단하게)
        PerformSearch();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Clear();
        SearchTextBox.Focus();
    }

    private void SearchScope_Changed(object sender, RoutedEventArgs e)
    {
        // 초기화 전에는 무시
        if (!_isInitialized) return;
        
        if (rbScopeAll?.IsChecked == true)
            _currentScope = SearchScope.All;
        else if (rbScopeMap?.IsChecked == true)
            _currentScope = SearchScope.Map;
        else if (rbScopeMib?.IsChecked == true)
            _currentScope = SearchScope.Mib;

        PerformSearch();
    }

    private void PerformSearch()
    {
        // 초기화 전에는 무시
        if (!_isInitialized) return;
        
        var query = SearchTextBox?.Text?.Trim() ?? "";
        
        _mapResults.Clear();
        _mibResults.Clear();

        if (string.IsNullOrEmpty(query))
        {
            UpdateUI();
            return;
        }

        // Map 검색
        if (_currentScope == SearchScope.All || _currentScope == SearchScope.Map)
        {
            SearchMapNodes(query);
        }

        // MIB 검색
        if (_currentScope == SearchScope.All || _currentScope == SearchScope.Mib)
        {
            SearchMibNodes(query);
        }

        UpdateUI();
    }

    private void SearchMapNodes(string query)
    {
        if (_viewModel == null) return;

        var q = query.ToLowerInvariant();
        SearchMapNodesRecursive(_viewModel.RootSubnet, q);
    }

    private void SearchMapNodesRecursive(MapNode parent, string query)
    {
        foreach (var child in parent.Children)
        {
            if (MatchesMapNode(child, query))
            {
                var result = new SearchResultItem
                {
                    DisplayName = child.DisplayName,
                    Detail = GetMapNodeDetail(child),
                    Icon = GetMapNodeIcon(child),
                    IconColor = GetMapNodeColor(child),
                    SourceObject = child
                };
                _mapResults.Add(result);
            }

            // 재귀적으로 하위 노드 검색
            if (child.Children.Count > 0)
            {
                SearchMapNodesRecursive(child, query);
            }
        }
    }

    private bool MatchesMapNode(MapNode node, string query)
    {
        // Name 검색
        if (node.Name?.ToLowerInvariant().Contains(query) == true)
            return true;

        // DisplayName 검색
        if (node.DisplayName?.ToLowerInvariant().Contains(query) == true)
            return true;

        // IP 주소 검색 (Device인 경우)
        if (node.Target?.IpAddress?.ToLowerInvariant().Contains(query) == true)
            return true;

        // Alias 검색
        if (node.Target?.Alias?.ToLowerInvariant().Contains(query) == true)
            return true;

        return false;
    }

    private string GetMapNodeDetail(MapNode node)
    {
        return node.NodeType switch
        {
            MapNodeType.Device => node.Target?.IpAddress ?? "",
            MapNodeType.Subnet => $"{node.Children.Count} items",
            MapNodeType.RootSubnet => $"{node.Children.Count} items",
            MapNodeType.Goto => "Goto link",
            _ => ""
        };
    }

    private string GetMapNodeIcon(MapNode node)
    {
        return node.NodeType switch
        {
            MapNodeType.Device => "\uE703",      // PC 아이콘
            MapNodeType.Subnet => "\uE8B7",      // 폴더 아이콘
            MapNodeType.RootSubnet => "\uE8B7",  // 폴더 아이콘
            MapNodeType.Goto => "\uE71B",        // 화살표 아이콘
            _ => "\uE7C3"                        // 기본 아이콘
        };
    }

    private Brush GetMapNodeColor(MapNode node)
    {
        return node.EffectiveStatus switch
        {
            DeviceStatus.Up => Brushes.LimeGreen,
            DeviceStatus.Down => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    private void SearchMibNodes(string query)
    {
        if (_mibService == null) return;

        var rootTree = _mibService.GetMibTree();
        if (rootTree == null) return;

        var q = query.ToLowerInvariant();
        SearchMibNodesRecursive(rootTree, q);
    }

    private void SearchMibNodesRecursive(MibTreeNode parent, string query)
    {
        // 현재 노드 검색
        if (MatchesMibNode(parent, query))
        {
            var result = new SearchResultItem
            {
                DisplayName = parent.Name,
                Detail = parent.Oid ?? "",
                Icon = GetMibNodeIcon(parent),
                IconColor = new SolidColorBrush(Color.FromRgb(0, 122, 204)), // VSCode Accent
                SourceObject = parent
            };
            _mibResults.Add(result);
        }

        // 재귀적으로 하위 노드 검색
        foreach (var child in parent.Children)
        {
            SearchMibNodesRecursive(child, query);
        }
    }

    private bool MatchesMibNode(MibTreeNode node, string query)
    {
        // Name 검색
        if (node.Name?.ToLowerInvariant().Contains(query) == true)
            return true;

        // OID 검색
        if (node.Oid?.ToLowerInvariant().Contains(query) == true)
            return true;

        // Description 검색
        if (node.Description?.ToLowerInvariant().Contains(query) == true)
            return true;

        return false;
    }

    private string GetMibNodeIcon(MibTreeNode node)
    {
        return node.NodeType switch
        {
            MibNodeType.Folder => "\uE8B7",       // 폴더 아이콘
            MibNodeType.Table => "\uE80A",        // 테이블 아이콘
            MibNodeType.Scalar => "\uE8A5",       // 문서 아이콘
            MibNodeType.CustomTable => "\uE80A", // 테이블 아이콘
            _ => "\uE7C3"                         // 기본 아이콘
        };
    }

    private void UpdateUI()
    {
        // 초기화 전에는 무시
        if (!_isInitialized) return;
        
        // UI 요소들이 null인지 확인 (강화된 체크)
        if (SearchTextBox == null || SearchSummaryText == null || MapResultsExpander == null || 
            MibResultsExpander == null || NoResultsText == null) return;
        
        var query = SearchTextBox.Text?.Trim() ?? "";
        var totalResults = _mapResults.Count + _mibResults.Count;

        // 검색 요약 텍스트 업데이트
        if (string.IsNullOrEmpty(query))
        {
            SearchSummaryText.Text = "";
        }
        else if (totalResults == 0)
        {
            SearchSummaryText.Text = "No results";
        }
        else
        {
            SearchSummaryText.Text = $"{totalResults} result{(totalResults != 1 ? "s" : "")}";
        }

        // Map 결과 업데이트
        if (_mapResults.Count > 0 && (_currentScope == SearchScope.All || _currentScope == SearchScope.Map))
        {
            MapResultsExpander.Visibility = Visibility.Visible;
            if (MapResultsHeader != null)
                MapResultsHeader.Text = $"MAP OBJECTS ({_mapResults.Count})";
            if (MapResultsList != null)
                MapResultsList.ItemsSource = _mapResults.Take(50).ToList(); // 최대 50개 표시
        }
        else
        {
            MapResultsExpander.Visibility = Visibility.Collapsed;
        }

        // MIB 결과 업데이트
        if (_mibResults.Count > 0 && (_currentScope == SearchScope.All || _currentScope == SearchScope.Mib))
        {
            MibResultsExpander.Visibility = Visibility.Visible;
            if (MibResultsHeader != null)
                MibResultsHeader.Text = $"MIB NODES ({_mibResults.Count})";
            if (MibResultsList != null)
                MibResultsList.ItemsSource = _mibResults.Take(50).ToList(); // 최대 50개 표시
        }
        else
        {
            MibResultsExpander.Visibility = Visibility.Collapsed;
        }

        // 결과 없음 메시지
        NoResultsText.Visibility = !string.IsNullOrEmpty(query) && totalResults == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MapResult_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is SearchResultItem item)
        {
            if (item.SourceObject is MapNode mapNode)
            {
                MapNodeSelected?.Invoke(this, mapNode);
            }
        }
    }

    private void MibResult_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is SearchResultItem item)
        {
            if (item.SourceObject is MibTreeNode mibNode)
            {
                MibNodeSelected?.Invoke(this, mibNode);
            }
        }
    }

    /// <summary>
    /// 검색 입력 필드에 포커스
    /// </summary>
    public void FocusSearchBox()
    {
        SearchTextBox?.Focus();
        SearchTextBox?.SelectAll();
    }
}

