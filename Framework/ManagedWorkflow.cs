using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text.Json;

namespace Eobim.RevitApi.Framework;


public abstract class ExternalCommand<TArgs, Dto, TResult> : ManagedWorkflow<TArgs, Dto, TResult>, IExternalCommand where Dto : class, IDto, new()
{
    protected override void SetCriticalVariables()
    {
        _doc = _commandData!.Application.ActiveUIDocument.Document;

        _workflowName = this.GetType().Name;

        _fileSystemManager = new FileSystemManager(_doc.Title, _workflowName);

        _workflowObservableData = new WorkflowObservableData
        {
            DocumentTitle = _doc.Title,
            WorkflowName = _workflowName,
        };
    }
}

public abstract class MultistepObservableAction<TArgs, Dto, TResult> : ManagedWorkflow<TArgs, Dto, TResult>
    where Dto : class, IDto, new()
{
    public string _parentCommandName { get; protected set; }

    public MultistepObservableAction(Document doc, string parentCommandName)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc), "Please provide a valid Revit Document before running this workflow.");
        _parentCommandName = parentCommandName;
    }

    protected override void SetCriticalVariables()
    {
        _workflowName = this.GetType().Name;

        _fileSystemManager = new FileSystemManager(_doc!.Title, _parentCommandName, _workflowName);

        _workflowObservableData = new WorkflowObservableData
        {
            DocumentTitle = _doc.Title,
            WorkflowName = _workflowName,
        };
    }
}

public interface ISubworkflow<TArgs, Dto, TResult>
{
    public void SafelyInitializeInputs(object[] args);
    public void SafelyInitializeInputs(TArgs args);
    public void Execute(int executedActionCounter);
    public TResult Result { get; set; }
}


public abstract class ManagedWorkflow<TArgs, Dto, TResult> : ISubworkflow<TArgs, Dto, TResult> where Dto : class, IDto, new()
{
    protected ExternalCommandData? _commandData;
    protected Document? _doc;
    protected string? _parentWorkflowName;
    protected string? _workflowName;
    protected FileSystemManager? _fileSystemManager;
    protected WorkflowObservableData? _workflowObservableData;
    protected Dto _dto = new();
    protected bool _isRolledBack = false;
    public TResult Result { get; set; }
    public int _executedActionCounter { get; set; } = 0;

    private readonly List<(Action<List<string>> action, bool mustLogAction, TransactionManagementOptions transactionManagementOption)> _actions = [];

    protected void Add(Action<List<string>> a, bool mustLogAction = true, TransactionManagementOptions b = TransactionManagementOptions.TransactionlessAction)
    {
        _actions.Add((a, mustLogAction, b));
    }

    public abstract void SafelyInitializeInputs(object[] args);

    public abstract void SafelyInitializeInputs(TArgs args);



    protected UResult RunSubworkflow<TSubworkflow, TSubDto, UResult>(object[] args)
    where TSubworkflow : ISubworkflow<TArgs, TSubDto, UResult>
    where TSubDto : class, IDto, new()
    {
        Type subworkflowType = typeof(TSubworkflow);
        var subWorkflow = (TSubworkflow)Activator.CreateInstance(subworkflowType, [_doc!, _workflowName!]);
        subWorkflow!.SafelyInitializeInputs(args);
        subWorkflow.Execute(_executedActionCounter);
        if (subWorkflow.Result is null) throw new NullReferenceException($"null result in {subWorkflow.GetType().FullName}");
        return subWorkflow.Result;
    }



    protected UResult RunSubworkflow<TSWArgs, TSubworkflow, TSubDto, UResult>(TSWArgs args)
    where TSubworkflow : ISubworkflow<TSWArgs, TSubDto, UResult>
    where TSubDto : class, IDto, new()
    {
        Type subworkflowType = typeof(TSubworkflow);
        var subWorkflow = (TSubworkflow)Activator.CreateInstance(subworkflowType, [_doc!, _workflowName!]);
        subWorkflow!.SafelyInitializeInputs(args);
        subWorkflow.Execute(_executedActionCounter);
        if (subWorkflow.Result is null) throw new NullReferenceException($"null result in {subWorkflow.GetType().FullName}");
        return subWorkflow.Result;
    }



    public void Execute(int executedActionCounter = 0)
    {
        SetCriticalVariables();
        SetActions();
        // NEVER REMOVE THE FOLLOWING try-finally bellow.
        // This enables the telemetry workflow to report errors and processed data at this point even in failures.
        // Don't catch here, let it bubble up to the caller
        try
        {
            ExecuteCorrespondingWorkflowTransactionApproach();
        }
        finally
        {
            RecordData(executedActionCounter);
        }
    }

