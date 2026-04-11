using Autodesk.Revit.DB;

namespace Eobim.RevitApi.Core;

public static class RevitDirectShape
{
    public static DirectShape GenericModelFromSolid
    (
        Document doc,
        Solid solid,
        string? directShapeName = null
    )
    {
        var solidCentroid = solid.ComputeCentroid();
        var directShapeCategory = Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
        var directShape = DirectShape.CreateElement(doc, directShapeCategory.Id);
        directShape.Name = string.IsNullOrEmpty(directShapeName) 
            ? $"Generic Model at ({solidCentroid!.X:F2}, {solidCentroid.Y:F2}, {solidCentroid.Z:F2})" 
            : directShapeName;
        directShape.SetShape([solid]);
        return directShape;
    }
}