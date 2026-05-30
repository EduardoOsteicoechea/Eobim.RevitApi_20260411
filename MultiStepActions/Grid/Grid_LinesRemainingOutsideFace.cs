using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions.Grid;

public record Grid_LinesRemainingOutsideFaceArgs(
      List<Line> Lines
    , Autodesk.Revit.DB.Face Face
    , bool ModelLines
);

public class Grid_LinesRemainingOutsideFace : MultistepObservableAction<Grid_LinesRemainingOutsideFaceArgs, Grid_LinesRemainingOutsideFaceDto, List<Line>>
{
    public override void SafelyInitializeInputs(Grid_LinesRemainingOutsideFaceArgs args)
    {
        _dto.Face = args.Face;
        _dto.InputLines = args.Lines;
        _dto.ModelLines = args.ModelLines;

        _dto.PerimeterCurves = new List<Curve>();
        _dto.FinalGridSegments = new List<Line>();
    }

    protected override void SetActions()
    {
        Add(ExtractFaceBoundaries);
        Add(ProcessAndFilterOutsideLines);
        Add(PlaceModelLines, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(SetResult);
    }

    public void ExtractFaceBoundaries(List<string> _telemetry)
    {
        IList<CurveLoop> curveLoops = _dto.Face.GetEdgesAsCurveLoops();

        foreach (CurveLoop loop in curveLoops)
        {
            foreach (Curve curve in loop)
            {
                // Flatten to Z=0 to ensure planar mathematical intersections
                XYZ p1 = new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0);
                XYZ p2 = new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0);

                _dto.PerimeterCurves.Add(Line.CreateBound(p1, p2));
            }
        }
    }

    public void ProcessAndFilterOutsideLines(List<string> _telemetry)
    {
        double minLength = 0.003; // Revit's short curve tolerance

        foreach (var line in _dto.InputLines)
        {
            List<XYZ> hitPoints = new List<XYZ>();

            // 1. Find all intersections between this line and the face boundaries
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

            // 2. Gather endpoints + intersection nodes
            var pointsOnLine = new List<XYZ> { line.GetEndPoint(0), line.GetEndPoint(1) };
            pointsOnLine.AddRange(hitPoints);

            // 3. Remove duplicates and sort sequentially from start to end
            var sortedPoints = pointsOnLine
                .GroupBy(p => Math.Round(p.DistanceTo(line.GetEndPoint(0)), 4))
                .Select(g => g.First())
                .OrderBy(p => p.DistanceTo(line.GetEndPoint(0)))
                .ToList();

            // 4. Fracture the line and evaluate each piece
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                XYZ p1 = sortedPoints[i];
                XYZ p2 = sortedPoints[i + 1];

                if (p1.DistanceTo(p2) > minLength)
                {
                    XYZ midPoint = (p1 + p2) / 2;

                    // If the midpoint is NOT inside the face, it means this segment is outside
                    if (!IsPointInsidePolygon(midPoint, _dto.PerimeterCurves))
                    {
                        _dto.FinalGridSegments.Add(Line.CreateBound(p1, p2));
                    }
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

            // Ray-casting algorithm
            if ((p1.Y > testPoint.Y) != (p2.Y > testPoint.Y) &&
                (testPoint.X < (p2.X - p1.X) * (testPoint.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
            {
                isInside = !isInside;
            }
        }

        return isInside;
    }

    public void PlaceModelLines(List<string> _telemetry)
    {
        if (_dto.ModelLines)
        {
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
    public List<Curve> PerimeterCurves { get; set; }
    public List<Line> FinalGridSegments { get; set; }
}