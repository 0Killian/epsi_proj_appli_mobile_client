using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Maui.Alerts;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using ScanMode = Plugin.BLE.Abstractions.Contracts.ScanMode;

namespace epsi_projet_mobile_client;

public class MainPageViewModel : ViewModel
{
    static readonly Guid ServiceUuid = new("0000ffe0-0000-1000-8000-00805f9b34fb");
    
    private readonly IBluetoothLE _bluetooth = CrossBluetoothLE.Current;
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;

    private ObservableCollection<IDevice> _devices = [];
    public ObservableCollection<IDevice> Devices
    {
        get => _devices;
        set
        {
            _devices = value;
            OnPropertyChanged();
        }
    }

    private string _bluetoothState;
    public string BluetoothStatus
    {
        get => _bluetoothState;
        set
        {
            _bluetoothState = value;
            OnPropertyChanged();
        }
    }

    public MainPageViewModel()
    {
        _bluetoothState = _bluetooth.State.ToString();
        
        Task.Run(async () =>
        {
            if (!await CheckPermissions())
            {
                await Toast.Make("Not all permissions were accepted. Application will now close.").Show();
                Application.Current!.Quit();
            }

            _bluetooth.StateChanged += async (_, e) =>
            {
                BluetoothStatus = e.NewState.ToString();
                if (e.NewState != BluetoothState.On) return;
                Devices.Clear();
                await _adapter.StartScanningForDevicesAsync([ServiceUuid]);
            };

            _adapter.DeviceDiscovered += OnDeviceDiscovered;

            _adapter.DeviceAdvertised += OnDeviceDiscovered;

            _adapter.ScanMode = ScanMode.LowLatency;
            await _adapter.StartScanningForDevicesAsync([ServiceUuid]);
        }).Wait();
    }

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        if (Devices.All(x => x.Id != e.Device.Id))
        {
            Debug.Print("Found device: " + e.Device.Name + "(" + e.Device.Id + ")\n");
            foreach (var ad in e.Device.AdvertisementRecords)
            {
                Debug.Print(ad + "\n");
            }
        }

        try
        {
            Devices.RemoveAt(Devices.IndexOf(Devices.First(x => x.Id == e.Device.Id)));
        }
        catch (InvalidOperationException ex)
        {
        }

        Devices.Add(e.Device);
    }
    
    private static async Task<bool> CheckPermissions()
    {
        var bluetoothStatus = await CheckBluetoothPermissions();

        return IsGranted(bluetoothStatus);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private static async Task<PermissionStatus> CheckBluetoothPermissions()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var bluetoothStatus = PermissionStatus.Granted;
        
        #if ANDROID
        if (DeviceInfo.Version.Major >= 12)
        {
            bluetoothStatus = await CheckPermissions<Permissions.Bluetooth>();
        }
        else
        {
            bluetoothStatus = await CheckPermissions<Permissions.LocationWhenInUse>();
        }
        #endif

        return bluetoothStatus;
    }

    private static async Task<PermissionStatus> CheckPermissions<T>() where T : Permissions.BasePermission, new()
    {
        var status = await Permissions.CheckStatusAsync<T>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<T>();
        }

        return status;
    }

    private static bool IsGranted(PermissionStatus status)
    {
        return status is PermissionStatus.Granted or PermissionStatus.Limited;
    }
}