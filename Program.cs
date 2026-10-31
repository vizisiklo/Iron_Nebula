using System;
using System.IO;
using IronNebula;

try
{
    if(args.Contains("--coop-bot"))
    {
        Environment.ExitCode=CoopSmokeClient.Run(args).GetAwaiter().GetResult();return;
    }
    using var game = new IronNebulaGame();
    game.Run();
}
catch(Exception ex)
{
    try
    {
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IronNebula");
        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir,"crash.log"),$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
    }
    catch { /* Logging must never hide the original exception. */ }
    throw;
}
