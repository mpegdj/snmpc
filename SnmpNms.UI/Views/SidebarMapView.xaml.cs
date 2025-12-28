using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnmpNms.UI.Models;
using SnmpNms.UI.ViewModels;

namespace SnmpNms.UI.Views;

public partial class SidebarMapView : UserControl
{
    public event MouseButtonEventHandler? MapNodeTextMouseLeftButtonDown;
    
    // 검색 관련
    private List<MapNode> _searchResults = new();
    private int _searchIndex = -1;

    public SidebarMapView()
    {
        InitializeComponent();
    }

    private void MapNodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        MapNodeTextMouseLeftButtonDown?.Invoke(sender, e);
    }
    
    // MainWindow에서 직접 접근할 수 있도록
    public TreeView TreeView => tvDevices;
    
    #region Search
    
    /// <summary>
    /// 외부에서 검색 패널을 열 수 있도록 (Ctrl+F 등)
    /// </summary>
    public void OpenSearch()
    {
        SearchPanel.Visibility = Visibility.Visible;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }
    
    public void CloseSearch()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        _searchResults.Clear();
        _searchIndex = -1;
        UpdateSearchResultText();
    }
    
    private void CloseSearch_Click(object sender, RoutedEventArgs e)
    {
        CloseSearch();
    }
    
    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                NavigateSearchResult(-1);
            else
                NavigateSearchResult(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseSearch();
            e.Handled = true;
        }
    }
    
    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PerformSearch();
    }
    
    private void SearchPrev_Click(object sender, RoutedEventArgs e)
    {
        NavigateSearchResult(-1);
    }
    
    private void SearchNext_Click(object sender, RoutedEventArgs e)
    {
        NavigateSearchResult(1);
    }
    
    private void PerformSearch()
    {
        _searchResults.Clear();
        _searchIndex = -1;
        
        var query = SearchTextBox?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            UpdateSearchResultText();
            return;
        }
        
        // DataContext에서 RootSubnet 가져오기
        if (DataContext is not MainViewModel vm)
        {
            UpdateSearchResultText();
            return;
        }
        
        // 모든 노드 검색
        SearchNodes(vm.RootSubnet, query);
        
        UpdateSearchResultText();
        
        // 결과가 있으면 첫 번째로 이동
        if (_searchResults.Count > 0)
        {
            _searchIndex = 0;
            NavigateToCurrentResult();
        }
    }
    
    private void SearchNodes(MapNode parent, string query)
    {
        foreach (var child in parent.Children)
        {
            if (MatchesQuery(child, query))
            {
                _searchResults.Add(child);
            }
            
            // 재귀적으로 하위 노드 검색
            if (child.Children.Count > 0)
            {
                SearchNodes(child, query);
            }
        }
    }
    
    private bool MatchesQuery(MapNode node, string query)
    {
        var q = query.ToLowerInvariant();
        
        // Name 검색
        if (node.Name?.ToLowerInvariant().Contains(q) == true)
            return true;
        
        // DisplayName 검색
        if (node.DisplayName?.ToLowerInvariant().Contains(q) == true)
            return true;
        
        // IP 주소 검색 (Device인 경우)
        if (node.Target?.IpAddress?.ToLowerInvariant().Contains(q) == true)
            return true;
        
        // Alias 검색
        if (node.Target?.Alias?.ToLowerInvariant().Contains(q) == true)
            return true;
        
        return false;
    }
    
    private void NavigateSearchResult(int direction)
    {
        if (_searchResults.Count == 0) return;
        
        _searchIndex += direction;
        
        // 순환
        if (_searchIndex < 0) _searchIndex = _searchResults.Count - 1;
        if (_searchIndex >= _searchResults.Count) _searchIndex = 0;
        
        NavigateToCurrentResult();
        UpdateSearchResultText();
    }
    
    private void NavigateToCurrentResult()
    {
        if (_searchIndex < 0 || _searchIndex >= _searchResults.Count) return;
        
        var node = _searchResults[_searchIndex];
        
        // 부모 노드들을 모두 확장
        ExpandToNode(node);
        
        // TreeViewItem 찾아서 선택
        SelectNodeInTreeView(node);
    }
    
    private void ExpandToNode(MapNode node)
    {
        // 부모 체인을 찾아서 모두 확장
        var parents = new List<MapNode>();
        var current = FindParent(node);
        while (current != null)
        {
            parents.Insert(0, current);
            current = FindParent(current);
        }
        
        foreach (var parent in parents)
        {
            parent.IsExpanded = true;
        }
    }
    
    private MapNode? FindParent(MapNode node)
    {
        if (DataContext is not MainViewModel vm) return null;
        return FindParentRecursive(vm.RootSubnet, node);
    }
    
    private MapNode? FindParentRecursive(MapNode parent, MapNode target)
    {
        foreach (var child in parent.Children)
        {
            if (child == target) return parent;
            
            var found = FindParentRecursive(child, target);
            if (found != null) return found;
        }
        return null;
    }
    
    private void SelectNodeInTreeView(MapNode node)
    {
        // TreeViewItem을 찾아서 선택
        var container = FindTreeViewItem(tvDevices, node);
        if (container != null)
        {
            container.IsSelected = true;
            container.BringIntoView();
            container.Focus();
        }
    }
    
    private TreeViewItem? FindTreeViewItem(ItemsControl parent, MapNode target)
    {
        if (parent == null) return null;
        
        foreach (var item in parent.Items)
        {
            if (item is MapNode mapNode)
            {
                var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container == null) continue;
                
                if (mapNode == target)
                {
                    return container;
                }
                
                // 재귀적으로 하위 검색
                container.IsExpanded = true;
                container.UpdateLayout();
                var found = FindTreeViewItem(container, target);
                if (found != null) return found;
            }
        }
        
        return null;
    }
    
    private void UpdateSearchResultText()
    {
        if (SearchResultText == null) return;
        
        if (_searchResults.Count == 0)
        {
            var query = SearchTextBox?.Text?.Trim() ?? "";
            SearchResultText.Text = string.IsNullOrEmpty(query) ? "" : "0";
        }
        else
        {
            SearchResultText.Text = $"{_searchIndex + 1}/{_searchResults.Count}";
        }
    }
    
    #endregion
}
