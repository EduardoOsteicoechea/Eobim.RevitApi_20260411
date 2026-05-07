using Autodesk.Revit.DB;

namespace Eobim.RevitApi.MultiStepActions;

public class DirectShapeDMFAData
{
    public List<Line> BoundaryLines { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public DirectShape DirectShape { get; set; }
    public Line PrintFaceLine { get; set; }
    public Reference PrintFaceReference { get; set; }
    public Face PrintFace { get; set; }
}
