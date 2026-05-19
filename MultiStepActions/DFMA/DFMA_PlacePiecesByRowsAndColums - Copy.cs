//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi.MultiStepActions;

//public record DFMA_PlacePiecesByRowsAndColumsArgs(
//    List<DirectShapeDMFAData> OriginalPieces,
//    XYZ ExtrusionDirection,
//    double ExtrusionThickness,
//    double InitialX,
//    double InitialY
//);

//public class DFMA_PlacePiecesByRowsAndColums(Document doc, string workflowName)
//    :
//MultistepObservableAction<DFMA_PlacePiecesByRowsAndColumsArgs, DFMA_PlacePiecesByRowsAndColumsDto, List<DirectShapeDMFAData>>(doc, workflowName)
//{
//    public override void SafelyInitializeInputs(DFMA_PlacePiecesByRowsAndColumsArgs args)
//    {
//        _dto.OriginalPieces = args.OriginalPieces;
//        _dto.ExtrusionDirection = args.ExtrusionDirection;
//        _dto.ExtrusionThickness = args.ExtrusionThickness;
//        _dto.InitialX = args.InitialX;
//        _dto.InitialY = args.InitialY;
//    }

//    protected override void SetActions()
//    {
//        Add(SetDirectShapeDMFADataPlacementData, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        Add(CollectDirectShapes);
//    }

//    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
//    //{
//    //    var result = new List<DirectShape>();
//    //    // 1. FIXED: Start at the origin (or a specific real coordinate), not at infinity!
//    //    var currentX = _dto.InitialX;
//    //    var currentY = _dto.InitialY;

//    //    foreach (var item in _dto.OriginalPieces)
//    //    {
//    //        item.PlacementPoint = new XYZ(currentX, currentY, 0);

//    //        item.RequiredXDisplacement = currentX - item.MinX;
//    //        item.RequiredYDisplacement = currentY;
//    //        item.RequiredZDisplacement = 0;

//    //        var face = item.DirectShapeLeadFace;
//    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
//    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

//    //        // ==========================================
//    //        // STEP 1: EXTRUDE IN PLACE 
//    //        // ==========================================
//    //        XYZ extrusionDirection = faceNormal.Negate();

//    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
//    //                new List<CurveLoop> { faceOuterLoop },
//    //                extrusionDirection,
//    //                _dto.ExtrusionThickness
//    //            );

//    //        // ==========================================
//    //        // STEP 2: CALCULATE THE TRANSFORM
//    //        // ==========================================
//    //        double angle = faceNormal.AngleTo(XYZ.BasisZ);
//    //        XYZ crossProduct = faceNormal.CrossProduct(XYZ.BasisZ);
//    //        Transform rotationTransform = Transform.Identity;

//    //        // FIXED: Check the cross product length, then NORMALIZE IT before rotating!
//    //        if (!crossProduct.IsAlmostEqualTo(XYZ.Zero))
//    //        {
//    //            XYZ validRotationAxis = crossProduct.Normalize(); // <--- CRUCIAL STEP
//    //            rotationTransform = Transform.CreateRotation(validRotationAxis, angle);
//    //        }
//    //        else if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
//    //        {
//    //            rotationTransform = Transform.CreateRotation(XYZ.BasisX, Math.PI);
//    //        }

//    //        XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
//    //        Transform translationTransform = Transform.CreateTranslation(translationVector);

//    //        // Multiply: Rotate first, then Translate
//    //        Transform finalTransform = translationTransform.Multiply(rotationTransform);

//    //        // ==========================================
//    //        // STEP 3: TRANSFORM THE SOLID
//    //        // ==========================================
//    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, finalTransform);

//    //        // ==========================================
//    //        // STEP 4: ASSIGN TO DIRECTSHAPE
//    //        // ==========================================
//    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);

//    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

//    //        displacedDirectShape.Name = $"{item.DirectShape.Name}_displaced";

//    //        displacedDirectShape.SetShape(new List<GeometryObject> { flatSolid });

