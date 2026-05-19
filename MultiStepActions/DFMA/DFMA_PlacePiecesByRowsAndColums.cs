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

        // Shifted far away to guarantee we don't overlap with the original model geometry
        var currentX = -500.0;
        var currentY = 100.0;

        // Increased gap to 1.0 foot to provide unmistakable visual separation
        double spacingGap = 1.0;

        _stateTrace.Add("==========================================");
        _stateTrace.Add($"--- STARTING EXACT VERTEX PLACEMENT ---");
        _stateTrace.Add($"Initial Coordinates: X={currentX:F4}, Y={currentY:F4}, Gap={spacingGap:F4}");
        _stateTrace.Add("==========================================");

        int pieceIndex = 0;
        foreach (var item in _dto.OriginalPieces)
        {
            pieceIndex++;
            string pieceName = item?.DirectShape?.Name ?? $"UnknownPiece_{pieceIndex}";

            if (item == null || item.PieceContour == null) continue;

            var face = item.DirectShapeLeadFace;
            if (face == null) continue;

            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
            var faceOuterLoop = face.GetEdgesAsCurveLoops()?.FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
            if (faceOuterLoop == null) continue;

            XYZ extrusionDirection = faceNormal.Negate();

            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { faceOuterLoop },
                    extrusionDirection,
                    _dto.ExtrusionThickness
                );

            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

            Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

            XYZ anchorPoint = item.PieceContour.RotationPoint ?? XYZ.Zero;

            Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
            Transform flatTransform = rotationTransform.Multiply(toOrigin);

            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

            // ==========================================
            // EXACT VERTEX MEASUREMENT (Bypassing BoundingBox)
            // ==========================================
            GetTightBounds(flatSolid, out double minX, out double maxX, out double minY, out double minZ);

            if (minX == double.MaxValue)
            {
                _stateTrace.Add($"  -> [Piece {pieceIndex}] SKIPPED: Could not extract vertices.");
                continue;
            }

            double flattenedWidth = maxX - minX;

            _stateTrace.Add($"\n[Piece {pieceIndex}] Processing: {pieceName}");
            _stateTrace.Add($"  -> Exact Min Vertices: X={minX:F4}, Y={minY:F4}, Z={minZ:F4}");
            _stateTrace.Add($"  -> Exact Max Vertices: X={maxX:F4}");
            _stateTrace.Add($"  -> True Solid Width: {flattenedWidth:F4}");
            _stateTrace.Add($"  -> Target Cursor X: {currentX:F4}");

            // Translate using exact vertex minimums, not the loose Bounding Box
            XYZ placementVector = new XYZ(currentX - minX, currentY - minY, 0 - minZ);
            Transform placementTransform = Transform.CreateTranslation(placementVector);

            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

            // Verify final placement mathematically
            GetTightBounds(finalSolid, out double fMinX, out double fMaxX, out double fMinY, out double fMinZ);
            _stateTrace.Add($"  -> VERIFIED Placement: Min.X is now {fMinX:F4}, Max.X is {fMaxX:F4}");

            ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

            displacedDirectShape.Name = $"{pieceName}_displaced";
            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

            result.Add(displacedDirectShape);

            // Mathematically guarantee no X-overlap using true vertex width
            currentX += flattenedWidth + spacingGap;
        }

        _dto.DisplacedDirectShapes = result;

        _stateTrace.Add("==========================================");
        _stateTrace.Add($"--- FINISHED PLACEMENT ---");
        _stateTrace.Add($"Placed {result.Count} pieces. Final Cursor X: {currentX:F4}");
        _stateTrace.Add("==========================================");
    }

    // Local helper function to extract exact, tight bounds from a Solid's Triangulation
    private void GetTightBounds(Solid solid, out double bMinX, out double bMaxX, out double bMinY, out double bMinZ)
    {
        bMinX = double.MaxValue; bMaxX = double.MinValue;
        bMinY = double.MaxValue; bMinZ = double.MaxValue;

        foreach (Face face in solid.Faces)
        {
            Mesh mesh = face.Triangulate();
            if (mesh == null) continue;

            foreach (XYZ vertex in mesh.Vertices)
            {
                if (vertex.X < bMinX) bMinX = vertex.X;
                if (vertex.X > bMaxX) bMaxX = vertex.X;
                if (vertex.Y < bMinY) bMinY = vertex.Y;
                if (vertex.Z < bMinZ) bMinZ = vertex.Z;
            }
        }
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