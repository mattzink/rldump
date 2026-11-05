namespace Rldump.Tests;

/// <summary>
/// Redirects the process-wide Console.Out/Console.Error to strings.
/// Command tests use this; test collections run serially (xunit.runner.json)
/// so the redirection is safe.
/// </summary>
public sealed class ConsoleCapture : IDisposable
{
    public StringWriter StdOut { get; } = new();
    public StringWriter StdErr { get; } = new();

    private readonly TextWriter _oldOut;
    private readonly TextWriter _oldErr;

    public ConsoleCapture()
    {
        _oldOut = Console.Out;
        _oldErr = Console.Error;
        Console.SetOut(StdOut);
        Console.SetError(StdErr);
    }

    public void Dispose()
    {
        Console.SetOut(_oldOut);
        Console.SetError(_oldErr);
    }
}
