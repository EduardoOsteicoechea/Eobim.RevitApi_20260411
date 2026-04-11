using Autodesk.Revit.DB;

public static class RevitFrame
{
    public static Frame ZToXAndYToZNegative(XYZ xyz)
    {
        var frameZDirection = XYZ.BasisX;
        var frameXDirection = XYZ.BasisY;
        var frameYDirection = frameZDirection.CrossProduct(frameXDirection);
        return new Frame(xyz, frameXDirection, frameYDirection, frameZDirection);
    }
}