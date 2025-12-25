using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;

namespace SnmpNms.UI.Views.Dialogs;

public partial class PingLogWindow : Window
{
    public string TargetHost { get; }

    public PingLogWindow(string targetHost)
    {
        TargetHost = targetHost;
        DataContext = this;
        InitializeComponent();
    }

    private CancellationTokenSource? _cts;
    private int _seq;
    private bool _stopped;

    public void Append(string line)
    {
        txtLog.AppendText(line + Environment.NewLine);
        txtLog.ScrollToEnd();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Append($"[{DateTime.Now:HH:mm:ss}] Start continuous ping -> {TargetHost}");

        try
        {
            using var ping = new Ping();

            while (!token.IsCancellationRequested)
            {
                _seq++;
                var now = DateTime.Now;
                Append($"[{now:HH:mm:ss}] Ping #{_seq} -> {TargetHost} (timeout=1200ms)");

                try
                {
                    var reply = await ping.SendPingAsync(TargetHost, 1200);
                    if (reply.Status == IPStatus.Success)
                        Append($"[{DateTime.Now:HH:mm:ss}]   Result: OK (Status={reply.Status}, RTT={reply.RoundtripTime}ms)");
                    else
                        Append($"[{DateTime.Now:HH:mm:ss}]   Result: Fail (Status={reply.Status})");
                }
                catch (Exception ex)
                {
                    Append($"[{DateTime.Now:HH:mm:ss}]   Result: Fail (Error={ex.Message})");
                }

                // 너무 빠르면 로그가 폭주하므로 1초 간격 유지
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        finally
        {
            if (!_stopped)
                Append($"[{DateTime.Now:HH:mm:ss}] Stopped (window closing)");
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _stopped = true;
        _cts?.Cancel();
        Append($"[{DateTime.Now:HH:mm:ss}] Stopped");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();
    }
}


