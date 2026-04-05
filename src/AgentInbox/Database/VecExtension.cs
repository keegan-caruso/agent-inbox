using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

public static class VecExtension
{
    private static string GetExtensionPath()
    {
        var extensionName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vec0.dll" :
                           RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "vec0.dylib" : "vec0.so";

        var rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" :
                  RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                      (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64") :
                      (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64");

        // First try to find it relative to the application directory
        var appDir = AppContext.BaseDirectory;
        var relativePath = Path.Combine(appDir, "runtimes", rid, "native", extensionName);
        if (File.Exists(relativePath))
            return relativePath;

        // Try current directory
        var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), extensionName);
        if (File.Exists(currentDirPath))
            return currentDirPath;

        // Just return the name and hope it's in the library path
        return extensionName;
    }

    public static void Load(SqliteConnection connection)
    {
        try
        {
            connection.EnableExtensions(true);
            var extensionPath = GetExtensionPath();
            connection.LoadExtension(extensionPath);
        }
        catch (SqliteException)
        {
            // If we can't load the extension, silently fail
            // This allows the app to work without vec extension if needed
        }
    }
}
