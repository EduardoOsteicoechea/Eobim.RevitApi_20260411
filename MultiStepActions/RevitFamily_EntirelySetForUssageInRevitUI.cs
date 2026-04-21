using Autodesk.Revit.DB;
using Eobim.RevitApi.Framework;
using System.IO;

namespace Eobim.RevitApi.MultiStepActions;

public class RevitFamily_EntirelySetForUssageInRevitUI(Document doc, string workflowName)
:
MultistepObservableAction<RevitFamily_EntirelySetForUssageInRevitUIDto, FamilySymbol>(doc, workflowName)
{
    public override void SafelyInitializeInputs(object[] args)
    {
        _dto.FamilyPath = args[0] as string;
        _dto.FamilyName = args[1] as string;
        _dto.FamilyTypeName = args[2] as string;
    }

    protected override void SetActions()
    {
        /* 1 */ Add(LoadCommonCardboardFamily);
        /* 2 */ Add(GetCommonCardboardFamilySymbol);
        /* 3 */ Add(ActivateCommonCardboardFamilySymbol, false, TransactionManagementOptions.RequiresDedicatedTransactionForAction);
        /* 4 */ Add(SetResult);
    }

    public void LoadCommonCardboardFamily(List<string> _telemetry)
    {
        if (!File.Exists(_dto.FamilyPath))
        {
            throw new ArgumentException("Invalid Path");
        }

        //doc.LoadFamily(_dto.FamilyPath, out Family result); // for some reason returns null;
        //if (result is null) throw new NullReferenceException();
        //_dto.Family = result;

        doc.LoadFamily(_dto.FamilyPath, out Family _);
    }

    public void GetCommonCardboardFamilySymbol(List<string> _telemetry)
    {
        FilteredElementCollector collector = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol));

        var result = collector
            .Cast<FamilySymbol>()
            .FirstOrDefault(
                sym => 
                    sym.FamilyName.Equals(_dto.FamilyName) 
                    && 
                    sym.Name.Equals(_dto.FamilyTypeName)
                );

        if (result is null) throw new NullReferenceException();

        _dto.FamilySymbol = result;
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


    //[Print(nameof(TypeFormatter.Family))] // See not about family at the top of the class.
    //public Family Family { get; set; }


    [Print(nameof(TypeFormatter.FamilySymbol))]
    public FamilySymbol FamilySymbol { get; set; }
}