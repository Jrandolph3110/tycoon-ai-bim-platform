# -*- coding: utf-8 -*-
"""
Wall Analyzer - GitHub Script
Analyze wall properties and geometry with AI-powered insights

@capability: P2-Analytic
@category: Analysis
@author: Tycoon AI-BIM Platform
@version: 1.0.0
@description: Analyze wall properties and geometry with AI-powered insights
@selection: required
"""

__title__ = "Wall Analyzer"
__author__ = "Tycoon AI-BIM Platform"
__version__ = "1.0.0"

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from pyrevit import revit, DB, UI
from pyrevit import script

def analyze_wall(wall):
    """Analyze a single wall and return properties"""
    
    wall_data = {
        'id': wall.Id.IntegerValue,
        'name': wall.Name,
        'type': wall.WallType.Name if wall.WallType else "Unknown",
        'length': 0,
        'height': 0,
        'area': 0,
        'volume': 0,
        'level': None,
        'orientation': None
    }
    
    try:
        # Get wall dimensions
        wall_data['length'] = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble()
        wall_data['height'] = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble()
        wall_data['area'] = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble()
        wall_data['volume'] = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble()
        
        # Get level
        level_param = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)
        if level_param:
            level_id = level_param.AsElementId()
            level = revit.doc.GetElement(level_id)
            wall_data['level'] = level.Name if level else "Unknown"
        
        # Get location curve for orientation
        location = wall.Location
        if isinstance(location, LocationCurve):
            curve = location.Curve
            if hasattr(curve, 'Direction'):
                direction = curve.Direction
                # Calculate angle from X-axis
                import math
                angle = math.atan2(direction.Y, direction.X) * 180 / math.pi
                wall_data['orientation'] = f"{angle:.1f}°"
                
    except Exception as e:
        print(f"Error analyzing wall {wall.Id}: {e}")
    
    return wall_data

def main():
    """Analyze selected walls"""
    
    # Get current selection
    selection = revit.get_selection()
    
    if not selection:
        UI.TaskDialog.Show("Wall Analyzer", "Please select walls to analyze.")
        return
    
    # Filter for walls only
    walls = [elem for elem in selection if isinstance(elem, Wall)]
    
    if not walls:
        UI.TaskDialog.Show("Wall Analyzer", "No walls found in selection.")
        return
    
    # Analyze each wall
    wall_data = []
    for wall in walls:
        data = analyze_wall(wall)
        wall_data.append(data)
    
    # Create output
    output = script.get_output()
    output.print_md("# Wall Analysis Report")
    output.print_md(f"**Analyzed {len(walls)} walls**")
    output.print_md("---")
    
    # Summary statistics
    total_length = sum(w['length'] for w in wall_data)
    total_area = sum(w['area'] for w in wall_data)
    total_volume = sum(w['volume'] for w in wall_data)
    
    output.print_md("## Summary")
    output.print_md(f"- **Total Length:** {total_length:.2f} ft")
    output.print_md(f"- **Total Area:** {total_area:.2f} sq ft")
    output.print_md(f"- **Total Volume:** {total_volume:.2f} cu ft")
    
    # Wall types breakdown
    wall_types = {}
    for w in wall_data:
        wall_types[w['type']] = wall_types.get(w['type'], 0) + 1
    
    output.print_md("## Wall Types")
    for wall_type, count in sorted(wall_types.items()):
        output.print_md(f"- **{wall_type}:** {count} walls")
    
    output.print_md("---")
    
    # Detailed wall data
    output.print_md("## Detailed Analysis")
    for wall in wall_data:
        output.print_md(f"### Wall ID: {wall['id']}")
        output.print_md(f"- **Name:** {wall['name']}")
        output.print_md(f"- **Type:** {wall['type']}")
        output.print_md(f"- **Length:** {wall['length']:.2f} ft")
        output.print_md(f"- **Height:** {wall['height']:.2f} ft")
        output.print_md(f"- **Area:** {wall['area']:.2f} sq ft")
        output.print_md(f"- **Level:** {wall['level']}")
        if wall['orientation']:
            output.print_md(f"- **Orientation:** {wall['orientation']}")
        output.print_md("")

if __name__ == "__main__":
    main()
