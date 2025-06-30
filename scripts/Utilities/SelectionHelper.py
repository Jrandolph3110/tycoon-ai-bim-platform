# Capability: P1-Deterministic
# Description: Advanced selection utilities for BIM workflows
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# Category: Utilities

"""
🔵 P1-DETERMINISTIC: Selection Helper
Advanced selection utilities including filter by type, parameter values,
and geometric properties for efficient BIM workflows.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from System.Collections.Generic import List

def selection_helper():
    """Advanced selection helper with multiple options"""
    
    # Create selection dialog
    dialog_result = show_selection_dialog()
    
    if dialog_result == "walls_by_type":
        select_walls_by_type()
    elif dialog_result == "elements_by_parameter":
        select_elements_by_parameter()
    elif dialog_result == "similar_elements":
        select_similar_elements()
    elif dialog_result == "flc_walls":
        select_flc_walls()

def show_selection_dialog():
    """Show selection options dialog"""
    dialog = TaskDialog("Selection Helper")
    dialog.MainInstruction = "Choose Selection Method"
    dialog.MainContent = "Select how you want to filter and select elements:"
    
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Select Walls by Type", "Choose wall type and select all instances")
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Select by Parameter", "Filter elements by parameter value")
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Select Similar", "Select elements similar to current selection")
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Select FLC Walls", "Select all FLC wall types")
    
    result = dialog.Show()
    
    if result == TaskDialogResult.CommandLink1:
        return "walls_by_type"
    elif result == TaskDialogResult.CommandLink2:
        return "elements_by_parameter"
    elif result == TaskDialogResult.CommandLink3:
        return "similar_elements"
    elif result == TaskDialogResult.CommandLink4:
        return "flc_walls"
    
    return None

def select_walls_by_type():
    """Select all walls of the same type as selected wall"""
    doc = __revit__.ActiveUIDocument.Document
    uidoc = __revit__.ActiveUIDocument
    selection = uidoc.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Selection Helper", "Please select a wall first")
        return
    
    # Get first selected wall
    first_element = doc.GetElement(list(selected_ids)[0])
    if not first_element or first_element.Category.Name != "Walls":
        TaskDialog.Show("Selection Helper", "Please select a wall")
        return
    
    target_type_id = first_element.GetTypeId()
    
    # Find all walls of the same type
    wall_collector = FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType()
    matching_walls = []
    
    for wall in wall_collector:
        if wall.GetTypeId() == target_type_id:
            matching_walls.append(wall.Id)
    
    # Select matching walls
    selection.SetElementIds(List[ElementId](matching_walls))
    
    wall_type_name = doc.GetElement(target_type_id).Name
    TaskDialog.Show("Selection Complete", f"Selected {len(matching_walls)} walls of type: {wall_type_name}")

def select_elements_by_parameter():
    """Select elements by parameter value (simplified for FLC workflows)"""
    doc = __revit__.ActiveUIDocument.Document
    uidoc = __revit__.ActiveUIDocument
    
    # For this example, select all elements with BIMSF_Container parameter
    all_elements = FilteredElementCollector(doc).WhereElementIsNotElementType()
    matching_elements = []
    
    for element in all_elements:
        bimsf_param = element.LookupParameter("BIMSF_Container")
        if bimsf_param and bimsf_param.HasValue and bimsf_param.AsString():
            matching_elements.append(element.Id)
    
    if matching_elements:
        uidoc.Selection.SetElementIds(List[ElementId](matching_elements))
        TaskDialog.Show("Selection Complete", f"Selected {len(matching_elements)} elements with BIMSF_Container values")
    else:
        TaskDialog.Show("Selection Helper", "No elements found with BIMSF_Container parameter values")

def select_similar_elements():
    """Select elements similar to current selection"""
    doc = __revit__.ActiveUIDocument.Document
    uidoc = __revit__.ActiveUIDocument
    selection = uidoc.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Selection Helper", "Please select elements first")
        return
    
    # Get characteristics of selected elements
    selected_categories = set()
    selected_types = set()
    
    for element_id in selected_ids:
        element = doc.GetElement(element_id)
        if element and element.Category:
            selected_categories.add(element.Category.Id)
            if hasattr(element, 'GetTypeId'):
                selected_types.add(element.GetTypeId())
    
    # Find similar elements
    similar_elements = []
    all_elements = FilteredElementCollector(doc).WhereElementIsNotElementType()
    
    for element in all_elements:
        if element.Category and element.Category.Id in selected_categories:
            if hasattr(element, 'GetTypeId') and element.GetTypeId() in selected_types:
                similar_elements.append(element.Id)
    
    # Select similar elements
    uidoc.Selection.SetElementIds(List[ElementId](similar_elements))
    TaskDialog.Show("Selection Complete", f"Selected {len(similar_elements)} similar elements")

def select_flc_walls():
    """Select all FLC wall types"""
    doc = __revit__.ActiveUIDocument.Document
    uidoc = __revit__.ActiveUIDocument
    
    # Find all walls with FLC in the type name
    wall_collector = FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType()
    flc_walls = []
    
    for wall in wall_collector:
        wall_type_name = wall.WallType.Name.upper()
        if 'FLC' in wall_type_name:
            flc_walls.append(wall.Id)
    
    if flc_walls:
        uidoc.Selection.SetElementIds(List[ElementId](flc_walls))
        TaskDialog.Show("Selection Complete", f"Selected {len(flc_walls)} FLC walls")
    else:
        TaskDialog.Show("Selection Helper", "No FLC walls found in the model")

if __name__ == "__main__":
    selection_helper()
