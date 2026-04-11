using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Eobim.RevitApi.Framework;

public abstract class ExternalCommand<Dto> : IExternalCommand where Dto : new()
{
    protected Document? _doc;
    protected Messenger? _messenger;
    protected FileSystemManager? _fileSystemManager;
    private readonly List<(Action action, TransactionManagementOptions transactionManagementOption)> _actions = [];
    protected abstract void Prepare(); // store the actions with the add method
    protected Dto _dto = new();
    protected void Add(Action a, TransactionManagementOptions b = TransactionManagementOptions.SingleTransaction)
    {
        _actions.Add((a, b));
    }
    public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        _doc = commandData.Application.ActiveUIDocument.Document;
        var commandName = this.GetType().Name;
        _messenger = new Messenger(_doc, commandName);
        _fileSystemManager = new FileSystemManager(_doc.Title, commandName);
        Prepare();
        return Workflow();
    }
    protected Autodesk.Revit.UI.Result Workflow()
    {
        try
        {
            ExecuteCorrespondingWorkflowTransactionApproach();
            return Autodesk.Revit.UI.Result.Succeeded;
        }
        catch (Exception ex)
        {
			_messenger?.Exception(ex);
            return Autodesk.Revit.UI.Result.Failed;
        }
        finally
        {
            _fileSystemManager?.WriteFile(_messenger?.Dump());
        }
    }
    private void ExecuteCorrespondingWorkflowTransactionApproach()
    {
        var transactionManagementOptions = _actions.Select(a => a.transactionManagementOption).ToList();

        if (transactionManagementOptions.Any(a => a.Equals(TransactionManagementOptions.DedicatedTransaction)))
        {
            ParticularizedTransactionsWorkflow();
        }
        else if (transactionManagementOptions.All(a => a.Equals(TransactionManagementOptions.Transactionless)))
        {
            TransactionlessWorkflow();
        }
        else
        {
            SingleTransactionWorkflow();
        }
    }
    private void ParticularizedTransactionsWorkflow()
    {
        var actionsCount = _actions.Count; // because property access is more memory intensive, we locally store it.
        for (int i = 0; i < actionsCount; i++)
        {
            var action = _actions[i].action;
            var transactionManagementOption = _actions[i].transactionManagementOption;

            if (transactionManagementOption.Equals(TransactionManagementOptions.DedicatedTransaction))
            {
                using (Transaction t = new Transaction(_doc, action.GetType().Name))
                {
                    try
                    {
                        t.Start();
                        _messenger?.Action(action.GetType().Name);
                        RunAction(action);
                        t.Commit();
                    }
                    catch
                    {
                        t.RollBack();
                        throw;
                    }
                }
            }
            else
            {
                RunAction(action);
            }
        }
    }
    private void TransactionlessWorkflow()
    {
        var actionsCount = _actions.Count; // because property access is more memory intensive, we locally store it.
        for (int i = 0; i < actionsCount; i++)
        {
            RunAction(_actions[i].action);
        }
    }

    private void SingleTransactionWorkflow()
    {
        using (Transaction t = new Transaction(_doc, this.GetType().Name))
        {
            try
            {
                t.Start();
                var actionsCount = _actions.Count; // because property access is more memory intensive, we locally store it.
                for (int i = 0; i < actionsCount; i++)
                {
                    var action = _actions[i].action;
                    _messenger?.Action(action.Method.Name);
                    RunAction(action);
                }
                t.Commit();
            }
            catch
            {
                t.RollBack();
                throw;
            }
        }
    }

    private static void RunAction(Action action)
    {
        action.Invoke();
    }
}