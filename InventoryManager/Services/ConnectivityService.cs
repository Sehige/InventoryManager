
// Services/ConnectivityService.cs
// Monitors network connectivity for sync operations

using System;
using System.Threading.Tasks;
using Microsoft.Maui.Networking;

namespace InventoryManager.Services
{
    /// <summary>
    /// Interface for connectivity monitoring
    /// </summary>
    public interface IConnectivityService
    {
        Task<bool> IsConnectedAsync();
        Task<bool> IsWifiConnectedAsync();
        event EventHandler<ConnectivityChangedEventArgs> ConnectivityChanged;
    }

    /// <summary>
    /// Implementation using MAUI's connectivity APIs
    /// </summary>
    public class ConnectivityService : IConnectivityService
    {
        public event EventHandler<ConnectivityChangedEventArgs>? ConnectivityChanged;

        public ConnectivityService()
        {
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        }

        /// <summary>
        /// Check if device has internet connectivity
        /// </summary>
        public Task<bool> IsConnectedAsync()
        {
            return Task.FromResult(Connectivity.Current.NetworkAccess == NetworkAccess.Internet);
        }

        /// <summary>
        /// Check if device is connected via WiFi
        /// </summary>
        public Task<bool> IsWifiConnectedAsync()
        {
            var profiles = Connectivity.Current.ConnectionProfiles;
            return Task.FromResult(profiles.Contains(ConnectionProfile.WiFi));
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            ConnectivityChanged?.Invoke(this, e);
        }
    }
}