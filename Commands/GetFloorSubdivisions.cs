using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.Workflows;
using static Eobim.RevitApi.Framework.DtoFormatter;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GetFloorSubdivisions : Framework.ExternalCommand<GetFloorSubdivisionsDto>
{
	private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

	private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits(.03, UnitTypeId.Meters);

	private readonly double FLOOR_OUTER_THICKNESS = UnitUtils.ConvertToInternalUnits(.3, UnitTypeId.Meters);

	// --- FAMILY CONSTANTS ---
	private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001.rfa";

	private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001";

	private readonly string CARDBOARD_TYPE_NAME = "Type 1";

	protected override void SetActions()
	{
        Add(GetAllFloors, true);
		Add(GetInterestFloorByMarkParameter, true);
		Add(GetInterestFloorTopFace, true);
		Add(GetInterestFloorTopFaceOuterCurveLoop, true);
		Add(ModelOuterCurveLoopPoints, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
		Add(GenerateCurveLoopSegmentationFrame, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
		Add(LoadCarboardFamilySymbol, true);
		Add(GetBasePlacementLevel, true);
		Add(GenerateOuterCurveLoopDisplacedLines, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
		//Add(PlaceOuterCarboardFamilyInstances, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
		//Add(SetOuterBorderPlacedFamilyInstancesHeight);
		//Add(SetOuterBorderPlacedFamilyInstancesThickness);

	}

    public void GetAllFloors(List<string> _telemetry)
	{
        var result = RevitFilteredElementCollector.ByBuiltInCategory<Floor>(_doc!, BuiltInCategory.OST_Floors);

		if (result is null) throw new NullReferenceException();

		if (result.Count.Equals(0)) throw new ArgumentOutOfRangeException($"Empty collection");

        _telemetry.Add($"Floors count: {result.Count}");

        _dto.Floors = result;
	}

	public void GetInterestFloorByMarkParameter(List<string> _telemetry)
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

        _telemetry.Add($"Floor: {result.Id} | {result.Name}");

        _dto.InterestFloor = result;
	}

	public void GetInterestFloorTopFace(List<string> _telemetry)
	{
		var result = RevitFace.Top(_dto.InterestFloor!);

		if (result is null) throw new NullReferenceException();

		_dto.InterestFloorTopFace = result;
	}

	public void GetInterestFloorTopFaceOuterCurveLoop(List<string> _telemetry)
	{
		var result = RevitFace.OuterCurveLoop(_dto.InterestFloorTopFace!);

		if (result is null) throw new NullReferenceException();

		_dto.OuterCurveLoop = result;
	}

	public void ModelOuterCurveLoopPoints(List<string> _telemetry)
	{
		var points = _dto.OuterCurveLoop!.SelectMany(a => a.Tessellate()).ToList();

		if (points is null) throw new NullReferenceException("Null points.");

		foreach (XYZ item in points)
		{
			var sphere = RevitSolid.SphereFromXYZAndRadius(item, .2);
			RevitDirectShape.GenericModelFromSolid(_doc!, sphere, "");
		}
	}

	public void GenerateCurveLoopSegmentationFrame(List<string> _telemetry)
	{
		_dto.OuterCurveLoopSegmentationFrame = RevitCurveLoop.SegmentationFrame(_doc!, _dto.OuterCurveLoop!, 2, 2);
	}

	public void LoadCarboardFamilySymbol(List<string> _telemetry)
	{
		var result = RevitFamily.GetSymbol(_doc!, CARDBOARD_FAMILY_NAME, CARDBOARD_TYPE_NAME);

		if (result is null)
		{
			RevitFamily.Load(_doc!, CARDBOARD_FAMILY_PATH);

			result = RevitFamily.GetSymbol(_doc!, CARDBOARD_FAMILY_NAME, CARDBOARD_TYPE_NAME);
		}

		if (result is null) throw new NullReferenceException();

		_dto.CarboardFamilySymbol = result;
	}

	public void GetBasePlacementLevel(List<string> _telemetry)
	{
		var result = _doc!.ActiveView.GenLevel;

		if (result == null)
		{
			result = new FilteredElementCollector(_doc!).OfClass(typeof(Level)).Cast<Level>().First();
		}

		if (result is null) throw new NullReferenceException();

		_dto.BasePlacementLevel = result;
	}

    private void GenerateOuterCurveLoopDisplacedLines(List<string> telemetry)
    {
        // Ensure we have the prerequisites from previous steps
        if (_dto.OuterCurveLoop == null) throw new Exception("OuterCurveLoop is missing.");

        // 1. Instantiate the sub-workflow
        var subWorkflow = new GenerateOuterCurveLoopDisplacedLinesWorkflow(_doc!, _workflowName!);

        // 2. Inject the necessary data
        subWorkflow.InitializeInputs(_dto.OuterCurveLoop, CARDBOARD_THICKNESS);

        // 3. Execute it safely
        subWorkflow.Execute();

        // 4. Retrieve the result and store it in the parent DTO for the next steps!
        _dto.OuterCurveLoopDisplacedLines = subWorkflow.Result;

        telemetry.Add($"Sub-workflow returned {_dto.OuterCurveLoopDisplacedLines!.Count} lines.");
    }

    //public void GenerateOuterCurveLoopDisplacedLines(List<string> _telemetry)
    //{
    //	var result = new List<Line>();

    //	var curveList = _dto.OuterCurveLoop!.ToList();

    //	for (int i = 1; i < curveList.Count; i++)
    //	{
    //		Curve curve = curveList[i];

    //		if (curve is Line line)
    //		{
    //			XYZ p0 = line.GetEndPoint(0);
    //			XYZ p1 = line.GetEndPoint(1);

    //			// Get the normalized direction vector of the line
    //			XYZ direction = (p1 - p0).Normalize();

    //			// Displace the START point forward by the cardboard thickness
    //			// Note: Doing this for every line in the closed loop creates a perfect 
    //			// "pinwheel" butt-joint at the corners where no pieces overlap.
    //			XYZ newP0 = p0 + direction * CARDBOARD_THICKNESS;

    //			//RevitDirectShape.GenericModelFromSolid(_doc!, RevitSolid.SphereFromXYZAndRadius(newP0, .1));

    //			// Safety check: Ensure the line hasn't been shrunk out of existence
    //			if (newP0.DistanceTo(p1) > 0.004)
    //			{
    //				result.Add(Line.CreateBound(newP0, p1));
    //			}
    //			else
    //			{
    //				// If the line is microscopic, keep the original to prevent a crash
    //				result.Add(line);
    //			}
    //		}
    //	}

    //	if (!result.Any()) throw new NullReferenceException("No lines were generated.");

    //	_dto.OuterCurveLoopDisplacedLines = result;
    //}

    public void PlaceOuterCarboardFamilyInstances(List<string> _telemetry)
	{
		var result = new List<FamilyInstance>();

		var linesToModel = _dto.OuterCurveLoopDisplacedLines!.ToList();

		foreach (Curve curve in linesToModel)
		{
			if (curve is Line line && line.Length > 0.004)
			{
				result.Add(RevitFamily.PlaceLineBasedTransactionless(
					_doc!,
					_dto.CarboardFamilySymbol!,
					_dto.BasePlacementLevel!,
					line.GetEndPoint(0),
					line.GetEndPoint(1)
				));
			}
		}

		if (result is null) throw new NullReferenceException();

		_dto.OuterBorderPlacedFamilyInstances = result;
	}

	public void SetOuterBorderPlacedFamilyInstancesHeight(List<string> _telemetry)
	{
		foreach (FamilyInstance instance in _dto.OuterBorderPlacedFamilyInstances!)
		{
			RevitFamily.SetSharedParameterValueByParameterName(instance, "Height", FLOOR_OUTER_THICKNESS);
		}
	}

	public void SetOuterBorderPlacedFamilyInstancesThickness(List<string> _telemetry)
	{
		foreach (FamilyInstance instance in _dto.OuterBorderPlacedFamilyInstances!)
		{
			RevitFamily.SetSharedParameterValueByParameterName(instance, "Thickness", CARDBOARD_THICKNESS);
		}
	}
}

public class GetFloorSubdivisionsDto: IDto
{
	[Print(nameof(TypeFormatter.FloorList))]
	public List<Floor> Floors { get; set; }

	public Floor? InterestFloor { get; set; }

	public Face? InterestFloorTopFace { get; set; }

	public CurveLoop? OuterCurveLoop { get; set; }

	public List<Line>? OuterCurveLoopDisplacedLines { get; set; }

	public CurveLoopSegmentationFrame? OuterCurveLoopSegmentationFrame { get; set; }

	public FamilySymbol? CarboardFamilySymbol { get; set; }

	public Level? BasePlacementLevel { get; set; }

	public List<FamilyInstance>? OuterBorderPlacedFamilyInstances { get; set; }

	public List<(string, object)> ToObservableObject()
	{
		return DtoFormater.FormatAsObject(this);
	}
}