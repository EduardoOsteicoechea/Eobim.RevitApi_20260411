using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Eobim.RevitApi.Framework;



public interface ISubworkflow<TArgs, Dto, TResult>
{
    void InitializeFrameworkContext(Document doc, string parentWorkflowPath, int actionCounter, int? iterativeActionCounter = null);
    public void SafelyInitializeInputs(TArgs args);
    public void Execute(int executedActionCounter);
    public void StopWorkflow(string message, WorkflowInterruptionReason workflowInterruptionReason = WorkflowInterruptionReason.Error);
    public TResult Result { get; set; }
}


public abstract class MultistepObservableAction<TArgs, Dto, TResult>
    :
ManagedWorkflow<TArgs, Dto, TResult>,
ISubworkflow<TArgs, Dto, TResult>
where Dto : class, IDto, new()
{
    // Because this is instantiated inside an already running ExternalCommand, the Document is already available.
    public void InitializeFrameworkContext(Document doc, string parentWorkflowPath, int actionCounter, int? iterativeActionCounter = null)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc), "Please provide a valid Revit Document before running this workflow.");

        _doc = doc;
        _workflowName = this.GetType().Name;

        _workflowObservableData = new WorkflowObservableData
        {
            DocumentTitle = _doc!.Title,
            WorkflowName = _workflowName,
        };

        _fileSystemManager = new SubworkflowTelemetryFileSystemManager(_doc.Title, parentWorkflowPath, _workflowName, actionCounter, iterativeActionCounter);
    }
}




public abstract class ExternalCommand<TArgs, Dto, TResult>
    :
ManagedWorkflow<TArgs, Dto, TResult>,
IExternalCommand
where Dto : class, IDto, new()
{
    // This is required because the ExternalCommand obtains the Document from the ExternalCommandData.
    // Therefore, it the document can't be obtained in the constructor.
    // SetCriticalVariables is called at runtime, when the ExternalCommandData is available.
    protected override void SetCriticalVariables()
    {
        _doc = _commandData!.Application.ActiveUIDocument.Document;

        _workflowName = this.GetType().Name;

        _fileSystemManager = new ExternalCommandTelemetryFileSystemManager(_doc.Title, _workflowName);

        _workflowObservableData = new WorkflowObservableData
        {
            DocumentTitle = _doc.Title,
            WorkflowName = _workflowName,
        };
    }

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
        {
            return ExecuteExternalCommandSingleTransactionGroup(ref message, elements);
        }
        else
        {
            return ExecuteExternalCommandSplitGeometryThenPostActions(ref message, elements, geometryPhaseLastOneBased.Value);
        }
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

                // Add the HTML report generator here
                if (_fileSystemManager != null)
                {
                    TelemetryHtmlWriter.GenerateHtmlReport(_fileSystemManager.InstanceLogDirectory);
                }
            }
        }
    }

    private Autodesk.Revit.UI.Result ExecuteExternalCommandSplitGeometryThenPostActions(ref string message, ElementSet elements, int geometryActionsOneBasedCount)
    {
        if (geometryActionsOneBasedCount < 1 || geometryActionsOneBasedCount > _actions.Count)
        {
            throw new InvalidOperationException($"{nameof(TransactionGroupGeometryPhaseLastActionOneBased)} must be between 1 and the registered action count ({_actions.Count}).");
        }

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
                    {
                        if (_interruptRequestEmmitted)
                        {
                            break;
                        }

                        ExecuteParticularizedStyleForSingleAction(i);
                    }

                    transGroup.Assimilate();

                    geometryAssimilated = !_interruptRequestEmmitted;
                }
                catch
                {
                    if (transGroup.HasStarted() == true) transGroup.RollBack();

                    throw;
                }
            }

            OnAfterGeometryTransactionGroupBeforeFileIo();

            for (int i = geometryExclusiveEndZeroBased; i < _actions.Count; i++)
            {
                if (_interruptRequestEmmitted)
                {
                    break;
                }

                ExecuteParticularizedStyleForSingleAction(i);
            }

            return Autodesk.Revit.UI.Result.Succeeded;
        }
        catch (Exception ex)
        {
            if (!geometryAssimilated) _isRolledBack = true;

            message = ex.Message;

            return Autodesk.Revit.UI.Result.Failed;
        }
        finally
        {
            RecordData();
            
            // Add the HTML report generator here
            if (_fileSystemManager != null)
            {
                TelemetryHtmlWriter.GenerateHtmlReport(_fileSystemManager.InstanceLogDirectory);
            }
        }
    }
}


