//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Core;
//using Eobim.RevitApi.Framework;
//using Eobim.RevitApi.MultiStepActions;
//using Eobim.RevitApi.Workflows;
//using static RevitCurveLoop;

//namespace Eobim.RevitApi.Commands;

//[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
//public class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<GenerateMarkedFloorsDFMADto>
//{
//	private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

//	private const double SCALE = 25;

//	private const double ORIGINAL_THICKNESS = 0.0015;

//	private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits((SCALE * ORIGINAL_THICKNESS), UnitTypeId.Meters);

//	// --- FAMILY CONSTANTS ---
//	private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";

//	private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";

//	private readonly string CARDBOARD_TYPE_NAME = "Type 1";

//	protected override void SetActions()
//	{
//		/* 1 */ Add(RunPrepareFamilyWorkflow, true);
//        /* 2 */ Add(GetAllFloors, true);
//		/* 3 */ Add(GetInterestFloorByMarkParameter, true);
//        /* 4 */ Add(RunGetInterestFloorDMFADataWorkflow, true);


//        /* 5 */ Add(GenerateCurveLoopInternalOffsetBoundary, true);

//        //Add(ModelOuterCurveLoopPointsOnDedicatedTransaction, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        //Add(ModelCurveLoopInternalOffsetBoundary, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        //Add(GenerateCurveLoopSegmentationFrame, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);

//        /* 6 */ Add(GenerateOuterCurveLoopDisplacedLines, true);
//        /* 7 */ Add(SetOuterCurveLoopDisplacedLinesZCoordinates, true);

//		/* 8 */ Add(PlaceOuterCarboardFamilyInstances, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//		/* 9 */ Add(SetOuterBorderPlacedFamilyInstancesHeight, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//		/* 10 */ Add(SetOuterBorderPlacedFamilyInstancesThickness, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//	}

//    public void RunPrepareFamilyWorkflow(List<string> _telemetry)
//    {
//        var subworkflow = new RevitFamily_EntirelySetForUssageInRevitUI(_doc!, this.GetType().Name);
//        subworkflow.SafelyInitializeInputs([CARDBOARD_FAMILY_PATH, CARDBOARD_FAMILY_NAME, CARDBOARD_TYPE_NAME]);
//        subworkflow.Execute();
//        if (subworkflow.Result is null) throw new NullReferenceException();
//        _dto.CommonCarboardFamilySymbol = subworkflow.Result;
//    }

//    public void GetAllFloors(List<string> _telemetry)
//    {
//        var result = RevitFilteredElementCollector.ByBuiltInCategory<Floor>(_doc!, BuiltInCategory.OST_Floors);

//        if (result is null) throw new NullReferenceException();

//        if (result.Count.Equals(0)) throw new ArgumentOutOfRangeException($"Empty collection");

//        _telemetry.Add($"Floors count: {result.Count}");

//        _dto.Floors = result;
//    }

//    public void GetInterestFloorByMarkParameter(List<string> _telemetry)
//	{
//		var result = _dto.Floors!.FirstOrDefault(a =>
//		{
//			var parameter = a.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
//			if (parameter is null) return false;
//			if (!parameter.HasValue) return false;
//			if (!parameter.AsValueString().Equals(INTEREST_FLOOR_MARK)) return false;
//			return true;
//		});

//		if (result is null) throw new NullReferenceException();

//        _telemetry.Add($"Floor: {result.Id} | {result.Name}");

//        _dto.InterestFloor = result;
//	}

//    public void RunGetInterestFloorDMFADataWorkflow(List<string> _telemetry)
//    {
//        var subworkflow = new RevitDFMA_ExtractFloorData(_doc!, this.GetType().Name);
//        subworkflow.SafelyInitializeInputs([_dto.InterestFloor]);
//        subworkflow.Execute();
//        if (subworkflow.Result is null) throw new NullReferenceException();
//        _dto.InterestFloorDFMAData = subworkflow.Result;
//	}

//    //public void ModelOuterCurveLoopPointsOnDedicatedTransaction(List<string> _telemetry)
//    //{
//    //	var points = _dto.OuterCurveLoop!.SelectMany(a => a.Tessellate()).ToList();

//    //	if (points is null) throw new NullReferenceException("Null points.");

//    //	foreach (XYZ item in points)
//    //	{
//    //		var sphere = RevitSolid.CreateSphereFromXYZAndRadius(item, .2);
//    //		RevitDirectShape.SetGenericModelFromSolidOnExistingTransaction(_doc!, sphere, "");
//    //	}
//    //}

//    public void GenerateCurveLoopInternalOffsetBoundary(List<string> _telemetry)
//	{
//		var subWorkflow = new CurveLoop_GenerateInnerOffsetBoundary(_doc!, _workflowName!);
//		subWorkflow.SafelyInitializeInputs([_dto.InterestFloorDFMAData.OuterCurveLoop!, CARDBOARD_THICKNESS / 2]);
//		subWorkflow.Execute();
//        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));
//		_dto.OuterCurveLoopInternalOffsetBoundary = subWorkflow.Result;
//    }

// //   public void ModelCurveLoopInternalOffsetBoundary(List<string> _telemetry)
// //   {
// //       foreach (var item in _dto.OuterCurveLoopInternalOffsetBoundary)
// //       {
// //           RevitDirectShape.SetGenericModelFromSolidOnExistingTransaction(
//	//			_doc!, 
//	//			RevitSolid.SquareBarFromLineAndRadius(item, .1), 
//	//			""
//	//			);
// //       }
// //   }

