using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GetFloorSubdivisions : Framework.ExternalCommand<GetFloorSubdivisionsDto>
{
	private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";
	protected override void Prepare()
	{
		Add(GetAllFloors);
		Add(GetInterestFloor);
		Add(GetInterestFloorTopFace);
		Add(GetInterestFloorTopFaceOuterCurveLoop);
		Add(ModelOuterCurveLoopPoints);
		Add(GenerateCurveLoopSegmentationFrame);
		//Add(ModelOuterCurveRichSegmentsOffsetsEndpoints);
	}

	public void GetAllFloors()
	{
		var result = RevitFilteredElementCollector.ByBuiltInCategory<Floor>(_doc!, BuiltInCategory.OST_Floors);
		if (result is null) throw new NullReferenceException();
		if (!result.Any()) throw new ArgumentOutOfRangeException($"Empty collection");
		_dto.Floors = result;
	}

	public void GetInterestFloor()
	{
		var result = _dto.Floors!.FirstOrDefault(a =>
		{
			var parameter = a.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
			if (parameter is null) return false;
			if (!parameter.HasValue) return false;
			if (!parameter.AsValueString().Equals(INTEREST_FLOOR_MARK)) return false;
			return true;
		});

		if (result is null) throw new NullReferenceException();

		_dto.InterestFloor = result;
	}

	public void GetInterestFloorTopFace()
	{
		var result = RevitFace.Top(_dto.InterestFloor!);

		if (result is null) throw new NullReferenceException();

		_dto.InterestFloorTopFace = result;
	}

	public void GetInterestFloorTopFaceOuterCurveLoop()
	{
		var result = RevitFace.OuterCurveLoop(_dto.InterestFloorTopFace!);

		if (result is null) throw new NullReferenceException();

		_dto.OuterCurveLoop = result;
	}

	public void ModelOuterCurveLoopPoints()
	{
		var points = _dto.OuterCurveLoop!.SelectMany(a => a.Tessellate()).ToList();
		foreach (XYZ item in points)
		{
			var sphere = RevitSolid.SphereFromXYZAndRadius(item, .2);
			RevitDirectShape.GenericModelFromSolid(_doc!, sphere);
		}
	}

	public void GenerateCurveLoopSegmentationFrame()
	{
		_dto.OuterCurveLoopSegmentationFrame = RevitCurveLoop.SegmentationFrame(_doc!, _dto.OuterCurveLoop!);
	}
}

public class GetFloorSubdivisionsDto
{
	public List<Floor>? Floors { get; set; }
	public Floor? InterestFloor { get; set; }
	public Face? InterestFloorTopFace { get; set; }
	public CurveLoop? OuterCurveLoop { get; set; }
	public CurveLoopSegmentationFrame? OuterCurveLoopSegmentationFrame { get; set; }
}