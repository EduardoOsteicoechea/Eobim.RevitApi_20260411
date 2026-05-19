using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.DFMA;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;


namespace Eobim.RevitApi.Commands;


[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public partial class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<bool, GenerateMarkedFloorsDFMADto, object>
{
    protected override void OnAfterGeometryTransactionGroupBeforeFileIo() { }

    private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

    //private const double SCALE = 20;
    //private const double ORIGINAL_THICKNESS = 0.0015;
    //private readonly double CONVERTED_THICKNESS = UnitUtils.ConvertToInternalUnits(ORIGINAL_THICKNESS, UnitTypeId.Meters);
    //private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters);
    //private readonly double INTERNAL_SUPPORTS_SEPARATION_1 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 50;
    //private readonly double INTERNAL_SUPPORTS_SEPARATION_2 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 100;

    // --- SCALING & THICKNESS MATH FIXED ---
    private const double SCALE = 20.0;
    private const double ORIGINAL_THICKNESS = 0.00175; // 1.5mm in meters

    // 1. Literal real-world thickness (1.5mm) converted to Revit internal units (Feet)
    private readonly double CONVERTED_THICKNESS = UnitUtils.ConvertToInternalUnits(ORIGINAL_THICKNESS, UnitTypeId.Meters);
    private readonly double SCALE_MULTIPLIER = UnitUtils.ConvertToInternalUnits(ORIGINAL_THICKNESS, UnitTypeId.Meters);

    // 2. Scaled thickness for the 1:1 Revit Model (1.5mm * 20 = 30mm). This is what creates accurate slots!
    private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits(ORIGINAL_THICKNESS * SCALE, UnitTypeId.Meters);

    // 3. Spacing derived accurately from the scaled thickness
    private readonly double INTERNAL_SUPPORTS_SEPARATION_1 = UnitUtils.ConvertToInternalUnits((ORIGINAL_THICKNESS * SCALE) * (SCALE * 1), UnitTypeId.Meters);
    private readonly double INTERNAL_SUPPORTS_SEPARATION_2 = UnitUtils.ConvertToInternalUnits((ORIGINAL_THICKNESS * SCALE) * (SCALE * 2), UnitTypeId.Meters);

    // --- FAMILY CONSTANTS & EXPORT PATHS ---
    private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";
    private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";
    private readonly string CARDBOARD_TYPE_NAME = "Type 1";

    // --- SHEET FAMILY CONSTANTS & EXPORT PATHS ---
    private readonly string SHEET_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Letter_Sheet_001.rfa";
    private readonly string SHEET_FAMILY_NAME = "Letter_Sheet_001";
    private readonly string SHEET_TYPE_NAME = "Margin_only";

    private readonly string EXPORT_FOLDER_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\PDF_Exports";


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

        /////////////////////////////////
        /// Final Output & Fabrication (DXF & PDF)
        /////////////////////////////////
        /* 23 */
        Add(OrderlyPlaceFaces);
        /* 24 */
        Add(PrepareSheetFamily);
        /* 25 */
        Add(GetPrintableAreaMetrics);
        /* 26 */
        Add(ArrangePiecesInGroups, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 27 */
        Add(GenerateSheetsForArrangedGroups, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 28 */
        Add(ExportSheetsToPDF);

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
            new(
                FamilyPath: CARDBOARD_FAMILY_PATH,
                FamilyName: CARDBOARD_FAMILY_NAME,
                FamilyTypeName: CARDBOARD_TYPE_NAME
            )
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
            RevitDFMA_ExtractFloorDataArgs,
            RevitDFMA_ExtractFloorData,
            RevitDFMA_ExtractFloorDataDto,
            FloorDFMAData
        >(
            new(
                InterestFloor: _dto.InterestFloor
            )
        );
    }


    /////////////////////////////////
    /////////////////////////////////
    /// Bottom Face
    /////////////////////////////////
    /////////////////////////////////
    public void ModelBottomFace(List<string> _telemetry)
    {
        var contour = new PieceContour 
        {
            ContourLines = _dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop.Select(a => a as Line).ToList()!
        };

        _dto.BottomFaceDirectShapeDMFAData = RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLinesArgs,
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            new(
                PieceContour: contour,
                ExtrusionDirection: XYZ.BasisZ.Negate(),
                ExtrusionThickness: CARDBOARD_THICKNESS,
                DirectShapeName: "BottomFace",
                HeightAdjustment: CARDBOARD_THICKNESS
            )
        );
    }

    public void GenerateCurveLoopInternalOffsetBoundaryWorkflow(List<string> _telemetry)
    {
        _dto.BottomFaceOuterCurveLoopInternalOffsetBoundary = RunSubworkflow<
            CurveLoop_GenerateInnerOffsetBoundaryArgs,
            CurveLoop_GenerateInnerOffsetBoundary,
            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
            List<Line>
        >(
            new(
                CurveLoop: _dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop!,
                Offset: CARDBOARD_THICKNESS,
                HeightAdjustment: CARDBOARD_THICKNESS,
                FaceDirection: XYZ.BasisZ.Negate()
            )
        );
    }

    public void ModelBottomInternalFace(List<string> _telemetry)
    {
        var contour = new PieceContour
        {
            ContourLines = _dto.BottomFaceOuterCurveLoopInternalOffsetBoundary!
        };

        _dto.BottomInternalFaceDirectShapeDMFAData = RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLinesArgs,
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            new(
                PieceContour: contour,
                ExtrusionDirection: XYZ.BasisZ.Negate(),
                ExtrusionThickness: CARDBOARD_THICKNESS,
                DirectShapeName: "BottomInternalFace",
                HeightAdjustment: CARDBOARD_THICKNESS
            )
        );
    }


    /////////////////////////////////
    /////////////////////////////////
    /// Top Face
    /////////////////////////////////
    /////////////////////////////////
    public void ModelTopFace(List<string> _telemetry)
    {
        var contour = new PieceContour
        {
            ContourLines = _dto.InterestFloorDFMAData.TopFaceOuterCurveLoop.Select(a => a as Line).ToList()!
        };

        _dto.TopFaceDirectShapeDMFAData = RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLinesArgs,
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            new(
                PieceContour: contour,
                ExtrusionDirection: XYZ.BasisZ.Negate(),
                ExtrusionThickness: CARDBOARD_THICKNESS,
                DirectShapeName: "TopFace",
                HeightAdjustment: 0.0
            )
        );
    }

    public void GenerateCurveLoopInternalOffsetBoundaryWorkflowForTopFace(List<string> _telemetry)
    {
        _dto.TopFaceOuterCurveLoopInternalOffsetBoundary = RunSubworkflow<
            CurveLoop_GenerateInnerOffsetBoundaryArgs,
            CurveLoop_GenerateInnerOffsetBoundary,
            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
            List<Line>
        >(
            new(
                CurveLoop: _dto.InterestFloorDFMAData.TopFaceOuterCurveLoop!,
                Offset: CARDBOARD_THICKNESS,
                HeightAdjustment: -CARDBOARD_THICKNESS,
                FaceDirection: XYZ.BasisZ
            )
        );
    }

    public void ModelTopInternalFace(List<string> _telemetry)
    {
        var contour = new PieceContour
        {
            ContourLines = _dto.TopFaceOuterCurveLoopInternalOffsetBoundary!
        };

        _dto.TopInternalFaceDirectShapeDMFAData = RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLinesArgs,
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            new(
                PieceContour: contour,
                ExtrusionDirection: XYZ.BasisZ.Negate(),
                ExtrusionThickness: CARDBOARD_THICKNESS,
                DirectShapeName: "TopInternalFace",
                HeightAdjustment: 0.0
            )
        );
    }


    /////////////////////////////////
    /////////////////////////////////
    /// Vertical External Faces
    /////////////////////////////////
    /////////////////////////////////
    public void GenerateBottomFaceOffsetOuterCurveLoop(List<string> telemetry)
    {
        _dto.BottomFaceOffsetOuterCurveLoop = RunSubworkflow<
            CurveLoop_GenerateInnerOffsetBoundaryArgs,
            CurveLoop_GenerateInnerOffsetBoundary,
            RevitCurveLoop_GenerateInnerOffsetBoundaryDto,
            List<Line>
        >(
            new(
                CurveLoop: _dto.InterestFloorDFMAData.BottomFaceOuterCurveLoop!,
                Offset: -CARDBOARD_THICKNESS / 2,
                HeightAdjustment: CARDBOARD_THICKNESS,
                FaceDirection: XYZ.BasisZ
            )
        );
    }

    public void GenerateBottomFaceOuterCurveLoopDisplacedLines(List<string> telemetry)
    {
        _dto.BottomFaceOuterCurveLoopDisplacedLines = RunSubworkflow<
            LineList_GenerateDisplacedLinesWorkflowArgs,
            LineList_GenerateDisplacedLinesWorkflow,
            LineList_GenerateDisplacedLinesWorkflowDto,
            List<Line>
        >(
            new(
                InputLineList: _dto.BottomFaceOffsetOuterCurveLoop,
                DisplacementThickness: CARDBOARD_THICKNESS,
                DisplacementValue: 0.0
            )
        );
    }

    public void GenerateBottomFaceOuterCurveLoopDisplacedLinesPiecesContoursCurveLoops(List<string> _telemetry)
    {
        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 2;

        _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours = GenerateDirectionSharingPieceContours(
                _dto.BottomFaceOuterCurveLoopDisplacedLines,
                pieceHeight,
                ContourWorkplaneAlignmentOptions.ZAndCustomAngle,
                _telemetry
            );
    }

    public void ModelBottomFaceOuterCurveLoopDisplacedLinesPiecesContours(List<string> _telemetry)
    {
        var pieces = new List<DirectShapeDMFAData>();

        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 2;
        var contourCount = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours.Count;

        _telemetry.Add($"{nameof(contourCount)}: {contourCount}");

        for (int i = 0; i < contourCount; i++)
        {
            var item = _dto.BottomFaceOuterCurveLoopDisplacedLinesPiecesContours[i];

            _telemetry.Add($"    Contour {i + 1}");
            _telemetry.Add($"    {item}");

            pieces.Add(
                ModelFromContourAndDirection(
                    item,
                    pieceIndex: i,
                    pieceHeight: pieceHeight,
                    nameBase: "BottomFaceOuterCurveLoopDisplacedLinesPiecesContour",
                    heightAdjustment: 0.0,
                    negateDirection: false
                )
            );
        }

        _dto.ExternalVerticalFacesDirectShapeDMFADataList = pieces;
    }


    /////////////////////////////////
    /////////////////////////////////
    /// Floor Vertical Internal Supports generation
    /////////////////////////////////
    /////////////////////////////////
    public void GetInternalBottomShapeTopFace(List<string> _telemetry)
    {
        _dto.InternalBottomShapeTopFace = RunSubworkflow<
            Face_FromDirectShapeArgs,
            Face_FromDirectShape,
            Face_FromDirectShapeDto,
            Face
        >(
            new(
                DirectShape: _dto.BottomInternalFaceDirectShapeDMFAData.DirectShape,
                FaceToObtain: "top"
            )
        );
    }

    public void GenerateBottomShapeTopFaceVerticalSubdivisoryLines(List<string> _telemetry)
    {
        _dto.BottomShapeTopFaceVerticalSubdivisoryLines = RunSubworkflow<
            Face_SubdivideInInternalLinesArgs,
            Face_SubdivideInInternalLines,
            Face_SubdivideInInternalLinesDto,
            List<Line>
        >(
            new(
                FaceToSubdivide: _dto.InternalBottomShapeTopFace,
                SubdivisionSeparation: INTERNAL_SUPPORTS_SEPARATION_1,
                SubdivisionBasis: SubdivisionAxis.X
            )
        );
    }

    public void GenerateBottomShapeTopFaceVerticalSubdivisoryLinesContours(List<string> _telemetry)
    {
        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;

        _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours = GenerateDirectionSharingPieceContours(
                _dto.BottomShapeTopFaceVerticalSubdivisoryLines,
                pieceHeight,
                ContourWorkplaneAlignmentOptions.ZAndCustomAngle,
                _telemetry
            );

        foreach (var item in _dto.BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours)
        {
            _telemetry.Add($"{item}");
        }
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
                    item,
                    pieceIndex: i,
                    pieceHeight: pieceHeight,
                    nameBase: "BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContour"
                )
            );
        }

        _dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData = pieces;
    }


    /////////////////////////////////
    /////////////////////////////////
    /// Floor Horizontal Internal Supports generation
    /////////////////////////////////
    /////////////////////////////////
    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLines(List<string> _telemetry)
    {
        _dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines = RunSubworkflow<
            Face_SubdivideInInternalLinesArgs,
            Face_SubdivideInInternalLines,
            Face_SubdivideInInternalLinesDto,
            List<Line>
        >(
            new(
                FaceToSubdivide: _dto.InternalBottomShapeTopFace,
                SubdivisionSeparation: INTERNAL_SUPPORTS_SEPARATION_2,
                SubdivisionBasis: SubdivisionAxis.Y
            )
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
            if (item.DirectBottomFace == null) throw new Exception($"Missing bottom face on direct Shape: {item.DirectShape.Id}");

            var validLoop = item.DirectBottomFace.GetEdgesAsCurveLoops().First();

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
            Line_SubdivideByContourIntersectionArgs,
            Line_SubdivideByContourIntersection,
            Line_SubdivideByContourIntersectionDto,
            List<Line>
        >(
            new(
                LinesToSubdivide: _dto.BottomShapeTopFaceInitialHorizontalSubdivisoryLines,
                Contours: verticalSupportContours
            )
        );

        _telemetry.Add($"{nameof(_dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines)}: {_dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines.Count}");

        foreach (var item in _dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines)    
        {
            _telemetry.Add($"Line: {item.GetEndPoint(0)}, {item.GetEndPoint(1)}");
        }
    }

    public void GenerateBottomShapeTopFaceHorizontalSubdivisoryLinesContours(List<string> _telemetry)
    {
        var pieceHeight = (_dto.InterestFloorDFMAData.TopFaceHighestPoint.Z - _dto.InterestFloorDFMAData.BottomFaceLowestPoint.Z) - CARDBOARD_THICKNESS * 4;

        _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours = GenerateDirectionSharingPieceContours(
                _dto.BottomShapeTopFaceFinalHorizontalSubdivisoryLines,
                pieceHeight,
                ContourWorkplaneAlignmentOptions.ZAndCustomAngle,
                _telemetry
            );

        foreach (var item in _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours)
        {
            _telemetry.Add($"{item}");
        }
    }

    private List<PieceContour> GenerateDirectionSharingPieceContours(
        List<Line> lines,
        double pieceHeight,
        ContourWorkplaneAlignmentOptions contourWorkplaneAlignmentOption,
        List<string> _telemetry
        )
    {
        var result = new List<PieceContour>();

        foreach (var item in lines)
        {
            _telemetry.Add($"Processing line for contour generation: Start({item.GetEndPoint(0)}), End({item.GetEndPoint(1)})");

            var contour = RunSubworkflow<
                DFMA_GeneratePieceFromLineArgs,
                DFMA_GeneratePieceFromLine,
                DFMA_GeneratePieceFromLineDto,
                PieceContour
            >(
                new(
                    InputLine: item,
                    ContourThickness: CARDBOARD_THICKNESS,
                    ContourWorkplaneAlignmentOption: contourWorkplaneAlignmentOption,
                    VerticalContourHeight: pieceHeight
                )
            );

            result.Add(contour);
        }

        return result;
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
                    item,
                    pieceIndex: i,
                    pieceHeight: pieceHeight,
                    nameBase: "BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContour",
                    heightAdjustment: 0.0,
                    negateDirection: true
                )
            );
        }

        _dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData = pieces;
    }

    private DirectShapeDMFAData ModelFromContourAndDirection
    (
        PieceContour contour,
        int pieceIndex,
        double pieceHeight,
        string nameBase,
        double heightAdjustment = 0.0,
        bool negateDirection = false
    )
    {
        var extrusionDirection = contour.ContourInitialLineDirection.CrossProduct(XYZ.BasisZ);
        var extrusionThickness = pieceHeight;

        if (contour.ContourWorkplaneAlignmentOption.Equals(ContourWorkplaneAlignmentOptions.XY))
        {
            extrusionDirection = XYZ.BasisZ;
        }
        else if (contour.ContourWorkplaneAlignmentOption.Equals(ContourWorkplaneAlignmentOptions.ZAndCustomAngle))
        {
            extrusionThickness = CARDBOARD_THICKNESS;
        }

        if (negateDirection)
        {
            extrusionDirection = extrusionDirection.Negate();
        }

        return RunSubworkflow<
            DirectShape_ModelPlanarByBoundaryLinesArgs,
            DirectShape_ModelPlanarByBoundaryLines,
            DirectShape_ModelByBoundaryLinesDto,
            DirectShapeDMFAData
        >(
            new(
                PieceContour: contour,
                ExtrusionDirection: extrusionDirection,
                ExtrusionThickness: extrusionThickness,
                DirectShapeName: $"{nameBase}_{pieceIndex + 1}",
                HeightAdjustment: heightAdjustment
            )
        );
    }


    /////////////////////////////////
    /////////////////////////////////
    /// Final Output & Fabrication
    /////////////////////////////////
    /////////////////////////////////
    public void OrderlyPlaceFaces(List<string> _telemetry)
    {
        _dto.PiecesPlacedAtStartPoint = RunSubworkflow<
            DFMA_PlacePiecesByRowsAndColumsArgs,
            DFMA_PlacePiecesByRowsAndColums,
            DFMA_PlacePiecesByRowsAndColumsDto,
            List<DirectShapeDMFAData>
        >(
            new(
                OriginalPieces: new List<DirectShapeDMFAData>
                    {
                        _dto.BottomFaceDirectShapeDMFAData,
                        _dto.BottomInternalFaceDirectShapeDMFAData,
                        _dto.TopFaceDirectShapeDMFAData,
                        _dto.TopInternalFaceDirectShapeDMFAData,
                    }
                    .Concat(_dto.BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData)
                    .Concat(_dto.BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData)
                    .Concat(_dto.ExternalVerticalFacesDirectShapeDMFADataList)
                    .ToList(),
                ExtrusionDirection: XYZ.BasisZ.Negate(),
                ExtrusionThickness: CARDBOARD_THICKNESS,
                InitialX: -10.0,
                InitialY: -10.0
            )
        );

        _telemetry.Add($"Total pieces placed at start point: {_dto.PiecesPlacedAtStartPoint.Count}");
    }

    public void PrepareSheetFamily(List<string> _telemetry)
    {
        _dto.SheetFamilySymbol = RunSubworkflow<
            RevitFamily_EntirelySetForUssageInRevitUIArgs,
            RevitFamily_EntirelySetForUssageInRevitUI,
            RevitFamily_EntirelySetForUssageInRevitUIDto,
            FamilySymbol
        >(
            new(
                FamilyPath: SHEET_FAMILY_PATH,
                FamilyName: SHEET_FAMILY_NAME,
                FamilyTypeName: SHEET_TYPE_NAME
            )
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

        _telemetry?.Add($"Metrics extracted: {nameof(_dto.SheetPrintableHeight)}: {_dto.SheetPrintableWidth}, {nameof(_dto.SheetPrintableWidth)}: {_dto.SheetPrintableHeight}");
    }

    public void ArrangePiecesInGroups(List<string> _telemetry)
    {
        if (_dto.PiecesPlacedAtStartPoint == null || !_dto.PiecesPlacedAtStartPoint.Any())
        {
            _telemetry.Add("No pieces available from the layout step to pack into sheets.");
            return;
        }

        if (_dto.SheetFamilySymbol != null && !_dto.SheetFamilySymbol.IsActive)
        {
            _dto.SheetFamilySymbol.Activate();
        }

        // Initialize the tracking list for the sheets
        _dto.ArrangedSheets = new List<List<DirectShapeDMFAData>>();
        var currentSheetGroup = new List<DirectShapeDMFAData>();

        // Apply SCALE factor to the printable area metrics so the physical geometry fits
        double widthLimit = _dto.SheetPrintableWidth > 0 ? _dto.SheetPrintableWidth * SCALE : 35.0 * SCALE;
        double heightLimit = _dto.SheetPrintableHeight > 0 ? _dto.SheetPrintableHeight * SCALE : 25.0 * SCALE;

        // Define proportional, tightly packed layout gaps
        double itemGap = 1.0;
        double sheetGap = widthLimit * 0.05;
        if (sheetGap < 5.0) sheetGap = 5.0;

        // Shift origin so sheets don't crash into the original model or initial placement location
        double currentSheetX = 0.0;
        double currentSheetY = 100.0;

        double cursorX = currentSheetX;
        double cursorY = currentSheetY;
        double currentRowMaxHeight = 0.0;
        int sheetCount = 0;

        // Helper action to place the visual border of the sheet behind the geometry
        Action placeSheetGeometry = () =>
        {
            if (_dto.SheetFamilySymbol != null)
            {
                _doc.Create.NewFamilyInstance(new XYZ(currentSheetX, currentSheetY, 0), _dto.SheetFamilySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            }
            sheetCount++;
            _telemetry.Add($"Generated Sheet Boundary {sheetCount} at X={currentSheetX:F2}, Y={currentSheetY:F2}");
        };

        // Initialize the first sheet
        placeSheetGeometry();

        // Pack the perfectly placed pieces from Action 23 into the scaled sheets
        foreach (var item in _dto.PiecesPlacedAtStartPoint)
        {
            var ds = item.DirectShape;
            if (ds == null) continue;

            // Extract exact geometric footprint in its CURRENT infinite-row position
            GetTightBoundsFromElement(ds, out double minX, out double maxX, out double minY, out double maxY, out double minZ, out double maxZ);

            if (minX == double.MaxValue)
            {
                // Fallback to Revit bounding box if triangulation fails
                var bbox = ds.get_BoundingBox(null);
                if (bbox != null)
                {
                    minX = bbox.Min.X; maxX = bbox.Max.X;
                    minY = bbox.Min.Y; maxY = bbox.Max.Y;
                    minZ = bbox.Min.Z; maxZ = bbox.Max.Z;
                }
                else
                {
                    _telemetry.Add($"  -> SKIPPED {ds.Name}: No computable bounds.");
                    continue;
                }
            }

            double pieceWidth = maxX - minX;
            double pieceHeight = maxY - minY;

            // 1. CHECK ROW WIDTH: Wrap to the next line on the same sheet if width is exceeded
            if (cursorX + pieceWidth > currentSheetX + widthLimit && cursorX > currentSheetX)
            {
                cursorX = currentSheetX;
                cursorY += currentRowMaxHeight + itemGap;
                currentRowMaxHeight = 0.0;
            }

            // 2. CHECK SHEET HEIGHT: Start a brand new sheet to the right if height is exceeded
            if (cursorY + pieceHeight > currentSheetY + heightLimit && cursorY > currentSheetY)
            {
                // We reached the end of a sheet, so save the current group to the DTO and start fresh
                if (currentSheetGroup.Count > 0)
                {
                    _dto.ArrangedSheets.Add(new List<DirectShapeDMFAData>(currentSheetGroup));
                    currentSheetGroup.Clear();
                }

                currentSheetX += widthLimit + sheetGap; // Move entire sheet to the right
                cursorX = currentSheetX;
                cursorY = currentSheetY; // Reset to bottom of the new sheet
                currentRowMaxHeight = 0.0;

                placeSheetGeometry();
            }

            // Physically move the element from its infinite-row location to its packed sheet location
            XYZ translationVector = new XYZ(cursorX - minX, cursorY - minY, 0);
            ElementTransformUtils.MoveElement(_doc, ds.Id, translationVector);

            // Add the item to the current sheet grouping
            currentSheetGroup.Add(item);

            currentRowMaxHeight = Math.Max(currentRowMaxHeight, pieceHeight);
            cursorX += pieceWidth + itemGap;
        }

        // Add the final group after the loop finishes
        if (currentSheetGroup.Count > 0)
        {
            _dto.ArrangedSheets.Add(currentSheetGroup);
        }

        _telemetry.Add($"Successfully grouped {_dto.PiecesPlacedAtStartPoint.Count} pieces into {_dto.ArrangedSheets.Count} sheet groups.");
    }


    // Helper method tightly bound to the main workflow to extract actual vertices from the solid geometry
    private void GetTightBoundsFromElement(DirectShape ds, out double bMinX, out double bMaxX, out double bMinY, out double bMaxY, out double bMinZ, out double bMaxZ)
    {
        bMinX = double.MaxValue; bMaxX = double.MinValue;
        bMinY = double.MaxValue; bMaxY = double.MinValue;
        bMinZ = double.MaxValue; bMaxZ = double.MinValue;

        Options opt = new Options();
        GeometryElement geomElem = ds.get_Geometry(opt);
        if (geomElem != null)
        {
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        Mesh mesh = face.Triangulate();
                        if (mesh == null) continue;

                        foreach (XYZ vertex in mesh.Vertices)
                        {
                            if (vertex.X < bMinX) bMinX = vertex.X;
                            if (vertex.X > bMaxX) bMaxX = vertex.X;
                            if (vertex.Y < bMinY) bMinY = vertex.Y;
                            if (vertex.Y > bMaxY) bMaxY = vertex.Y;
                            if (vertex.Z < bMinZ) bMinZ = vertex.Z;
                            if (vertex.Z > bMaxZ) bMaxZ = vertex.Z;
                        }
                    }
                }
            }
        }
    }


    public void GenerateSheetsForArrangedGroups(List<string> _telemetry)
    {
        Document doc = _doc;

        if (_dto.GeneratedSheetIds == null)
            _dto.GeneratedSheetIds = new List<ElementId>();

        double sheetCenterX = _dto.SheetHorizontalMargin + (_dto.SheetPrintableWidth / 2.0);
        double sheetCenterY = _dto.SheetVerticalMargin + (_dto.SheetPrintableHeight / 2.0);
        XYZ sheetPlacementPoint = new XYZ(sheetCenterX, sheetCenterY, 0);

        ViewFamilyType view3DType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

        // ==========================================
        // CREATE A SMALL TEXT NOTE TYPE FOR THE SHEET
        // ==========================================
        TextNoteType smallTextType = new FilteredElementCollector(doc)
            .OfClass(typeof(TextNoteType))
            .Cast<TextNoteType>()
            .FirstOrDefault(t => t.Name == "DFMA_Small_Label");

        if (smallTextType == null)
        {
            TextNoteType defaultTextType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault();

            if (defaultTextType != null)
            {
                smallTextType = defaultTextType.Duplicate("DFMA_Small_Label") as TextNoteType;
                // Set text size to 1.5mm (1/16") which is approx 0.0052 feet in internal units
                smallTextType.get_Parameter(BuiltInParameter.TEXT_SIZE)?.Set(0.005);
            }
        }

        if (view3DType == null) return;

        int sheetCount = 0;

        foreach (var pieceGroup in _dto.ArrangedSheets)
        {
            if (pieceGroup.Count == 0) continue;

            List<ElementId> groupIds = pieceGroup
                .Where(p => p.DirectShape != null)
                .Select(p => p.DirectShape.Id)
                .ToList();

            if (groupIds.Count == 0) continue;

            ViewSheet sheet = ViewSheet.Create(doc, _dto.SheetFamilySymbol.Id);
            sheet.Name = $"DFMA Cut File - Bed {sheetCount + 1}";
            sheet.SheetNumber = $"FAB-{sheetCount + 1:D3}";

            View3D view = View3D.CreateIsometric(doc, view3DType.Id);
            double viewScale = 20.0;
            view.Scale = (int)viewScale;

            ViewOrientation3D topOrientation = new ViewOrientation3D(
                XYZ.BasisZ,
                XYZ.BasisY,
                XYZ.BasisZ.Negate()
            );
            view.SetOrientation(topOrientation);

            view.IsolateElementsTemporary(groupIds);
            view.ConvertTemporaryHideIsolateToPermanent();

            BoundingBoxXYZ combinedBBox = GetCombinedBoundingBox(pieceGroup, view);
            XYZ combinedCenter = XYZ.Zero;

            if (combinedBBox != null)
            {
                combinedCenter = (combinedBBox.Min + combinedBBox.Max) / 2.0;

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
                _dto.GeneratedSheetIds.Add(sheet.Id);
                sheetCount++;

                // ==========================================
                // DRAW THE SMALL TEXT ON THE SHEET
                // ==========================================
                if (smallTextType != null && combinedBBox != null)
                {
                    foreach (var piece in pieceGroup)
                    {
                        if (piece.DirectShape == null) continue;

                        GetTightBoundsFromElement(piece.DirectShape, out double minX, out double maxX, out double minY, out double maxY, out double minZ, out double maxZ);
                        if (minX == double.MaxValue) continue;

                        XYZ pieceCenter3D = new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0);

                        double sheetOffsetX = (pieceCenter3D.X - combinedCenter.X) / viewScale;
                        double sheetOffsetY = (pieceCenter3D.Y - combinedCenter.Y) / viewScale;

                        XYZ textSheetLocation = new XYZ(
                            sheetPlacementPoint.X + sheetOffsetX,
                            sheetPlacementPoint.Y + sheetOffsetY,
                            0
                        );

                        string pieceCode = piece.DirectShape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "Unknown";

                        TextNoteOptions textOpts = new TextNoteOptions(smallTextType.Id)
                        {
                            HorizontalAlignment = HorizontalTextAlignment.Center,
                            TypeId = smallTextType.Id
                        };

                        TextNote.Create(doc, sheet.Id, textSheetLocation, pieceCode, textOpts);
                    }
                }
            }
        }

        _telemetry.Add($"Successfully generated and wrote 8-digit codes on {sheetCount} grouped DFMA fabrication sheets.");
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

    public void ExportSheetsToPDF(List<string> _telemetry)
    {
        if (_dto.GeneratedSheetIds == null || !_dto.GeneratedSheetIds.Any())
        {
            _telemetry.Add("No sheets were generated to export.");
            return;
        }

        string fileName = "DFMA_Fabrication_Sheets";

        try
        {
            PDFExportOptions pdfOptions = new PDFExportOptions
            {
                FileName = fileName,
                Combine = true,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100, // Forces 1:20 scale to be respected instead of fitting to page
                HideScopeBoxes = true,
                HideUnreferencedViewTags = true,
                HideReferencePlane = true,
                PaperFormat = ExportPaperFormat.Default // Uses the Letter_Sheet_001 dimensions
            };

            // Execute the native Revit API PDF Export
            _doc.Export(EXPORT_FOLDER_PATH, _dto.GeneratedSheetIds, pdfOptions);

            _telemetry.Add($"Successfully exported {_dto.GeneratedSheetIds.Count} sheets to PDF at: {EXPORT_FOLDER_PATH}\\{fileName}.pdf");
        }
        catch (System.Exception ex)
        {
            _telemetry.Add($"Failed to export PDF: {ex.Message}");
        }
    }





















    /////////////////////////////////
    /////////////////////////////////
    /// Cleanup
    /////////////////////////////////
    /////////////////////////////////
    public void HideInterestFloor(List<string> _telemetry)
    {
        _doc!.ActiveView.HideElements([_dto.InterestFloor.Id]);
    }
}


