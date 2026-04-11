using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Core;

public static class RevitFace
{
	public static Face Top
	(
		Element item
	)
	{
		IList<Reference> references = HostObjectUtils.GetTopFaces(item as HostObject);

		if (references.Count == 0)
		{
			throw new Exception($"No top face found for element with id: {item.Id}");
		}

		return item.GetGeometryObjectFromReference(references[0]) as Face;
	}

	public static CurveLoop OuterCurveLoop(Face face)
	{
		IList<CurveLoop> loops = face.GetEdgesAsCurveLoops();

		if (loops.Count == 1) return loops[0];

		if (face is PlanarFace planarFace)
		{
			XYZ normal = planarFace.FaceNormal;
			foreach (CurveLoop loop in loops)
			{
				if (loop.IsCounterclockwise(normal))
				{
					return loop;
				}
			}
		}
		else
		{
			BoundingBoxUV bbox = face.GetBoundingBox();
			UV center = new UV((bbox.Min.U + bbox.Max.U) / 2, (bbox.Min.V + bbox.Max.V) / 2);
			XYZ normal = face.ComputeNormal(center);

			foreach (CurveLoop loop in loops)
			{
				if (loop.IsCounterclockwise(normal))
				{
					return loop;
				}
			}
		}

		return loops.FirstOrDefault();
	}
}