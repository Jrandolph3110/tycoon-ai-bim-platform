using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using TycoonRevitAddin.Communication;
using TycoonRevitAddin.Resources;
using TycoonRevitAddin.Services;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// Manages dynamic ribbon button updates with status indicators
    /// </summary>
    public class DynamicRibbonManager : IDisposable
    {
        private readonly Logger _logger;
        private readonly StatusPollingService _statusService;
        private readonly Timer _flashTimer;
        private readonly Timer _updateTimer;
        
        private PushButton _connectButton;
        private ConnectionStatus _currentStatus = ConnectionStatus.Disconnected;
        private bool _isFlashing = false;
        private bool _flashState = false;
        private bool _disposed = false;

        public DynamicRibbonManager(StatusPollingService statusService, Logger logger)
        {
            _statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to status changes
            _statusService.StatusChanged += OnStatusChanged;

            // Create timers for flashing and updates
            _flashTimer = new Timer(OnFlashTimer, null, Timeout.Infinite, Timeout.Infinite);
            _updateTimer = new Timer(OnUpdateTimer, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

            _logger.Log("🎨 Dynamic Ribbon Manager initialized");
        }

        /// <summary>
        /// Register the connect button for dynamic updates
        /// </summary>
        public void RegisterConnectButton(PushButton button)
        {
            _connectButton = button;
            UpdateButtonStatus(_statusService.CurrentStatus);
            _logger.Log("🔗 Connect button registered for dynamic updates");
        }

        /// <summary>
        /// Handle status changes from the polling service
        /// </summary>
        private void OnStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            _currentStatus = e.Status;
            UpdateButtonStatus(e.Status);
            _logger.Log($"🎯 Status changed: {e.Status} - {e.Message}");
        }

        /// <summary>
        /// Update the button appearance based on status
        /// </summary>
        private void UpdateButtonStatus(ConnectionStatus status)
        {
            if (_connectButton == null) return;

            try
            {
                // Stop flashing if not active
                if (status != ConnectionStatus.Active)
                {
                    StopFlashing();
                }
                else
                {
                    StartFlashing();
                }

                // Update button icon and tooltip
                var iconPath = StatusIconManager.GetStatusIconPath(status);
                var tooltip = GetStatusTooltip(status);

                // Update button properties (Revit UI thread safe)
                try
                {
                    _connectButton.LargeImage = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                    _connectButton.ToolTip = tooltip;
                    // Note: PushButton doesn't have Text property, only tooltip
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to update button appearance", ex);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating button status", ex);
            }
        }

        /// <summary>
        /// Start flashing animation for active status
        /// </summary>
        private void StartFlashing()
        {
            if (_isFlashing) return;

            _isFlashing = true;
            _flashTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
            _logger.Log("💫 Started flashing animation");
        }

        /// <summary>
        /// Stop flashing animation
        /// </summary>
        private void StopFlashing()
        {
            if (!_isFlashing) return;

            _isFlashing = false;
            _flashTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _flashState = false;
            _logger.Log("🛑 Stopped flashing animation");
        }

        /// <summary>
        /// Handle flash timer tick
        /// </summary>
        private void OnFlashTimer(object state)
        {
            if (!_isFlashing || _connectButton == null) return;

            try
            {
                _flashState = !_flashState;
                var iconPath = StatusIconManager.GetFlashingIconPath(_flashState);

                try
                {
                    _connectButton.LargeImage = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to update flashing icon", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in flash timer", ex);
            }
        }

        /// <summary>
        /// Handle update timer tick
        /// </summary>
        private void OnUpdateTimer(object state)
        {
            // Trigger status refresh
            Task.Run(async () =>
            {
                try
                {
                    await _statusService.RefreshStatusAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error refreshing status", ex);
                }
            });
        }

        /// <summary>
        /// Get tooltip text for status
        /// </summary>
        private string GetStatusTooltip(ConnectionStatus status)
        {
            return status switch
            {
                ConnectionStatus.Disconnected => "🔴 Tycoon AI-BIM: Server not available\nClick to attempt connection",
                ConnectionStatus.Available => "🟡 Tycoon AI-BIM: Server available\nClick to connect",
                ConnectionStatus.Connecting => "🔄 Tycoon AI-BIM: Connecting...\nPlease wait",
                ConnectionStatus.Connected => "🟢 Tycoon AI-BIM: Connected\nClick to disconnect or view status",
                ConnectionStatus.Active => "🔵💚 Tycoon AI-BIM: Processing request\nServer is actively working",
                ConnectionStatus.Error => "❌ Tycoon AI-BIM: Connection error\nClick to retry connection",
                _ => "⚫ Tycoon AI-BIM: Unknown status\nClick to check connection"
            };
        }



        /// <summary>
        /// Set status to active (called during request processing)
        /// </summary>
        public void SetActiveStatus()
        {
            _statusService.SetActive();
        }

        /// <summary>
        /// Set status to connecting (called during connection attempts)
        /// </summary>
        public void SetConnectingStatus()
        {
            _statusService.SetConnecting();
        }

        /// <summary>
        /// Set status to error (called on connection failures)
        /// </summary>
        public void SetErrorStatus()
        {
            _statusService.SetError();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _statusService.StatusChanged -= OnStatusChanged;
                _flashTimer?.Dispose();
                _updateTimer?.Dispose();
                StatusIconManager.CleanupTempIcons();
                _disposed = true;
                _logger.Log("🧹 Dynamic Ribbon Manager disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error disposing Dynamic Ribbon Manager", ex);
            }
        }
    }
}
