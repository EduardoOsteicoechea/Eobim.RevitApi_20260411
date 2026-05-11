namespace Eobim.RevitApi.DxfExport;

/// <summary>Plain line segment for DXF export — no Revit API handles.</summary>
public readonly record struct ExportableLine(double StartX, double StartY, double EndX, double EndY);

public sealed class DxfExportPiece
{
    public string UniqueCode { get; init; } = "";
    public double CentroidX { get; init; }
    public double CentroidY { get; init; }
    public List<ExportableLine> Contours { get; init; } = [];
}

public sealed class DxfExportSheet
{
    public int SheetNumber { get; init; }
    public List<DxfExportPiece> Pieces { get; init; } = [];
}
