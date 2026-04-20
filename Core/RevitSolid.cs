using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;

public static class RevitSolid
{
	// Existing method...
	public static Solid CreateSphereFromXYZAndRadius(
		XYZ point,
		double raidus = .5,
		double startAngle = 0,
		double endAngle = 2 * Math.PI
	)
	{
		// Assuming RevitFrame and RevitCurveLoop are defined elsewhere in your project
		return GeometryCreationUtilities.CreateRevolvedGeometry(
			RevitFrame.ZToXAndYToZNegative(point),
			new List<CurveLoop> { RevitCurveLoop.HalfSphereProfile(point, raidus) },
			startAngle,
			endAngle
		);
	}

	/// <summary>
	/// Creates a solid square bar extruded along a line.
	/// </summary>
	/// <param name="line">The axis and length of the bar.</param>
	/// <param name="radius">Half of the square's side length (distance from center to edge).</param>
	/// <returns>A Solid representing the square bar.</returns>
	public static Solid SquareBarFromLineAndRadius(Line line, double radius = .25)
	{
		XYZ p0 = line.GetEndPoint(0);
		XYZ p1 = line.GetEndPoint(1);

		XYZ direction = (p1 - p0).Normalize();
		double length = p0.DistanceTo(p1);

		// 1. Establish the orthogonal plane for the profile.
		// We need an 'up' reference to calculate the cross product. 
		// If the line itself is perfectly vertical, BasisZ will fail, so we fallback to BasisY.
		XYZ referenceUp = XYZ.BasisZ;
		if (direction.IsAlmostEqualTo(XYZ.BasisZ) || direction.IsAlmostEqualTo(-XYZ.BasisZ))
		{
			referenceUp = XYZ.BasisY;
		}

		// Calculate two vectors (u, v) that are orthogonal to the direction and to each other.
		XYZ u = direction.CrossProduct(referenceUp).Normalize();
		XYZ v = direction.CrossProduct(u).Normalize();

		// 2. Define the four corners of the square profile using the radius.
		XYZ c1 = p0 + (u * radius) + (v * radius);
		XYZ c2 = p0 - (u * radius) + (v * radius);
		XYZ c3 = p0 - (u * radius) - (v * radius);
		XYZ c4 = p0 + (u * radius) - (v * radius);

		// 3. Construct the boundary curves ensuring a continuous, closed loop.
		CurveLoop profileLoop = new CurveLoop();
		profileLoop.Append(Line.CreateBound(c1, c2));
		profileLoop.Append(Line.CreateBound(c2, c3));
		profileLoop.Append(Line.CreateBound(c3, c4));
		profileLoop.Append(Line.CreateBound(c4, c1));

		// 4. Extrude the profile along the exact vector of the line.
		return GeometryCreationUtilities.CreateExtrusionGeometry(
			new List<CurveLoop> { profileLoop },
			direction,
			length
		);
	}
}