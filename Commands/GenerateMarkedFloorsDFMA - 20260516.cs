//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
//using Eobim.RevitApi.Core;
//using Eobim.RevitApi.DxfExport;
//using Eobim.RevitApi.Framework;
//using Eobim.RevitApi.MultiStepActions;
//using static RevitCurveLoop;

//namespace Eobim.RevitApi.Commands;

//[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
//public class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<GenerateMarkedFloorsDFMADto, object>
//{
//    /// <summary>Assimilate the geometry TransactionGroup after nesting (step 25); file export runs afterward.</summary>
//    //protected override int? TransactionGroupGeometryPhaseLastActionOneBased => 25;

//    protected override void OnAfterGeometryTransactionGroupBeforeFileIo()
//    {
//        if (_dto.NestedLayoutSheets is null)
//            throw new InvalidOperationException("NestedLayoutSheets must be populated before building the DXF snapshot.");

//        _dto.SheetsForDxfExport = DxfExportSnapshotBuilder.FromNestedLayoutSheets(_dto.NestedLayoutSheets);
//    }

//    private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

//    private const double SCALE = 20;
//    private const double ORIGINAL_THICKNESS = 0.0015;
//    private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits(((100/SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters);
//    private readonly double INTERNAL_SUPPORTS_SEPARATION_1 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 50;
//    private readonly double INTERNAL_SUPPORTS_SEPARATION_2 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 100;

//    // --- PHYSICAL STOCK CONSTANTS (e.g., 2400mm x 1200mm standard sheet) ---
//    private readonly double MAX_CARDBOARD_WIDTH = UnitUtils.ConvertToInternalUnits((SCALE * 2.4), UnitTypeId.Meters);
//    private readonly double MAX_CARDBOARD_HEIGHT = UnitUtils.ConvertToInternalUnits((SCALE * 1.2), UnitTypeId.Meters);

//    // --- FAMILY CONSTANTS & EXPORT PATHS ---
//    private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";
//    private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";
//    private readonly string CARDBOARD_TYPE_NAME = "Type 1";

//    private readonly string DXF_EXPORT_DIRECTORY = @"C:\Users\eduar\Desktop\Room_003\Revit2027\DXF_Exports";
//    private readonly string PDF_EXPORT_DIRECTORY = @"C:\Users\eduar\Desktop\Room_003\Revit2027\PDF_Exports";


//    public override void SafelyInitializeInputs(object[] args) { }

//    protected override void SetActions()
//    {
//        /* 1 */
//        Add(RunPrepareFamilyWorkflow);
//        /* 2 */
//        Add(GetAllFloors);
//        /* 3 */
//        Add(GetInterestFloorByMarkParameter);
//        /* 4 */
//        Add(GetInterestFloorDMFADataWorkflow);
//        /* 5 */
//        Add(ModelBottomFace);
//        /* 6 */
//        Add(GenerateCurveLoopInternalOffsetBoundaryWorkflow);
//        /* 7 */
//        Add(ModelBottomInternalFace);
//        /* 8 */
//        Add(ModelTopFace);
//        /* 9 */
//        Add(GenerateCurveLoopInternalOffsetBoundaryWorkflowForTopFace);
//        /* 10 */
//        Add(ModelTopInternalFace);
//        /* 11 */
//        Add(GenerateBottomFaceOffsetOuterCurveLoop);
//        /* 12 */
//        Add(GenerateBottomFaceOuterCurveLoopDisplacedLines);
//        /* 13 */
//        Add(GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops);
//        /* 14 */
//        Add(ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours);

//        /////////////////////////////////
//        /// Floor Vertical Internal Supports generation
//        /////////////////////////////////
//        /* 15 */
//        Add(GetInternalBottomShapeTopFace);
//        /* 16 */
//        Add(GenerateBottomShapeTopFaceVerticalSubdivisoryLines);
//        /* 17 */
//        Add(GenerateBottomShapeTopFaceVerticalSubdivisoryLinesContours);
//        /* 18 */
//        Add(ModelBottomShapeTopFaceVerticalSubdivisoryLines);

