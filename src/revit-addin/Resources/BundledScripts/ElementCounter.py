# Description: Count elements in the current Revit model
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# @capability: P1-Deterministic
# @stack: Analysis Tools
# @panel: LocalScripts

"""
Element Counter - Essential Bundled Script
Counts all elements in the current Revit model by category
Provides immediate functionality for fresh installations
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *

def main():
    """Count elements by category in the current model"""
    
    # Get the current document
    doc = __revit__.ActiveUIDocument.Document
    
    if doc is None:
        TaskDialog.Show("Error", "No active document found")
        return
    
    # Collect all elements (excluding views, schedules, etc.)
    collector = FilteredElementCollector(doc).WhereElementIsNotElementType()
    elements = collector.ToElements()
    
    # Group elements by category
    category_counts = {}
    
    for element in elements:
        try:
            category_name = element.Category.Name if element.Category else "No Category"
            
            if category_name in category_counts:
                category_counts[category_name] += 1
            else:
                category_counts[category_name] = 1
        except:
            # Handle elements without categories
            if "No Category" in category_counts:
                category_counts["No Category"] += 1
            else:
                category_counts["No Category"] = 1
    
    # Sort categories by count (descending)
    sorted_categories = sorted(category_counts.items(), key=lambda x: x[1], reverse=True)
    
    # Build result message
    total_elements = sum(category_counts.values())
    message = f"Total Elements: {total_elements}\n\n"
    message += "Elements by Category:\n"
    message += "-" * 30 + "\n"
    
    for category, count in sorted_categories[:15]:  # Show top 15 categories
        message += f"{category}: {count}\n"
    
    if len(sorted_categories) > 15:
        remaining = len(sorted_categories) - 15
        message += f"\n... and {remaining} more categories"
    
    # Show results
    TaskDialog.Show("Element Count Results", message)

if __name__ == "__main__":
    main()
