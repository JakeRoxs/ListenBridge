namespace ListenBridge.Cli.Reports;

internal static class ReportOutputDirectory
{
    public static void EnsureParentDirectoryExists(string outputPath)
    {
        var directoryPath = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}
