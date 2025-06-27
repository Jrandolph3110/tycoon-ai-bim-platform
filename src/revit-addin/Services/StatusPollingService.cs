using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TycoonRevitAddin.Communication;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Services
{
    /// <summary>
    /// Background service for polling MCP server status
    /// </summary>
    public class StatusPollingService
    {
        private readonly Logger _logger;
        private readonly Timer _pollingTimer;
        private readonly TycoonBridge _bridge;
        
        private ConnectionStatus _currentStatus = ConnectionStatus.Disconnected;
        private bool _isPolling = false;
        private readonly object _lockObject = new object();

        public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;

        public ConnectionStatus CurrentStatus 
        { 
            get { lock (_lockObject) { return _currentStatus; } }
            private set 
            { 
                lock (_lockObject) 
                { 
                    if (_currentStatus != value)
                    {
                        _currentStatus = value;
                        StatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(value));
                    }
                } 
            }
        }

        public StatusPollingService(TycoonBridge bridge, Logger logger)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Poll every 10 seconds
            _pollingTimer = new Timer(PollServerStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            // Subscribe to bridge events
            _bridge.ConnectionStatusChanged += OnBridgeStatusChanged;
        }

        private void OnBridgeStatusChanged(object sender, bool isConnected)
        {
            // Update status based on bridge connection state
            if (isConnected)
            {
                CurrentStatus = ConnectionStatus.Connected;
            }
            else
            {
                // Will be updated by next poll
                CurrentStatus = ConnectionStatus.Disconnected;
            }
        }

        private async void PollServerStatus(object state)
        {
            if (_isPolling) return;

            try
            {
                _isPolling = true;
                await CheckServerStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error polling server status", ex);
            }
            finally
            {
                _isPolling = false;
            }
        }

        private async Task CheckServerStatusAsync()
        {
            try
            {
                // Check if bridge is connected
                if (_bridge.IsConnected)
                {
                    CurrentStatus = ConnectionStatus.Connected;
                    return;
                }

                // Try to discover server
                var serverPort = await DiscoverServerAsync();
                if (serverPort > 0)
                {
                    CurrentStatus = ConnectionStatus.Available;
                }
                else
                {
                    CurrentStatus = ConnectionStatus.Disconnected;
                }
            }
            catch
            {
                CurrentStatus = ConnectionStatus.Disconnected;
            }
        }

        private async Task<int> DiscoverServerAsync()
        {
            try
            {
                // Quick port scan for MCP server (ports 8765-8774)
                for (int port = 8765; port <= 8774; port++)
                {
                    if (await IsPortOpenAsync("localhost", port, 1000))
                    {
                        return port;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs)
        {
            try
            {
                using (var ping = new Ping())
                {
                    // Quick ping test first
                    var reply = await ping.SendPingAsync(host, timeoutMs / 2);
                    if (reply.Status != IPStatus.Success)
                        return false;
                }

                // Try TCP connection
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(timeoutMs);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == connectTask && client.Connected)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Manually trigger a status check
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            await CheckServerStatusAsync();
        }

        /// <summary>
        /// Set status to active (when processing requests)
        /// </summary>
        public void SetActive()
        {
            if (CurrentStatus == ConnectionStatus.Connected)
            {
                CurrentStatus = ConnectionStatus.Active;
                
                // Reset to connected after 5 seconds
                Task.Delay(5000).ContinueWith(_ =>
                {
                    if (CurrentStatus == ConnectionStatus.Active)
                    {
                        CurrentStatus = ConnectionStatus.Connected;
                    }
                });
            }
        }

        /// <summary>
        /// Set status to connecting
        /// </summary>
        public void SetConnecting()
        {
            CurrentStatus = ConnectionStatus.Connecting;
        }

        /// <summary>
        /// Set status to error
        /// </summary>
        public void SetError()
        {
            CurrentStatus = ConnectionStatus.Error;
        }

        public void Dispose()
        {
            _pollingTimer?.Dispose();
            if (_bridge != null)
            {
                _bridge.ConnectionStatusChanged -= OnBridgeStatusChanged;
            }
        }
    }
}
