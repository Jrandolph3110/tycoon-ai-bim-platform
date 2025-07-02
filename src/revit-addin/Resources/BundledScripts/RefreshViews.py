# Description: Refresh all views in the current model
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# @capability: P2-Analytic
# @stack: Management Tools
# @panel: Management

"""
Refresh Views - Essential Bundled Script
Refreshes all views in the current Revit model
Provides immediate functionality for fresh installations
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *

def main():
    """Refresh all views in the model"""
    
    # Get the current document
    doc = __revit__.ActiveUIDocument.Document
    
    if doc is None:
        TaskDialog.Show("Error", "No active document found")
        return
    
    try:
        # Start a transaction
        with Transaction(doc, "Refresh All Views") as t:
            t.Start()
            
            # Get all views (excluding templates)
            collector = FilteredElementCollector(doc).OfClass(View)
            views = [v for v in collector if not v.IsTemplate]
            
            refreshed_count = 0
            skipped_count = 0
            
            for view in views:
                try:
                    # Skip certain view types that can't be refreshed
                    if isinstance(view, (ViewSchedule, ViewSheet)):
                        skipped_count += 1
                        continue
                    
                    # Refresh the view
                    view.Regenerate()
                    refreshed_count += 1
                    
                except Exception as view_error:
                    # Some views might not be refreshable
                    skipped_count += 1
                    continue
            
            t.Commit()
            
            # Show results
            message = f"View Refresh Complete!\n\n"
            message += f"Refreshed: {refreshed_count} views\n"
            message += f"Skipped: {skipped_count} views\n"
            message += f"Total: {len(views)} views processed"
            
            TaskDialog.Show("Refresh Views Results", message)
            
    except Exception as e:
        TaskDialog.Show("Error", f"Failed to refresh views:\n{str(e)}")

if __name__ == "__main__":
    main()
