using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows; // Required for WPF MessageBox
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;

public static class RevitCurveLoop
{
	public static CurveLoop HalfSphereProfile(XYZ point, double raidus)
	{
		var startPoint = point + new XYZ(raidus, 0, 0);
		var endPoint = point + new XYZ(-raidus, 0, 0);
		var pointOnArc = point + new XYZ(0, raidus, 0);

		var arc = Arc.Create(startPoint, endPoint, pointOnArc);
		var line = Line.CreateBound(endPoint, startPoint);

		var curves = new CurveLoop();
		curves.Append(arc);
		curves.Append(line);

		return curves;
	}

	public class CurveLoopSegmentationFrameSegment
	{
		public Curve Curve { get; set; }
		public XYZ P1 { get; set; }
		public XYZ P2 { get; set; }
		public List<Line> SegmentationLines { get; set; }
		public List<Line> OddStartingSegments { get; set; } // These are the internal line segments
		public List<XYZ> OddStartingSegmentsInternalSegments { get; set; } // These are the points making up the internal segments
	}

	public class CurveLoopSegmentationFrame
	{
		public double SegmentLength { get; set; }
		public List<CurveLoopSegmentationFrameSegment> CurveLoopSegmentationFrameSegment { get; set; }
	}

	/// <summary>
	/// Helper method to chop a line into smaller line segments based on a max length.
	/// </summary>
	public static List<Line> SubdivideLine(Line originalLine, double segmentLength)
	{
		var subdividedLines = new List<Line>();
		double totalLength = originalLine.Length;

		// Use Ceiling to ensure we cover the remainder at the end of the line
		int numSegments = (int)Math.Ceiling(totalLength / segmentLength);

		for (int i = 0; i < numSegments; i++)
		{
			double startDist = i * segmentLength;
			double endDist = (i + 1) * segmentLength;

			// Clamp the end distance so it doesn't overshoot the actual line
			if (endDist > totalLength)
			{
				endDist = totalLength;
			}

			// Tolerance check: Prevent creating a 0-length line if the math divides perfectly
			if (endDist - startDist < 1e-5) continue;

			// Evaluate the normalized parameters (0 to 1) to get the true XYZ coordinates
			XYZ startPt = originalLine.Evaluate(startDist / totalLength, true);
			XYZ endPt = originalLine.Evaluate(endDist / totalLength, true);

			subdividedLines.Add(Line.CreateBound(startPt, endPt));
		}

		return subdividedLines;
	}

	/// <summary>
	/// Intersects a ray with a CurveLoop and returns only the line segments that fall INSIDE the loop.
	/// Compatible with Revit 2026 / 2027 API.
	/// </summary>
	public static List<Line> GetInternalSegments(Line ray, CurveLoop boundary)
	{
		var rawIntersections = new List<XYZ>();

		// 1. Find all intersection points using the Revit 2026+ API
		foreach (Curve curve in boundary)
		{
			CurveIntersectResult intersectRes = ray.Intersect(curve, CurveIntersectResultOption.Detailed);

			if (intersectRes.Result == SetComparisonResult.Overlap)
			{
				var overlaps = intersectRes.GetOverlaps();

				foreach (var overlap in overlaps)
				{
					rawIntersections.Add(overlap.Point);
				}
			}
		}

		// 2. Sort intersections by distance from the start of the ray
		var sortedPoints = rawIntersections.OrderBy(p => p.DistanceTo(ray.GetEndPoint(0))).ToList();

		// 3. Remove duplicate intersections (happens if a ray precisely hits a corner vertex)
		var uniquePoints = new List<XYZ>();
		foreach (var pt in sortedPoints)
		{
			if (!uniquePoints.Any(up => up.DistanceTo(pt) < 1e-4))
			{
				uniquePoints.Add(pt);
			}
		}

		// 4. Apply Even-Odd rule: Pair up points (0 to 1, 2 to 3) to create the internal segments
		var internalSegments = new List<Line>();
		for (int i = 0; i < uniquePoints.Count - 1; i += 2)
		{
			XYZ pStart = uniquePoints[i];
			XYZ pEnd = uniquePoints[i + 1];

			// Prevent creating 0-length lines
			if (pStart.DistanceTo(pEnd) > 1e-5)
			{
				internalSegments.Add(Line.CreateBound(pStart, pEnd));
			}
		}

		return internalSegments;
	}

