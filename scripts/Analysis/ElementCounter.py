# -*- coding: utf-8 -*-
"""
Element Counter - GitHub Script
Count selected elements by category and type

@capability: P1-Deterministic
@category: Analysis
@author: Tycoon AI-BIM Platform
@version: 1.0.0
@description: Count selected elements by category and type
@selection: optional
"""

__title__ = "Element Counter"
__author__ = "Tycoon AI-BIM Platform"
__version__ = "1.0.0"

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from pyrevit import revit, DB, UI
from pyrevit import script

def main():
    """Count elements in current selection or entire model"""
    
    # Get current selection
    selection = revit.get_selection()
    
    if not selection:
        # If no selection, ask user if they want to count all elements
        result = UI.TaskDialog.Show(
            "Element Counter",
            "No elements selected. Count all elements in model?",
            UI.TaskDialogCommonButtons.Yes | UI.TaskDialogCommonButtons.No
        )
        
        if result == UI.TaskDialogResult.Yes:
            # Get all elements in model
            collector = FilteredElementCollector(revit.doc)
            elements = collector.WhereElementIsNotElementType().ToElements()
        else:
            return
    else:
        elements = selection
    
    # Count elements by category
    category_counts = {}
    type_counts = {}
    
    for element in elements:
        # Count by category
        if element.Category:
            cat_name = element.Category.Name
            category_counts[cat_name] = category_counts.get(cat_name, 0) + 1
            
            # Count by type within category
            type_name = element.Name if hasattr(element, 'Name') else "Unknown"
            full_type = f"{cat_name} - {type_name}"
            type_counts[full_type] = type_counts.get(full_type, 0) + 1
        else:
            category_counts["No Category"] = category_counts.get("No Category", 0) + 1
    
    # Create output
    output = script.get_output()
    output.print_md("# Element Count Report")
    output.print_md(f"**Total Elements:** {len(elements)}")
    output.print_md("---")
    
    # Print category summary
    output.print_md("## By Category")
    for category, count in sorted(category_counts.items()):
        output.print_md(f"- **{category}:** {count}")
    
    output.print_md("---")
    
    # Print detailed type breakdown
    output.print_md("## Detailed Breakdown")
    for type_name, count in sorted(type_counts.items()):
        output.print_md(f"- {type_name}: {count}")

if __name__ == "__main__":
    main()
