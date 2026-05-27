//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi.MultiStepActions.Grid;

//public record Grid_LinesFromInternalOriginXYAlignedFaceArgs(
//    Autodesk.Revit.DB.Face Face
//    , double YDistance
//    , double XDistance
//    , bool ModelLines
//);

//public class Grid_LinesFromInternalOriginXYAlignedFace : MultistepObservableAction<Grid_LinesFromInternalOriginXYAlignedFaceArgs, Grid_LinesFromInternalOriginXYAlignedFaceDto, List<Line>>
//{
//    public override void SafelyInitializeInputs(Grid_LinesFromInternalOriginXYAlignedFaceArgs args)
//    {
//        _dto.Face = args.Face;
//        _dto.YDistance = args.YDistance;
//        _dto.XDistance = args.XDistance;
//        _dto.ModelLines = args.ModelLines;

//        _dto.PerimeterCurves = new();
//        _dto.ExtendedLines = new();
//        _dto.TrimmedLines = new();
//        _dto.IntersectionPoints = new();
//        _dto.FinalGridSegments = new();
//    }

//    protected override void SetActions()
//    {
//        Add(GetFaceBoundingBoxAndPerimeter);
//        Add(GenerateUnboundOrthogonalLines);
//        Add(TrimLinesToFaceBoundaries);
//        Add(CalculateGridIntersections);
//        Add(GenerateGridSegmentsFromIntersections);
//        Add(PlaceModelLines, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        Add(SetResult);
//    }

//    public void GetFaceBoundingBoxAndPerimeter(List<string> _telemetry)
//    {
//        _dto.BoundingBoxUV = _dto.Face.GetBoundingBox();

//        IList<CurveLoop> curveLoops = _dto.Face.GetEdgesAsCurveLoops();

//        foreach (CurveLoop loop in curveLoops)
//        {
//            foreach (Curve curve in loop)
//            {
//                XYZ p1 = new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0);
//                XYZ p2 = new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0);

//                _dto.PerimeterCurves.Add(Line.CreateBound(p1, p2));
//            }
//        }
//    }

//    public void GenerateUnboundOrthogonalLines(List<string> _telemetry)
//    {
//        // FIX 1: Calculate the true global bounding box from our flattened perimeter curves.
//        // This is 100% immune to inverted or rotated Face UV domains.
//        double minX = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
//        double maxX = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
//        double minY = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));
//        double maxY = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));

//        double extension = 10.0; // Extend past boundary to guarantee clean cuts later

//        // Generate Vertical Lines (Iterating across X axis)
//        for (double x = minX; x <= maxX; x += _dto.XDistance)
//        {
//            XYZ start = new XYZ(x, minY - extension, 0);
//            XYZ end = new XYZ(x, maxY + extension, 0);
//            _dto.ExtendedLines.Add(Line.CreateBound(start, end));
//        }

//        // Generate Horizontal Lines (Iterating across Y axis)
//        for (double y = minY; y <= maxY; y += _dto.YDistance)
//        {
//            XYZ start = new XYZ(minX - extension, y, 0);
//            XYZ end = new XYZ(maxX + extension, y, 0);
//            _dto.ExtendedLines.Add(Line.CreateBound(start, end));
//        }
//    }

//    public void TrimLinesToFaceBoundaries(List<string> _telemetry)
//    {
//        double minLength = 0.01;

//        // FIX 2: Find the actual Z-elevation of the face. 
//        // Even if UV bounds are inverted, evaluating Min will give us a point on the surface.
//        double faceZ = _dto.Face.Evaluate(_dto.BoundingBoxUV.Min).Z;

//        foreach (var line in _dto.ExtendedLines)
//        {
//            List<XYZ> hitPoints = new List<XYZ>();

//            // Find every point where the unbound line crosses the face's perimeter
//            foreach (var boundaryCurve in _dto.PerimeterCurves)
//            {
//                CurveIntersectResult intersectResult = line.Intersect(boundaryCurve, CurveIntersectResultOption.Detailed);

//                if (intersectResult != null && intersectResult.Result == SetComparisonResult.Overlap)
//                {
//                    IList<CurveOverlapPoint> overlaps = intersectResult.GetOverlaps();
//                    if (overlaps != null)
//                    {
//                        foreach (CurveOverlapPoint overlap in overlaps)
//                        {
//                            if (overlap.Type == CurveOverlapPointType.Intersection)
//                            {
//                                hitPoints.Add(line.Evaluate(overlap.FirstParameter, false));
//                            }
//                        }
//                    }
//                }
//            }

