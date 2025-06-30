# Capability: P2-Analytic
# Description: Analyze wall properties and geometry with AI-powered insights
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0
# Category: Analysis

"""
🟡 P2-ANALYTIC: Wall Analyzer
AI-powered analysis of wall properties, geometry, and structural characteristics
for FLC steel framing optimization.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
import math

def analyze_walls():
    """AI-powered wall analysis with structural insights"""
    doc = __revit__.ActiveUIDocument.Document
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    # Filter for walls only
    walls = []
    for element_id in selected_ids:
        element = doc.GetElement(element_id)
        if element and element.Category and element.Category.Name == "Walls":
            walls.append(element)
    
    if not walls:
        TaskDialog.Show("Wall Analyzer", "Please select walls for analysis")
        return
    
    # AI Analysis Phase
    analysis_results = []
    total_area = 0
    total_length = 0
    
    for wall in walls:
        wall_analysis = analyze_single_wall(wall)
        analysis_results.append(wall_analysis)
        total_area += wall_analysis['area']
        total_length += wall_analysis['length']
    
    # Generate AI insights
    insights = generate_ai_insights(analysis_results, total_area, total_length)
    
    # Build comprehensive report
    message = "🏗️ AI WALL ANALYSIS REPORT\n\n"
    message += f"Analyzed: {len(walls)} walls\n"
    message += f"Total Length: {total_length:.2f} ft\n"
    message += f"Total Area: {total_area:.2f} sq ft\n\n"
    
    message += "📊 DETAILED ANALYSIS:\n"
    for i, result in enumerate(analysis_results):
        message += f"\nWall {i+1}: {result['type_name']}\n"
        message += f"  • Length: {result['length']:.2f} ft\n"
        message += f"  • Height: {result['height']:.2f} ft\n"
        message += f"  • Thickness: {result['thickness']:.2f} in\n"
        message += f"  • Area: {result['area']:.2f} sq ft\n"
        message += f"  • FLC Classification: {result['flc_type']}\n"
    
    message += f"\n🤖 AI INSIGHTS:\n{insights}"
    
    TaskDialog.Show("AI Wall Analysis Results", message)

def analyze_single_wall(wall):
    """Analyze individual wall properties"""
    result = {}
    
    # Basic properties
    result['type_name'] = wall.WallType.Name
    result['thickness'] = wall.Width * 12  # Convert to inches
    
    # Geometry analysis
    location_curve = wall.Location
    if hasattr(location_curve, 'Curve'):
        curve = location_curve.Curve
        result['length'] = curve.Length
    else:
        result['length'] = 0
    
    # Height calculation
    result['height'] = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble()
    result['area'] = result['length'] * result['height']
    
    # FLC Classification Analysis
    wall_type_name = result['type_name'].upper()
    if 'FLC' in wall_type_name:
        if 'LB' in wall_type_name or 'LOAD' in wall_type_name:
            result['flc_type'] = 'Load Bearing'
        elif 'SW' in wall_type_name or 'SHEAR' in wall_type_name:
            result['flc_type'] = 'Shear Wall'
        elif 'EXT' in wall_type_name:
            result['flc_type'] = 'Exterior'
        elif 'INT' in wall_type_name:
            result['flc_type'] = 'Interior'
        else:
            result['flc_type'] = 'Standard FLC'
    else:
        result['flc_type'] = 'Non-FLC Wall'
    
    return result

def generate_ai_insights(analysis_results, total_area, total_length):
    """Generate AI-powered insights from wall analysis"""
    insights = []
    
    # Structural analysis
    load_bearing_count = sum(1 for r in analysis_results if 'Load' in r['flc_type'])
    if load_bearing_count > 0:
        insights.append(f"• {load_bearing_count} load-bearing walls detected - verify structural connections")
    
    # Efficiency analysis
    avg_thickness = sum(r['thickness'] for r in analysis_results) / len(analysis_results)
    if avg_thickness > 6:
        insights.append(f"• Average thickness {avg_thickness:.1f}\" - consider optimization for material efficiency")
    
    # Panel optimization
    long_walls = [r for r in analysis_results if r['length'] > 20]
    if long_walls:
        insights.append(f"• {len(long_walls)} walls >20ft - recommend panel segmentation for FrameCAD")
    
    # Area efficiency
    if total_area > 1000:
        insights.append(f"• Large wall area ({total_area:.0f} sq ft) - excellent for prefab efficiency")
    
    return "\n".join(insights) if insights else "• All walls within optimal parameters"

if __name__ == "__main__":
    analyze_walls()
