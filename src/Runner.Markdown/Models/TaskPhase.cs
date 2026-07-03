using System.Collections.Generic;

namespace Runner.Markdown.Models;

public record TaskPhase(
    string Name,
    int LineNumber,
    IReadOnlyList<TaskItem> Tasks,
    double CompletionPercentage
);
