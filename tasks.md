# AntigravityTaskRunner — Task Checklist

**Status Key:**
- `[x]` — Completed (code exists, compiles)
- `[/]` — In Progress
- `[ ]` — Not Started
- `[!]` — Blocked / Failed

---

- [x] **Phase A: Solution & Project Scaffolding**
  - [x] **Task A.01:** Create directory structure (`src/`, `tests/`)
  - [x] **Task A.02:** Create `Directory.Build.props` (.NET 8, C# 12, nullable, warnings-as-errors)
  - [x] **Task A.03:** Create `AntigravityTaskRunner.sln` solution file
  - [x] **Task A.04:** Create `src/Runner.Configuration/Runner.Configuration.csproj`
  - [x] **Task A.05:** Create `src/Runner.Logging/Runner.Logging.csproj`
  - [x] **Task A.06:** Create `src/Runner.Markdown/Runner.Markdown.csproj`
  - [x] **Task A.07:** Create `src/Runner.Terminal/Runner.Terminal.csproj`
  - [x] **Task A.08:** Create `src/Runner.Core/Runner.Core.csproj`
  - [x] **Task A.09:** Create `src/Runner.Console/Runner.Console.csproj` (executable)
  - [x] **Task A.10:** Create `tests/Runner.Markdown.Tests/Runner.Markdown.Tests.csproj`
  - [x] **Task A.11:** Create `tests/Runner.Core.Tests/Runner.Core.Tests.csproj`
  - [x] **Task A.12:** Verify entire solution builds clean with `dotnet build`

- [x] **Phase B: Configuration**
  - [x] **Task B.01:** Define `RunnerOptions` record (workspace, model, dry-run, verbose)
  - [x] **Task B.02:** Define `RetryOptions` record (max retries, backoff base, backoff max)
  - [x] **Task B.03:** Define `TimeoutOptions` record (task timeout, session timeout, model switch timeout)
  - [x] **Task B.04:** Define `ParallelOptions` record (max workers, execution mode enum)
  - [x] **Task B.05:** Define `ModelOptions` record (target model, fallback models, auto-switch enabled)
  - [x] **Task B.06:** Define `WorkspaceOptions` record (workspace path, solution file, detect strategy)
  - [x] **Task B.07:** Define `PromptTemplateOptions` record (template string, variables, prefix/suffix)
  - [x] **Task B.08:** Define `CompletionOptions` record (success markers, failure markers, timeout markers)
  - [x] **Task B.09:** Create default `appsettings.json` with all configuration sections
  - [x] **Task B.10:** Implement `IValidateOptions<RunnerOptions>` for fail-fast validation
  - [x] **Task B.11:** Create `ConfigurationServiceExtensions` DI registration methods
  - [x] **Task B.12:** Write configuration validation unit tests

- [x] **Phase C: Logging**
  - [x] **Task C.01:** Define `ITaskLogger` interface (scoped per task)
  - [x] **Task C.02:** Define `TaskLogEntry` record (timestamp, level, task ID, message, exception)
  - [x] **Task C.03:** Define `LogLevel` enum (Trace, Debug, Info, Warning, Error, Fatal)
  - [x] **Task C.04:** Implement `ConsoleTaskLogger` with color-coded output
  - [x] **Task C.05:** Implement `FileTaskLogger` with per-task log files
  - [x] **Task C.06:** Implement `TaskLogScope` for scoped context (task name, attempt number)
  - [x] **Task C.07:** Implement `JsonLogFormatter` for structured log serialization
  - [x] **Task C.08:** Implement `AggregateTaskLogger` combining multiple loggers
  - [x] **Task C.09:** Create `LoggingServiceExtensions` DI registration methods
  - [x] **Task C.10:** Write logging unit tests

- [x] **Phase D: Markdown Engine**
  - [x] **Task D.01:** Define `TaskStatus` enum (NotStarted, InProgress, Completed, Failed, Skipped)
  - [x] **Task D.02:** Define `TaskItem` record (line number, raw text, display text, status, phase, indent level)
  - [x] **Task D.03:** Define `TaskPhase` record (name, line number, tasks list, completion percentage)
  - [x] **Task D.04:** Define `ITaskParser` interface with `ParseAsync` and `GetNextTask` methods
  - [x] **Task D.05:** Implement checkbox regex patterns (unchecked `[ ]`, checked `[x]`, in-progress `[/]`, failed `[!]`)
  - [x] **Task D.06:** Implement `MarkdownTaskParser` — parse all tasks with status
  - [x] **Task D.07:** Implement `MarkdownTaskParser` — nested/indented task detection
  - [x] **Task D.08:** Implement `MarkdownTaskParser` — phase grouping from heading/bold markers
  - [x] **Task D.09:** Implement `MarkdownTaskParser` — `GetNextTask()` to find first unchecked
  - [x] **Task D.10:** Define `ITaskWriter` interface with `UpdateStatusAsync` method
  - [x] **Task D.11:** Implement `MarkdownTaskWriter` — mark task as completed (`[x]`)
  - [x] **Task D.12:** Implement `MarkdownTaskWriter` — mark task as in-progress (`[/]`)
  - [x] **Task D.13:** Implement `MarkdownTaskWriter` — mark task as failed (`[!] <reason>`)
  - [x] **Task D.14:** Implement `MarkdownTaskWriter` — atomic file locking (read-modify-write)
  - [x] **Task D.15:** Define `RunnerState` record for resume support (last task, timestamp, attempt)
  - [x] **Task D.16:** Define `IStateManager` interface (save/load/clear)
  - [x] **Task D.17:** Implement `JsonStateManager` persisting to `runner-state.json`
  - [x] **Task D.18:** Write parser unit tests — basic checkbox patterns
  - [x] **Task D.19:** Write parser unit tests — nested tasks and phases
  - [x] **Task D.20:** Write writer unit tests — status transitions and file locking

- [x] **Phase E: Terminal Management**
  - [x] **Task E.01:** Define `TerminalOptions` record (shell path, arguments, environment variables)
  - [x] **Task E.02:** Define `ITerminalSession` interface (start, send input, read output, wait, kill)
  - [x] **Task E.03:** Define `TerminalSessionResult` record (exit code, stdout, stderr, duration)
  - [x] **Task E.04:** Implement `ProcessTerminalSession` — spawn cmd.exe/powershell.exe with `Process`
  - [x] **Task E.05:** Implement `ProcessTerminalSession` — async stdout/stderr streaming capture
  - [x] **Task E.06:** Implement `ProcessTerminalSession` — stdin write for sending prompts
  - [x] **Task E.07:** Implement `ProcessTerminalSession` — configurable timeout with `CancellationToken`
  - [x] **Task E.08:** Implement `ProcessTerminalSession` — graceful kill (SIGTERM then SIGKILL)
  - [x] **Task E.09:** Define `IModelDetector` interface (detect current model from output)
  - [x] **Task E.10:** Implement `OutputModelDetector` — regex-based model name extraction from CLI output
  - [x] **Task E.11:** Define `IModelSwitcher` interface (switch to target model)
  - [x] **Task E.12:** Implement `CliModelSwitcher` — send model-switch command via stdin
  - [x] **Task E.13:** Define `ICompletionDetector` interface (detect task completion from output)
  - [x] **Task E.14:** Implement `MarkerCompletionDetector` — configurable success/failure marker matching
  - [x] **Task E.15:** Define `IWorkspaceAnalyzer` interface (snapshot before/after, detect changes)
  - [x] **Task E.16:** Implement `FileChangeWorkspaceAnalyzer` — file timestamp/hash comparison
  - [x] **Task E.17:** Define `WorkspaceSnapshot` record (files, timestamps, hashes)
  - [x] **Task E.18:** Create `TerminalServiceExtensions` DI registration methods
  - [x] **Task E.19:** Write terminal session unit tests (mock process)
  - [x] **Task E.20:** Write completion detector unit tests

- [x] **Phase F: Core Orchestration**
  - [x] **Task F.01:** Define `ITaskOrchestrator` interface (run all, run single, stop)
  - [x] **Task F.02:** Define `TaskExecutionContext` record (task item, attempt, workspace snapshot, cancellation token)
  - [x] **Task F.03:** Define `TaskExecutionResult` record (task item, success, duration, error, retry count)
  - [x] **Task F.04:** Define `ExecutionMode` enum (Sequential, Parallel)
  - [x] **Task F.05:** Implement `TaskPipeline` — orchestrate single task lifecycle
  - [x] **Task F.06:** Implement `TaskPipeline` — Step 1: Take workspace snapshot
  - [x] **Task F.07:** Implement `TaskPipeline` — Step 2: Spawn terminal session
  - [x] **Task F.08:** Implement `TaskPipeline` — Step 3: Wait for CLI ready
  - [x] **Task F.09:** Implement `TaskPipeline` — Step 4: Verify/switch model
  - [x] **Task F.10:** Implement `TaskPipeline` — Step 5: Build prompt from template
  - [x] **Task F.11:** Implement `TaskPipeline` — Step 6: Send prompt via stdin
  - [x] **Task F.12:** Implement `TaskPipeline` — Step 7: Monitor output for completion
  - [x] **Task F.13:** Implement `TaskPipeline` — Step 8: Verify workspace changes
  - [x] **Task F.14:** Implement `TaskPipeline` — Step 9: Mark checkbox and close terminal
  - [x] **Task F.15:** Implement `SequentialOrchestrator` — process tasks one-by-one
  - [x] **Task F.16:** Implement `ParallelOrchestrator` — configurable workers with `SemaphoreSlim`
  - [x] **Task F.17:** Implement `RetryPolicy` — configurable max retries
  - [x] **Task F.18:** Implement `RetryPolicy` — exponential backoff with jitter
  - [x] **Task F.19:** Implement `CancellationManager` — Ctrl+C and graceful shutdown
  - [x] **Task F.20:** Implement `ProgressTracker` — track overall and per-task progress
  - [x] **Task F.21:** Create `CoreServiceExtensions` DI registration methods
  - [x] **Task F.22:** Write sequential orchestrator unit tests
  - [x] **Task F.23:** Write parallel orchestrator unit tests
  - [x] **Task F.24:** Write retry policy unit tests
  - [x] **Task F.25:** Write pipeline step unit tests

- [x] **Phase G: Console Application**
  - [x] **Task G.01:** Create `Program.cs` with `Microsoft.Extensions.Hosting` generic host
  - [x] **Task G.02:** Implement DI composition root — register all services
  - [x] **Task G.03:** Implement CLI argument parsing (tasks file, model, dry-run, parallel count, verbose)
  - [x] **Task G.04:** Implement `OrchestratorHostedService` as `IHostedService`
  - [x] **Task G.05:** Add Spectre.Console — rich progress bars
  - [x] **Task G.06:** Add Spectre.Console — live status table (task name, status, duration)
  - [x] **Task G.07:** Add Spectre.Console — task tree visualization
  - [x] **Task G.08:** Implement dry-run mode — parse and display tasks without executing
  - [x] **Task G.09:** Implement verbose mode — detailed terminal output passthrough
  - [x] **Task G.10:** Implement Ctrl+C graceful shutdown with status save
  - [x] **Task G.11:** Define exit codes (0=success, 1=partial, 2=error, 3=config error)
  - [x] **Task G.12:** Implement `--help` text with usage examples
  - [x] **Task G.13:** Embed `appsettings.json` as content file in output
  - [x] **Task G.14:** Write console integration tests (dry-run against sample tasks.md)
  - [x] **Task G.15:** Write end-to-end test — full pipeline with mock terminal

- [x] **Phase H: Integration & Polish**
  - [x] **Task H.01:** Implement prompt template engine — variable substitution ({task}, {workspace}, {file})
  - [x] **Task H.02:** Implement prompt template — workspace context injection
  - [x] **Task H.03:** Implement prompt template — task-only scope enforcement
  - [x] **Task H.04:** End-to-end resume after interruption test
  - [x] **Task H.05:** End-to-end multi-task sequential test
  - [x] **Task H.06:** Error reporting — aggregate errors into summary table
  - [x] **Task H.07:** Log aggregation — summary report on completion
  - [x] **Task H.08:** Progress summary — final statistics display
  - [x] **Task H.09:** Create `README.md` documentation
  - [x] **Task H.10:** Final solution verification — clean build, all tests pass

---

## Progress Summary

| Phase | Status | Completed | Total | Progress |
|-------|--------|-----------|-------|----------|
| Phase A: Solution Scaffolding | ✅ Completed | 12/12 | 12 | 100% |
| Phase B: Configuration | ✅ Completed | 12/12 | 12 | 100% |
| Phase C: Logging | ✅ Completed | 10/10 | 10 | 100% |
| Phase D: Markdown Engine | ✅ Completed | 20/20 | 20 | 100% |
| Phase E: Terminal Management | ✅ Completed | 20/20 | 20 | 100% |
| Phase F: Core Orchestration | ✅ Completed | 25/25 | 25 | 100% |
| Phase G: Console Application | ✅ Completed | 15/15 | 15 | 100% |
| Phase H: Integration & Polish | ✅ Completed | 10/10 | 10 | 100% |
| **TOTAL** | | **124/124** | **124** | **100%** |

### Next Up
* All tasks completed!
