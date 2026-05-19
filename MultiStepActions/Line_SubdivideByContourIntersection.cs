//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi.MultiStepActions;

//public record Line_SubdivideByContourIntersectionArgs
//(
//    List<Line> LinesToSubdivide,
//    List<List<Line>> Contours
//);

//public class Line_SubdivideByContourIntersection(Document doc, string workflowName)
//    : MultistepObservableAction<Line_SubdivideByContourIntersectionArgs, Line_SubdivideByContourIntersectionDto, List<Line>>(doc, workflowName)
//{
//    public override void SafelyInitializeInputs(Line_SubdivideByContourIntersectionArgs args)
//    {
//        _dto.LinesToSubdivide = args.LinesToSubdivide;
//        _dto.Contours = args.Contours;
//    }

//    protected override void SetActions()
//    {
//        Add(IntersectAndFilterLines);
//        Add(SetResult);
//    }

//    public void IntersectAndFilterLines(List<string> _telemetry)
//    {
//        var result = new List<Line>();
//        double revitMinimumLineLength = 0.0016;

//        foreach (var line in _dto.LinesToSubdivide)
//        {
//            var intersectionPoints = new List<XYZ>
//            {
//                line.GetEndPoint(0),
//                line.GetEndPoint(1)
//            };

//            // 1. Gather all intersection points against all contours
//            foreach (var contour in _dto.Contours)
//            {
//                foreach (var edge in contour)
//                {
//                    var intersectResult = line.Intersect(edge, CurveIntersectResultOption.Detailed);
//                    var overlaps = intersectResult?.GetOverlaps();

//                    if (overlaps != null)
//                    {
//                        foreach (var overlap in overlaps)
//                        {
//                            if (overlap.Point != null) intersectionPoints.Add(overlap.Point);
//                        }
//                    }
//                }
//            }

//            // 2. Deduplicate and sort points along the line
//            var uniquePts = new List<XYZ>();
//            foreach (var pt in intersectionPoints)
//            {
//                if (!uniquePts.Any(u => u.IsAlmostEqualTo(pt)))
//                {
//                    uniquePts.Add(pt);
//                }
//            }

//            // Sort by distance from the start point of the original line
//            var sortedPts = uniquePts.OrderBy(p => p.DistanceTo(line.GetEndPoint(0))).ToList();

//            // 3. Create segments and test their midpoints
//            for (int i = 0; i < sortedPts.Count - 1; i++)
//            {
//                XYZ p1 = sortedPts[i];
//                XYZ p2 = sortedPts[i + 1];

//                if (p1.DistanceTo(p2) > revitMinimumLineLength)
//                {
//                    XYZ midPoint = (p1 + p2) / 2.0;

//                    // If the midpoint is NOT inside any vertical contour, it is the space "between" them
//                    if (!IsPointInsideAnyContour(midPoint, _dto.Contours))
//                    {
//                        result.Add(Line.CreateBound(p1, p2));
//                    }
//                }
//            }
//        }

//        _dto.SubdividedLines = result;
//    }

//    // --- Standard 2D Ray-Casting Algorithm for Point-in-Polygon ---
//    private bool IsPointInsideAnyContour(XYZ pt, List<List<Line>> contours)
//    {
//        foreach (var contour in contours)
//        {
//            if (IsPointInsidePolygon(pt, contour)) return true;
//        }
//        return false;
//    }

//    private bool IsPointInsidePolygon(XYZ pt, List<Line> polygon)
//    {
//        int crossings = 0;
//        foreach (var edge in polygon)
//        {
//            XYZ p1 = edge.GetEndPoint(0);
//            XYZ p2 = edge.GetEndPoint(1);

//            // Check if a horizontal ray cast from the point intersects the edge
//            if (((p1.Y > pt.Y) != (p2.Y > pt.Y)) &&
//                (pt.X < (p2.X - p1.X) * (pt.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
//            {
//                crossings++;
//            }
//        }
//        // Odd number of crossings means the point is strictly inside the polygon
//        return crossings % 2 != 0;
//    }

//    public void SetResult(List<string> _telemetry)
//    {
//        Result = _dto.SubdividedLines ?? new List<Line>();
//    }
//}

//public class Line_SubdivideByContourIntersectionDto : Dto
//{
//    public List<Line> LinesToSubdivide { get; set; }
//    public List<List<Line>> Contours { get; set; }
//    public List<Line> SubdividedLines { get; set; }
//}


using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public record Line_SubdivideByContourIntersectionArgs
(
    List<Line> LinesToSubdivide,
    List<List<Line>> Contours
);

