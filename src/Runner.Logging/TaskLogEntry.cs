using System;

namespace Runner.Logging;

public record TaskLogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    TaskLogScope Scope,
    string Message,
    Exception? Exception = null);
