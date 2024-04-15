namespace epsi_projet_mobile_client;

internal class BluetoothPermissions : Permissions.BasePlatformPermission
{
    #if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new List<(string permission, bool isRuntime)>
        {
            ("android.permission.BLUETOOTH", true),
            ("android.permission.BLUETOOTH_ADMIN", true),
            ("android.permission.ACCESS_FINE_LOCATION", true),
            ("android.permission.ACCESS_COARSE_LOCATION", true)
        }.ToArray();
#endif
}