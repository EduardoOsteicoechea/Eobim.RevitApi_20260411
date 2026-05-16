using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public class DFMA_PlacePiecesByRowsAndColums(Document doc, string workflowName)
    :
MultistepObservableAction<DFMA_PlacePiecesByRowsAndColumsDto, List<DirectShapeDMFAData>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.OriginalPieces = args[0] as List<DirectShapeDMFAData>;
        _dto.ExtrusionDirection = args[1] as XYZ;
        _dto.ExtrusionThickness = (double)args[2];
    }

    protected override void SetActions()
    {
        Add(SetDirectShapeDMFADataPlacementData, true, TransactionManagementOptions.RequiresEnclosingTransactionForCommand);
        Add(SetResult, true, TransactionManagementOptions.RequiresEnclosingTransactionForCommand);
    }

    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    {
        // 1. FIXED: Start at the origin (or a specific real coordinate), not at infinity!
        var currentX = 0.0;
        var currentY = 0.0;

        foreach (var item in _dto.OriginalPieces)
        {
            item.PlacementPoint = new XYZ(currentX, currentY, 0);

            item.RequiredXDisplacement = currentX - item.MinX;
            item.RequiredYDisplacement = 0;
            item.RequiredZDisplacement = 0;

            var face = item.DirectShapeLeadFace;
            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
            var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

            // ==========================================
            // STEP 1: EXTRUDE IN PLACE 
            // ==========================================
            XYZ extrusionDirection = faceNormal.Negate();

            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { faceOuterLoop },
                    extrusionDirection,
                    _dto.ExtrusionThickness
                );

            // ==========================================
            // STEP 2: CALCULATE THE TRANSFORM
            // ==========================================
            double angle = faceNormal.AngleTo(XYZ.BasisZ);
            XYZ crossProduct = faceNormal.CrossProduct(XYZ.BasisZ);
            Transform rotationTransform = Transform.Identity;

            // FIXED: Check the cross product length, then NORMALIZE IT before rotating!
            if (!crossProduct.IsAlmostEqualTo(XYZ.Zero))
            {
                XYZ validRotationAxis = crossProduct.Normalize(); // <--- CRUCIAL STEP
                rotationTransform = Transform.CreateRotation(validRotationAxis, angle);
            }
            else if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
            {
                rotationTransform = Transform.CreateRotation(XYZ.BasisX, Math.PI);
            }

            XYZ translationVector = new XYZ(item.RequiredXDisplacement, 0, 0);
            Transform translationTransform = Transform.CreateTranslation(translationVector);

            // Multiply: Rotate first, then Translate
            Transform finalTransform = translationTransform.Multiply(rotationTransform);

            // ==========================================
            // STEP 3: TRANSFORM THE SOLID
            // ==========================================
            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, finalTransform);

            // ==========================================
            // STEP 4: ASSIGN TO DIRECTSHAPE
            // ==========================================
            var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

            displacedDirectShape.Name = $"{item.DirectShape.Name}_displaced";
            displacedDirectShape.SetShape(new List<GeometryObject> { flatSolid });

            item.DisplacedCurveLoop = faceOuterLoop;

            currentX += item.MaxX - item.MinX;
            currentY += 0;
        }
    }

    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    //{
    //    var currentX = double.MinValue;
    //    var currentY = double.MinValue;

    //    foreach (var item in _dto.OriginalPieces)
    //    {
    //        // ... [Your displacement math for currentX and currentY stays here] ...

    //        var face = item.DirectShapeLeadFace;
    //        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(faceNormal));

    //        // 1. Calculate the rotation needed to lay the face flat
    //        // We want to rotate the face so its normal points to XYZ.BasisZ (Straight Up)
    //        double angle = faceNormal.AngleTo(XYZ.BasisZ);
    //        XYZ axisOfRotation = faceNormal.CrossProduct(XYZ.BasisZ);

    //        //Transform rotationTransform = Transform.Identity;

    //        //// If the cross product is almost zero, it means the face is already flat (either pointing up or down)
    //        //if (!axisOfRotation.IsAlmostEqualTo(XYZ.Zero))
    //        //{
    //        //    rotationTransform = Transform.CreateRotation(axisOfRotation, angle);
    //        //}
    //        //else if (faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
    //        //{
    //        //    // If it's pointing exactly down, flip it 180 degrees over the X axis
    //        //    rotationTransform = Transform.CreateRotation(XYZ.BasisX, Math.PI);
    //        //}

    //        // 2. Calculate the translation to move it to your DFMA grid (currentX, currentY)
    //        // Note: You should ideally calculate the centroid of the rotated loop here to place it exactly, 
    //        // but adding your displacement vector works as a rough placement.
    //        XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
    //        Transform translationTransform = Transform.CreateTranslation(translationVector);

    //        // Combine the transforms: First rotate it flat, then move it to the grid
    //        //Transform finalTransform = translationTransform.Multiply(rotationTransform);

    //        // 3. Apply the transform safely to the loop
    //        var faceDisplacedCurveLoop = new CurveLoop();
    //        foreach (var originalCurve in faceOuterLoop)
    //        {
    //            faceDisplacedCurveLoop.Append(originalCurve.CreateTransformed(translationTransform));
    //        }

    //        item.DisplacedCurveLoop = faceDisplacedCurveLoop;

    //        // 4. Now, safely extrude straight down!
    //        var solid = GeometryCreationUtilities.CreateExtrusionGeometry(
    //                new List<CurveLoop> { faceDisplacedCurveLoop },
    //                _dto.ExtrusionDirection, // XYZ.BasisZ.Negate()
    //                _dto.ExtrusionThickness  // CARDBOARD_THICKNESS
    //            );

    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);
    //        displacedDirectShape.Name = $"{item.DirectShape.Name}_displaced";
    //        displacedDirectShape.SetShape(new List<GeometryObject> { solid });
    //    }
    //}

    //public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    //{
    //    var currentX = double.MinValue;
    //    var currentY = double.MinValue;

    //    foreach (var item in _dto.OriginalPieces)
    //    {
    //        item.PlacementPoint = new XYZ(currentX, currentY, 0);

    //        currentX += item.MaxX - item.MinX;
    //        currentY += item.MaxY - item.MinY;

    //        item.RequiredXDisplacement = currentX - item.MinX;
    //        item.RequiredYDisplacement = currentY - item.MinY;

    //        //var face = item.DirectShapeLeadFace;
    //        //var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(face.ComputeNormal(new UV(.5, .5))));
    //        //var faceDisplacedCurveLoop = new CurveLoop();

    //        //foreach (var item1 in faceOuterLoop)
    //        //{
    //        //    faceDisplacedCurveLoop.Append(
    //        //        Line.CreateBound(
    //        //            new XYZ(item1.GetEndPoint(0).X + item.RequiredXDisplacement, item1.GetEndPoint(0).Y + item.RequiredYDisplacement, 0),
    //        //            new XYZ(item1.GetEndPoint(1).X + item.RequiredXDisplacement, item1.GetEndPoint(1).Y + item.RequiredYDisplacement, 0)
    //        //        ));
    //        //}

    //        //item.DisplacedCurveLoop = faceDisplacedCurveLoop;

    //        var face = item.DirectShapeLeadFace;
    //        var faceOuterLoop = face.GetEdgesAsCurveLoops().FirstOrDefault(a => a.IsCounterclockwise(face.ComputeNormal(new UV(.5, .5))));
    //        var faceDisplacedCurveLoop = new CurveLoop();

    //        // 1. Create a Revit Translation Transform
    //        XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
    //        Transform translationTransform = Transform.CreateTranslation(translationVector);

    //        // 2. Safely transform the existing curves
    //        foreach (var originalCurve in faceOuterLoop)
    //        {
    //            // This moves the curve exactly as it is (Line, Arc, etc.) without rebuilding endpoints
    //            var displacedCurve = originalCurve.CreateTransformed(translationTransform);

    //            faceDisplacedCurveLoop.Append(displacedCurve);
    //        }

    //        item.DisplacedCurveLoop = faceDisplacedCurveLoop;

    //        // 3. (Optional) Check if the loop somehow became open
    //        if (faceDisplacedCurveLoop.IsOpen())
    //        {
    //            throw new InvalidOperationException("The curve loop is open. Revit cannot extrude an unclosed loop into a solid.");
    //        }

    //        XYZ safeExtrusionDirection = _dto.ExtrusionDirection.Normalize();

    //        // 2. Ensure thickness is valid (e.g., greater than 1mm / ~0.003 feet)
    //        if (_dto.ExtrusionThickness < _doc.Application.ShortCurveTolerance)
    //        {
    //            throw new InvalidOperationException($"Extrusion thickness ({_dto.ExtrusionThickness}) is below Revit's tolerance.");
    //        }

    //        // 3. Create the solid
    //        var solid = GeometryCreationUtilities.CreateExtrusionGeometry(
    //                new List<CurveLoop> { faceDisplacedCurveLoop },
    //                safeExtrusionDirection,
    //                _dto.ExtrusionThickness
    //            );

    //        // 4. Validate the Solid has actual volume and faces
    //        if (solid == null || solid.Faces.Size == 0 || solid.Volume <= 0)
    //        {
    //            throw new InvalidOperationException("The extrusion succeeded, but created an empty or zero-volume solid.");
    //        }

    //        var geometryList = new List<GeometryObject> { solid };

    //        // 6. Finally, assign it
    //        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);
    //        var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);
    //        displacedDirectShape.Name = $"{item.DirectShape.Name}_displaced";
    //        displacedDirectShape.SetShape(geometryList);
    //    }


    public void SetResult(List<string> _stateTrace)
    {
        Result = new List<DirectShapeDMFAData>();
    }
}


public class DFMA_PlacePiecesByRowsAndColumsDto : Dto
{
    public List<DirectShapeDMFAData> OriginalPieces { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
}