using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text.Json;

namespace Eobim.RevitApi.Framework;


public abstract class ExternalCommand<Dto, TResult> : ManagedWorkflow<Dto, TResult>, IExternalCommand where Dto : class, IDto, new()
{
    protected override void SetCriticalVariables()
    {
        _doc = _commandData!.Application.ActiveUIDocument.Document;

        _workflowName = this.GetType().Name;

        _fileSystemManager = new FileSystemManager(_doc.Title, _workflowName);

        _workflowObservableData = new WorkflowObservableData
        {
            DocumentTitle = _doc.Title,
            WorkflowName = _workflowName
        };
    }
}

public abstract class MultistepObservableAction<Dto, TResult> : ManagedWorkflow<Dto, TResult>
    where Dto : class, IDto, new()
{
    public string _parentCommandName { get; protected set; }

    public MultistepObservableAction(Document doc, string parentCommandName)
    {
        _doc = doc;
        _parentCommandName = parentCommandName;
    }

    protected override void SetCriticalVariables()
    {
        _workflowName = this.GetType().Name;

        _fileSystemManager = new FileSystemManager(_doc!.Title, _workflowName, _parentCommandName);

        _workflowObservableData = new WorkflowObservableData
        {
            DocumentTitle = _doc.Title,
            WorkflowName = _workflowName
        };
    }
}

public interface ISubworkflow <Dto, TResult>
{
    public void SafelyInitializeInputs(object[] args);
    public void Execute();
    public TResult Result { get; set; }
}


public abstract class ManagedWorkflow<Dto, TResult>: ISubworkflow<Dto, TResult> where Dto : class, IDto, new()
{
    protected ExternalCommandData? _commandData;
    protected Document? _doc;
    protected string? _parentWorkflowName;
    protected string? _workflowName;
    protected FileSystemManager? _fileSystemManager;
    protected WorkflowObservableData? _workflowObservableData;
    protected Dto _dto = new();
    public TResult Result { get; set; }
    private readonly List<(Action<List<string>> action, bool mustLogAction, TransactionManagementOptions transactionManagementOption)> _actions = [];

    protected void Add(Action<List<string>> a, bool mustLogAction = true, TransactionManagementOptions b = TransactionManagementOptions.TransactionlessAction)
    {
        _actions.Add((a, mustLogAction, b));
    }

    public abstract void SafelyInitializeInputs(object[] args);

    protected UResult RunSubworkflow<TSubworkflow, TSubDto, UResult>(object[] args)
    where TSubworkflow : ISubworkflow<TSubDto, UResult>
    where TSubDto : class, IDto, new()
    {
        Type subworkflowType = typeof(TSubworkflow);
        var subWorkflow = (TSubworkflow)Activator.CreateInstance(subworkflowType, [_doc!, _workflowName!]);
        subWorkflow!.SafelyInitializeInputs(args);
        subWorkflow.Execute();
        if (subWorkflow.Result is null) throw new NullReferenceException($"null result in {subWorkflow.GetType().FullName}");
        return subWorkflow.Result;
    }

    public void Execute()
    {
        SetCriticalVariables();

        if (_doc is null)
        {
            throw new Exception("Please provide a valid Revit Document before running this workflow.");
        }

        SetActions();

        bool isEntirelyTransactionless = _actions.All(a => a.transactionManagementOption == TransactionManagementOptions.TransactionlessAction);

        using (TransactionGroup? transGroup = isEntirelyTransactionless ? null : new TransactionGroup(_doc, _workflowName))
        {
            try
            {
                transGroup?.Start();

                ExecuteCorrespondingWorkflowTransactionApproach();

                transGroup?.Assimilate();
            }
            catch (Exception)
            {
                if (transGroup?.HasStarted() == true)
                {
                    transGroup.RollBack();
                }

                throw;
            }
            finally
            {
                RecordData();
            }
        }
    }

    public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        _commandData = commandData;

        if (_commandData.Application.ActiveUIDocument == null)
        {
            message = "Please open a Revit project before running this workflow.";
            return Autodesk.Revit.UI.Result.Cancelled;
        }

        SetCriticalVariables();

        SetActions();

        bool isEntirelyTransactionless = _actions.All(a => a.transactionManagementOption == TransactionManagementOptions.TransactionlessAction);

