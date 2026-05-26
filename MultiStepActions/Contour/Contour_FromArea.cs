using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions.Contour;

public record Contour_FromBoundarySegmentsArgs(
      List<List<BoundarySegment>> Segments
);

public class Contour_FromBoundarySegments : MultistepObservableAction<Contour_FromBoundarySegmentsArgs, Contour_FromBoundarySegmentsDto, List<CountourSegment>>
{
    public override void SafelyInitializeInputs(Contour_FromBoundarySegmentsArgs args)
    {
        _dto.Segments = args.Segments;
    }

    protected override void SetActions()
    {
        Add(GetSelectedRoomContourSegments);
        //Add(PlaceEnclosingModelLines, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(SetResult);
    }

    public void GetSelectedRoomContourSegments(List<string> _telemetry)
    {
        var result = new List<CountourSegment>();

        var outerBoundary = _dto.Segments.FirstOrDefault() ?? throw new ArgumentNullException($"Failed to obtain first roomBoundarySegment");

        var outerBoundaryCurves = outerBoundary.Select(a => a.GetCurve()).ToList();

        for (int i = 0; i < outerBoundaryCurves.Count; i++)
        {
            var curve = outerBoundaryCurves[i];

            var contourSegment = new CountourSegment();

            var contourLines = new List<Line>();

            if (curve is Line line)
            {
                contourLines.Add(line);
            }
            else
            {
                var tesselation = curve.Tessellate().ToList();
                var lines = CreateLinesFromTesselation(tesselation);
                contourLines.AddRange(lines);
            }

            if (contourLines is null) throw new Exception("Failed to obtain contour lines. Result is null.");
            if (!contourLines.Any()) throw new Exception("Failed to obtain contour lines. Result is empty.");

            contourSegment.Lines = contourLines;
            result.Add(contourSegment);
        }

        if (result is null) throw new Exception("Failed to obtain contour lines. Result is null.");
        if (!result.Any()) throw new Exception("Failed to obtain contour lines. Result is empty.");

        _dto.ContourSegments = result;
    }

    private List<Line> CreateLinesFromTesselation(List<XYZ> curvePoints)
    {
        var result = new List<Line>();

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

        return result;
    }

    public void PlaceEnclosingModelLines(List<string> _stateTrace)
    {
        if (_dto.ContourSegments == null || !_dto.ContourSegments.Any())
        {
            throw new InvalidOperationException("No contour segments available to calculate the bounding box.");
        }

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        // Extract the Z elevation from the first available point to keep the lines coplanar
        double zHeight = _dto.ContourSegments
            .FirstOrDefault()?.Lines.FirstOrDefault()?.GetEndPoint(0).Z ?? 0.0;

        // 1. Calculate the bounding box limits dynamically from the contour segments
        foreach (var segment in _dto.ContourSegments)
        {
            foreach (var line in segment.Lines)
            {
                // Check both start and end points of each tessellated line
                for (int i = 0; i < 2; i++)
                {
                    XYZ pt = line.GetEndPoint(i);
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
            }
        }

        // 2. Define the 4 corners of the enclosing orthogonal rectangle
        var p1 = new XYZ(minX, minY, zHeight);
        var p2 = new XYZ(maxX, minY, zHeight);
        var p3 = new XYZ(maxX, maxY, zHeight);
        var p4 = new XYZ(minX, maxY, zHeight);

        var lines = new List<Line>
        {
            Line.CreateBound(p1, p2),
            Line.CreateBound(p2, p3),
            Line.CreateBound(p3, p4),
            Line.CreateBound(p4, p1)
        };

        var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, p1);

        var sketchPlane = SketchPlane.Create(_doc, plane);

        foreach (var line in lines)
        {
            _doc!.Create.NewModelCurve(line, sketchPlane);
        }

        _stateTrace.Add($"Placed 4 enclosing model lines at Z elevation: {zHeight}");
    }

    public void SetResult(List<string> _stateTrace)
    {
        Result = _dto.ContourSegments;
    }
}

public class Contour_FromBoundarySegmentsDto : Dto
{
    public List<List<BoundarySegment>> Segments { get; set; }
    public List<CountourSegment> ContourSegments { get; set; }
}