//        /////////////////////////////////
//        /// Floor Horizontal Internal Supports generation
//        /////////////////////////////////
//        /* 19 */
//        Add(GenerateBottomShapeTopFaceHorizontalSubdivisoryLines);
//        /* 20 */
//        Add(GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesIntersectionsWithVerticalSubdivisoryLines);
//        /* 21 */
//        Add(GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesContours);
//        /* 22 */
//        Add(ModelBottomShapeTopFaceHorizontalSubdivisoryLines);

//        ///////////////////////////////////
//        ///// Final Output & Fabrication (DXF & PDF)
//        ///////////////////////////////////
//        /* 23 */
//        Add(OrderlyPlaceFaces);
//        ///* 23 */
//        //Add(ExtractAndFlattenAllPiecesToZ0);
//        ///* 24 */
//        //Add(CalculateCentroidsAndAssignUniqueCodes);
//        ///* 25 */
//        //Add(NestPiecesIntoCardboardStockLimits);
//        ///* 26 */
//        //Add(ExportNestedLayoutToDXF);

//        //// Post–geometry-group: each DB-mutating step runs in its own Transaction (split group ended after step 25).
//        ///* 27 */
//        //Add(GenerateDraftingViewsFromNestedLayout, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        ///* 28 */
//        //Add(PlaceDraftingViewsOnAssemblySheets, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        ///* 29 */
//        //Add(ExportSheetsToPDF, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);

//        /////////////////////////////////
//        /// Cleanup
//        /////////////////////////////////
//        Add(HideInterestFloor, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//    }

//    public void RunPrepareFamilyWorkflow(List<string> _telemetry)
//    {
//        _dto.CommonCarboardFamilySymbol = RunSubworkflow<
//            RevitFamily_EntirelySetForUssageInRevitUI,
//            RevitFamily_EntirelySetForUssageInRevitUIDto,
//            FamilySymbol
//        >(
//            [CARDBOARD_FAMILY_PATH, CARDBOARD_FAMILY_NAME, CARDBOARD_TYPE_NAME]
//        );
//    }

//    /////////////////////////////////
//    /// Floor Data preparation
//    /////////////////////////////////
//    public void GetAllFloors(List<string> _telemetry)
//    {
//        var result = RevitFilteredElementCollector.ByBuiltInCategory<Floor>(_doc!, BuiltInCategory.OST_Floors);
//        if (result is null) throw new NullReferenceException();
//        if (result.Count.Equals(0)) throw new ArgumentOutOfRangeException($"Empty collection");
//        _telemetry.Add($"Floors count: {result.Count}");
//        _dto.Floors = result;
//    }

//    public void GetInterestFloorByMarkParameter(List<string> _telemetry)
//    {
//        var result = _dto.Floors!.FirstOrDefault(a =>
//        {
//            var parameter = a.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
//            if (parameter is null) return false;
//            if (!parameter.HasValue) return false;
//            if (!parameter.AsValueString().Equals(INTEREST_FLOOR_MARK)) return false;
//            return true;
//        });

//        if (result is null) throw new NullReferenceException();

//        _telemetry.Add($"Floor: {result.Id} | {result.Name}");

//        _dto.InterestFloor = result;
//    }

//    public void GetInterestFloorDMFADataWorkflow(List<string> _telemetry)
//    {
//        _dto.InterestFloorDFMAData = RunSubworkflow<
//            RevitDFMA_ExtractFloorData,
//            RevitDFMA_ExtractFloorDataDto,
//            FloorDFMAData
//        >(
//            [_dto.InterestFloor]
//        );
//    }

//    /////////////////////////////////
//    /// Bottom Face
//    /////////////////////////////////
//    public void ModelBottomFace(List<string> _telemetry)
//    {
//        _dto.BottomFaceDirectShapeDMFAData = RunSubworkflow<
//            DirectShape_ModelPlanarByBoundaryLines,
//            DirectShape_ModelByBoundaryLinesDto,
//            DirectShapeDMFAData
//        >(
//            [
//            _dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop.Select(a => a as Line).ToList()!,
//            XYZ.BasisZ.Negate(),
//            CARDBOARD_THICKNESS,
//            "BottomFace",
//            CARDBOARD_THICKNESS,
//            ]
//        );
//    }