//    //        item.DisplacedCurveLoop = faceOuterLoop;

//    //        result.Add(displacedDirectShape);

//    //        currentX += item.MaxX - item.MinX;
//    //        //currentY += 0;
//    //    }

//    //    _dto.DisplacedDirectShapes = result;
//    //}

//    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
//    //{
//    //    var result = new List<DirectShape>();

//    //    var currentX = _dto.InitialX;
//    //    var currentY = _dto.InitialY;

//    //    // Add a small gap so pieces don't physically touch in the layout
//    //    double spacingGap = 2.0 / 12.0;

//    //    foreach (var item in _dto.OriginalPieces)
//    //    {
//    //        item.PlacementPoint = new XYZ(currentX, currentY, 0);

//    //        // We go back to using the DirectShapeDMFAData bounds to zero-out the piece
//    //        item.RequiredXDisplacement = currentX - item.MinX;
//    //        item.RequiredYDisplacement = currentY - item.MinY; // Using MinY ensures it aligns neatly on the Y axis too
//    //        item.RequiredZDisplacement = 0;

//    //        var face = item.DirectShapeLeadFace;
//    //        if (face == null) continue;

//    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
//    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

//    //        if (faceOuterLoop == null) continue;

//    //        // ==========================================
//    //        // STEP 1: EXTRUDE IN PLACE 
//    //        // ==========================================
//    //        XYZ extrusionDirection = faceNormal.Negate();

//    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
//    //                new List<CurveLoop> { faceOuterLoop },
//    //                extrusionDirection,
//    //                _dto.ExtrusionThickness
//    //            );

//    //        // ==========================================
//    //        // STEP 2: APPLY ROTATIONS
//    //        // ==========================================
//    //        Transform rotationTransform = Transform.Identity;

//    //        // If it has a PieceContour, use your explicit Print Rotations
//    //        if (item.PieceContour != null)
//    //        {
//    //            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
//    //            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
//    //            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

//    //            rotationTransform = rotZ.Multiply(rotY).Multiply(rotX);
//    //        }
//    //        else // Fallback for faces that might not have a PieceContour (like top/bottom faces)
//    //        {
//    //            double angle = faceNormal.AngleTo(XYZ.BasisZ);
//    //            XYZ crossProduct = faceNormal.CrossProduct(XYZ.BasisZ);

//    //            if (!crossProduct.IsAlmostEqualTo(XYZ.Zero))
//    //            {
//    //                rotationTransform = Transform.CreateRotation(crossProduct.Normalize(), angle);
//    //            }
//    //            else if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
//    //            {
//    //                rotationTransform = Transform.CreateRotation(XYZ.BasisX, Math.PI);
//    //            }
//    //        }

//    //        XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
//    //        Transform translationTransform = Transform.CreateTranslation(translationVector);

//    //        // Multiply: Rotate first, then Translate
//    //        Transform finalTransform = translationTransform.Multiply(rotationTransform);

//    //        // ==========================================
//    //        // STEP 3: TRANSFORM THE SOLID & CALCULATE WIDTH
//    //        // ==========================================
//    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, finalTransform);

//    //        // THIS IS THE NEW MAGIC: Measure the solid AFTER it is flattened
//    //        BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();
//    //        double flattenedPieceWidth = bbox.Max.X - bbox.Min.X;

//    //        // ==========================================
//    //        // STEP 4: ASSIGN TO DIRECTSHAPE
//    //        // ==========================================
//    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
//    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

//    //        displacedDirectShape.Name = $"{item.DirectShape?.Name}_displaced";
//    //        displacedDirectShape.SetShape(new List<GeometryObject> { flatSolid });

//    //        item.DisplacedCurveLoop = faceOuterLoop;
//    //        result.Add(displacedDirectShape);

//    //        // ==========================================
//    //        // STEP 5: ADVANCE THE PLACEMENT CURSOR
//    //        // ==========================================
//    //        // Push the cursor forward by the exact width of the newly flattened piece
//    //        currentX += flattenedPieceWidth + spacingGap;
//    //    }

