# Antigravity Task Runner

Antigravity Task Runner is an AI-powered task orchestration tool that automates software development tasks defined in a markdown checklist (`tasks.md`). It reads tasks, spawns a terminal session, communicates with an AI model via CLI (e.g. `aider` or `repocoder`), verifies workspace changes, and updates the markdown file with progress.

## Features

- **Markdown-driven Orchestration:** Parses `tasks.md` to find pending tasks (`[ ]`), marking them as in-progress (`[/]`), completed (`[x]`), or failed (`[!]`).
- **Parallel and Sequential Execution:** Supports executing tasks one by one or in parallel (configurable).
- **Workspace Verification:** Snapshots the workspace before and after execution to detect changes.
- **Resilient Execution:** Features exponential backoff, retry mechanisms, and graceful shutdown (Ctrl+C).
- **Prompt Templating:** Uses a customizable prompt template with variables (`{task}`, `{workspaceContext}`, etc.).
- **Rich Console Interface:** Uses `Spectre.Console` for live progress tracking and summary reporting.

## Project Structure

- `Runner.Configuration`: Configuration models and parsing.
- `Runner.Logging`: Structured and console logging logic.
- `Runner.Markdown`: Parses and updates the `tasks.md` checklist.
- `Runner.Terminal`: Manages terminal processes, captures stdout/stderr, and analyzes workspace changes.
- `Runner.Core`: Orchestrates the task lifecycle (Pipeline, Retry, Progress).
- `Runner.Console`: The main executable featuring the CLI and rich reporting.

## Configuration

Configuration is managed via `appsettings.json` and CLI arguments.

### `appsettings.json`

```json
{
  "Runner": {
    "Parallel": {
      "Mode": "Sequential",
      "MaxWorkers": 4
    },
    "Retry": {
      "MaxRetries": 3
    },
    "Terminal": {
      "ShellPath": "powershell.exe",
      "Arguments": "-NoProfile -Command -"
    }
  }
}
```

## Usage

Build the project:
```sh
dotnet build
```

Run the application:
```sh
cd src/Runner.Console
dotnet run -- -t ../../tasks.md
```

### CLI Arguments

- `-t, --tasks <path>`: Path to the markdown tasks file. (Default: `tasks.md`)
- `--model <name>`: The AI model to use.
- `--dry-run`: Parse tasks and show progress without actually running terminal commands.
- `--parallel <count>`: Number of parallel workers to use.
- `--verbose`: Enable verbose logging output.

## Extensibility

The `IPromptTemplateEngine` in `Runner.Core` can be customized to inject more context (e.g., specific file contents, lint errors) into the prompt. The `ICompletionDetector` uses regular expressions to determine if a terminal task succeeded or failed.
