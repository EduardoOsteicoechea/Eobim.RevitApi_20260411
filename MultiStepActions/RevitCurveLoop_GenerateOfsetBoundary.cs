using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public record CurveLoop_GenerateInnerOffsetBoundaryArgs(
    CurveLoop CurveLoop,
    double Offset,
    double HeightAdjustment,
    XYZ FaceDirection
    );

public class CurveLoop_GenerateInnerOffsetBoundary : MultistepObservableAction<CurveLoop_GenerateInnerOffsetBoundaryArgs, RevitCurveLoop_GenerateInnerOffsetBoundaryDto, List<Line>>
{
    public override void SafelyInitializeInputs(CurveLoop_GenerateInnerOffsetBoundaryArgs args)
    {
        _dto.CurveLoop = args.CurveLoop;
        _dto.Offset = args.Offset;
        _dto.HeightAdjustment = args.HeightAdjustment;
        _dto.FaceDirection = args.FaceDirection;
    }

    protected override void SetActions()
    {
        Add(ExtractCurveLoopOrderedCurves);
        Add(GetCurveLoopLines);
        Add(AdjustLinesZCoordinate);
        Add(GenerateOffsetLines);
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

    public void AdjustLinesZCoordinate(List<string> _tracing)
    {
        var result = new List<Line>();

        foreach (var item in _dto.CurveLoopLines)
        {
            var p1 = item.GetEndPoint(0);
            var p2 = item.GetEndPoint(1);
            result.Add(Line.CreateBound(
                new XYZ(p1.X, p1.Y, p1.Z + _dto.HeightAdjustment),
                new XYZ(p2.X, p2.Y, p2.Z + _dto.HeightAdjustment)
                ));
        }

        if (result is null) throw new ArgumentNullException();

        _dto.ZAdjustedCurveLoopLines = result;
    }

    //public void GenerateOffsetLines(List<string> _tracing)
    //{
    //    var result = new List<Line>();

    //    foreach (var curveLoopLine in _dto.ZAdjustedCurveLoopLines)
    //    {
    //        var p1 = curveLoopLine.GetEndPoint(0) + (curveLoopLine.Direction.Negate() * 1);
    //        var p2 = curveLoopLine.GetEndPoint(1) + (curveLoopLine.Direction * 1); 

    //        var offsetDirection = curveLoopLine.Direction.CrossProduct(XYZ.BasisZ) * (_dto.FaceDirection.IsAlmostEqualTo(XYZ.BasisZ) ? -1 : 1);

    //        var displacedP1 = p1 + (offsetDirection * _dto.Offset);
    //        var displacedP2 = p2 + (offsetDirection * _dto.Offset);

    //        var displacedLine = Line.CreateBound(displacedP1, displacedP2);

    //        result.Add(displacedLine);
    //    }

    //    if (result is null) throw new ArgumentNullException(nameof(result));

    //    _dto.OffsetLines = result;
    //}

    public void GenerateOffsetLines(List<string> _tracing)
    {
        var result = new List<Line>();

        // 1. Ask the CurveLoop itself how it is winding relative to the global UP direction.
        // This abstracts away whether the loop came from a top face or bottom face.
        bool isCCW = _dto.CurveLoop.IsCounterclockwise(XYZ.BasisZ);

        // 2. Set the multiplier based solely on the intrinsic winding order.
        // A CrossProduct with BasisZ ALWAYS points to the RIGHT of the curve's direction.
        // - For CCW loops, RIGHT is OUTSIDE. To go INSIDE, we must invert it (-1).
        // - For CW loops, RIGHT is INSIDE. We leave it as is (1).
        int offsetMultiplier = isCCW ? -1 : 1;

        _tracing.Add($"CurveLoop IsCCW: {isCCW}. Using Offset Multiplier: {offsetMultiplier}");

        foreach (var curveLoopLine in _dto.ZAdjustedCurveLoopLines)
        {
            // Extend the lines slightly to ensure clean intersections later
            var p1 = curveLoopLine.GetEndPoint(0) + (curveLoopLine.Direction.Negate() * 1);
            var p2 = curveLoopLine.GetEndPoint(1) + (curveLoopLine.Direction * 1);

            // Generate the perpendicular vector (always points right)
            var rightPointingVector = curveLoopLine.Direction.CrossProduct(XYZ.BasisZ).Normalize();

            // Apply the multiplier to guarantee we are pointing INSIDE the shape
            var offsetDirection = rightPointingVector * offsetMultiplier;

            var displacedP1 = p1 + (offsetDirection * _dto.Offset);
            var displacedP2 = p2 + (offsetDirection * _dto.Offset);

            var displacedLine = Line.CreateBound(displacedP1, displacedP2);

            result.Add(displacedLine);
        }

        if (result.Count == 0) throw new ArgumentNullException(nameof(result));

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
            else if (intersectResult.Result == SetComparisonResult.Subset || intersectResult.Result == SetComparisonResult.Equal)
            {
                // Collinear lines: they lie on the exact same infinite line. 
                // The logical "corner" is simply the endpoint of the previous segment.
                _tracing.Add($"Note: Lines at index {i} are collinear ({intersectResult.Result}). Using previous line endpoint.");
                exactVertices.Add(previousLine.GetEndPoint(1));
            }
            else if (intersectResult.Result == SetComparisonResult.Disjoint)
            {
                // Parallel lines: Common in tessellated splines where adjacent segments are functionally parallel.
                // Because they are parallel, they never intersect. The logical "corner" is the gap between them.
                // We average the end of the previous line and the start of the current line to bridge the gap smoothly.
                _tracing.Add($"Note: Lines at index {i} are parallel but disjoint. Bridging the gap.");
                var p1 = previousLine.GetEndPoint(1);
                var p2 = currentLine.GetEndPoint(0);
                var midpoint = (p1 + p2) / 2.0;
                exactVertices.Add(midpoint);
            }
            else
            {
                // Fallback for unexpected geometric results
                var prevDir = unboundPrev.Direction;
                var currDir = unboundCurrent.Direction;
                double angleRad = prevDir.AngleTo(currDir);

                string debugMessage =
                    $"Intersection failed at index {i}.\n" +
                    $"Comparison Result: {intersectResult.Result}\n" +
                    $"Prev Line Dir: {prevDir}\n" +
                    $"Curr Line Dir: {currDir}\n" +
                    $"Angle Between (rads): {angleRad}\n" +
                    $"Prev Endpoint 1: {previousLine.GetEndPoint(1)}\n" +
                    $"Curr Endpoint 0: {currentLine.GetEndPoint(0)}";

                _tracing.Add(debugMessage);
                throw new InvalidOperationException(debugMessage);
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

    [Print(nameof(TypeFormatter.Double))]
    public double HeightAdjustment { get; set; }

    [Print(nameof(TypeFormatter.XYZ))]
    public XYZ FaceDirection { get; set; }

    [Print(nameof(TypeFormatter.CurveList))]
    public List<Curve> CurveLoopOrderedCurves { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> CurveLoopLines { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> ZAdjustedCurveLoopLines { get; set; }

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