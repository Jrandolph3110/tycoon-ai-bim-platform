using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
    }
}
