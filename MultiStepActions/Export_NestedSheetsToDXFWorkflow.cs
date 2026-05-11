using System;
using System.Collections.Generic;
using System.IO;
using Eobim.RevitApi.DxfExport;
using Eobim.RevitApi.Framework;
using netDxf;
using netDxf.Tables;

namespace Eobim.RevitApi;

public class Export_NestedSheetsToDXFWorkflow(Autodesk.Revit.DB.Document doc, string workflowName)
    : MultistepObservableAction<Export_NestedSheetsToDXFDto, bool>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.Sheets = args[0] as List<DxfExportSheet>;
        _dto.ExportDirectory = args[1] as string;
    }

    protected override void SetActions() { Add(GenerateDXFs); Add(SetResult); }

    public void GenerateDXFs(List<string> _telemetry)
    {
        if (_dto.Sheets is null)
            throw new InvalidOperationException($"{nameof(Export_NestedSheetsToDXFWorkflow)} requires POCO sheet data.");

        if (!Directory.Exists(_dto.ExportDirectory)) Directory.CreateDirectory(_dto.ExportDirectory);

        var cutLayer = new Layer("CUT") { Color = AciColor.Red };
        var scoreLayer = new Layer("SCORE") { Color = AciColor.Blue };

        foreach (var sheet in _dto.Sheets)
        {
            var dxf = new DxfDocument();

            foreach (var piece in sheet.Pieces)
            {
                foreach (var line in piece.Contours)
                {
                    var dxfLine = new netDxf.Entities.Line(
                        new netDxf.Vector3(line.StartX, line.StartY, 0),
                        new netDxf.Vector3(line.EndX, line.EndY, 0));

                    dxfLine.Layer = cutLayer;
                    dxf.Entities.Add(dxfLine);
                }

                var dxfText = new netDxf.Entities.Text(
                    piece.UniqueCode,
                    new netDxf.Vector3(piece.CentroidX, piece.CentroidY, 0),
                    0.05);

                dxfText.Layer = scoreLayer;
                dxf.Entities.Add(dxfText);
            }

            dxf.Save(Path.Combine(_dto.ExportDirectory, $"Cardboard_Cut_Sheet_{sheet.SheetNumber}.dxf"));
        }

        _dto.Success = true;
    }

    public void SetResult(List<string> _telemetry) => Result = _dto.Success;
}

public class Export_NestedSheetsToDXFDto : Dto
{
    public List<DxfExportSheet> Sheets { get; set; }
    public string ExportDirectory { get; set; }
    public bool Success { get; set; }
}