//    public void GenerateCurveLoopInternalOffsetBoundaryWorkflow(List<string> _telemetry)
//    {
//        _dto.BottomFaceOuterCurveLoopInternalOffsetBoundary = RunSubworkflow<
//            CurveLoop_GenerateInnerOffsetBoundary,
//            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
//            List<Line>
//        >(
//            [_dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop!, CARDBOARD_THICKNESS, CARDBOARD_THICKNESS, XYZ.BasisZ.Negate()]
//        );
//    }

//    public void ModelBottomInternalFace(List<string> _telemetry)
//    {
//        _dto.BottomInternalFaceDirectShapeDMFAData = RunSubworkflow<
//            DirectShape_ModelPlanarByBoundaryLines,
//            DirectShape_ModelByBoundaryLinesDto,
//            DirectShapeDMFAData
//        >(
//            [_dto.BottomFaceOuterCurveLoopInternalOffsetBoundary!, XYZ.BasisZ, CARDBOARD_THICKNESS, "BottomInternalFace", 0.0]
//        );
//    }

//    /////////////////////////////////
//    /// Top Face
//    /////////////////////////////////
//    public void ModelTopFace(List<string> _telemetry)
//    {
//        _dto.TopFaceDirectShapeDMFAData = RunSubworkflow<
//            DirectShape_ModelPlanarByBoundaryLines,
//            DirectShape_ModelByBoundaryLinesDto,
//            DirectShapeDMFAData
//        >(
//            [_dto.InterestFloorDFMAData.TopFaceOuterCurveLoop.Select(a => a as Line).ToList()!, XYZ.BasisZ.Negate(), CARDBOARD_THICKNESS, "TopFace", 0.0]
//        );
//    }

//    public void GenerateCurveLoopInternalOffsetBoundaryWorkflowForTopFace(List<string> _telemetry)
//    {
//        _dto.TopFaceOuterCurveLoopInternalOffsetBoundary = RunSubworkflow<
//            CurveLoop_GenerateInnerOffsetBoundary,
//            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
//            List<Line>
//        >(
//            [_dto.InterestFloorDFMAData.TopFaceOuterCurveLoop!, CARDBOARD_THICKNESS, -CARDBOARD_THICKNESS, XYZ.BasisZ]
//        );
//    }

//    public void ModelTopInternalFace(List<string> _telemetry)
//    {
//        _dto.TopInternalFaceDirectShapeDMFAData = RunSubworkflow<
//            DirectShape_ModelPlanarByBoundaryLines,
//            DirectShape_ModelByBoundaryLinesDto,
//            DirectShapeDMFAData
//        >(
//            [_dto.TopFaceOuterCurveLoopInternalOffsetBoundary!, XYZ.BasisZ.Negate(), CARDBOARD_THICKNESS, "TopInternalFace", 0.0]
//        );
//    }

//    /////////////////////////////////
//    /// Vertical External Faces
//    /////////////////////////////////
//    private void GenerateBottomFaceOffsetOuterCurveLoop(List<string> telemetry)
//    {
//        _dto.BottomFaceOffsetOuterCurveLoop = RunSubworkflow<
//            CurveLoop_GenerateInnerOffsetBoundary,
//            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
//            List<Line>
//        >(
//            [_dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop, -CARDBOARD_THICKNESS / 2, CARDBOARD_THICKNESS, XYZ.BasisZ]
//        );
//    }

//    private void GenerateBottomFaceOuterCurveLoopDisplacedLines(List<string> telemetry)
//    {
//        _dto.BottomFaceOuterCurveLoopDisplacedLines = RunSubworkflow<
//            LineList_GenerateDisplacedLinesWorkflow,
//            LineList_GenerateDisplacedLinesWorkflowDto,
//            List<Line>
//        >(
//            [_dto.BottomFaceOffsetOuterCurveLoop, CARDBOARD_THICKNESS]
//        );
//    }

//    private void GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops(List<string> telemetry)
//    {
//        _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours = RunSubworkflow<
//            LineList_GenerateCurveLoopsFromLines,
//            LineList_GenerateCurveLoopsFromLinesDto,
//            List<(List<Line>, string)>
//        >(
//            [_dto.BottomFaceOuterCurveLoopDisplacedLines, CARDBOARD_THICKNESS, "", 0.0]
//        );
//    }