public class Line_SubdivideByContourIntersection(Document doc, string workflowName)
    : MultistepObservableAction<Line_SubdivideByContourIntersectionArgs, Line_SubdivideByContourIntersectionDto, List<Line>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(Line_SubdivideByContourIntersectionArgs args)
    {
        _dto.LinesToSubdivide = args.LinesToSubdivide;
        _dto.Contours = args.Contours;
    }

    protected override void SetActions()
    {
        Add(IntersectAndFilterLines);
        Add(SetResult);
    }

    public void IntersectAndFilterLines(List<string> _telemetry)
    {
        var result = new List<Line>();
        double revitMinimumLineLength = 0.0016;

        _telemetry.Add($"--- STARTING SUBDIVISION ---");
        _telemetry.Add($"Lines to subdivide: {_dto.LinesToSubdivide?.Count ?? 0}");
        _telemetry.Add($"Total contours available for intersection: {_dto.Contours?.Count ?? 0}");

        foreach (var line in _dto.LinesToSubdivide)
        {
            _telemetry.Add($"\nProcessing Line: Start({line.GetEndPoint(0)}) to End({line.GetEndPoint(1)}). Direction: {line.Direction}");

            var intersectionPoints = new List<XYZ>
            {
                line.GetEndPoint(0),
                line.GetEndPoint(1)
            };

            int intersectionHits = 0;

            // 1. Gather all intersection points against all contours
            for (int cIndex = 0; cIndex < _dto.Contours.Count; cIndex++)
            {
                var contour = _dto.Contours[cIndex];
                foreach (var edge in contour)
                {
                    var intersectResult = line.Intersect(edge, CurveIntersectResultOption.Detailed);
                    var overlaps = intersectResult?.GetOverlaps();

                    if (overlaps != null)
                    {
                        foreach (var overlap in overlaps)
                        {
                            if (overlap.Point != null)
                            {
                                intersectionPoints.Add(overlap.Point);
                                intersectionHits++;
                            }
                        }
                    }
                }
            }

            _telemetry.Add($"Total raw intersections found for this line: {intersectionHits}");

            // 2. Deduplicate and sort points along the line
            var uniquePts = new List<XYZ>();
            foreach (var pt in intersectionPoints)
            {
                if (!uniquePts.Any(u => u.IsAlmostEqualTo(pt)))
                {
                    uniquePts.Add(pt);
                }
            }

            // Sort by distance from the start point of the original line
            var sortedPts = uniquePts.OrderBy(p => p.DistanceTo(line.GetEndPoint(0))).ToList();

            _telemetry.Add($"Unique sorted points along line: {sortedPts.Count}");
            for (int i = 0; i < sortedPts.Count; i++)
            {
                _telemetry.Add($"  Pt[{i}]: {sortedPts[i]}");
            }

            // 3. Create segments and test their midpoints
            for (int i = 0; i < sortedPts.Count - 1; i++)
            {
                XYZ p1 = sortedPts[i];
                XYZ p2 = sortedPts[i + 1];
                double distance = p1.DistanceTo(p2);

                _telemetry.Add($"Evaluating Segment [{i} to {i + 1}]: Length = {distance:F4}");

                if (distance > revitMinimumLineLength)
                {
                    XYZ midPoint = (p1 + p2) / 2.0;
                    _telemetry.Add($"  Segment Midpoint: {midPoint}");

                    // If the midpoint is NOT inside any vertical contour, it is the space "between" them
                    bool isInside = IsPointInsideAnyContour(midPoint, _dto.Contours, _telemetry);

                    if (!isInside)
                    {
                        _telemetry.Add($"  -> Midpoint is OUTSIDE contours. Segment ADDED to results.");
                        result.Add(Line.CreateBound(p1, p2));
                    }
                    else
                    {
                        _telemetry.Add($"  -> Midpoint is INSIDE a contour. Segment DISCARDED.");
                    }
                }
                else
                {
                    _telemetry.Add($"  -> Segment too short (<= {revitMinimumLineLength}). Segment DISCARDED.");
                }
            }
        }

        _dto.SubdividedLines = result;
        _telemetry.Add($"\n--- SUBDIVISION COMPLETE ---");
        _telemetry.Add($"Total resulting subdivided lines: {_dto.SubdividedLines.Count}");
    }



    // --- Standard 2D Ray-Casting Algorithm for Point-in-Polygon ---
    // Added _telemetry parameter to track the ray-caster
    private bool IsPointInsideAnyContour(XYZ pt, List<List<Line>> contours, List<string> _telemetry)
    {
        for (int i = 0; i < contours.Count; i++)
        {
            if (IsPointInsidePolygon(pt, contours[i]))
            {
                _telemetry.Add($"  [Ray-Cast] Point {pt} fell inside Contour Index {i}.");
                return true;
            }
        }
        return false;
    }

    private bool IsPointInsidePolygon(XYZ pt, List<Line> polygon)
    {
        int crossings = 0;
        foreach (var edge in polygon)
        {
            XYZ p1 = edge.GetEndPoint(0);
            XYZ p2 = edge.GetEndPoint(1);

            // Check if a horizontal ray cast from the point intersects the edge
            if (((p1.Y > pt.Y) != (p2.Y > pt.Y)) &&
                (pt.X < (p2.X - p1.X) * (pt.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
            {
                crossings++;
            }
        }
        // Odd number of crossings means the point is strictly inside the polygon
        return crossings % 2 != 0;
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.SubdividedLines ?? new List<Line>();
    }
}

public class Line_SubdivideByContourIntersectionDto : Dto
{
    public List<Line> LinesToSubdivide { get; set; }
    public List<List<Line>> Contours { get; set; }
    public List<Line> SubdividedLines { get; set; }
}