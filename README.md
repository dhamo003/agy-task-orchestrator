# Antigravity Task Runner

**Antigravity Task Runner** is a production-grade orchestrator that automates a checklist of
software-development tasks by driving the [Antigravity CLI](https://antigravitylab.net/) (`agy`)
**strictly one task at a time**. You write your work as a markdown checklist (`tasks.md`); the
runner executes each unchecked item in its **own fresh `agy` session**, verifies that real code
was implemented, validates the build and tests, ticks the box, and only then moves on.

The design goal is unattended reliability: each task gets a clean, isolated session with a focused
prompt, completion is **proven** (not just claimed), failures **halt** the pipeline instead of
being skipped, and every step is checkpointed so a crash or rate limit resumes exactly where it
stopped.

```
tasks.md ──▶ [ pick next unfinished task ]
                     │
                     ▼          finite-state workflow
         ┌────────────────────────────────────────────┐
         │ Pending → Running → Verifying → Completed  │
         │              │  ▲        │                 │
         │           Paused─┘     Failed → HALT       │
         └────────────────────────────────────────────┘
                     │  (session fully terminated, checkpoint cleared)
                     ▼
              mark [x], repeat
```

## Production guarantees

1. **Strict sequential execution.** At most one `agy` session is ever alive. The next task cannot
   start until the current one is fully verified and its process tree is confirmed dead. There is
   no parallel mode and no optimistic scheduling.
2. **Tasks are never skipped.** A task that fails all retries marks itself `[!]` and **halts the
   entire pipeline** with a full failure report (reason, retry history, verification report, build
   output, suggested next action). On the next run the failed task still blocks the pipeline until
   you fix it or pass `--retry-failed`.
3. **Completion must be proven.** A task counts as complete only when ALL checks pass:
   - the `TASK_COMPLETED` marker was printed,
   - workspace files actually changed (created/modified/deleted/renamed — tracked by content hash),
   - at least one change is a **meaningful implementation change** — edits limited to markdown,
     comments, whitespace/formatting, cache files, or `tasks.md` itself are rejected,
   - `dotnet restore` + `dotnet build` succeed and tests pass (configurable commands).

   Test gating is **baseline-aware**: before the first task runs, the runner records which
   tests already fail. A task is only blocked by NEW failing tests (regressions it
   introduced) — pre-existing red tests are reported but don't deadlock the pipeline.
   Set `Build.FailOnlyOnNewTestFailures` to `false` for strict all-tests-must-pass mode.
4. **Intelligent retries.** Every retry prompt includes the previous failure reason, the failed
   verification checks, compiler/test errors, and exactly which files were (and were not) touched,
   plus targeted guidance — so the AI fixes the actual problem instead of repeating itself.
5. **Capacity limits pause, never fail.** Token/rate/quota/context-overflow indicators in the
   agent output put the task into `Paused`: state is persisted, execution waits, and the SAME task
   resumes. Pauses never consume retry attempts.
6. **Crash recovery.** The current task, workflow state, attempt number, prompt, model, modified
   files, attempt history, and the pre-task workspace snapshot are checkpointed (atomically) to
   `.antigravity/` in the workspace. A crashed or interrupted run resumes from the exact
   checkpoint — same task, same attempt, same verification baseline.
7. **Deterministic.** Content-hash change detection, no backoff jitter by default, a validated
   finite-state machine for every transition, and structured logs for Pending, Running, Prompt
   Sent, Response Received, Verification, Build, Tests, Retry, Pause, Resume, Completed, Failed.

## Execution modes

### Interactive mode (default)

Launches an interactive `agy` session inside a pseudo-terminal, sends the task prompt, and detects
completion from the `TASK_COMPLETED` / `TASK_FAILED` markers (authoritative) or the idle-footer
heuristic — followed by full verification. Live status (elapsed time, AI heartbeat, verification,
build, tests) is shown throughout, so the UI never appears frozen.

### One-Shot mode (`--one-shot`)

Launches `agy` in print mode (`agy -p "<prompt>"`) inside a pseudo-TTY and takes completion from
the **real process exit**, combined with the same verification pipeline.

## Requirements

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- The [Antigravity CLI](https://antigravitylab.net/) (`agy`) on your `PATH`
  (or set `Terminal.AgentCommand` to its full path).
- For unattended runs, authenticate `agy` ahead of time or set `GEMINI_API_KEY` /
  `ANTIGRAVITY_API_KEY` in the environment.
- Windows is the primary target (uses `cmd.exe` + winpty and `taskkill` for process-tree cleanup).

## Build & test

```sh
dotnet build AntigravityTaskRunner.slnx
dotnet test  AntigravityTaskRunner.slnx
```

## Run

```sh
dotnet run --project src/Runner.Console -- --workspace "B:\MyProjects\RepoGPT" --tasks "B:\MyProjects\RepoGPT\tasks.md"
```

### CLI options

| Option | Description |
| --- | --- |
| `-w, --workspace <PATH>` | Workspace root the agent operates in. Defaults to the current directory. |
| `-t, --tasks <FILE>` | Path to the markdown tasks file. Defaults to `tasks.md`. |
| `-m, --model <MODEL>` | Target model for the agent. |
| `--one-shot` | Run each task via `agy -p` and detect completion from the real process exit. |
| `--dry-run` | Parse and list pending tasks without executing them. |
| `--retry-failed` | Re-attempt a task previously marked `[!]` instead of halting on it. |
| `--no-build-validation` | Skip the dotnet restore/build/test stage (not recommended unattended). |
| `-v, --verbose` | Verbose terminal output passthrough. |

Exit codes: `0` all tasks completed · `1` finished with failures recorded · `2` pipeline halted on
an unrecoverable failure · `130` cancelled.

## The `tasks.md` format

Tasks are GitHub-style checkboxes. Optional bold **Phase** headers group tasks:

```markdown
- [ ] **Phase 1: Setup**
  - [x] Initialize the project skeleton
  - [ ] Add the configuration layer
```

| Marker | Meaning |
| --- | --- |
| `[ ]` | Not started (pending) |
| `[/]` | In progress (resumed first after an interruption) |
| `[x]` | Completed |
| `[!]` | Failed — **blocks the pipeline** until fixed or `--retry-failed` |
| `[-]` | Skipped (by you, never by the runner) |

The runner picks the first task that is not `[x]`/`[-]`, executes it, and writes back the result.
The agent is instructed **not** to edit `tasks.md` itself — status is managed by the runner, and
changes to `tasks.md` never count as implementation changes.

## Configuration (`appsettings.json`)

CLI options override the corresponding settings. Key sections beyond the basics:

```json
{
  "Runner": {
    "Retry": { "MaxRetries": 3, "BackoffBaseSeconds": 5, "BackoffMaxSeconds": 300, "UseJitter": false },
    "Verification": {
      "RequireMeaningfulDiff": true,
      "RequireCompletionMarker": true
    },
    "Build": {
      "Enabled": true,
      "SkipWhenNoProject": true,
      "RunTests": true,
      "Commands": [
        { "Name": "restore", "Command": "dotnet", "Arguments": [ "restore" ], "TimeoutMinutes": 10 },
        { "Name": "build",   "Command": "dotnet", "Arguments": [ "build", "--no-restore" ], "TimeoutMinutes": 15 },
        { "Name": "test",    "Command": "dotnet", "Arguments": [ "test", "--no-build" ], "TimeoutMinutes": 30 }
      ]
    },
    "Limits": { "PauseSeconds": 300, "MaxPausesPerTask": 12 },
    "Checkpoint": { "Enabled": true, "Directory": ".antigravity" },
    "Workspace": { "DetectStrategy": "Hash" }
  }
}
```

Notable settings:

- **`Verification`** — what counts as a real implementation change (source extensions,
  documentation extensions, ignored path fragments, meaningful-diff requirement).
- **`Build.Commands`** — the validation stages run in the workspace after each attempt. The
  `test` stage only runs when test projects exist. A failed stage keeps the task incomplete and
  feeds the error output into the next retry prompt.
- **`Limits.LimitPatterns`** — output substrings that indicate token/rate/quota/context limits.
- **`Checkpoint.Directory`** — where the checkpoint + workspace snapshot live (inside the
  workspace, excluded from change detection).
- **`Terminal.ExecutionMode`** — `Interactive` (default) or `OneShot`; `Terminal.OneShotArguments`
  templates the one-shot argv (`{prompt}`, `{model}`, `{workspace}`, `{tasksFile}`).

## Project structure

| Project | Responsibility |
| --- | --- |
| `Runner.Configuration` | Strongly-typed options and fail-fast validation. |
| `Runner.Markdown` | Parsing and updating the `tasks.md` checklist. |
| `Runner.Terminal` | PTY session runners, process teardown, workspace diffing + meaningful-change classification, build/test validation, limit detection. |
| `Runner.Core` | Orchestration: state machine, sequential loop, per-attempt pipeline, retry with failure context, verification, checkpointing, progress. |
| `Runner.Logging` | Structured + console logging. |
| `Runner.Console` | The CLI executable, live status display, and halt reports. |

## Testing

```sh
dotnet test AntigravityTaskRunner.slnx
```

The suite (160 tests) runs entirely against mocks and temp directories — no real `agy` or network
required. Coverage includes: successful execution, strict sequential non-overlap, retry logic with
failure context, verification failures (missing marker, no changes, non-meaningful changes), build
and test failures, session timeouts, token/rate-limit pause/resume, checkpoint persistence, crash
recovery at the exact attempt, fail-stop on unrecoverable failures, and the state machine's legal
and illegal transitions.
