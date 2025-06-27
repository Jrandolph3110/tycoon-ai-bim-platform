using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using MessagePack;

namespace TycoonRevitAddin.Communication
{
    /// <summary>
    /// Message types for Tycoon-Revit communication
    /// </summary>

    public class TycoonCommand
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("payload")]
        public object Payload { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    public class TycoonResponse
    {
        [JsonProperty("commandId")]
        public string CommandId { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }

    public class RevitElementData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("elementId")]
        public int ElementId { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        [JsonProperty("geometry")]
        public GeometryData Geometry { get; set; }

        [JsonProperty("relationships")]
        public RelationshipData Relationships { get; set; }
    }

    public class GeometryData
    {
        [JsonProperty("location")]
        public Point3D Location { get; set; }

        [JsonProperty("boundingBox")]
        public BoundingBoxData BoundingBox { get; set; }
    }

    public class Point3D
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public class BoundingBoxData
    {
        [JsonProperty("min")]
        public Point3D Min { get; set; }

        [JsonProperty("max")]
        public Point3D Max { get; set; }
    }

    public class RelationshipData
    {
        [JsonProperty("hostId")]
        public string HostId { get; set; }

        [JsonProperty("dependentIds")]
        public List<string> DependentIds { get; set; }
    }

    public class SelectionData
    {
        [JsonProperty("elements")]
        public List<RevitElementData> Elements { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("viewId")]
        public string ViewId { get; set; }

        [JsonProperty("viewName")]
        public string ViewName { get; set; }

        [JsonProperty("documentTitle")]
        public string DocumentTitle { get; set; }

        [JsonProperty("processingTier")]
        public string ProcessingTier { get; set; }

        [JsonProperty("streamingInfo")]
        public object StreamingInfo { get; set; }
    }

    /// <summary>
    /// Optimized streaming chunk for binary transport
    /// </summary>
    [MessagePackObject]
    public class ElementChunk
    {
        [Key(0)]
        [JsonProperty("chunkId")]
        public int ChunkId { get; set; }

        [Key(1)]
        [JsonProperty("totalChunks")]
        public int TotalChunks { get; set; }

        [Key(2)]
        [JsonProperty("elements")]
        public List<RevitElementData> Elements { get; set; }

        [Key(3)]
        [JsonProperty("progress")]
        public double Progress { get; set; }

        [Key(4)]
        [JsonProperty("memoryUsage")]
        public long MemoryUsage { get; set; }

        [Key(5)]
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [Key(6)]
        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Streaming message wrapper for binary transport
    /// </summary>
    [MessagePackObject]
    public class StreamingMessage
    {
        [Key(0)]
        [JsonProperty("type")]
        public string Type { get; set; }

        [Key(1)]
        [JsonProperty("commandId")]
        public string CommandId { get; set; }

        [Key(2)]
        [JsonProperty("chunk")]
        public ElementChunk Chunk { get; set; }

        [Key(3)]
        [JsonProperty("metadata")]
        public StreamingMetadata Metadata { get; set; }
    }

    /// <summary>
    /// Metadata for streaming operations
    /// </summary>
    [MessagePackObject]
    public class StreamingMetadata
    {
        [Key(0)]
        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [Key(1)]
        [JsonProperty("processingTier")]
        public string ProcessingTier { get; set; }

        [Key(2)]
        [JsonProperty("chunkSize")]
        public int ChunkSize { get; set; }

        [Key(3)]
        [JsonProperty("compressionEnabled")]
        public bool CompressionEnabled { get; set; }

        [Key(4)]
        [JsonProperty("binaryMode")]
        public bool BinaryMode { get; set; }

        [Key(5)]
        [JsonProperty("viewName")]
        public string ViewName { get; set; }

        [Key(6)]
        [JsonProperty("documentTitle")]
        public string DocumentTitle { get; set; }
    }

    /// <summary>
    /// Real-time streaming message types for progressive data transmission
    /// </summary>
    public static class StreamingMessageTypes
    {
        public const string STREAM_START = "stream_start";
        public const string STREAM_CHUNK = "stream_chunk";
        public const string STREAM_PROGRESS = "stream_progress";
        public const string STREAM_COMPLETE = "stream_complete";
        public const string STREAM_ERROR = "stream_error";
    }

    /// <summary>
    /// Real-time streaming response for immediate chunk transmission
    /// </summary>
    public class StreamingResponse
    {
        [JsonProperty("commandId")]
        public string CommandId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("chunkNumber")]
        public int ChunkNumber { get; set; }

        [JsonProperty("totalChunks")]
        public int TotalChunks { get; set; }

        [JsonProperty("elements")]
        public List<RevitElementData> Elements { get; set; }

        [JsonProperty("progress")]
        public double Progress { get; set; }

        [JsonProperty("elementsInChunk")]
        public int ElementsInChunk { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("processedElements")]
        public int ProcessedElements { get; set; }

        [JsonProperty("memoryUsage")]
        public long MemoryUsage { get; set; }

        [JsonProperty("chunkSize")]
        public int ChunkSize { get; set; }

        [JsonProperty("estimatedTimeRemaining")]
        public double EstimatedTimeRemaining { get; set; }

        [JsonProperty("processingRate")]
        public double ProcessingRate { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; }

        [JsonProperty("metadata")]
        public StreamingMetadata Metadata { get; set; }
    }
}
