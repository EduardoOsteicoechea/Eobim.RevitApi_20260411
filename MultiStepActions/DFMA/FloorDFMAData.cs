using Autodesk.Revit.DB;

namespace Eobim.RevitApi.DFMA;

public class FloorDFMAData
{
    public ElementId Id { get; set; }
    public string Name { get; set; }
    public double Area { get; set; }
    public double Volume { get; set; }
    public Face TopFace { get; set; }
    public Face BottomFace { get; set; }
    public XYZ TopFaceHighestPoint { get; set; }
    public XYZ BottomFaceLowestPoint { get; set; }
    public double Thickness { get; set; }
    public CurveLoop TopFaceOuterCurveLoop { get; set; }
    public CurveLoop BottomFaceOuterCurveLoop { get; set; }
}