    /// <summary>
    /// When non-null, the first N workflow actions (1-based count) run inside a TransactionGroup that is assimilated
    /// before any remaining actions. Use for committing Revit geometry before file I/O that must not roll back DB work.
    /// </summary>
    protected virtual int? TransactionGroupGeometryPhaseLastActionOneBased => null;

    /// <summary>Runs after the geometry TransactionGroup has assimilated and before post-geometry actions (e.g. DXF snapshot).</summary>
    protected virtual void OnAfterGeometryTransactionGroupBeforeFileIo() { }

    public virtual Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        _commandData = commandData;

        if (_commandData.Application.ActiveUIDocument == null)
        {
            message = "Please open a Revit project before running this workflow.";
            return Autodesk.Revit.UI.Result.Cancelled;
        }

        SetCriticalVariables();

        SetActions();

        var geometryPhaseLastOneBased = TransactionGroupGeometryPhaseLastActionOneBased;

        if (geometryPhaseLastOneBased is null)
            return ExecuteExternalCommandSingleTransactionGroup(ref message, elements);

        return ExecuteExternalCommandSplitGeometryThenPostActions(ref message, elements, geometryPhaseLastOneBased.Value);
    }

    private Autodesk.Revit.UI.Result ExecuteExternalCommandSingleTransactionGroup(ref string message, ElementSet elements)
    {
        using (TransactionGroup? transGroup = new TransactionGroup(_doc, _workflowName))
        {
            try
            {
                transGroup.Start();

                ExecuteCorrespondingWorkflowTransactionApproach();

                transGroup.Assimilate();

                return Autodesk.Revit.UI.Result.Succeeded;
            }
            catch (Exception ex)
            {
                if (transGroup.HasStarted() == true)
                {
                    transGroup.RollBack();
                    _isRolledBack = true;
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

    private Autodesk.Revit.UI.Result ExecuteExternalCommandSplitGeometryThenPostActions(ref string message, ElementSet elements, int geometryActionsOneBasedCount)
    {
        if (geometryActionsOneBasedCount < 1 || geometryActionsOneBasedCount > _actions.Count)
            throw new InvalidOperationException($"{nameof(TransactionGroupGeometryPhaseLastActionOneBased)} must be between 1 and the registered action count ({_actions.Count}).");

        var geometryExclusiveEndZeroBased = geometryActionsOneBasedCount;
        var geometryAssimilated = false;

        try
        {
            using (TransactionGroup transGroup = new TransactionGroup(_doc, _workflowName))
            {
                transGroup.Start();
                try
                {
                    for (int i = 0; i < geometryExclusiveEndZeroBased; i++)
                        ExecuteParticularizedStyleForSingleAction(i);

                    transGroup.Assimilate();
                    geometryAssimilated = true;
                }
                catch
                {
                    if (transGroup.HasStarted() == true)
                        transGroup.RollBack();

                    throw;
                }
            }

            OnAfterGeometryTransactionGroupBeforeFileIo();

            for (int i = geometryExclusiveEndZeroBased; i < _actions.Count; i++)
                ExecuteParticularizedStyleForSingleAction(i);

            return Autodesk.Revit.UI.Result.Succeeded;
        }
        catch (Exception ex)
        {
            if (!geometryAssimilated)
                _isRolledBack = true;

            message = ex.Message;
            return Autodesk.Revit.UI.Result.Failed;
        }
        finally
        {
            RecordData();
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
            ExecuteParticularizedStyleForSingleAction(i);
    }

    private void ExecuteParticularizedStyleForSingleAction(int i)
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
        _executedActionCounter++;

        var observableAction = new WorkflowObservableAction
        {
            Name = action.Method.Name,
            ActionNumber = actionNumber,
        };

        var telemetryCollector = new List<string>();

        try
        {
            action.Invoke(telemetryCollector);
        }
        catch (Exception ex)
        {
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
            if (mustReportTelemetry) observableAction.Telemetry = telemetryCollector;

            _workflowObservableData!.ActionsNames.Add(observableAction.Name);

            _workflowObservableData!.Actions.Add(observableAction);
        }
    }

    protected void RecordData(int executedActionCounter = 0)
    {
        ////////////////
        // Convert workflow data to observable object
        //////////////// 

        object convertedData;

        if (_isRolledBack)
        {
            // FATAL SHIELD: Do NOT touch the DTO. The geometry pointers are dead!
            convertedData = "[Serialization Skipped: Transaction Rolled Back. DTO contains dead C++ pointers.]";
        }
        else
        {
            // Safe to read
            convertedData = _dto.ToObservableObject();
        }

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

        _fileSystemManager?.WriteTelemetryFile(serializedData, executedActionCounter);
    }
}