//    //    _dto.DisplacedDirectShapes = result;
//    //}

//    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
//    //{
//    //    var result = new List<DirectShape>();
//    //    var currentX = _dto.InitialX;
//    //    var currentY = _dto.InitialY;

//    //    // Add a small gap so pieces don't physically touch in the layout
//    //    double spacingGap = 2.0 / 12.0;

//    //    foreach (var item in _dto.OriginalPieces)
//    //    {
//    //        // Safety checks
//    //        if (item == null || item.PieceContour == null) continue;

//    //        var face = item.DirectShapeLeadFace;
//    //        if (face == null) continue;

//    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
//    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
//    //        if (faceOuterLoop == null) continue;

//    //        // ==========================================
//    //        // 1. EXTRUDE WITH TRUE PIECE HEIGHT
//    //        // ==========================================
//    //        XYZ extrusionDirection = faceNormal.Negate();
//    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
//    //                new List<CurveLoop> { faceOuterLoop },
//    //                extrusionDirection,
//    //                item.ExtrusionThickness // Uses the piece's actual calculated height
//    //            );

//    //        // ==========================================
//    //        // 2. BUILD ROTATION (Z first, then X)
//    //        // ==========================================
//    //        Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
//    //        Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
//    //        Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

//    //        Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

//    //        // ==========================================
//    //        // 3. DYNAMIC ORIGIN (THE FIX)
//    //        // ==========================================
//    //        // Find the center of the solid in 3D space, bypassing RotationPoint entirely
//    //        BoundingBoxXYZ initialBbox = solidInPlace.GetBoundingBox();
//    //        XYZ solidCenter = (initialBbox.Min + initialBbox.Max) / 2.0;

//    //        // Move the solid's center to (0,0,0) to prevent the "Orbiting" bug
//    //        Transform toOrigin = Transform.CreateTranslation(solidCenter.Negate());

//    //        // Apply ToOrigin -> Then Rotate
//    //        Transform flatTransform = rotationTransform.Multiply(toOrigin);
//    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

//    //        // ==========================================
//    //        // 4. MEASURE AND PLACE IN ROW
//    //        // ==========================================
//    //        BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();
//    //        double flattenedWidth = bbox.Max.X - bbox.Min.X;
//    //        double flattenedMinX = bbox.Min.X;
//    //        double flattenedMinY = bbox.Min.Y;

//    //        // Translate it to the neat row, aligning its bottom-left corner to currentX, currentY
//    //        XYZ placementVector = new XYZ(currentX - flattenedMinX, currentY - flattenedMinY, 0);
//    //        Transform placementTransform = Transform.CreateTranslation(placementVector);

//    //        Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

//    //        // ==========================================
//    //        // 5. CREATE FINAL DIRECT SHAPE
//    //        // ==========================================
//    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
//    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);
//    //        displacedDirectShape.Name = $"{item.DirectShape?.Name}_displaced";
//    //        displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

//    //        result.Add(displacedDirectShape);

//    //        // Advance cursor for the next piece
//    //        currentX += flattenedWidth + spacingGap;
//    //    }

//    //    _dto.DisplacedDirectShapes = result;
//    //}

//    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
//    //{
//    //    var result = new List<DirectShape>();
//    //    var currentX = _dto.InitialX;
//    //    var currentY = _dto.InitialY;

//    //    // Add a small gap so pieces don't physically touch in the layout
//    //    double spacingGap = 2.0 / 12.0;

//    //    _stateTrace.Add($"--- STARTING PLACEMENT. InitialX: {currentX}, InitialY: {currentY} ---");

//    //    int pieceIndex = 0;
//    //    foreach (var item in _dto.OriginalPieces)
//    //    {
//    //        string pName = item?.DirectShape?.Name ?? $"Piece_{pieceIndex}";
//    //        _stateTrace.Add($"\n--- Processing [Index: {pieceIndex}] {pName} ---");
//    //        pieceIndex++;

