using System.IO;

namespace Eobim.RevitApi.Framework;

public class FileSystemManager
{
    private readonly string _mainLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eobim.Logs");
    private readonly string _documentLogDirectory;
    private readonly string _workflowLogDirectory;
    private string _commandName { get; set; }
    private string _multistepActionName { get; set; } = "";
    private string _logFilePath;

    public FileSystemManager(string revitDocumentTitle, string commandName)
    {
        _documentLogDirectory = Path.Combine(_mainLogDirectory, revitDocumentTitle);
        _workflowLogDirectory = Path.Combine(_documentLogDirectory, commandName);
        _commandName = commandName;
        _logFilePath = Path.Combine(_workflowLogDirectory, $"{_commandName}.json");

        ValidateLogDirectory();
    }

    public FileSystemManager(string revitDocumentTitle, string commandName, string multistepActionName)
    {
        _documentLogDirectory = Path.Combine(_mainLogDirectory, revitDocumentTitle);
        _workflowLogDirectory = Path.Combine(_documentLogDirectory, commandName);
        _commandName = commandName;
        _multistepActionName = multistepActionName;
        _logFilePath = Path.Combine(_workflowLogDirectory, $"{commandName}_{multistepActionName}.json");

        ValidateLogDirectory();
    }

    private void ValidateLogDirectory()
    {
        var targetDirectory = Path.GetDirectoryName(_logFilePath);

        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }
    }

    public void WriteTelemetryFile(string? content, int workflowNumber)
    {
        var finalLogfilePath = _logFilePath;

        if (_multistepActionName.Equals(""))
        {
            finalLogfilePath = Path.Combine(_workflowLogDirectory, $"{workflowNumber}_{_commandName}.json");
        }
        else
        {
            finalLogfilePath = Path.Combine(_workflowLogDirectory, $"{workflowNumber}_{_commandName}_{_multistepActionName}.json");
        }

        File.WriteAllText(finalLogfilePath, content ?? "Empty");
    }
}