//            if (hitPoints.Count == 0) continue;

//            // Group by distance to remove duplicates, then sort sequentially along the line
//            var sortedPoints = hitPoints
//                .OrderBy(p => p.DistanceTo(line.GetEndPoint(0)))
//                .ToList();

//            // Clean microscopic segments by snapping close points together
//            var cleanedPoints = new List<XYZ> { sortedPoints.First() };
//            for (int i = 1; i < sortedPoints.Count; i++)
//            {
//                XYZ lastKept = cleanedPoints.Last();
//                XYZ current = sortedPoints[i];

//                if (lastKept.DistanceTo(current) > minLength)
//                {
//                    cleanedPoints.Add(current);
//                }
//            }

//            // Create segments between each consecutive intersection point
//            for (int i = 0; i < cleanedPoints.Count - 1; i++)
//            {
//                XYZ p1 = cleanedPoints[i];
//                XYZ p2 = cleanedPoints[i + 1];

//                XYZ midPoint = (p1 + p2) / 2;

//                // FIX 2 Continued: Elevate the midpoint to the exact Face elevation.
//                // This guarantees Face.Project() will immediately hit the surface and succeed.
//                XYZ elevatedMidPoint = new XYZ(midPoint.X, midPoint.Y, faceZ);

//                // Verify if this segment is actually inside the face (handles holes & concave shapes like L-shapes)
//                IntersectionResult proj = _dto.Face.Project(elevatedMidPoint);

//                if (proj != null && _dto.Face.IsInside(proj.UVPoint))
//                {
//                    _dto.TrimmedLines.Add(Line.CreateBound(p1, p2));
//                }
//            }
//        }
//    }

//    public void CalculateGridIntersections(List<string> _telemetry)
//    {
//        // Cross-check all trimmed lines against each other to find internal crossing nodes
//        for (int i = 0; i < _dto.TrimmedLines.Count; i++)
//        {
//            for (int j = i + 1; j < _dto.TrimmedLines.Count; j++)
//            {
//                var lineA = _dto.TrimmedLines[i];
//                var lineB = _dto.TrimmedLines[j];

//                // Updated for Revit 2026/2027 API
//                CurveIntersectResult intersectResult = lineA.Intersect(lineB, CurveIntersectResultOption.Detailed);

//                if (intersectResult != null && intersectResult.Result == SetComparisonResult.Overlap)
//                {
//                    IList<CurveOverlapPoint> overlaps = intersectResult.GetOverlaps();
//                    if (overlaps != null)
//                    {
//                        var firstIntersection = overlaps.FirstOrDefault(o => o.Type == CurveOverlapPointType.Intersection);
//                        if (firstIntersection != null)
//                        {
//                            XYZ intersectionPoint = lineA.Evaluate(firstIntersection.FirstParameter, false);
//                            _dto.IntersectionPoints.Add(intersectionPoint);
//                        }
//                    }
//                }
//            }
//        }
//    }

//    public void GenerateGridSegmentsFromIntersections(List<string> _telemetry)
//    {
//        foreach (var line in _dto.TrimmedLines)
//        {
//            // Start with the boundary endpoints of the trimmed line
//            var pointsOnLine = new List<XYZ> { line.GetEndPoint(0), line.GetEndPoint(1) };

//            // Add any intersection grid node that physically falls on this line
//            foreach (var pt in _dto.IntersectionPoints)
//            {
//                // If distance from point to line is practically zero, it lies on the line
//                if (line.Distance(pt) < 1e-5)
//                {
//                    pointsOnLine.Add(pt);
//                }
//            }

//            // Remove duplicates and sort sequentially from start to end
//            var sortedPoints = pointsOnLine
//                .GroupBy(p => Math.Round(p.DistanceTo(line.GetEndPoint(0)), 4))
//                .Select(g => g.First())
//                .OrderBy(p => p.DistanceTo(line.GetEndPoint(0)))
//                .ToList();

//            // Fracture the trimmed line into individual sub-segments for pathfinding/turning
//            for (int i = 0; i < sortedPoints.Count - 1; i++)
//            {
//                XYZ p1 = sortedPoints[i];
//                XYZ p2 = sortedPoints[i + 1];

//                // Discard micros-segments below Revit's tolerance threshold
//                if (p1.DistanceTo(p2) > 0.003)
//                {
//                    _dto.FinalGridSegments.Add(Line.CreateBound(p1, p2));
//                }
//            }
//        }
//    }

