

using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public class LineList_GenerateCurveLoopsFromLines
(
    Document doc,
    string parentCommandName
)
:
MultistepObservableAction<LineList_GenerateCurveLoopsFromLinesDto, List<List<Line>>>
(
    doc,
    parentCommandName
)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.InputLines = args[0] as List<Line>;
        _dto.ContourThickness = (double)args[1];
    }

    protected override void SetActions()
    {
        Add(GenerateCurveLoopsFromOrderedOffsetLines);
        Add(SetResult);
    }

    //public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> telemetry) 
    //{
    //    var result = new List<List<Line>>();
    //    var displacement = _dto.ContourThickness;

    //    foreach (var line in _dto.InputLines) 
    //    {
    //        var lineContour = new List<Line>();

    //        var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

    //        var lineP1 = line.GetEndPoint(0);
    //        var lineP2 = line.GetEndPoint(1);

    //        var newLine1ParallelSide1P1 = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
    //        var newLine1ParallelSide1P2 = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);
    //        var newLine1ParallelSide1Line = Line.CreateBound(newLine1ParallelSide1P1, newLine1ParallelSide1P2);

    //        var newLine1ParallelSide2P1 = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) + (linePerpendicularDirection * displacement);
    //        var newLine1ParallelSide2P2 = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) + (linePerpendicularDirection * displacement);
    //        var newLine1ParallelSide2Line = Line.CreateBound(newLine1ParallelSide2P1, newLine1ParallelSide2P2);

    //        var newLine1PerpendicularSide1Line = Line.CreateBound(newLine1ParallelSide1Line.GetEndPoint(0), newLine1ParallelSide2Line.GetEndPoint(0));
    //        var newLine1PerpendicularSide2Line = Line.CreateBound(newLine1ParallelSide1Line.GetEndPoint(1), newLine1ParallelSide2Line.GetEndPoint(1));

    //        lineContour.Add(newLine1PerpendicularSide2Line);
    //        lineContour.Add(newLine1ParallelSide1Line);
    //        lineContour.Add(newLine1PerpendicularSide1Line);
    //        lineContour.Add(newLine1ParallelSide2Line);

    //        result.Add(lineContour);
    //    }

    //    _dto.Contours = result;
    //}

    public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> telemetry)
    {
        var result = new List<List<Line>>();
        var displacement = _dto.ContourThickness / 2;

        foreach (var line in _dto.InputLines)
        {
            var lineContour = new List<Line>();

            var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

            var lineP1 = line.GetEndPoint(0);
            var lineP2 = line.GetEndPoint(1);

            // Define the 4 corners of the offset rectangle
            var pA = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
            var pB = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);
            var pC = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) + (linePerpendicularDirection * displacement);
            var pD = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) + (linePerpendicularDirection * displacement);

            // Create the 4 lines head-to-tail: A->B, B->C, C->D, D->A
            var side1 = Line.CreateBound(pA, pB);
            var endCap1 = Line.CreateBound(pB, pC);
            var side2 = Line.CreateBound(pC, pD);
            var endCap2 = Line.CreateBound(pD, pA);

            // Add them to the contour in the exact sequence they were created
            lineContour.Add(side1);
            lineContour.Add(endCap1);
            lineContour.Add(side2);
            lineContour.Add(endCap2);

            result.Add(lineContour);
        }

        _dto.Contours = result;
    }

    public void SetResult(List<string> telemetry) 
    {
        Result = _dto.Contours;
    }
}

public class LineList_GenerateCurveLoopsFromLinesDto: Dto
{
    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> InputLines { get; set; }


    [Print(nameof(TypeFormatter.Double))]
    public double ContourThickness { get; set; }


    [Print(nameof(TypeFormatter.LineListList))]
    public List<List<Line>> Contours { get; set; }
}
