using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.DxfExport;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GenerateMarkedFloorsDFMA
    : 
Framework.ExternalCommand<bool, GenerateMarkedFloorsDFMADto, object>
{
    protected override void OnAfterGeometryTransactionGroupBeforeFileIo()
    {
        if (_dto.NestedLayoutSheets is null)
            throw new InvalidOperationException("NestedLayoutSheets must be populated before building the DXF snapshot.");

        _dto.SheetsForDxfExport = DxfExportSnapshotBuilder.FromNestedLayoutSheets(_dto.NestedLayoutSheets);
    }

    private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

    private const double SCALE = 20;
    private const double ORIGINAL_THICKNESS = 0.0015;
    private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters);
    private readonly double INTERNAL_SUPPORTS_SEPARATION_1 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 50;
    private readonly double INTERNAL_SUPPORTS_SEPARATION_2 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 100;

    // --- PHYSICAL STOCK CONSTANTS (e.g., 2400mm x 1200mm standard sheet) ---
    private readonly double MAX_CARDBOARD_WIDTH = UnitUtils.ConvertToInternalUnits((SCALE * 2.4), UnitTypeId.Meters);
    private readonly double MAX_CARDBOARD_HEIGHT = UnitUtils.ConvertToInternalUnits((SCALE * 1.2), UnitTypeId.Meters);

    // --- FAMILY CONSTANTS & EXPORT PATHS ---
    private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";
    private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";
    private readonly string CARDBOARD_TYPE_NAME = "Type 1";

    // --- SHEET FAMILY CONSTANTS & EXPORT PATHS ---
    private readonly string SHEET_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Letter_Sheet_001.rfa";
    private readonly string SHEET_FAMILY_NAME = "Letter_Sheet_001";
    private readonly string SHEET_TYPE_NAME = "Type 1";

    private readonly string DXF_EXPORT_DIRECTORY = @"C:\Users\eduar\Desktop\Room_003\Revit2027\DXF_Exports";
    private readonly string PDF_EXPORT_DIRECTORY = @"C:\Users\eduar\Desktop\Room_003\Revit2027\PDF_Exports";


    public override void SafelyInitializeInputs(object[] args) { }
    public override void SafelyInitializeInputs(bool args) { }

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

        /////////////////////////////////
        /// Floor Vertical Outer Faces
        /////////////////////////////////
        /* 11 */
        Add(GenerateBottomFaceOffsetOuterCurveLoop);
        /* 12 */
        Add(GenerateBottomFaceOuterCurveLoopDisplacedLines);
        /* 13 */
        Add(GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops);
        /* 14 */
        Add(ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours);

        /////////////////////////////////
        /// Floor Vertical Internal Supports generation
        /////////////////////////////////
        /* 15 */
        Add(GetInternalBottomShapeTopFace);
        /* 16 */
        Add(GenerateBottomShapeTopFaceVerticalSubdivisoryLines);
        /* 17 */
        Add(GenerateBottomShapeTopFaceVerticalSubdivisoryLinesContours);
        /* 18 */
        Add(ModelBottomShapeTopFaceVerticalSubdivisoryLines);

        /////////////////////////////////
        /// Floor Horizontal Internal Supports generation
        /////////////////////////////////
        /* 19 */
        Add(GenerateBottomShapeTopFaceHorizontalSubdivisoryLines);
        /* 20 */
        Add(GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesIntersectionsWithVerticalSubdivisoryLines);
        /* 21 */
        Add(GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesContours);
        /* 22 */
        Add(ModelBottomShapeTopFaceHorizontalSubdivisoryLines);

        ///////////////////////////////////
        ///// Final Output & Fabrication (DXF & PDF)
        ///////////////////////////////////
        /* 23 */
        Add(OrderlyPlaceFaces);
        /* 24 */
        Add(PrepareSheetFamily);
        /* 25 */
        Add(GetPrintableAreaMetrics);
        /* 26 */
        Add(GroupPiecesByWidthAndHeight, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 27 */
        Add(TranslateGroupedPiecesByPrintableDimensionsSquares, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);

        // ==========================================
        // CRITICAL FIX: Use the Arranged Groups method, 
        // and DO NOT ask the framework for a dedicated transaction.
        // ==========================================
        /* 28 */
        Add(GenerateSheetsForArrangedGroups);


        /////////////////////////////////
        /// Cleanup
        /////////////////////////////////
        Add(HideInterestFloor, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
    }

    public void RunPrepareFamilyWorkflow(List<string> _telemetry)
    {
        _dto.CommonCarboardFamilySymbol = RunSubworkflow<
            RevitFamily_EntirelySetForUssageInRevitUIArgs,
            RevitFamily_EntirelySetForUssageInRevitUI,
            RevitFamily_EntirelySetForUssageInRevitUIDto,
            FamilySymbol
        >(
            new RevitFamily_EntirelySetForUssageInRevitUIArgs 
            {
                FamilyPath = CARDBOARD_FAMILY_PATH,
                FamilyName = CARDBOARD_FAMILY_NAME,
                FamilyTypeName = CARDBOARD_TYPE_NAME,
            }
        );
    }

    /////////////////////////////////
    /// Floor Data preparation
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
    /// Bottom Face
    /////////////////////////////////
    public void ModelBottomFace(List<string> _telemetry)
    {
        _dto.BottomFaceDirectShapeDMFAData = RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            [
            _dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop.Select(a => a as Line).ToList()!,
            XYZ.BasisZ.Negate(),
            CARDBOARD_THICKNESS,
            "BottomFace",
            CARDBOARD_THICKNESS,
            ]
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
            [
            _dto.BottomFaceOuterCurveLoopInternalOffsetBoundary!,
            XYZ.BasisZ.Negate(),
            CARDBOARD_THICKNESS,
            "BottomInternalFace",
            CARDBOARD_THICKNESS
            ]
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
            [_dto.InterestFloorDFMAData.TopFaceOuterCurveLoop.Select(a => a as Line).ToList()!, XYZ.BasisZ.Negate(), CARDBOARD_THICKNESS, "TopFace", 0.0]
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
            [_dto.TopFaceOuterCurveLoopInternalOffsetBoundary!, XYZ.BasisZ.Negate(), CARDBOARD_THICKNESS, "TopInternalFace", 0.0]
        );
    }

    /////////////////////////////////
    /// Vertical External Faces
    /////////////////////////////////
    private void GenerateBottomFaceOffsetOuterCurveLoop(List<string> telemetry)
    {
        _dto.BottomFaceOffsetOuterCurveLoop = RunSubworkflow<
            CurveLoop_GenerateInnerOffsetBoundary,
            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
            List<Line>
        >(
            [_dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop, -CARDBOARD_THICKNESS / 2, CARDBOARD_THICKNESS, XYZ.BasisZ]
        );
    }

    private void GenerateBottomFaceOuterCurveLoopDisplacedLines(List<string> telemetry)
    {
        _dto.BottomFaceOuterCurveLoopDisplacedLines = RunSubworkflow<
            LineList_GenerateDisplacedLinesWorkflow,
            LineList_GenerateDisplacedLinesWorkflowDto,
            List<Line>
        >(
            [_dto.BottomFaceOffsetOuterCurveLoop, CARDBOARD_THICKNESS]
        );
    }

    private void GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops(List<string> telemetry)
    {
        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 2;

        _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours = RunSubworkflow<
            LineList_GenerateCurveLoopsFromLines,
            LineList_GenerateCurveLoopsFromLinesDto,
            List<(List<Line>, string)>
        >(
            [_dto.BottomFaceOuterCurveLoopDisplacedLines, CARDBOARD_THICKNESS, "vertical", pieceHeight]
        //[_dto.BottomFaceOuterCurveLoopDisplacedLines, CARDBOARD_THICKNESS, "", 0.0]
        );
    }

    private DirectShapeDMFAData ModelFromContourAndDirection
    (

        List<Line> items,
        string orientation,
        int pieceIndex,
        double pieceHeight,
        string nameBase,
        double heightAdjustment = 0.0,
        bool negateDirection = false
    )
    {
        var extrusionDirection = XYZ.BasisZ;

        if (orientation.Equals("vertical"))
        {
            extrusionDirection = items.FirstOrDefault()?.Direction.CrossProduct(XYZ.BasisZ) ?? XYZ.BasisZ;

            if (negateDirection)
            {
                extrusionDirection = extrusionDirection.Negate();
            }

            pieceHeight = CARDBOARD_THICKNESS;
        }

        return RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            [items, extrusionDirection, pieceHeight, $"{nameBase}_{pieceIndex + 1}", heightAdjustment]
        );
    }

    public void ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours(List<string> _telemetry)
    {
        var pieces = new List<DirectShapeDMFAData>();

        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 2;
        var contourCount = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours.Count;

        for (int i = 0; i < contourCount; i++)
        {
            var item = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours[i];

            pieces.Add(
                ModelFromContourAndDirection(
                    item.Item1,
                    item.Item2,
                    i,
                    pieceHeight,
                    "BottomFaceOuterCurveLoopDisplacedLinesPiecesContour",
                    0.0,
                    true
                )
            );
        }

        _dto.ExternalVerticalFacesDirectShapeDMFADataList = pieces;
    }

    /////////////////////////////////
    /// Shape Internal Supports generation
    /////////////////////////////////
    public void GetInternalBottomShapeTopFace(List<string> _telemetry)
    {
        _dto.InternalBottomShapeTopFace = RunSubworkflow<
            Face_FromDirectShape,
            Face_FromDirectShapeDto,
            Face
        >(
            [_dto.BottomInternalFaceDirectShapeDMFAData.DirectShape, "top"]
        );
    }

    public void GenerateBottomShapeTopFaceVerticalSubdivisoryLines(List<string> _telemetry)
    {
        _dto.BottomShapeTopFaceVerticalSubdivisoryLines = RunSubworkflow<
            Face_SubdivideInInternalLines,
            Face_SubdivideInInternalLinesDto,
            List<Line>
        >(
            [_dto.InternalBottomShapeTopFace, INTERNAL_SUPPORTS_SEPARATION_1, SubdivisionAxis.Y]
        );
    }

    public void GenerateBottomShapeTopFaceVerticalSubdivisoryLinesContours(List<string> _telemetry)
    {
        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;

        _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours = RunSubworkflow<
            LineList_GenerateCurveLoopsFromLines,
            LineList_GenerateCurveLoopsFromLinesDto,
            List<(List<Line>, string)>
        >(
            [_dto.BottomShapeTopFaceVerticalSubdivisoryLines, CARDBOARD_THICKNESS, "vertical", pieceHeight]
        );
    }

    public void ModelBottomShapeTopFaceVerticalSubdivisoryLines(List<string> _telemetry)
    {
        var pieces = new List<DirectShapeDMFAData>();

        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;
        var contourCount = _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours.Count;

        _telemetry.Add($"BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContoursCount: {contourCount}");

        for (int i = 0; i < contourCount; i++)
        {
            var item = _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours[i];

            pieces.Add(
                ModelFromContourAndDirection(
                    item.Item1,
                    item.Item2,
                    i,
                    pieceHeight,
                    "BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContour"
                )
            );
        }

        _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData = pieces;
    }

    /////////////////////////////////
    /// Floor Horizontal Internal Supports generation
    /////////////////////////////////
    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLines(List<string> _telemetry)
    {
        _dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines = RunSubworkflow<
            Face_SubdivideInInternalLines,
            Face_SubdivideInInternalLinesDto,
            List<Line>
        >(
            [_dto.InternalBottomShapeTopFace, INTERNAL_SUPPORTS_SEPARATION_2, SubdivisionAxis.X]
        );
    }

    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesIntersectionsWithVerticalSubdivisoryLines(List<string> _telemetry)
    {
        // 1. Upstream Null Check
        if (_dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData == null)
        {
            throw new NullReferenceException("CRITICAL ERROR: '_dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData' is null.");
        }

        var verticalSupportContours = new List<List<Line>>();

        foreach (var item in _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData)
        {
            if (item.DirectBottomFace == null) continue;

            // 1. Get the direction THIS specific face is pointing
            XYZ faceNormal = item.DirectBottomFace.ComputeNormal(new UV(0.5, 0.5));

            // 2. Evaluate counter-clockwise against the face's own normal!
            var validLoop = item.DirectBottomFace.GetEdgesAsCurveLoops()
                .FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

            if (validLoop != null)
            {
                var straightLinesForThisContour = validLoop.OfType<Line>().ToList();

                if (straightLinesForThisContour.Any())
                {
                    verticalSupportContours.Add(straightLinesForThisContour);
                }
            }
            else
            {
                _telemetry.Add($"Warning: Could not find a counter-clockwise loop for DirectShape {item.DirectShape?.Id}.");
            }
        }

        _telemetry.Add($"Total vertical support contours extracted: {verticalSupportContours.Count}");

        if (_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines == null)
        {
            throw new NullReferenceException("CRITICAL ERROR: '_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines' is null.");
        }

        _dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines = RunSubworkflow<
            Line_SubdivideByContourIntersection,
            Line_SubdivideByContourIntersectionDto,
            List<Line>
        >(
            [_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines, verticalSupportContours]
        );
    }

    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesContours(List<string> _telemetry)
    {
        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;

        _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours = RunSubworkflow<
            LineList_GenerateCurveLoopsFromLines,
            LineList_GenerateCurveLoopsFromLinesDto,
            List<(List<Line>, string)>
        >(
            [_dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines, CARDBOARD_THICKNESS, "vertical", pieceHeight]
        );
    }

    public void ModelBottomShapeTopFaceHorizontalSubdivisoryLines(List<string> _telemetry)
    {
        var pieces = new List<DirectShapeDMFAData>();

        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;
        var contourCount = _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours.Count;

        _telemetry.Add($"BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContoursCount: {contourCount}");

        for (int i = 0; i < contourCount; i++)
        {
            var item = _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours[i];

            pieces.Add(
                ModelFromContourAndDirection(
                    item.Item1,
                    item.Item2,
                    i,
                    pieceHeight,
                    "BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContour",
                    0.0,
                    true
                )
            );
        }

        _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData = pieces;
    }

    /////////////////////////////////
    /// Final Output & Fabrication
    /////////////////////////////////

    public void OrderlyPlaceFaces(List<string> _telemetry)
    {
        _dto.PiecesPlacedAtStartPoint = RunSubworkflow<
            DFMA_PlacePiecesByRowsAndColums,
            DFMA_PlacePiecesByRowsAndColumsDto,
            List<DirectShapeDMFAData>
        >(
            [
                new List<DirectShapeDMFAData>
                {
                    _dto.BottomFaceDirectShapeDMFAData,
                    _dto.BottomInternalFaceDirectShapeDMFAData,
                    _dto.TopFaceDirectShapeDMFAData,
                    _dto.TopInternalFaceDirectShapeDMFAData,
                }
                .Concat(
                    _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData
                )
                .Concat(
                    _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData
                )
                .Concat(
                    _dto.ExternalVerticalFacesDirectShapeDMFADataList
                )
                .ToList(),
                XYZ.BasisZ.Negate(),
                CARDBOARD_THICKNESS,
                -10.0, // Start X
                -10.0, // Start Y
            ]
        );

        _telemetry.Add($"Total pieces placed at start point: {_dto.PiecesPlacedAtStartPoint.Count}");
    }

    public void PrepareSheetFamily(List<string> _telemetry)
    {
        _dto.SheetFamilySymbol = RunSubworkflow<
            RevitFamily_EntirelySetForUssageInRevitUI,
            RevitFamily_EntirelySetForUssageInRevitUIDto,
            FamilySymbol
        >(
            [SHEET_FAMILY_PATH, SHEET_FAMILY_NAME, SHEET_TYPE_NAME]
        );
    }

    public void GetPrintableAreaMetrics(List<string> _telemetry)
    {
        if (_dto?.SheetFamilySymbol == null)
        {
            _telemetry?.Add("Warning: SheetFamilySymbol in DTO is null. Metrics set to 0.0.");
            return;
        }

        _dto.SheetPrintableHeight = _dto.SheetFamilySymbol.LookupParameter("SheetDrawingAreaHeight")?.AsDouble() ?? 0.0;
        _dto.SheetPrintableWidth = _dto.SheetFamilySymbol.LookupParameter("SheetDrawingAreaWidth")?.AsDouble() ?? 0.0;
        _dto.SheetVerticalMargin = _dto.SheetFamilySymbol.LookupParameter("SheetVerticalMargin")?.AsDouble() ?? 0.0;
        _dto.SheetHorizontalMargin = _dto.SheetFamilySymbol.LookupParameter("SheetHorizontalMargin")?.AsDouble() ?? 0.0;

        _telemetry?.Add($"Metrics extracted: W:{_dto.SheetPrintableWidth}, H:{_dto.SheetPrintableHeight}");
    }

    public void GroupPiecesByWidthAndHeight(List<string> _telemetry)
    {
        var scaledPrintableHeight = _dto.SheetPrintableHeight * 20;
        var scaledPrintableWidth = _dto.SheetPrintableWidth * 20;
        var verticalFraction = (scaledPrintableHeight / 10); // 20
        var horizontalFraction = (scaledPrintableWidth / 10); // 20

        _telemetry.Add($"Vertical Fraction: {verticalFraction}, Horizontal Fraction: {horizontalFraction}");

        var piecesCount = _dto.PiecesPlacedAtStartPoint.Count;

        var orderedPiecesByHeight = new List<(int verticalFractionNumber, DirectShapeDMFAData piece)>();

        var verticalFractionCounter = verticalFraction;

        for (int i = 0; i < piecesCount; i++)
        {
            var currentPiece = _dto.PiecesPlacedAtStartPoint[i];

            while (currentPiece.MaxY - currentPiece.MinY > verticalFractionCounter)
            {
                verticalFractionCounter += verticalFraction;
            }

            orderedPiecesByHeight.Add(((int)(verticalFractionCounter / verticalFraction), currentPiece));

            verticalFractionCounter = verticalFraction;
        }

        orderedPiecesByHeight = orderedPiecesByHeight.OrderByDescending(a => a.verticalFractionNumber).ToList();

        var secondSortingPiecesCount = orderedPiecesByHeight.Count;
        var horizontalFractionCounter = horizontalFraction;

        var orderedPiecesByHeightAndWidth = new List<(int verticalFractionNumber, int horizontalFractionNumber, DirectShapeDMFAData piece)>();

        for (int i = 0; i < secondSortingPiecesCount; i++)
        {
            var currentPiece = orderedPiecesByHeight[i];

            while (currentPiece.piece.MaxX - currentPiece.piece.MinX > horizontalFractionCounter)
            {
                horizontalFractionCounter += horizontalFraction;
            }

            orderedPiecesByHeightAndWidth.Add((currentPiece.verticalFractionNumber, (int)(horizontalFractionCounter / horizontalFraction), currentPiece.piece));

            horizontalFractionCounter = horizontalFraction;
        }

        orderedPiecesByHeightAndWidth = orderedPiecesByHeightAndWidth
            .OrderByDescending(a => a.verticalFractionNumber)
            .ThenByDescending(a => a.horizontalFractionNumber)
            .ToList();

        //foreach (var item in orderedPiecesByHeightAndWidth)
        //{
        //    _telemetry.Add($"Piece V-Bin: {item.verticalFractionNumber}, H-Bin: {item.horizontalFractionNumber} | Dimensions: X({item.piece.MaxX - item.piece.MinX}), Y({item.piece.MaxY - item.piece.MinY})");
        //}

        var orderedPiecesByColumnsAndRows = new List<(int rowCapacity, int columnCapacity, DirectShapeDMFAData piece)>();

        foreach (var item in orderedPiecesByHeightAndWidth)
        {
            var itemHeight = item.piece.MaxY - item.piece.MinY;
            var itemWidth = item.piece.MaxX - item.piece.MinX;

            // CALCULATING CAPACITY: 
            // 10 / fractionNumber calculates how many times this piece can fit into the sheet dimension.
            var itemRowCapacity = (int)(10 / item.verticalFractionNumber);
            var itemColumnCapacity = (int)(10 / item.horizontalFractionNumber);

            // Added the actual values to the exception messages for easier debugging
            if (itemHeight > scaledPrintableHeight)
            {
                throw new Exception($"Piece height ({itemHeight:F2}) exceeds sheet printable height ({scaledPrintableHeight:F2}).");
            }
            else if (itemWidth > scaledPrintableWidth)
            {
                throw new Exception($"Piece width ({itemWidth:F2}) exceeds sheet printable width ({scaledPrintableWidth:F2}).");
            }
            else
            {
                orderedPiecesByColumnsAndRows.Add((itemRowCapacity, itemColumnCapacity, item.piece));
            }
        }

        //foreach (var item in orderedPiecesByColumnsAndRows)
        //{
        //    _telemetry.Add($"Piece Row Capacity: {item.rowCapacity}, Column Capacity: {item.columnCapacity} | Dimensions: X({item.piece.MaxX - item.piece.MinX:F2}), Y({item.piece.MaxY - item.piece.MinY:F2})");
        //}

        var groupedByCapacity = orderedPiecesByColumnsAndRows
            .GroupBy(item => (item.rowCapacity, item.columnCapacity))
            .ToList();

        // 2. Initialize your final nested list
        var finalNestedList = new List<List<DirectShapeDMFAData>>();

        // 3. Populate the nested list and log the results
        foreach (var group in groupedByCapacity)
        {
            // Extract just the pieces from this specific group into a List
            var piecesInGroup = group.Select(g => g.piece).ToList();

            // Add that list of pieces to the parent list
            finalNestedList.Add(piecesInGroup);

            // Log the group details
            _telemetry.Add($"Created Group [Row Cap: {group.Key.rowCapacity}, Col Cap: {group.Key.columnCapacity}] containing {piecesInGroup.Count} pieces.");
        }

        var finalSheetsList = new List<List<DirectShapeDMFAData>>();
        int sheetCounter = 1;

        // 2. Iterate through each size category
        foreach (var group in groupedByCapacity)
        {
            var rowCap = group.Key.rowCapacity;
            var colCap = group.Key.columnCapacity;

            // The maximum number of pieces of this size that can fit on a single sheet
            var maxPiecesPerSheet = rowCap * colCap;

            var currentSheet = new List<DirectShapeDMFAData>();

            foreach (var item in group)
            {
                currentSheet.Add(item.piece);

                // If the current sheet reaches its maximum capacity, "print" it and start a new one
                if (currentSheet.Count == maxPiecesPerSheet)
                {
                    finalSheetsList.Add(currentSheet);
                    _telemetry.Add($"Sheet {sheetCounter:D3} [FULL]: Contains {currentSheet.Count}/{maxPiecesPerSheet} pieces | Grid: {colCap}x{rowCap}");

                    sheetCounter++;
                    currentSheet = new List<DirectShapeDMFAData>(); // Reset for the next batch
                }
            }

            // After looping through all pieces in this category, we might have a partially filled sheet left over
            if (currentSheet.Count > 0)
            {
                finalSheetsList.Add(currentSheet);
                _telemetry.Add($"Sheet {sheetCounter:D3} [PARTIAL]: Contains {currentSheet.Count}/{maxPiecesPerSheet} pieces | Grid: {colCap}x{rowCap}");

                sheetCounter++;
            }
        }

        // Optional: Assign finalSheetsList to your DTO to pass it back to the main executor
        _dto.ArrangedSheets = finalSheetsList;
    }

    public void TranslateGroupedPiecesByPrintableDimensionsSquares(List<string> _telemetry)
    {
        // Using unscaled printable dimensions to ensure the physical elements 
        // fit exactly 1:1 as they will on the fabrication bed.
        var printableHeight = _dto.SheetPrintableHeight;
        var printableWidth = _dto.SheetPrintableWidth;

        // Define a gap between sheets so they don't overlap in the model space (e.g., 20% of sheet width)
        var sheetSpacingMargin = printableWidth * 0.2;

        int sheetIndex = 0;

        foreach (var sheet in _dto.ArrangedSheets)
        {
            if (sheet.Count == 0) continue;

            // Recalculate capacities based on the first piece of this specific sheet group
            var samplePiece = sheet.First();

            var verticalFraction = printableHeight / 10.0;
            var horizontalFraction = printableWidth / 10.0;

            int vBin = (int)Math.Ceiling((samplePiece.MaxY - samplePiece.MinY) / verticalFraction);
            int hBin = (int)Math.Ceiling((samplePiece.MaxX - samplePiece.MinX) / horizontalFraction);

            vBin = vBin > 0 ? vBin : 1;
            hBin = hBin > 0 ? hBin : 1;

            int colCapacity = Math.Max(1, 10 / hBin);
            int rowCapacity = Math.Max(1, 10 / vBin);

            double cellWidth = printableWidth / colCapacity;
            double cellHeight = printableHeight / rowCapacity;

            // Define the bottom-left origin point for this specific sheet layout in the model
            double currentSheetOriginX = sheetIndex * (printableWidth + sheetSpacingMargin);
            double currentSheetOriginY = 0;

            _telemetry.Add($"--- Laying out Sheet {sheetIndex + 1} ({sheet.Count} pieces) | Grid: {colCapacity}x{rowCapacity} ---");

            for (int i = 0; i < sheet.Count; i++)
            {
                var piece = sheet[i];

                int colIndex = i % colCapacity;
                int rowIndex = i / colCapacity;

                double targetX = currentSheetOriginX + (colIndex * cellWidth);
                double targetY = currentSheetOriginY + (rowIndex * cellHeight);

                piece.RequiredXDisplacement = targetX - piece.MinX;
                piece.RequiredYDisplacement = targetY - piece.MinY;
                piece.RequiredZDisplacement = 0;

                piece.PlacementPoint = new XYZ(targetX, targetY, 0);

                // PHYSICALLY MOVE THE ELEMENT IN REVIT
                XYZ translationVector = new XYZ(piece.RequiredXDisplacement, piece.RequiredYDisplacement, piece.RequiredZDisplacement);
                ElementTransformUtils.MoveElement(_doc, piece.DirectShape.Id, translationVector);

                piece.MinX += piece.RequiredXDisplacement;
                piece.MaxX += piece.RequiredXDisplacement;
                piece.MinY += piece.RequiredYDisplacement;
                piece.MaxY += piece.RequiredYDisplacement;
            }

            sheetIndex++;
        }
    }


    //public void TranslateGroupedPiecesByPrintableDimensionsSquares(List<string> _telemetry)
    //{
    //    // Using unscaled printable dimensions to ensure the physical elements 
    //    // fit exactly 1:1 as they will on the fabrication bed.
    //    var printableHeight = _dto.SheetPrintableHeight;
    //    var printableWidth = _dto.SheetPrintableWidth;

    //    // Define a gap between sheets so they don't overlap in the model space (e.g., 20% of sheet width)
    //    var sheetSpacingMargin = printableWidth * 0.2;

    //    int sheetIndex = 0;

    //    foreach (var sheet in _dto.ArrangedSheets)
    //    {
    //        // Skip empty sheet collections
    //        if (sheet.Count == 0) continue;

    //        // 1. Recalculate capacities based on the first piece of this specific sheet group
    //        var samplePiece = sheet.First();

    //        var verticalFraction = printableHeight / 10.0;
    //        var horizontalFraction = printableWidth / 10.0;

    //        int vBin = (int)Math.Ceiling((samplePiece.MaxY - samplePiece.MinY) / verticalFraction);
    //        int hBin = (int)Math.Ceiling((samplePiece.MaxX - samplePiece.MinX) / horizontalFraction);

    //        // Safety check to prevent divide by zero exceptions from the fraction side
    //        vBin = vBin > 0 ? vBin : 1;
    //        hBin = hBin > 0 ? hBin : 1;

    //        // ==========================================
    //        // CRITICAL FIX: Clamp to a minimum of 1
    //        // ==========================================
    //        // Prevents Modulo by Zero crash if Math.Ceiling pushes the fraction past 10.
    //        int colCapacity = Math.Max(1, 10 / hBin);
    //        int rowCapacity = Math.Max(1, 10 / vBin);

    //        // 2. Define the Grid Cell size in the coordinate space
    //        double cellWidth = printableWidth / colCapacity;
    //        double cellHeight = printableHeight / rowCapacity;

    //        // 3. Define the bottom-left origin point for this specific sheet
    //        double currentSheetOriginX = sheetIndex * (printableWidth + sheetSpacingMargin);
    //        double currentSheetOriginY = 0; // Align all sheets on the Y=0 baseline

    //        _telemetry.Add($"--- Laying out Sheet {sheetIndex + 1} ({sheet.Count} pieces) | Grid: {colCapacity}x{rowCapacity} | Origin X: {currentSheetOriginX:F2} ---");

    //        // 4. Place pieces into the grid
    //        for (int i = 0; i < sheet.Count; i++)
    //        {
    //            var piece = sheet[i];

    //            // Map the 1D list index 'i' to a 2D Grid (Column and Row)
    //            int colIndex = i % colCapacity;
    //            int rowIndex = i / colCapacity;

    //            // Calculate the target bottom-left corner for this piece within the sheet
    //            // X = Sheet Origin + (Column * Cell Width)
    //            double targetX = currentSheetOriginX + (colIndex * cellWidth);

    //            // Y = Sheet Origin + (Row * Cell Height)
    //            double targetY = currentSheetOriginY + (rowIndex * cellHeight);

    //            // Calculate Required Displacement. 
    //            // Subtracting the piece's original MinX/MinY ensures its bottom-left corner 
    //            // snaps exactly to the targetX/targetY coordinate.
    //            piece.RequiredXDisplacement = targetX - piece.MinX;
    //            piece.RequiredYDisplacement = targetY - piece.MinY;
    //            piece.RequiredZDisplacement = 0; // Set all pieces to Z 0

    //            // Assign the final Placement Point
    //            piece.PlacementPoint = new XYZ(targetX, targetY, 0);

    //            // ==========================================
    //            // PHYSICALLY MOVE THE ELEMENT IN REVIT
    //            // ==========================================
    //            XYZ translationVector = new XYZ(piece.RequiredXDisplacement, piece.RequiredYDisplacement, piece.RequiredZDisplacement);

    //            // Move the actual Revit DirectShape element to its new grid coordinate
    //            ElementTransformUtils.MoveElement(_doc, piece.DirectShape.Id, translationVector);

    //            // Update the Min/Max bounds to reflect their new real-world location 
    //            // (Crucial for any subsequent boundary checks, tagging, or exporting)
    //            piece.MinX += piece.RequiredXDisplacement;
    //            piece.MaxX += piece.RequiredXDisplacement;
    //            piece.MinY += piece.RequiredYDisplacement;
    //            piece.MaxY += piece.RequiredYDisplacement;

    //            _telemetry.Add($"Piece {i + 1} -> Grid[{rowIndex},{colIndex}] | Displaced by X:{piece.RequiredXDisplacement:F2}, Y:{piece.RequiredYDisplacement:F2}");
    //        }

    //        sheetIndex++;
    //    }
    //}

    public void GenerateSheetsForEachPiece(List<string> _telemetry)
    {
        // Assuming your DTO holds the active document. 
        // If it is stored elsewhere in your class, adjust this reference.
        Document doc = _doc;

        // 1. Calculate the exact center of the printable area.
        // Assuming the Title Block's origin (0,0) is at the bottom-left corner.
        double sheetCenterX = _dto.SheetHorizontalMargin + (_dto.SheetPrintableWidth / 2.0);
        double sheetCenterY = _dto.SheetVerticalMargin + (_dto.SheetPrintableHeight / 2.0);
        XYZ sheetPlacementPoint = new XYZ(sheetCenterX, sheetCenterY, 0);

        // 2. Retrieve the ViewFamilyType for 3D views to generate the fabrication views
        ViewFamilyType view3DType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

        if (view3DType == null)
        {
            _telemetry.Add("Error: Could not find a 3D ViewFamilyType in the document.");
            return;
        }

        // 3. Batch the creation process inside a single transaction for performance
        using (Transaction trans = new Transaction(doc, "Generate DFMA Fabrication Sheets"))
        {
            trans.Start();

            int sheetCount = 0;

            foreach (var item in _dto.PiecesPlacedAtStartPoint)
            {
                if (item.DirectShape == null) continue;

                // Create the Sheet
                ViewSheet sheet = ViewSheet.Create(doc, _dto.SheetFamilySymbol.Id);
                sheet.Name = $"DFMA Piece {item.DirectShape.Id}";
                sheet.SheetNumber = $"FAB-{sheetCount + 1:D3}";

                // Create a new 3D view for this specific piece
                View3D view = View3D.CreateIsometric(doc, view3DType.Id);

                // Set the required 1:20 scale
                view.Scale = 20;

                // Orient the view Top-Down (Orthographic) so the flat cardboard pieces are perfectly planar
                ViewOrientation3D topOrientation = new ViewOrientation3D(
                    XYZ.BasisZ,             // Eye position
                    XYZ.BasisY,             // Up direction
                    XYZ.BasisZ.Negate()     // Forward direction (looking straight down)
                );
                view.SetOrientation(topOrientation);

                // Isolate the element to prevent the rest of the model from showing up on the fabrication sheet
                view.IsolateElementTemporary(item.DirectShape.Id);

                // Configure the crop box around the piece's bounding box
                BoundingBoxXYZ bbox = item.DirectShape.get_BoundingBox(view);
                if (bbox != null)
                {
                    // Add a slight padding (e.g., 0.5 feet) so the crop box isn't touching the edge of the geometry
                    double padding = 0.5;
                    bbox.Min -= new XYZ(padding, padding, padding);
                    bbox.Max += new XYZ(padding, padding, padding);

                    view.CropBox = bbox;
                    view.CropBoxActive = true;
                    view.CropBoxVisible = false; // Hides the crop boundary lines on the sheet
                }

                // Place the Viewport centered in the calculated printable area
                if (Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
                {
                    Viewport.Create(doc, sheet.Id, view.Id, sheetPlacementPoint);
                    sheetCount++;
                }
                else
                {
                    _telemetry.Add($"Warning: Could not add view to {sheet.SheetNumber}.");
                }
            }

            trans.Commit();
            _telemetry.Add($"Successfully generated and placed {sheetCount} DFMA fabrication sheets.");
        }
    }

    public void GenerateSheetsForArrangedGroups(List<string> _telemetry)
    {
        Document doc = _doc;

        // 1. Calculate the exact center of the printable area.
        double sheetCenterX = _dto.SheetHorizontalMargin + (_dto.SheetPrintableWidth / 2.0);
        double sheetCenterY = _dto.SheetVerticalMargin + (_dto.SheetPrintableHeight / 2.0);
        XYZ sheetPlacementPoint = new XYZ(sheetCenterX, sheetCenterY, 0);

        // 2. Retrieve the ViewFamilyType for 3D views
        ViewFamilyType view3DType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

        if (view3DType == null)
        {
            _telemetry.Add("Error: Could not find a 3D ViewFamilyType in the document.");
            return;
        }

        using (Transaction trans = new Transaction(doc, "Generate DFMA Fabrication Sheets"))
        {
            trans.Start();

            int sheetCount = 0;

            // Iterate over the pre-arranged layout grids instead of individual pieces
            foreach (var pieceGroup in _dto.ArrangedSheets)
            {
                if (pieceGroup.Count == 0) continue;

                // Extract all DirectShape ElementIds for this specific layout group
                List<ElementId> groupIds = pieceGroup
                    .Where(p => p.DirectShape != null)
                    .Select(p => p.DirectShape.Id)
                    .ToList();

                if (groupIds.Count == 0) continue;

                // Create the Sheet
                ViewSheet sheet = ViewSheet.Create(doc, _dto.SheetFamilySymbol.Id);
                sheet.Name = $"DFMA Cut File - Bed {sheetCount + 1}";
                sheet.SheetNumber = $"FAB-{sheetCount + 1:D3}";

                // Create the View
                View3D view = View3D.CreateIsometric(doc, view3DType.Id);
                view.Scale = 20;

                ViewOrientation3D topOrientation = new ViewOrientation3D(
                    XYZ.BasisZ,
                    XYZ.BasisY,
                    XYZ.BasisZ.Negate()
                );
                view.SetOrientation(topOrientation);

                // Isolate ALL pieces belonging to this specific sheet group
                view.IsolateElementsTemporary(groupIds);

                // Calculate the combined bounding box for the entire arranged grid
                BoundingBoxXYZ combinedBBox = GetCombinedBoundingBox(pieceGroup, view);

                if (combinedBBox != null)
                {
                    double padding = 0.5;
                    combinedBBox.Min -= new XYZ(padding, padding, padding);
                    combinedBBox.Max += new XYZ(padding, padding, padding);

                    view.CropBox = combinedBBox;
                    view.CropBoxActive = true;
                    view.CropBoxVisible = false;
                }

                if (Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
                {
                    Viewport.Create(doc, sheet.Id, view.Id, sheetPlacementPoint);
                    sheetCount++;
                }
                else
                {
                    _telemetry.Add($"Warning: Could not add view to {sheet.SheetNumber}.");
                }
            }

            trans.Commit();
            _telemetry.Add($"Successfully generated and placed {sheetCount} grouped DFMA fabrication sheets.");
        }
    }

    /// <summary>
    /// Calculates a single overarching BoundingBoxXYZ for a grouped list of elements.
    /// </summary>
    private BoundingBoxXYZ GetCombinedBoundingBox(List<DirectShapeDMFAData> pieceGroup, View view)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        bool found = false;

        foreach (var piece in pieceGroup)
        {
            if (piece.DirectShape == null) continue;

            BoundingBoxXYZ bbox = piece.DirectShape.get_BoundingBox(view);
            if (bbox != null)
            {
                found = true;
                minX = Math.Min(minX, bbox.Min.X);
                minY = Math.Min(minY, bbox.Min.Y);
                minZ = Math.Min(minZ, bbox.Min.Z);

                maxX = Math.Max(maxX, bbox.Max.X);
                maxY = Math.Max(maxY, bbox.Max.Y);
                maxZ = Math.Max(maxZ, bbox.Max.Z);
            }
        }

        if (!found) return null;

        return new BoundingBoxXYZ
        {
            Min = new XYZ(minX, minY, minZ),
            Max = new XYZ(maxX, maxY, maxZ)
        };
    }
























    /////////////////////////////////
    /// Cleanup
    /////////////////////////////////
    public void HideInterestFloor(List<string> _telemetry)
    {
        _doc!.ActiveView.HideElements([_dto.InterestFloor.Id]);
    }
}

