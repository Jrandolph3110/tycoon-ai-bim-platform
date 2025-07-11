# -*- coding: utf-8 -*-
"""
Parameter Manager - GitHub Script
Manage and batch edit parameters for FLC workflows

@capability: P1-Deterministic
@category: Utilities
@author: Tycoon AI-BIM Platform
@version: 1.0.0
@description: Manage and batch edit parameters for FLC workflows
@selection: optional
"""

__title__ = "Parameter Manager"
__author__ = "Tycoon AI-BIM Platform"
__version__ = "1.0.0"

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from pyrevit import revit, DB, UI
from pyrevit import script, forms

def get_element_parameters(element):
    """Get all parameters for an element"""
    
    parameters = []
    
    for param in element.Parameters:
        if param.HasValue:
            param_info = {
                'name': param.Definition.Name,
                'value': get_parameter_value(param),
                'type': param.StorageType.ToString(),
                'is_read_only': param.IsReadOnly,
                'parameter': param
            }
            parameters.append(param_info)
    
    return parameters

def get_parameter_value(param):
    """Get parameter value as string"""
    
    if param.StorageType == StorageType.String:
        return param.AsString() or ""
    elif param.StorageType == StorageType.Integer:
        return str(param.AsInteger())
    elif param.StorageType == StorageType.Double:
        return f"{param.AsDouble():.3f}"
    elif param.StorageType == StorageType.ElementId:
        elem_id = param.AsElementId()
        if elem_id != ElementId.InvalidElementId:
            elem = revit.doc.GetElement(elem_id)
            return elem.Name if elem else str(elem_id.IntegerValue)
        return "None"
    else:
        return str(param.AsValueString() or "")

def set_parameter_value(param, value_str):
    """Set parameter value from string"""
    
    try:
        if param.StorageType == StorageType.String:
            param.Set(value_str)
        elif param.StorageType == StorageType.Integer:
            param.Set(int(value_str))
        elif param.StorageType == StorageType.Double:
            param.Set(float(value_str))
        else:
            # For other types, try AsValueString
            param.SetValueString(value_str)
        return True
    except:
        return False

def batch_edit_parameters():
    """Batch edit parameters for selected elements"""
    
    selection = revit.get_selection()
    
    if not selection:
        UI.TaskDialog.Show("Parameter Manager", "Please select elements to edit.")
        return
    
    # Get common parameters across all selected elements
    common_params = None
    
    for elem in selection:
        elem_params = {p['name']: p for p in get_element_parameters(elem) if not p['is_read_only']}
        
        if common_params is None:
            common_params = elem_params
        else:
            # Keep only parameters that exist in all elements
            common_params = {name: param for name, param in common_params.items() 
                           if name in elem_params}
    
    if not common_params:
        UI.TaskDialog.Show("Parameter Manager", "No common editable parameters found.")
        return
    
    # Show parameter selection dialog
    param_names = sorted(common_params.keys())
    selected_param = forms.SelectFromList.show(
        param_names,
        title="Select Parameter to Edit",
        multiselect=False
    )
    
    if not selected_param:
        return
    
    # Get current values for this parameter
    current_values = []
    for elem in selection:
        for param_info in get_element_parameters(elem):
            if param_info['name'] == selected_param:
                current_values.append(param_info['value'])
                break
    
    # Show current values and get new value
    unique_values = list(set(current_values))
    
    if len(unique_values) == 1:
        current_display = f"Current value: {unique_values[0]}"
    else:
        current_display = f"Multiple values: {', '.join(unique_values[:3])}{'...' if len(unique_values) > 3 else ''}"
    
    # Simple input dialog (in real implementation, use a proper form)
    new_value = forms.ask_for_string(
        prompt=f"Enter new value for '{selected_param}':\n{current_display}",
        title="Parameter Manager"
    )
    
    if new_value is None:
        return
    
    # Apply changes
    with revit.Transaction("Batch Edit Parameters"):
        success_count = 0
        error_count = 0
        
        for elem in selection:
            for param in elem.Parameters:
                if param.Definition.Name == selected_param and not param.IsReadOnly:
                    if set_parameter_value(param, new_value):
                        success_count += 1
                    else:
                        error_count += 1
                    break
    
    # Show results
    message = f"Parameter update complete:\n"
    message += f"✅ Successfully updated: {success_count} elements\n"
    if error_count > 0:
        message += f"❌ Failed to update: {error_count} elements"
    
    UI.TaskDialog.Show("Parameter Manager", message)

def export_parameters():
    """Export parameters to text report"""
    
    selection = revit.get_selection()
    
    if not selection:
        UI.TaskDialog.Show("Parameter Manager", "Please select elements to export.")
        return
    
    # Create output
    output = script.get_output()
    output.print_md("# Parameter Export Report")
    output.print_md(f"**Exported {len(selection)} elements**")
    output.print_md("---")
    
    for i, elem in enumerate(selection, 1):
        output.print_md(f"## Element {i}: {elem.Name} (ID: {elem.Id.IntegerValue})")
        
        if elem.Category:
            output.print_md(f"**Category:** {elem.Category.Name}")
        
        # Get parameters
        parameters = get_element_parameters(elem)
        
        # Group by type
        instance_params = [p for p in parameters if 'Instance' in p.get('group', '')]
        type_params = [p for p in parameters if 'Type' in p.get('group', '')]
        other_params = [p for p in parameters if p not in instance_params and p not in type_params]
        
        if instance_params:
            output.print_md("### Instance Parameters")
            for param in sorted(instance_params, key=lambda x: x['name']):
                output.print_md(f"- **{param['name']}:** {param['value']}")
        
        if type_params:
            output.print_md("### Type Parameters")
            for param in sorted(type_params, key=lambda x: x['name']):
                output.print_md(f"- **{param['name']}:** {param['value']}")
        
        if other_params:
            output.print_md("### Other Parameters")
            for param in sorted(other_params, key=lambda x: x['name']):
                output.print_md(f"- **{param['name']}:** {param['value']}")
        
        output.print_md("---")

def main():
    """Main parameter manager menu"""
    
    # Show options dialog
    options = [
        "Batch Edit Parameters",
        "Export Parameters to Report"
    ]
    
    selected_option = forms.SelectFromList.show(
        options,
        title="Parameter Manager - Choose Action",
        multiselect=False
    )
    
    if selected_option == "Batch Edit Parameters":
        batch_edit_parameters()
    elif selected_option == "Export Parameters to Report":
        export_parameters()

if __name__ == "__main__":
    main()
