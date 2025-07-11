# -*- coding: utf-8 -*-
"""
Selection Helper - GitHub Script
Advanced selection utilities for BIM workflows

@capability: P1-Deterministic
@category: Utilities
@author: Tycoon AI-BIM Platform
@version: 1.0.0
@description: Advanced selection utilities for BIM workflows
@selection: optional
"""

__title__ = "Selection Helper"
__author__ = "Tycoon AI-BIM Platform"
__version__ = "1.0.0"

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from pyrevit import revit, DB, UI
from pyrevit import script, forms

def select_by_category():
    """Select all elements of a specific category"""
    
    # Get all categories in the model
    categories = []
    collector = FilteredElementCollector(revit.doc)
    elements = collector.WhereElementIsNotElementType().ToElements()
    
    for elem in elements:
        if elem.Category and elem.Category.Name not in categories:
            categories.append(elem.Category.Name)
    
    categories.sort()
    
    # Show selection dialog
    selected_category = forms.SelectFromList.show(
        categories,
        title="Select Category",
        multiselect=False
    )
    
    if selected_category:
        # Select all elements of this category
        category_filter = ElementCategoryFilter(
            [cat for cat in revit.doc.Settings.Categories 
             if cat.Name == selected_category][0].Id
        )
        
        collector = FilteredElementCollector(revit.doc)
        elements = collector.WherePasses(category_filter).WhereElementIsNotElementType().ToElements()
        
        # Set selection
        element_ids = [elem.Id for elem in elements]
        revit.get_selection().set_to(element_ids)
        
        UI.TaskDialog.Show(
            "Selection Helper",
            f"Selected {len(elements)} elements of category '{selected_category}'"
        )

def select_by_level():
    """Select all elements on a specific level"""
    
    # Get all levels
    levels = FilteredElementCollector(revit.doc).OfClass(Level).ToElements()
    level_names = [level.Name for level in levels]
    level_names.sort()
    
    # Show selection dialog
    selected_level = forms.SelectFromList.show(
        level_names,
        title="Select Level",
        multiselect=False
    )
    
    if selected_level:
        # Find the level element
        level_elem = next(level for level in levels if level.Name == selected_level)
        
        # Get all elements on this level
        level_filter = ElementLevelFilter(level_elem.Id)
        collector = FilteredElementCollector(revit.doc)
        elements = collector.WherePasses(level_filter).WhereElementIsNotElementType().ToElements()
        
        # Set selection
        element_ids = [elem.Id for elem in elements]
        revit.get_selection().set_to(element_ids)
        
        UI.TaskDialog.Show(
            "Selection Helper",
            f"Selected {len(elements)} elements on level '{selected_level}'"
        )

def select_similar_elements():
    """Select elements similar to current selection"""
    
    selection = revit.get_selection()
    
    if not selection:
        UI.TaskDialog.Show("Selection Helper", "Please select an element first.")
        return
    
    if len(selection) > 1:
        UI.TaskDialog.Show("Selection Helper", "Please select only one element to find similar.")
        return
    
    reference_elem = selection[0]
    
    # Find similar elements based on type
    if hasattr(reference_elem, 'GetTypeId'):
        type_id = reference_elem.GetTypeId()
        
        # Find all elements of the same type
        collector = FilteredElementCollector(revit.doc)
        similar_elements = []
        
        for elem in collector.WhereElementIsNotElementType().ToElements():
            if hasattr(elem, 'GetTypeId') and elem.GetTypeId() == type_id:
                similar_elements.append(elem)
        
        # Set selection
        element_ids = [elem.Id for elem in similar_elements]
        revit.get_selection().set_to(element_ids)
        
        UI.TaskDialog.Show(
            "Selection Helper",
            f"Selected {len(similar_elements)} similar elements"
        )
    else:
        UI.TaskDialog.Show("Selection Helper", "Cannot find similar elements for this type.")

def clear_selection():
    """Clear current selection"""
    revit.get_selection().clear()
    UI.TaskDialog.Show("Selection Helper", "Selection cleared.")

def main():
    """Main selection helper menu"""
    
    # Show options dialog
    options = [
        "Select by Category",
        "Select by Level", 
        "Select Similar Elements",
        "Clear Selection"
    ]
    
    selected_option = forms.SelectFromList.show(
        options,
        title="Selection Helper - Choose Action",
        multiselect=False
    )
    
    if selected_option == "Select by Category":
        select_by_category()
    elif selected_option == "Select by Level":
        select_by_level()
    elif selected_option == "Select Similar Elements":
        select_similar_elements()
    elif selected_option == "Clear Selection":
        clear_selection()

if __name__ == "__main__":
    main()
