using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using System.Net.NetworkInformation;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using WebSocketSharp;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Communication
{
    /// <summary>
    /// TycoonBridge - Communication bridge between Revit and Tycoon MCP Server
    /// 
    /// Handles:
    /// - WebSocket connection to MCP server
    /// - Real-time selection monitoring
    /// - Command execution and response handling
    /// - Element data serialization
    /// </summary>
    public class TycoonBridge
    {
        private WebSocket _webSocket;
        private Timer _heartbeatTimer;
        private Timer _selectionMonitorTimer;
        private bool _isConnected = false;
        private string _serverUrl = "";
        private int _serverPort = 0;
        private TycoonRevitAddin.Utils.Logger _logger;
        private SelectionManager _selectionManager;
        
        // Current document and UI context
        private Document _currentDocument;
        private UIDocument _currentUIDocument;
        private ICollection<ElementId> _lastSelection;

        public bool IsConnected => _isConnected;
        public event EventHandler<bool> ConnectionStatusChanged;

        public TycoonBridge()
        {
            _logger = Application.Logger;
            _selectionManager = new SelectionManager();
            _lastSelection = new List<ElementId>();
            
            // Setup selection monitoring timer (check every 500ms)
            _selectionMonitorTimer = new Timer(500);
            _selectionMonitorTimer.Elapsed += OnSelectionMonitorTick;
            _selectionMonitorTimer.AutoReset = true;
        }

        /// <summary>
        /// Discover Tycoon MCP Server port
        /// </summary>
        private async Task<int> DiscoverServerPortAsync()
        {
            const int startPort = 8765;
            const int maxAttempts = 100;

            _logger.Log($"🔍 Discovering Tycoon server port (scanning {startPort}-{startPort + maxAttempts - 1})...");

            for (int port = startPort; port < startPort + maxAttempts; port++)
            {
                try
                {
                    // Quick test connection to see if server is listening
                    using (var testSocket = new WebSocket($"ws://localhost:{port}"))
                    {
                        var connected = false;
                        var timeout = false;

                        testSocket.OnOpen += (sender, e) => { connected = true; };
                        testSocket.OnError += (sender, e) => { /* Ignore errors during discovery */ };

                        testSocket.Connect();

                        // Wait up to 100ms for connection
                        var startTime = DateTime.Now;
                        while (!connected && !timeout && (DateTime.Now - startTime).TotalMilliseconds < 100)
                        {
                            await Task.Delay(10);
                            timeout = (DateTime.Now - startTime).TotalMilliseconds >= 100;
                        }

                        if (connected)
                        {
                            testSocket.Close();
                            _logger.Log($"✅ Found Tycoon server on port {port}");
                            return port;
                        }

                        testSocket.Close();
                    }
                }
                catch
                {
                    // Port not available or connection failed, try next port
                    continue;
                }
            }

            throw new Exception($"Tycoon server not found on ports {startPort}-{startPort + maxAttempts - 1}");
        }

        /// <summary>
        /// Connect to Tycoon MCP Server
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.Log("🔍 Starting connection process...");

                // Discover server port dynamically
                _serverPort = await DiscoverServerPortAsync();
                _serverUrl = $"ws://localhost:{_serverPort}";
                _logger.Log($"🎯 Connecting to discovered server on port {_serverPort}");

                _logger.Log($"🔗 Creating WebSocket for: {_serverUrl}");

                _webSocket = new WebSocket(_serverUrl);
                _logger.Log("✅ WebSocket created successfully");
                
                _logger.Log("🔧 Setting up WebSocket event handlers...");
                _webSocket.OnOpen += OnWebSocketOpen;
                _webSocket.OnMessage += OnWebSocketMessage;
                _webSocket.OnError += OnWebSocketError;
                _webSocket.OnClose += OnWebSocketClose;
                _logger.Log("✅ Event handlers set up");

                _logger.Log("🚀 Starting connection attempt...");
                // Connect with timeout
                var connectTask = Task.Run(() => {
                    _logger.Log("📡 Calling WebSocket.Connect()...");
                    _webSocket.Connect();
                    _logger.Log("📡 WebSocket.Connect() returned");
                });
                var timeoutTask = Task.Delay(5000); // 5 second timeout

                _logger.Log("⏱️ Waiting for connection or timeout...");
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogError("❌ Connection timeout after 5 seconds");
                    return false;
                }

                _logger.Log("⏳ Connection attempt completed, checking status...");
                // Wait a moment for the connection to establish
                await Task.Delay(100);

                _logger.Log($"🔍 Connection status: {_isConnected}");
                return _isConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to connect to Tycoon server", ex);
                return false;
            }
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _heartbeatTimer?.Stop();
                _selectionMonitorTimer?.Stop();
                
                if (_webSocket != null && _webSocket.ReadyState == WebSocketState.Open)
                {
                    _webSocket.Close();
                }
                
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                
                _logger.Log("🔌 Disconnected from Tycoon server");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during disconnect", ex);
            }
        }

        /// <summary>
        /// Send command to MCP server
        /// </summary>
        public async Task<TycoonResponse> SendCommandAsync(TycoonCommand command)
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to Tycoon server");
            }

            try
            {
                string json = JsonConvert.SerializeObject(command);
                _webSocket.Send(json);
                
                _logger.Log($"📤 Sent command: {command.Type} ({command.Id})");
                
                // For now, return a success response
                // In a full implementation, we'd wait for the actual response
                return new TycoonResponse
                {
                    CommandId = command.Id,
                    Success = true,
                    Timestamp = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send command: {command.Type}", ex);
                throw;
            }
        }

        /// <summary>
        /// Handle document opened
        /// </summary>
        public void OnDocumentOpened(Document document)
        {
            _currentDocument = document;
            
            // Get UI document
            var uiApp = new UIApplication(document.Application);
            _currentUIDocument = uiApp.ActiveUIDocument;
            
            // Start selection monitoring if connected
            if (_isConnected)
            {
                _selectionMonitorTimer.Start();
                _logger.Log($"📋 Started selection monitoring for: {document.Title}");
            }
        }

        /// <summary>
        /// Handle document closed
        /// </summary>
        public void OnDocumentClosed(Document document)
        {
            // In Revit 2024+, document parameter may be null due to API changes
            if (_currentDocument != null)
            {
                string documentTitle = document?.Title ?? _currentDocument.Title;

                _selectionMonitorTimer.Stop();
                _currentDocument = null;
                _currentUIDocument = null;
                _lastSelection.Clear();

                _logger.Log($"📋 Stopped selection monitoring for: {documentTitle}");
            }
        }

        /// <summary>
        /// WebSocket connection opened
        /// </summary>
        private void OnWebSocketOpen(object sender, EventArgs e)
        {
            _isConnected = true;
            ConnectionStatusChanged?.Invoke(this, true);
            
            _logger.Log("✅ Connected to Tycoon MCP Server");
            
            // Start heartbeat
            _heartbeatTimer = new Timer(30000); // 30 seconds
            _heartbeatTimer.Elapsed += SendHeartbeat;
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
            
            // Start selection monitoring if document is open
            if (_currentDocument != null)
            {
                _selectionMonitorTimer.Start();
            }
        }

        /// <summary>
        /// WebSocket message received
        /// </summary>
        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            try
            {
                _logger.Log($"📥 Received message: {e.Data.Substring(0, Math.Min(100, e.Data.Length))}...");
                
                var message = JsonConvert.DeserializeObject<dynamic>(e.Data);
                
                // Handle different message types
                string messageType = message.type?.ToString();
                
                switch (messageType)
                {
                    case "heartbeat":
                        SendHeartbeatResponse();
                        break;
                    case "command":
                        HandleCommand(message);
                        break;
                    default:
                        _logger.Log($"⚠️ Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing message", ex);
            }
        }

        /// <summary>
        /// WebSocket error occurred
        /// </summary>
        private void OnWebSocketError(object sender, ErrorEventArgs e)
        {
            _logger.LogError("WebSocket error", new Exception(e.Message));
        }

        /// <summary>
        /// WebSocket connection closed
        /// </summary>
        private void OnWebSocketClose(object sender, CloseEventArgs e)
        {
            _isConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            
            _heartbeatTimer?.Stop();
            _selectionMonitorTimer?.Stop();
            
            _logger.Log($"❌ Connection closed: {e.Reason}");
        }

        /// <summary>
        /// Send heartbeat to server
        /// </summary>
        private void SendHeartbeat(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_isConnected)
                {
                    var heartbeat = new
                    {
                        type = "heartbeat",
                        timestamp = DateTime.UtcNow.ToString("O")
                    };
                    
                    string json = JsonConvert.SerializeObject(heartbeat);
                    _webSocket.Send(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send heartbeat", ex);
            }
        }

        /// <summary>
        /// Send heartbeat response
        /// </summary>
        private void SendHeartbeatResponse()
        {
            try
            {
                var response = new
                {
                    type = "heartbeat_response",
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                string json = JsonConvert.SerializeObject(response);
                _webSocket.Send(json);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send heartbeat response", ex);
            }
        }

        /// <summary>
        /// Handle incoming command
        /// </summary>
        private void HandleCommand(dynamic message)
        {
            // This would handle commands from the MCP server
            // For now, just log the command
            _logger.Log($"📋 Received command: {message.type}");
        }

        /// <summary>
        /// Monitor selection changes
        /// </summary>
        private void OnSelectionMonitorTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_currentUIDocument == null || !_isConnected)
                    return;

                var currentSelection = _currentUIDocument.Selection.GetElementIds();
                
                // Check if selection has changed
                if (!SelectionsEqual(currentSelection, _lastSelection))
                {
                    _lastSelection = new List<ElementId>(currentSelection);
                    SendSelectionUpdate(currentSelection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error monitoring selection", ex);
            }
        }

        /// <summary>
        /// Send selection update to server
        /// </summary>
        private void SendSelectionUpdate(ICollection<ElementId> selection)
        {
            try
            {
                var selectionData = _selectionManager.SerializeSelection(_currentDocument, selection);
                
                var message = new
                {
                    type = "selection_changed",
                    data = selectionData,
                    timestamp = DateTime.UtcNow.ToString("O")
                };
                
                string json = JsonConvert.SerializeObject(message);
                _webSocket.Send(json);
                
                _logger.Log($"📋 Selection update sent: {selection.Count} elements");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send selection update", ex);
            }
        }

        /// <summary>
        /// Compare two selections for equality
        /// </summary>
        private bool SelectionsEqual(ICollection<ElementId> selection1, ICollection<ElementId> selection2)
        {
            if (selection1.Count != selection2.Count)
                return false;
                
            foreach (var id in selection1)
            {
                if (!selection2.Contains(id))
                    return false;
            }
            
            return true;
        }
    }
}
