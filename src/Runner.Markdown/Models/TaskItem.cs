namespace Runner.Markdown.Models;

public record TaskItem(
    int LineNumber,
    string RawText,
    string DisplayText,
    TaskStatus Status,
    string? Phase,
    int IndentLevel
);
