using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Net.NetworkInformation;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using WebSocketSharp;
using TycoonRevitAddin.Utils;
using TycoonRevitAddin.Storage;
using TycoonRevitAddin.AIActions.Commands;
using TycoonRevitAddin.AIActions.Events;

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
        private System.Timers.Timer _heartbeatTimer;
        // Removed _selectionMonitorTimer - selection now queried on-demand only
        private bool _isConnected = false;
        private string _serverUrl = "";
        private int _serverPort = 0;
        private TycoonRevitAddin.Utils.Logger _logger;
        private SelectionManager _selectionManager;
        private BinaryStreamingManager _streamingManager;
        private ParameterManagementCommands _parameterCommands;
        private EventStore _eventStore;

        // 🚀 ADVANCED PERFORMANCE MANAGERS
        private AdvancedSerializationManager _serializationManager;
        private AdaptiveChunkManager _chunkManager;
        private PipelineParallelismManager _pipelineManager;
        private CircuitBreakerManager _circuitBreaker;

        // Current document and UI context
        private Document _currentDocument;
        private UIDocument _currentUIDocument;
        private ICollection<ElementId> _lastSelection;

        // 🚀 STREAMING DATA VAULT - File-based streaming system
        private StreamingDataVault _dataVault;

        // 🤖 AI PARAMETER MANAGEMENT - External Event Handler
        private ExternalEvent _aiParameterEvent;
        private AIParameterEventHandler _aiParameterHandler;

        public bool IsConnected => _isConnected;
        public event EventHandler<bool> ConnectionStatusChanged;

        public TycoonBridge()
        {
            _logger = Application.Logger;
            _selectionManager = new SelectionManager();
            _lastSelection = new List<ElementId>();

            // Initialize streaming data vault
            _dataVault = new StreamingDataVault(_logger);

            // 🤖 Initialize AI Actions components
            _eventStore = new EventStore();
            _parameterCommands = new ParameterManagementCommands(_logger, _eventStore);

            // 🤖 Initialize AI Parameter External Event Handler with high priority
            _aiParameterHandler = new AIParameterEventHandler(_logger, _parameterCommands);
            _aiParameterEvent = ExternalEvent.Create(_aiParameterHandler);

            // 🚀 Pre-warm External Event to reduce queue time
            _logger.Log("🔥 Pre-warming AI Parameter External Event for optimal performance...");

            // 🚀 Initialize advanced performance managers
            _serializationManager = new AdvancedSerializationManager(_logger);
            _chunkManager = new AdaptiveChunkManager(_logger);
            _circuitBreaker = new CircuitBreakerManager(_logger);
            _pipelineManager = new PipelineParallelismManager(_logger, _serializationManager, _chunkManager);

            _logger.Log("🚀 TycoonBridge initialized with advanced performance managers");

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

                // Initialize binary streaming manager
                _streamingManager = new BinaryStreamingManager(_webSocket, _logger);
                _streamingManager.ConfigureStreaming(enableCompression: true, enableBinaryMode: true);
                _logger.Log("🚀 Binary streaming manager initialized");

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
            _heartbeatTimer = new System.Timers.Timer(30000); // 30 seconds
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
                    case "heartbeat_response":
                        // Heartbeat response received - connection is healthy
                        _logger.Log($"💓 Heartbeat response received");
                        break;
                    case "command":
                    case "selection":
                    case "ai_rename_panel_elements":
                    case "ai_modify_parameters":
                    case "ai_analyze_panel_structure":
                    case "flc_hybrid_operation":
                    case "flc_script_graduation_analytics":
                    case "ai_create_elements":
                    case "ai_modify_geometry":
                    case "generate_hot_script":
                    case "execute_custom_operation":
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
                    case "query":
                        HandleQueryCommand(message);
                        break;
                    case "modify":
                        HandleModifyCommand(message);
                        break;
                    case "ai_rename_panel_elements":
                        HandleAIRenamePanelElements(message);
                        break;
                    case "ai_modify_parameters":
                        HandleAIModifyParameters(message);
                        break;
                    case "ai_analyze_panel_structure":
                        HandleAIAnalyzePanelStructure(message);
                        break;
                    case "flc_hybrid_operation":
                        HandleFLCHybridOperation(message);
                        break;
                    case "flc_script_graduation_analytics":
                        HandleFLCScriptGraduationAnalytics(message);
                        break;
                    case "ai_create_elements":
                        HandleAICreateElements(message);
                        break;
                    case "ai_modify_geometry":
                        HandleAIModifyGeometry(message);
                        break;
                    case "generate_hot_script":
                        HandleGenerateHotScript(message);
                        break;
                    case "execute_custom_operation":
                        HandleExecuteCustomOperation(message);
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
                    // 🚀 FAFB IMMEDIATE ACKNOWLEDGMENT - Send progress start message
                    SendProgressUpdate(commandId, "selection_started", 0, 0, "Analyzing selection...");

                    // Get current selection and send response
                    var selectionResponse = GetCurrentSelection(commandId);
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
        /// Handle query command (like getting element parameters)
        /// </summary>
        private void HandleQueryCommand(dynamic message)
        {
            try
            {
                string commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🔍 Handling query command ({commandId})");

                // Execute on UI thread
                if (_currentUIDocument?.Application != null)
                {
                    _currentUIDocument.Application.Idling += (sender, e) =>
                    {
                        try
                        {
                            var doc = _currentUIDocument?.Document;
                            var uidoc = _currentUIDocument;

                        if (doc == null)
                        {
                            SendCommandResponse(commandId, false, "No active document", null);
                            return;
                        }

                        // Handle GetElementsByIds or GetSelection
                        string result;
                        if (payload?.elementIds != null)
                        {
                            result = _parameterCommands.GetElementParameters(doc, uidoc, payload);
                        }
                        else
                        {
                            result = _parameterCommands.GetElementParameters(doc, uidoc, payload);
                        }

                        var response = JsonConvert.DeserializeObject<dynamic>(result);
                        SendCommandResponse(commandId, response.success, response.message, response.data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in query command execution: {ex.Message}");
                        SendCommandResponse(commandId, false, ex.Message, null);
                    }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling query command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// Handle modify command (like modifying element parameters)
        /// </summary>
        private void HandleModifyCommand(dynamic message)
        {
            try
            {
                string commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🔧 Handling modify command ({commandId})");

                // Execute on UI thread
                if (_currentUIDocument?.Application != null)
                {
                    _currentUIDocument.Application.Idling += (sender, e) =>
                    {
                        try
                        {
                            var doc = _currentUIDocument?.Document;
                            var uidoc = _currentUIDocument;

                        if (doc == null)
                        {
                            SendCommandResponse(commandId, false, "No active document", null);
                            return;
                        }

                        string result = _parameterCommands.ModifyParameters(doc, uidoc, payload);
                        var response = JsonConvert.DeserializeObject<dynamic>(result);
                        SendCommandResponse(commandId, response.success, response.message, response.data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in modify command execution: {ex.Message}");
                        SendCommandResponse(commandId, false, ex.Message, null);
                    }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling modify command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// Handle AI rename panel elements command
        /// </summary>
        private void HandleAIRenamePanelElements(dynamic message)
        {
            string operationId = $"rename_{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}";
            var startTime = DateTime.UtcNow;

            try
            {
                string commandId = message.id?.ToString();
                var payload = message.payload;
                int payloadSize = JsonConvert.SerializeObject(payload).Length;

                _logger.Log($"🎯 [OP:{operationId}] Starting AI rename panel elements command ({commandId}) | PayloadSize: {payloadSize} bytes | StartTime: {startTime:HH:mm:ss.fff}");

                // Execute using External Event for proper document modification context
                var doc = _currentUIDocument?.Document;
                var uidoc = _currentUIDocument;

                if (doc == null || uidoc == null)
                {
                    var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    _logger.Log($"❌ [OP:{operationId}] No active document | Elapsed: {elapsed}ms");
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data with operation tracking
                _aiParameterHandler.CommandType = "ai_rename_panel_elements";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.OperationId = operationId;
                _aiParameterHandler.StartTime = startTime;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                var preEventTime = DateTime.UtcNow;
                _logger.Log($"⚡ [OP:{operationId}] Raising External Event | PreEventTime: {preEventTime:HH:mm:ss.fff}");

                // Raise External Event
                _aiParameterEvent.Raise();

                var postEventTime = DateTime.UtcNow;
                var eventRaiseMs = (postEventTime - preEventTime).TotalMilliseconds;
                _logger.Log($"📤 [OP:{operationId}] External Event raised | EventRaiseTime: {eventRaiseMs}ms");
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError($"💥 [OP:{operationId}] Error handling AI rename command | Elapsed: {elapsed}ms", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// Handle AI modify parameters command
        /// </summary>
        private void HandleAIModifyParameters(dynamic message)
        {
            try
            {
                string commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🔧 Handling AI modify parameters command ({commandId})");

                // Execute using External Event for proper document modification context
                var doc = _currentUIDocument?.Document;
                var uidoc = _currentUIDocument;

                if (doc == null || uidoc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "ai_modify_parameters";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling AI modify parameters command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// Handle AI analyze panel structure command
        /// </summary>
        private void HandleAIAnalyzePanelStructure(dynamic message)
        {
            try
            {
                string commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🧠 Handling AI analyze panel structure command ({commandId})");

                // Execute using External Event for proper document modification context
                var doc = _currentUIDocument?.Document;
                var uidoc = _currentUIDocument;

                if (doc == null || uidoc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "ai_analyze_panel_structure";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling AI analyze panel structure command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// 🌉 Handle FLC Hybrid Operation - Phase 0 Implementation
        /// Routes to FLC Script Bridge for existing script calls or generation
        /// </summary>
        private void HandleFLCHybridOperation(dynamic message)
        {
            try
            {
                var commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🌉 Processing FLC Hybrid Operation command: {commandId}");

                // Get current document and UI context
                var doc = _currentDocument;
                var uidoc = _currentUIDocument;

                if (doc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "flc_hybrid_operation";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling FLC Hybrid Operation command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// 📊 Handle FLC Script Graduation Analytics - Phase 1 Implementation
        /// Routes to analytics system for script promotion analysis
        /// </summary>
        private void HandleFLCScriptGraduationAnalytics(dynamic message)
        {
            try
            {
                var commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"📊 Processing FLC Script Graduation Analytics command: {commandId}");

                // Get current document and UI context
                var doc = _currentDocument;
                var uidoc = _currentUIDocument;

                if (doc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "flc_script_graduation_analytics";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling FLC Script Graduation Analytics command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// 🏗️ Handle AI Create Elements - Phase 2A Priority 1
        /// Routes to element creation system with atomic rollback
        /// </summary>
        private void HandleAICreateElements(dynamic message)
        {
            try
            {
                var commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🏗️ Processing AI Create Elements command: {commandId}");

                // Get current document and UI context
                var doc = _currentDocument;
                var uidoc = _currentUIDocument;

                if (doc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "ai_create_elements";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling AI Create Elements command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// 📐 Handle AI Modify Geometry - Phase 2A Priority 3
        /// Routes to geometry transformation system with spatial validation
        /// </summary>
        private void HandleAIModifyGeometry(dynamic message)
        {
            try
            {
                var commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"📐 Processing AI Modify Geometry command: {commandId}");

                // Get current document and UI context
                var doc = _currentDocument;
                var uidoc = _currentUIDocument;

                if (doc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "ai_modify_geometry";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling AI Modify Geometry command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// 🔥 Handle Generate Hot Script - Phase 2B KILLER FEATURE
        /// Routes to ScriptHotLoader for AI-generated PyRevit code execution
        /// </summary>
        private void HandleGenerateHotScript(dynamic message)
        {
            try
            {
                var commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🔥 Processing Generate Hot Script command: {commandId}");

                // Get current document and UI context
                var doc = _currentDocument;
                var uidoc = _currentUIDocument;

                if (doc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "generate_hot_script";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling Generate Hot Script command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// 🌟 Handle Execute Custom Operation - Phase 2B Advanced Orchestration
        /// Routes to complex multi-step operation system
        /// </summary>
        private void HandleExecuteCustomOperation(dynamic message)
        {
            try
            {
                var commandId = message.id?.ToString();
                var payload = message.payload;

                _logger.Log($"🌟 Processing Execute Custom Operation command: {commandId}");

                // Get current document and UI context
                var doc = _currentDocument;
                var uidoc = _currentUIDocument;

                if (doc == null)
                {
                    SendCommandResponse(commandId, false, "No active document", null);
                    return;
                }

                // Set up External Event Handler data
                _aiParameterHandler.CommandType = "execute_custom_operation";
                _aiParameterHandler.CommandId = commandId;
                _aiParameterHandler.Payload = payload;
                _aiParameterHandler.Document = doc;
                _aiParameterHandler.UIDocument = uidoc;
                _aiParameterHandler.ResponseCallback = SendCommandResponse;

                // Raise External Event
                _aiParameterEvent.Raise();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling Execute Custom Operation command", ex);
                SendCommandResponse(message.id?.ToString(), false, ex.Message, null);
            }
        }

        /// <summary>
        /// Send command response back to MCP server
        /// </summary>
        private void SendCommandResponse(string commandId, bool success, string message, object data)
        {
            try
            {
                var response = new
                {
                    id = commandId,
                    type = "command_response",
                    success = success,
                    message = message,
                    data = data,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                string jsonResponse = JsonConvert.SerializeObject(response);
                _webSocket?.Send(jsonResponse);
                _logger.Log($"📤 Sent command response: {commandId} - {(success ? "Success" : "Failed")}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending command response: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 FAFB Send progress update to prevent timeouts during large selections
        /// </summary>
        private void SendProgressUpdate(string commandId, string status, int processed, int total, string message)
        {
            try
            {
                var progressMessage = new
                {
                    type = "selection_progress",
                    id = commandId,
                    status = status,
                    processed = processed,
                    total = total,
                    message = message,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                string jsonMessage = JsonConvert.SerializeObject(progressMessage);
                _webSocket?.Send(jsonMessage);
                _logger.Log($"📊 Progress update sent: {status} - {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send progress update", ex);
            }
        }

        /// <summary>
        /// Send selection response back to MCP server using optimized streaming
        /// </summary>
        private async void SendSelectionResponse(TycoonResponse response)
        {
            try
            {
                if (response.Data is SelectionData selectionData && selectionData.Count > 1000)
                {
                    // Use binary streaming for large selections
                    _logger.Log($"🚀 STREAMING: Large selection detected ({selectionData.Count} elements), using binary streaming");

                    var processingTier = DetermineProcessingTier(selectionData.Count);
                    var success = await _streamingManager.StreamSelectionAsync(selectionData, response.CommandId, processingTier);

                    if (!success)
                    {
                        _logger.LogError("Binary streaming failed, falling back to JSON");
                        // Fallback to traditional JSON
                        await SendJsonResponseAsync(response);
                    }
                }
                else
                {
                    // Use traditional JSON for smaller selections
                    await SendJsonResponseAsync(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send selection response", ex);
            }
        }

        /// <summary>
        /// Send traditional JSON response (fallback or small selections)
        /// </summary>
        private async Task SendJsonResponseAsync(TycoonResponse response)
        {
            try
            {
                string json = JsonConvert.SerializeObject(response);
                _webSocket.Send(json);
                _logger.Log($"📤 JSON: Sent selection response: {response.CommandId}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to send JSON response", ex);
                throw;
            }
        }

        /// <summary>
        /// Determine processing tier based on element count
        /// </summary>
        private string DetermineProcessingTier(int elementCount)
        {
            if (elementCount <= 1000) return "GREEN";
            if (elementCount <= 2500) return "YELLOW";
            if (elementCount <= 5000) return "ORANGE";
            if (elementCount <= 10000) return "RED";
            if (elementCount <= 50000) return "EXTREME";
            return "LUDICROUS";
        }

        /// <summary>
        /// Get current selection on-demand (called only when AI is requested)
        /// </summary>
        public TycoonResponse GetCurrentSelection(string commandId = null)
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

                // 🦝💨 FAFB Enhanced Tiered Selection Limits for Massive Processing
                const int GREEN_LIMIT = 1000;    // Immediate execution (< 30 seconds)
                const int YELLOW_LIMIT = 2500;   // Progress tracking (30-60 seconds)
                const int ORANGE_LIMIT = 5000;   // Chunked processing (1-3 minutes)
                const int RED_LIMIT = 10000;     // Heavy chunking (3-10 minutes)
                const int EXTREME_LIMIT = 50000; // FAFB Extreme mode (10+ minutes)

                _logger.Log($"🔍 Selection count: {currentSelection.Count} elements");

                // 🚀 FAFB Send progress update with selection count
                if (!string.IsNullOrEmpty(commandId))
                {
                    SendProgressUpdate(commandId, "selection_analyzed", 0, currentSelection.Count,
                        $"Found {currentSelection.Count} elements, determining processing strategy...");
                }

                // 🦝💨 FAFB Enhanced Tier Determination with Extreme Mode Support
                string tier = currentSelection.Count <= GREEN_LIMIT ? "GREEN" :
                             currentSelection.Count <= YELLOW_LIMIT ? "YELLOW" :
                             currentSelection.Count <= ORANGE_LIMIT ? "ORANGE" :
                             currentSelection.Count <= RED_LIMIT ? "RED" : "EXTREME";

                _logger.Log($"📊 Processing tier: {tier} ({currentSelection.Count} elements)");

                // Enhanced tier handling for massive selections
                if (currentSelection.Count > RED_LIMIT && currentSelection.Count <= EXTREME_LIMIT)
                {
                    _logger.Log($"🔥 EXTREME TIER DETECTED! {currentSelection.Count} > {RED_LIMIT} - ENABLING FAFB MASSIVE PROCESSING!");
                    tier = "EXTREME_CHUNKED"; // FAFB Extreme processing mode
                }
                else if (currentSelection.Count > EXTREME_LIMIT)
                {
                    _logger.Log($"💀 LUDICROUS TIER! {currentSelection.Count} > {EXTREME_LIMIT} - MAXIMUM FAFB PROTECTION MODE!");
                    tier = "LUDICROUS"; // Ultimate protection mode
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
            _logger.Log($"🔄 Processing {elementIds.Count} elements in {tier} tier with advanced pipeline");

            // 🚀 NEW: Use circuit breaker for resilient processing
            try
            {
                return _circuitBreaker.ExecuteAsync(async () =>
                {
                    // 🚀 ADVANCED PIPELINE: Use streaming for all selections > 500 elements
                    if (elementIds.Count > 500)
                    {
                        _logger.Log($"📡 ADVANCED PIPELINE MODE: {elementIds.Count} elements → Pipeline Processing");
                        return await ProcessWithAdvancedPipeline(elementIds, tier);
                    }

                    switch (tier)
                    {
                        case "GREEN":
                            // Immediate processing for small selections with MessagePack
                            _logger.Log($"💚 GREEN tier: Immediate processing with MessagePack");
                            return await ProcessWithMessagePack(elementIds);

                        case "YELLOW":
                            // Process with adaptive chunking
                            _logger.Log($"⚡ YELLOW tier: Adaptive chunking with progress tracking");
                            return await ProcessWithAdaptiveChunking(elementIds);

                        case "ORANGE":
                        case "RED":
                        case "EXTREME_CHUNKED":
                        case "LUDICROUS":
                            // All large selections use advanced pipeline
                            _logger.Log($"🚀 {tier} tier: Advanced pipeline processing");
                            return await ProcessWithAdvancedPipeline(elementIds, tier);

                        default:
                            // Fallback to MessagePack processing
                            return await ProcessWithMessagePack(elementIds);
                    }
                }, $"ProcessSelection-{tier}").Result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Circuit breaker failed for {tier} processing", ex);
                // Fallback to legacy processing
                return _selectionManager.SerializeSelection(_currentDocument, elementIds);
            }
        }

        /// <summary>
        /// 🚀 STREAMING DATA VAULT: Process selection with file streaming
        /// </summary>
        private SelectionData ProcessWithStreaming(ICollection<ElementId> elementIds, string tier)
        {
            try
            {
                var documentTitle = _currentDocument?.Title ?? "Unknown Document";
                var viewName = _currentUIDocument?.ActiveView?.Name ?? "Unknown View";

                // Start streaming session
                _dataVault.StartStreaming(elementIds.Count, documentTitle, viewName);
                _logger.Log($"🚀 STREAMING SESSION STARTED: {elementIds.Count} elements");

                // 🎯 IMMEDIATE RESPONSE FOR MASSIVE SELECTIONS (>10K elements)
                if (elementIds.Count > 10000)
                {
                    _logger.Log($"🚀 MASSIVE SELECTION: {elementIds.Count} elements - Starting background streaming");

                    // Start background streaming task
                    Task.Run(() => ProcessStreamingInBackground(elementIds, tier));

                    // Return immediate response with session info
                    return new SelectionData
                    {
                        Count = elementIds.Count,
                        Elements = new List<RevitElementData>(), // Empty - data is streaming
                        ViewName = viewName,
                        DocumentTitle = documentTitle,
                        Timestamp = DateTime.UtcNow.ToString("O"),
                        ProcessingTier = tier + "_BACKGROUND_STREAMING",
                        StreamingInfo = _dataVault.GetSessionInfo()
                    };
                }

                // Process smaller selections normally (in foreground)
                return ProcessStreamingInForeground(elementIds, tier, documentTitle, viewName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in streaming processing", ex);
                throw;
            }
        }

        /// <summary>
        /// Process streaming in background for massive selections
        /// </summary>
        private void ProcessStreamingInBackground(ICollection<ElementId> elementIds, string tier)
        {
            try
            {
                _logger.Log($"🔄 BACKGROUND STREAMING: Processing {elementIds.Count} elements");

                var chunkSize = GetOptimalChunkSize(elementIds.Count);
                var elementList = elementIds.ToList();
                var chunks = ChunkList(elementList, chunkSize);

                int chunkNumber = 1;
                foreach (var chunk in chunks)
                {
                    _logger.Log($"📤 BACKGROUND chunk {chunkNumber}: {chunk.Count} elements");

                    // Serialize chunk
                    var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);

                    // Stream to vault
                    _dataVault.StreamChunk(chunkData.Elements.Cast<object>().ToList(), chunkNumber);

                    chunkNumber++;

                    // Memory management
                    if (chunkNumber % 5 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }

                // Complete streaming session
                _dataVault.CompleteStreaming();
                _logger.Log($"✅ BACKGROUND STREAMING COMPLETE: {elementIds.Count} elements");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in background streaming", ex);
            }
        }

        /// <summary>
        /// Process streaming in foreground for smaller selections
        /// </summary>
        private SelectionData ProcessStreamingInForeground(ICollection<ElementId> elementIds, string tier, string documentTitle, string viewName)
        {
            var chunkSize = GetOptimalChunkSize(elementIds.Count);
            var elementList = elementIds.ToList();
            var chunks = ChunkList(elementList, chunkSize);
            var allElements = new List<RevitElementData>();

            int chunkNumber = 1;
            foreach (var chunk in chunks)
            {
                _logger.Log($"📤 STREAMING chunk {chunkNumber}: {chunk.Count} elements");

                // Serialize chunk
                var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);

                // Stream to vault
                _dataVault.StreamChunk(chunkData.Elements.Cast<object>().ToList(), chunkNumber);

                // Add to response (for immediate use)
                allElements.AddRange(chunkData.Elements);

                chunkNumber++;

                // Memory management
                if (chunkNumber % 5 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            // Complete streaming session
            _dataVault.CompleteStreaming();
            _logger.Log($"✅ STREAMING COMPLETE: {allElements.Count} elements streamed to vault");

            return new SelectionData
            {
                Count = allElements.Count,
                Elements = allElements,
                ViewName = viewName,
                DocumentTitle = documentTitle,
                Timestamp = DateTime.UtcNow.ToString("O"),
                ProcessingTier = tier,
                StreamingInfo = _dataVault.GetSessionInfo()
            };
        }

        /// <summary>
        /// Get optimal chunk size based on element count
        /// </summary>
        private int GetOptimalChunkSize(int elementCount)
        {
            if (elementCount <= 2500) return 500;
            if (elementCount <= 10000) return 1000;
            if (elementCount <= 50000) return 2000;
            return 4000;
        }

        /// <summary>
        /// Chunk a list into smaller lists
        /// </summary>
        private IEnumerable<List<T>> ChunkList<T>(List<T> source, int chunkSize)
        {
            for (int i = 0; i < source.Count; i += chunkSize)
            {
                yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
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
        /// 🦝💨 FAFB EXTREME MODE: Process massive selections with real-time streaming
        /// </summary>
        private SelectionData ProcessExtremeSelectionInChunks(ICollection<ElementId> elementIds)
        {
            var memoryOptimizer = new DynamicMemoryOptimizer(_logger, memoryCheckInterval: 5);
            var elementList = elementIds.ToList();

            _logger.Log($"💥 FAFB EXTREME PROCESSING: {elementIds.Count} elements with REAL-TIME STREAMING!");
            // Start real-time streaming (stable version)
            return ProcessWithRealTimeStreaming(elementList, memoryOptimizer);

        }

        /// <summary>
        /// 💀 FAFB LUDICROUS MODE: Ultimate protection with real-time streaming
        /// </summary>
        private SelectionData ProcessLudicrousWithStreaming(ICollection<ElementId> elementIds)
        {
            var memoryOptimizer = new DynamicMemoryOptimizer(_logger, memoryCheckInterval: 3); // More frequent monitoring
            var elementList = elementIds.ToList();

            _logger.Log($"💀 FAFB LUDICROUS MODE: {elementIds.Count} elements with ULTRA-SAFE STREAMING!");

            // Use streaming with ultra-conservative settings
            return ProcessWithRealTimeStreaming(elementList, memoryOptimizer);
        }

        /// <summary>
        /// 💀 FAFB LUDICROUS MODE: Ultimate protection for insanely massive selections (LEGACY - kept for fallback)
        /// </summary>
        private SelectionData ProcessLudicrousSelectionInChunks(ICollection<ElementId> elementIds)
        {
            const int CHUNK_SIZE = 250; // Optimized chunks for maximum performance
            var allElements = new List<RevitElementData>();
            var elementList = elementIds.ToList();
            var totalChunks = (elementList.Count + CHUNK_SIZE - 1) / CHUNK_SIZE;

            _logger.Log($"💀 FAFB LUDICROUS MODE ACTIVATED: {elementIds.Count} elements into {totalChunks} optimized batches of {CHUNK_SIZE}");
            _logger.Log($"⏱️ Estimated time: {totalChunks * 5}-{totalChunks * 10} seconds (MAXIMUM PROTECTION)");
            _logger.Log($"🦝💨 That raccoon is about to BOOK IT through {elementIds.Count} elements with MAXIMUM SAFETY!");

            var startTime = DateTime.UtcNow;
            var processedCount = 0;
            var failedChunks = 0;

            for (int i = 0; i < elementList.Count; i += CHUNK_SIZE)
            {
                var chunk = elementList.Skip(i).Take(CHUNK_SIZE).ToList();
                var chunkNumber = (i / CHUNK_SIZE) + 1;
                var progress = (double)chunkNumber / totalChunks * 100;

                // Ultra-conservative memory monitoring
                var memoryBefore = GC.GetTotalMemory(false) / (1024 * 1024);

                // Ultra-conservative memory safety - abort at 4GB
                if (memoryBefore > 4000)
                {
                    _logger.Log($"💀 LUDICROUS MEMORY LIMIT: {memoryBefore}MB > 4000MB - EMERGENCY ABORT!");
                    break;
                }

                _logger.Log($"💀 LUDICROUS chunk {chunkNumber}/{totalChunks} ({progress:F1}%) - {chunk.Count} elements (Memory: {memoryBefore}MB)");

                try
                {
                    // Ultra-safe processing
                    var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);
                    allElements.AddRange(chunkData.Elements);
                    processedCount += chunkData.Elements.Count;

                    // Frequent progress updates
                    if (chunkNumber % 5 == 0)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        var rate = processedCount / elapsed.TotalSeconds;
                        var eta = (elementIds.Count - processedCount) / Math.Max(rate, 1);
                        _logger.Log($"💀 LUDICROUS Progress: {processedCount}/{elementIds.Count} ({rate:F0} elements/sec, ETA: {eta:F0}s)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"❌ LUDICROUS chunk {chunkNumber} failed: {ex.Message} - MAXIMUM SAFETY RECOVERY");
                    failedChunks++;

                    // If too many chunks fail, abort for safety
                    if (failedChunks > totalChunks * 0.1) // 10% failure rate
                    {
                        _logger.Log($"💀 TOO MANY FAILURES ({failedChunks}) - EMERGENCY ABORT FOR SAFETY!");
                        break;
                    }
                }

                // Ultra-aggressive memory cleanup
                chunk.Clear();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                // Maximum yield for system recovery
                System.Threading.Thread.Sleep(100); // 100ms yield for ludicrous safety
            }

            var finalTime = DateTime.UtcNow - startTime;
            var finalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            _logger.Log($"💀 FAFB LUDICROUS COMPLETE! {processedCount} elements in {finalTime.TotalSeconds:F1}s (Memory: {finalMemory}MB, Failed: {failedChunks})");
            _logger.Log($"🦝💨 That chunky raccoon BOOKED IT through {processedCount} elements with MAXIMUM PROTECTION!");

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

        /// <summary>
        /// 🚀 REVOLUTIONARY: Process with real-time streaming - send chunks as they're processed
        /// </summary>
        private SelectionData ProcessWithRealTimeStreaming(List<ElementId> elementList, DynamicMemoryOptimizer memoryOptimizer)
        {
            var startTime = DateTime.UtcNow;
            var processedCount = 0;
            var chunkNumber = 0;
            var currentIndex = 0;
            var totalElements = elementList.Count;

            _logger.Log($"🚀 Starting REAL-TIME STREAMING processing for {totalElements} elements...");

            // Send stream start message
            SendStreamingMessage(StreamingMessageTypes.STREAM_START, null, 0, 0, totalElements, 0, 0, 0, 0);

            while (currentIndex < elementList.Count)
            {
                // Get current optimal chunk size
                var currentChunkSize = memoryOptimizer.GetOptimalChunkSize();
                var remainingElements = elementList.Count - currentIndex;
                var actualChunkSize = Math.Min(currentChunkSize, remainingElements);

                var chunk = elementList.Skip(currentIndex).Take(actualChunkSize).ToList();
                chunkNumber++;
                var progress = (double)currentIndex / elementList.Count * 100;

                _logger.Log($"🚀 STREAMING chunk {chunkNumber} ({progress:F1}%) - {chunk.Count} elements | {memoryOptimizer.GetMemoryStats()}");

                try
                {
                    // Process chunk
                    var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);
                    processedCount += chunkData.Elements.Count;
                    currentIndex += actualChunkSize;

                    // 🚀 IMMEDIATELY SEND CHUNK - No accumulation!
                    var elapsed = DateTime.UtcNow - startTime;
                    var rate = processedCount / Math.Max(elapsed.TotalSeconds, 1);
                    var eta = (totalElements - processedCount) / Math.Max(rate, 1);

                    SendStreamingMessage(
                        StreamingMessageTypes.STREAM_CHUNK,
                        chunkData.Elements,
                        chunkNumber,
                        0, // totalChunks unknown with dynamic sizing
                        totalElements,
                        processedCount,
                        chunkData.Elements.Count,
                        rate,
                        eta
                    );

                    // Update memory optimizer for dynamic chunk sizing
                    memoryOptimizer.UpdateChunkSize();

                    // Progress feedback every 10 chunks
                    if (chunkNumber % 10 == 0)
                    {
                        _logger.Log($"🚀 STREAMING Progress: {processedCount}/{totalElements} ({rate:F0} elements/sec, ETA: {eta:F0}s)");
                    }

                    // 🚀 IMMEDIATE MEMORY RELEASE - No accumulation!
                    chunkData.Elements.Clear();
                    chunk.Clear();

                    // 🧠 AGGRESSIVE MEMORY PRESSURE MONITORING
                    var currentMemory = GC.GetTotalMemory(false) / (1024 * 1024);
                    if (currentMemory > 2000) // 2GB threshold for immediate GC
                    {
                        _logger.Log($"🗑️ Memory pressure detected ({currentMemory}MB) - forcing immediate GC");
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect(); // Double GC for aggressive cleanup
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"❌ STREAMING chunk {chunkNumber} failed: {ex.Message} - CONTINUING WITH NEXT CHUNK");
                    currentIndex += actualChunkSize; // Still advance to avoid infinite loop

                    // Send error for this chunk
                    SendStreamingMessage(StreamingMessageTypes.STREAM_ERROR, null, chunkNumber, 0, totalElements, processedCount, 0, 0, 0);
                }

                // 🗑️ ENHANCED MEMORY CLEANUP STRATEGY
                if (chunkNumber % 2 == 0) // More frequent GC every 2 chunks
                {
                    var memoryBefore = GC.GetTotalMemory(false) / (1024 * 1024);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    var memoryAfter = GC.GetTotalMemory(false) / (1024 * 1024);
                    var freed = memoryBefore - memoryAfter;

                    if (freed > 50) // Log significant memory releases
                    {
                        _logger.Log($"🗑️ GC freed {freed}MB: {memoryBefore}MB → {memoryAfter}MB");
                    }
                }

                // Adaptive yield based on chunk size
                var yieldTime = Math.Max(10, Math.Min(50, actualChunkSize / 20)); // 10-50ms based on chunk size
                System.Threading.Thread.Sleep(yieldTime);
            }

            var finalTime = DateTime.UtcNow - startTime;
            _logger.Log($"🚀 REAL-TIME STREAMING COMPLETE! {processedCount} elements in {finalTime.TotalSeconds:F1}s | {memoryOptimizer.GetMemoryStats()}");

            // Send completion message
            SendStreamingMessage(StreamingMessageTypes.STREAM_COMPLETE, null, chunkNumber, chunkNumber, totalElements, processedCount, 0, 0, 0);

            // Return minimal SelectionData since actual data was streamed
            return new SelectionData
            {
                Count = processedCount,
                Elements = new List<RevitElementData>(), // Empty - data was streamed!
                ViewName = _currentUIDocument?.ActiveView?.Name,
                DocumentTitle = _currentDocument?.Title,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// Send real-time streaming message with enhanced protocol
        /// </summary>
        private void SendStreamingMessage(string messageType, List<RevitElementData> elements, int chunkNumber, int totalChunks, int totalElements, int processedElements, int elementsInChunk, double processingRate, double eta)
        {
            try
            {
                // Check connection health before sending
                if (!_isConnected || _webSocket?.ReadyState != WebSocketState.Open)
                {
                    _logger.LogError($"❌ Cannot send streaming message: Connection not available (Type: {messageType})");
                    return;
                }
                var streamingResponse = new StreamingResponse
                {
                    CommandId = "streaming_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Type = messageType,
                    ChunkNumber = chunkNumber,
                    TotalChunks = totalChunks,
                    Elements = elements ?? new List<RevitElementData>(),
                    Progress = totalElements > 0 ? (double)processedElements / totalElements * 100 : 0,
                    ElementsInChunk = elementsInChunk,
                    TotalElements = totalElements,
                    ProcessedElements = processedElements,
                    MemoryUsage = GC.GetTotalMemory(false),
                    ChunkSize = elements?.Count ?? 0,
                    EstimatedTimeRemaining = eta,
                    ProcessingRate = processingRate,
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    IsComplete = messageType == StreamingMessageTypes.STREAM_COMPLETE,
                    Metadata = new StreamingMetadata
                    {
                        TotalElements = totalElements,
                        ProcessingTier = "EXTREME_STREAMING",
                        ChunkSize = elements?.Count ?? 0,
                        CompressionEnabled = false,
                        BinaryMode = false,
                        ViewName = _currentUIDocument?.ActiveView?.Name,
                        DocumentTitle = _currentDocument?.Title
                    }
                };

                string json = JsonConvert.SerializeObject(streamingResponse);

                // 🔄 RETRY LOGIC for critical streaming messages
                int maxRetries = 3;
                int retryCount = 0;
                bool sent = false;

                while (!sent && retryCount < maxRetries)
                {
                    try
                    {
                        _webSocket.Send(json);
                        sent = true;

                        if (messageType == StreamingMessageTypes.STREAM_CHUNK)
                        {
                            _logger.Log($"📤 STREAMED chunk {chunkNumber}: {elementsInChunk} elements");
                        }
                    }
                    catch (Exception sendEx)
                    {
                        retryCount++;
                        _logger.Log($"⚠️ Send attempt {retryCount} failed for {messageType}: {sendEx.Message}");

                        if (retryCount < maxRetries)
                        {
                            System.Threading.Thread.Sleep(100 * retryCount); // Exponential backoff
                        }
                    }
                }

                if (!sent)
                {
                    _logger.LogError($"❌ Failed to send {messageType} after {maxRetries} attempts - CONNECTION UNSTABLE");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send streaming message: {messageType}", ex);
            }
        }

        /// <summary>
        /// 🚀 REVOLUTIONARY: Parallel processing with real-time streaming for maximum performance
        /// </summary>
        private SelectionData ProcessWithParallelStreaming(List<ElementId> elementList, DynamicMemoryOptimizer memoryOptimizer)
        {
            var startTime = DateTime.UtcNow;
            var totalElements = elementList.Count;
            var processedCount = 0;
            var chunkNumber = 0;

            // Determine optimal parallelism based on system resources
            var maxParallelism = DetermineOptimalParallelism();

            _logger.Log($"🚀 Starting PARALLEL STREAMING processing for {totalElements} elements with {maxParallelism} parallel streams...");

            // Send stream start message
            SendStreamingMessage(StreamingMessageTypes.STREAM_START, null, 0, 0, totalElements, 0, 0, 0, 0);

            // Create thread-safe collections for parallel processing
            var processedChunks = new ConcurrentBag<(int chunkId, List<RevitElementData> elements)>();
            var chunkQueue = new ConcurrentQueue<(int chunkId, List<ElementId> chunk)>();
            var lockObject = new object();

            // Prepare chunks for parallel processing
            var currentIndex = 0;
            var preparedChunkId = 0;
            while (currentIndex < elementList.Count)
            {
                var currentChunkSize = memoryOptimizer.GetOptimalChunkSize();
                var remainingElements = elementList.Count - currentIndex;
                var actualChunkSize = Math.Min(currentChunkSize, remainingElements);

                var chunk = elementList.Skip(currentIndex).Take(actualChunkSize).ToList();
                chunkQueue.Enqueue((++preparedChunkId, chunk));
                currentIndex += actualChunkSize;
            }

            var totalChunks = preparedChunkId;
            _logger.Log($"🔄 Prepared {totalChunks} chunks for parallel processing");

            // Process chunks in parallel
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism
            };

            var processedChunkCount = 0;

            Parallel.ForEach(chunkQueue.ToArray(), parallelOptions, chunkInfo =>
            {
                var (chunkId, chunk) = chunkInfo;

                try
                {
                    var progress = (double)chunkId / totalChunks * 100;
                    _logger.Log($"🚀 PARALLEL chunk {chunkId}/{totalChunks} ({progress:F1}%) - {chunk.Count} elements | Thread: {Thread.CurrentThread.ManagedThreadId}");

                    // Process chunk (this is thread-safe as each chunk is independent)
                    var chunkData = _selectionManager.SerializeSelection(_currentDocument, chunk);

                    // Thread-safe increment
                    lock (lockObject)
                    {
                        processedCount += chunkData.Elements.Count;
                        processedChunkCount++;

                        // Update memory optimizer periodically (thread-safe)
                        if (processedChunkCount % 5 == 0)
                        {
                            memoryOptimizer.UpdateChunkSize();
                        }
                    }

                    // 🚀 IMMEDIATELY SEND CHUNK - Thread-safe streaming
                    var elapsed = DateTime.UtcNow - startTime;
                    var rate = processedCount / Math.Max(elapsed.TotalSeconds, 1);
                    var eta = (totalElements - processedCount) / Math.Max(rate, 1);

                    SendStreamingMessage(
                        StreamingMessageTypes.STREAM_CHUNK,
                        chunkData.Elements,
                        chunkId,
                        totalChunks,
                        totalElements,
                        processedCount,
                        chunkData.Elements.Count,
                        rate,
                        eta
                    );

                    // Progress feedback every 10 chunks
                    if (chunkId % 10 == 0)
                    {
                        _logger.Log($"🚀 PARALLEL Progress: {processedCount}/{totalElements} ({rate:F0} elements/sec, ETA: {eta:F0}s)");
                    }

                    // 🚀 IMMEDIATE MEMORY RELEASE - No accumulation!
                    chunkData.Elements.Clear();
                    chunk.Clear();

                    // 🧠 PARALLEL-SAFE MEMORY PRESSURE MONITORING
                    var currentMemory = GC.GetTotalMemory(false) / (1024 * 1024);
                    if (currentMemory > 4000) // 4GB threshold for parallel processing
                    {
                        lock (lockObject)
                        {
                            _logger.Log($"🗑️ Parallel memory pressure detected ({currentMemory}MB) - forcing GC");
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"❌ PARALLEL chunk {chunkId} failed: {ex.Message} - CONTINUING WITH OTHER CHUNKS");

                    // Send error for this chunk
                    SendStreamingMessage(StreamingMessageTypes.STREAM_ERROR, null, chunkId, totalChunks, totalElements, processedCount, 0, 0, 0);
                }
            });

            var finalTime = DateTime.UtcNow - startTime;
            var finalRate = processedCount / finalTime.TotalSeconds;

            _logger.Log($"🚀 PARALLEL STREAMING COMPLETE! {processedCount} elements in {finalTime.TotalSeconds:F1}s ({finalRate:F0} elements/sec) | {memoryOptimizer.GetMemoryStats()}");

            // Send completion message
            SendStreamingMessage(StreamingMessageTypes.STREAM_COMPLETE, null, totalChunks, totalChunks, totalElements, processedCount, 0, finalRate, 0);

            // Return minimal SelectionData since actual data was streamed
            return new SelectionData
            {
                Count = processedCount,
                Elements = new List<RevitElementData>(), // Empty - data was streamed!
                ViewName = _currentUIDocument?.ActiveView?.Name,
                DocumentTitle = _currentDocument?.Title,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
        }

        /// <summary>
        /// Determine optimal parallelism based on system resources
        /// </summary>
        private int DetermineOptimalParallelism()
        {
            var processorCount = Environment.ProcessorCount;
            var availableMemoryGB = GC.GetTotalMemory(false) / (1024 * 1024 * 1024);

            // Conservative parallelism based on system resources
            int optimalParallelism;

            if (availableMemoryGB > 32) // High-end system
            {
                optimalParallelism = Math.Min(8, processorCount); // Up to 8 parallel streams
            }
            else if (availableMemoryGB > 16) // Standard system
            {
                optimalParallelism = Math.Min(4, processorCount); // Up to 4 parallel streams
            }
            else // Conservative system
            {
                optimalParallelism = Math.Min(2, processorCount); // Up to 2 parallel streams
            }

            _logger.Log($"🔄 Optimal parallelism: {optimalParallelism} streams (CPU: {processorCount}, Memory: {availableMemoryGB}GB)");
            return optimalParallelism;
        }

        /// <summary>
        /// 🚀 Process with MessagePack serialization for small selections
        /// </summary>
        private async Task<SelectionData> ProcessWithMessagePack(ICollection<ElementId> elementIds)
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.Log($"📦 Processing {elementIds.Count} elements with MessagePack (ID: {correlationId})");

            var selectionData = _selectionManager.SerializeSelection(_currentDocument, elementIds);
            var (serialized, _) = await _serializationManager.SerializeWithCorrelationAsync(selectionData.Elements, correlationId);

            _logger.Log($"✅ MessagePack processing complete: {serialized.Length:N0} bytes (ID: {correlationId})");

            return selectionData;
        }

        /// <summary>
        /// 🚀 Process with adaptive chunking for medium selections
        /// </summary>
        private async Task<SelectionData> ProcessWithAdaptiveChunking(ICollection<ElementId> elementIds)
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.Log($"🎯 Processing {elementIds.Count} elements with adaptive chunking (ID: {correlationId})");

            var selectionData = _selectionManager.SerializeSelection(_currentDocument, elementIds);
            var elements = selectionData.Elements;
            var chunks = _chunkManager.CalculateAdaptiveChunks(elements);

            var processedElements = new List<object>();
            var startTime = DateTime.UtcNow;

            foreach (var chunk in chunks)
            {
                var chunkElements = elements.Skip(chunk.StartIndex).Take(chunk.Size).ToList();
                var serialized = await _serializationManager.SerializeAsync(chunkElements);

                processedElements.AddRange(chunkElements);

                // Record performance for adaptive learning
                var chunkTime = DateTime.UtcNow - startTime;
                _chunkManager.RecordPerformance(chunk.Size, chunkTime, GC.GetTotalMemory(false));

                _logger.Log($"📊 Processed chunk {chunk.Index + 1}/{chunks.Count()} " +
                           $"({chunk.Size:N0} elements, {chunkTime.TotalMilliseconds:F0}ms)");
            }

            var stats = _chunkManager.GetPerformanceStats();
            _logger.Log($"✅ Adaptive chunking complete: {processedElements.Count:N0} elements, " +
                       $"avg throughput: {stats.AverageThroughput:F0} elem/s (ID: {correlationId})");

            return selectionData;
        }

        /// <summary>
        /// 🚀 Process with advanced pipeline for large selections
        /// </summary>
        private async Task<SelectionData> ProcessWithAdvancedPipeline(ICollection<ElementId> elementIds, string tier)
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.Log($"🚀 Processing {elementIds.Count} elements with advanced pipeline - {tier} (ID: {correlationId})");

            var selectionData = _selectionManager.SerializeSelection(_currentDocument, elementIds);
            var elements = selectionData.Elements;

            // Define transmission handler for pipeline
            Task<bool> TransmissionHandler(SerializedChunk chunk)
            {
                try
                {
                    // Stream to data vault for background processing
                    var chunkData = new List<object> { chunk.SerializedData.ToArray() };
                    _dataVault.StreamChunk(chunkData, chunk.ChunkInfo.Index, "serialized_chunk");

                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Transmission failed for chunk {chunk.ChunkInfo.Index}", ex);
                    return Task.FromResult(false);
                }
            }

            // Process through pipeline
            var result = await _pipelineManager.ProcessAsync(elements, TransmissionHandler, correlationId);

            _logger.Log($"🎉 Advanced pipeline complete: {result.ProcessedChunks}/{result.TotalChunks} chunks, " +
                       $"{result.Throughput:F0} elem/s, {(result.Success ? "✅" : "❌")} (ID: {correlationId})");

            return selectionData;
        }

        /// <summary>
        /// Execute AI renaming logic for panel elements
        /// </summary>
        private string ExecuteAIRenaming(Document doc, UIDocument uidoc, List<string> elementIds, string namingConvention, string direction, bool dryRun)
        {
            try
            {
                _logger.Log($"🎯 Executing AI renaming: {elementIds?.Count ?? 0} elements, convention: {namingConvention}, direction: {direction}");

                // Get elements to rename (use selection if no IDs provided)
                var elements = new List<Element>();
                if (elementIds != null && elementIds.Count > 0)
                {
                    foreach (var idStr in elementIds)
                    {
                        if (int.TryParse(idStr, out int id))
                        {
                            var element = doc.GetElement(new ElementId(id));
                            if (element != null) elements.Add(element);
                        }
                    }
                }
                else
                {
                    // Use current selection
                    var selection = uidoc.Selection.GetElementIds();
                    foreach (var id in selection)
                    {
                        var element = doc.GetElement(id);
                        if (element != null) elements.Add(element);
                    }
                }

                if (elements.Count == 0)
                {
                    return JsonConvert.SerializeObject(new { success = false, message = "No elements found to rename", data = (object)null });
                }

                // Analyze spatial positions and generate renaming plan
                var renamingPlan = GenerateRenamingPlan(elements, namingConvention, direction);

                if (dryRun)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        message = $"Preview: {renamingPlan.Count} elements would be renamed",
                        data = new { dryRun = true, renamingPlan = renamingPlan }
                    });
                }

                // Apply the renaming
                using (Transaction trans = new Transaction(doc, "AI Rename Panel Elements"))
                {
                    trans.Start();

                    int successCount = 0;
                    var errors = new List<string>();

                    foreach (var rename in renamingPlan)
                    {
                        try
                        {
                            var element = doc.GetElement(new ElementId(rename.ElementId));
                            if (element != null)
                            {
                                // Update BIMSF_Label parameter
                                var bimsf_label = element.LookupParameter("BIMSF_Label");
                                if (bimsf_label != null && !bimsf_label.IsReadOnly)
                                {
                                    bimsf_label.Set(rename.NewName);
                                }

                                // Update Label parameter
                                var label = element.LookupParameter("Label");
                                if (label != null && !label.IsReadOnly)
                                {
                                    label.Set(rename.NewName);
                                }

                                successCount++;
                                _logger.Log($"✅ Renamed element {rename.ElementId}: '{rename.CurrentName}' → '{rename.NewName}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Element {rename.ElementId}: {ex.Message}");
                            _logger.LogError($"❌ Failed to rename element {rename.ElementId}: {ex.Message}");
                        }
                    }

                    trans.Commit();

                    return JsonConvert.SerializeObject(new
                    {
                        success = successCount > 0,
                        message = $"Successfully renamed {successCount} elements" + (errors.Count > 0 ? $", {errors.Count} errors" : ""),
                        data = new {
                            applied = true,
                            successCount = successCount,
                            errorCount = errors.Count,
                            errors = errors,
                            renamingPlan = renamingPlan
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AI renaming execution: {ex.Message}");
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message, data = (object)null });
            }
        }

        /// <summary>
        /// Generate renaming plan based on spatial analysis
        /// </summary>
        private List<dynamic> GenerateRenamingPlan(List<Element> elements, string namingConvention, string direction)
        {
            var plan = new List<dynamic>();

            // Group elements by panel container
            var panelGroups = elements.GroupBy(e =>
            {
                var container = e.LookupParameter("BIMSF_Container")?.AsString() ??
                               e.LookupParameter("MasterContainer")?.AsString() ??
                               "Unassigned";
                return container;
            });

            foreach (var panelGroup in panelGroups)
            {
                var panelElements = panelGroup.ToList();

                // Sort elements spatially based on direction
                var sortedElements = SortElementsSpatially(panelElements, direction);

                // Generate names based on convention
                var elementNames = GenerateElementNames(sortedElements, namingConvention);

                // Create renaming entries
                for (int i = 0; i < sortedElements.Count; i++)
                {
                    var element = sortedElements[i];
                    var newName = elementNames[i];
                    var currentName = element.LookupParameter("BIMSF_Label")?.AsString() ??
                                     element.LookupParameter("Label")?.AsString() ??
                                     "Unnamed";

                    if (newName != currentName)
                    {
                        plan.Add(new
                        {
                            ElementId = element.Id.IntegerValue,
                            CurrentName = currentName,
                            NewName = newName,
                            ElementType = element.Category?.Name ?? "Unknown",
                            Panel = panelGroup.Key,
                            Reason = $"Rename {element.Category?.Name} from '{currentName}' to '{newName}' following {namingConvention} convention"
                        });
                    }
                }
            }

            return plan;
        }

        /// <summary>
        /// Sort elements spatially based on direction
        /// </summary>
        private List<Element> SortElementsSpatially(List<Element> elements, string direction)
        {
            return elements.OrderBy(element =>
            {
                var location = (element.Location as LocationPoint)?.Point ??
                              (element.Location as LocationCurve)?.Curve?.GetEndPoint(0) ??
                              XYZ.Zero;

                switch (direction.ToLower())
                {
                    case "left_to_right":
                        return location.X;
                    case "bottom_to_top":
                        return location.Z;
                    case "front_to_back":
                        return location.Y;
                    default:
                        return location.X; // Default to left-to-right
                }
            }).ToList();
        }

        /// <summary>
        /// Generate element names based on convention
        /// </summary>
        private List<string> GenerateElementNames(List<Element> sortedElements, string convention)
        {
            var names = new List<string>();
            var typeCounts = new Dictionary<string, int>();

            foreach (var element in sortedElements)
            {
                var category = element.Category?.Name ?? "Unknown";
                var description = element.LookupParameter("BIMSF_Description")?.AsString() ?? "";

                string name = "";

                switch (convention.ToLower())
                {
                    case "flc_standard":
                        if (category == "Structural Framing")
                        {
                            if (description.Contains("TTOP") || description.Contains("TOP"))
                            {
                                name = "Top Track";
                            }
                            else if (description.Contains("TBOT") || description.Contains("BOT"))
                            {
                                name = "Bottom Track";
                            }
                            else if (description.Contains("EV") || description.Contains("STUD"))
                            {
                                if (!typeCounts.ContainsKey("Stud")) typeCounts["Stud"] = 0;
                                typeCounts["Stud"]++;
                                name = $"Stud {typeCounts["Stud"]}";
                            }
                            else
                            {
                                if (!typeCounts.ContainsKey(category)) typeCounts[category] = 0;
                                typeCounts[category]++;
                                name = $"{category} {typeCounts[category]}";
                            }
                        }
                        else
                        {
                            if (!typeCounts.ContainsKey(category)) typeCounts[category] = 0;
                            typeCounts[category]++;
                            name = $"{category} {typeCounts[category]}";
                        }
                        break;

                    case "sequential_numbers":
                        if (!typeCounts.ContainsKey(category)) typeCounts[category] = 0;
                        typeCounts[category]++;
                        name = $"{category} {typeCounts[category]}";
                        break;

                    default:
                        name = element.LookupParameter("BIMSF_Label")?.AsString() ?? $"Element {names.Count + 1}";
                        break;
                }

                names.Add(name);
            }

            return names;
        }
    }

    /// <summary>
    /// External Event Handler for AI Parameter Management Commands
    /// Allows proper document modification from external threads
    /// </summary>
    public class AIParameterEventHandler : IExternalEventHandler
    {
        private TycoonRevitAddin.Utils.Logger _logger;
        private ParameterManagementCommands _parameterCommands;

        // Command data storage
        public string CommandType { get; set; }
        public string CommandId { get; set; }
        public string OperationId { get; set; }
        public DateTime StartTime { get; set; }
        public dynamic Payload { get; set; }
        public Document Document { get; set; }
        public UIDocument UIDocument { get; set; }
        public Action<string, bool, string, object> ResponseCallback { get; set; }

        public AIParameterEventHandler(TycoonRevitAddin.Utils.Logger logger, ParameterManagementCommands parameterCommands)
        {
            _logger = logger;
            _parameterCommands = parameterCommands;
        }

        public void Execute(UIApplication app)
        {
            var executeStartTime = DateTime.UtcNow;
            var totalElapsed = (executeStartTime - StartTime).TotalMilliseconds;

            try
            {
                _logger.Log($"🤖 [OP:{OperationId}] Executing AI parameter command: {CommandType} ({CommandId}) | QueueTime: {totalElapsed}ms");

                var commandStartTime = DateTime.UtcNow;
                string result = "";

                switch (CommandType)
                {
                    case "ai_rename_panel_elements":
                        _logger.Log($"🎯 [OP:{OperationId}] Starting RenamePanelElements execution");
                        result = _parameterCommands.RenamePanelElements(Document, UIDocument, Payload);
                        break;
                    case "ai_modify_parameters":
                        _logger.Log($"🔧 [OP:{OperationId}] Starting ModifyParameters execution");
                        result = _parameterCommands.ModifyParameters(Document, UIDocument, Payload);
                        break;
                    case "ai_analyze_panel_structure":
                        _logger.Log($"🧠 [OP:{OperationId}] Starting AnalyzePanelStructure execution");
                        result = _parameterCommands.AnalyzePanelStructure(Document, UIDocument, Payload);
                        break;
                    case "flc_hybrid_operation":
                        _logger.Log($"🌉 [OP:{OperationId}] Starting FLC Hybrid Operation execution");
                        result = _parameterCommands.ExecuteFLCOperation(Document, UIDocument, Payload);
                        break;
                    case "flc_script_graduation_analytics":
                        _logger.Log($"📊 [OP:{OperationId}] Starting FLC Script Graduation Analytics execution");
                        result = _parameterCommands.GetFLCScriptGraduationAnalytics(Document, UIDocument, Payload);
                        break;
                    case "ai_create_elements":
                        _logger.Log($"🏗️ [OP:{OperationId}] Starting AI Create Elements execution");
                        result = _parameterCommands.CreateElements(Document, UIDocument, Payload);
                        break;
                    case "ai_modify_geometry":
                        _logger.Log($"📐 [OP:{OperationId}] Starting AI Modify Geometry execution");
                        result = _parameterCommands.ModifyGeometry(Document, UIDocument, Payload);
                        break;
                    case "generate_hot_script":
                        _logger.Log($"🔥 [OP:{OperationId}] Starting Generate Hot Script execution");
                        result = _parameterCommands.GenerateHotScript(Document, UIDocument, Payload);
                        break;
                    case "execute_custom_operation":
                        _logger.Log($"🌟 [OP:{OperationId}] Starting Execute Custom Operation execution");
                        result = _parameterCommands.ExecuteCustomOperation(Document, UIDocument, Payload);
                        break;
                    default:
                        _logger.Log($"❌ [OP:{OperationId}] Unknown command type: {CommandType}");
                        result = JsonConvert.SerializeObject(new
                        {
                            success = false,
                            message = $"Unknown AI parameter command: {CommandType}",
                            data = (object)null
                        });
                        break;
                }

                var commandEndTime = DateTime.UtcNow;
                var commandElapsed = (commandEndTime - commandStartTime).TotalMilliseconds;
                var totalOperationTime = (commandEndTime - StartTime).TotalMilliseconds;

                _logger.Log($"⏱️ [OP:{OperationId}] Command execution completed | CommandTime: {commandElapsed}ms | TotalTime: {totalOperationTime}ms");

                var response = JsonConvert.DeserializeObject<dynamic>(result);
                bool success = response.success ?? false;
                string message = response.message?.ToString() ?? "";
                object data = response.data;

                var responseStartTime = DateTime.UtcNow;
                ResponseCallback?.Invoke(CommandId, success, message, data);
                var responseEndTime = DateTime.UtcNow;
                var responseTime = (responseEndTime - responseStartTime).TotalMilliseconds;

                _logger.Log($"📤 [OP:{OperationId}] Response sent | Success: {success} | ResponseTime: {responseTime}ms | Message: {message}");
            }
            catch (Exception ex)
            {
                var errorTime = DateTime.UtcNow;
                var errorElapsed = (errorTime - StartTime).TotalMilliseconds;
                _logger.LogError($"💥 [OP:{OperationId}] Error executing AI parameter command {CommandType} | ErrorTime: {errorElapsed}ms", ex);
                ResponseCallback?.Invoke(CommandId, false, ex.Message, null);
            }
        }

        public string GetName()
        {
            return "AI Parameter Management Event Handler";
        }
    }
}
