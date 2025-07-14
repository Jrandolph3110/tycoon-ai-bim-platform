using System;
using System.Collections.Generic;

namespace Tycoon.Scripting.Contracts
{
    /// <summary>
    /// Defines the contract for a runnable script.
    /// Every script assembly must contain at least one public class that implements this interface.
    /// </summary>
    public interface IScript
    {
        /// <summary>
        /// The main entry point for the script. The host application will wrap this entire call
        /// in a single Revit transaction. If the method completes successfully, the transaction
        /// will be committed. If it throws any exception, the transaction will be rolled back.
        /// </summary>
        /// <param name="host">The gateway object providing access to Revit API functionality.</param>
        void Execute(IRevitHost host);
    }

    /// <summary>
    /// Defines the API surface available to scripts for interacting with the Revit host.
    /// This interface is implemented by the proxy in the main AppDomain.
    /// </summary>
    public interface IRevitHost
    {
        // --- Selection & Filtering ---

        /// <summary>
        /// Gets a list of elements currently selected by the user in the Revit UI.
        /// </summary>
        List<ElementDto> GetSelectedElements();

        /// <summary>
        /// Gets all elements belonging to a specific built-in category.
        /// </summary>
        List<ElementDto> GetElementsByCategory(BuiltInCategoryDto category);

        /// <summary>
        /// Gets all elements of a specific family and type.
        /// </summary>
        /// <param name="familyAndTypeName">The combined family and type name, e.g., "W-Wide Flange: W12x26"</param>
        List<ElementDto> GetElementsByType(string familyAndTypeName);

        // --- Parameter Access ---

        /// <summary>
        /// Retrieves all parameters for a given element.
        /// </summary>
        List<ParameterDto> GetElementParameters(int elementId);

        /// <summary>
        /// Sets the value of a parameter on a given element.
        /// </summary>
        void SetElementParameter(int elementId, string parameterName, ParameterValueDto value);

        // --- Element Creation ---

        /// <summary>
        /// Creates a new family instance in the model.
        /// </summary>
        /// <returns>The ElementId of the newly created instance.</returns>
        int CreateFamilyInstance(FamilyInstanceCreationDto creationData);

        // --- UI Feedback ---

        /// <summary>
        /// Displays a simple task dialog to the user.
        /// </summary>
        void ShowMessage(string title, string message);
    }
}
