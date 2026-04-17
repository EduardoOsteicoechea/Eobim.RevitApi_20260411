using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text.Json;

namespace Eobim.RevitApi.Framework;

public abstract class ExternalCommand<Dto> : IExternalCommand where Dto : class, IDto, new()
{
    protected Document? _doc;
    protected string? _commandName;
    protected FileSystemManager? _fileSystemManager;
    protected CommandObservableData? _commandObservableData;
    protected Dto _dto = new();
    private readonly List<(Action<List<string>> action, bool mustLogAction, TransactionManagementOptions transactionManagementOption)> _actions = [];

    protected void Add(Action<List<string>> a, bool mustLogAction = false, TransactionManagementOptions b = TransactionManagementOptions.TransactionlessAction)
    {
        _actions.Add((a, mustLogAction, b));
    }

    public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (commandData.Application.ActiveUIDocument == null)
        {
            message = "Please open a Revit project before running this command.";

            return Autodesk.Revit.UI.Result.Cancelled;
        }

        SetCriticalVariables(commandData);

        SetActions();

        using (TransactionGroup transGroup = new TransactionGroup(_doc, _commandName))
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
                if (transGroup.HasStarted())
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

    private void SetCriticalVariables(ExternalCommandData commandData)
    {
        _doc = commandData.Application.ActiveUIDocument.Document;

        _commandName = this.GetType().Name;

        _fileSystemManager = new FileSystemManager(_doc.Title, _commandName);

        _commandObservableData = new CommandObservableData
        {
            DocumentTitle = _doc.Title,
            CommandName = _commandName
        };
    }

    protected abstract void SetActions();

    private void ExecuteCorrespondingWorkflowTransactionApproach()
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
            //var actionName = action.GetType().Name;
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

        var observableAction = new CommandObservableAction
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

            var failure = new CommandObservableDataFailure
            {
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                ActionNumber = actionNumber,
            };

            observableAction.Failure = failure;

            _commandObservableData!.Failure = failure;

            throw;
        }
        finally
        {
            ////////////////
            // Collect Command Action telemetry
            //////////////// 

            if (mustReportTelemetry) observableAction.Telemetry = telemetryCollector;

            _commandObservableData!.ActionsNames.Add(observableAction.Name);

            _commandObservableData!.Actions.Add(observableAction);
        }
    }

    private void RecordData()
    {
        ////////////////
        // Convert command data to observable object
        //////////////// 

        var convertedData = _dto.ToObservableObject();

        ////////////////
        // Append command data to telemetry collector
        ////////////////

        _commandObservableData!.Data = convertedData;

        ////////////////
        // Serialize telemetry collector
        ////////////////

        var serializedData = JsonSerializer.Serialize(
            _commandObservableData,
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