//    //        // Safety checks
//    //        if (item == null) { _stateTrace.Add("Item is null. Skipping."); continue; }
//    //        if (item.PieceContour == null) { _stateTrace.Add("PieceContour is null. Skipping."); continue; }

//    //        var face = item.DirectShapeLeadFace;
//    //        if (face == null) { _stateTrace.Add("DirectShapeLeadFace is null. Skipping."); continue; }

//    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
//    //        _stateTrace.Add($"Face Normal: {faceNormal}");

//    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
//    //        if (faceOuterLoop == null) { _stateTrace.Add("Counterclockwise CurveLoop not found. Skipping."); continue; }

//    //        // ==========================================
//    //        // 1. EXTRUDE WITH TRUE PIECE HEIGHT
//    //        // ==========================================
//    //        XYZ extrusionDirection = faceNormal.Negate();
//    //        _stateTrace.Add($"Extrusion Direction: {extrusionDirection}, Thickness: {item.ExtrusionThickness:F4}");

//    //        var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
//    //                new List<CurveLoop> { faceOuterLoop },
//    //                extrusionDirection,
//    //                item.ExtrusionThickness // Uses the piece's actual calculated height
//    //            );

//    //        // ==========================================
//    //        // 2. BUILD ROTATION (Z first, then X)
//    //        // ==========================================
//    //        double rotXDeg = item.PieceContour.ContourPrintXRotation * 180 / Math.PI;
//    //        double rotYDeg = item.PieceContour.ContourPrintYRotation * 180 / Math.PI;
//    //        double rotZDeg = item.PieceContour.ContourPrintZRotation * 180 / Math.PI;

//    //        _stateTrace.Add($"Rotations (Radians) X: {item.PieceContour.ContourPrintXRotation:F4}, Y: {item.PieceContour.ContourPrintYRotation:F4}, Z: {item.PieceContour.ContourPrintZRotation:F4}");
//    //        _stateTrace.Add($"Rotations (Degrees) X: {rotXDeg:F1}, Y: {rotYDeg:F1}, Z: {rotZDeg:F1}");

//    //        Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
//    //        Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
//    //        Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

//    //        Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

//    //        // ==========================================
//    //        // 3. DYNAMIC ORIGIN
//    //        // ==========================================
//    //        BoundingBoxXYZ initialBbox = solidInPlace.GetBoundingBox();
//    //        XYZ solidCenter = (initialBbox.Min + initialBbox.Max) / 2.0;

//    //        _stateTrace.Add($"Initial 3D BBox -> Min: {initialBbox.Min}, Max: {initialBbox.Max}");
//    //        _stateTrace.Add($"Calculated Solid Center: {solidCenter}");

//    //        // Move the solid's center to (0,0,0) to prevent the "Orbiting" bug
//    //        Transform toOrigin = Transform.CreateTranslation(solidCenter.Negate());

//    //        // Apply ToOrigin -> Then Rotate
//    //        Transform flatTransform = rotationTransform.Multiply(toOrigin);
//    //        Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

//    //        // ==========================================
//    //        // 4. MEASURE AND PLACE IN ROW
//    //        // ==========================================
//    //        BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();
//    //        double flattenedWidth = bbox.Max.X - bbox.Min.X;
//    //        double flattenedHeight = bbox.Max.Y - bbox.Min.Y;
//    //        double flattenedThickness = bbox.Max.Z - bbox.Min.Z;

//    //        _stateTrace.Add($"Flat 3D BBox -> Min: {bbox.Min}, Max: {bbox.Max}");
//    //        _stateTrace.Add($"Flat Dimensions -> Width(X): {flattenedWidth:F4}, Height(Y): {flattenedHeight:F4}, Thickness(Z): {flattenedThickness:F4}");

//    //        // Translate it to the neat row, aligning its bottom-left corner to currentX, currentY
//    //        XYZ placementVector = new XYZ(currentX - bbox.Min.X, currentY - bbox.Min.Y, 0);
//    //        _stateTrace.Add($"Placement Translation Vector: {placementVector}");

