using System;
using System.Collections.Generic;

namespace Runner.Logging;

public class AggregateTaskLogger : ITaskLogger
{
    private readonly IEnumerable<ITaskLogger> _loggers;

    public AggregateTaskLogger(IEnumerable<ITaskLogger> loggers)
    {
        _loggers = loggers;
    }

    public void Log(LogLevel level, TaskLogScope scope, string message, Exception? exception = null)
    {
        foreach (var logger in _loggers)
        {
            try
            {
                logger.Log(level, scope, message, exception);
            }
            catch
            {
                // Suppress exceptions from individual loggers
            }
        }
    }
}
