# Capability: P1-Deterministic
# Description: Count selected elements by category and type
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# Category: Analysis

"""
🔵 P1-DETERMINISTIC: Element Counter
Counts selected elements by category and type, providing detailed statistics
for BIM analysis and reporting.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from System.Collections.Generic import Dictionary
from System import String

def count_elements():
    """Count selected elements by category and type"""
    doc = __revit__.ActiveUIDocument.Document
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Element Counter", "Please select elements to count")
        return
    
    # Dictionary to store counts
    category_counts = Dictionary[String, int]()
    type_counts = Dictionary[String, int]()
    
    # Count elements by category and type
    for element_id in selected_ids:
        element = doc.GetElement(element_id)
        
        if element and element.Category:
            category_name = element.Category.Name
            
            # Count by category
            if category_counts.ContainsKey(category_name):
                category_counts[category_name] += 1
            else:
                category_counts[category_name] = 1
            
            # Count by type
            element_type = None
            if hasattr(element, 'GetTypeId'):
                type_id = element.GetTypeId()
                if type_id != ElementId.InvalidElementId:
                    element_type = doc.GetElement(type_id)
            
            if element_type:
                type_name = f"{category_name}: {element_type.Name}"
            else:
                type_name = f"{category_name}: [No Type]"
            
            if type_counts.ContainsKey(type_name):
                type_counts[type_name] += 1
            else:
                type_counts[type_name] = 1
    
    # Build result message
    message = f"📊 ELEMENT COUNT ANALYSIS\n\n"
    message += f"Total Selected: {len(selected_ids)} elements\n\n"
    
    message += "📋 BY CATEGORY:\n"
    for category in category_counts.Keys:
        message += f"  • {category}: {category_counts[category]}\n"
    
    message += "\n🏷️ BY TYPE:\n"
    for type_name in type_counts.Keys:
        message += f"  • {type_name}: {type_counts[type_name]}\n"
    
    TaskDialog.Show("Element Counter Results", message)

if __name__ == "__main__":
    count_elements()
