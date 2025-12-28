using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Services;

/// <summary>
/// Event Log를 CSV 파일로 저장하는 서비스
/// </summary>
public class LogSaveService : INotifyPropertyChanged
{
    private string? _currentDate;
    private string? _currentFilePath;
    private StreamWriter? _writer;
    private readonly object _lock = new();

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }
    
    private int _maxLinesInMemory = 1000;
    public int MaxLinesInMemory
    {
        get => _maxLinesInMemory;
        set
        {
            if (_maxLinesInMemory == value) return;
            _maxLinesInMemory = value;
            OnPropertyChanged();
        }
    }
    
    public string LogDirectory { get; set; } = "Logs";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public LogSaveService()
    {
        // Logs 디렉토리 생성
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    /// <summary>
    /// 로그 항목 저장
    /// </summary>
    public void SaveLogEntry(SnmpEventLog entry)
    {
        if (!IsEnabled) return;

        lock (_lock)
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            
            // 날짜가 바뀌면 새 파일 생성
            if (_currentDate != today)
            {
                CloseCurrentFile();
                _currentDate = today;
                _currentFilePath = Path.Combine(LogDirectory, $"log_{today}.csv");
                
                // CSV 헤더 작성 (파일이 없을 때만)
                if (!File.Exists(_currentFilePath))
                {
                    _writer = new StreamWriter(_currentFilePath, true, Encoding.UTF8);
                    _writer.WriteLine("Timestamp,Severity,Device,Message");
                }
                else
                {
                    _writer = new StreamWriter(_currentFilePath, true, Encoding.UTF8);
                }
            }
            else if (_writer == null)
            {
                // 날짜는 같지만 파일이 열려있지 않으면 다시 열기
                _currentDate = today;
                _currentFilePath = Path.Combine(LogDirectory, $"log_{today}.csv");
                
                if (!File.Exists(_currentFilePath))
                {
                    _writer = new StreamWriter(_currentFilePath, true, Encoding.UTF8);
                    _writer.WriteLine("Timestamp,Severity,Device,Message");
                }
                else
                {
                    _writer = new StreamWriter(_currentFilePath, true, Encoding.UTF8);
                }
            }

            // CSV 라인 작성
            if (_writer != null)
            {
                var device = entry.Device ?? "";
                var message = EscapeCsvField(entry.Message ?? "");
                _writer.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{entry.Severity},{device},{message}");
                _writer.Flush();
            }
        }
    }

    /// <summary>
    /// 현재 파일 닫기
    /// </summary>
    public void CloseCurrentFile()
    {
        lock (_lock)
        {
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;
        }
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // CSV 필드에 쉼표나 따옴표가 있으면 따옴표로 감싸고 내부 따옴표는 두 개로
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    public void Dispose()
    {
        CloseCurrentFile();
    }
}