// //   public void GenerateCurveLoopSegmentationFrame(List<string> _telemetry)
//	//{
//	//	_dto.OuterCurveLoopSegmentationFrame = RevitCurveLoop.SegmentationFrame(_doc!, _dto.OuterCurveLoop!, 2, 2);
//	//}

//    private void GenerateOuterCurveLoopDisplacedLines(List<string> telemetry)
//    {
//        var subWorkflow = new LineList_GenerateDisplacedLinesWorkflow(_doc!, _workflowName!);
//        subWorkflow.SafelyInitializeInputs([_dto.OuterCurveLoopInternalOffsetBoundary, CARDBOARD_THICKNESS]);
//        subWorkflow.Execute();
//        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));
//        _dto.OuterCurveLoopDisplacedLines = subWorkflow.Result;
//    }

//    private void SetOuterCurveLoopDisplacedLinesZCoordinates(List<string> telemetry)
//    {
//        var result = new List<Line>();

//        foreach (var item in _dto.OuterCurveLoopDisplacedLines)
//        {
//            var p1 = item.GetEndPoint(0);
//            var p2 = item.GetEndPoint(1);
//            result.Add(Line.CreateBound(
//                new XYZ(p1.X, p1.Y, _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z),
//                new XYZ(p2.X, p2.Y, _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z)
//                )
//            );
//        }

//        _dto.AdjustedInZCoordinateOuterCurveLoopDisplacedLines = result;
//    }

// //   public void PlaceOuterCarboardFamilyInstances(List<string> _telemetry)
//	//{
//	//	var result = new List<FamilyInstance>();

//	//	var linesToModel = _dto.AdjustedInZCoordinateOuterCurveLoopDisplacedLines!.ToList();

//	//	foreach (Curve curve in linesToModel)
//	//	{
//	//		if (curve is Line line && line.Length > 0.004)
//	//		{
// //               if (!AdaptiveComponentFamilyUtils.IsAdaptiveComponentFamily(_dto.CommonCarboardFamilySymbol.Family))
// //               {
//	//				var message = $"Error: The symbol '{_dto.CommonCarboardFamilySymbol.Family.Name} - {_dto.CommonCarboardFamilySymbol.Name}' is not valid for adaptive placement.";
//	//				throw new Exception(message);
// //               }

// //               // 2. Create the adaptive component instance
// //               // Note: It temporarily generates at the origin (0,0,0)
// //               FamilyInstance adaptiveInstance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(_doc, _dto.CommonCarboardFamilySymbol);

// //               // 3. Retrieve the internal adaptive points
// //               IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(adaptiveInstance);

// //               if (placePointIds.Count < 2)
// //               {
// //                   // Safety check: Ensure the family actually has 2 points
// //                   throw new System.Exception("The provided family does not have two adaptive placement points.");
// //               }

// //               // 4. Cast the ElementIds to ReferencePoints and move them
// //               ReferencePoint point1 = _doc.GetElement(placePointIds[0]) as ReferencePoint;
// //               ReferencePoint point2 = _doc.GetElement(placePointIds[1]) as ReferencePoint;

// //               // This is where you bypass Revit's snapping engine! 
// //               // You are forcing strict absolute coordinates.
// //               point1.Position = line.GetEndPoint(0);
// //               point2.Position = line.GetEndPoint(1);

// //               result.Add(adaptiveInstance);
// //           }
//	//	}

//	//	if (result is null) throw new NullReferenceException();

//	//	_dto.OuterBorderPlacedFamilyInstances = result;
//	//}

//	//public void SetOuterBorderPlacedFamilyInstancesHeight(List<string> _telemetry)
//	//{
//	//	foreach (FamilyInstance instance in _dto.OuterBorderPlacedFamilyInstances!)
//	//	{
//	//		RevitFamily.SetParameterValueByParameterNameTransactionless(instance, "Height", _dto.InterestFloorDFMAData.Thickness);
//	//	}
//	//}

//	//public void SetOuterBorderPlacedFamilyInstancesThickness(List<string> _telemetry)
//	//{
//	//	foreach (FamilyInstance instance in _dto.OuterBorderPlacedFamilyInstances!)
//	//	{
//	//		RevitFamily.SetParameterValueByParameterNameTransactionless(instance, "Thickness", CARDBOARD_THICKNESS);
//	//	}
//	//}
//}

//public class GenerateMarkedFloorsDFMADto : Dto
//{
//	[Print(nameof(TypeFormatter.FloorList))]
//	public List<Floor> Floors { get; set; }

//	[Print(nameof(TypeFormatter.Floor))]
//	public Floor InterestFloor { get; set; }


//    [Print(nameof(TypeFormatter.Floor))]
//    public FloorDFMAData InterestFloorDFMAData { get; set; }


//    [Print(nameof(TypeFormatter.LineList))]
//    public List<Line> OuterCurveLoopInternalOffsetBoundary { get; set; }

//	public List<Line> OuterCurveLoopDisplacedLines { get; set; }

//    public List<Line> AdjustedInZCoordinateOuterCurveLoopDisplacedLines { get; set; }

//    public CurveLoopSegmentationFrame OuterCurveLoopSegmentationFrame { get; set; }

//	public FamilySymbol CommonCarboardFamilySymbol { get; set; }
//	public Family CommonCarboardFamily { get; set; }


//    [Print(nameof(TypeFormatter.FamilyInstanceList))]
//    public List<FamilyInstance> OuterBorderPlacedFamilyInstances { get; set; }
//}