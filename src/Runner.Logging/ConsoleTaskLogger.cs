using System;

namespace Runner.Logging;

public class ConsoleTaskLogger : ITaskLogger
{
    private readonly LogLevel _minimumLevel;

    public ConsoleTaskLogger(LogLevel minimumLevel = LogLevel.Info)
    {
        _minimumLevel = minimumLevel;
    }

    public void Log(LogLevel level, TaskLogScope scope, string message, Exception? exception = null)
    {
        if (level < _minimumLevel)
            return;

        var color = level switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Fatal => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;

        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        var levelStr = level.ToString().ToUpperInvariant().PadRight(7);
        
        Console.WriteLine($"[{timestamp}] {levelStr} [{scope.TaskId}] {message}");
        
        if (exception != null)
        {
            Console.WriteLine(exception.ToString());
        }

        Console.ForegroundColor = originalColor;
    }
}