//    //        Transform placementTransform = Transform.CreateTranslation(placementVector);

//    //        Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

//    //        // ==========================================
//    //        // 5. CREATE FINAL DIRECT SHAPE
//    //        // ==========================================
//    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
//    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);
//    //        displacedDirectShape.Name = $"{item.DirectShape?.Name}_displaced";
//    //        displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

//    //        result.Add(displacedDirectShape);

//    //        _stateTrace.Add($"Piece successfully placed. Old currentX: {currentX:F4}");
//    //        // Advance cursor for the next piece
//    //        currentX += flattenedWidth + spacingGap;
//    //        _stateTrace.Add($"Advanced cursor. New currentX: {currentX:F4}");
//    //    }

//    //    _stateTrace.Add($"\n--- PLACEMENT WORKFLOW COMPLETE. Total pieces placed: {result.Count} ---");
//    //    _dto.DisplacedDirectShapes = result;
//    //}

//    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
//    {
//        var result = new List<DirectShape>();
//        var currentX = _dto.InitialX;
//        var currentY = _dto.InitialY;

//        // Gap to keep pieces strictly side-by-side without touching
//        double spacingGap = 2.0 / 12.0;

//        foreach (var item in _dto.OriginalPieces)
//        {
//            if (item == null || item.PieceContour == null) continue;

//            var face = item.DirectShapeLeadFace;
//            if (face == null) continue;

//            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
//            var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
//            if (faceOuterLoop == null) continue;

//            XYZ extrusionDirection = faceNormal.Negate();
//            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
//                    new List<CurveLoop> { faceOuterLoop },
//                    extrusionDirection,
//                    item.ExtrusionThickness
//                );

//            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
//            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
//            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

//            Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);

//            // ==========================================
//            // APPLY THE PERFECT ANCHOR
//            // ==========================================
//            // This is the point we calculated upstream
//            XYZ anchorPoint = item.PieceContour.RotationPoint;

//            // Translate the anchor to (0,0,0), then apply the flattening rotations
//            Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
//            Transform flatTransform = rotationTransform.Multiply(toOrigin);

//            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

//            // ==========================================
//            // MEASURE & PLACE
//            // ==========================================
//            BoundingBoxXYZ bbox = flatSolid.GetBoundingBox();
//            double flattenedWidth = bbox.Max.X - bbox.Min.X;

//            // Because of the perfect anchor, bbox.Min should naturally sit exactly at or near (0,0,0).
//            // We subtract it just to eliminate floating-point precision errors.
//            XYZ placementVector = new XYZ(currentX - bbox.Min.X, currentY - bbox.Min.Y, 0);
//            Transform placementTransform = Transform.CreateTranslation(placementVector);

//            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

//            var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
//            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);
//            displacedDirectShape.Name = $"{item.DirectShape?.Name}_displaced";
//            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

//            result.Add(displacedDirectShape);

//            // Advance cursor perfectly
//            currentX += flattenedWidth + spacingGap;
//        }

//        _dto.DisplacedDirectShapes = result;
//    }


//    public void CollectDirectShapes(List<string> _stateTrace)
//    {
//        var result = new List<DirectShapeDMFAData>();

//        foreach (var item in _dto.DisplacedDirectShapes)
//        {
//            var directShapeDMFAData = new DirectShapeDMFAData(item);
//            result.Add(directShapeDMFAData);
//        }

//        Result = result;
//    }
//}


//public class DFMA_PlacePiecesByRowsAndColumsDto : Dto
//{
//    public List<DirectShapeDMFAData> OriginalPieces { get; set; }
//    public XYZ ExtrusionDirection { get; set; }
//    public double ExtrusionThickness { get; set; }
//    public double InitialX { get; set; }
//    public double InitialY { get; set; }
//    public List<DirectShape> DisplacedDirectShapes { get; set; }
    
//}