//    public void ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours(List<string> _telemetry)
//    {
//        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 2;
//        var contourCount = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours.Count;

//        _telemetry.Add($"BottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCount: {contourCount}");

//        for (int i = 0; i < contourCount; i++)
//        {
//            var item = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours[i];

//            RunSubworkflow<
//                DirectShape_ModelPlanarByBoundaryLines,
//                DirectShape_ModelByBoundaryLinesDto,
//                DirectShapeDMFAData
//            >(
//                [item.Item1, XYZ.BasisZ, pieceHeight, $"BottomFaceOuterCurveLoopDisplacedLinesPiecesContour_{i + 1}", 0.0]
//            );
//        }
//    }

//    /////////////////////////////////
//    /// Shape Internal Supports generation
//    /////////////////////////////////
//    public void GetInternalBottomShapeTopFace(List<string> _telemetry)
//    {
//        _dto.InternalBottomShapeTopFace = RunSubworkflow<
//            Face_FromDirectShape,
//            Face_FromDirectShapeDto,
//            Face
//        >(
//            [_dto.BottomInternalFaceDirectShapeDMFAData.DirectShape, "top"]
//        );
//    }

//    public void GenerateBottomShapeTopFaceVerticalSubdivisoryLines(List<string> _telemetry)
//    {
//        _dto.BottomShapeTopFaceVerticalSubdivisoryLines = RunSubworkflow<
//            Face_SubdivideInInternalLines,
//            Face_SubdivideInInternalLinesDto,
//            List<Line>
//        >(
//            [_dto.InternalBottomShapeTopFace, INTERNAL_SUPPORTS_SEPARATION_1, SubdivisionAxis.Y]
//        );
//    }

//    public void GenerateBottomShapeTopFaceVerticalSubdivisoryLinesContours(List<string> _telemetry)
//    {
//        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;

//        _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours = RunSubworkflow<
//            LineList_GenerateCurveLoopsFromLines,
//            LineList_GenerateCurveLoopsFromLinesDto,
//            List<(List<Line>, string)>
//        >(
//            [_dto.BottomShapeTopFaceVerticalSubdivisoryLines, CARDBOARD_THICKNESS, "vertical", pieceHeight]
//        );
//    }

//    public void ModelBottomShapeTopFaceVerticalSubdivisoryLines(List<string> _telemetry)
//    {
//        var result = new List<DirectShapeDMFAData>();

//        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;
//        var contourCount = _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours.Count;

//        _telemetry.Add($"BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContoursCount: {contourCount}");

//        for (int i = 0; i < contourCount; i++)
//        {
//            var item = _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours[i];
//            var extrusionDirection = XYZ.BasisZ;
            

//            if (item.Item2.Equals("vertical"))
//            {
//                extrusionDirection = item.Item1.FirstOrDefault()?.Direction.CrossProduct(XYZ.BasisZ) ?? XYZ.BasisZ;

//                pieceHeight = CARDBOARD_THICKNESS;
//            }

//            var piece = RunSubworkflow<
//                DirectShape_ModelPlanarByBoundaryLines,
//                DirectShape_ModelByBoundaryLinesDto,
//                DirectShapeDMFAData
//            >(
//                [item.Item1, extrusionDirection, pieceHeight, $"BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContour_{i + 1}", 0.0]
//            );

//            result.Add(piece);
//        }

//        _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData = result;
//    }

//    /////////////////////////////////
//    /// Floor Horizontal Internal Supports generation
//    /////////////////////////////////
//    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLines(List<string> _telemetry)
//    {
//        _dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines = RunSubworkflow<
//            Face_SubdivideInInternalLines,
//            Face_SubdivideInInternalLinesDto,
//            List<Line>
//        >(
//            [_dto.InternalBottomShapeTopFace, INTERNAL_SUPPORTS_SEPARATION_2, SubdivisionAxis.X]
//        );
//    }

//    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesIntersectionsWithVerticalSubdivisoryLines(List<string> _telemetry)
//    {
//        // 1. Upstream Null Check
//        if (_dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData == null)
//        {
//            throw new NullReferenceException("CRITICAL ERROR: '_dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData' is null.");
//        }