//    public void PlaceModelLines(List<string> _telemetry)
//    {
//        if (_dto.ModelLines)
//        {
//            // Note: To create physical lines in the model, you need the Revit Document.
//            // Replace `_dto.Document` with wherever your Eobim framework stores the active document 
//            // (e.g., this.Document, _dto.Document, or passing it in through the Args).
//            Document doc = _doc;

//            if (doc == null)
//            {
//                throw new InvalidOperationException("Revit Document is null. Ensure the Document is passed into the workflow to place Model Lines.");
//            }

//            // 1. We need a SketchPlane to draw the Model Lines on.
//            // If the Face is a flat planar face, we can generate the SketchPlane directly from it.
//            SketchPlane sketchPlane = null;

//            if (_dto.Face is PlanarFace planarFace)
//            {
//                if (planarFace.Reference != null)
//                {
//                    // If the face is tied to a physical element in the model, use its Reference
//                    sketchPlane = SketchPlane.Create(doc, planarFace.Reference);
//                }
//                else
//                {
//                    // If the face is transient (generated in memory), extract its mathematical plane
//                    Plane facePlane = Plane.CreateByNormalAndOrigin(planarFace.FaceNormal, planarFace.Origin);
//                    sketchPlane = SketchPlane.Create(doc, facePlane);
//                }
//            }
//            else
//            {
//                // Fallback: Create a SketchPlane strictly at Z=0 to match our math
//                Plane flatPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
//                sketchPlane = SketchPlane.Create(doc, flatPlane);
//            }

//            int successCount = 0;
//            int skippedCount = 0;

//            // 2. Safely place the lines
//            foreach (Line segment in _dto.FinalGridSegments)
//            {
//                try
//                {
//                    // The ultimate failsafe: Do not let Revit attempt to draw a microscopic line
//                    if (segment.Length > 0.01)
//                    {
//                        // Create the physical line in the Revit model
//                        doc.Create.NewModelCurve(segment, sketchPlane);
//                        successCount++;
//                    }
//                    else
//                    {
//                        skippedCount++;
//                    }
//                }
//                catch (Autodesk.Revit.Exceptions.ArgumentException ex)
//                {
//                    // If Revit still rejects the curve length or sketch plane alignment, ignore it and keep drawing the rest
//                    _telemetry.Add($"Skipped microscopic or invalid grid segment: {ex.Message}");
//                    skippedCount++;
//                    continue;
//                }
//                catch (Exception ex)
//                {
//                    // Catch any other unexpected Revit API geometry errors without crashing the whole workflow
//                    _telemetry.Add($"Failed to draw segment: {ex.Message}");
//                    continue;
//                }
//            }

//            _telemetry.Add($"Grid Generation Complete: Placed {successCount} lines. Skipped {skippedCount} invalid/microscopic segments.");
//        }
//    }

//    public void SetResult(List<string> _telemetry)
//    {
//        if (_dto.FinalGridSegments == null || _dto.FinalGridSegments.Count == 0)
//        {
//            throw new InvalidOperationException("Failed to generate grid segments. The face may be too small or bounds invalid.");
//        }

//        Result = _dto.FinalGridSegments;
//    }
//}

//public class Grid_LinesFromInternalOriginXYAlignedFaceDto : Dto
//{
//    [Print(nameof(TypeFormatter.Face))]
//    public Autodesk.Revit.DB.Face Face { get; set; }

//    [Print(nameof(TypeFormatter.Double))]
//    public double YDistance { get; set; }

//    [Print(nameof(TypeFormatter.Double))]
//    public double XDistance { get; set; }

//    [Print(nameof(TypeFormatter.Boolean))]
//    public bool ModelLines { get; set; }

//    [Print(nameof(TypeFormatter.BoundingBoxUV))]
//    public BoundingBoxUV BoundingBoxUV { get; set; }

//    [Print(nameof(TypeFormatter.CurveList))]
//    public List<Curve> PerimeterCurves { get; set; }

//    [Print(nameof(TypeFormatter.LineList))]
//    public List<Line> ExtendedLines { get; set; }

//    [Print(nameof(TypeFormatter.LineList))]
//    public List<Line> TrimmedLines { get; set; }

//    [Print(nameof(TypeFormatter.XYZList))]
//    public List<XYZ> IntersectionPoints { get; set; }

