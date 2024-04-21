using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Maui.Alerts;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace epsi_projet_mobile_client;

public class MainPageViewModel : ViewModel
{
    private static readonly Guid ServiceUuid = new("0A806176-5C2B-4BDD-B288-F38A94D1F957");
    
    private readonly IBluetoothLE _bluetooth = CrossBluetoothLE.Current;
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;

    public event EventHandler OnScanStarted = (_, _) => { };
    public event EventHandler OnScanStopped = (_, _) => { };

    private CancellationTokenSource? _cancellationTokenSource;

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
                
                Debug.Assert(Application.Current != null, "Application.Current != null");
                Application.Current.Quit();
            }

            _bluetooth.StateChanged += async (_, e) =>
            {
                BluetoothStatus = e.NewState.ToString();
                if (e.NewState != BluetoothState.On) return;
                await ScanDevices();
            };

            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _adapter.DeviceAdvertised += OnDeviceDiscovered;
            
            await ScanDevices();
        }).Wait();
    }
    
    public async void ScanDevicesCommand(object? sender, EventArgs e)
    {
        await ScanDevices();
    }

    private async Task ScanDevices()
    {
        Devices.Clear();
        
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
        }
        
        _cancellationTokenSource = new CancellationTokenSource();
        ScanFilterOptions filter = new()
        {
            ServiceUuids = [ServiceUuid]
        };

        OnScanStarted(this, EventArgs.Empty);
        await _adapter.StartScanningForDevicesAsync(filter, cancellationToken: _cancellationTokenSource.Token);
        OnScanStopped(this, EventArgs.Empty);
    }

    public async void CancelScan()
    {
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
        }
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
        catch (InvalidOperationException)
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