//        var verticalSupportContours = new List<List<Line>>();

//        foreach (var item in _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData)
//        {
//            if (item.DirectBottomFace == null) continue;

//            // 1. Get the direction THIS specific face is pointing
//            XYZ faceNormal = item.DirectBottomFace.ComputeNormal(new UV(0.5, 0.5));

//            // 2. Evaluate counter-clockwise against the face's own normal!
//            var validLoop = item.DirectBottomFace.GetEdgesAsCurveLoops()
//                .FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

//            if (validLoop != null)
//            {
//                var straightLinesForThisContour = validLoop.OfType<Line>().ToList();

//                if (straightLinesForThisContour.Any())
//                {
//                    verticalSupportContours.Add(straightLinesForThisContour);
//                }
//            }
//            else
//            {
//                _telemetry.Add($"Warning: Could not find a counter-clockwise loop for DirectShape {item.DirectShape?.Id}.");
//            }
//        }

//        _telemetry.Add($"Total vertical support contours extracted: {verticalSupportContours.Count}");

//        if (_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines == null)
//        {
//            throw new NullReferenceException("CRITICAL ERROR: '_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines' is null.");
//        }

//        _dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines = RunSubworkflow<
//            Line_SubdivideByContourIntersection,
//            Line_SubdivideByContourIntersectionDto,
//            List<Line>
//        >(
//            [_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines, verticalSupportContours]
//            //[_dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines, _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours]
//        );
//    }

//    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesContours(List<string> _telemetry)
//    {
//        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;

//        _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours = RunSubworkflow<
//            LineList_GenerateCurveLoopsFromLines,
//            LineList_GenerateCurveLoopsFromLinesDto,
//            List<(List<Line>, string)>
//        >(
//            [_dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines, CARDBOARD_THICKNESS, "vertical", pieceHeight]
//        );
//    }

//    public void ModelBottomShapeTopFaceHorizontalSubdivisoryLines(List<string> _telemetry)
//    {
//        var result = new List<DirectShapeDMFAData>();

//        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;
//        var contourCount = _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours.Count;

//        _telemetry.Add($"BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContoursCount: {contourCount}");

//        for (int i = 0; i < contourCount; i++)
//        {
//            //var item = _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours[i];

//            //var piece = RunSubworkflow<
//            //    DirectShape_ModelPlanarByBoundaryLines,
//            //    DirectShape_ModelByBoundaryLinesDto,
//            //    DirectShapeDMFAData
//            //>(
//            //    [item.Item1, XYZ.BasisZ, pieceHeight, $"BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContour_{i + 1}", 0.0]
//            //);


//            var item = _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours[i];
//            var extrusionDirection = XYZ.BasisZ;


//            if (item.Item2.Equals("vertical"))
//            {
//                extrusionDirection = item.Item1.FirstOrDefault()?.Direction.CrossProduct(XYZ.BasisZ) ?? XYZ.BasisZ;

//                pieceHeight = CARDBOARD_THICKNESS;
//            }

//            var piece = RunSubworkflow<
//                DirectShape_ModelPlanarByBoundaryLines,
//                DirectShape_ModelByBoundaryLinesDto,
//                DirectShapeDMFAData
//            >(
//                [item.Item1, extrusionDirection, pieceHeight, $"BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContour_{i + 1}", 0.0]
//            );

//            result.Add(piece);


//        }

//        _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData = result;
//    }

//    /////////////////////////////////
//    /// Final Output & Fabrication
//    /////////////////////////////////
//    ///


//    public void OrderlyPlaceFaces(List<string> _telemetry)
//    {
//        RunSubworkflow<
//            DFMA_PlacePiecesByRowsAndColums,
//            DFMA_PlacePiecesByRowsAndColumsDto,
//            List<DirectShapeDMFAData>
//        >(
//            [
//                new List<DirectShapeDMFAData>
//                {
//                    _dto.TopFaceDirectShapeDMFAData,
//                    _dto.BottomFaceDirectShapeDMFAData
//                }
//                .Concat(
//                    _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData
//                )
//                .Concat(
//                    _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData
//                )
//                .ToList(),
//                XYZ.BasisZ.Negate(),
//                CARDBOARD_THICKNESS,
//            ]
//        );

