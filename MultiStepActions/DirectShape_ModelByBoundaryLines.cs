//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Core;
//using Eobim.RevitApi.DFMA;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi.MultiStepActions;

//public record DirectShape_ModelPlanarByBoundaryLinesArgs(
//    PieceContour PieceContour,
//    XYZ ExtrusionDirection,
//    double ExtrusionThickness,
//    string DirectShapeName,
//    double HeightAdjustment = 0.0
//);

//public class DirectShape_ModelPlanarByBoundaryLines(Document doc, string workflowName) : MultistepObservableAction<
//    DirectShape_ModelPlanarByBoundaryLinesArgs, 
//    DirectShape_ModelByBoundaryLinesDto, 
//    DirectShapeDMFAData
//    >(doc, workflowName)
//{
//    public override void SafelyInitializeInputs(DirectShape_ModelPlanarByBoundaryLinesArgs args)
//    {
//        _dto.PieceContour = args.PieceContour;
//        _dto.ExtrusionDirection = args.ExtrusionDirection;
//        _dto.ExtrusionThickness = args.ExtrusionThickness;
//        _dto.DirectShapeName = args.DirectShapeName;
//        _dto.HeightAdjustment = args.HeightAdjustment;
//    }

//    protected override void SetActions()
//    {
//        Add(AdjustLineHeight);
//        Add(GetCurveLoop);
//        Add(GetEnclosingDimmensions);
//        Add(GenerateSolid);
//        Add(SetShape, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
//        Add(ExtractDirectShapeLeadFace);
//        Add(ExtractDirectShapeBottomFace);
//        Add(SetResult);
//    }

//    public void AdjustLineHeight(List<string> _stateTrace)
//    {
//        _dto.ZAdjustedBoundaryLines = new List<Line>();

//        if (!_dto.HeightAdjustment.Equals(0.0))
//        {
//            foreach (var item in _dto.PieceContour.ContourLines)
//            {
//                _dto.ZAdjustedBoundaryLines.Add(Line.CreateBound(
//                    new XYZ(item.GetEndPoint(0).X, item.GetEndPoint(0).Y, item.GetEndPoint(0).Z + _dto.HeightAdjustment),
//                    new XYZ(item.GetEndPoint(1).X, item.GetEndPoint(1).Y, item.GetEndPoint(0).Z + _dto.HeightAdjustment)
//                    ));
//            }
//        }
//        else
//        {
//            _dto.ZAdjustedBoundaryLines = _dto.PieceContour.ContourLines;
//        }
//    }

//    public void GetCurveLoop(List<string> _stateTrace)
//    {
//        _dto.CurveLoop = new CurveLoop();

//        foreach (var item in _dto.ZAdjustedBoundaryLines)
//        {
//            _stateTrace.Add($"Adding line from {item.GetEndPoint(0)} to {item.GetEndPoint(1)} to curve loop.");
//            _dto.CurveLoop.Append(item);
//        }
//    }

//    public void GetEnclosingDimmensions(List<string> _stateTrace)
//    {
//        var minX = double.MaxValue;
//        var minY = double.MaxValue;
//        var maxX = double.MinValue;
//        var maxY = double.MinValue;

//        foreach (var item in _dto.ZAdjustedBoundaryLines)
//        {
//            var p1 = item.GetEndPoint(0);
//            var p2 = item.GetEndPoint(1);

//            if (p1.X < minX) minX = p1.X;
//            if (p2.X < minX) minX = p2.X;

//            if (p1.X > maxX) maxX = p1.X;
//            if (p2.X > maxX) maxX = p2.X;

//            if (p1.Y < minY) minY = p1.Y;
//            if (p2.Y < minY) minY = p2.Y;

//            if (p1.Y > maxY) maxY = p1.Y;
//            if (p2.Y > maxY) maxY = p2.Y;
//        }

//        _dto.MinX = minX;
//        _dto.MinY = minY;
//        _dto.MaxX = maxX;
//        _dto.MaxY = maxY;
//    }

