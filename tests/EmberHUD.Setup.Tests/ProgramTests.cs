namespace EmberHUD.Setup.Tests;

public class ProgramTests
{
    [Fact]
    public void OpenLogWritesTheLogFile()
    {
        using var temp = new TempDir();
        using (var log = Program.OpenLog(temp.Path)) log.WriteLine("hello");
        Assert.Contains("hello", File.ReadAllText(Path.Combine(temp.Path, "Logs", "setup.log")));
    }

    [Fact]
    public void OpenLogFallsBackToNullWhenTheFolderCannotBeWritten()
    {
        using var temp = new TempDir();
        temp.File("Logs", "not a folder");
        Assert.Same(TextWriter.Null, Program.OpenLog(temp.Path));
    }
}
