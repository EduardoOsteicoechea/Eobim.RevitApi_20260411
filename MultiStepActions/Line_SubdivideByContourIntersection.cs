using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions
{
    public class Line_SubdivideByContourIntersection(Document doc, string workflowName)
        : MultistepObservableAction<Line_SubdivideByContourIntersectionDto, List<Line>>(doc, workflowName)
    {
        public override void SafelyInitializeInputs(object[] args)
        {
            if (args == null || args.Length < 2)
                throw new ArgumentException("Insufficient arguments provided.");

            _dto.LinesToSubdivide = args[0] as List<Line> ?? throw new ArgumentException("First argument must be a List of Lines.");
            _dto.Contours = args[1] as List<List<Line>> ?? throw new ArgumentException("Second argument must be a List of Contours (List<List<Line>>).");
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

            foreach (var line in _dto.LinesToSubdivide)
            {
                var intersectionPoints = new List<XYZ>
                {
                    line.GetEndPoint(0),
                    line.GetEndPoint(1)
                };

                // 1. Gather all intersection points against all contours
                foreach (var contour in _dto.Contours)
                {
                    foreach (var edge in contour)
                    {
                        var intersectResult = line.Intersect(edge, CurveIntersectResultOption.Detailed);
                        var overlaps = intersectResult?.GetOverlaps();

                        if (overlaps != null)
                        {
                            foreach (var overlap in overlaps)
                            {
                                if (overlap.Point != null) intersectionPoints.Add(overlap.Point);
                            }
                        }
                    }
                }

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

                // 3. Create segments and test their midpoints
                for (int i = 0; i < sortedPts.Count - 1; i++)
                {
                    XYZ p1 = sortedPts[i];
                    XYZ p2 = sortedPts[i + 1];

                    if (p1.DistanceTo(p2) > revitMinimumLineLength)
                    {
                        XYZ midPoint = (p1 + p2) / 2.0;

                        // If the midpoint is NOT inside any vertical contour, it is the space "between" them
                        if (!IsPointInsideAnyContour(midPoint, _dto.Contours))
                        {
                            result.Add(Line.CreateBound(p1, p2));
                        }
                    }
                }
            }

            _dto.SubdividedLines = result;
        }

        // --- Standard 2D Ray-Casting Algorithm for Point-in-Polygon ---
        private bool IsPointInsideAnyContour(XYZ pt, List<List<Line>> contours)
        {
            foreach (var contour in contours)
            {
                if (IsPointInsidePolygon(pt, contour)) return true;
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
}