//    }

















//    public void ExtractAndFlattenAllPiecesToZ0(List<string> _telemetry)
//    {
//        _dto.FlattenedPieces = RunSubworkflow<
//            Pieces_ExtractAndFlattenWorkflow,
//            Pieces_ExtractAndFlattenDto,
//            List<DFMAPiece>
//        >(
//            [
//                _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours,
//                _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours,
//                _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours
//            ]
//        );
//        _telemetry.Add($"Total 2D pieces extracted: {_dto.FlattenedPieces.Count}");
//    }

//    public void CalculateCentroidsAndAssignUniqueCodes(List<string> _telemetry)
//    {
//        _dto.CodedPieces = RunSubworkflow<
//            Pieces_AssignCentroidsAndCodesWorkflow,
//            Pieces_AssignCentroidsAndCodesDto,
//            List<DFMAPiece>
//        >(
//            [_dto.FlattenedPieces]
//        );
//    }

//    public void NestPiecesIntoCardboardStockLimits(List<string> _telemetry)
//    {
//        _dto.NestedLayoutSheets = RunSubworkflow<
//            Pieces_NestIntoCardboardSheetsWorkflow,
//            Pieces_NestIntoCardboardSheetsDto,
//            List<DFMANestedSheet>
//        >(
//            [_dto.CodedPieces, MAX_CARDBOARD_WIDTH, MAX_CARDBOARD_HEIGHT]
//        );
//        _telemetry.Add($"Total physical cardboard sheets required: {_dto.NestedLayoutSheets.Count}");
//    }

//    public void ExportNestedLayoutToDXF(List<string> _telemetry)
//    {
//        try
//        {
//            if (_dto.SheetsForDxfExport is null || _dto.SheetsForDxfExport.Count == 0)
//            {
//                _telemetry.Add("DXF export skipped: no POCO snapshot (SheetsForDxfExport).");
//                return;
//            }

//            var exportSuccess = RunSubworkflow<
//                Export_NestedSheetsToDXFWorkflow,
//                Export_NestedSheetsToDXFDto,
//                bool
//            >(
//                [_dto.SheetsForDxfExport, DXF_EXPORT_DIRECTORY]
//            );

//            if (!exportSuccess) _telemetry.Add("Warning: DXF Export failed or was partially incomplete.");
//        }
//        catch (Exception ex)
//        {
//            _telemetry.Add($"DXF export exception (model already committed through nesting): {ex.Message}");
//        }
//    }

//    public void GenerateDraftingViewsFromNestedLayout(List<string> _telemetry)
//    {
//        _dto.AssemblyDraftingViews = RunSubworkflow<
//            RevitViews_CreateDraftingViewsForNestedSheetsWorkflow,
//            RevitViews_CreateDraftingViewsDto,
//            List<ViewDrafting>
//        >(
//            [_dto.NestedLayoutSheets]
//        );
//    }

//    public void PlaceDraftingViewsOnAssemblySheets(List<string> _telemetry)
//    {
//        _dto.AssemblySheets = RunSubworkflow<
//            RevitSheets_CreateAndPlaceDraftingViewsWorkflow,
//            RevitSheets_CreateAndPlaceDraftingViewsDto,
//            List<ViewSheet>
//        >(
//            [_dto.AssemblyDraftingViews]
//        );
//    }

//    public void ExportSheetsToPDF(List<string> _telemetry)
//    {
//        try
//        {
//            if (_dto.AssemblySheets is null || _dto.AssemblySheets.Count == 0)
//            {
//                _telemetry.Add("PDF export skipped: no assembly sheets (run sheet placement first).");
//                return;
//            }

//            var pdfSuccess = RunSubworkflow<
//                Export_SheetsToPDFWorkflow,
//                Export_SheetsToPDFDto,
//                bool
//            >(
//                [_dto.AssemblySheets, PDF_EXPORT_DIRECTORY]
//            );

//            if (!pdfSuccess) _telemetry.Add("Warning: PDF export completed unsuccessfully.");
//        }
//        catch (Exception ex)
//        {
//            _telemetry.Add($"PDF export exception (geometry and sheets may already be committed): {ex.Message}");
//        }
//    }