//    public void GenerateSolid(List<string> _stateTrace)
//    {
//        _stateTrace.Add($"{nameof(_dto.ExtrusionDirection)}: {_dto.ExtrusionDirection}");

//        _dto.Solid = GeometryCreationUtilities.CreateExtrusionGeometry(
//                new List<CurveLoop> { _dto.CurveLoop },
//                _dto.ExtrusionDirection,
//                _dto.ExtrusionThickness
//            );
//    }

//    public void SetShape(List<string> _stateTrace)
//    {
//        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);

//        _dto.DirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

//        _dto.DirectShape.Name = _dto.DirectShapeName;

//        _dto.DirectShape.SetShape(new List<GeometryObject> { _dto.Solid });
//    }

//    public void ExtractDirectShapeLeadFace(List<string> _stateTrace)
//    {
//        var faceGeometry = _dto.DirectShape.get_Geometry(new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine });

//        var solid = faceGeometry.OfType<Solid>().FirstOrDefault(s => s.Volume > 0);

//        var faces = solid?.Faces.Cast<Face>();

//        var result = faces?.FirstOrDefault(a =>
//        {
//            var faceNormal = a.ComputeNormal(new UV(.5, .5));
//            return faceNormal.IsAlmostEqualTo(_dto.ExtrusionDirection);
//        });

//        if (result is null) throw new NullReferenceException("Lead face not found.");

//        _dto.DirectShapeLeadFace = result;
//    }

//    public void ExtractDirectShapeBottomFace(List<string> _stateTrace)
//    {
//        var faceGeometry = _dto.DirectShape.get_Geometry(new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine });

//        var solid = faceGeometry.OfType<Solid>().FirstOrDefault(s => s.Volume > 0);

//        var faces = solid?.Faces.Cast<Face>();

//        var result = faces?.FirstOrDefault(a =>
//        {
//            var faceNormal = a.ComputeNormal(new UV(.5, .5));

//            return faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate());
//        });

//        if (result is null) throw new NullReferenceException("Bottom face not found.");

//        _dto.DirectShapeBottomFace = result;
//    }

//    public void SetResult(List<string> _stateTrace)
//    {
//        Result = new DirectShapeDMFAData
//        {
//            BoundaryLines = _dto.ZAdjustedBoundaryLines,
//            ExtrusionDirection = _dto.ExtrusionDirection,
//            ExtrusionThickness = _dto.ExtrusionThickness,
//            DirectBottomFace = _dto.DirectShapeBottomFace,
//            DirectShape = _dto.DirectShape,
//            MinX = _dto.MinX,
//            MaxX = _dto.MaxX,
//            MinY = _dto.MinY,
//            MaxY = _dto.MaxY,
//            DirectShapeLeadFace = _dto.DirectShapeLeadFace,
//            DirectShapeLeadFaceReference = _dto.DirectShapeLeadFace.Reference,
//            AngleToXYZBasisZ = _dto.ExtrusionDirection.AngleTo(XYZ.BasisZ),
//            PieceContour = _dto.PieceContour
//        };
//    }
//}
//public class DirectShape_ModelByBoundaryLinesDto : Dto
//{
//    public PieceContour PieceContour { get; set; }
//    public XYZ ExtrusionDirection { get; set; }
//    public double ExtrusionThickness { get; set; }
//    public string DirectShapeName { get; set; }
//    public double HeightAdjustment { get; set; }

//    public List<Line> ZAdjustedBoundaryLines { get; set; }
//    public double MinX { get; set; }
//    public double MinY { get; set; }
//    public double MaxX { get; set; }
//    public double MaxY { get; set; }
//    public Face DirectShapeBottomFace { get; set; }
//    public Face DirectShapeLeadFace { get; set; }

//    public CurveLoop CurveLoop { get; set; }
//    public Solid Solid { get; set; }
//    public DirectShape DirectShape { get; set; }

