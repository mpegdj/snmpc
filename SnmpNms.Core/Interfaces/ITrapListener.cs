using SnmpNms.Core.Models;

namespace SnmpNms.Core.Interfaces;

public interface ITrapListener
{
    bool IsListening { get; }
    
    void Start(int port = 162);
    void Stop();
    
    event EventHandler<TrapEvent> OnTrapReceived;
    
    (string ipAddress, int port) GetListenerInfo();
    
    string GetLocalNetworkIp();
}

