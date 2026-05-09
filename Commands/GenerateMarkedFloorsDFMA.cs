using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using Eobim.RevitApi.Workflows;
using System.Reflection;
using System.Windows;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<GenerateMarkedFloorsDFMADto, object>
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
        /* 1 */
        Add(RunPrepareFamilyWorkflow);

        /* 2 */
        Add(GetAllFloors);

        /* 3 */
        Add(GetInterestFloorByMarkParameter);

        /* 4 */
        Add(GetInterestFloorDMFADataWorkflow);

        /* 5 */
        Add(ModelBottomFace);

        /* 6 */
        Add(GenerateCurveLoopInternalOffsetBoundaryWorkflow);

        /* 7 */
        Add(ModelBottomInternalFace);

        /* 8 */
        Add(ModelTopFace);

        /* 9 */
        Add(GenerateCurveLoopInternalOffsetBoundaryWorkflowForTopFace);

        /* 10 */
        Add(ModelTopInternalFace);

        //Add(ModelOuterCurveLoopPointsOnDedicatedTransaction, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        //Add(ModelCurveLoopInternalOffsetBoundary, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        //Add(GenerateCurveLoopSegmentationFrame, true, Framework.TransactionManagementOptions.RequiresDedicatedTransactionForAction);

        /* 11 */
        Add(GenerateBottomFaceOuterCurveLoopDisplacedLines);

        /* 12 */
        Add(GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops);

        /* 13 */
        Add(ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours);
    }

    public void RunPrepareFamilyWorkflow(List<string> _telemetry)
    {
        _dto.CommonCarboardFamilySymbol = RunSubworkflow<
        RevitFamily_EntirelySetForUssageInRevitUI,
        RevitFamily_EntirelySetForUssageInRevitUIDto,
        FamilySymbol
        >(
            [CARDBOARD_FAMILY_PATH, CARDBOARD_FAMILY_NAME, CARDBOARD_TYPE_NAME]
        );
    }

    /////////////////////////////////
    /////////////////////////////////
    /// Floor Data preparation
    /////////////////////////////////
    /////////////////////////////////

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

    public void GetInterestFloorDMFADataWorkflow(List<string> _telemetry)
    {
        _dto.InterestFloorDFMAData = RunSubworkflow<
        RevitDFMA_ExtractFloorData,
        RevitDFMA_ExtractFloorDataDto,
        FloorDFMAData
        >(
            [_dto.InterestFloor]
        );
    }

    /////////////////////////////////
    /////////////////////////////////
    /// Bottom Face
    /////////////////////////////////
    /////////////////////////////////

    public void ModelBottomFace(List<string> _telemetry)
    {
        _dto.BottomFaceDirectShapeDMFAData = RunSubworkflow<
        DirectShape_ModelPlanarByBoundaryLines,
        DirectShape_ModelByBoundaryLinesDto,
        DirectShapeDMFAData
        >(
            [_dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop.Select(a => a as Line).ToList()!, XYZ.BasisZ, CARDBOARD_THICKNESS, "BottomFace"]
        );
    }

    public void GenerateCurveLoopInternalOffsetBoundaryWorkflow(List<string> _telemetry)
    {
        _dto.BottomFaceOuterCurveLoopInternalOffsetBoundary = RunSubworkflow<
        CurveLoop_GenerateInnerOffsetBoundary,
        RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
        List<Line>
        >(
            [_dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop!, CARDBOARD_THICKNESS, CARDBOARD_THICKNESS, XYZ.BasisZ.Negate()]
        );
    }

    public void ModelBottomInternalFace(List<string> _telemetry)
    {
        _dto.BottomInternalFaceDirectShapeDMFAData = RunSubworkflow<
        DirectShape_ModelPlanarByBoundaryLines,
        DirectShape_ModelByBoundaryLinesDto,
        DirectShapeDMFAData
        >(
            [_dto.BottomFaceOuterCurveLoopInternalOffsetBoundary!, XYZ.BasisZ, CARDBOARD_THICKNESS, "BottomInternalFace"]
        );
    }

    /////////////////////////////////
    /// Top Face
    /////////////////////////////////
    public void ModelTopFace(List<string> _telemetry)
    {
        _dto.TopFaceDirectShapeDMFAData = RunSubworkflow<
        DirectShape_ModelPlanarByBoundaryLines,
        DirectShape_ModelByBoundaryLinesDto,
        DirectShapeDMFAData
        >(
            [_dto.InterestFloorDFMAData.TopFaceOuterCurveLoop.Select(a => a as Line).ToList()!, XYZ.BasisZ.Negate(), CARDBOARD_THICKNESS, "TopFace"]
        );
    }
    public void GenerateCurveLoopInternalOffsetBoundaryWorkflowForTopFace(List<string> _telemetry)
    {
        _dto.TopFaceOuterCurveLoopInternalOffsetBoundary = RunSubworkflow<
        CurveLoop_GenerateInnerOffsetBoundary,
        RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
        List<Line>
        >(
            [_dto.InterestFloorDFMAData.TopFaceOuterCurveLoop!, CARDBOARD_THICKNESS, -CARDBOARD_THICKNESS, XYZ.BasisZ]
        );
    }
    public void ModelTopInternalFace(List<string> _telemetry)
    {
        _dto.TopInternalFaceDirectShapeDMFAData = RunSubworkflow<
        DirectShape_ModelPlanarByBoundaryLines,
        DirectShape_ModelByBoundaryLinesDto,
        DirectShapeDMFAData
        >(
            [_dto.TopFaceOuterCurveLoopInternalOffsetBoundary!, XYZ.BasisZ.Negate(), CARDBOARD_THICKNESS, "TopInternalFace"]
        );
    }

    /////////////////////////////////
    /////////////////////////////////
    /// Vertical External Faces
    /////////////////////////////////
    /////////////////////////////////
    private void GenerateBottomFaceOuterCurveLoopDisplacedLines(List<string> telemetry)
    {
        _dto.BottomFaceOuterCurveLoopDisplacedLines = RunSubworkflow<
        LineList_GenerateDisplacedLinesWorkflow,
        LineList_GenerateDisplacedLinesWorkflowDto,
        List<Line>
        >(
            [_dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop.Select(a => a as Line).ToList()!, CARDBOARD_THICKNESS]
        );
    }
    private void GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops(List<string> telemetry)
    {
        _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours = RunSubworkflow<
        LineList_GenerateCurveLoopsFromLines,
        LineList_GenerateCurveLoopsFromLinesDto,
        List<List<Line>>
        >(
            [_dto.BottomFaceOuterCurveLoopDisplacedLines, CARDBOARD_THICKNESS]
        );
    }
    public void ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours(List<string> _telemetry)
    {
        var pieceHeight = _dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z;

        var bottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCount = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours.Count;

        _telemetry.Add($"bottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCount: {bottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCount}");

        for (int i = 0; i < bottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCount; i++) 
        {
            var item = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours[i];

            RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
            >(
                [item, XYZ.BasisZ, pieceHeight, $"bottomFaceOuterCurveLoopDisplacedLinesPiecesContour_{i+1}"]
            );
        }
    }

    public override void SafelyInitializeInputs(object[] args)
    {
        throw new NotImplementedException();
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
    public List<Line> BottomFaceOuterCurveLoopInternalOffsetBoundary { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> TopFaceOuterCurveLoopInternalOffsetBoundary { get; set; }


    public DirectShapeDMFAData BottomFaceDirectShapeDMFAData { get; set; }

    public DirectShapeDMFAData BottomInternalFaceDirectShapeDMFAData { get; set; }


    public DirectShapeDMFAData TopFaceDirectShapeDMFAData { get; set; }

    public DirectShapeDMFAData TopInternalFaceDirectShapeDMFAData { get; set; }


    public List<Line> BottomFaceOuterCurveLoopDisplacedLines { get; set; }


    public List<List<Line>> BottomFaceOuterCurveLoopDisplacedLinesPiecesContours { get; set; }


    public List<Line> AdjustedInZCoordinateOuterCurveLoopDisplacedLines { get; set; }

    public CurveLoopSegmentationFrame OuterCurveLoopSegmentationFrame { get; set; }

    public FamilySymbol CommonCarboardFamilySymbol { get; set; }
    public Family CommonCarboardFamily { get; set; }


    [Print(nameof(TypeFormatter.FamilyInstanceList))]
    public List<FamilyInstance> OuterBorderPlacedFamilyInstances { get; set; }
}