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


    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    //{
    //    var result = new List<DirectShape>();
    //    var currentX = _dto.InitialX;
    //    var currentY = _dto.InitialY;

    //    // Gap to keep pieces strictly side-by-side without touching
    //    double spacingGap = 2.0 / 12.0;

    //    _stateTrace.Add("==========================================");
    //    _stateTrace.Add($"--- STARTING PIECE PLACEMENT ---");
    //    _stateTrace.Add($"Initial Coordinates: X={currentX:F4}, Y={currentY:F4}, Spacing Gap={spacingGap:F4}");
    //    _stateTrace.Add("==========================================");

    //    int pieceIndex = 0;
    //    foreach (var item in _dto.OriginalPieces)
    //    {
    //        pieceIndex++;
    //        string pieceName = item?.DirectShape?.Name ?? $"UnknownPiece_{pieceIndex}";
    //        _stateTrace.Add($"\n[Piece {pieceIndex}] Processing: {pieceName}");

    //        // 1. Defend against null items or missing contours
    //        if (item == null || item.PieceContour == null)
    //        {
    //            _stateTrace.Add($"  -> SKIPPED: Item or PieceContour is null.");
    //            continue;
    //        }

    //        var face = item.DirectShapeLeadFace;
    //        if (face == null)
    //        {
    //            _stateTrace.Add($"  -> SKIPPED: DirectShapeLeadFace is null.");
    //            continue;
    //        }

    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
    //        var faceOuterLoop = face.GetEdgesAsCurveLoops()?.FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
    //        if (faceOuterLoop == null)
    //        {
    //            _stateTrace.Add($"  -> SKIPPED: Valid counterclockwise outer loop not found.");
    //            continue;
    //        }

    //        XYZ extrusionDirection = faceNormal.Negate();

    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
    //                new List<CurveLoop> { faceOuterLoop },
    //                extrusionDirection,
    //                _dto.ExtrusionThickness
    //            );

    //        Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
    //        Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
    //        Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

    //        Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

    //        // ==========================================
    //        // APPLY THE PERFECT ANCHOR
    //        // ==========================================
    //        XYZ anchorPoint = item.PieceContour.RotationPoint ?? XYZ.Zero;
    //        _stateTrace.Add($"  -> Anchor Point (RotationPoint): {anchorPoint}");

    //        // Translate the anchor to (0,0,0), then apply the flattening rotations
    //        Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
    //        Transform flatTransform = rotationTransform.Multiply(toOrigin);

    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

    //        // ==========================================
    //        // MEASURE & PLACE
    //        // ==========================================
    //        BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();

    //        if (bbox == null)
    //        {
    //            _stateTrace.Add($"  -> SKIPPED: BoundingBox is null after flattening.");
    //            continue;
    //        }

    //        double flattenedWidth = bbox.Max.X - bbox.Min.X;

    //        _stateTrace.Add($"  -> Flattened BBox Min: {bbox.Min}");
    //        _stateTrace.Add($"  -> Flattened BBox Max: {bbox.Max}");
    //        _stateTrace.Add($"  -> Calculated Width (Max.X - Min.X): {flattenedWidth:F4}");

    //        // Because of the perfect anchor, bbox.Min should naturally sit exactly at or near (0,0,0).
    //        // We subtract it just to eliminate floating-point precision errors and align to the cursor.
    //        XYZ placementVector = new XYZ(currentX - bbox.Min.X, currentY - bbox.Min.Y, 0);

    //        _stateTrace.Add($"  -> Target Cursor Coordinates: X={currentX:F4}, Y={currentY:F4}");
    //        _stateTrace.Add($"  -> Applied Translation Vector: {placementVector}");

    //        Transform placementTransform = Transform.CreateTranslation(placementVector);
    //        Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

    //        ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

    //        displacedDirectShape.Name = $"{pieceName}_displaced";
    //        displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

    //        result.Add(displacedDirectShape);

    //        // Advance cursor perfectly
    //        double nextX = currentX + flattenedWidth + spacingGap;
    //        _stateTrace.Add($"  -> Advancing Cursor X: {currentX:F4} + {flattenedWidth:F4} (width) + {spacingGap:F4} (gap) = {nextX:F4}");
    //        currentX = nextX;
    //    }

    //    _dto.DisplacedDirectShapes = result;
    //    _stateTrace.Add("==========================================");
    //    _stateTrace.Add($"--- FINISHED PLACEMENT ---");
    //    _stateTrace.Add($"Placed {result.Count} pieces. Final Cursor X: {currentX:F4}");
    //    _stateTrace.Add("==========================================");
    //}


    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    //{
    //    var result = new List<DirectShape>();
    //    var currentX = _dto.InitialX;
    //    var currentY = _dto.InitialY;

    //    // Gap to keep pieces strictly side-by-side without touching
    //    double spacingGap = 2.0 / 12.0;

    //    foreach (var item in _dto.OriginalPieces)
    //    {
    //        // 1. Defend against null items or missing contours
    //        if (item == null || item.PieceContour == null) continue;

    //        var face = item.DirectShapeLeadFace;
    //        if (face == null) continue;

    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
    //        var faceOuterLoop = face.GetEdgesAsCurveLoops()?.FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
    //        if (faceOuterLoop == null) continue;

    //        XYZ extrusionDirection = faceNormal.Negate();

    //        // 2. USE _dto.ExtrusionThickness instead of item.ExtrusionThickness
    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
    //                new List<CurveLoop> { faceOuterLoop },
    //                extrusionDirection,
    //                _dto.ExtrusionThickness
    //            );

    //        Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
    //        Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
    //        Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

    //        Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

    //        // ==========================================
    //        // APPLY THE PERFECT ANCHOR
    //        // ==========================================
    //        // 3. SAFE FALLBACK for XYZ to prevent NRE on .Negate()
    //        XYZ anchorPoint = item.PieceContour.RotationPoint ?? XYZ.Zero;

    //        // Translate the anchor to (0,0,0), then apply the flattening rotations
    //        Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
    //        Transform flatTransform = rotationTransform.Multiply(toOrigin);

    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

    //        // ==========================================
    //        // MEASURE & PLACE
    //        // ==========================================
    //        BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();

    //        // 4. Defend against empty bounding boxes
    //        if (bbox == null) continue;

    //        double flattenedWidth = bbox.Max.X - bbox.Min.X;

    //        // Because of the perfect anchor, bbox.Min should naturally sit exactly at or near (0,0,0).
    //        // We subtract it just to eliminate floating-point precision errors.
    //        XYZ placementVector = new XYZ(currentX - bbox.Min.X, currentY - bbox.Min.Y, 0);
    //        Transform placementTransform = Transform.CreateTranslation(placementVector);

    //        Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

    //        // 5. Bypass Category.GetCategory entirely to guarantee we don't hit an NRE fetching the ID
    //        ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

    //        // Ensure DirectShape.Name doesn't throw if item.DirectShape is unexpectedly null
    //        displacedDirectShape.Name = $"{item.DirectShape?.Name ?? "Piece"}_displaced";
    //        displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

    //        result.Add(displacedDirectShape);

    //        // Advance cursor perfectly
    //        currentX += flattenedWidth + spacingGap;
    //    }

    //    _dto.DisplacedDirectShapes = result;
    //}


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