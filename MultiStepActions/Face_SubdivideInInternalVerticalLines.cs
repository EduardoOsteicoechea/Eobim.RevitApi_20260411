using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public class Face_SubdivideInInternalVerticalLines(Document doc, string workflowName)
    :
    MultistepObservableAction<Face_SubdivideInInternalVerticalLinesDto, List<Line>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        //_dto.FaceToSubdivide = args[0] as Face;
        //_dto.SubdivisionSeparation = (double)args[1];
    }

    protected override void SetActions()
    {
        //Add(GetAllFaceLines);
        //Add(GenerateEnclosingOrthogonalSquare);
        //Add(SubdivideEnclosingOrthogonalSquareTopLineBySubdivisionSeparation);
        //Add(GenerateVerticalLinesFromEnclosingOrthogonalSquareTopLineSubdivisionsToEnclosingOrthogonalSquareBottomLine);
        //Add(IntersectFullVerticalSubdivisoryLinesWithFaceOuterBoundaryLines);
        //Add(FilterFaceOuterBoundaryInternalSegments);
        Add(SetResult);
    }

    //public void GetAllFaceLines(List<string> _telemetry)
    //{
    //}

    //public void GenerateEnclosingOrthogonalSquare(List<string> _telemetry)
    //{
    //}

    //public void SubdivideEnclosingOrthogonalSquareTopLineBySubdivisionSeparation(List<string> _telemetry)
    //{
    //}

    //public void GenerateVerticalLinesFromEnclosingOrthogonalSquareTopLineSubdivisionsToEnclosingOrthogonalSquareBottomLine(List<string> _telemetry)
    //{
    //}

    //public void IntersectFullVerticalSubdivisoryLinesWithFaceOuterBoundaryLines(List<string> _telemetry)
    //{
    //}

    //public void FilterFaceOuterBoundaryInternalSegments(List<string> _telemetry)
    //{
    //}

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.SubdivisoryLines;
    }
}

public class Face_SubdivideInInternalVerticalLinesDto : Dto
{
    //[Print(nameof(TypeFormatter.Face))] // OR [JsonIgnore]
    //public Face FaceToSubdivide { get; set; }


    //[Print(nameof(TypeFormatter.Double))]
    //public double SubdivisionSeparation { get; set; }



    //[Print(nameof(TypeFormatter.CurveList))]
    //public List<Curve> FaceBoundaryCurves { get; set; }


    //[Print(nameof(TypeFormatter.Double))]
    //public double MinX { get; set; }


    //[Print(nameof(TypeFormatter.Double))]
    //public double MaxX { get; set; }


    //[Print(nameof(TypeFormatter.Double))]
    //public double MinY { get; set; }


    //[Print(nameof(TypeFormatter.Double))]
    //public double MaxY { get; set; }


    //[Print(nameof(TypeFormatter.Double))]
    //public double ZLevel { get; set; }


    //[Print(nameof(TypeFormatter.XYZList))]
    //public List<XYZ> TopLineSubdivisionPoints { get; set; }


    //[Print(nameof(TypeFormatter.LineList))]
    //public List<Line> FullVerticalLines { get; set; }

    //public Dictionary<Line, List<XYZ>> VerticalLinesIntersections { get; set; }


    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> SubdivisoryLines { get; set; }
}