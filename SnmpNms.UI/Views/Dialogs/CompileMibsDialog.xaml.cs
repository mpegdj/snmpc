using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnmpNms.Core.Interfaces;

namespace SnmpNms.UI.Views.Dialogs;

public partial class CompileMibsDialog : Window
{
    private readonly IMibService _mibService;
    private readonly ObservableCollection<string> _mibFiles = new();

    public CompileMibsDialog(IMibService mibService)
    {
        InitializeComponent();
        _mibService = mibService;
        listMibFiles.ItemsSource = _mibFiles;
        
        // MIB 디렉터리에서 기존 파일 목록 로드
        LoadExistingMibFiles();
    }

    private void LoadExistingMibFiles()
    {
        var projectRoot = @"D:\git\snmpc\Mib";
        if (!Directory.Exists(projectRoot))
        {
            projectRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mib");
        }

        if (Directory.Exists(projectRoot))
        {
            var files = Directory.GetFiles(projectRoot, "*.mib", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(projectRoot, "*.txt", SearchOption.AllDirectories))
                .ToList();

            foreach (var file in files)
            {
                _mibFiles.Add(file);
            }
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MIB Files (*.mib;*.txt)|*.mib;*.txt|All Files (*.*)|*.*",
            Multiselect = true,
            Title = "Select MIB Files"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var fileName in dialog.FileNames)
            {
                if (!_mibFiles.Contains(fileName))
                {
                    _mibFiles.Add(fileName);
                    AddHistory($"Added: {Path.GetFileName(fileName)}");
                }
            }
        }
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = listMibFiles.SelectedItems.Cast<string>().ToList();
        foreach (var item in selectedItems)
        {
            _mibFiles.Remove(item);
            AddHistory($"Removed: {Path.GetFileName(item)}");
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
            "Clear all MIB files from the list?",
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _mibFiles.Clear();
            AddHistory("Cleared all MIB files");
        }
    }

    private void BtnCompile_Click(object sender, RoutedEventArgs e)
    {
        if (_mibFiles.Count == 0)
        {
            MessageBox.Show(
                "No MIB files selected. Please add MIB files first.",
                "No Files",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        AddHistory("=== Starting MIB Compilation ===");
        
        var successCount = 0;
        var errorCount = 0;
        var allDirectories = new HashSet<string>();

        // 모든 파일의 디렉터리 수집
        foreach (var filePath in _mibFiles)
        {
            if (!File.Exists(filePath))
            {
                AddHistory($"[ERROR] File not found: {Path.GetFileName(filePath)}");
                errorCount++;
                continue;
            }

            var fileDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDir))
            {
                allDirectories.Add(fileDir);
            }
        }

        // 모든 디렉터리를 한 번에 로드 (MIB 파일 의존성 해결을 위해)
        foreach (var dir in allDirectories)
        {
            try
            {
                AddHistory($"[INFO] Loading MIB files from: {dir}");
                _mibService.LoadMibModules(dir);
                successCount++;
            }
            catch (Exception ex)
            {
                AddHistory($"[ERROR] Failed to load directory {dir}: {ex.Message}");
                errorCount++;
            }
        }

        AddHistory("=== Compilation Complete ===");
        AddHistory($"Success: {successCount}, Errors: {errorCount}");
        
        // 디버깅: 로드된 OID 개수 확인 (리플렉션 사용)
        try
        {
            var mibServiceType = _mibService.GetType();
            var oidToNameField = mibServiceType.GetField("_oidToName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (oidToNameField?.GetValue(_mibService) is System.Collections.IDictionary oidDict)
            {
                AddHistory($"Total OIDs in database: {oidDict.Count}");
            }
        }
        catch
        {
            // 리플렉션 실패 시 무시
        }

        if (errorCount == 0)
        {
            MessageBox.Show(
                $"Successfully compiled {successCount} MIB directory(ies).\nPlease check the MIB tab to see the loaded MIBs.",
                "Compilation Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                $"Compilation completed with errors.\nSuccess: {successCount}, Errors: {errorCount}\nCheck History tab for details.",
                "Compilation Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AddHistory(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        txtHistory.Text += $"[{timestamp}] {message}\n";
        
        // 스크롤을 맨 아래로 이동
        var scrollViewer = FindVisualChild<ScrollViewer>(txtHistory.Parent as DependencyObject);
        scrollViewer?.ScrollToEnd();
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }
}

