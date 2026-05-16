using Autodesk.Revit.DB;

namespace Eobim.RevitApi.MultiStepActions;

public class DirectShapeDMFAData
{
    public List<Line> BoundaryLines { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public DirectShape DirectShape { get; set; }
    public Face DirectBottomFace { get; set; }
    public Line PrintFaceLine { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public Face DirectShapeLeadFace { get; set; }
    public Reference DirectShapeLeadFaceReference { get; set; }

    // From here bellow, these properties must be filled in the 
    // Dynamic placement algorithm
    public XYZ PlacementPoint { get; set; }
    public double RequiredXDisplacement { get; set; }
    public double RequiredYDisplacement { get; set; }
    public double RequiredZDisplacement { get; set; }
    public CurveLoop DisplacedCurveLoop { get; set; }
    public double AngleToXYZBasisZ { get; set; }
}
