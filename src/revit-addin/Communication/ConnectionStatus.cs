using System;

namespace TycoonRevitAddin.Communication
{
    /// <summary>
    /// Connection status for MCP server with visual indicators
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// 🔴 RED: MCP server not available/not running
        /// </summary>
        Disconnected,

        /// <summary>
        /// 🟡 YELLOW: Server available but not connected
        /// </summary>
        Available,

        /// <summary>
        /// 🔄 BLUE: Attempting to connect
        /// </summary>
        Connecting,

        /// <summary>
        /// 🟢 GREEN: Connected with active handshake
        /// </summary>
        Connected,

        /// <summary>
        /// 🔵💚 FLASHING GREEN/BLUE: Server actively processing requests
        /// </summary>
        Active,

        /// <summary>
        /// ❌ RED: Connection failed or error state
        /// </summary>
        Error
    }

    /// <summary>
    /// Connection progress information for real-time dialog
    /// </summary>
    public class ConnectionProgress
    {
        public string Message { get; set; }
        public int ProgressPercentage { get; set; }
        public ConnectionStatus Status { get; set; }
        public bool IsComplete { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }

        public ConnectionProgress(string message, int progress = 0, ConnectionStatus status = ConnectionStatus.Connecting)
        {
            Message = message;
            ProgressPercentage = progress;
            Status = status;
            IsComplete = false;
            IsSuccess = false;
            Timestamp = DateTime.Now;
        }

        public static ConnectionProgress Success(string message)
        {
            return new ConnectionProgress(message, 100, ConnectionStatus.Connected)
            {
                IsComplete = true,
                IsSuccess = true
            };
        }

        public static ConnectionProgress Error(string message, string errorDetails = null)
        {
            return new ConnectionProgress(message, 0, ConnectionStatus.Error)
            {
                IsComplete = true,
                IsSuccess = false,
                ErrorMessage = errorDetails
            };
        }
    }

    /// <summary>
    /// Event args for connection status changes
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public ConnectionStatus Status { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public ConnectionStatusChangedEventArgs(ConnectionStatus status, string message = null)
        {
            Status = status;
            Message = message ?? status.ToString();
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Event args for connection progress updates
    /// </summary>
    public class ConnectionProgressEventArgs : EventArgs
    {
        public ConnectionProgress Progress { get; }

        public ConnectionProgressEventArgs(ConnectionProgress progress)
        {
            Progress = progress;
        }
    }
}
