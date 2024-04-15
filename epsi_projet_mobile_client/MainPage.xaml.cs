using System.ComponentModel;
using CommunityToolkit.Maui.Alerts;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

namespace epsi_projet_mobile_client;

public partial class MainPage : ContentPage, INotifyPropertyChanged
{

    public MainPage()
    {
        InitializeComponent();
    }

}