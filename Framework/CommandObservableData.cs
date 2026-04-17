namespace Eobim.RevitApi.Framework;

public class WorkflowObservableData
{
    public string DocumentTitle { get; set; }
    public string WorkflowName { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public List<string> ActionsNames { get; set; } = [];
    public List<WorkflowObservableAction> Actions { get; set; } = [];
    public WorkflowObservableDataFailure Failure { get; set; }
    public object Data { get; set; }
}

public class WorkflowObservableAction
{
    public int ActionNumber { get; set; }
    public string Name { get; set; }
    public bool Succeeded => Failure is null;
    public List<string> Telemetry { get; set; }
    public WorkflowObservableDataFailure Failure { get; set; }
}

public class WorkflowObservableDataFailure
{
    public int ActionNumber { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }
}
