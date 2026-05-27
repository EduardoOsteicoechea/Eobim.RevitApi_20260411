using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions.Grid;

public record Grid_LinesRemainingOutsideFaceArgs(
      List<Line> Lines
    , Autodesk.Revit.DB.Face Face
    , double YDistance
    , double XDistance
    , bool ModelLines
);

public class Grid_LinesRemainingOutsideFace : MultistepObservableAction<Grid_LinesRemainingOutsideFaceArgs, Grid_LinesRemainingOutsideFaceDto, List<Line>>
{
    public override void SafelyInitializeInputs(Grid_LinesRemainingOutsideFaceArgs args)
    {
        _dto.Face = args.Face;
        _dto.YDistance = args.YDistance;
        _dto.XDistance = args.XDistance;
        _dto.InputLines = args.Lines;
        _dto.ModelLines = args.ModelLines;

        // Self-heal lists to protect against any null references during execution
        _dto.PerimeterCurves = new List<Curve>();
        _dto.ExtendedLines = new List<Line>();
        _dto.TrimmedLines = new List<Line>();
        _dto.IntersectionPoints = new List<XYZ>();
        _dto.FinalGridSegments = new List<Line>();
    }

    protected override void SetActions()
    {
        Add(GetFaceBoundingBoxAndPerimeter);
        Add(GenerateUnboundOrthogonalLines);
        Add(TrimLinesToFaceBoundaries);
        Add(CalculateGridIntersections);
        Add(GenerateGridSegmentsFromIntersections);
        Add(PlaceModelLines, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(SetResult);
    }

    public void GetFaceBoundingBoxAndPerimeter(List<string> _telemetry)
    {
        if (_dto.Face == null)
        {
            throw new ArgumentNullException(nameof(_dto.Face), "The Face provided to the workflow is null.");
        }

        _dto.BoundingBoxUV = _dto.Face.GetBoundingBox();

        IList<CurveLoop> curveLoops = _dto.Face.GetEdgesAsCurveLoops();

        foreach (CurveLoop loop in curveLoops)
        {
            foreach (Curve curve in loop)
            {
                if (curve == null) continue;

                XYZ p1 = new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0);
                XYZ p2 = new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0);

                _dto.PerimeterCurves.Add(Line.CreateBound(p1, p2));
            }
        }
    }

    public void GenerateUnboundOrthogonalLines(List<string> _telemetry)
    {
        // Calculate the true global bounding box from our flattened perimeter curves
        double minX = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
        double maxX = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
        double minY = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));
        double maxY = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));

        double extension = 10.0; // Extension to cross perimeter boundaries cleanly

        // Generate Vertical Lines (Iterating across X axis)
        for (double x = minX; x <= maxX; x += _dto.XDistance)
        {
            XYZ start = new XYZ(x, minY - extension, 0);
            XYZ end = new XYZ(x, maxY + extension, 0);
            _dto.ExtendedLines.Add(Line.CreateBound(start, end));
        }

        // Generate Horizontal Lines (Iterating across Y axis)
        for (double y = minY; y <= maxY; y += _dto.YDistance)
        {
            XYZ start = new XYZ(minX - extension, y, 0);
            XYZ end = new XYZ(maxX + extension, y, 0);
            _dto.ExtendedLines.Add(Line.CreateBound(start, end));
        }
    }

    public void TrimLinesToFaceBoundaries(List<string> _telemetry)
    {
        double minLength = 0.01; // Safety buffer above short curve tolerance
        double faceZ = _dto.Face.Evaluate(_dto.BoundingBoxUV.Min).Z;

        // Extract accurate bounds to cleanly clip outside limits
        double minX = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
        double maxX = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
        double minY = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));
        double maxY = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));

        foreach (var line in _dto.ExtendedLines)
        {
            bool isVertical = Math.Abs(line.GetEndPoint(0).X - line.GetEndPoint(1).X) < 1e-5;

            // Establish bounding box boundary points as anchor points
            XYZ boxStart = isVertical ? new XYZ(line.GetEndPoint(0).X, minY, 0) : new XYZ(minX, line.GetEndPoint(0).Y, 0);
            XYZ boxEnd = isVertical ? new XYZ(line.GetEndPoint(0).X, maxY, 0) : new XYZ(maxX, line.GetEndPoint(0).Y, 0);

            List<XYZ> hitPoints = new List<XYZ> { boxStart, boxEnd };

            // Find every point where the unbound line crosses the face's perimeter
            foreach (var boundaryCurve in _dto.PerimeterCurves)
            {
                CurveIntersectResult intersectResult = line.Intersect(boundaryCurve, CurveIntersectResultOption.Detailed);

                if (intersectResult != null && intersectResult.Result == SetComparisonResult.Overlap)
                {
                    IList<CurveOverlapPoint> overlaps = intersectResult.GetOverlaps();
                    if (overlaps != null)
                    {
                        foreach (CurveOverlapPoint overlap in overlaps)
                        {
                            if (overlap.Type == CurveOverlapPointType.Intersection)
                            {
                                hitPoints.Add(line.Evaluate(overlap.FirstParameter, false));
                            }
                        }
                    }
                }
            }

            // Sort all hits sequentially along the direction of the line starting from boxStart
            var sortedPoints = hitPoints
                .OrderBy(p => p.DistanceTo(boxStart))
                .ToList();

            // Clean microscopic segments by snapping close points together
            var cleanedPoints = new List<XYZ> { sortedPoints.First() };
            for (int i = 1; i < sortedPoints.Count; i++)
            {
                XYZ lastKept = cleanedPoints.Last();
                XYZ current = sortedPoints[i];

                if (lastKept.DistanceTo(current) > minLength)
                {
                    cleanedPoints.Add(current);
                }
            }

            // Create segments between each consecutive intersection point
            for (int i = 0; i < cleanedPoints.Count - 1; i++)
            {
                XYZ p1 = cleanedPoints[i];
                XYZ p2 = cleanedPoints[i + 1];

                XYZ midPoint = (p1 + p2) / 2;
                XYZ elevatedMidPoint = new XYZ(midPoint.X, midPoint.Y, faceZ);

                // Project onto the face to evaluate inclusion
                IntersectionResult proj = _dto.Face.Project(elevatedMidPoint);

                // INVERSION LOGIC: If it misses the face entirely or projects outside boundaries, it stays!
                if (proj == null || !_dto.Face.IsInside(proj.UVPoint))
                {
                    _dto.TrimmedLines.Add(Line.CreateBound(p1, p2));
                }
            }
        }
    }

    public void CalculateGridIntersections(List<string> _telemetry)
    {
        for (int i = 0; i < _dto.TrimmedLines.Count; i++)
        {
            for (int j = i + 1; j < _dto.TrimmedLines.Count; j++)
            {
                var lineA = _dto.TrimmedLines[i];
                var lineB = _dto.TrimmedLines[j];

                CurveIntersectResult intersectResult = lineA.Intersect(lineB, CurveIntersectResultOption.Detailed);

                if (intersectResult != null && intersectResult.Result == SetComparisonResult.Overlap)
                {
                    IList<CurveOverlapPoint> overlaps = intersectResult.GetOverlaps();
                    if (overlaps != null)
                    {
                        var firstIntersection = overlaps.FirstOrDefault(o => o.Type == CurveOverlapPointType.Intersection);
                        if (firstIntersection != null)
                        {
                            XYZ intersectionPoint = lineA.Evaluate(firstIntersection.FirstParameter, false);
                            _dto.IntersectionPoints.Add(intersectionPoint);
                        }
                    }
                }
            }
        }
    }

    public void GenerateGridSegmentsFromIntersections(List<string> _telemetry)
    {
        double minLength = 0.01;

        foreach (var line in _dto.TrimmedLines)
        {
            var pointsOnLine = new List<XYZ> { line.GetEndPoint(0), line.GetEndPoint(1) };

            foreach (var pt in _dto.IntersectionPoints)
            {
                if (line.Distance(pt) < 1e-4)
                {
                    pointsOnLine.Add(pt);
                }
            }

            var sortedPoints = pointsOnLine
                .OrderBy(p => p.DistanceTo(line.GetEndPoint(0)))
                .ToList();

            var cleanedPoints = new List<XYZ> { sortedPoints.First() };
            for (int i = 1; i < sortedPoints.Count; i++)
            {
                XYZ lastKept = cleanedPoints.Last();
                XYZ current = sortedPoints[i];

                if (lastKept.DistanceTo(current) > minLength)
                {
                    cleanedPoints.Add(current);
                }
            }

            for (int i = 0; i < cleanedPoints.Count - 1; i++)
            {
                XYZ p1 = cleanedPoints[i];
                XYZ p2 = cleanedPoints[i + 1];

                _dto.FinalGridSegments.Add(Line.CreateBound(p1, p2));
            }
        }
    }

    public void PlaceModelLines(List<string> _telemetry)
    {
        if (_dto.ModelLines)
        {
            // Reference your framework instance variables if configured differently
            Document doc = _doc;

            if (doc == null)
            {
                throw new InvalidOperationException("Revit Document is null. Cannot place model lines.");
            }

            SketchPlane sketchPlane = null;

            if (_dto.Face is PlanarFace planarFace)
            {
                if (planarFace.Reference != null)
                {
                    sketchPlane = SketchPlane.Create(doc, planarFace.Reference);
                }
                else
                {
                    Plane facePlane = Plane.CreateByNormalAndOrigin(planarFace.FaceNormal, planarFace.Origin);
                    sketchPlane = SketchPlane.Create(doc, facePlane);
                }
            }
            else
            {
                Plane flatPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
                sketchPlane = SketchPlane.Create(doc, flatPlane);
            }

            int successCount = 0;
            int skippedCount = 0;

            foreach (Line segment in _dto.FinalGridSegments)
            {
                try
                {
                    if (segment.Length > 0.01)
                    {
                        doc.Create.NewModelCurve(segment, sketchPlane);
                        successCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                {
                    _telemetry.Add($"Skipped microscopic or invalid grid segment: {ex.Message}");
                    skippedCount++;
                    continue;
                }
                catch (Exception ex)
                {
                    _telemetry.Add($"Failed to draw segment: {ex.Message}");
                    continue;
                }
            }

            _telemetry.Add($"Outside Grid Generation Complete: Placed {successCount} lines. Skipped {skippedCount} invalid segments.");
        }
    }

    public void SetResult(List<string> _telemetry)
    {
        if (_dto.FinalGridSegments == null || _dto.FinalGridSegments.Count == 0)
        {
            throw new InvalidOperationException("Failed to generate grid segments outside the face.");
        }

        Result = _dto.FinalGridSegments;
    }
}

public class Grid_LinesRemainingOutsideFaceDto : Dto
{
    public Autodesk.Revit.DB.Face Face { get; set; }
    public double YDistance { get; set; }
    public double XDistance { get; set; }
    public List<Line> InputLines { get; set; }
    public bool ModelLines { get; set; }
    public BoundingBoxUV BoundingBoxUV { get; set; }
    public List<Curve> PerimeterCurves { get; set; }
    public List<Line> ExtendedLines { get; set; }
    public List<Line> TrimmedLines { get; set; }
    public List<XYZ> IntersectionPoints { get; set; }
    public List<Line> FinalGridSegments { get; set; }
}