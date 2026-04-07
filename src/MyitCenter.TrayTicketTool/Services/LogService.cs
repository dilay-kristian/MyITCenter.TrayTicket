using System.IO;

namespace MyitCenter.TrayTicketTool.Services;

public static class LogService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyitCenter", "TrayTicketTool");

    private static readonly string LogFile = Path.Combine(LogDir, "app.log");
    private static readonly object Lock = new();

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            lock (Lock)
            {
                // Log-Datei auf max 1 MB begrenzen
                if (File.Exists(LogFile))
                {
                    var info = new FileInfo(LogFile);
                    if (info.Length > 1_048_576)
                    {
                        var backup = LogFile + ".old";
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(LogFile, backup);
                    }
                }

                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging darf nie die App crashen
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex.Message}\n{ex.StackTrace}");
}
