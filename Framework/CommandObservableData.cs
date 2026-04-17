namespace Eobim.RevitApi.Framework;

public class CommandObservableData
{
    public string DocumentTitle { get; set; }
    public string CommandName { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public List<string> ActionsNames { get; set; } = [];
    public List<CommandObservableAction> Actions { get; set; } = [];
    public CommandObservableDataFailure Failure { get; set; }
    public object Data { get; set; }
}

public class CommandObservableAction
{
    public int ActionNumber { get; set; }
    public string Name { get; set; }
    public bool Succeeded => Failure is null;
    public List<string> Telemetry { get; set; }
    public CommandObservableDataFailure Failure { get; set; }
}

public class CommandObservableDataFailure
{
    public int ActionNumber { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }
}
