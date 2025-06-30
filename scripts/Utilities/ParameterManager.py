# Capability: P1-Deterministic
# Description: Manage and batch edit parameters for FLC workflows
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# Category: Utilities

"""
🔵 P1-DETERMINISTIC: Parameter Manager
Batch parameter management for FLC workflows including BIMSF parameters,
panel numbering, and custom parameter operations.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *

def parameter_manager():
    """Parameter management utilities"""
    
    # Show parameter management options
    dialog_result = show_parameter_dialog()
    
    if dialog_result == "view_parameters":
        view_element_parameters()
    elif dialog_result == "batch_edit":
        batch_edit_parameters()
    elif dialog_result == "clear_bimsf":
        clear_bimsf_parameters()
    elif dialog_result == "validate_parameters":
        validate_flc_parameters()

def show_parameter_dialog():
    """Show parameter management options"""
    dialog = TaskDialog("Parameter Manager")
    dialog.MainInstruction = "Parameter Management Options"
    dialog.MainContent = "Choose parameter management operation:"
    
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "View Parameters", "Display parameters of selected elements")
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Batch Edit", "Edit parameters for multiple elements")
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Clear BIMSF", "Clear BIMSF parameters from selection")
    dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Validate FLC", "Validate FLC parameter consistency")
    
    result = dialog.Show()
    
    if result == TaskDialogResult.CommandLink1:
        return "view_parameters"
    elif result == TaskDialogResult.CommandLink2:
        return "batch_edit"
    elif result == TaskDialogResult.CommandLink3:
        return "clear_bimsf"
    elif result == TaskDialogResult.CommandLink4:
        return "validate_parameters"
    
    return None

def view_element_parameters():
    """Display parameters of selected elements"""
    doc = __revit__.ActiveUIDocument.Document
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Parameter Manager", "Please select elements to view parameters")
        return
    
    # Get first selected element
    element = doc.GetElement(list(selected_ids)[0])
    
    # Build parameter list
    message = f"📋 PARAMETERS FOR: {element.Category.Name}\n"
    message += f"Element ID: {element.Id}\n\n"
    
    # FLC-specific parameters
    flc_params = ["BIMSF_Container", "BIMSF_Id", "Main Panel", "Sub. Panel"]
    
    message += "🏗️ FLC PARAMETERS:\n"
    for param_name in flc_params:
        param = element.LookupParameter(param_name)
        if param:
            if param.HasValue:
                if param.StorageType == StorageType.String:
                    value = param.AsString() or "[Empty]"
                elif param.StorageType == StorageType.Integer:
                    value = str(param.AsInteger())
                elif param.StorageType == StorageType.Double:
                    value = f"{param.AsDouble():.2f}"
                else:
                    value = "[Unknown Type]"
            else:
                value = "[No Value]"
        else:
            value = "[Parameter Not Found]"
        
        message += f"  • {param_name}: {value}\n"
    
    # Basic properties
    message += "\n📐 BASIC PROPERTIES:\n"
    if hasattr(element, 'WallType'):
        message += f"  • Wall Type: {element.WallType.Name}\n"
    
    if element.Category.Name == "Walls":
        length_param = element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)
        if length_param:
            message += f"  • Length: {length_param.AsDouble():.2f} ft\n"
        
        height_param = element.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)
        if height_param:
            message += f"  • Height: {height_param.AsDouble():.2f} ft\n"
    
    TaskDialog.Show("Element Parameters", message)

def batch_edit_parameters():
    """Batch edit BIMSF_Container parameter (example)"""
    doc = __revit__.ActiveUIDocument.Document
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Parameter Manager", "Please select elements for batch editing")
        return
    
    # Simple batch operation: Set BIMSF_Container to "BATCH_EDIT_TEST"
    with Transaction(doc, "Batch Edit Parameters") as t:
        t.Start()
        
        success_count = 0
        error_count = 0
        
        for element_id in selected_ids:
            try:
                element = doc.GetElement(element_id)
                container_param = element.LookupParameter("BIMSF_Container")
                
                if container_param and not container_param.IsReadOnly:
                    container_param.Set("BATCH_EDIT_TEST")
                    success_count += 1
                else:
                    error_count += 1
                    
            except Exception:
                error_count += 1
        
        t.Commit()
    
    message = f"✅ Batch Edit Complete\n\n"
    message += f"Successfully updated: {success_count} elements\n"
    message += f"Errors/Skipped: {error_count} elements\n\n"
    message += "Set BIMSF_Container = 'BATCH_EDIT_TEST'"
    
    TaskDialog.Show("Batch Edit Results", message)

def clear_bimsf_parameters():
    """Clear BIMSF parameters from selected elements"""
    doc = __revit__.ActiveUIDocument.Document
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Parameter Manager", "Please select elements to clear BIMSF parameters")
        return
    
    with Transaction(doc, "Clear BIMSF Parameters") as t:
        t.Start()
        
        cleared_count = 0
        bimsf_params = ["BIMSF_Container", "BIMSF_Id"]
        
        for element_id in selected_ids:
            element = doc.GetElement(element_id)
            
            for param_name in bimsf_params:
                param = element.LookupParameter(param_name)
                if param and not param.IsReadOnly and param.HasValue:
                    param.Set("")
                    cleared_count += 1
        
        t.Commit()
    
    TaskDialog.Show("Clear Complete", f"Cleared BIMSF parameters from {len(selected_ids)} elements\nTotal parameter values cleared: {cleared_count}")

def validate_flc_parameters():
    """Validate FLC parameter consistency"""
    doc = __revit__.ActiveUIDocument.Document
    
    # Get all walls
    wall_collector = FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType()
    
    validation_results = {
        'total_walls': 0,
        'flc_walls': 0,
        'main_panels': 0,
        'sub_panels': 0,
        'missing_container': 0,
        'orphaned_subs': 0
    }
    
    container_values = set()
    
    for wall in wall_collector:
        validation_results['total_walls'] += 1
        
        wall_type_name = wall.WallType.Name.upper()
        if 'FLC' in wall_type_name:
            validation_results['flc_walls'] += 1
            
            # Check Main Panel parameter
            main_panel_param = wall.LookupParameter("Main Panel")
            if main_panel_param and main_panel_param.HasValue:
                if main_panel_param.AsInteger() == 1:
                    validation_results['main_panels'] += 1
                else:
                    validation_results['sub_panels'] += 1
            
            # Check BIMSF_Container
            container_param = wall.LookupParameter("BIMSF_Container")
            if container_param and container_param.HasValue and container_param.AsString():
                container_values.add(container_param.AsString())
            else:
                validation_results['missing_container'] += 1
    
    # Build validation report
    message = "🔍 FLC PARAMETER VALIDATION\n\n"
    message += f"📊 SUMMARY:\n"
    message += f"  • Total Walls: {validation_results['total_walls']}\n"
    message += f"  • FLC Walls: {validation_results['flc_walls']}\n"
    message += f"  • Main Panels: {validation_results['main_panels']}\n"
    message += f"  • Sub Panels: {validation_results['sub_panels']}\n"
    message += f"  • Unique Containers: {len(container_values)}\n\n"
    
    message += "⚠️ ISSUES:\n"
    message += f"  • Missing BIMSF_Container: {validation_results['missing_container']}\n"
    
    if validation_results['missing_container'] == 0:
        message += "\n✅ All FLC walls have proper BIMSF_Container values!"
    
    TaskDialog.Show("FLC Validation Results", message)

if __name__ == "__main__":
    parameter_manager()
