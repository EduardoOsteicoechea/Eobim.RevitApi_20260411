using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using Eobim.RevitApi.Workflows;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<GenerateMarkedFloorsDFMADto>
{
	private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

	private const double SCALE = 25;

	private const double ORIGINAL_THICKNESS = 0.0015;

	private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits((SCALE * ORIGINAL_THICKNESS), UnitTypeId.Meters);

	private readonly double FLOOR_OUTER_THICKNESS = UnitUtils.ConvertToInternalUnits(.3, UnitTypeId.Meters);

	// --- FAMILY CONSTANTS ---
	private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";

	private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";

	private readonly string CARDBOARD_TYPE_NAME = "Type 1";

	protected override void SetActions()
	{
		/* 1 */ Add(GetBasePlacementLevel, true);
		/* 2 */ Add(LoadCarboardFamilySymbol, true);
        /* 3 */ Add(GetAllFloors, true);

		/* 4 */ Add(GetInterestFloorByMarkParameter, true);
        /* 5 */ Add(GetInterestFloorTopFace, true);
        /* 6 */ Add(GetInterestFloorBottomFace, true);
        /* 7 */ Add(GetInterestFloorTopFaceHighestPoint, true);
        /* 8 */ Add(GetInterestFloorBottomFaceLowestPoint, true);
        /* 9 */ Add(GetFamilyInstancesCommonHeight, true);
        /* 10 */ Add(GetInterestFloorTopFaceOuterCurveLoop, true);

        //Add(ModelOuterCurveLoopPointsOnDedicatedTransaction, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);

        /* 11 */ Add(GenerateCurveLoopInternalOffsetBoundary, true);

        //Add(ModelCurveLoopInternalOffsetBoundary, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);

        //Add(GenerateCurveLoopSegmentationFrame, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);

        /* 12 */ Add(GenerateOuterCurveLoopDisplacedLines, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 13 */ Add(SetOuterCurveLoopDisplacedLinesZCoordinates, true);

		/* 14 */ Add(PlaceOuterCarboardFamilyInstances, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
		/* 15 */ Add(SetOuterBorderPlacedFamilyInstancesHeight);
		/* 16 */ Add(SetOuterBorderPlacedFamilyInstancesThickness);
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

    public void GetInterestFloorBottomFace(List<string> _telemetry)
    {
        var result = RevitFace.Bottom(_dto.InterestFloor!);

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorBottomFace = result;
    }

    public void GetInterestFloorTopFaceHighestPoint(List<string> _telemetry)
    {
        var curveLoops = _dto.InterestFloorTopFace.GetEdgesAsCurveLoops();

        var curves = curveLoops.SelectMany(a => a).ToList();

        var points = curves.Select(a => a.GetEndPoint(0)).Concat(curves.Select(a => a.GetEndPoint(1))).ToList();

        var result = points.OrderByDescending(a => a.Z).First();

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorTopFaceHighestPoint = result;
    }

    public void GetInterestFloorBottomFaceLowestPoint(List<string> _telemetry)
    {
        var curveLoops = _dto.InterestFloorBottomFace.GetEdgesAsCurveLoops();

        var curves = curveLoops.SelectMany(a => a).ToList();

        var points = curves.Select(a => a.GetEndPoint(0)).Concat(curves.Select(a => a.GetEndPoint(1))).ToList();

        var result = points.OrderBy(a => a.Z).First();

        if (result is null) throw new NullReferenceException();

        _dto.InterestFloorBottomFaceLowestPoint = result;
    }

    public void GetFamilyInstancesCommonHeight(List<string> _telemetry)
    {
        var result  = _dto.InterestFloorTopFaceHighestPoint.Z - _dto.InterestFloorBottomFaceLowestPoint.Z;

        if(result.Equals(0)) throw new InvalidOperationException("The calculated common height is zero, which may indicate an issue with the input data.");

        _dto.FamilyInstancesCommonHeight = result;
    }

    //public void ModelOuterCurveLoopPointsOnDedicatedTransaction(List<string> _telemetry)
    //{
    //	var points = _dto.OuterCurveLoop!.SelectMany(a => a.Tessellate()).ToList();

    //	if (points is null) throw new NullReferenceException("Null points.");

    //	foreach (XYZ item in points)
    //	{
    //		var sphere = RevitSolid.CreateSphereFromXYZAndRadius(item, .2);
    //		RevitDirectShape.SetGenericModelFromSolidOnExistingTransaction(_doc!, sphere, "");
    //	}
    //}

    public void GenerateCurveLoopInternalOffsetBoundary(List<string> _telemetry)
	{
		var subWorkflow = new CurveLoop_GenerateInnerOffsetBoundary(_doc!, _workflowName!);

		subWorkflow.InitializeInputs(_dto.OuterCurveLoop!, CARDBOARD_THICKNESS / 2);

		subWorkflow.Execute();

        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));

		_dto.OuterCurveLoopInternalOffsetBoundary = subWorkflow.Result;
    }

 //   public void ModelCurveLoopInternalOffsetBoundary(List<string> _telemetry)
 //   {
 //       foreach (var item in _dto.OuterCurveLoopInternalOffsetBoundary)
 //       {
 //           RevitDirectShape.SetGenericModelFromSolidOnExistingTransaction(
	//			_doc!, 
	//			RevitSolid.SquareBarFromLineAndRadius(item, .1), 
	//			""
	//			);
 //       }
 //   }

 //   public void GenerateCurveLoopSegmentationFrame(List<string> _telemetry)
	//{
	//	_dto.OuterCurveLoopSegmentationFrame = RevitCurveLoop.SegmentationFrame(_doc!, _dto.OuterCurveLoop!, 2, 2);
	//}

    private void GenerateOuterCurveLoopDisplacedLines(List<string> telemetry)
    {
        var subWorkflow = new LineList_GenerateDisplacedLinesWorkflow(_doc!, _workflowName!);

        subWorkflow.InitializeInputs(_dto.OuterCurveLoopInternalOffsetBoundary, CARDBOARD_THICKNESS);

        subWorkflow.Execute();

        if (subWorkflow.Result is null) throw new NullReferenceException(nameof(subWorkflow.Result));

        _dto.OuterCurveLoopDisplacedLines = subWorkflow.Result;
    }

    private void SetOuterCurveLoopDisplacedLinesZCoordinates(List<string> telemetry)
    {
        var result = new List<Line>();

        foreach (var item in _dto.OuterCurveLoopDisplacedLines)
        {
            var p1 = item.GetEndPoint(0);
            var p2 = item.GetEndPoint(1);
            result.Add(Line.CreateBound(
                new XYZ(p1.X, p1.Y, _dto.InterestFloorBottomFaceLowestPoint.Z),
                new XYZ(p2.X, p2.Y, _dto.InterestFloorBottomFaceLowestPoint.Z)
                )
            );
        }

        _dto.AdjustedInZCoordinateOuterCurveLoopDisplacedLines = result;
    }

    public void PlaceOuterCarboardFamilyInstances(List<string> _telemetry)
	{
		var result = new List<FamilyInstance>();

		var linesToModel = _dto.AdjustedInZCoordinateOuterCurveLoopDisplacedLines!.ToList();

		foreach (Curve curve in linesToModel)
		{
			if (curve is Line line && line.Length > 0.004)
			{
                if (!_dto.CarboardFamilySymbol.IsActive)
                {
                    _dto.CarboardFamilySymbol!.Activate();
                    _doc!.Regenerate();
                }

                if (!AdaptiveComponentFamilyUtils.IsAdaptiveComponentFamily(_dto.CarboardFamilySymbol.Family))
                {
					var message = $"Error: The symbol '{_dto.CarboardFamilySymbol.Family.Name} - {_dto.CarboardFamilySymbol.Name}' is not valid for adaptive placement.";
					throw new Exception(message);
                }

                // 2. Create the adaptive component instance
                // Note: It temporarily generates at the origin (0,0,0)
                FamilyInstance adaptiveInstance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(_doc, _dto.CarboardFamilySymbol);

                // 3. Retrieve the internal adaptive points
                IList<ElementId> placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(adaptiveInstance);

                if (placePointIds.Count < 2)
                {
                    // Safety check: Ensure the family actually has 2 points
                    throw new System.Exception("The provided family does not have two adaptive placement points.");
                }

                // 4. Cast the ElementIds to ReferencePoints and move them
                ReferencePoint point1 = _doc.GetElement(placePointIds[0]) as ReferencePoint;
                ReferencePoint point2 = _doc.GetElement(placePointIds[1]) as ReferencePoint;

                // This is where you bypass the snapping engine! 
                // You are forcing strict absolute coordinates.
                point1.Position = line.GetEndPoint(0);
                point2.Position = line.GetEndPoint(1);

                // 5. Update the instance parameters (Thickness and Height)
                //Parameter thicknessParam = adaptiveInstance.LookupParameter("Thickness");

                //if (thicknessParam != null && !thicknessParam.IsReadOnly)
                //{
                //    thicknessParam.Set(CARDBOARD_THICKNESS);
                //}

                //Parameter heightParam = adaptiveInstance.LookupParameter("Height");
                //if (heightParam != null && !heightParam.IsReadOnly)
                //{
                //    heightParam.Set(_dto.InterestFloor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble());
                //}

                result.Add(adaptiveInstance);
            }
		}

		if (result is null) throw new NullReferenceException();

		_dto.OuterBorderPlacedFamilyInstances = result;
	}

	public void SetOuterBorderPlacedFamilyInstancesHeight(List<string> _telemetry)
	{
		foreach (FamilyInstance instance in _dto.OuterBorderPlacedFamilyInstances!)
		{
			RevitFamily.SetSharedParameterValueByParameterName(instance, "Height", _dto.FamilyInstancesCommonHeight);
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

public class GenerateMarkedFloorsDFMADto : IDto
{
	[Print(nameof(TypeFormatter.FloorList))]
	public List<Floor> Floors { get; set; }


    [Print(nameof(TypeFormatter.Floor))]
    public Floor InterestFloor { get; set; }


    [Print(nameof(TypeFormatter.Face))]
    public Face InterestFloorTopFace { get; set; }


    [Print(nameof(TypeFormatter.Face))]
    public Face InterestFloorBottomFace { get; set; }


    [Print(nameof(TypeFormatter.XYZ))]
    public XYZ InterestFloorTopFaceHighestPoint { get; set; }


    [Print(nameof(TypeFormatter.XYZ))]
    public XYZ InterestFloorBottomFaceLowestPoint { get; set; }


    [Print(nameof(TypeFormatter.Double))]
    public double FamilyInstancesCommonHeight { get; set; }


    [Print(nameof(TypeFormatter.CurveLoop))]
    public CurveLoop OuterCurveLoop { get; set; }


    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> OuterCurveLoopInternalOffsetBoundary { get; set; }

	public List<Line> OuterCurveLoopDisplacedLines { get; set; }

    public List<Line> AdjustedInZCoordinateOuterCurveLoopDisplacedLines { get; set; }

    public CurveLoopSegmentationFrame OuterCurveLoopSegmentationFrame { get; set; }

	public FamilySymbol CarboardFamilySymbol { get; set; }

	public Level BasePlacementLevel { get; set; }


    [Print(nameof(TypeFormatter.FamilyInstanceList))]
    public List<FamilyInstance> OuterBorderPlacedFamilyInstances { get; set; }

	public List<(string, object)> ToObservableObject()
    {
        return DtoFormatter.FormatAsObject(this);
    }
}