//}

using Autodesk.Revit.DB;
using Eobim.RevitApi.Core;
using Eobim.RevitApi.DFMA;
using Eobim.RevitApi.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Eobim.RevitApi.MultiStepActions;

public record DirectShape_ModelPlanarByBoundaryLinesArgs(
    PieceContour PieceContour,
    XYZ ExtrusionDirection,
    double ExtrusionThickness,
    string DirectShapeName,
    double HeightAdjustment = 0.0
);

public class DirectShape_ModelPlanarByBoundaryLines(Document doc, string workflowName) : MultistepObservableAction<
    DirectShape_ModelPlanarByBoundaryLinesArgs,
    DirectShape_ModelByBoundaryLinesDto,
    DirectShapeDMFAData
    >(doc, workflowName)
{
    public override void SafelyInitializeInputs(DirectShape_ModelPlanarByBoundaryLinesArgs args)
    {
        _dto.PieceContour = args.PieceContour;
        _dto.ExtrusionDirection = args.ExtrusionDirection;
        _dto.ExtrusionThickness = args.ExtrusionThickness;
        _dto.DirectShapeName = args.DirectShapeName;
        _dto.HeightAdjustment = args.HeightAdjustment;

        // Generate the 8-digit tracking code immediately upon initialization
        _dto.FabricationCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
    }

    protected override void SetActions()
    {
        Add(AdjustLineHeight);
        Add(GetCurveLoop);
        Add(GetEnclosingDimmensions);
        Add(GenerateSolid);
        Add(SetShape, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        Add(ExtractDirectShapeLeadFace);
        Add(ExtractDirectShapeBottomFace);
        Add(SetResult);
    }

    public void AdjustLineHeight(List<string> _stateTrace)
    {
        _dto.ZAdjustedBoundaryLines = new List<Line>();

        if (!_dto.HeightAdjustment.Equals(0.0))
        {
            foreach (var item in _dto.PieceContour.ContourLines)
            {
                _dto.ZAdjustedBoundaryLines.Add(Line.CreateBound(
                    new XYZ(item.GetEndPoint(0).X, item.GetEndPoint(0).Y, item.GetEndPoint(0).Z + _dto.HeightAdjustment),
                    new XYZ(item.GetEndPoint(1).X, item.GetEndPoint(1).Y, item.GetEndPoint(0).Z + _dto.HeightAdjustment)
                    ));
            }
        }
        else
        {
            _dto.ZAdjustedBoundaryLines = _dto.PieceContour.ContourLines;
        }
    }

    public void GetCurveLoop(List<string> _stateTrace)
    {
        _dto.CurveLoop = new CurveLoop();

        foreach (var item in _dto.ZAdjustedBoundaryLines)
        {
            _stateTrace.Add($"Adding line from {item.GetEndPoint(0)} to {item.GetEndPoint(1)} to curve loop.");
            _dto.CurveLoop.Append(item);
        }
    }

    public void GetEnclosingDimmensions(List<string> _stateTrace)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var item in _dto.ZAdjustedBoundaryLines)
        {
            var p1 = item.GetEndPoint(0);
            var p2 = item.GetEndPoint(1);

            if (p1.X < minX) minX = p1.X;
            if (p2.X < minX) minX = p2.X;

            if (p1.X > maxX) maxX = p1.X;
            if (p2.X > maxX) maxX = p2.X;

            if (p1.Y < minY) minY = p1.Y;
            if (p2.Y < minY) minY = p2.Y;

            if (p1.Y > maxY) maxY = p1.Y;
            if (p2.Y > maxY) maxY = p2.Y;
        }

        _dto.MinX = minX;
        _dto.MinY = minY;
        _dto.MaxX = maxX;
        _dto.MaxY = maxY;
    }

    public void GenerateSolid(List<string> _stateTrace)
    {
        _stateTrace.Add($"{nameof(_dto.ExtrusionDirection)}: {_dto.ExtrusionDirection}");

        _dto.Solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { _dto.CurveLoop },
                _dto.ExtrusionDirection,
                _dto.ExtrusionThickness
            );
    }

    public void SetShape(List<string> _stateTrace)
    {
        var directShapeCategory = Category.GetCategory(_doc, BuiltInCategory.OST_GenericModel);

        _dto.DirectShape = DirectShape.CreateElement(_doc, directShapeCategory.Id);

        // Apply the generated code to the name
        _dto.DirectShape.Name = _dto.FabricationCode;

        _dto.DirectShape.SetShape(new List<GeometryObject> { _dto.Solid });

        // Safely set the Mark parameter to the code
        Parameter markParam = _dto.DirectShape.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
        if (markParam != null && !markParam.IsReadOnly)
        {
            markParam.Set(_dto.FabricationCode);
        }
    }

    public void ExtractDirectShapeLeadFace(List<string> _stateTrace)
    {
        var faceGeometry = _dto.DirectShape.get_Geometry(new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine });

        var solid = faceGeometry.OfType<Solid>().FirstOrDefault(s => s.Volume > 0);

        var faces = solid?.Faces.Cast<Face>();

        var result = faces?.FirstOrDefault(a =>
        {
            var faceNormal = a.ComputeNormal(new UV(.5, .5));
            return faceNormal.IsAlmostEqualTo(_dto.ExtrusionDirection);
        });

        if (result is null) throw new NullReferenceException("Lead face not found.");

        _dto.DirectShapeLeadFace = result;
    }

    public void ExtractDirectShapeBottomFace(List<string> _stateTrace)
    {
        var faceGeometry = _dto.DirectShape.get_Geometry(new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine });

        var solid = faceGeometry.OfType<Solid>().FirstOrDefault(s => s.Volume > 0);

        var faces = solid?.Faces.Cast<Face>();

        var result = faces?.FirstOrDefault(a =>
        {
            var faceNormal = a.ComputeNormal(new UV(.5, .5));

            return faceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate());
        });

        if (result is null) throw new NullReferenceException("Bottom face not found.");

        _dto.DirectShapeBottomFace = result;
    }

    public void SetResult(List<string> _stateTrace)
    {
        Result = new DirectShapeDMFAData
        {
            BoundaryLines = _dto.ZAdjustedBoundaryLines,
            ExtrusionDirection = _dto.ExtrusionDirection,
            ExtrusionThickness = _dto.ExtrusionThickness,
            DirectBottomFace = _dto.DirectShapeBottomFace,
            DirectShape = _dto.DirectShape,
            MinX = _dto.MinX,
            MaxX = _dto.MaxX,
            MinY = _dto.MinY,
            MaxY = _dto.MaxY,
            DirectShapeLeadFace = _dto.DirectShapeLeadFace,
            DirectShapeLeadFaceReference = _dto.DirectShapeLeadFace.Reference,
            AngleToXYZBasisZ = _dto.ExtrusionDirection.AngleTo(XYZ.BasisZ),
            PieceContour = _dto.PieceContour,

            // DTO updated internally down below, assuming DirectShapeDMFAData definition includes this now
        };
    }
}

public class DirectShape_ModelByBoundaryLinesDto : Dto
{
    public PieceContour PieceContour { get; set; }
    public XYZ ExtrusionDirection { get; set; }
    public double ExtrusionThickness { get; set; }
    public string DirectShapeName { get; set; }
    public double HeightAdjustment { get; set; }

    // The newly generated 8 character code
    public string FabricationCode { get; set; }

    public List<Line> ZAdjustedBoundaryLines { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public Face DirectShapeBottomFace { get; set; }
    public Face DirectShapeLeadFace { get; set; }

    public CurveLoop CurveLoop { get; set; }
    public Solid Solid { get; set; }
    public DirectShape DirectShape { get; set; }

}