        using (TransactionGroup? transGroup = isEntirelyTransactionless ? null : new TransactionGroup(_doc, _workflowName))
        {
            try
            {
                transGroup?.Start();

                ExecuteCorrespondingWorkflowTransactionApproach();

                transGroup?.Assimilate();

                return Autodesk.Revit.UI.Result.Succeeded;
            }
            catch (Exception ex)
            {
                if (transGroup?.HasStarted() == true)
                {
                    transGroup.RollBack();
                }

                message = ex.Message;
                return Autodesk.Revit.UI.Result.Failed;
            }
            finally
            {
                RecordData();
            }
        }
    }

    protected abstract void SetCriticalVariables();

    protected abstract void SetActions();

    protected void ExecuteCorrespondingWorkflowTransactionApproach()
    {
        var transactionManagementOptions = _actions.Select(a => a.transactionManagementOption).ToList();

        var isDedicatedTransactionWorkflow = transactionManagementOptions.Any(a => a.Equals(TransactionManagementOptions.RequiresDedicatedTransactionForAction));
        var isTransactionlessWorkflow = transactionManagementOptions.All(a => a.Equals(TransactionManagementOptions.TransactionlessAction));

        if (isDedicatedTransactionWorkflow) ParticularizedTransactionsWorkflow();
        else if (isTransactionlessWorkflow) TransactionlessWorkflow();
        else SingleTransactionWorkflow();
    }

    private void ParticularizedTransactionsWorkflow()
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            var actionName = action.action.Method.Name;
            var actionTransactionManagementOption = action.transactionManagementOption;
            var actionRequiresTransaction = actionTransactionManagementOption
                is TransactionManagementOptions.RequiresDedicatedTransactionForAction
                or TransactionManagementOptions.RequiresEnclosingTransactionForCommand;

            if (actionRequiresTransaction)
            {
                using (Transaction t = new Transaction(_doc, actionName))
                {
                    try
                    {
                        t.Start();

                        ManageAction(action.action, action.mustLogAction, i + 1);

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
                ManageAction(action.action, action.mustLogAction, i + 1);
            }
        }
    }

    private void TransactionlessWorkflow()
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            ManageAction(action.action, action.mustLogAction, i + 1);
        }
    }

    private void SingleTransactionWorkflow()
    {
        using (Transaction t = new Transaction(_doc, this.GetType().Name))
        {
            try
            {
                t.Start();

                for (int i = 0; i < _actions.Count; i++)
                {
                    var action = _actions[i];
                    ManageAction(action.action, action.mustLogAction, i + 1);
                }

                t.Commit();
            }
            catch (Exception ex)
            {
                t.RollBack();

                throw;
            }
        }
    }

    private void ManageAction(Action<List<string>> action, bool mustReportTelemetry, int actionNumber)
    {
        ////////////////
        // Prepare Command Action telemetry
        ////////////////

        var observableAction = new WorkflowObservableAction
        {
            Name = action.Method.Name,
            ActionNumber = actionNumber,
        };

        var telemetryCollector = new List<string>();

        try
        {
            ////////////////
            // Run Command Action
            ////////////////

            action.Invoke(telemetryCollector);
        }
        catch (Exception ex)
        {
            ////////////////
            // Handle Comnnad Action failure
            //////////////// 

            var failure = new WorkflowObservableDataFailure
            {
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                ActionNumber = actionNumber,
            };

            observableAction.Failure = failure;

            _workflowObservableData!.Failure = failure;

            throw;
        }
        finally
        {
            ////////////////
            // Collect Command Action telemetry
            //////////////// 

            if (mustReportTelemetry) observableAction.Telemetry = telemetryCollector;

            _workflowObservableData!.ActionsNames.Add(observableAction.Name);

            _workflowObservableData!.Actions.Add(observableAction);
        }
    }

    protected void RecordData()
    {
        ////////////////
        // Convert workflow data to observable object
        //////////////// 

        var convertedData = _dto.ToObservableObject();

        ////////////////
        // Append workflow data to telemetry collector
        ////////////////

        _workflowObservableData!.Data = convertedData;

        ////////////////
        // Serialize telemetry collector
        ////////////////

        var serializedData = JsonSerializer.Serialize(
            _workflowObservableData,
            new JsonSerializerOptions
            {
                IncludeFields = true,
                WriteIndented = true,
            }
        );

        ////////////////
        // Write telemetry to file System
        ////////////////

        _fileSystemManager?.WriteTelemetryFile(serializedData);
    }
}