// -------------------------------------------------------------
// DTO Definition
// -------------------------------------------------------------

public class GenerateMarkedFloorsDFMADto : Dto
{
    public List<Floor> Floors { get; set; }
    public Floor InterestFloor { get; set; }
    public FloorDFMAData InterestFloorDFMAData { get; set; }

    public List<Line> BottomFaceOuterCurveLoopInternalOffsetBoundary { get; set; }
    public List<Line> TopFaceOuterCurveLoopInternalOffsetBoundary { get; set; }

    public DirectShapeDMFAData BottomFaceDirectShapeDMFAData { get; set; }
    public DirectShapeDMFAData BottomInternalFaceDirectShapeDMFAData { get; set; }
    public DirectShapeDMFAData TopFaceDirectShapeDMFAData { get; set; }
    public DirectShapeDMFAData TopInternalFaceDirectShapeDMFAData { get; set; }

    public List<Line> BottomFaceOffsetOuterCurveLoop { get; set; }
    public List<Line> BottomFaceOuterCurveLoopDisplacedLines { get; set; }
    public List<(List<Line>, string)> BottomFaceOuterCurveLoopDisplacedLinesPiecesContours { get; set; }


    public List<DirectShapeDMFAData> ExternalVerticalFacesDirectShapeDMFADataList { get; set; }


