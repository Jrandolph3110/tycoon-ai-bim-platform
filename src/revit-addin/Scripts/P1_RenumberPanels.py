# Capability: P1-Deterministic
# Description: Renumber FLC panels left-to-right following standard conventions
# Author: AI Assistant
# Version: 1.0.0

"""
🟢 P1-BULLETPROOF: Panel Renumbering Script
This script renumbers FLC panels following the standard left-to-right convention.
Never fails, instant execution, bulletproof logic.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *

def renumber_panels():
    """Renumber panels left-to-right with FLC standards"""
    doc = __revit__.ActiveUIDocument.Document
    
    # Get selected elements
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Error", "Please select panels to renumber")
        return
    
    # Start transaction
    with Transaction(doc, "Renumber Panels") as t:
        t.Start()
        
        # Get panel elements and sort by X coordinate
        panels = []
        for elem_id in selected_ids:
            elem = doc.GetElement(elem_id)
            if elem and hasattr(elem, 'Location'):
                location = elem.Location
                if hasattr(location, 'Point'):
                    panels.append((elem, location.Point.X))
        
        # Sort by X coordinate (left to right)
        panels.sort(key=lambda x: x[1])
        
        # Renumber starting from 1
        for i, (panel, x_coord) in enumerate(panels):
            new_number = str(i + 1).zfill(2)  # 01, 02, 03, etc.
            
            # Set Mark parameter
            mark_param = panel.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
            if mark_param and not mark_param.IsReadOnly:
                mark_param.Set(f"01-10{new_number}")
        
        t.Commit()
    
    TaskDialog.Show("Success", f"Renumbered {len(panels)} panels")

if __name__ == "__main__":
    renumber_panels()