public abstract class ManagedWorkflow<TArgs, Dto, TResult>
    where Dto : class, IDto, new()
{
    protected ExternalCommandData? _commandData;
    protected Document? _doc;
    protected string? _parentWorkflowName;
    protected string? _workflowName;
    protected TelemetryFileSystemManager? _fileSystemManager;
    protected WorkflowObservableData? _workflowObservableData;
    protected Dto _dto = new();
    protected bool _isRolledBack = false;
    protected readonly List<(Action<List<string>> action, bool mustLogAction, TransactionManagementOptions transactionManagementOption)> _actions = [];

    public TResult Result { get; set; }
    public int _executedActionCounter { get; set; } = 0;
    public bool _interruptRequestEmmitted { get; set; } = false;

    //public abstract void SafelyInitializeInputs(TArgs args);
    public virtual void SafelyInitializeInputs(TArgs args) { }
    protected virtual void SetCriticalVariables() { }
    protected abstract void SetActions();


    protected void Add(Action<List<string>> a, bool mustLogAction = true, TransactionManagementOptions b = TransactionManagementOptions.TransactionlessAction)
    {
        _actions.Add((a, mustLogAction, b));
    }

    protected void ReportUnmanagedFailure(string message, string stackTrace = "Stopped at")
    {
        int actionIndex = _executedActionCounter - 1;

        string actionName = $"Action {actionIndex}";

        if (actionIndex >= 0 && actionIndex < _actions.Count)
        {
            actionName = _actions[actionIndex].action.Method.Name;
        }

        _workflowObservableData.Failure = new WorkflowObservableDataFailure
        {
            Message = message,
            StackTrace = $"{stackTrace}: '{actionName}'",
            ActionNumber = _executedActionCounter
        };
    }

    public void StopWorkflow(string message, WorkflowInterruptionReason workflowInterruptionReason = WorkflowInterruptionReason.Error)
    {
        _interruptRequestEmmitted = true;

        if (_workflowObservableData != null && workflowInterruptionReason.Equals(WorkflowInterruptionReason.Error))
        {
            ReportUnmanagedFailure($"Workflow intentionally stopped: {message}", $"Invoked via StopWorkflow() during action");
        }
    }

    protected virtual UResult RunSubworkflow<TSWArgs, TSubworkflow, TSubDto, UResult>(TSWArgs args, int? iterationCounter = null)
    where TSubworkflow : ISubworkflow<TSWArgs, TSubDto, UResult>, new() // <-- Note the new() constraint
    where TSubDto : class, IDto, new()
    {
        Type subworkflowType = typeof(TSubworkflow);
        TSubworkflow subWorkflow;

        try
        {
            // 1. Instantiate cleanly with NO arguments
            subWorkflow = Activator.CreateInstance<TSubworkflow>();
        }
        catch (Exception ex)
        {
            ReportUnmanagedFailure($"Failed to instantiate {subworkflowType.Name}", $"{subworkflowType.Name}");
            throw;
        }

        // 2. Inject the framework variables immediately
        subWorkflow.InitializeFrameworkContext(_doc!, _fileSystemManager!.InstanceLogDirectory, _executedActionCounter, iterationCounter);

        // 3. Proceed as normal
        subWorkflow.SafelyInitializeInputs(args);
        subWorkflow.Execute(_executedActionCounter);

        if (subWorkflow.Result is null)
        {
            throw new NullReferenceException($"null result in {subWorkflow.GetType().FullName}");
        }

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
            if (_interruptRequestEmmitted)
            {
                break;
            }

            ExecuteParticularizedStyleForSingleAction(i);
        }
    }

    protected void ExecuteParticularizedStyleForSingleAction(int i)
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
            if (_interruptRequestEmmitted)
            {
                break;
            }

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
                    if (_interruptRequestEmmitted)
                    {
                        break;
                    }

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



    private void ManageAction
    (
        Action<List<string>> action,
        bool mustReportTelemetry,
        int actionNumber
    )
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

    protected void RecordData(int executedActionCounter = 0) => _fileSystemManager?.WriteTelemetryFile(SafelySerializeWorkflowData(), executedActionCounter);

    protected string SafelySerializeWorkflowData()
    {
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

        _workflowObservableData!.Data = convertedData;

        return JsonSerializer.Serialize(
            _workflowObservableData,
            new JsonSerializerOptions
            {
                IncludeFields = true,
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }
        );
    }
}




