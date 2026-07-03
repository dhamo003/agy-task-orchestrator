using System;

namespace Runner.Logging;

public interface ITaskLogger
{
    void Log(LogLevel level, TaskLogScope scope, string message, Exception? exception = null);
    
    // Convenience methods
    void LogTrace(TaskLogScope scope, string message) => Log(LogLevel.Trace, scope, message);
    void LogDebug(TaskLogScope scope, string message) => Log(LogLevel.Debug, scope, message);
    void LogInfo(TaskLogScope scope, string message) => Log(LogLevel.Info, scope, message);
    void LogWarning(TaskLogScope scope, string message, Exception? exception = null) => Log(LogLevel.Warning, scope, message, exception);
    void LogError(TaskLogScope scope, string message, Exception? exception = null) => Log(LogLevel.Error, scope, message, exception);
    void LogFatal(TaskLogScope scope, string message, Exception? exception = null) => Log(LogLevel.Fatal, scope, message, exception);
}
