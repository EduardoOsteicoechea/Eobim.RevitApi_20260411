////using Autodesk.Revit.DB;
////using Eobim.RevitApi.Framework;

////namespace Eobim.RevitApi.MultiStepActions;

////public record DFMA_PlacePiecesByRowsAndColumsArgs(
////    List<DirectShapeDMFAData> OriginalPieces,
////    XYZ ExtrusionDirection,
////    double ExtrusionThickness,
////    double InitialX,
////    double InitialY
////);

////public class DFMA_PlacePiecesByRowsAndColums(Document doc, string workflowName)
////    :
////MultistepObservableAction<DFMA_PlacePiecesByRowsAndColumsArgs, DFMA_PlacePiecesByRowsAndColumsDto, List<DirectShapeDMFAData>>(doc, workflowName)
////{
////    public override void SafelyInitializeInputs(DFMA_PlacePiecesByRowsAndColumsArgs args)
////    {
////        _dto.OriginalPieces = args.OriginalPieces;
////        _dto.ExtrusionDirection = args.ExtrusionDirection;
////        _dto.ExtrusionThickness = args.ExtrusionThickness;
////        _dto.InitialX = args.InitialX;
////        _dto.InitialY = args.InitialY;
////    }

////    protected override void SetActions()
////    {
////        Add(SetDirectShapeDMFADataPlacementData, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
////        Add(CollectDirectShapes);
////    }

////    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
////    {
////        var result = new List<DirectShape>();

////        var currentX = -500.0;
////        var currentY = 100.0;
////        double spacingGap = 1.0;

////        _stateTrace.Add("==========================================");
////        _stateTrace.Add($"--- STARTING EXACT VERTEX PLACEMENT ---");
////        _stateTrace.Add($"Initial Coordinates: X={currentX:F4}, Y={currentY:F4}, Gap={spacingGap:F4}");
////        _stateTrace.Add("==========================================");

////        int pieceIndex = 0;
////        foreach (var item in _dto.OriginalPieces)
////        {
////            pieceIndex++;

////            // Generate a clean, unique 8-character alphanumeric code for fabrication tracking
////            string pieceCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

////            if (item == null || item.PieceContour == null) continue;

////            var face = item.DirectShapeLeadFace;
////            if (face == null) continue;

////            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
////            var faceOuterLoop = face.GetEdgesAsCurveLoops()?.FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
////            if (faceOuterLoop == null) continue;

////            XYZ extrusionDirection = faceNormal.Negate();

////            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
////                    new List<CurveLoop> { faceOuterLoop },
////                    extrusionDirection,
////                    _dto.ExtrusionThickness
////                );

////            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
////            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
////            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

////            Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);
////            XYZ anchorPoint = item.PieceContour.RotationPoint ?? XYZ.Zero;

////            Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
////            Transform flatTransform = rotationTransform.Multiply(toOrigin);

////            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

////            GetTightBounds(flatSolid, out double minX, out double maxX, out double minY, out double minZ);

////            if (minX == double.MaxValue) continue;

////            double flattenedWidth = maxX - minX;

////            XYZ placementVector = new XYZ(currentX - minX, currentY - minY, 0 - minZ);
////            Transform placementTransform = Transform.CreateTranslation(placementVector);

////            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

////            ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
////            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

////            // Name the element the 8-digit code
////            displacedDirectShape.Name = pieceCode;
////            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

////            // ==========================================
////            // ASSIGN THE FABRICATION CODE TO THE MARK
////            // ==========================================
////            Parameter markParam = displacedDirectShape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
////            if (markParam != null && !markParam.IsReadOnly)
////            {
////                markParam.Set(pieceCode);
////            }

////            result.Add(displacedDirectShape);
////            currentX += flattenedWidth + spacingGap;
////        }

////        _dto.DisplacedDirectShapes = result;
////    }

////    private void GetTightBounds(Solid solid, out double bMinX, out double bMaxX, out double bMinY, out double bMinZ)
////    {
////        bMinX = double.MaxValue; bMaxX = double.MinValue;
////        bMinY = double.MaxValue; bMinZ = double.MaxValue;

////        foreach (Face face in solid.Faces)
////        {
////            Mesh mesh = face.Triangulate();
////            if (mesh == null) continue;

////            foreach (XYZ vertex in mesh.Vertices)
////            {
////                if (vertex.X < bMinX) bMinX = vertex.X;
////                if (vertex.X > bMaxX) bMaxX = vertex.X;
////                if (vertex.Y < bMinY) bMinY = vertex.Y;
////                if (vertex.Z < bMinZ) bMinZ = vertex.Z;
////            }
////        }
////    }

////    public void CollectDirectShapes(List<string> _stateTrace)
////    {
////        var result = new List<DirectShapeDMFAData>();

////        foreach (var item in _dto.DisplacedDirectShapes)
////        {
////            var directShapeDMFAData = new DirectShapeDMFAData(item);
////            result.Add(directShapeDMFAData);
////        }

