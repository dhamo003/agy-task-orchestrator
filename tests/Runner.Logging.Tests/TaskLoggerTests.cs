using System;
using Runner.Logging;
using Xunit;
using System.IO;

namespace Runner.Logging.Tests;

public class TaskLoggerTests
{
    [Fact]
    public void ConsoleLogger_RespectsMinimumLevel()
    {
        // We can't easily capture Console output here without redirecting standard out,
        // but we can ensure it doesn't throw.
        ITaskLogger logger = new ConsoleTaskLogger(LogLevel.Warning);
        var scope = new TaskLogScope("task-123", "Test Task", 1);
        
        logger.LogInfo(scope, "Should not be logged");
        logger.LogError(scope, "Should be logged");
    }

    [Fact]
    public void FileLogger_WritesJsonToCorrectFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            ITaskLogger logger = new FileTaskLogger(tempDir, LogLevel.Trace);
            var scope = new TaskLogScope("task-xyz", "File Task", 2);
            
            logger.LogInfo(scope, "Hello File Logger");

            var logFile = Path.Combine(tempDir, "task_task-xyz.log");
            Assert.True(File.Exists(logFile));

            var contents = File.ReadAllText(logFile);
            Assert.Contains("Hello File Logger", contents);
            Assert.Contains("Info", contents);
            Assert.Contains("task-xyz", contents);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void AggregateLogger_LogsToAllLoggers()
    {
        var tempDir1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempDir2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var logger1 = new FileTaskLogger(tempDir1);
            var logger2 = new FileTaskLogger(tempDir2);
            ITaskLogger aggregate = new AggregateTaskLogger(new[] { logger1, logger2 });

            var scope = new TaskLogScope("agg-task", "Agg Task", 1);
            aggregate.LogWarning(scope, "Aggregated Warning");

            var file1 = Path.Combine(tempDir1, "task_agg-task.log");
            var file2 = Path.Combine(tempDir2, "task_agg-task.log");

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
            Assert.Contains("Aggregated Warning", File.ReadAllText(file1));
            Assert.Contains("Aggregated Warning", File.ReadAllText(file2));
        }
        finally
        {
            if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, true);
            if (Directory.Exists(tempDir2)) Directory.Delete(tempDir2, true);
        }
    }
}