//    [Print(nameof(TypeFormatter.LineList))]
//    public List<Line> FinalGridSegments { get; set; }
//}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions.Grid;

public record Grid_LinesFromInternalOriginXYAlignedFaceArgs(
    Autodesk.Revit.DB.Face Face
    , double YDistance
    , double XDistance
    , bool ModelLines
);

public class Grid_LinesFromInternalOriginXYAlignedFace : MultistepObservableAction<Grid_LinesFromInternalOriginXYAlignedFaceArgs, Grid_LinesFromInternalOriginXYAlignedFaceDto, List<Line>>
{
    public override void SafelyInitializeInputs(Grid_LinesFromInternalOriginXYAlignedFaceArgs args)
    {
        _dto.Face = args.Face;
        _dto.YDistance = args.YDistance;
        _dto.XDistance = args.XDistance;
        _dto.ModelLines = args.ModelLines;

        _dto.PerimeterCurves = new();
        _dto.ExtendedLines = new();
        _dto.TrimmedLines = new();
        _dto.IntersectionPoints = new();
        _dto.FinalGridSegments = new();
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
        _dto.BoundingBoxUV = _dto.Face.GetBoundingBox();

        IList<CurveLoop> curveLoops = _dto.Face.GetEdgesAsCurveLoops();

        foreach (CurveLoop loop in curveLoops)
        {
            foreach (Curve curve in loop)
            {
                XYZ p1 = new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0);
                XYZ p2 = new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0);

                _dto.PerimeterCurves.Add(Line.CreateBound(p1, p2));
            }
        }
    }

    public void GenerateUnboundOrthogonalLines(List<string> _telemetry)
    {
        // FIX 1: Calculate the true global bounding box from our flattened perimeter curves.
        // This is 100% immune to inverted or rotated Face UV domains.
        double minX = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
        double maxX = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).X, c.GetEndPoint(1).X));
        double minY = _dto.PerimeterCurves.Min(c => Math.Min(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));
        double maxY = _dto.PerimeterCurves.Max(c => Math.Max(c.GetEndPoint(0).Y, c.GetEndPoint(1).Y));

        double extension = 10.0; // Extend past boundary to guarantee clean cuts later

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
        double minLength = 0.01;

        foreach (var line in _dto.ExtendedLines)
        {
            List<XYZ> hitPoints = new List<XYZ>();

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

            if (hitPoints.Count == 0) continue;

            // Group by distance to remove duplicates, then sort sequentially along the line
            var sortedPoints = hitPoints
                .OrderBy(p => p.DistanceTo(line.GetEndPoint(0)))
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

                // FIX 2: Use mathematical ray-casting instead of Revit's buggy transient Face.IsInside()
                if (IsPointInsidePolygon(midPoint, _dto.PerimeterCurves))
                {
                    _dto.TrimmedLines.Add(Line.CreateBound(p1, p2));
                }
            }
        }
    }

    private bool IsPointInsidePolygon(XYZ testPoint, List<Curve> boundaryCurves)
    {
        bool isInside = false;

        foreach (var curve in boundaryCurves)
        {
            XYZ p1 = curve.GetEndPoint(0);
            XYZ p2 = curve.GetEndPoint(1);

            // Standard 2D Ray-casting algorithm (ignoring Z-axis since your face is flat)
            if ((p1.Y > testPoint.Y) != (p2.Y > testPoint.Y) &&
                (testPoint.X < (p2.X - p1.X) * (testPoint.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
            {
                isInside = !isInside;
            }
        }

        return isInside;
    }

    public void CalculateGridIntersections(List<string> _telemetry)
    {
        // Cross-check all trimmed lines against each other to find internal crossing nodes
        for (int i = 0; i < _dto.TrimmedLines.Count; i++)
        {
            for (int j = i + 1; j < _dto.TrimmedLines.Count; j++)
            {
                var lineA = _dto.TrimmedLines[i];
                var lineB = _dto.TrimmedLines[j];

                // Updated for Revit 2026/2027 API
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
        foreach (var line in _dto.TrimmedLines)
        {
            // Start with the boundary endpoints of the trimmed line
            var pointsOnLine = new List<XYZ> { line.GetEndPoint(0), line.GetEndPoint(1) };

            // Add any intersection grid node that physically falls on this line
            foreach (var pt in _dto.IntersectionPoints)
            {
                // If distance from point to line is practically zero, it lies on the line
                if (line.Distance(pt) < 1e-5)
                {
                    pointsOnLine.Add(pt);
                }
            }

            // Remove duplicates and sort sequentially from start to end
            var sortedPoints = pointsOnLine
                .GroupBy(p => Math.Round(p.DistanceTo(line.GetEndPoint(0)), 4))
                .Select(g => g.First())
                .OrderBy(p => p.DistanceTo(line.GetEndPoint(0)))
                .ToList();

            // Fracture the trimmed line into individual sub-segments for pathfinding/turning
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                XYZ p1 = sortedPoints[i];
                XYZ p2 = sortedPoints[i + 1];

                // Discard micros-segments below Revit's tolerance threshold
                if (p1.DistanceTo(p2) > 0.003)
                {
                    _dto.FinalGridSegments.Add(Line.CreateBound(p1, p2));
                }
            }
        }
    }

    public void PlaceModelLines(List<string> _telemetry)
    {
        if (_dto.ModelLines)
        {
            // Note: To create physical lines in the model, you need the Revit Document.
            // Replace `_dto.Document` with wherever your Eobim framework stores the active document 
            // (e.g., this.Document, _dto.Document, or passing it in through the Args).
            Document doc = _doc;

            if (doc == null)
            {
                throw new InvalidOperationException("Revit Document is null. Ensure the Document is passed into the workflow to place Model Lines.");
            }

            // 1. We need a SketchPlane to draw the Model Lines on.
            // If the Face is a flat planar face, we can generate the SketchPlane directly from it.
            SketchPlane sketchPlane = null;

            if (_dto.Face is PlanarFace planarFace)
            {
                if (planarFace.Reference != null)
                {
                    // If the face is tied to a physical element in the model, use its Reference
                    sketchPlane = SketchPlane.Create(doc, planarFace.Reference);
                }
                else
                {
                    // If the face is transient (generated in memory), extract its mathematical plane
                    Plane facePlane = Plane.CreateByNormalAndOrigin(planarFace.FaceNormal, planarFace.Origin);
                    sketchPlane = SketchPlane.Create(doc, facePlane);
                }
            }
            else
            {
                // Fallback: Create a SketchPlane strictly at Z=0 to match our math
                Plane flatPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
                sketchPlane = SketchPlane.Create(doc, flatPlane);
            }

            int successCount = 0;
            int skippedCount = 0;

            // 2. Safely place the lines
            foreach (Line segment in _dto.FinalGridSegments)
            {
                try
                {
                    // The ultimate failsafe: Do not let Revit attempt to draw a microscopic line
                    if (segment.Length > 0.01)
                    {
                        // Create the physical line in the Revit model
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
                    // If Revit still rejects the curve length or sketch plane alignment, ignore it and keep drawing the rest
                    _telemetry.Add($"Skipped microscopic or invalid grid segment: {ex.Message}");
                    skippedCount++;
                    continue;
                }
                catch (Exception ex)
                {
                    // Catch any other unexpected Revit API geometry errors without crashing the whole workflow
                    _telemetry.Add($"Failed to draw segment: {ex.Message}");
                    continue;
                }
            }

            _telemetry.Add($"Grid Generation Complete: Placed {successCount} lines. Skipped {skippedCount} invalid/microscopic segments.");
        }
    }

    public void SetResult(List<string> _telemetry)
    {
        if (_dto.FinalGridSegments == null || _dto.FinalGridSegments.Count == 0)
        {
            throw new InvalidOperationException("Failed to generate grid segments. The face may be too small or bounds invalid.");
        }

        Result = _dto.FinalGridSegments;
    }
}

public class Grid_LinesFromInternalOriginXYAlignedFaceDto : Dto
{
    [Print(nameof(TypeFormatter.Face))]
    public Autodesk.Revit.DB.Face Face { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double YDistance { get; set; }

    [Print(nameof(TypeFormatter.Double))]
    public double XDistance { get; set; }

    [Print(nameof(TypeFormatter.Boolean))]
    public bool ModelLines { get; set; }

    [Print(nameof(TypeFormatter.BoundingBoxUV))]
    public BoundingBoxUV BoundingBoxUV { get; set; }

    [Print(nameof(TypeFormatter.CurveList))]
    public List<Curve> PerimeterCurves { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> ExtendedLines { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> TrimmedLines { get; set; }

    [Print(nameof(TypeFormatter.XYZList))]
    public List<XYZ> IntersectionPoints { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> FinalGridSegments { get; set; }
}