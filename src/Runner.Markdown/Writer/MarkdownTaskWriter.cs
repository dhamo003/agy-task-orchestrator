using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace Runner.Markdown.Writer;

public class MarkdownTaskWriter : ITaskWriter
{
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(100);
    private const int MaxLockRetries = 50;
    private static readonly string[] LineSeparators = new[] { "\r\n", "\r", "\n" };

    public async Task UpdateStatusAsync(string filePath, TaskItem task, TaskStatus newStatus, string? reason = null, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < MaxLockRetries; i++)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                using var reader = new StreamReader(fileStream);
                
                var content = await reader.ReadToEndAsync(cancellationToken);
                var lines = content.Split(LineSeparators, StringSplitOptions.None);
                
                if (task.LineNumber > 0 && task.LineNumber <= lines.Length)
                {
                    int index = task.LineNumber - 1;
                    string line = lines[index];
                    string marker = newStatus switch
                    {
                        TaskStatus.NotStarted => "[ ]",
                        TaskStatus.InProgress => "[/]",
                        TaskStatus.Completed => "[x]",
                        TaskStatus.Failed => "[!]",
                        TaskStatus.Skipped => "[-]",
                        _ => "[ ]"
                    };

                    var regex = new Regex(@"\[[\sXx/!]\]");
                    line = regex.Replace(line, marker, 1);

                    if (newStatus == TaskStatus.Failed && !string.IsNullOrWhiteSpace(reason))
                    {
                        line += $" (Reason: {reason})";
                    }

                    lines[index] = line;
                }

                fileStream.SetLength(0);
                fileStream.Position = 0;
                
                using var writer = new StreamWriter(fileStream);
                await writer.WriteAsync(string.Join(Environment.NewLine, lines));
                await writer.FlushAsync(cancellationToken);
                
                return;
            }
            catch (IOException ex) when (IsFileLocked(ex))
            {
                if (i == MaxLockRetries - 1) throw;
                await Task.Delay(LockRetryDelay, cancellationToken);
            }
        }
    }

    private static bool IsFileLocked(IOException exception)
    {
        int errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(exception) & ((1 << 16) - 1);
        return errorCode == 32 || errorCode == 33;
    }
}
