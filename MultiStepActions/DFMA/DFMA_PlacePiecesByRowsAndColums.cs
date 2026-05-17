using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
        _dto.InitialX = (double)args[3];
        _dto.InitialY = (double)args[4];
    }

    protected override void SetActions()
    {
        Add(SetDirectShapeDMFADataPlacementData, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(CollectDirectShapes);
    }

    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
    {
        var result = new List<DirectShape>();
        // 1. FIXED: Start at the origin (or a specific real coordinate), not at infinity!
        var currentX = _dto.InitialX;
        var currentY = _dto.InitialY;

        foreach (var item in _dto.OriginalPieces)
        {
            item.PlacementPoint = new XYZ(currentX, currentY, 0);

            item.RequiredXDisplacement = currentX - item.MinX;
            item.RequiredYDisplacement = currentY;
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

            XYZ translationVector = new XYZ(item.RequiredXDisplacement, item.RequiredYDisplacement, 0);
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

            result.Add(displacedDirectShape);

            currentX += item.MaxX - item.MinX;
            //currentY += 0;
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