//using System.IO;

//namespace Eobim.RevitApi.Framework;


//public class TelemetryFileSystemManager 
//{
//    private readonly string _mainLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eobim.Logs");
//    protected string _revitDocumentLogDirectory { get; set; }
//    public string InstanceLogDirectory { get; set; }
//    protected string _actionName { get; set; }

//    public TelemetryFileSystemManager(string revitDocumentTitle, string actionName) 
//    {
//        _revitDocumentLogDirectory = Path.Combine(_mainLogDirectory, revitDocumentTitle);
//        _actionName = actionName;
//    }

//    protected static void SafelyDeleteAndRecreateDirectory(string path)
//    {
//        try
//        {
//            if (Directory.Exists(path))
//            {
//                Directory.Delete(path, true);
//            }

//            Directory.CreateDirectory(path);
//        }
//        catch (Exception ex)
//        {
//            System.Windows.MessageBox.Show
//            (
//                $"Failed to delete existing log directory: {ex.Message}",
//                "Error",
//                System.Windows.MessageBoxButton.OK,
//                System.Windows.MessageBoxImage.Error
//            );

//            throw;
//        }
//    }

//    public void WriteTelemetryFile(string? content, int workflowNumber)
//    {
//        //var finalLogfilePath = Path.Combine(InstanceLogDirectory, $"{workflowNumber}_{_actionName}.json");
//        var finalLogfilePath = Path.Combine(InstanceLogDirectory, $"{_actionName}.json");

//        File.WriteAllText(finalLogfilePath, content ?? "Empty");
//    }
//}

//public class ExternalCommandTelemetryFileSystemManager: TelemetryFileSystemManager
//{
//    public ExternalCommandTelemetryFileSystemManager(string revitDocumentTitle, string actionName)
//        : 
//    base(revitDocumentTitle, actionName)
//    {
//        InstanceLogDirectory = Path.Combine(_revitDocumentLogDirectory, actionName);

//        SafelyDeleteAndRecreateDirectory(InstanceLogDirectory);
//    }
//}

//public class SubworkflowTelemetryFileSystemManager: TelemetryFileSystemManager
//{
//    public SubworkflowTelemetryFileSystemManager(string revitDocumentTitle, string parentActionLogDirectoryPath, string actionName, int actionNumber, int? iterativeActionCounter = null) 
//        : 
//    base(revitDocumentTitle, actionName)
//    {
//        if (iterativeActionCounter is null)
//        {
//            InstanceLogDirectory = Path.Combine(parentActionLogDirectoryPath, $"{actionNumber}_{actionName}");
//        }
//        else
//        {
//            InstanceLogDirectory = Path.Combine(parentActionLogDirectoryPath, $"{iterativeActionCounter}_{actionNumber}_{actionName}");
//        }

//        SafelyDeleteAndRecreateDirectory(InstanceLogDirectory);
//    }
//}