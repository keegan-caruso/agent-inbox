using System.Reflection;
using Xunit.Runner.InProc.SystemConsole;

// In a native AOT single-file binary Assembly.Location is always empty. Passing the entry
// assembly explicitly to the ConsoleRunner constructor bypasses the file-path lookup that
// would otherwise fail with "assembly not found".
using var runner = new ConsoleRunner(args, Assembly.GetEntryAssembly()!);
return await runner.EntryPoint(Console.In, Console.Out);
