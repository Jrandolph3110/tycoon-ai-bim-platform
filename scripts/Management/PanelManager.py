# Capability: P3-Generative
# Description: AI-powered panel management and optimization for FLC workflows
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# Category: Management

"""
🔴 P3-GENERATIVE: Panel Manager
AI generates optimal panel configurations, manages BIMSF_Id relationships,
and creates intelligent panel numbering for FLC steel framing workflows.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from System.Collections.Generic import List
import re

def manage_panels():
    """AI-powered panel management and optimization"""
    doc = __revit__.ActiveUIDocument.Document
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    if not selected_ids:
        TaskDialog.Show("Panel Manager", "Please select walls or panels for management")
        return
    
    # AI Analysis Phase
    walls = get_walls_from_selection(doc, selected_ids)
    if not walls:
        TaskDialog.Show("Panel Manager", "No walls found in selection")
        return
    
    # Generate AI-powered panel strategy
    panel_strategy = generate_panel_strategy(walls)
    
    # Execute panel management
    with Transaction(doc, "AI Panel Management") as t:
        t.Start()
        
        results = execute_panel_strategy(doc, walls, panel_strategy)
        
        t.Commit()
    
    # Report results
    report_panel_results(results, panel_strategy)

def get_walls_from_selection(doc, selected_ids):
    """Extract walls from selection"""
    walls = []
    for element_id in selected_ids:
        element = doc.GetElement(element_id)
        if element and element.Category and element.Category.Name == "Walls":
            walls.append(element)
    return walls

def generate_panel_strategy(walls):
    """AI generates optimal panel management strategy"""
    strategy = {
        'total_walls': len(walls),
        'panel_assignments': [],
        'optimization_notes': []
    }
    
    # AI Analysis: Group walls by characteristics
    wall_groups = {}
    
    for i, wall in enumerate(walls):
        # Analyze wall characteristics
        wall_type = wall.WallType.Name
        length = get_wall_length(wall)
        height = get_wall_height(wall)
        
        # AI grouping logic
        group_key = f"{wall_type}_{int(length/5)*5}_{int(height)}"  # Group by type, length (5ft increments), height
        
        if group_key not in wall_groups:
            wall_groups[group_key] = []
        wall_groups[group_key].append({'wall': wall, 'index': i})
    
    # AI Panel Assignment Strategy
    panel_counter = 1
    
    for group_key, group_walls in wall_groups.items():
        # AI determines optimal panel configuration for this group
        if len(group_walls) == 1:
            # Single wall - main panel
            panel_id = f"01-{panel_counter:04d}"
            strategy['panel_assignments'].append({
                'wall': group_walls[0]['wall'],
                'panel_id': panel_id,
                'panel_type': 'main',
                'bimsf_container': panel_id
            })
            strategy['optimization_notes'].append(f"Panel {panel_id}: Single wall optimization")
        else:
            # Multiple similar walls - create main + sub panels
            main_panel_id = f"01-{panel_counter:04d}"
            
            for j, wall_info in enumerate(group_walls):
                if j == 0:
                    # First wall is main panel
                    strategy['panel_assignments'].append({
                        'wall': wall_info['wall'],
                        'panel_id': main_panel_id,
                        'panel_type': 'main',
                        'bimsf_container': main_panel_id
                    })
                else:
                    # Subsequent walls are sub-panels
                    sub_panel_id = f"{main_panel_id}-{j}"
                    strategy['panel_assignments'].append({
                        'wall': wall_info['wall'],
                        'panel_id': sub_panel_id,
                        'panel_type': 'sub',
                        'bimsf_container': main_panel_id,
                        'bimsf_id': f"Py-{sub_panel_id}"
                    })
            
            strategy['optimization_notes'].append(f"Panel Group {main_panel_id}: {len(group_walls)} similar walls optimized")
        
        panel_counter += 1
    
    return strategy

def execute_panel_strategy(doc, walls, strategy):
    """Execute the AI-generated panel strategy"""
    results = {'success': 0, 'errors': []}
    
    for assignment in strategy['panel_assignments']:
        try:
            wall = assignment['wall']
            
            # Set BIMSF_Container parameter
            container_param = wall.LookupParameter("BIMSF_Container")
            if container_param and not container_param.IsReadOnly:
                container_param.Set(assignment['bimsf_container'])
            
            # Set BIMSF_Id parameter for sub-panels
            if assignment['panel_type'] == 'sub' and 'bimsf_id' in assignment:
                bimsf_param = wall.LookupParameter("BIMSF_Id")
                if bimsf_param and not bimsf_param.IsReadOnly:
                    bimsf_param.Set(assignment['bimsf_id'])
            
            # Set Main Panel parameter
            main_panel_param = wall.LookupParameter("Main Panel")
            if main_panel_param and not main_panel_param.IsReadOnly:
                main_panel_value = 1 if assignment['panel_type'] == 'main' else 0
                main_panel_param.Set(main_panel_value)
            
            results['success'] += 1
            
        except Exception as e:
            results['errors'].append(f"Wall {assignment['panel_id']}: {str(e)}")
    
    return results

def get_wall_length(wall):
    """Get wall length"""
    location_curve = wall.Location
    if hasattr(location_curve, 'Curve'):
        return location_curve.Curve.Length
    return 0

def get_wall_height(wall):
    """Get wall height"""
    height_param = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)
    if height_param:
        return height_param.AsDouble()
    return 0

def report_panel_results(results, strategy):
    """Report AI panel management results"""
    message = "🤖 AI PANEL MANAGEMENT COMPLETE\n\n"
    message += f"✅ Successfully processed: {results['success']} walls\n"
    message += f"📊 Total panels created: {len(strategy['panel_assignments'])}\n\n"
    
    if results['errors']:
        message += f"⚠️ Errors: {len(results['errors'])}\n"
        for error in results['errors'][:3]:  # Show first 3 errors
            message += f"  • {error}\n"
    
    message += "\n🎯 AI OPTIMIZATION NOTES:\n"
    for note in strategy['optimization_notes']:
        message += f"  • {note}\n"
    
    TaskDialog.Show("AI Panel Management Results", message)

if __name__ == "__main__":
    manage_panels()
