using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Views;

public partial class SidebarMibView : UserControl
{
    public event RoutedPropertyChangedEventHandler<object>? SelectedItemChanged;
    public event RoutedEventHandler? MibTreeGetClick;
    public event RoutedEventHandler? MibTreeGetNextClick;
    public event RoutedEventHandler? MibTreeWalkClick;
    public event RoutedEventHandler? MibTreeViewTableClick;
    public event RoutedEventHandler? MibTreeCopyOidClick;
    public event RoutedEventHandler? MibTreeCopyNameClick;
    
    // 검색 관련
    private List<MibTreeNode> _searchResults = new();
    private int _searchIndex = -1;

    public SidebarMibView()
    {
        InitializeComponent();
        treeMib.SelectedItemChanged += (s, e) => SelectedItemChanged?.Invoke(s, e);
    }
    
    // MainWindow에서 직접 접근할 수 있도록
    public TreeView TreeView => treeMib;
    
    private void MibTreeGet_Click(object sender, RoutedEventArgs e)
    {
        MibTreeGetClick?.Invoke(sender, e);
    }
    
    private void MibTreeGetNext_Click(object sender, RoutedEventArgs e)
    {
        MibTreeGetNextClick?.Invoke(sender, e);
    }
    
    private void MibTreeWalk_Click(object sender, RoutedEventArgs e)
    {
        MibTreeWalkClick?.Invoke(sender, e);
    }
    
    private void MibTreeViewTable_Click(object sender, RoutedEventArgs e)
    {
        MibTreeViewTableClick?.Invoke(sender, e);
    }
    
    private void MibTreeCopyOid_Click(object sender, RoutedEventArgs e)
    {
        MibTreeCopyOidClick?.Invoke(sender, e);
    }
    
    private void MibTreeCopyName_Click(object sender, RoutedEventArgs e)
    {
        MibTreeCopyNameClick?.Invoke(sender, e);
    }
    
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
        
        // TreeView의 ItemsSource에서 루트 노드 가져오기
        if (treeMib.ItemsSource == null)
        {
            UpdateSearchResultText();
            return;
        }
        
        foreach (var item in treeMib.ItemsSource)
        {
            if (item is MibTreeNode rootNode)
            {
                SearchNodes(rootNode, query);
            }
        }
        
        UpdateSearchResultText();
        
        // 결과가 있으면 첫 번째로 이동
        if (_searchResults.Count > 0)
        {
            _searchIndex = 0;
            NavigateToCurrentResult();
        }
    }
    
    private void SearchNodes(MibTreeNode parent, string query)
    {
        if (MatchesQuery(parent, query))
        {
            _searchResults.Add(parent);
        }
        
        foreach (var child in parent.Children)
        {
            SearchNodes(child, query);
        }
    }
    
    private bool MatchesQuery(MibTreeNode node, string query)
    {
        var q = query.ToLowerInvariant();
        
        // Name 검색
        if (node.Name?.ToLowerInvariant().Contains(q) == true)
            return true;
        
        // OID 검색
        if (node.Oid?.ToLowerInvariant().Contains(q) == true)
            return true;
        
        // Description 검색
        if (node.Description?.ToLowerInvariant().Contains(q) == true)
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
    
    private void ExpandToNode(MibTreeNode node)
    {
        // 부모 체인을 찾아서 모두 확장
        var parents = new List<MibTreeNode>();
        
        // 루트부터 시작해서 대상 노드까지의 경로 찾기
        if (treeMib.ItemsSource != null)
        {
            foreach (var item in treeMib.ItemsSource)
            {
                if (item is MibTreeNode rootNode)
                {
                    FindPathToNode(rootNode, node, parents);
                }
            }
        }
        
        foreach (var parent in parents)
        {
            parent.IsExpanded = true;
        }
    }
    
    private bool FindPathToNode(MibTreeNode current, MibTreeNode target, List<MibTreeNode> path)
    {
        if (current == target)
        {
            return true;
        }
        
        foreach (var child in current.Children)
        {
            path.Add(current);
            if (FindPathToNode(child, target, path))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        
        return false;
    }
    
    private void SelectNodeInTreeView(MibTreeNode node)
    {
        // TreeViewItem을 찾아서 선택
        var container = FindTreeViewItem(treeMib, node);
        if (container != null)
        {
            container.IsSelected = true;
            container.BringIntoView();
            container.Focus();
        }
    }
    
    private TreeViewItem? FindTreeViewItem(ItemsControl parent, MibTreeNode target)
    {
        if (parent == null) return null;
        
        foreach (var item in parent.Items)
        {
            if (item is MibTreeNode mibNode)
            {
                var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container == null) continue;
                
                if (mibNode == target)
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

