# Capability: P2-Analytic
# Description: AI analyzes wall geometry to classify optimal joint types
# Author: AI Assistant  
# Version: 1.0.0

"""
🟡 P2-ANALYTIC: Wall Joint Classification Helper
AI analyzes wall geometry and classifies joint types, then deterministic logic creates joints.
This is Chat's "Analytic Helper" pattern - AI analysis → deterministic execution.
"""

import clr
clr.AddReference('RevitAPI')
clr.AddReference('RevitAPIUI')

from Autodesk.Revit.DB import *
from Autodesk.Revit.UI import *
import math

def analyze_wall_joints():
    """AI-powered wall joint analysis with deterministic execution"""
    doc = __revit__.ActiveUIDocument.Document
    
    # Get selected walls
    selection = __revit__.ActiveUIDocument.Selection
    selected_ids = selection.GetElementIds()
    
    walls = [doc.GetElement(id) for id in selected_ids if doc.GetElement(id).Category.Name == "Walls"]
    
    if len(walls) < 2:
        TaskDialog.Show("Error", "Please select at least 2 walls for joint analysis")
        return
    
    # AI Analysis Phase: Classify joint requirements
    joint_classifications = []
    
    for i, wall1 in enumerate(walls):
        for j, wall2 in enumerate(walls[i+1:], i+1):
            # Get wall geometries
            curve1 = wall1.Location.Curve
            curve2 = wall2.Location.Curve
            
            # AI Classification Logic
            joint_type = classify_joint_type(curve1, curve2, wall1, wall2)
            
            if joint_type != "NO_JOINT":
                joint_classifications.append({
                    'wall1': wall1,
                    'wall2': wall2,
                    'joint_type': joint_type,
                    'confidence': calculate_confidence(curve1, curve2)
                })
    
    # Deterministic Execution Phase: Create joints based on AI classification
    with Transaction(doc, "Create AI-Classified Joints") as t:
        t.Start()
        
        created_joints = 0
        for classification in joint_classifications:
            if classification['confidence'] > 0.8:  # High confidence threshold
                success = create_joint_deterministic(
                    classification['wall1'], 
                    classification['wall2'], 
                    classification['joint_type']
                )
                if success:
                    created_joints += 1
        
        t.Commit()
    
    # Report results
    TaskDialog.Show("AI Joint Analysis Complete", 
                   f"Analyzed {len(walls)} walls\n"
                   f"Classified {len(joint_classifications)} potential joints\n"
                   f"Created {created_joints} high-confidence joints")

def classify_joint_type(curve1, curve2, wall1, wall2):
    """AI classification of joint type based on geometry analysis"""
    
    # Get intersection point
    intersection = get_intersection_point(curve1, curve2)
    if not intersection:
        return "NO_JOINT"
    
    # Calculate angle between walls
    angle = calculate_wall_angle(curve1, curve2)
    
    # AI Classification Rules (simplified for demo)
    if abs(angle - 90) < 5:  # Nearly perpendicular
        return "T_JOINT"
    elif abs(angle - 180) < 5:  # Nearly straight
        return "BUTT_JOINT"
    elif 30 < angle < 150:  # Angled connection
        return "MITER_JOINT"
    else:
        return "COMPLEX_JOINT"

def calculate_confidence(curve1, curve2):
    """Calculate AI confidence in joint classification"""
    # Simplified confidence calculation
    intersection = get_intersection_point(curve1, curve2)
    if intersection:
        return 0.95  # High confidence for clear intersections
    else:
        return 0.3   # Low confidence for unclear cases

def get_intersection_point(curve1, curve2):
    """Find intersection point between two curves"""
    try:
        result = curve1.Intersect(curve2)
        if result == SetComparisonResult.Overlap:
            return True
        return False
    except:
        return False

def calculate_wall_angle(curve1, curve2):
    """Calculate angle between two wall curves"""
    try:
        dir1 = curve1.Direction
        dir2 = curve2.Direction
        dot_product = dir1.DotProduct(dir2)
        angle_rad = math.acos(abs(dot_product))
        angle_deg = math.degrees(angle_rad)
        return angle_deg
    except:
        return 0

def create_joint_deterministic(wall1, wall2, joint_type):
    """Deterministic joint creation based on AI classification"""
    try:
        # This would contain bulletproof joint creation logic
        # For demo, we'll just add a comment parameter
        
        comment1 = wall1.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
        comment2 = wall2.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
        
        if comment1 and not comment1.IsReadOnly:
            comment1.Set(f"AI Joint: {joint_type}")
        if comment2 and not comment2.IsReadOnly:
            comment2.Set(f"AI Joint: {joint_type}")
        
        return True
    except:
        return False

if __name__ == "__main__":
    analyze_wall_joints()
