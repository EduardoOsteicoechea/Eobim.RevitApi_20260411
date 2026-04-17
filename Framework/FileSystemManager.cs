using System.IO;

namespace Eobim.RevitApi.Framework;

public class FileSystemManager
{
    private readonly string _mainLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eobim.Logs");
    private readonly string _instanceLogDirectory;
    private string _filePath;

    public FileSystemManager(string revitDocumentTitle, string commandName)
    {
        _instanceLogDirectory = Path.Combine(_mainLogDirectory, revitDocumentTitle);
        _filePath = Path.Combine(_instanceLogDirectory, $"{commandName}.json");

        ValidateLogDirectory();
    }

    public FileSystemManager(string revitDocumentTitle, string commandName, string multistepActionName)
    {
        _instanceLogDirectory = Path.Combine(_mainLogDirectory, revitDocumentTitle);
        _filePath = Path.Combine(_instanceLogDirectory, multistepActionName, $"{commandName}.json");

        ValidateLogDirectory();
    }

    private void ValidateLogDirectory()
    {
        var targetDirectory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }
    }

    public void WriteTelemetryFile(string? content)
    {
        if (!string.IsNullOrEmpty(content)) File.WriteAllText(_filePath, content);
    }
}