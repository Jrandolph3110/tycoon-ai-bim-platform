using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TycoonRevitAddin.Utils;

namespace TycoonRevitAddin.Storage
{
    /// <summary>
    /// 🚀 TYCOON STREAMING DATA VAULT - Real-time BIM data streaming system
    /// 
    /// Features:
    /// - Real-time file streaming (no memory constraints)
    /// - Live progress monitoring
    /// - Fault-tolerant chunked writing
    /// - Automatic session management
    /// - Historical data retention
    /// </summary>
    public class StreamingDataVault
    {
        private readonly Logger _logger;
        private readonly string _vaultPath;
        private readonly string _sessionId;
        private readonly DateTime _sessionStart;
        
        // File paths
        private readonly string _streamingFile;
        private readonly string _metadataFile;
        private readonly string _progressFile;
        private readonly string _indexFile;
        
        // Streaming state
        private int _chunkCount = 0;
        private int _totalElements = 0;
        private int _processedElements = 0;
        private bool _isStreaming = false;
        
        public StreamingDataVault(Logger logger, string basePath = null)
        {
            _logger = logger;
            _sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _sessionStart = DateTime.UtcNow;
            
            // Create vault directory structure
            _vaultPath = basePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "Tycoon", "DataVault");
            
            var sessionPath = Path.Combine(_vaultPath, "Sessions", _sessionId);
            Directory.CreateDirectory(sessionPath);
            Directory.CreateDirectory(Path.Combine(_vaultPath, "Archive"));
            Directory.CreateDirectory(Path.Combine(_vaultPath, "Database"));
            
            // Initialize file paths
            _streamingFile = Path.Combine(sessionPath, "stream.jsonl");
            _metadataFile = Path.Combine(sessionPath, "metadata.json");
            _progressFile = Path.Combine(sessionPath, "progress.json");
            _indexFile = Path.Combine(sessionPath, "index.json");
            
            _logger.Log($"🏗️ Streaming Data Vault initialized: {_sessionId}");
            _logger.Log($"📁 Vault path: {_vaultPath}");
        }
        
        /// <summary>
        /// Start streaming session with metadata
        /// </summary>
        public void StartStreaming(int totalElements, string documentTitle, string viewName = null)
        {
            _totalElements = totalElements;
            _isStreaming = true;
            
            var metadata = new
            {
                sessionId = _sessionId,
                startTime = _sessionStart.ToString("O"),
                documentTitle = documentTitle,
                viewName = viewName,
                totalElements = totalElements,
                revitVersion = "2024", // Could be detected
                tycoonVersion = "1.0.26.0",
                processingTier = DetermineProcessingTier(totalElements)
            };
            
            File.WriteAllText(_metadataFile, JsonConvert.SerializeObject(metadata, Formatting.Indented));
            UpdateProgress("streaming_started", "Streaming session initialized");
            
            _logger.Log($"🚀 STREAMING STARTED: {totalElements} elements, Session: {_sessionId}");
        }
        
        /// <summary>
        /// Stream a chunk of elements to file
        /// </summary>
        public void StreamChunk(List<object> elements, int chunkNumber, string chunkType = "elements")
        {
            if (!_isStreaming)
            {
                _logger.LogError("Cannot stream chunk - session not started");
                return;
            }
            
            try
            {
                var chunk = new
                {
                    chunkId = chunkNumber,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    type = chunkType,
                    elementCount = elements.Count,
                    elements = elements
                };
                
                // Append chunk to streaming file (JSONL format)
                var chunkJson = JsonConvert.SerializeObject(chunk);
                File.AppendAllText(_streamingFile, chunkJson + Environment.NewLine);
                
                _chunkCount++;
                _processedElements += elements.Count;
                
                UpdateProgress("chunk_streamed", $"Chunk {chunkNumber} streamed ({elements.Count} elements)");
                
                _logger.Log($"📤 CHUNK STREAMED: {chunkNumber} ({elements.Count} elements) → {_streamingFile}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to stream chunk {chunkNumber}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Complete streaming session
        /// </summary>
        public void CompleteStreaming()
        {
            if (!_isStreaming) return;
            
            _isStreaming = false;
            var endTime = DateTime.UtcNow;
            var duration = endTime - _sessionStart;
            
            UpdateProgress("streaming_complete", $"Session complete: {_processedElements} elements in {duration.TotalSeconds:F1}s");
            
            // Create final index
            var index = new
            {
                sessionId = _sessionId,
                startTime = _sessionStart.ToString("O"),
                endTime = endTime.ToString("O"),
                duration = duration.TotalSeconds,
                totalElements = _totalElements,
                processedElements = _processedElements,
                chunkCount = _chunkCount,
                streamingFile = _streamingFile,
                metadataFile = _metadataFile,
                status = "complete"
            };
            
            File.WriteAllText(_indexFile, JsonConvert.SerializeObject(index, Formatting.Indented));
            
            _logger.Log($"✅ STREAMING COMPLETE: {_processedElements} elements, {_chunkCount} chunks, {duration.TotalSeconds:F1}s");
        }
        
        /// <summary>
        /// Update progress file for live monitoring
        /// </summary>
        private void UpdateProgress(string status, string message)
        {
            var progress = new
            {
                sessionId = _sessionId,
                timestamp = DateTime.UtcNow.ToString("O"),
                status = status,
                message = message,
                totalElements = _totalElements,
                processedElements = _processedElements,
                chunkCount = _chunkCount,
                progressPercent = _totalElements > 0 ? (double)_processedElements / _totalElements * 100 : 0,
                isStreaming = _isStreaming
            };
            
            File.WriteAllText(_progressFile, JsonConvert.SerializeObject(progress, Formatting.Indented));
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
        /// Get current session info
        /// </summary>
        public object GetSessionInfo()
        {
            return new
            {
                sessionId = _sessionId,
                vaultPath = _vaultPath,
                streamingFile = _streamingFile,
                metadataFile = _metadataFile,
                progressFile = _progressFile,
                isStreaming = _isStreaming,
                chunkCount = _chunkCount,
                processedElements = _processedElements,
                totalElements = _totalElements
            };
        }
    }
}
