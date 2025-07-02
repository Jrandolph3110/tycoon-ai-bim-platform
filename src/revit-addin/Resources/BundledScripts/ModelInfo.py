# Description: Display basic model information and statistics
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# @capability: P1-Deterministic
# @stack: Analysis Tools
# @panel: LocalScripts

"""
Model Info - Essential Bundled Script
Displays basic information about the current Revit model
Provides immediate functionality for fresh installations
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
import System

def main():
    """Display basic model information"""
    
    # Get the current document
    doc = __revit__.ActiveUIDocument.Document
    
    if doc is None:
        TaskDialog.Show("Error", "No active document found")
        return
    
    try:
        # Basic document info
        model_name = doc.Title
        model_path = doc.PathName if doc.PathName else "Not saved"
        is_workshared = doc.IsWorkshared
        
        # Get Revit version info
        app = doc.Application
        revit_version = app.VersionName
        revit_build = app.VersionBuild
        
        # Count basic elements
        all_elements = FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount()
        element_types = FilteredElementCollector(doc).WhereElementIsElementType().GetElementCount()
        
        # Count views
        views = FilteredElementCollector(doc).OfClass(View).GetElementCount()
        
        # Count sheets
        sheets = FilteredElementCollector(doc).OfClass(ViewSheet).GetElementCount()
        
        # Get project info
        project_info = doc.ProjectInformation
        project_name = project_info.Name if project_info.Name else "Not specified"
        project_number = project_info.Number if project_info.Number else "Not specified"
        
        # Build info message
        message = f"Model: {model_name}\n"
        message += f"Path: {model_path}\n"
        message += f"Project: {project_name}\n"
        message += f"Number: {project_number}\n"
        message += f"Workshared: {'Yes' if is_workshared else 'No'}\n\n"
        
        message += f"Revit Version: {revit_version}\n"
        message += f"Build: {revit_build}\n\n"
        
        message += "Element Counts:\n"
        message += "-" * 20 + "\n"
        message += f"Model Elements: {all_elements:,}\n"
        message += f"Element Types: {element_types:,}\n"
        message += f"Views: {views:,}\n"
        message += f"Sheets: {sheets:,}\n"
        
        # Show results
        TaskDialog.Show("Model Information", message)
        
    except Exception as e:
        TaskDialog.Show("Error", f"Failed to get model information:\n{str(e)}")

if __name__ == "__main__":
    main()
