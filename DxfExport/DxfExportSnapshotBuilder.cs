//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Commands;

//namespace Eobim.RevitApi.DxfExport;

///// <summary>Copies nested layout geometry into POCOs while Revit handles are still valid.</summary>
//public static class DxfExportSnapshotBuilder
//{
//    public static List<DxfExportSheet> FromNestedLayoutSheets(IReadOnlyList<DFMANestedSheet> sheets)
//    {
//        var result = new List<DxfExportSheet>(sheets.Count);
//        foreach (var sheet in sheets)
//        {
//            var exportPieces = new List<DxfExportPiece>(sheet.PlacedPieces.Count);
//            foreach (var piece in sheet.PlacedPieces)
//            {
//                var contours = new List<ExportableLine>();
//                foreach (var curve in piece.FlattenedContours)
//                {
//                    if (curve is not Line line)
//                        throw new InvalidOperationException("DXF snapshot expects FlattenedContours to contain only Line curves.");
//                    var p0 = line.GetEndPoint(0);
//                    var p1 = line.GetEndPoint(1);
//                    contours.Add(new ExportableLine(p0.X, p0.Y, p1.X, p1.Y));
//                }

//                exportPieces.Add(new DxfExportPiece
//                {
//                    UniqueCode = piece.UniqueCode ?? "",
//                    CentroidX = piece.Centroid.X,
//                    CentroidY = piece.Centroid.Y,
//                    Contours = contours,
//                });
//            }

//            result.Add(new DxfExportSheet
//            {
//                SheetNumber = sheet.SheetNumber,
//                Pieces = exportPieces,
//            });
//        }

//        return result;
//    }
//}
