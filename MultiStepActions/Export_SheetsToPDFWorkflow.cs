//using System.IO;
//using System.Linq;
//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi;

//public class Export_SheetsToPDFWorkflow(Document doc, string workflowName)
//    : MultistepObservableAction<Export_SheetsToPDFDto, bool>(doc, workflowName)
//{
//    public override void SafelyInitializeInputs(object[] args)
//    {
//        if (args == null || args.Length < 2)
//            throw new ArgumentException("Insufficient arguments for PDF Export.");

//        _dto.SheetsToExport = args[0] as List<ViewSheet> ?? throw new ArgumentException("First argument must be List<ViewSheet>.");
//        _dto.ExportDirectory = args[1] as string ?? throw new ArgumentException("Second argument must be a valid directory path string.");
//    }

//    protected override void SetActions()
//    {
//        Add(ValidateAndPrepareDirectory);
//        Add(ExportPDF);
//        Add(SetResult);
//    }

//    public void ValidateAndPrepareDirectory(List<string> _telemetry)
//    {
//        if (string.IsNullOrWhiteSpace(_dto.ExportDirectory))
//            throw new ArgumentException("PDF export directory path is missing.");

//        _dto.ExportDirectory = Path.GetFullPath(_dto.ExportDirectory);

//        if (!Directory.Exists(_dto.ExportDirectory))
//        {
//            Directory.CreateDirectory(_dto.ExportDirectory);
//            _telemetry.Add($"Created PDF export directory: {_dto.ExportDirectory}");
//        }
//    }

//    public void ExportPDF(List<string> _telemetry)
//    {
//        if (_dto.SheetsToExport == null || !_dto.SheetsToExport.Any())
//        {
//            _telemetry.Add("Warning: No sheets were provided to export.");
//            _dto.Success = false;
//            return;
//        }

//        // 1. Gather the ElementIds of the newly created sheets
//        IList<ElementId> sheetIds = _dto.SheetsToExport.Select(s => s.Id).ToList();

//        // 2. Configure the PDF Options (Revit 2022+ Native API)
//        var pdfOptions = new PDFExportOptions
//        {
//            FileName = "DFMA_Assembly_Manual",
//            Combine = true,
//            // Note: ExportRange is removed. The API handles it implicitly via the sheetIds list!
//            PaperFormat = ExportPaperFormat.ISO_A0, // Adjust to match your TitleBlock size
//            ZoomType = ZoomType.Zoom,
//            ZoomPercentage = 100,
//            RasterQuality = RasterQualityType.High,
//            ColorDepth = ColorDepthType.Color,

//            // Clean up the drawing by hiding unnecessary Revit UI elements
//            HideUnreferencedViewTags = true,
//            HideScopeBoxes = true,
//            HideReferencePlane = true,
//            HideCropBoundaries = true
//        };

//        try
//        {
//            // 3. Execute the Export
//            _doc.Export(_dto.ExportDirectory, sheetIds, pdfOptions);
//            _telemetry.Add($"Successfully exported {_dto.SheetsToExport.Count} sheets to combined PDF at: {_dto.ExportDirectory}\\DFMA_Assembly_Manual.pdf");
//            _dto.Success = true;
//        }
//        catch (Exception ex)
//        {
//            // Same class of failures as DXF: locked file, open viewer, bad path — do not unwind the whole command.
//            _telemetry.Add($"PDF Export Failed: {ex.Message}");
//            _dto.Success = false;
//        }
//    }

//    public void SetResult(List<string> _telemetry)
//    {
//        Result = _dto.Success;
//    }
//}

//public class Export_SheetsToPDFDto : Dto
//{
//    public List<ViewSheet> SheetsToExport { get; set; }
//    public string ExportDirectory { get; set; }
//    public bool Success { get; set; }
//}