using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnmpNms.Core.Models;

public enum MibNodeType
{
    Folder,      // 폴더/서브트리
    Table,       // MIB 테이블
    Scalar,      // 스칼라 값
    CustomTable  // 사용자 정의 테이블
}

public class MibTreeNode : INotifyPropertyChanged
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; OnPropertyChanged(); }
    }

    private string _oid = "";
    public string Oid
    {
        get => _oid;
        set { if (_oid == value) return; _oid = value; OnPropertyChanged(); }
    }

    private MibNodeType _nodeType = MibNodeType.Folder;
    public MibNodeType NodeType
    {
        get => _nodeType;
        set { if (_nodeType == value) return; _nodeType = value; OnPropertyChanged(); }
    }

    public ObservableCollection<MibTreeNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    public string? Description { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