//    /////////////////////////////////
//    /// Cleanup
//    /////////////////////////////////
//    public void HideInterestFloor(List<string> _telemetry)
//    {
//        _doc!.ActiveView.HideElements([_dto.InterestFloor.Id]);
//    }
//}

//// -------------------------------------------------------------
//// DTO Definition
//// -------------------------------------------------------------

//public class GenerateMarkedFloorsDFMADto : Dto
//{
//    public List<Floor> Floors { get; set; }
//    public Floor InterestFloor { get; set; }
//    public FloorDFMAData InterestFloorDFMAData { get; set; }

//    public List<Line> BottomFaceOuterCurveLoopInternalOffsetBoundary { get; set; }
//    public List<Line> TopFaceOuterCurveLoopInternalOffsetBoundary { get; set; }

//    public DirectShapeDMFAData BottomFaceDirectShapeDMFAData { get; set; }
//    public DirectShapeDMFAData BottomInternalFaceDirectShapeDMFAData { get; set; }
//    public DirectShapeDMFAData TopFaceDirectShapeDMFAData { get; set; }
//    public DirectShapeDMFAData TopInternalFaceDirectShapeDMFAData { get; set; }

//    public List<Line> BottomFaceOffsetOuterCurveLoop { get; set; }
//    public List<Line> BottomFaceOuterCurveLoopDisplacedLines { get; set; }
//    public List<(List<Line>, string)> BottomFaceOuterCurveLoopDisplacedLinesPiecesContours { get; set; }

//    [Print(nameof(TypeFormatter.Face))]
//    public Face InternalBottomShapeTopFace { get; set; }

//    [Print(nameof(TypeFormatter.LineList))]
//    public List<Line> BottomShapeTopFaceVerticalSubdivisoryLines { get; set; }
//    public List<(List<Line>, string)> BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours { get; set; }
//    public List<DirectShapeDMFAData> BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData { get; set; }

//    [Print(nameof(TypeFormatter.LineList))]
//    public List<Line> BottomShapeTopFaceInitialHorizontalSubdivisoryLines { get; set; }
//    public List<Line> BottomShapeTopFaceFinalHorizontalSubdivisoryLines { get; set; }
//    public List<(List<Line>, string)> BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours { get; set; }
//    public List<DirectShapeDMFAData> BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData { get; set; }

//    // --- FINAL OUTPUT PROPERTIES ---
//    public List<DFMAPiece> FlattenedPieces { get; set; }
//    public List<DFMAPiece> CodedPieces { get; set; }
//    public List<DFMANestedSheet> NestedLayoutSheets { get; set; }

//    /// <summary>Plain snapshot for DXF export after the geometry TransactionGroup has assimilated.</summary>
//    public List<DxfExportSheet> SheetsForDxfExport { get; set; }

//    public List<ViewDrafting> AssemblyDraftingViews { get; set; }
//    public List<ViewSheet> AssemblySheets { get; set; }

//    public List<Line> AdjustedInZCoordinateOuterCurveLoopDisplacedLines { get; set; }
//    public CurveLoopSegmentationFrame OuterCurveLoopSegmentationFrame { get; set; }
//    public FamilySymbol CommonCarboardFamilySymbol { get; set; }
//    public Family CommonCarboardFamily { get; set; }
//    public List<FamilyInstance> OuterBorderPlacedFamilyInstances { get; set; }
//}

//// -------------------------------------------------------------
//// HELPER MODELS 
//// -------------------------------------------------------------

//public class DFMAPiece
//{
//    public string UniqueCode { get; set; }
//    public XYZ Centroid { get; set; }
//    public List<Line> FlattenedContours { get; set; }

//    public double MinX { get; set; }
//    public double MaxX { get; set; }
//    public double MinY { get; set; }
//    public double MaxY { get; set; }

//    public double Width => MaxX - MinX;
//    public double Height => MaxY - MinY;
//}

//public class DFMANestedSheet
//{
//    public int SheetNumber { get; set; }
//    public List<DFMAPiece> PlacedPieces { get; set; } = new List<DFMAPiece>();
//}