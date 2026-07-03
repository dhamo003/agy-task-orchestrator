using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Runner.Markdown.Models;
using TaskStatus = Runner.Markdown.Models.TaskStatus;

namespace Runner.Markdown.Parser;

public class MarkdownTaskParser : ITaskParser
{
    private static readonly Regex TaskRegex = new(
        @"^(?<indent>\s*)-\s*\[(?<status>[\sXx/!\-])\]\s+(?<text>.*)$", 
        RegexOptions.Compiled);

    public async Task<IReadOnlyList<TaskPhase>> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var phases = new List<TaskPhase>();
        if (!File.Exists(filePath))
        {
            return phases;
        }

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        
        string? currentPhaseName = null;
        int currentPhaseLine = -1;
        var currentTasks = new List<TaskItem>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = TaskRegex.Match(line);
            if (match.Success)
            {
                var indentStr = match.Groups["indent"].Value;
                int indent = indentStr.Length;
                
                var statusChar = match.Groups["status"].Value.FirstOrDefault();
                var status = ParseStatus(statusChar);
                var rawText = line;
                var displayText = match.Groups["text"].Value;

                if (indent == 0 && displayText.Contains("**Phase", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentPhaseName != null)
                    {
                        phases.Add(CreatePhase(currentPhaseName, currentPhaseLine, currentTasks));
                    }

                    var phaseMatch = Regex.Match(displayText, @"\*\*(Phase.*?)\*\*");
                    currentPhaseName = phaseMatch.Success ? phaseMatch.Groups[1].Value : displayText.Replace("**", "").Trim();
                    currentPhaseLine = i + 1;
                    currentTasks = new List<TaskItem>();
                }
                else
                {
                    if (currentPhaseName == null)
                    {
                        currentPhaseName = "Default";
                        currentPhaseLine = i + 1;
                    }

                    var taskItem = new TaskItem(
                        LineNumber: i + 1,
                        RawText: rawText,
                        DisplayText: displayText,
                        Status: status,
                        Phase: currentPhaseName,
                        IndentLevel: indent
                    );
                    currentTasks.Add(taskItem);
                }
            }
        }

        if (currentPhaseName != null)
        {
            phases.Add(CreatePhase(currentPhaseName, currentPhaseLine, currentTasks));
        }

        return phases;
    }

    private static TaskPhase CreatePhase(string name, int line, List<TaskItem> tasks)
    {
        double completion = 0;
        if (tasks.Count > 0)
        {
            int completed = tasks.Count(t => t.Status == TaskStatus.Completed || t.Status == TaskStatus.Skipped);
            completion = (double)completed / tasks.Count * 100.0;
        }
        return new TaskPhase(name, line, tasks, completion);
    }

    public TaskItem? GetNextTask(IReadOnlyList<TaskPhase> phases)
    {
        foreach (var phase in phases)
        {
            foreach (var task in phase.Tasks)
            {
                if (task.Status == TaskStatus.NotStarted || task.Status == TaskStatus.InProgress || task.Status == TaskStatus.Failed)
                {
                    return task;
                }
            }
        }
        return null;
    }

    private static TaskStatus ParseStatus(char c) => c switch
    {
        ' ' => TaskStatus.NotStarted,
        'x' or 'X' => TaskStatus.Completed,
        '/' => TaskStatus.InProgress,
        '!' => TaskStatus.Failed,
        '-' => TaskStatus.Skipped,
        _ => TaskStatus.NotStarted
    };
}
