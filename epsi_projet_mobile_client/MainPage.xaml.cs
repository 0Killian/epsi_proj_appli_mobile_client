using System.Diagnostics;
using CommunityToolkit.Maui.Alerts;
using epsi_projet_mobile_client.Services;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

namespace epsi_projet_mobile_client;

public partial class MainPage
{
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;

    private Protocol? _protocol;
    
    public MainPage()
    {
        InitializeComponent();
        ((MainPageViewModel)BindingContext).OnScanStarted += (_, _) => ScanButton.IsEnabled = false;
        ((MainPageViewModel)BindingContext).OnScanStopped += (_, _) => ScanButton.IsEnabled = true;
    }
    
    private void ScanDevicesCommand(object? sender, EventArgs e)
    {
        ((MainPageViewModel)BindingContext).ScanDevicesCommand(sender, e);
    }

    private async void DevicesList_OnItemSelected(object? sender, SelectedItemChangedEventArgs e)
    {
        try
        {
            ((MainPageViewModel)BindingContext).CancelScan();

            if (e.SelectedItem == null) return;
            var dev = (IDevice)e.SelectedItem;
            await _adapter.ConnectToDeviceAsync(dev);

            var service = await dev.GetServiceAsync(Guid.Parse("0A806176-5C2B-4BDD-B288-F38A94D1F957"));
            var characteristic =
                await service.GetCharacteristicAsync(Guid.Parse("37450687-DD65-4C73-9793-950AC81AC824"));

            _protocol = new Protocol(new BluetoothCharacteristic(characteristic),
                new BluetoothCharacteristic(characteristic));
            await _protocol.PerformHandshake();
            Debug.Print("SessionId: " + _protocol.SessionId);
            await Toast.Make("SessionId: " + _protocol.SessionId).Show();
        }
        catch (Exception exc)
        {
            Debug.Print(exc.ToString());
            await Toast.Make(exc.Message).Show();
        }
    }
}