using Autodesk.Revit.DB;
using Eobim.RevitApi.Commands;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi;

public class Pieces_NestIntoCardboardSheetsWorkflow(Document doc, string workflowName)
    : MultistepObservableAction<Pieces_NestIntoCardboardSheetsDto, List<DFMANestedSheet>>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        if (args == null || args.Length < 3)
            throw new ArgumentException("Insufficient arguments for nesting algorithm.");

        _dto.Pieces = args[0] as List<DFMAPiece> ?? throw new ArgumentException("First arg must be List<DFMAPiece>.");
        _dto.StockWidth = (double)args[1];
        _dto.StockHeight = (double)args[2];
    }

    protected override void SetActions()
    {
        Add(ValidatePieces);
        Add(RunShelfPackingAlgorithm);
        Add(SetResult);
    }

    public void ValidatePieces(List<string> _telemetry)
    {
        if (_dto.Pieces == null || !_dto.Pieces.Any())
        {
            throw new InvalidOperationException("No pieces provided for nesting.");
        }

        // Safety Check: Ensure no single piece is larger than the cardboard stock itself!
        foreach (var piece in _dto.Pieces)
        {
            if (piece.Width > _dto.StockWidth || piece.Height > _dto.StockHeight)
            {
                _telemetry.Add($"CRITICAL ERROR: Piece {piece.UniqueCode} ({piece.Width:F2}x{piece.Height:F2}) is larger than the cardboard stock ({_dto.StockWidth:F2}x{_dto.StockHeight:F2}).");
                throw new InvalidOperationException($"Physical constraints violated: Piece {piece.UniqueCode} cannot fit on the specified cardboard stock.");
            }
        }
    }

    public void RunShelfPackingAlgorithm(List<string> _telemetry)
    {
        var sheets = new List<DFMANestedSheet>();

        // 50mm buffer (in Revit internal units) to prevent laser cuts from burning into each other
        double margin = UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters);

        // SORTING: Sort pieces by Height (Descending) to optimize shelf packing space
        var sortedPieces = _dto.Pieces.OrderByDescending(p => p.Height).ToList();

        var currentSheet = new DFMANestedSheet { SheetNumber = 1 };
        double currentX = margin;
        double currentY = margin;
        double currentRowHeight = 0;

        foreach (var piece in sortedPieces)
        {
            // 1. Check Horizontal Fit (Does it fit in the current row?)
            if (currentX + piece.Width + margin > _dto.StockWidth)
            {
                // Move to the next row (shelf)
                currentX = margin;
                currentY += currentRowHeight + margin;
                currentRowHeight = 0; // Reset row height for the new row
            }

            // 2. Check Vertical Fit (Does the new row fit on the current cardboard sheet?)
            if (currentY + piece.Height + margin > _dto.StockHeight)
            {
                // Sheet is full. Save it and start a new physical cardboard sheet.
                sheets.Add(currentSheet);
                currentSheet = new DFMANestedSheet { SheetNumber = sheets.Count + 1 };

                // Reset coordinates for the new sheet
                currentX = margin;
                currentY = margin;
                currentRowHeight = 0;
            }

            // 3. Mathematical Translation
            // The ExtractAndFlatten step shifted the piece to (0,0). We now move it to (currentX, currentY).
            XYZ translationVector = new XYZ(currentX, currentY, 0);
            ApplyTransformToPiece(piece, translationVector);

            // Add the translated piece to the current sheet
            currentSheet.PlacedPieces.Add(piece);

            // 4. Advance X coordinate for the next piece
            currentX += piece.Width + margin;

            // Track the tallest piece in this specific row to know where the next row should start
            if (piece.Height > currentRowHeight)
            {
                currentRowHeight = piece.Height;
            }
        }

        // Add the final sheet if it has pieces on it
        if (currentSheet.PlacedPieces.Any())
        {
            sheets.Add(currentSheet);
        }

        _dto.NestedSheets = sheets;
        _telemetry.Add($"Successfully packed {_dto.Pieces.Count} pieces into {sheets.Count} sheets.");
    }

    private void ApplyTransformToPiece(DFMAPiece piece, XYZ translation)
    {
        var translatedLines = new List<Line>();

        // Move boundaries
        foreach (var line in piece.FlattenedContours)
        {
            var newStart = line.GetEndPoint(0) + translation;
            var newEnd = line.GetEndPoint(1) + translation;
            translatedLines.Add(Line.CreateBound(newStart, newEnd));
        }

        piece.FlattenedContours = translatedLines;

        // Move centroid (for the Text Code)
        if (piece.Centroid != null)
        {
            piece.Centroid += translation;
        }

        // Update bounding box tracker
        piece.MinX += translation.X;
        piece.MaxX += translation.X;
        piece.MinY += translation.Y;
        piece.MaxY += translation.Y;
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.NestedSheets;
    }
}

public class Pieces_NestIntoCardboardSheetsDto : Dto
{
    public List<DFMAPiece> Pieces { get; set; }
    public double StockWidth { get; set; }
    public double StockHeight { get; set; }
    public List<DFMANestedSheet> NestedSheets { get; set; }
}