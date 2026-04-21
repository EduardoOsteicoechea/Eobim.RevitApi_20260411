using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using Eobim.RevitApi.Workflows;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<GenerateMarkedFloorsDFMADto>
{
	private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

	private const double SCALE = 25;

	private const double ORIGINAL_THICKNESS = 0.0015;

	private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits((SCALE * ORIGINAL_THICKNESS), UnitTypeId.Meters);

	// --- FAMILY CONSTANTS ---
	private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";

	private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";

	private readonly string CARDBOARD_TYPE_NAME = "Type 1";

	protected override void SetActions()
	{
		/* 1 */ Add(RunPrepareFamilyWorkflow);
        /* 2 */ Add(GetAllFloors);
		/* 3 */ Add(GetInterestFloorByMarkParameter);
        /* 4 */ Add(RunGetInterestFloorDMFADataWorkflow);
        /* 5 */ Add(ModelBottomFace);
        /* 5 */ Add(RunGenerateCurveLoopInternalOffsetBoundaryWorkflow);
        /* 5 */ Add(ModelBottomInternalFace);

        //Add(ModelOuterCurveLoopPointsOnDedicatedTransaction, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        //Add(ModelCurveLoopInternalOffsetBoundary, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        //Add(GenerateCurveLoopSegmentationFrame, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);

        ///* 6 */ Add(GenerateOuterCurveLoopDisplacedLines);
	}

    public void RunPrepareFamilyWorkflow(List<string> _telemetry)
    {
        var subworkflow = new RevitFamily_EntirelySetForUssageInRevitUI(_doc!, this.GetType().Name);
        subworkflow.SafelyInitializeInputs([CARDBOARD_FAMILY_PATH, CARDBOARD_FAMILY_NAME, CARDBOARD_TYPE_NAME]);
        subworkflow.Execute();
        if (subworkflow.Result is null) throw new NullReferenceException();
        _dto.CommonCarboardFamilySymbol = subworkflow.Result;
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

    public void RunGetInterestFloorDMFADataWorkflow(List<string> _telemetry)
    {
        var subworkflow = new RevitDFMA_ExtractFloorData(_doc!, this.GetType().Name);
        subworkflow.SafelyInitializeInputs([_dto.InterestFloor]);
        subworkflow.Execute();
        if (subworkflow.Result is null) throw new NullReferenceException();
        _dto.InterestFloorDFMAData = subworkflow.Result;
	}

    public void ModelBottomFace(List<string> _telemetry)
    {
        var subWorkflow = new DirectShape_ModelPlanarByBoundaryLines(_doc!, _workflowName!);
        subWorkflow.SafelyInitializeInputs([_dto.InterestFloorDFMAData.OuterCurveLoop.Select(a => a as Line).ToList()!, XYZ.BasisZ, CARDBOARD_THICKNESS, "BottomFace"]);
        subWorkflow.Execute();
        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));
        _dto.BottomFaceDirectShapeDMFAData = subWorkflow.Result;
    }

    public void RunGenerateCurveLoopInternalOffsetBoundaryWorkflow(List<string> _telemetry)
    {
        var subWorkflow = new CurveLoop_GenerateInnerOffsetBoundary(_doc!, _workflowName!);
        subWorkflow.SafelyInitializeInputs([_dto.InterestFloorDFMAData.OuterCurveLoop!, CARDBOARD_THICKNESS, CARDBOARD_THICKNESS, XYZ.BasisZ.Negate()]);
        subWorkflow.Execute();
        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));
        _dto.OuterCurveLoopInternalOffsetBoundary = subWorkflow.Result;
    }

    public void ModelBottomInternalFace(List<string> _telemetry)
    {
        var subWorkflow = new DirectShape_ModelPlanarByBoundaryLines(_doc!, _workflowName!);
        subWorkflow.SafelyInitializeInputs([_dto.OuterCurveLoopInternalOffsetBoundary!, XYZ.BasisZ, CARDBOARD_THICKNESS, "BottomInternalFace"]);
        subWorkflow.Execute();
        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));
        _dto.BottomFaceDirectShapeDMFAData = subWorkflow.Result;
    }

    private void GenerateOuterCurveLoopDisplacedLines(List<string> telemetry)
    {
        var subWorkflow = new LineList_GenerateDisplacedLinesWorkflow(_doc!, _workflowName!);
        subWorkflow.SafelyInitializeInputs([_dto.OuterCurveLoopInternalOffsetBoundary, CARDBOARD_THICKNESS]);
        subWorkflow.Execute();
        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));
        _dto.OuterCurveLoopDisplacedLines = subWorkflow.Result;
    }
}

public class GenerateMarkedFloorsDFMADto : Dto
{
	[Print(nameof(TypeFormatter.FloorList))]
	public List<Floor> Floors { get; set; }

	[Print(nameof(TypeFormatter.Floor))]
	public Floor InterestFloor { get; set; }


    public FloorDFMAData InterestFloorDFMAData { get; set; }


    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> OuterCurveLoopInternalOffsetBoundary { get; set; }


    public DirectShapeDMFAData BottomFaceDirectShapeDMFAData { get; set; }

	public List<Line> OuterCurveLoopDisplacedLines { get; set; }

    public List<Line> AdjustedInZCoordinateOuterCurveLoopDisplacedLines { get; set; }

    public CurveLoopSegmentationFrame OuterCurveLoopSegmentationFrame { get; set; }

	public FamilySymbol CommonCarboardFamilySymbol { get; set; }
	public Family CommonCarboardFamily { get; set; }


    [Print(nameof(TypeFormatter.FamilyInstanceList))]
    public List<FamilyInstance> OuterBorderPlacedFamilyInstances { get; set; }
}