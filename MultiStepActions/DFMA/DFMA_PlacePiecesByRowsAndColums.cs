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

    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    //{
    //    var result = new List<DirectShape>();
    //    // 1. FIXED: Start at the origin (or a specific real coordinate), not at infinity!
    //    var currentX = _dto.InitialX;
    //    var currentY = _dto.InitialY;

    //    foreach (var item in _dto.OriginalPieces)
    //    {
    //        item.PlacementPoint = new XYZ(currentX, currentY, 0);

    //        item.RequiredXDisplacement = currentX - item.MinX;
    //        item.RequiredYDisplacement = currentY;
    //        item.RequiredZDisplacement = 0;

    //        var face = item.DirectShapeLeadFace;
    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

    //        // ==========================================
    //        // STEP 1: EXTRUDE IN PLACE 
    //        // ==========================================
    //        XYZ extrusionDirection = faceNormal.Negate();

    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
    //                new List<CurveLoop> { faceOuterLoop },
    //                extrusionDirection,
    //                _dto.ExtrusionThickness
    //            );

    //        // ==========================================
    //        // STEP 2: CALCULATE THE TRANSFORM
    //        // ==========================================
    //        double angle = faceNormal.AngleTo(XYZ.BasisZ);
    //        XYZ crossProduct = faceNormal.CrossProduct(XYZ.BasisZ);
    //        Transform rotationTransform = Transform.Identity;

    //        // FIXED: Check the cross product length, then NORMALIZE IT before rotating!
    //        if (!crossProduct.IsAlmostEqualTo(XYZ.Zero))
    //        {
    //            XYZ validRotationAxis = crossProduct.Normalize(); // <--- CRUCIAL STEP
    //            rotationTransform = Transform.CreateRotation(validRotationAxis, angle);
    //        }
    //        else if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
    //        {
    //            rotationTransform = Transform.CreateRotation(XYZ.BasisX, Math.PI);
    //        }

    //        XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
    //        Transform translationTransform = Transform.CreateTranslation(translationVector);

    //        // Multiply: Rotate first, then Translate
    //        Transform finalTransform = translationTransform.Multiply(rotationTransform);

    //        // ==========================================
    //        // STEP 3: TRANSFORM THE SOLID
    //        // ==========================================
    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, finalTransform);

    //        // ==========================================
    //        // STEP 4: ASSIGN TO DIRECTSHAPE
    //        // ==========================================
    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);

    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

    //        displacedDirectShape.Name = $"{item.DirectShape.Name}_displaced";

    //        displacedDirectShape.SetShape(new List<GeometryObject> { flatSolid });

    //        item.DisplacedCurveLoop = faceOuterLoop;

    //        result.Add(displacedDirectShape);

    //        currentX += item.MaxX - item.MinX;
    //        //currentY += 0;
    //    }

    //    _dto.DisplacedDirectShapes = result;
    //}

    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    //{
    //    var result = new List<DirectShape>();

    //    var currentX = _dto.InitialX;
    //    var currentY = _dto.InitialY;

    //    // Add a small gap so pieces don't physically touch in the layout
    //    double spacingGap = 2.0 / 12.0;

    //    foreach (var item in _dto.OriginalPieces)
    //    {
    //        item.PlacementPoint = new XYZ(currentX, currentY, 0);

    //        // We go back to using the DirectShapeDMFAData bounds to zero-out the piece
    //        item.RequiredXDisplacement = currentX - item.MinX;
    //        item.RequiredYDisplacement = currentY - item.MinY; // Using MinY ensures it aligns neatly on the Y axis too
    //        item.RequiredZDisplacement = 0;

    //        var face = item.DirectShapeLeadFace;
    //        if (face == null) continue;

    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

    //        if (faceOuterLoop == null) continue;

    //        // ==========================================
    //        // STEP 1: EXTRUDE IN PLACE 
    //        // ==========================================
    //        XYZ extrusionDirection = faceNormal.Negate();

    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
    //                new List<CurveLoop> { faceOuterLoop },
    //                extrusionDirection,
    //                _dto.ExtrusionThickness
    //            );

    //        // ==========================================
    //        // STEP 2: APPLY ROTATIONS
    //        // ==========================================
    //        Transform rotationTransform = Transform.Identity;

    //        // If it has a PieceContour, use your explicit Print Rotations
    //        if (item.PieceContour != null)
    //        {
    //            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
    //            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
    //            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

    //            rotationTransform = rotZ.Multiply(rotY).Multiply(rotX);
    //        }
    //        else // Fallback for faces that might not have a PieceContour (like top/bottom faces)
    //        {
    //            double angle = faceNormal.AngleTo(XYZ.BasisZ);
    //            XYZ crossProduct = faceNormal.CrossProduct(XYZ.BasisZ);

    //            if (!crossProduct.IsAlmostEqualTo(XYZ.Zero))
    //            {
    //                rotationTransform = Transform.CreateRotation(crossProduct.Normalize(), angle);
    //            }
    //            else if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
    //            {
    //                rotationTransform = Transform.CreateRotation(XYZ.BasisX, Math.PI);
    //            }
    //        }

    //        XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
    //        Transform translationTransform = Transform.CreateTranslation(translationVector);

    //        // Multiply: Rotate first, then Translate
    //        Transform finalTransform = translationTransform.Multiply(rotationTransform);

    //        // ==========================================
    //        // STEP 3: TRANSFORM THE SOLID & CALCULATE WIDTH
    //        // ==========================================
    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, finalTransform);

    //        // THIS IS THE NEW MAGIC: Measure the solid AFTER it is flattened
    //        BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();
    //        double flattenedPieceWidth = bbox.Max.X - bbox.Min.X;

    //        // ==========================================
    //        // STEP 4: ASSIGN TO DIRECTSHAPE
    //        // ==========================================
    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

    //        displacedDirectShape.Name = $"{item.DirectShape?.Name}_displaced";
    //        displacedDirectShape.SetShape(new List<GeometryObject> { flatSolid });

    //        item.DisplacedCurveLoop = faceOuterLoop;
    //        result.Add(displacedDirectShape);

    //        // ==========================================
    //        // STEP 5: ADVANCE THE PLACEMENT CURSOR
    //        // ==========================================
    //        // Push the cursor forward by the exact width of the newly flattened piece
    //        currentX += flattenedPieceWidth + spacingGap;
    //    }

    //    _dto.DisplacedDirectShapes = result;
    //}

    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    {
        var result = new List<DirectShape>();
        var currentX = _dto.InitialX;
        var currentY = _dto.InitialY;

        // Add a small gap so pieces don't physically touch in the layout
        double spacingGap = 2.0 / 12.0;

        foreach (var item in _dto.OriginalPieces)
        {
            // Safety checks
            if (item == null || item.PieceContour == null) continue;

            var face = item.DirectShapeLeadFace;
            if (face == null) continue;

            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
            var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
            if (faceOuterLoop == null) continue;

            // ==========================================
            // 1. EXTRUDE WITH TRUE PIECE HEIGHT
            // ==========================================
            XYZ extrusionDirection = faceNormal.Negate();
            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { faceOuterLoop },
                    extrusionDirection,
                    item.ExtrusionThickness // Uses the piece's actual calculated height
                );

            // ==========================================
            // 2. BUILD ROTATION (Z first, then X)
            // ==========================================
            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

            Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

            // ==========================================
            // 3. DYNAMIC ORIGIN (THE FIX)
            // ==========================================
            // Find the center of the solid in 3D space, bypassing RotationPoint entirely
            BoundingBoxXYZ initialBbox = solidInPlace.GetBoundingBox();
            XYZ solidCenter = (initialBbox.Min + initialBbox.Max) / 2.0;

            // Move the solid's center to (0,0,0) to prevent the "Orbiting" bug
            Transform toOrigin = Transform.CreateTranslation(solidCenter.Negate());

            // Apply ToOrigin -> Then Rotate
            Transform flatTransform = rotationTransform.Multiply(toOrigin);
            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

            // ==========================================
            // 4. MEASURE AND PLACE IN ROW
            // ==========================================
            BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();
            double flattenedWidth = bbox.Max.X - bbox.Min.X;
            double flattenedMinX = bbox.Min.X;
            double flattenedMinY = bbox.Min.Y;

            // Translate it to the neat row, aligning its bottom-left corner to currentX, currentY
            XYZ placementVector = new XYZ(currentX - flattenedMinX, currentY - flattenedMinY, 0);
            Transform placementTransform = Transform.CreateTranslation(placementVector);

            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

            // ==========================================
            // 5. CREATE FINAL DIRECT SHAPE
            // ==========================================
            var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);
            displacedDirectShape.Name = $"{item.DirectShape?.Name}_displaced";
            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

            result.Add(displacedDirectShape);

            // Advance cursor for the next piece
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