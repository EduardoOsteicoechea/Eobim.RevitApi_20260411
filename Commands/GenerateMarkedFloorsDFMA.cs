using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.DFMA;
using Eobim.RevitApi.DxfExport;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;
using static RevitCurveLoop;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public partial class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<bool, GenerateMarkedFloorsDFMADto, object>
{
    protected override void OnAfterGeometryTransactionGroupBeforeFileIo() { }

    private readonly string INTEREST_FLOOR_MARK = "room_structural_bottom";

    private const double SCALE = 20;
    private const double ORIGINAL_THICKNESS = 0.0015;
    private readonly double CARDBOARD_THICKNESS = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters);
    private readonly double INTERNAL_SUPPORTS_SEPARATION_1 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 50;
    private readonly double INTERNAL_SUPPORTS_SEPARATION_2 = UnitUtils.ConvertToInternalUnits(((100 / SCALE) * ORIGINAL_THICKNESS), UnitTypeId.Meters) * 100;

    // --- FAMILY CONSTANTS & EXPORT PATHS ---
    private readonly string CARDBOARD_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa";
    private readonly string CARDBOARD_FAMILY_NAME = "Carboard_Segment_001_adaptative";
    private readonly string CARDBOARD_TYPE_NAME = "Type 1";

    // --- SHEET FAMILY CONSTANTS & EXPORT PATHS ---
    private readonly string SHEET_FAMILY_PATH = @"C:\Users\eduar\Desktop\Room_003\Revit2027\Letter_Sheet_001.rfa";
    private readonly string SHEET_FAMILY_NAME = "Letter_Sheet_001";
    private readonly string SHEET_TYPE_NAME = "Margin_only";


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
        //Add(PrepareSheetFamily);
        ///* 25 */
        //Add(GetPrintableAreaMetrics);
        ///* 26 */
        //Add(GroupPiecesByWidthAndHeight, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        ///* 27 */
        //Add(TranslateGroupedPiecesByPrintableDimensionsSquares, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        ///* 28 */
        //Add(GenerateSheetsForArrangedGroups);


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
}