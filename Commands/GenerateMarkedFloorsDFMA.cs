using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.DxfExport;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<GenerateMarkedFloorsDFMADto, object>
{
    /// <summary>Assimilate the geometry TransactionGroup after nesting (step 25); file export runs afterward.</summary>
    //protected override int? TransactionGroupGeometryPhaseLastActionOneBased => 25;

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
        /* 25 */
        Add(GenerateSheetsForEachPiece);


        /////////////////////////////////
        /// Cleanup
        /////////////////////////////////
        Add(HideInterestFloor, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
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
                //.Concat(
                //    _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData
                //)
                //.Concat(
                //    _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData
                //)
                //.Concat(
                //    _dto.ExternalVerticalFacesDirectShapeDMFADataList
                //)
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



    // --- FINAL OUTPUT PROPERTIES ---
    public List<DFMAPiece> FlattenedPieces { get; set; }
    public List<DFMAPiece> CodedPieces { get; set; }
    public List<DFMANestedSheet> NestedLayoutSheets { get; set; }

    /// <summary>Plain snapshot for DXF export after the geometry TransactionGroup has assimilated.</summary>
    public List<DxfExportSheet> SheetsForDxfExport { get; set; }

    public List<ViewDrafting> AssemblyDraftingViews { get; set; }
    public List<ViewSheet> AssemblySheets { get; set; }

    public List<Line> AdjustedInZCoordinateOuterCurveLoopDisplacedLines { get; set; }
    public CurveLoopSegmentationFrame OuterCurveLoopSegmentationFrame { get; set; }
    public FamilySymbol CommonCarboardFamilySymbol { get; set; }
    public Family CommonCarboardFamily { get; set; }
    public List<FamilyInstance> OuterBorderPlacedFamilyInstances { get; set; }


    public FamilySymbol SheetFamilySymbol { get; set; }
    public double SheetPrintableHeight { get; set; }
    public double SheetVerticalMargin { get; set; }
    public double SheetPrintableWidth { get; set; }
    public double SheetHorizontalMargin { get; set; }
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