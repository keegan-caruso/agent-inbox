using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace AgentInbox.Database;

internal static class VecExtension
{
    public static bool TryLoad(SqliteConnection connection)
    {
        try
        {
            var libPath = FindLibraryPath();
            if (libPath == null) return false;
            connection.LoadExtension(libPath, "sqlite3_vec_init");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindLibraryPath()
    {
        var libName = GetLibraryFileName();
        var baseDir = AppContext.BaseDirectory;

        // For published builds (NativeAOT and self-contained), native libs land at output root.
        var path = Path.Combine(baseDir, libName);
        if (File.Exists(path)) return path;

        // For framework-dependent / development builds, native libs are in runtimes/{rid}/native/.
        var rid = GetRid();
        path = Path.Combine(baseDir, "runtimes", rid, "native", libName);
        if (File.Exists(path)) return path;

        return null;
    }

    private static string GetLibraryFileName() =>
        OperatingSystem.IsWindows() ? "vec0.dll" :
        OperatingSystem.IsMacOS() ? "vec0.dylib" : "vec0.so";

    private static string GetRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (OperatingSystem.IsWindows())
            return arch == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (OperatingSystem.IsMacOS())
            return arch == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return arch == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }
}
