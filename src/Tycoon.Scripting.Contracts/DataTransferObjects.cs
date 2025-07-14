using System;
using System.Collections.Generic;

namespace Tycoon.Scripting.Contracts
{
    [Serializable]
    public class ElementDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int LevelId { get; set; }
    }

    [Serializable]
    public class ParameterDto
    {
        public string Name { get; set; }
        public ParameterValueDto Value { get; set; }
        /// <summary>
        /// The underlying data type of the parameter (e.g., "Text", "Length", "Integer", "YesNo").
        /// </summary>
        public string StorageType { get; set; }
        public bool IsReadOnly { get; set; }
    }

    [Serializable]
    public class ParameterValueDto
    {
        public string StringValue { get; set; }
        public double? DoubleValue { get; set; } // For numbers, lengths, areas, etc.
        public int? IntValue { get; set; }       // For integers and Yes/No (0 or 1)
        public int? ElementIdValue { get; set; } // For parameters that store another element's ID
    }

    [Serializable]
    public class FamilyInstanceCreationDto
    {
        /// <summary>
        /// The combined family and type name, e.g., "W-Wide Flange: W12x26"
        /// </summary>
        public string FamilyAndTypeName { get; set; }
        public XYZDto Origin { get; set; }
        public double RotationAngleRadians { get; set; }
        public int LevelId { get; set; }
    }

    [Serializable]
    public class XYZDto 
    { 
        public double X { get; set; } 
        public double Y { get; set; } 
        public double Z { get; set; } 
    }

    /// <summary>
    /// A serializable subset of Revit's BuiltInCategory enum.
    /// This must be populated with values needed by scripts.
    /// The host application is responsible for mapping these to the actual Revit API enum.
    /// </summary>
    [Serializable]
    public enum BuiltInCategoryDto
    {
        // Add most common categories first
        OST_Walls,
        OST_StructuralFraming,
        OST_StructuralColumns,
        OST_Floors,
        OST_Doors,
        OST_Windows,
        OST_GenericModel,
        // Add more as required by scripts...
    }

    /// <summary>
    /// Script manifest metadata for discovery and UI display
    /// </summary>
    [Serializable]
    public class ScriptManifest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string EntryAssembly { get; set; }
        public string EntryType { get; set; }
        public string Panel { get; set; } = "Production";
        public string[] Tags { get; set; } = new string[0];
        public bool RequiresSelection { get; set; } = false;
    }
}
