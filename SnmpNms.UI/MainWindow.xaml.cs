using System.Net;
using System.Windows;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace SnmpManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnGet_Click(object sender, RoutedEventArgs e)
    {
        txtResult.Text = "Sending SNMP GET request...";
        
        try
        {
            if (!IPAddress.TryParse(txtIp.Text, out var ip))
            {
                txtResult.Text = "Invalid IP Address format.";
                return;
            }
            
            var community = new OctetString(txtCommunity.Text);
            var oid = new ObjectIdentifier(txtOid.Text);
            var version = VersionCode.V2; // V2c

            // SharpSnmpLib Messenger.Get
            var result = Messenger.Get(version, 
                                       new IPEndPoint(ip, 161), 
                                       community, 
                                       new List<Variable> { new Variable(oid) }, 
                                       3000); // 3000ms timeout

            if (result != null && result.Count > 0)
            {
                var variable = result[0];
                txtResult.Text = $"Success!\nOID: {variable.Id}\nType: {variable.Data.TypeCode}\nValue: {variable.Data}";
            }
            else
            {
                txtResult.Text = "No response received.";
            }
        }
        catch (Exception ex)
        {
            txtResult.Text = $"Error: {ex.Message}";
        }
    }
}