////        Result = result;
////    }
////}


////public class DFMA_PlacePiecesByRowsAndColumsDto : Dto
////{
////    public List<DirectShapeDMFAData> OriginalPieces { get; set; }
////    public XYZ ExtrusionDirection { get; set; }
////    public double ExtrusionThickness { get; set; }
////    public double InitialX { get; set; }
////    public double InitialY { get; set; }
////    public List<DirectShape> DisplacedDirectShapes { get; set; }

////}

//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;
//using System.Collections.Generic;
//using System.Linq;

//namespace Eobim.RevitApi.MultiStepActions;

//public record DFMA_PlacePiecesByRowsAndColumsArgs(
//    List<DirectShapeDMFAData> OriginalPieces,
//    XYZ ExtrusionDirection,
//    double ExtrusionThickness,
//    double InitialX,
//    double InitialY
//);

//public class DFMA_PlacePiecesByRowsAndColums(Document doc, string workflowName)
//    : MultistepObservableAction<DFMA_PlacePiecesByRowsAndColumsArgs, DFMA_PlacePiecesByRowsAndColumsDto, List<DirectShapeDMFAData>>(doc, workflowName)
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

//    public void SetDirectShapeDMFADataPlacementData(List<string> _stateTrace)
//    {
//        var result = new List<DirectShape>();

//        // Re-hooked to your DTO arguments (Change this to -500 and 100 in the Main Command!)
//        var currentX = _dto.InitialX;
//        var currentY = _dto.InitialY;
//        double spacingGap = 1.0;

//        _stateTrace.Add("==========================================");
//        _stateTrace.Add($"--- STARTING EXACT VERTEX PLACEMENT ---");
//        _stateTrace.Add($"Initial Coordinates: X={currentX:F4}, Y={currentY:F4}, Gap={spacingGap:F4}");
//        _stateTrace.Add("==========================================");

//        int pieceIndex = 0;
//        foreach (var item in _dto.OriginalPieces)
//        {
//            pieceIndex++;

//            if (item == null || item.PieceContour == null) continue;

//            // INHERIT the 8-digit code that was already assigned to the original piece's Name!
//            string pieceCode = item.DirectShape?.Name ?? $"UNKNOWN_{pieceIndex:D2}";

//            var face = item.DirectShapeLeadFace;
//            if (face == null) continue;

//            var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));
//            var faceOuterLoop = face.GetEdgesAsCurveLoops()?.FirstOrDefault(a => a.IsCounterclockwise(faceNormal));
//            if (faceOuterLoop == null) continue;

//            XYZ extrusionDirection = faceNormal.Negate();

//            var solidInPlace = GeometryCreationUtilities.CreateExtrusionGeometry(
//                    new List<CurveLoop> { faceOuterLoop },
//                    extrusionDirection,
//                    _dto.ExtrusionThickness
//                );

//            Transform rotX = Transform.CreateRotation(XYZ.BasisX, item.PieceContour.ContourPrintXRotation);
//            Transform rotY = Transform.CreateRotation(XYZ.BasisY, item.PieceContour.ContourPrintYRotation);
//            Transform rotZ = Transform.CreateRotation(XYZ.BasisZ, item.PieceContour.ContourPrintZRotation);

//            Transform rotationTransform = rotX.Multiply(rotY).Multiply(rotZ);
//            XYZ anchorPoint = item.PieceContour.RotationPoint ?? XYZ.Zero;

//            Transform toOrigin = Transform.CreateTranslation(anchorPoint.Negate());
//            Transform flatTransform = rotationTransform.Multiply(toOrigin);

//            Solid flatSolid = SolidUtils.CreateTransformed(solidInPlace, flatTransform);

//            GetTightBounds(flatSolid, out double minX, out double maxX, out double minY, out double minZ);

//            if (minX == double.MaxValue) continue;

//            double flattenedWidth = maxX - minX;

//            XYZ placementVector = new XYZ(currentX - minX, currentY - minY, 0 - minZ);
//            Transform placementTransform = Transform.CreateTranslation(placementVector);

//            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

//            ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
//            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

//            // Name the new element with the inherited 8-digit code
//            displacedDirectShape.Name = pieceCode;
//            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

//            // ==========================================
//            // ASSIGN THE FABRICATION CODE TO THE MARK
//            // ==========================================
//            Parameter markParam = displacedDirectShape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
//            if (markParam != null && !markParam.IsReadOnly)
//            {
//                markParam.Set(pieceCode);
//            }

//            result.Add(displacedDirectShape);
//            currentX += flattenedWidth + spacingGap;
//        }

//        _dto.DisplacedDirectShapes = result;
//    }

//    private void GetTightBounds(Solid solid, out double bMinX, out double bMaxX, out double bMinY, out double bMinZ)
//    {
//        bMinX = double.MaxValue; bMaxX = double.MinValue;
//        bMinY = double.MaxValue; bMinZ = double.MaxValue;

