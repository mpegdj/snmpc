using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SnmpNms.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // WPF에서 창이 안 뜨고 바로 종료되는 경우를 잡기 위한 안전장치:
        // 예외를 파일로 남기고 메시지 박스로도 보여준다.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var msg = $"[DispatcherUnhandledException]\n{e.Exception}\n";
            File.AppendAllText(GetCrashLogPath(), msg);
            MessageBox.Show(msg, "SnmpNms.UI Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // ignore
        }

        // 기본 크래시 종료 대신, 여기서는 종료를 유도(원인 파악 우선)
        e.Handled = true;
        Current?.Shutdown(-1);
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var msg = $"[DomainUnhandledException]\n{e.ExceptionObject}\n";
            File.AppendAllText(GetCrashLogPath(), msg);
        }
        catch
        {
            // ignore
        }
    }

    private static string GetCrashLogPath()
    {
        // 실행 폴더에 crash.log 남김 (배포/테스트 모두 쉽게 확인 가능)
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
    }
}