public class TelemetryFileSystemManager
{
    private readonly string _mainLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eobim.Logs");
    protected string _revitDocumentLogDirectory { get; set; }
    public string InstanceLogDirectory { get; set; }
    protected string _actionName { get; set; }

    public TelemetryFileSystemManager(string revitDocumentTitle, string actionName)
    {
        _revitDocumentLogDirectory = Path.Combine(_mainLogDirectory, revitDocumentTitle);
        _actionName = actionName;
    }

    protected static void SafelyDeleteAndRecreateDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show
            (
                $"Failed to delete existing log directory: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );

            throw;
        }
    }

    public void WriteTelemetryFile(string? content, int workflowNumber)
    {
        var finalLogfilePath = Path.Combine(InstanceLogDirectory, $"{_actionName}.json");

        File.WriteAllText(finalLogfilePath, content ?? "Empty");
    }
}

public class ExternalCommandTelemetryFileSystemManager : TelemetryFileSystemManager
{
    public ExternalCommandTelemetryFileSystemManager(string revitDocumentTitle, string actionName)
        :
    base(revitDocumentTitle, actionName)
    {
        InstanceLogDirectory = Path.Combine(_revitDocumentLogDirectory, actionName);

        SafelyDeleteAndRecreateDirectory(InstanceLogDirectory);
    }
}

public class SubworkflowTelemetryFileSystemManager : TelemetryFileSystemManager
{
    public SubworkflowTelemetryFileSystemManager(string revitDocumentTitle, string parentActionLogDirectoryPath, string actionName, int actionNumber, int? iterativeActionCounter = null)
        :
    base(revitDocumentTitle, actionName)
    {
        if (iterativeActionCounter is null)
        {
            InstanceLogDirectory = Path.Combine(parentActionLogDirectoryPath, $"{actionNumber}_{actionName}");
        }
        else
        {
            InstanceLogDirectory = Path.Combine(parentActionLogDirectoryPath, $"{actionNumber}_{iterativeActionCounter}_{actionName}");
        }

        SafelyDeleteAndRecreateDirectory(InstanceLogDirectory);
    }
}

public enum WorkflowInterruptionReason
{
    Success,
    Error
}









