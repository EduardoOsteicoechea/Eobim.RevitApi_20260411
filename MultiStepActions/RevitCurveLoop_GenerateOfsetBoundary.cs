using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

internal class CurveLoop_GenerateInnerOffsetBoundary
(
    Document doc,
    string parentCommandName
)
: MultistepObservableAction<RevitCurveLoop_GenerateInnerOffsetBoundaryDto, List<Line>>
(
    doc,
    parentCommandName
)
{

    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.CurveLoop = args[0] as CurveLoop;
        _dto.Offset = (double)args[1];
    }

    protected override void SetActions()
    {
        Add(ExtractCurveLoopOrderedCurves);
        Add(GetCurveLoopLines);
        Add(GenerateOffsetLines);
        // Split into two granular steps for better observability
        Add(ExtractExactOffsetVertices);
        Add(GenerateExactOffsetLines);
    }

    public void ExtractCurveLoopOrderedCurves(List<string> _tracing)
    {
        _dto.CurveLoopOrderedCurves = _dto.CurveLoop.ToList();
    }

    public void GetCurveLoopLines(List<string> _tracing)
    {
        var result = new List<Line>();

        foreach (var curveLoopOrderedCurve in _dto.CurveLoopOrderedCurves)
        {
            var curvePoints = curveLoopOrderedCurve is Line
                ? [curveLoopOrderedCurve.GetEndPoint(0), curveLoopOrderedCurve.GetEndPoint(1)]
                : curveLoopOrderedCurve.Tessellate().ToList();

            if (curvePoints.Count < 2) continue;

            for (int x = 0; x < curvePoints.Count - 1; x++)
            {
                var currentPoint = curvePoints[x];
                var nextPoint = curvePoints[x + 1];

                if (!currentPoint.IsAlmostEqualTo(nextPoint))
                {
                    var line = Line.CreateBound(currentPoint, nextPoint);
                    result.Add(line);
                }
            }
        }

        if (result is null) throw new ArgumentNullException(nameof(result));

        _dto.CurveLoopLines = result;
    }

    public void GenerateOffsetLines(List<string> _tracing)
    {
        var result = new List<Line>();

        foreach (var curveLoopLine in _dto.CurveLoopLines)
        {
            var p1 = curveLoopLine.GetEndPoint(0) + (curveLoopLine.Direction.Negate() * 1);
            var p2 = curveLoopLine.GetEndPoint(1) + (curveLoopLine.Direction * 1);

            var offsetDirection = curveLoopLine.Direction.CrossProduct(XYZ.BasisZ).Negate();

            var displacedP1 = p1 + (offsetDirection * _dto.Offset);
            var displacedP2 = p2 + (offsetDirection * _dto.Offset);

            var displacedLine = Line.CreateBound(displacedP1, displacedP2);

            result.Add(displacedLine);
        }

        if (result is null) throw new ArgumentNullException(nameof(result));

        _dto.OffsetLines = result;
    }

    public void ExtractExactOffsetVertices(List<string> _tracing)
    {
        var offsetLines = _dto.OffsetLines;
        int count = offsetLines.Count;

        if (count < 3) throw new InvalidOperationException("Cannot form a closed loop with less than 3 lines.");

        var exactVertices = new List<XYZ>();

        for (int i = 0; i < count; i++)
        {
            var currentLine = offsetLines[i];

            // Modulo math safely wraps the first index back to the last index
            var previousLine = offsetLines[(i - 1 + count) % count];

            var unboundCurrent = (Line)currentLine.Clone();
            unboundCurrent.MakeUnbound();

            var unboundPrev = (Line)previousLine.Clone();
            unboundPrev.MakeUnbound();

            var intersectResult = unboundPrev.Intersect(unboundCurrent, CurveIntersectResultOption.Detailed);

            if (intersectResult.Result == SetComparisonResult.Overlap)
            {
                var overlaps = intersectResult.GetOverlaps();

                if (overlaps != null && overlaps.Count > 0)
                {
                    XYZ intersectionPoint = overlaps[0].Point;
                    exactVertices.Add(intersectionPoint);
                }
                else
                {
                    throw new InvalidOperationException($"Overlap detected but no points returned at index {i}.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Failed to find intersection between offset lines at index {i}.");
            }
        }

        _dto.ExactOffsetVertices = exactVertices;
    }

    public void GenerateExactOffsetLines(List<string> _tracing)
    {
        var result = new List<Line>();
        var exactVertices = _dto.ExactOffsetVertices;

        if (exactVertices == null || exactVertices.Count < 3)
        {
            throw new InvalidOperationException("Exact vertices were not properly generated.");
        }

        for (int i = 0; i < exactVertices.Count; i++)
        {
            var startPoint = exactVertices[i];
            var endPoint = exactVertices[(i + 1) % exactVertices.Count];

            if (!startPoint.IsAlmostEqualTo(endPoint))
            {
                var exactLine = Line.CreateBound(startPoint, endPoint);
                result.Add(exactLine);
            }
        }

        if (result.Count == 0) throw new ArgumentNullException(nameof(result), "Exact offset line generation failed.");

        _dto.ExactOffsetLines = result;
        Result = _dto.ExactOffsetLines;
    }
}

public class RevitCurveLoop_GenerateInnerOffsetBoundaryDto : IDto
{
    [Print(nameof(TypeFormatter.CurveLoop))]
    public CurveLoop CurveLoop { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double Offset { get; set; }

    [Print(nameof(TypeFormatter.CurveList))]
    public List<Curve> CurveLoopOrderedCurves { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> CurveLoopLines { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> OffsetLines { get; set; }

    [Print(nameof(TypeFormatter.XYZList))]
    public List<XYZ> ExactOffsetVertices { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> ExactOffsetLines { get; set; }

    public List<(string, object)> ToObservableObject()
    {
        return DtoFormatter.FormatAsObject(this);
    }
}