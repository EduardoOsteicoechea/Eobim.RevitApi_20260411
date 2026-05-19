using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public record Face_SubdivideInInternalLinesArgs(Face FaceToSubdivide, double SubdivisionSeparation, SubdivisionAxis SubdivisionBasis);

public class Face_SubdivideInInternalLines(Document doc, string workflowName)
    : MultistepObservableAction<Face_SubdivideInInternalLinesArgs, Face_SubdivideInInternalLinesDto, List<Line>>(doc, workflowName)
{
    // ------------------------------------------------------------------------
    // HELPER METHODS FOR A/B ABSTRACTION
    // ------------------------------------------------------------------------
    private double GetA(XYZ p) => _dto.SubdivisionBasis == SubdivisionAxis.X ? p.X : p.Y;
    private double GetB(XYZ p) => _dto.SubdivisionBasis == SubdivisionAxis.X ? p.Y : p.X;

    private XYZ CreateXYZ(double a, double b, double z) =>
        _dto.SubdivisionBasis == SubdivisionAxis.X ? new XYZ(a, b, z) : new XYZ(b, a, z);

    public override void SafelyInitializeInputs(Face_SubdivideInInternalLinesArgs args)
    {
        _dto.FaceToSubdivide = args.FaceToSubdivide;
        _dto.SubdivisionSeparation = args.SubdivisionSeparation;
        _dto.SubdivisionBasis = args.SubdivisionBasis;
    }

    protected override void SetActions()
    {
        Add(GetFaceExternalBoundaryLines);
        Add(GenerateEnclosingOrthogonalRectangle);
        Add(SubdivideBoundaryLine);
        Add(GenerateFullLines);
        Add(IntersectLinesWithFaceBoundaryLines);
        Add(FilterFaceOuterBoundaryInternalSegments);
        Add(SetResult);
    }

    public void GetFaceExternalBoundaryLines(List<string> _telemetry)
    {
        var curveLoops = _dto.FaceToSubdivide.GetEdgesAsCurveLoops();
        var externalCurveLoop = curveLoops.FirstOrDefault(loop => loop.IsCounterclockwise(XYZ.BasisZ));

        if (externalCurveLoop == null)
        {
            _telemetry?.Add("Failed to find a counterclockwise curve loop.");
            throw new InvalidOperationException("No counterclockwise curve loop found on the given face.");
        }

        var boundaryCurves = externalCurveLoop.ToList();

        if (!boundaryCurves.Any())
        {
            throw new InvalidOperationException("The external curve loop was found but contains no curves.");
        }

        _dto.FaceBoundaryCurves = boundaryCurves;
    }

    public void GenerateEnclosingOrthogonalRectangle(List<string> _telemetry)
    {
        double minA = double.MaxValue;
        double maxA = double.MinValue;
        double minB = double.MaxValue;
        double maxB = double.MinValue;

        foreach (var curve in _dto.FaceBoundaryCurves)
        {
            for (int i = 0; i <= 1; i++)
            {
                var point = curve.GetEndPoint(i);

                double a = GetA(point);
                double b = GetB(point);

                if (a < minA) minA = a;
                if (a > maxA) maxA = a;
                if (b < minB) minB = b;
                if (b > maxB) maxB = b;
            }
        }

        _dto.MinA = minA;
        _dto.MaxA = maxA;
        _dto.MinB = minB;
        _dto.MaxB = maxB;

        _dto.ZLevel = _dto.FaceBoundaryCurves.First().GetEndPoint(0).Z;
    }

    public void SubdivideBoundaryLine(List<string> _telemetry)
    {
        if (_dto.SubdivisionSeparation <= 0)
        {
            _telemetry?.Add("SubdivisionSeparation is zero or negative. Cannot subdivide.");
            throw new InvalidOperationException("Subdivision separation must be greater than zero.");
        }

        var result = new List<XYZ>();
        double currentA = _dto.MinA + _dto.SubdivisionSeparation;

        while (currentA < _dto.MaxA)
        {
            result.Add(CreateXYZ(currentA, _dto.MaxB, _dto.ZLevel));
            currentA += _dto.SubdivisionSeparation;
        }

        _dto.SubdivisionStartPoints = result;
    }

    public void GenerateFullLines(List<string> _telemetry)
    {
        var result = new List<Line>();
        double revitMinimumLineLength = 0.001;

        foreach (var item in _dto.SubdivisionStartPoints)
        {
            double currentA = GetA(item);
            var endPoint = CreateXYZ(currentA, _dto.MinB, _dto.ZLevel);

            if (item.DistanceTo(endPoint) > revitMinimumLineLength)
            {
                result.Add(Line.CreateBound(item, endPoint));
            }
            else
            {
                _telemetry?.Add($"Skipped line at A-coordinate {currentA} - distance too short for Revit API.");
            }
        }

        _dto.FullLines = result;
    }

    public void IntersectLinesWithFaceBoundaryLines(List<string> _telemetry)
    {
        if (_dto.FullLines == null || _dto.FaceBoundaryCurves == null)
        {
            _telemetry?.Add("Missing lines or boundary curves.");
            throw new InvalidOperationException("Cannot calculate intersections without required geometry.");
        }

        var intersectionsDict = new Dictionary<Line, List<XYZ>>();

        foreach (var line in _dto.FullLines)
        {
            var rawIntersectionPoints = new List<XYZ>();

            foreach (var boundaryCurve in _dto.FaceBoundaryCurves)
            {
                var intersectResult = boundaryCurve.Intersect(line, CurveIntersectResultOption.Detailed);
                var overlaps = intersectResult?.GetOverlaps();

                if (overlaps != null)
                {
                    foreach (var overlap in overlaps)
                    {
                        if (overlap.Point != null)
                        {
                            rawIntersectionPoints.Add(overlap.Point);
                        }
                    }
                }
            }

            var uniqueIntersections = new List<XYZ>();
            foreach (var pt in rawIntersectionPoints)
            {
                if (!uniqueIntersections.Any(u => u.IsAlmostEqualTo(pt)))
                {
                    uniqueIntersections.Add(pt);
                }
            }

            intersectionsDict.Add(line, uniqueIntersections);
        }

        _dto.LinesIntersections = intersectionsDict;
    }

    public void FilterFaceOuterBoundaryInternalSegments(List<string> _telemetry)
    {
        if (_dto.LinesIntersections == null)
        {
            _telemetry?.Add("Intersection dictionary is null.");
            throw new InvalidOperationException("Intersections must be calculated before filtering.");
        }

        var result = new List<Line>();
        double revitMinimumLineLength = 0.0016;

        foreach (var kvp in _dto.LinesIntersections)
        {
            var baseLine = kvp.Key;
            var intersectionPoints = kvp.Value;

            if (intersectionPoints.Count >= 2)
            {
                var sortedPoints = intersectionPoints.OrderBy(p => GetB(p)).ToList();

                for (int i = 0; i < sortedPoints.Count - 1; i += 2)
                {
                    var start = sortedPoints[i];
                    var end = sortedPoints[i + 1];

                    if (start.DistanceTo(end) > revitMinimumLineLength)
                    {
                        result.Add(Line.CreateBound(start, end));
                    }
                    else
                    {
                        _telemetry?.Add($"Skipped internal segment at A-coord {GetA(start)} - distance too short.");
                    }
                }
            }
            else if (intersectionPoints.Count == 1)
            {
                _telemetry?.Add($"Line at A-coord {GetA(baseLine.GetEndPoint(0))} grazed the boundary (1 point) and was skipped.");
            }
        }

        _dto.SubdivisoryLines = result;
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.SubdivisoryLines ?? new List<Line>();
    }
}

public class Face_SubdivideInInternalLinesDto : Dto
{
    [Print(nameof(TypeFormatter.Face))]
    public Face FaceToSubdivide { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double SubdivisionSeparation { get; set; }

    //[Print(nameof(TypeFormatter.Enum))]
    public SubdivisionAxis SubdivisionBasis { get; set; }


    [Print(nameof(TypeFormatter.CurveList))]
    public List<Curve> FaceBoundaryCurves { get; set; }


    [Print(nameof(TypeFormatter.Double))]
    public double MinA { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double MaxA { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double MinB { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double MaxB { get; set; }


    [Print(nameof(TypeFormatter.Double))]
    public double ZLevel { get; set; }


    [Print(nameof(TypeFormatter.XYZList))]
    public List<XYZ> SubdivisionStartPoints { get; set; }


    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> FullLines { get; set; }

    public Dictionary<Line, List<XYZ>> LinesIntersections { get; set; }


    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> SubdivisoryLines { get; set; }
}