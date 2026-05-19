using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
using System.IO;
using System.Windows;

namespace Eobim.RevitApi.MultiStepActions;

public record RevitFamily_EntirelySetForUssageInRevitUIArgs(string FamilyPath, string FamilyName, string FamilyTypeName);

public class RevitFamily_EntirelySetForUssageInRevitUI(Document doc, string workflowName)
    : 
MultistepObservableAction<
    RevitFamily_EntirelySetForUssageInRevitUIArgs, 
    RevitFamily_EntirelySetForUssageInRevitUIDto, 
    FamilySymbol
> (doc, workflowName)
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
        Add(LoadCommonCardboardFamily, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 2 */
        Add(GetCommonCardboardFamilySymbol);
        /* 3 */
        Add(ActivateCommonCardboardFamilySymbol, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 4 */
        Add(SetResult);
    }

    public void LoadCommonCardboardFamily(List<string> _telemetry)
    {
        if (string.IsNullOrEmpty(_dto.FamilyPath) || !File.Exists(_dto.FamilyPath))
        {
            throw new ArgumentException($"Invalid Path: {_dto.FamilyPath}");
        }

        bool didLoad = doc.LoadFamily(_dto.FamilyPath, out Family _);

        if (!didLoad)
        {
            _telemetry.Add("Note: doc.LoadFamily returned false. The family might already be loaded in the document or requires overwrite options.");
        }
    }

    public void GetCommonCardboardFamilySymbol(List<string> _telemetry)
    {
        FilteredElementCollector collector = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol));

        var result = collector
            .Cast<FamilySymbol>()
            .FirstOrDefault(
                sym =>
                    sym.FamilyName == _dto.FamilyName
                    &&
                    sym.Name == _dto.FamilyTypeName
                );

        if (result is null)
        {
            throw new Exception($"Failed to find FamilySymbol. FamilyName: '{_dto.FamilyName}', TypeName: '{_dto.FamilyTypeName}'. Ensure the family was loaded successfully.");
        }

        _dto.FamilySymbol = (FamilySymbol)result;
    }

    public void ActivateCommonCardboardFamilySymbol(List<string> _telemetry)
    {
        _dto.FamilySymbol.Activate();

        _doc!.Regenerate();
    }

    public void SetResult(List<string> _telemetry)
    {
        Result = _dto.FamilySymbol;
    }
}

public class RevitFamily_EntirelySetForUssageInRevitUIDto : Dto
{
    [Print(nameof(TypeFormatter.String))]
    public string FamilyPath { get; set; }

    [Print(nameof(TypeFormatter.String))]
    public string FamilyName { get; set; }

    [Print(nameof(TypeFormatter.String))]
    public string FamilyTypeName { get; set; }

    [Print(nameof(TypeFormatter.FamilySymbol))]
    public FamilySymbol FamilySymbol { get; set; }
}