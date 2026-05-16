

using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public class LineList_GenerateCurveLoopsFromLines
(
    Document doc,
    string parentCommandName
)
:
MultistepObservableAction<LineList_GenerateCurveLoopsFromLinesDto, List<(List<Line>, string)>>
(
    doc,
    parentCommandName
)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.InputLines = args[0] as List<Line>;
        _dto.ContourThickness = (double)args[1];
        _dto.ContourDirection = args[2] as string;
        _dto.VerticalContourHeight = args[3] is null ? 0.0 : (double)args[3];
    }

    protected override void SetActions()
    {
        Add(GenerateCurveLoopsFromOrderedOffsetLines);
        Add(SetResult);
    }

    public void GenerateCurveLoopsFromOrderedOffsetLines(List<string> telemetry)
    {
        var result = new List<(List<Line>, string)>();
        var displacement = _dto.ContourThickness / 2;

        foreach (var line in _dto.InputLines)
        {
            var lineContour = new List<Line>();

            var linePerpendicularDirection = line.Direction.CrossProduct(XYZ.BasisZ).Normalize();

            var lineP1 = line.GetEndPoint(0);
            var lineP2 = line.GetEndPoint(1);

            XYZ pA = null;
            XYZ pB = null;
            XYZ pC = null;
            XYZ pD = null;

            // Define the 4 corners of the offset rectangle 
            if (_dto.ContourDirection.Equals("vertical"))
            {
                //var displacedLineP1 = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
                //var displacedLineP2 = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);

                //var displacedLine = Line.CreateBound(displacedLineP1, displacedLineP2);

                //var displacedTopLineP1 = new XYZ(displacedLineP1.X, displacedLineP1.Y, displacedLineP1.Z + _dto.VerticalContourHeight);
                //var displacedTopLineP2 = new XYZ(displacedLineP2.X, displacedLineP2.Y, displacedLineP2.Z + _dto.VerticalContourHeight);

                //var displacedTopLine = Line.CreateBound(displacedTopLineP1, displacedTopLineP2);

                //lineContour.Add(displacedLine);
                //lineContour.Add(Line.CreateBound(displacedLineP1, displacedTopLineP1));
                //lineContour.Add(displacedTopLine);
                //lineContour.Add(Line.CreateBound(displacedLineP2, displacedTopLineP2));
                var displacedLineP1 = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
                var displacedLineP2 = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);

                var displacedTopLineP1 = new XYZ(displacedLineP1.X, displacedLineP1.Y, displacedLineP1.Z + _dto.VerticalContourHeight);
                var displacedTopLineP2 = new XYZ(displacedLineP2.X, displacedLineP2.Y, displacedLineP2.Z + _dto.VerticalContourHeight);

                // 1. Bottom edge: P1 -> P2
                var bottomEdge = Line.CreateBound(displacedLineP1, displacedLineP2);

                // 2. Right vertical edge: P2 -> TopP2 (Connects to end of bottom edge)
                var rightEdge = Line.CreateBound(displacedLineP2, displacedTopLineP2);

                // 3. Top edge: TopP2 -> TopP1 (Connects to end of right edge, draws backwards)
                var topEdge = Line.CreateBound(displacedTopLineP2, displacedTopLineP1);

                // 4. Left vertical edge: TopP1 -> P1 (Connects to end of top edge, closes back to start)
                var leftEdge = Line.CreateBound(displacedTopLineP1, displacedLineP1);

                // Add them in the exact order they were drawn
                lineContour.Add(bottomEdge);
                lineContour.Add(rightEdge);
                lineContour.Add(topEdge);
                lineContour.Add(leftEdge);
            }
            else
            {
                pA = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) - (linePerpendicularDirection * displacement);
                pB = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) - (linePerpendicularDirection * displacement);
                pC = new XYZ(lineP2.X, lineP2.Y, lineP2.Z) + (linePerpendicularDirection * displacement);
                pD = new XYZ(lineP1.X, lineP1.Y, lineP1.Z) + (linePerpendicularDirection * displacement);

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
            }

            result.Add((lineContour, _dto.ContourDirection));
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


    [Print(nameof(TypeFormatter.String))]
    public string ContourDirection { get; set; }


    [Print(nameof(TypeFormatter.Double))]
    public double VerticalContourHeight { get; set; }


    [Print(nameof(TypeFormatter.LineListList))]
    public List<(List<Line>, string)> Contours { get; set; }
}
