using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public record DFMA_PlacePiecesByRowsAndColumsArgs(
    List<DirectShapeDMFAData> OriginalPieces,
    XYZ ExtrusionDirection,
    double ExtrusionThickness,
    double InitialX,
    double InitialY
);

public class DFMA_PlacePiecesByRowsAndColums(Document doc, string workflowName)
    :
MultistepObservableAction<DFMA_PlacePiecesByRowsAndColumsArgs, DFMA_PlacePiecesByRowsAndColumsDto, List<DirectShapeDMFAData>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(DFMA_PlacePiecesByRowsAndColumsArgs args)
    {
        _dto.OriginalPieces = args.OriginalPieces;
        _dto.ExtrusionDirection = args.ExtrusionDirection;
        _dto.ExtrusionThickness = args.ExtrusionThickness;
        _dto.InitialX = args.InitialX;
        _dto.InitialY = args.InitialY;
    }

    protected override void SetActions()
    {
        Add(SetDirectShapeDMFADataPlacementData, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(CollectDirectShapes);
    }

    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    {
        var result = new List<DirectShape>();
        var currentX = _dto.InitialX;
        var currentY = _dto.InitialY;

        // Gap to keep pieces strictly side-by-side without touching
        double spacingGap = 2.0 / 12.0;

        foreach (var item in _dto.OriginalPieces)
        {
            // 1. Defend against null items or missing contours
            if (item == null || item.PieceContour == null) continue;

            var face = item.DirectShapeLeadFace;
            if (face == null) continue;

            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
            var faceOuterLoop = face.GetEdgesAsCurveLoops()?.FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
            if (faceOuterLoop == null) continue;

            XYZ extrusionDirection = faceNormal.Negate();

            // 2. USE _dto.ExtrusionThickness instead of item.ExtrusionThickness
            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { faceOuterLoop },
                    extrusionDirection,
                    _dto.ExtrusionThickness
                );

            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

            Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

            // ==========================================
            // APPLY THE PERFECT ANCHOR
            // ==========================================
            // 3. SAFE FALLBACK for XYZ to prevent NRE on .Negate()
            XYZ anchorPoint = item.PieceContour.RotationPoint ?? XYZ.Zero;

            // Translate the anchor to (0,0,0), then apply the flattening rotations
            Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
            Transform flatTransform = rotationTransform.Multiply(toOrigin);

            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

            // ==========================================
            // MEASURE & PLACE
            // ==========================================
            BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();

            // 4. Defend against empty bounding boxes
            if (bbox == null) continue;

            double flattenedWidth = bbox.Max.X - bbox.Min.X;

            // Because of the perfect anchor, bbox.Min should naturally sit exactly at or near (0,0,0).
            // We subtract it just to eliminate floating-point precision errors.
            XYZ placementVector = new XYZ(currentX - bbox.Min.X, currentY - bbox.Min.Y, 0);
            Transform placementTransform = Transform.CreateTranslation(placementVector);

            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

            // 5. Bypass Category.GetCategory entirely to guarantee we don't hit an NRE fetching the ID
            ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

            // Ensure DirectShape.Name doesn't throw if item.DirectShape is unexpectedly null
            displacedDirectShape.Name = $"{item.DirectShape?.Name ?? "Piece"}_displaced";
            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

            result.Add(displacedDirectShape);

            // Advance cursor perfectly
            currentX += flattenedWidth + spacingGap;
        }

        _dto.DisplacedDirectShapes = result;
    }


    public void CollectDirectShapes(List<string> _stateTrace)
    {
        var result = new List<DirectShapeDMFAData>();

        foreach (var item in _dto.DisplacedDirectShapes)
        {
            var directShapeDMFAData = new DirectShapeDMFAData(item);
            result.Add(directShapeDMFAData);
        }

        Result = result;
    }
}


public class DFMA_PlacePiecesByRowsAndColumsDto : Dto
{
    public List<DirectShapeDMFAData> OriginalPieces { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public double InitialX { get; set; }
    public double InitialY { get; set; }
    public List<DirectShape> DisplacedDirectShapes { get; set; }
    
}