//        foreach (Face face in solid.Faces)
//        {
//            Mesh mesh = face.Triangulate();
//            if (mesh == null) continue;

//            foreach (XYZ vertex in mesh.Vertices)
//            {
//                if (vertex.X < bMinX) bMinX = vertex.X;
//                if (vertex.X > bMaxX) bMaxX = vertex.X;
//                if (vertex.Y < bMinY) bMinY = vertex.Y;
//                if (vertex.Z < bMinZ) bMinZ = vertex.Z;
//            }
//        }
//    }

//    public void CollectDirectShapes(List<string> _stateTrace)
//    {
//        var result = new List<DirectShapeDMFAData>();

//        foreach (var item in _dto.DisplacedDirectShapes)
//        {
//            // Find the original piece using the shared 8-digit Fabrication Code (Name)
//            var originalPiece = _dto.OriginalPieces.FirstOrDefault(p => p.DirectShape != null && p.DirectShape.Name == item.Name);

//            // Rebuild the data object referencing the new displaced shape
//            var directShapeDMFAData = new DirectShapeDMFAData(item)
//            {
//                // Safely grab the contour data from the original wrapper object
//                PieceContour = originalPiece?.PieceContour
//            };

//            result.Add(directShapeDMFAData);
//        }

//        Result = result;
//    }

//    //public void CollectDirectShapes(List<string> _stateTrace)
//    //{
//    //    var result = new List<DirectShapeDMFAData>();

//    //    foreach (var item in _dto.DisplacedDirectShapes)
//    //    {
//    //        // Rebuild the data object referencing the new displaced shape
//    //        var directShapeDMFAData = new DirectShapeDMFAData(item)
//    //        {
//    //            PieceContour = item.PieceContour // Keep the contour data if you need it downstream
//    //        };
//    //        result.Add(directShapeDMFAData);
//    //    }

//    //    Result = result;
//    //}
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

using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Eobim.RevitApi.MultiStepActions;

public record DFMA_PlacePiecesByRowsAndColumsArgs(
    List<DirectShapeDMFAData> OriginalPieces,
    XYZ ExtrusionDirection,
    double ExtrusionThickness,
    double InitialX,
    double InitialY
);

public class DFMA_PlacePiecesByRowsAndColums(Document doc, string workflowName)
    : MultistepObservableAction<DFMA_PlacePiecesByRowsAndColumsArgs, DFMA_PlacePiecesByRowsAndColumsDto, List<DirectShapeDMFAData>>(doc, workflowName)
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
        double spacingGap = 1.0;

        _stateTrace.Add("==========================================");
        _stateTrace.Add($"--- STARTING EXACT VERTEX PLACEMENT ---");
        _stateTrace.Add($"Initial Coordinates: X={currentX:F4}, Y={currentY:F4}, Gap={spacingGap:F4}");
        _stateTrace.Add("==========================================");

        int pieceIndex = 0;
        foreach (var item in _dto.OriginalPieces)
        {
            pieceIndex++;

            if (item == null || item.PieceContour == null) continue;

            // INHERIT the 8-digit code, but ADD A PREFIX so Revit doesn't complain about duplicates!
            string originalCode = item.DirectShape?.Name ?? $"UNKNOWN_{pieceIndex:D2}";
            string pieceCode = $"FAB-{originalCode}";

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

            GetTightBounds(flatSolid, out double minX, out double maxX, out double minY, out double minZ);

            if (minX == double.MaxValue) continue;

            double flattenedWidth = maxX - minX;

            XYZ placementVector = new XYZ(currentX - minX, currentY - minY, 0 - minZ);
            Transform placementTransform = Transform.CreateTranslation(placementVector);

            Solid finalSolid = SolidUtils.CreateTransformed(flatSolid, placementTransform);

            ElementId directShapeCategoryId = new ElementId(BuiltInCategory.OST_GenericModel);
            var displacedDirectShape = DirectShape.CreateElement(_doc, directShapeCategoryId);

            // Name the new element with the prefixed code
            displacedDirectShape.Name = pieceCode;
            displacedDirectShape.SetShape(new List<GeometryObject> { finalSolid });

            // ==========================================
            // ASSIGN THE FABRICATION CODE TO THE MARK
            // ==========================================
            Parameter markParam = displacedDirectShape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (markParam != null && !markParam.IsReadOnly)
            {
                markParam.Set(pieceCode);
            }

            result.Add(displacedDirectShape);
            currentX += flattenedWidth + spacingGap;
        }

        _dto.DisplacedDirectShapes = result;
    }

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
            // Find the original piece using the shared 8-digit Fabrication Code (we strip the FAB- prefix to look it up)
            string originalName = item.Name.Replace("FAB-", "");
            var originalPiece = _dto.OriginalPieces.FirstOrDefault(p => p.DirectShape != null && p.DirectShape.Name == originalName);

            var directShapeDMFAData = new DirectShapeDMFAData(item)
            {
                PieceContour = originalPiece?.PieceContour
            };

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