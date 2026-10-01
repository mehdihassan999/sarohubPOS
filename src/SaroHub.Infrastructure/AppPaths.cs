// src/SaroHub.Infrastructure/AppPaths.cs
namespace SaroHub.Infrastructure;

public static class AppPaths
{
    public static string Root { get; private set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaroHub POS");

    public static void OverrideRoot(string root) => Root = root;

    public static string DataFolder => Ensure(Path.Combine(Root, "Data"));
    public static string BackupFolder => Ensure(Path.Combine(Root, "Backups"));
    public static string LogFolder => Ensure(Path.Combine(Root, "Logs"));
    public static string ReceiptFolder => Ensure(Path.Combine(Root, "Receipts"));
    public static string DatabaseFile => Path.Combine(DataFolder, "sarohub.db");

    public static string ConnectionString =>
        $"Data Source={DatabaseFile};Foreign Keys=True;Pooling=False";

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}