//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Autodesk.Revit.DB;
//using Eobim.RevitApi.Framework;

//namespace Eobim.RevitApi;

//public class RevitSheets_CreateAndPlaceDraftingViewsWorkflow(Document doc, string workflowName)
//    : MultistepObservableAction<RevitSheets_CreateAndPlaceDraftingViewsDto, List<ViewSheet>>(doc, workflowName)
//{
//    public override void SafelyInitializeInputs(object[] args)
//    {
//        if (args == null || args.Length == 0)
//            throw new ArgumentException("Drafting Views must be provided.");

//        _dto.DraftingViews = args[0] as List<ViewDrafting> ?? throw new ArgumentException("First argument must be List<ViewDrafting>.");
//    }

//    protected override void SetActions()
//    {
//        Add(FetchTitleBlockId);
//        Add(CreateSheetsAndViewports);
//        Add(SetResult);
//    }

//    public void FetchTitleBlockId(List<string> _telemetry)
//    {
//        // Find the first available TitleBlock in the project
//        var titleBlockId = new FilteredElementCollector(_doc)
//            .OfCategory(BuiltInCategory.OST_TitleBlocks)
//            .WhereElementIsElementType()
//            .FirstElementId();

//        if (titleBlockId == ElementId.InvalidElementId)
//        {
//            _telemetry.Add("Warning: No TitleBlocks loaded in the project. Creating empty sheets without a TitleBlock.");
//        }

//        _dto.TitleBlockId = titleBlockId;
//    }

//    public void CreateSheetsAndViewports(List<string> _telemetry)
//    {
//        _dto.AssemblySheets = new List<ViewSheet>();
//        int startingSheetNumber = 100;

//        foreach (var draftingView in _dto.DraftingViews)
//        {
//            // 1. Create the Sheet
//            var sheet = ViewSheet.Create(_doc, _dto.TitleBlockId);
//            sheet.Name = "DFMA Assembly Nested Layout";

//            // Ensure a unique sheet number to prevent Revit exceptions
//            string sheetNum = $"A{startingSheetNumber}";
//            while (IsSheetNumberInUse(sheetNum))
//            {
//                startingSheetNumber++;
//                sheetNum = $"A{startingSheetNumber}";
//            }
//            sheet.SheetNumber = sheetNum;

//            // 2. Determine Viewport placement point
//            // XYZ.Zero usually places the center of the viewport at the bottom-left of the sheet.
//            // 1.5, 1.0 (in feet) pushes it safely into the middle of a standard Title Block.
//            XYZ viewportCenter = new XYZ(1.5, 1.0, 0);

//            // 3. Place the Drafting View onto the Sheet
//            if (Viewport.CanAddViewToSheet(_doc, sheet.Id, draftingView.Id))
//            {
//                Viewport.Create(_doc, sheet.Id, draftingView.Id, viewportCenter);
//                _telemetry.Add($"Created Sheet {sheet.SheetNumber} and attached Drafting View '{draftingView.Name}'.");
//            }
//            else
//            {
//                _telemetry.Add($"Error: Could not add Drafting View '{draftingView.Name}' to Sheet {sheet.SheetNumber}.");
//            }

//            _dto.AssemblySheets.Add(sheet);
//            startingSheetNumber++;
//        }
//    }

//    private bool IsSheetNumberInUse(string sheetNumber)
//    {
//        return new FilteredElementCollector(_doc)
//            .OfClass(typeof(ViewSheet))
//            .Cast<ViewSheet>()
//            .Any(s => s.SheetNumber.Equals(sheetNumber, StringComparison.OrdinalIgnoreCase));
//    }

//    public void SetResult(List<string> _telemetry)
//    {
//        Result = _dto.AssemblySheets;
//    }
//}

//public class RevitSheets_CreateAndPlaceDraftingViewsDto : Dto
//{
//    public List<ViewDrafting> DraftingViews { get; set; }
//    public ElementId TitleBlockId { get; set; }
//    public List<ViewSheet> AssemblySheets { get; set; }
//}