/////////////////////////////////
/////////////////////////////////
// DTO Definition
/////////////////////////////////
/////////////////////////////////

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
    public List<PieceContour> BottomFaceOuterCurveLoopDisplacedLinesPiecesContours { get; set; }


    public List<DirectShapeDMFAData> ExternalVerticalFacesDirectShapeDMFADataList { get; set; }


    public Face InternalBottomShapeTopFace { get; set; }
    public List<Line> BottomShapeTopFaceVerticalSubdivisoryLines { get; set; }
    public List<PieceContour> BottomShapeTopFaceVerticalSubdivisoryLinesPiecesContours { get; set; }
    public List<DirectShapeDMFAData> BottomShapeTopFaceVerticalSubdivisoryLinesDirectShapeDMFAData { get; set; }
    public List<Line> BottomShapeTopFaceInitialHorizontalSubdivisoryLines { get; set; }
    public List<Line> BottomShapeTopFaceFinalHorizontalSubdivisoryLines { get; set; }
    public List<PieceContour> BottomShapeTopFaceHorizontalSubdivisoryLinesPiecesContours { get; set; }
    public List<DirectShapeDMFAData> BottomShapeTopFaceHorizontalSubdivisoryLinesDirectShapeDMFAData { get; set; }


    public List<DirectShapeDMFAData> PiecesPlacedAtStartPoint { get; set; }



    public FamilySymbol CommonCarboardFamilySymbol { get; set; }


    public FamilySymbol SheetFamilySymbol { get; set; }
    public double SheetPrintableHeight { get; set; }
    public double SheetVerticalMargin { get; set; }
    public double SheetPrintableWidth { get; set; }
    public double SheetHorizontalMargin { get; set; }
    public List<List<DirectShapeDMFAData>> ArrangedSheets { get; set; }
    public List<ElementId> GeneratedSheetIds { get; set; }
}