	public static CurveLoopSegmentationFrame SegmentationFrame(
		Document doc,
		CurveLoop curveLoop,
		double offset = 1,
		double segmentLength = .5)
	{
		var segments = new List<CurveLoopSegmentationFrameSegment>();

		var points = new List<XYZ>();
		foreach (Curve curve in curveLoop)
		{
			// Tessellate returns points along the curve, catching the true apex of any arcs
			points.AddRange(curve.Tessellate());
		}

		// 2. Find the absolute minimums and maximums
		var minX = points.Min(p => p.X);
		var maxX = points.Max(p => p.X);
		var minY = points.Min(p => p.Y);
		var maxY = points.Max(p => p.Y);

		// 3. Define the bounding limits with your offset applied
		double finalMinX = minX - offset;
		double finalMaxX = maxX + offset;
		double finalMinY = minY - offset;
		double finalMaxY = maxY + offset;

		// 4. Define the four corners explicitly (forcing Z to 0 as you requested)
		XYZ bottomLeft = new XYZ(finalMinX, finalMinY, 0);
		XYZ bottomRight = new XYZ(finalMaxX, finalMinY, 0);
		XYZ topLeft = new XYZ(finalMinX, finalMaxY, 0);
		XYZ topRight = new XYZ(finalMaxX, finalMaxY, 0);

		// 5. Safely create your bounding lines
		Line bottomLine = Line.CreateBound(bottomLeft, bottomRight);
		Line leftLine = Line.CreateBound(bottomLeft, topLeft);
		Line rightLine = Line.CreateBound(bottomRight, topRight);
		Line topLine = Line.CreateBound(topLeft, topRight);

		var bottomLineSolid = RevitSolid.SquareBarFromLineAndRadius(bottomLine);
		var leftLineSolid = RevitSolid.SquareBarFromLineAndRadius(leftLine);
		var rightLineSolid = RevitSolid.SquareBarFromLineAndRadius(rightLine);
		var topLineSolid = RevitSolid.SquareBarFromLineAndRadius(topLine);

		RevitDirectShape.GenericModelFromSolid(doc, bottomLineSolid);
		RevitDirectShape.GenericModelFromSolid(doc, leftLineSolid);
		RevitDirectShape.GenericModelFromSolid(doc, rightLineSolid);
		RevitDirectShape.GenericModelFromSolid(doc, topLineSolid);
		
		// 6. Subdivide the Bottom and Left lines into manageable segments
		List<Line> bottomSubdivisions = SubdivideLine(bottomLine, segmentLength);
		List<Line> leftSubdivisions = SubdivideLine(leftLine, segmentLength);

		foreach (var subdivision in bottomSubdivisions)
		{
			RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(subdivision, .05));
			RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(subdivision.GetEndPoint(0), .05));
			RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(subdivision.GetEndPoint(1), .05));
		}

		foreach (var subdivision in leftSubdivisions)
		{
			RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(subdivision, .05));
			RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(subdivision.GetEndPoint(0), .05));
			RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(subdivision.GetEndPoint(1), .05));
		}

		// 7. Generate the perpendicular ray-casting lines spanning the bounding box
		// For the bottom subdivisions, keep the X from the start point and shoot Y up to finalMaxY
		List<Line> bottomPerpendicularSubdivision = bottomSubdivisions
			.Select(sub => Line.CreateBound(
				sub.GetEndPoint(0),
				new XYZ(sub.GetEndPoint(0).X, finalMaxY, 0)))
			.ToList();

		// For the left subdivisions, keep the Y from the start point and shoot X right to finalMaxX
		List<Line> leftPerpendicularSubdivision = leftSubdivisions
			.Select(sub => Line.CreateBound(
				sub.GetEndPoint(0),
				new XYZ(finalMaxX, sub.GetEndPoint(0).Y, 0)))
			.ToList();

		// --- VISUAL DEBUGGING FOR RAYS (Optional) ---
		//foreach (var ray in bottomPerpendicularSubdivision)
		//{
		//	RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(ray, .01));
		//}
		//foreach (var ray in leftPerpendicularSubdivision)
		//{
		//	RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(ray, .01));
		//}

		// 8. Intersect the horizontal rays (left perpendiculars) with the CurveLoop
		List<Line> horizontalInterceptionsWithCurveLoop = new List<Line>();
		foreach (var ray in leftPerpendicularSubdivision)
		{
			List<Line> internalLines = GetInternalSegments(ray, curveLoop);

			// Visual Debugging
			foreach (Line line in internalLines)
			{
				RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(line, .02));
				RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(line.GetEndPoint(0), .05));
				RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(line.GetEndPoint(1), .05));
			}
			//horizontalInterceptionsWithCurveLoop.AddRange(GetInternalSegments(doc, ray, curveLoop));
		}

		// 9. Intersect the vertical rays (bottom perpendiculars) with the CurveLoop
		List<Line> verticalInterceptionsWithCurveLoop = new List<Line>();
		foreach (var ray in bottomPerpendicularSubdivision)
		{
			List<Line> internalLines = GetInternalSegments(ray, curveLoop);

			// Visual Debugging
			foreach (Line line in internalLines)
			{
				RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(line, .02));
				RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(line.GetEndPoint(0), .05));
				RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SphereFromXYZAndRadius(line.GetEndPoint(1), .05));
			}
			//verticalInterceptionsWithCurveLoop.AddRange(GetInternalSegments(doc, ray, curveLoop));
		}

		//// --- VISUAL DEBUGGING FOR INTERNAL SEGMENTS ---
		//// See the actual chopped grid lines inside your boundary
		//foreach (var internalLine in horizontalInterceptionsWithCurveLoop)
		//{
		//	RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(internalLine, .02));
		//}
		//foreach (var internalLine in verticalInterceptionsWithCurveLoop)
		//{
		//	RevitDirectShape.GenericModelFromSolid(doc, RevitSolid.SquareBarFromLineAndRadius(internalLine, .02));
		//}

		return new CurveLoopSegmentationFrame
		{
			SegmentLength = segmentLength,
			CurveLoopSegmentationFrameSegment = segments
		};
	}
}