public class TelemetryHtmlWriter
{
    /// <summary>
    /// Traverses the workflow log directory, compiles all JSON files into a single tree,
    /// and generates an interactive HTML viewer.
    /// </summary>
    /// <param name="rootDirectory">The InstanceLogDirectory of the main ExternalCommand</param>
    public static void GenerateHtmlReport(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory)) return;

        var rootNode = BuildTelemetryTree(rootDirectory);

        // Serialize the compiled tree into a formatted string
        string jsonString = rootNode.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        // Inject into the HTML template
        string htmlContent = GetHtmlTemplate().Replace("{{TELEMETRY_DATA}}", jsonString);

        // Save the HTML file alongside the main root directory
        var directoryInfo = new DirectoryInfo(rootDirectory);
        string reportName = $"{directoryInfo.Name}_Report.html";
        string outputPath = Path.Combine(directoryInfo.Parent!.FullName, reportName);

        File.WriteAllText(outputPath, htmlContent);
    }

    private static JsonNode BuildTelemetryTree(string directoryPath)
    {
        var node = new JsonObject();
        var dirInfo = new DirectoryInfo(directoryPath);

        node["WorkflowDirectory"] = dirInfo.Name;

        // 1. Grab the JSON file in the current directory level
        var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
        if (jsonFiles.Length > 0)
        {
            try
            {
                string content = File.ReadAllText(jsonFiles[0]);
                node["State"] = JsonNode.Parse(content);
            }
            catch (Exception ex)
            {
                node["State"] = $"[Failed to parse JSON: {ex.Message}]";
            }
        }

        // 2. Recursively grab subworkflows (subdirectories)
        // We use LINQ to parse the action number (and optional iteration number) 
        // from the folder name (e.g., "4_Area..." or "3_0_Face...") to sort numerically instead of alphabetically.
        var subDirs = Directory.GetDirectories(directoryPath)
            .OrderBy(dir =>
            {
                var name = new DirectoryInfo(dir).Name;
                var parts = name.Split('_');
                // Primary sort by the first number (Action Counter)
                return int.TryParse(parts[0], out int primary) ? primary : int.MaxValue;
            })
            .ThenBy(dir =>
            {
                var name = new DirectoryInfo(dir).Name;
                var parts = name.Split('_');
                // Secondary sort by the second number (Iterative Action Counter), if it exists
                return parts.Length > 1 && int.TryParse(parts[1], out int secondary) ? secondary : -1;
            })
            .ToArray();

        if (subDirs.Length > 0)
        {
            var subworkflowsArray = new JsonArray();

            foreach (var subDir in subDirs)
            {
                subworkflowsArray.Add(BuildTelemetryTree(subDir));
            }

            node["Subworkflows"] = subworkflowsArray;
        }

        return node;
    }

    //    private static string GetHtmlTemplate()
    //    {
    //        // Using standard string literal with single quotes for HTML/JS to avoid heavy escaping in C#
    //        return @"<!DOCTYPE html>
    //<html lang='en'>
    //<head>
    //    <meta charset='utf-8'>
    //    <title>Workflow Telemetry Report</title>
    //    <style>
    //        body { font-family: Consolas, 'Courier New', monospace; background: #1e1e1e; color: #d4d4d4; padding: 20px; font-size: 14px; }
    //        button { background: #333; color: #fff; border: 1px solid #555; padding: 5px 10px; cursor: pointer; border-radius: 3px; margin-right: 10px; }
    //        button:hover { background: #444; }
    //        details { margin-left: 10px; margin-bottom: 2px; }
    //        summary { cursor: pointer; padding: 3px; border-radius: 3px; font-weight: bold; }
    //        summary:hover { background: #2a2d2e; }
    //        .key { color: #9cdcfe; margin-right: 5px; }
    //        .string { color: #ce9178; }
    //        .number { color: #b5cea8; }
    //        .boolean { color: #569cd6; }
    //        .null { color: #c586c0; font-style: italic; }
    //        ul { list-style-type: none; padding-left: 20px; margin: 0; border-left: 1px solid #404040; }
    //        li { margin: 3px 0; }
    //        /*.root-container { background: #252526; padding: 20px; border-radius: 5px; border: 1px solid #333; }*/
    //    </style>
    //</head>
    //<body>
    //    <div id='app' class='root-container'></div>

    //    <script>
    //        const telemetryData = {{TELEMETRY_DATA}};

    //        function renderNode(data, keyName = null, isRoot = false) {

    //            if (data === null) {
    //                const span = document.createElement('span');
    //                span.className = 'null';
    //                span.textContent = 'null';
    //                return span;
    //            }

    //            const type = typeof data;

    //            if (type !== 'object') {
    //                const span = document.createElement('span');
    //                span.className = type;
    //                span.textContent = type === 'string' ? '""' + data + '""' : data;
    //                return span;
    //            }

    //            const details = document.createElement('details');

    //            if (isRoot || keyName === 'Subworkflows' || keyName === 'State') {
    //                details.open = true;
    //            }

    //            const summary = document.createElement('summary');
    //            const summaryText = document.createElement('span');
    //            summaryText.className = 'key';

    //            // Format arrays differently from objects in the summary

    //            const isArray = Array.isArray(data);
    //            summaryText.textContent = keyName ? keyName + (isArray ? ' [' + data.length + ']' : '') : (isArray ? 'Array' : 'Object');
    //            summary.appendChild(summaryText);
    //            details.appendChild(summary);

    //            const ul = document.createElement('ul');

    //            for (const key in data) {
    //                const li = document.createElement('li');

    //                if (typeof data[key] === 'object' && data[key] !== null) {
    //                    li.appendChild(renderNode(data[key], key));
    //                } else {
    //                    const keySpan = document.createElement('span');
    //                    keySpan.className = 'key';
    //                    keySpan.textContent = key + ': ';
    //                    li.appendChild(keySpan);
    //                    li.appendChild(renderNode(data[key]));
    //                }
    //                ul.appendChild(li);
    //            }

    //            details.appendChild(ul);

    //            return details;
    //        }

    //        document.getElementById('app').appendChild(renderNode(telemetryData, 'Execution Lifecycle', true));
    //    </script>
    //</body>
    //</html>";
    //    }

    //    private static string GetHtmlTemplate()
    //    {
    //        // Using standard string literal with single quotes for HTML/JS to avoid heavy escaping in C#
    //        return @"<!DOCTYPE html>
    //<html lang='en'>
    //<head>
    //    <meta charset='utf-8'>
    //    <title>Workflow Telemetry Report</title>
    //    <style>
    //        body { font-family: Consolas, 'Courier New', monospace; background: #1e1e1e; color: #d4d4d4; padding: 20px; font-size: 14px; }
    //        h2 { color: #569cd6; border-bottom: 1px solid #404040; padding-bottom: 10px; }
    //        .controls { margin-bottom: 20px; }
    //        button { background: #333; color: #fff; border: 1px solid #555; padding: 5px 10px; cursor: pointer; border-radius: 3px; margin-right: 10px; }
    //        button:hover { background: #444; }
    //        details { margin-left: 10px; margin-bottom: 2px; }
    //        summary { cursor: pointer; padding: 3px; border-radius: 3px; font-weight: bold; }
    //        summary:hover { background: #2a2d2e; }
    //        .key { color: #9cdcfe; margin-right: 5px; }
    //        .string { color: #ce9178; }
    //        .number { color: #b5cea8; }
    //        .boolean { color: #569cd6; }
    //        .null { color: #c586c0; font-style: italic; }
    //        ul { list-style-type: none; padding-left: 20px; margin: 0; border-left: 1px solid #404040; }
    //        li { margin: 3px 0; }
    //        .root-container { background: #252526; padding: 20px; border-radius: 5px; border: 1px solid #333; }

    //        /* New classes for subworkflows */
    //        .wf-success { color: #89d185; margin-right: 5px; } /* VS Code Green */
    //        .wf-error { color: #f14c4c; margin-right: 5px; }   /* VS Code Red */
    //        .wf-name { color: #dcdcaa; font-weight: normal; font-style: italic; } /* VS Code Yellow-ish */
    //    </style>
    //</head>
    //<body>
    //    <h2>Workflow Telemetry State</h2>
    //    <div class='controls'>
    //        <button onclick='toggleAll(true)'>Expand All</button>
    //        <button onclick='toggleAll(false)'>Collapse All</button>
    //    </div>
    //    <div id='app' class='root-container'></div>

    //    <script>
    //        const telemetryData = {{TELEMETRY_DATA}};

    //        function renderNode(data, keyName = null, isRoot = false) {
    //            if (data === null) {
    //                const span = document.createElement('span');
    //                span.className = 'null';
    //                span.textContent = 'null';
    //                return span;
    //            }

    //            const type = typeof data;
    //            if (type !== 'object') {
    //                const span = document.createElement('span');
    //                span.className = type;
    //                span.textContent = type === 'string' ? '""' + data + '""' : data;
    //                return span;
    //            }

    //            const details = document.createElement('details');
    //            if (isRoot || keyName === 'Subworkflows' || keyName === 'State') {
    //                details.open = true; // Auto-open high level structures
    //            }

    //            const summary = document.createElement('summary');
    //            const summaryText = document.createElement('span');
    //            summaryText.className = 'key';

    //            const isArray = Array.isArray(data);

    //            // MAGIC TRICK: Check if this object represents a workflow container
    //            const isWorkflowObj = data && !isArray && data.hasOwnProperty('WorkflowDirectory');

    //            if (isWorkflowObj && keyName !== null && !isNaN(keyName)) {
    //                // 1. We are iterating through a Subworkflows array.
    //                const index1Based = parseInt(keyName) + 1;

    //                // 2. Extract the name safely
    //                const wfName = (data.State && typeof data.State === 'object' && data.State.WorkflowName) 
    //                    ? data.State.WorkflowName 
    //                    : data.WorkflowDirectory;

    //                // 3. Check for failures (including parsing failures from C#)
    //                let hasError = false;
    //                if (typeof data.State === 'string' && data.State.startsWith('[Failed')) {
    //                    hasError = true;
    //                } else if (data.State && typeof data.State === 'object' && data.State.Failure !== null) {
    //                    hasError = true;
    //                }

    //                // Apply our new styling
    //                summaryText.className = hasError ? 'wf-error' : 'wf-success';
    //                summaryText.textContent = index1Based;

    //                const nameSpan = document.createElement('span');
    //                nameSpan.className = 'wf-name';
    //                nameSpan.textContent = ' - ' + wfName;

    //                summary.appendChild(summaryText);
    //                summary.appendChild(nameSpan);
    //            } 
    //            else 
    //            {
    //                // Default fallback formatting
    //                summaryText.textContent = keyName ? keyName + (isArray ? ' [' + data.length + ']' : '') : (isArray ? 'Array' : 'Object');
    //                summary.appendChild(summaryText);
    //            }

    //            details.appendChild(summary);

    //            const ul = document.createElement('ul');
    //            for (const key in data) {
    //                const li = document.createElement('li');

    //                if (typeof data[key] === 'object' && data[key] !== null) {
    //                    li.appendChild(renderNode(data[key], key));
    //                } else {
    //                    const keySpan = document.createElement('span');
    //                    keySpan.className = 'key';
    //                    keySpan.textContent = key + ': ';
    //                    li.appendChild(keySpan);
    //                    li.appendChild(renderNode(data[key]));
    //                }
    //                ul.appendChild(li);
    //            }

    //            details.appendChild(ul);
    //            return details;
    //        }

    //        function toggleAll(open) {
    //            document.querySelectorAll('details').forEach(d => d.open = open);
    //        }

    //        document.getElementById('app').appendChild(renderNode(telemetryData, 'Execution Lifecycle', true));
    //    </script>
    //</body>
    //</html>";
    //    }





    private static string GetHtmlTemplate()
    {
        // Using standard string literal with single quotes for HTML/JS to avoid heavy escaping in C#
        return @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8'>
    <title>Workflow Telemetry</title>
    <style>
        body { font-family: Consolas, 'Courier New', monospace; background: #1e1e1e; color: #d4d4d4; padding: 20px; font-size: 14px; margin: 0; }
        details { margin-left: 10px; margin-bottom: 2px; }
        summary { cursor: pointer; padding: 4px; border-radius: 3px; font-weight: bold; margin-bottom: 2px; }
        summary:hover { background: #2a2d2e; }
        
        /* Data type colors */
        .key { color: #9cdcfe; margin-right: 5px; }
        .string { color: #ce9178; }
        .number { color: #b5cea8; }
        .boolean { color: #569cd6; }
        .null { color: #c586c0; font-style: italic; }
        
        ul { list-style-type: none; padding-left: 20px; margin: 0; border-left: 1px solid #404040; }
        li { margin: 3px 0; }
        .root-container { background: #252526; padding: 20px; border-radius: 5px; border: 1px solid #333; min-height: 95vh; }
        
        /* Workflow Container Styling */
        .wf-summary-success { 
            background-color: rgba(137, 209, 133, 0.15); 
            border-left: 4px solid #89d185; 
            color: #ffffff;
        }
        .wf-summary-error { 
            background-color: rgba(241, 76, 76, 0.2); 
            border-left: 4px solid #f14c4c; 
            color: #ffffff;
        }
        .wf-summary-success:hover { background-color: rgba(137, 209, 133, 0.25); }
        .wf-summary-error:hover { background-color: rgba(241, 76, 76, 0.3); }
    </style>
</head>
<body>
    <div id='app' class='root-container'></div>

    <script>
        const telemetryData = {{TELEMETRY_DATA}};

        // Dynamically set the page title
        if (telemetryData && telemetryData.State) {
            const docTitle = telemetryData.State.DocumentTitle || 'UnknownDoc';
            const wfName = telemetryData.State.WorkflowName || 'UnknownWorkflow';
            document.title = docTitle + ' - ' + wfName;
        }

        function renderNode(data, keyName = null, isRoot = false) {
            if (data === null) {
                const span = document.createElement('span');
                span.className = 'null';
                span.textContent = 'null';
                return span;
            }

            const type = typeof data;
            if (type !== 'object') {
                const span = document.createElement('span');
                span.className = type;
                span.textContent = type === 'string' ? '""' + data + '""' : data;
                return span;
            }

            const details = document.createElement('details');
            if (isRoot || keyName === 'Subworkflows' || keyName === 'State') {
                details.open = true;
            }

            const summary = document.createElement('summary');
            const isArray = Array.isArray(data);
            
            // Check if this object represents a workflow container (Root or Subworkflow)
            const isWorkflowObj = data && !isArray && data.hasOwnProperty('WorkflowDirectory');

            if (isWorkflowObj) {
                // Determine error state
                let hasError = false;
                if (typeof data.State === 'string' && data.State.startsWith('[Failed')) {
                    hasError = true;
                } else if (data.State && typeof data.State === 'object' && data.State.Failure !== null) {
                    hasError = true;
                }
                
                // Apply the full-bar background styling
                summary.className = hasError ? 'wf-summary-error' : 'wf-summary-success';
                
                // Extract the workflow name safely
                const wfName = (data.State && typeof data.State === 'object' && data.State.WorkflowName) 
                    ? data.State.WorkflowName 
                    : data.WorkflowDirectory;
                
                const summaryText = document.createElement('span');
                
                if (!isRoot && keyName !== null && !isNaN(keyName)) {
                    // It's a subworkflow: prepend the 1-based index
                    const index1Based = parseInt(keyName) + 1;
                    summaryText.textContent = index1Based + ' - ' + wfName;
                } else {
                    // It's the root workflow: just show the name
                    summaryText.textContent = wfName;
                }
                
                summary.appendChild(summaryText);
            } 
            else 
            {
                // Standard object/array summary styling
                const summaryText = document.createElement('span');
                summaryText.className = 'key';
                summaryText.textContent = keyName ? keyName + (isArray ? ' [' + data.length + ']' : '') : (isArray ? 'Array' : 'Object');
                summary.appendChild(summaryText);
            }
            
            details.appendChild(summary);

            const ul = document.createElement('ul');
            for (const key in data) {
                const li = document.createElement('li');
                
                if (typeof data[key] === 'object' && data[key] !== null) {
                    li.appendChild(renderNode(data[key], key));
                } else {
                    const keySpan = document.createElement('span');
                    keySpan.className = 'key';
                    keySpan.textContent = key + ': ';
                    li.appendChild(keySpan);
                    li.appendChild(renderNode(data[key]));
                }
                ul.appendChild(li);
            }
            
            details.appendChild(ul);
            return details;
        }

        // Render the root node without passing a keyName, but flagging it as root
        document.getElementById('app').appendChild(renderNode(telemetryData, null, true));
    </script>
</body>
</html>";
    }




}