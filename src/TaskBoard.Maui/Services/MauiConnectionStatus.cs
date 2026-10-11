using TaskBoard.ClientCore.Interfaces;

namespace TaskBoard.Maui.Services;

public sealed class MauiConnectionStatus : IConnectionStatus
{
    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
}
