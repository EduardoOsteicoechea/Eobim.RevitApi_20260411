using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
using Eobim.RevitApi.MultiStepActions;

namespace Eobim.RevitApi.Commands;

[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public partial class GenerateMarkedFloorsDFMA : Framework.ExternalCommand<bool, GenerateMarkedFloorsDFMADto, object>
{
    public override void SafelyInitializeInputs(bool args) { }

    protected override void SetActions()
    {
        /* 1 */
        Add(PrepareCarboardFamily);
        /* 2 */
        Add(PrepareSheetFamily);
        /* 3 */
        Add(GetInterestFloors);
        /* 4 */
        Add(RunGenerateMarkedFloorsDFMAForEachInterestFloor);
    }

    /* 1 */
    public void PrepareCarboardFamily(List<string> _telemetry)
    {
        _dto.CommonCarboardFamilySymbol = RunSubworkflow<
            RevitFamily_EntirelySetForUssageInRevitUIArgs,
            RevitFamily_EntirelySetForUssageInRevitUI,
            RevitFamily_EntirelySetForUssageInRevitUIDto,
            FamilySymbol
        >(
            new(
                FamilyPath: @"C:\Users\eduar\Desktop\Room_003\Revit2027\Carboard_Segment_001_adaptative.rfa",
                FamilyName: "Carboard_Segment_001_adaptative",
                FamilyTypeName: "Type 1"
            )
        );
    }

    /* 2 */
    public void PrepareSheetFamily(List<string> _telemetry)
    {
        _dto.SheetFamilySymbol = RunSubworkflow<
            RevitFamily_EntirelySetForUssageInRevitUIArgs,
            RevitFamily_EntirelySetForUssageInRevitUI,
            RevitFamily_EntirelySetForUssageInRevitUIDto,
            FamilySymbol
        >(
            new(
                FamilyPath: @"C:\Users\eduar\Desktop\Room_003\Revit2027\Letter_Sheet_001.rfa",
                FamilyName: "Letter_Sheet_001",
                FamilyTypeName: "Margin_only"
            )
        );
    }

    /* 3 */
    public void GetInterestFloors(List<string> _telemetry)
    {
        var stringEqualsEvaluator = new FilterStringEquals();

        var markValue = "DFMA_target";

        var parameterValueProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_MARK));
        var filterStringRule = new FilterStringRule(parameterValueProvider, stringEqualsEvaluator, markValue);
        var filter = new ElementParameterFilter(filterStringRule);

        var result = new FilteredElementCollector(_doc!)
            .OfClass(typeof(Floor))
            .WherePasses(filter)
            .Cast<Floor>()
            .ToList();

        if (result is null || result.Count.Equals(0))
        { 
            StopWorkflow($"No floors marked with: {markValue}");
        }

        _telemetry.Add($"Marked floor count: {result.Count}");

        _dto.InterestFloors = result;
    }

    /* 4 */
    public void RunGenerateMarkedFloorsDFMAForEachInterestFloor(List<string> _telemetry)
    {
        foreach (var item in _dto.InterestFloors!)
        {
            RunSubworkflow<
                GenerateMarkedFloorsDFMASingleItemArgs,
                GenerateMarkedFloorsDFMASingleItem,
                GenerateMarkedFloorsDFMASingleItemDto,
                bool
            >(
                new(
                    InterestFloor: item,
                    CommonCarboardFamilySymbol: _dto.CommonCarboardFamilySymbol!,
                    SheetFamilySymbol: _dto.SheetFamilySymbol!
                )
            );
        }
    }
}


public class GenerateMarkedFloorsDFMADto : Dto
{
    [Print(nameof(TypeFormatter.FamilySymbol))]
    public FamilySymbol? CommonCarboardFamilySymbol { get; set; }

    [Print(nameof(TypeFormatter.FamilySymbol))]
    public FamilySymbol? SheetFamilySymbol { get; set; }

    [Print(nameof(TypeFormatter.FloorList))]
    public List<Floor>? InterestFloors { get; set; }
}