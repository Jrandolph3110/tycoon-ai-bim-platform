using System;
using System.Collections.Generic;
using System.Linq;
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
    /// - On-demand selection querying (when AI is requested)
    /// - Command execution and response handling
    /// - Element data serialization
    /// </summary>
    public class TycoonBridge
    {
        private WebSocket _webSocket;
        private Timer _heartbeatTimer;
        // Removed _selectionMonitorTimer - selection now queried on-demand only
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
            
            // Selection monitoring removed - now queried on-demand only for stability
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
                // Selection monitoring timer removed
                
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
            
            // Selection monitoring removed - now queried on-demand only
            _logger.Log($"📋 Document opened: {document.Title}");
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

                _currentDocument = null;
                _currentUIDocument = null;
                _lastSelection.Clear();

                _logger.Log($"📋 Document closed: {documentTitle}");
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
            
            // Selection monitoring removed - now queried on-demand only
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
                    case "selection":
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
            // Selection monitoring timer removed
            
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
            try
            {
                string messageType = message.type?.ToString();
                string commandId = message.id?.ToString();

                _logger.Log($"📋 Processing command: {messageType} ({commandId})");

                switch (messageType)
                {
                    case "selection":
                        HandleSelectionCommand(message);
                        break;
                    case "command":
                        // Handle other commands
                        _logger.Log($"📋 Received command: {messageType}");
                        break;
                    default:
                        _logger.Log($"⚠️ Unknown command type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling command", ex);
            }
        }

        /// <summary>
        /// Handle selection command from MCP server
        /// </summary>
        private void HandleSelectionCommand(dynamic message)
        {
            try
            {
                string commandId = message.id?.ToString();
                var payload = message.payload;
                string action = payload?.action?.ToString();

                _logger.Log($"📋 Handling selection command: {action} ({commandId})");

                if (action == "get")
                {
                    // Get current selection and send response
                    var selectionResponse = GetCurrentSelection();
                    selectionResponse.CommandId = commandId;

                    // Send response back to MCP server
                    SendSelectionResponse(selectionResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling selection command", ex);
            }
        }

        /// <summary>
        /// Send selection response back to MCP server
        /// </summary>
        private void SendSelectionResponse(TycoonResponse response)
        {
            try
            {
                string json = JsonConvert.SerializeObject(response);
                _webSocket.Send(json);

                _logger.Log($"📤 Sent selection response: {response.CommandId}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send selection response", ex);
            }
        }

        /// <summary>
        /// Get current selection on-demand (called only when AI is requested)
        /// </summary>
        public TycoonResponse GetCurrentSelection()
        {
            try
            {
                if (_currentUIDocument == null || !_isConnected)
                {
                    return new TycoonResponse
                    {
                        CommandId = Guid.NewGuid().ToString(),
                        Success = false,
                        Error = "No document open or not connected",
                        Timestamp = DateTime.UtcNow.ToString("O")
                    };
                }

                var currentSelection = _currentUIDocument.Selection.GetElementIds();

                // Tiered selection limits based on expert recommendations
                const int GREEN_LIMIT = 1000;    // Immediate execution
                const int YELLOW_LIMIT = 2500;   // Progress bar + cancel
                const int ORANGE_LIMIT = 4000;   // Warning + chunked processing
                const int RED_LIMIT = 4000;      // Hard limit - refuse or auto-chunk

                _logger.Log($"🔍 Selection count: {currentSelection.Count} elements");

                // Determine processing tier
                string tier = currentSelection.Count <= GREEN_LIMIT ? "GREEN" :
                             currentSelection.Count <= YELLOW_LIMIT ? "YELLOW" :
                             currentSelection.Count <= ORANGE_LIMIT ? "ORANGE" : "RED";

                _logger.Log($"📊 Processing tier: {tier} ({currentSelection.Count} elements)");

                // Enable auto-chunking for RED tier (BRUTE FORCE MODE!)
                if (currentSelection.Count > RED_LIMIT)
                {
                    _logger.Log($"🔥 RED TIER DETECTED! {currentSelection.Count} > {RED_LIMIT} - ENABLING BRUTE FORCE AUTO-CHUNKING!");
                    tier = "RED_CHUNKED"; // Special tier for massive selections
                }

                // Process selection based on tier
                var selectionData = ProcessSelectionByTier(currentSelection, tier);

                return new TycoonResponse
                {
                    CommandId = Guid.NewGuid().ToString(),
                    Success = true,
                    Data = selectionData,
                    Timestamp = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting current selection", ex);
                return new TycoonResponse
                {
                    CommandId = Guid.NewGuid().ToString(),
                    Success = false,
                    Error = $"Error getting selection: {ex.Message}",
                    Timestamp = DateTime.UtcNow.ToString("O")
                };
            }
        }

        // SendSelectionUpdate method removed - selection now queried on-demand only

        /// <summary>
        /// Process selection based on tier with appropriate optimizations
        /// </summary>
        private SelectionData ProcessSelectionByTier(ICollection<ElementId> elementIds, string tier)
        {
            _logger.Log($"🔄 Processing {elementIds.Count} elements in {tier} tier");

            switch (tier)
            {
                case "GREEN":
                    // Immediate processing for small selections
                    return _selectionManager.SerializeSelection(_currentDocument, elementIds);

                case "YELLOW":
                    // Process with progress indication (future: add progress callback)
                    _logger.Log($"⚡ YELLOW tier: Processing with progress tracking");
                    return _selectionManager.SerializeSelection(_currentDocument, elementIds);

                case "ORANGE":
                    // Chunked processing for large selections
                    _logger.Log($"🔶 ORANGE tier: Using chunked processing");
                    return ProcessSelectionInChunks(elementIds);

                case "RED_CHUNKED":
                    // BRUTE FORCE MODE: Auto-chunking for massive selections
                    _logger.Log($"🔥 RED_CHUNKED tier: BRUTE FORCE AUTO-CHUNKING ENGAGED!");
                    return ProcessMassiveSelectionInChunks(elementIds);

                default:
                    // Fallback to standard processing
                    return _selectionManager.SerializeSelection(_currentDocument, elementIds);
            }
        }

        /// <summary>
        /// Process large selections in memory-safe chunks
        /// </summary>
        private SelectionData ProcessSelectionInChunks(ICollection<ElementId> elementIds)
        {
            const int CHUNK_SIZE = 250; // Optimized chunk size to prevent memory issues
            var allElements = new List<RevitElementData>();
            var elementList = elementIds.ToList();
            var totalChunks = (elementList.Count + CHUNK_SIZE - 1) / CHUNK_SIZE;

            _logger.Log($"📦 MEMORY-SAFE Chunking {elementIds.Count} elements into {totalChunks} batches of {CHUNK_SIZE}");

            for (int i = 0; i < elementList.Count; i += CHUNK_SIZE)
            {
                var chunk = elementList.Skip(i).Take(CHUNK_SIZE).ToList();
                var chunkNumber = (i / CHUNK_SIZE) + 1;
                var progress = (double)chunkNumber / totalChunks * 100;

                // Memory monitoring
                var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB
                _logger.Log($"🔄 Processing chunk {chunkNumber}/{totalChunks} ({progress:F1}%) - {chunk.Count} elements (Memory: {memoryUsage}MB)");

                // Process chunk WITHOUT transactions (read-only operation)
                try
                {
                    var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);
                    allElements.AddRange(chunkData.Elements);
                }
                catch (Exception ex)
                {
                    _logger.Log($"❌ Chunk {chunkNumber} failed: {ex.Message}");
                }

                // Memory cleanup after each chunk
                chunk.Clear();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                // Yield control to keep Revit responsive
                System.Threading.Thread.Sleep(25); // 25ms yield for cleanup
            }

            var finalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            _logger.Log($"📦 CHUNKING COMPLETE! Final Memory: {finalMemory}MB");

            return new SelectionData
            {
                Count = allElements.Count,
                Elements = allElements,
                ViewName = _currentUIDocument?.ActiveView?.Name,
                DocumentTitle = _currentDocument?.Title,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// CRASH-PROOF MODE: Process massive selections with memory-safe chunking
        /// </summary>
        private SelectionData ProcessMassiveSelectionInChunks(ICollection<ElementId> elementIds)
        {
            const int CHUNK_SIZE = 250; // Optimized chunk size to prevent LOH fragmentation
            var allElements = new List<RevitElementData>();
            var elementList = elementIds.ToList();
            var totalChunks = (elementList.Count + CHUNK_SIZE - 1) / CHUNK_SIZE;

            _logger.Log($"🛡️ CRASH-PROOF CHUNKING: {elementIds.Count} elements into {totalChunks} batches of {CHUNK_SIZE}");
            _logger.Log($"⏱️ Estimated time: {totalChunks * 2}-{totalChunks * 4} seconds");

            var startTime = DateTime.UtcNow;

            for (int i = 0; i < elementList.Count; i += CHUNK_SIZE)
            {
                var chunk = elementList.Skip(i).Take(CHUNK_SIZE).ToList();
                var chunkNumber = (i / CHUNK_SIZE) + 1;
                var progress = (double)chunkNumber / totalChunks * 100;

                // Memory monitoring - abort if approaching dangerous levels
                var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB
                if (memoryUsage > 6000) // 6GB threshold
                {
                    _logger.Log($"🚨 MEMORY THRESHOLD EXCEEDED: {memoryUsage}MB > 6000MB - ABORTING SAFELY");
                    break;
                }

                _logger.Log($"🛡️ CRASH-PROOF chunk {chunkNumber}/{totalChunks} ({progress:F1}%) - {chunk.Count} elements (Memory: {memoryUsage}MB)");

                // Process chunk WITHOUT transactions (read-only operation)
                try
                {
                    var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);
                    allElements.AddRange(chunkData.Elements);

                    _logger.Log($"✅ Chunk {chunkNumber} completed - {chunkData.Elements.Count} elements processed");
                }
                catch (Exception ex)
                {
                    _logger.Log($"❌ Chunk {chunkNumber} failed: {ex.Message}");
                    // Continue with next chunk instead of failing completely
                }

                // Aggressive memory cleanup after each chunk
                chunk.Clear();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                // Yield to keep Revit responsive
                System.Threading.Thread.Sleep(50); // 50ms yield for memory cleanup
            }

            var endTime = DateTime.UtcNow;
            var totalTime = (endTime - startTime).TotalSeconds;
            var finalMemory = GC.GetTotalMemory(false) / (1024 * 1024);

            _logger.Log($"🎯 CRASH-PROOF COMPLETE! {allElements.Count} elements processed in {totalTime:F1} seconds (Final Memory: {finalMemory}MB)");

            return new SelectionData
            {
                Count = allElements.Count,
                Elements = allElements,
                ViewName = _currentUIDocument?.ActiveView?.Name,
                DocumentTitle = _currentDocument?.Title,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
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