    [Print(nameof(TypeFormatter.Face))]
    public Face InternalBottomShapeTopFace { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> BottomShapeTopFaceVerticalSubdivisoryLines { get; set; }
    public List<(List<Line>, string)> BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours { get; set; }
    public List<DirectShapeDMFAData> BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData { get; set; }

    [Print(nameof(TypeFormatter.LineList))]
    public List<Line> BottomShapeTopFaceInitialHorizontalSubdivisoryLines { get; set; }
    public List<Line> BottomShapeTopFaceFinalHorizontalSubdivisoryLines { get; set; }
    public List<(List<Line>, string)> BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours { get; set; }
    public List<DirectShapeDMFAData> BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData { get; set; }


    public List<DirectShapeDMFAData> PiecesPlacedAtStartPoint { get; set; }



    public List<DFMANestedSheet> NestedLayoutSheets { get; set; }

    /// <summary>Plain snapshot for DXF export after the geometry TransactionGroup has assimilated.</summary>
    public List<DxfExportSheet> SheetsForDxfExport { get; set; }
    public FamilySymbol CommonCarboardFamilySymbol { get; set; }


    public FamilySymbol SheetFamilySymbol { get; set; }
    public double SheetPrintableHeight { get; set; }
    public double SheetVerticalMargin { get; set; }
    public double SheetPrintableWidth { get; set; }
    public double SheetHorizontalMargin { get; set; }
    public List<List<DirectShapeDMFAData>> ArrangedSheets { get; set; }
}

// -------------------------------------------------------------
// HELPER MODELS 
// -------------------------------------------------------------

public class DFMAPiece
{
    public string UniqueCode { get; set; }
    public XYZ Centroid { get; set; }
    public List<Line> FlattenedContours { get; set; }

    public double MinX { get; set; }
    public double MaxX { get; set; }
    public double MinY { get; set; }
    public double MaxY { get; set; }

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
}

public class DFMANestedSheet
{
    public int SheetNumber { get; set; }
    public List<DFMAPiece> PlacedPieces { get; set; } = new List<DFMAPiece>();
}