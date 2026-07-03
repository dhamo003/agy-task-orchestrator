using System;

namespace Runner.Markdown.Models;

public record RunnerState(
    int? LastTaskLine,
    DateTimeOffset Timestamp,
    int Attempt
);
