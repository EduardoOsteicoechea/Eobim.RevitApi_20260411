using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;

namespace Eobim.RevitApi.MultiStepActions;

public record RevitFamily_EntirelySetForUssageInRevitUIArgs(string FamilyPath, string FamilyName, string FamilyTypeName);

public class RevitFamily_EntirelySetForUssageInRevitUI(Document doc, string workflowName)
    : MultistepObservableAction<
    RevitFamily_EntirelySetForUssageInRevitUIArgs,
    RevitFamily_EntirelySetForUssageInRevitUIDto,
    FamilySymbol
>(doc, workflowName)
{
    public override void SafelyInitializeInputs(RevitFamily_EntirelySetForUssageInRevitUIArgs args)
    {
        _dto.FamilyPath = args.FamilyPath;
        _dto.FamilyName = args.FamilyName;
        _dto.FamilyTypeName = args.FamilyTypeName;
    }

    protected override void SetActions()
    {
        /* 1 */
        Add(LoadCommonCardboardFamily, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 2 */
        Add(CheckAndLogFamilyTypes, true);
        /* 3 */
        Add(GetCommonCardboardFamilySymbol, true);
        /* 4 */
        Add(ActivateCommonCardboardFamilySymbol, true, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 5 */
        Add(SetResult);
    }

    public void LoadCommonCardboardFamily(List<string> _telemetry)
    {
        _telemetry.Add($"Attempting to load family from path: '{_dto.FamilyPath}'");

        if (string.IsNullOrEmpty(_dto.FamilyPath) || !File.Exists(_dto.FamilyPath))
        {
            throw new ArgumentException($"Invalid Path: {_dto.FamilyPath}");
        }

        bool didLoad = doc.LoadFamily(_dto.FamilyPath, out Family family);

        if (!didLoad)
        {
            _telemetry.Add("Note: doc.LoadFamily returned false. The family is likely already loaded in the document.");
        }

        _dto.Family = family;
    }

    public void CheckAndLogFamilyTypes(List<string> _telemetry)
    {
        if (_dto.Family != null)
        {
            string categoryName = _dto.Family.FamilyCategory != null ? _dto.Family.FamilyCategory.Name : "Unassigned/Unknown";
            _telemetry.Add($"Family Data -> Name: '{_dto.Family.Name}', Category: '{categoryName}', IsEditable: {_dto.Family.IsEditable}");

            var loadedSymbols = _dto.Family.GetFamilySymbolIds()
                .Select(id => doc.GetElement(id) as FamilySymbol)
                .Where(symbol => symbol != null)
                .ToList();

            _telemetry.Add($"--- LOADED TYPES IN FAMILY '{_dto.Family.Name}' ({loadedSymbols.Count} found) ---");

            foreach (var symbol in loadedSymbols)
            {
                _telemetry.Add($"  -> Type: '{symbol!.Name}' [ID: {symbol.Id}]");
            }

            _telemetry.Add("--------------------------------------------------");

            var loadedTypeNames = loadedSymbols.Select(s => s!.Name).ToList();

            if (!string.IsNullOrEmpty(_dto.FamilyTypeName) && !loadedTypeNames.Contains(_dto.FamilyTypeName))
            {
                throw new Exception($"Aborting early: The type '{_dto.FamilyTypeName}' does not exist in the family '{_dto.FamilyName}'. Available types are: [{string.Join(", ", loadedTypeNames)}].");
            }
        }
        else
        {
            _telemetry.Add("Warning: The Family object is null in the DTO. Could not verify loaded types. Proceeding to collector search.");
        }
    }

    public void GetCommonCardboardFamilySymbol(List<string> _telemetry)
    {
        _telemetry.Add($"Searching for FamilySymbol. FamilyName: '{_dto.FamilyName}', TypeName: '{_dto.FamilyTypeName}'");

        var stringEqualsEvaluator = new FilterStringEquals();

        var familyNameProvider = new ParameterValueProvider(new ElementId((long)BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM));
        var familyNameRule = new FilterStringRule(familyNameProvider, stringEqualsEvaluator, _dto.FamilyName);
        var familyNameFilter = new ElementParameterFilter(familyNameRule);

        var familyTypeNameProvider = new ParameterValueProvider(new ElementId((long)BuiltInParameter.SYMBOL_NAME_PARAM));
        var familyTypeNameRule = new FilterStringRule(familyTypeNameProvider, stringEqualsEvaluator, _dto.FamilyTypeName);
        var familyTypeNameFilter = new ElementParameterFilter(familyTypeNameRule);

        var logicalAndFilter = new LogicalAndFilter(familyNameFilter, familyTypeNameFilter);

        var result = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .WherePasses(logicalAndFilter)
            .FirstElement() as FamilySymbol;

        if (result is null)
        {
            throw new Exception($"Failed to find FamilySymbol. FamilyName: '{_dto.FamilyName}', TypeName: '{_dto.FamilyTypeName}'. Ensure the family was loaded successfully and the type name matches exactly.");
        }

        _telemetry.Add($"Successfully found FamilySymbol -> ID: {result.Id}, Name: '{result.Name}', IsActive: {result.IsActive}");

        _dto.FamilySymbol = result;
    }

    public void ActivateCommonCardboardFamilySymbol(List<string> _telemetry)
    {
        bool wasActive = _dto.FamilySymbol!.IsActive;

        if (!wasActive)
        {
            _telemetry.Add($"FamilySymbol '{_dto.FamilySymbol.Name}' is not active. Activating now...");
            _dto.FamilySymbol.Activate();
            _doc!.Regenerate();
            _telemetry.Add($"FamilySymbol activated. Current IsActive status: {_dto.FamilySymbol.IsActive}");
        }
        else
        {
            _telemetry.Add($"FamilySymbol '{_dto.FamilySymbol.Name}' was already active. Skipping activation.");
        }
    }

    public void SetResult(List<string> _telemetry)
    {
        _telemetry.Add("Action complete. Setting result.");
        Result = _dto.FamilySymbol!;
    }
}

public class RevitFamily_EntirelySetForUssageInRevitUIDto : Dto
{
    [Print(nameof(TypeFormatter.String))]
    public string? FamilyPath { get; set; }

    [Print(nameof(TypeFormatter.String))]
    public string? FamilyName { get; set; }

    [Print(nameof(TypeFormatter.String))]
    public string? FamilyTypeName { get; set; }

    [Print(nameof(TypeFormatter.Family))]
    public Family? Family { get; set; }

    [Print(nameof(TypeFormatter.FamilySymbol))]
    public FamilySymbol? FamilySymbol { get; set; }
}