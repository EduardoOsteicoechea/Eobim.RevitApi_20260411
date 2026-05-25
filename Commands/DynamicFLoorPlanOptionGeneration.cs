using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
namespace Eobim.RevitApi.Commands;


[Transaction(TransactionMode.Manual)]
public class DynamicFLoorPlanOptionGeneration : ExternalCommand<object, DynamicFLoorPlanOptionGenerationDto, bool>
{
    protected override void SetActions()
    {
        Add(GetAllColumns);
        Add(SetResult);
    }

    public void GetAllColumns(List<string> _telemetry)
    {
        var result = new FilteredElementCollector(_doc!)
            .WhereElementIsNotElementType()
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .ToElements()
            .ToList();

        if (result is null)
        {
            throw new ArgumentException("No columns found.");
        }
        else
        {
            _telemetry.Add($"{result.Count} found.");

            _dto.Columns = result;
        }
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = true;
    }
}

public class DynamicFLoorPlanOptionGenerationDto : Dto
{
    public List<Element> Columns { get; set; }
}