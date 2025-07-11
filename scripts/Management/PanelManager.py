# -*- coding: utf-8 -*-
"""
Panel Manager - GitHub Script
AI-powered panel management and optimization for FLC workflows

@capability: P3-Generative
@category: Management
@author: Tycoon AI-BIM Platform
@version: 1.0.0
@description: AI-powered panel management and optimization for FLC workflows
@selection: required
"""

__title__ = "Panel Manager"
__author__ = "Tycoon AI-BIM Platform"
__version__ = "1.0.0"

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
from pyrevit import revit, DB, UI
from pyrevit import script

def get_panel_elements(selection):
    """Extract panel-related elements from selection"""
    
    panels = []
    studs = []
    tracks = []
    other = []
    
    for elem in selection:
        if hasattr(elem, 'Category') and elem.Category:
            cat_name = elem.Category.Name
            
            # Classify elements by category
            if 'Wall' in cat_name:
                panels.append(elem)
            elif 'Structural Framing' in cat_name:
                # Check if it's a stud or track based on name/type
                elem_name = elem.Name.lower() if hasattr(elem, 'Name') else ""
                if 'stud' in elem_name:
                    studs.append(elem)
                elif 'track' in elem_name:
                    tracks.append(elem)
                else:
                    other.append(elem)
            else:
                other.append(elem)
    
    return {
        'panels': panels,
        'studs': studs,
        'tracks': tracks,
        'other': other
    }

def analyze_panel_efficiency(panel_data):
    """Analyze panel layout efficiency"""
    
    analysis = {
        'total_panels': len(panel_data['panels']),
        'total_studs': len(panel_data['studs']),
        'total_tracks': len(panel_data['tracks']),
        'stud_spacing_issues': [],
        'panel_size_recommendations': [],
        'efficiency_score': 0
    }
    
    # Basic efficiency calculations
    if analysis['total_panels'] > 0:
        studs_per_panel = analysis['total_studs'] / analysis['total_panels']
        
        # Ideal stud spacing analysis (simplified)
        if studs_per_panel < 3:
            analysis['stud_spacing_issues'].append("Low stud density - consider structural requirements")
        elif studs_per_panel > 8:
            analysis['stud_spacing_issues'].append("High stud density - potential material waste")
        
        # Calculate basic efficiency score
        base_score = 70
        if 3 <= studs_per_panel <= 6:
            base_score += 20
        if analysis['total_tracks'] >= analysis['total_panels'] * 2:  # Top and bottom tracks
            base_score += 10
        
        analysis['efficiency_score'] = min(100, base_score)
    
    return analysis

def generate_recommendations(analysis, panel_data):
    """Generate AI-powered recommendations"""
    
    recommendations = []
    
    # Panel count recommendations
    if analysis['total_panels'] == 0:
        recommendations.append("⚠️ No panels detected in selection")
    elif analysis['total_panels'] > 10:
        recommendations.append("📊 Large panel count - consider breaking into phases")
    
    # Stud recommendations
    if analysis['total_studs'] == 0:
        recommendations.append("⚠️ No studs detected - verify framing elements")
    else:
        avg_studs = analysis['total_studs'] / max(1, analysis['total_panels'])
        if avg_studs < 3:
            recommendations.append("🔧 Consider adding studs for structural integrity")
        elif avg_studs > 7:
            recommendations.append("💰 Potential material savings with optimized stud spacing")
    
    # Track recommendations
    expected_tracks = analysis['total_panels'] * 2  # Top and bottom
    if analysis['total_tracks'] < expected_tracks:
        recommendations.append("🔧 Missing tracks detected - verify top/bottom tracks")
    
    # Efficiency recommendations
    if analysis['efficiency_score'] < 60:
        recommendations.append("📈 Panel efficiency below optimal - review layout")
    elif analysis['efficiency_score'] > 85:
        recommendations.append("✅ Excellent panel efficiency!")
    
    # FLC-specific recommendations
    recommendations.append("📋 Verify BIMSF_Id parameters for panel tracking")
    recommendations.append("🏗️ Check panel sequencing for fabrication workflow")
    
    return recommendations

def main():
    """Main panel management function"""
    
    # Get current selection
    selection = revit.get_selection()
    
    if not selection:
        UI.TaskDialog.Show("Panel Manager", "Please select panel elements to analyze.")
        return
    
    # Classify elements
    panel_data = get_panel_elements(selection)
    
    # Analyze efficiency
    analysis = analyze_panel_efficiency(panel_data)
    
    # Generate recommendations
    recommendations = generate_recommendations(analysis, panel_data)
    
    # Create output
    output = script.get_output()
    output.print_md("# Panel Management Report")
    output.print_md("*AI-Powered Analysis for FLC Workflows*")
    output.print_md("---")
    
    # Element summary
    output.print_md("## Element Summary")
    output.print_md(f"- **Panels:** {analysis['total_panels']}")
    output.print_md(f"- **Studs:** {analysis['total_studs']}")
    output.print_md(f"- **Tracks:** {analysis['total_tracks']}")
    output.print_md(f"- **Other Elements:** {len(panel_data['other'])}")
    
    # Efficiency analysis
    output.print_md("## Efficiency Analysis")
    output.print_md(f"**Overall Score:** {analysis['efficiency_score']}/100")
    
    if analysis['stud_spacing_issues']:
        output.print_md("### Spacing Issues")
        for issue in analysis['stud_spacing_issues']:
            output.print_md(f"- {issue}")
    
    # AI Recommendations
    output.print_md("## AI Recommendations")
    for rec in recommendations:
        output.print_md(f"- {rec}")
    
    output.print_md("---")
    output.print_md("*Generated by Tycoon AI-BIM Platform*")

if __name__ == "__main__":
    main()
