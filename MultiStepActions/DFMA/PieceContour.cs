using Autodesk.Revit.DB;
using static Eobim.RevitApi.Commands.GenerateMarkedFloorsDFMA;

namespace Eobim.RevitApi.DFMA;

public class PieceContour
{
    public List<Line>? ContourLines { get; set; }
    public ContourWorkplaneAlignmentOptions ContourWorkplaneAlignmentOption { get; set; }
    public XYZ RotationPoint { get; set; }
    public XYZ ContourInitialLineDirection { get; set; }
    public double ContourPrintXRotation { get; set; }
    public double ContourPrintYRotation { get; set; }
    public double ContourPrintZRotation { get; set; }
    public double ContourBasePlaneXRotation { get; set; }
    public double ContourBasePlaneYRotation { get; set; }
    public double ContourBasePlaneZRotation { get; set; }
    public XYZ InternalOriginAlignedMinXYZ { get; set; }
    public XYZ InternalOriginAlignedMaxXYZ { get; set; }

    public override string ToString()
    {
        int lineCount = ContourLines?.Count ?? 0;

        return $"PieceContour {{\n" +
               $"  ContourLines: {lineCount} lines\n" +
               $"  RotationPoint: {RotationPoint}\n" +
               $"  AlignmentOption: {ContourWorkplaneAlignmentOption}\n" +
               $"  InitialLineDirection: {ContourInitialLineDirection}\n" +
               $"  PrintRotation (X, Y, Z): ({ContourPrintXRotation:F3}, {ContourPrintYRotation:F3}, {ContourPrintZRotation:F3})\n" +
               $"  BasePlaneRotation (X, Y, Z): ({ContourBasePlaneXRotation:F3}, {ContourBasePlaneYRotation:F3}, {ContourBasePlaneZRotation:F3})\n" +
               $"  MinXYZ: {InternalOriginAlignedMinXYZ}\n" +
               $"  MaxXYZ: {InternalOriginAlignedMaxXYZ}\n" +
               $"}}";
    }
}