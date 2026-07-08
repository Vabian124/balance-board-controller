using System.Text.Json;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App.Services;

internal enum PhysicalTestStepOutcome
{
    Pending,
    Passed,
    Failed,
    Skipped,
}

internal sealed class PhysicalTestObservation
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public bool IsConnected { get; init; }
    public double WeightKg { get; init; }
    public double BalanceX { get; init; }
    public double BalanceY { get; init; }
    public bool JumpDetected { get; init; }
    public string StatusText { get; init; } = string.Empty;
}

internal sealed class PhysicalTestStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Instructions { get; init; }
    public string ExpectedSignal { get; init; } = string.Empty;
    public Func<PhysicalTestObservation, bool>? AutoPassWhen { get; init; }
}

internal sealed class PhysicalTestScenarioDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<PhysicalTestStep> Steps { get; init; }
}

internal sealed class PhysicalTestStepRecord
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public PhysicalTestStepOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

internal sealed class PhysicalTestRunRecord
{
    public required string ScenarioId { get; set; }
    public required string ScenarioName { get; set; }
    public required string RunId { get; set; }
    public required string OutputDirectory { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "Running";
    public string MachineName { get; set; } = Environment.MachineName;
    public string UserName { get; set; } = Environment.UserName;
    public List<PhysicalTestStepRecord> Steps { get; set; } = [];
}

internal sealed class PhysicalTestArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly object _sync = new();

    public PhysicalTestArtifactWriter(string scenarioId)
    {
        var safeScenario = string.Concat(scenarioId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(safeScenario))
        {
            safeScenario = "physical-test";
        }

        RunId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeScenario}";
        OutputDirectory = System.IO.Path.Combine(AppDataPaths.PhysicalTestsDirectory, RunId);
        System.IO.Directory.CreateDirectory(OutputDirectory);
        SummaryPath = System.IO.Path.Combine(OutputDirectory, "run.json");
        EventLogPath = System.IO.Path.Combine(OutputDirectory, "events.jsonl");
    }

    public string RunId { get; }

    public string OutputDirectory { get; }

    public string SummaryPath { get; }

    public string EventLogPath { get; }

    public void WriteSummary(PhysicalTestRunRecord record)
    {
        lock (_sync)
        {
            System.IO.File.WriteAllText(SummaryPath, JsonSerializer.Serialize(record, JsonOptions));
        }
    }

    public void AppendEvent(string eventType, object payload)
    {
        var envelope = new
        {
            timestampUtc = DateTime.UtcNow,
            eventType,
            payload,
        };

        lock (_sync)
        {
            System.IO.File.AppendAllText(
                EventLogPath,
                JsonSerializer.Serialize(envelope) + Environment.NewLine);
        }
    }
}

internal static class PhysicalTestScenarioCatalog
{
    public static PhysicalTestScenarioDefinition Create(string scenarioId)
    {
        if (string.Equals(scenarioId, "connect-basic", StringComparison.OrdinalIgnoreCase))
        {
            return new PhysicalTestScenarioDefinition
            {
                Id = "connect-basic",
                DisplayName = "Connect and motion basics",
                Description = "Manual hardware check for connect, weight detection, lean response, and disconnect.",
                Steps =
                [
                    new PhysicalTestStep
                    {
                        Id = "connect",
                        Title = "Connect the board",
                        Instructions = "Click Connect, then press the red SYNC button under the battery cover if Windows has not paired the board yet.",
                        ExpectedSignal = "Connection chip turns ready and session status shows Connected.",
                        AutoPassWhen = obs => obs.IsConnected,
                    },
                    new PhysicalTestStep
                    {
                        Id = "step-on",
                        Title = "Verify live weight",
                        Instructions = "Step on the board and wait for the live weight readout to move above the idle threshold.",
                        ExpectedSignal = "Weight rises above 5 kg and live balance values update.",
                        AutoPassWhen = obs => obs.IsConnected && obs.WeightKg >= 5,
                    },
                    new PhysicalTestStep
                    {
                        Id = "lean-check",
                        Title = "Check lean feedback",
                        Instructions = "Lean left, right, forward, and backward. Confirm the balance visual, direction text, and any active output profile feedback look correct.",
                        ExpectedSignal = "Dashboard feedback follows movement without freezes or obvious inversion.",
                    },
                    new PhysicalTestStep
                    {
                        Id = "tare-center",
                        Title = "Validate tare and center",
                        Instructions = "Step off the board, click Tare, then stand neutrally and click Set center if needed.",
                        ExpectedSignal = "Weight returns near zero off-board and neutral standing looks centered.",
                    },
                    new PhysicalTestStep
                    {
                        Id = "disconnect",
                        Title = "Disconnect cleanly",
                        Instructions = "Click Disconnect and confirm the board returns to offline without leaving stuck input or vJoy output.",
                        ExpectedSignal = "Connection chip returns offline and controls are released cleanly.",
                    },
                ],
            };
        }

        throw new ArgumentException(
            $"Unknown physical test scenario '{scenarioId}'. Supported scenarios: connect-basic.",
            nameof(scenarioId));
    }
}

internal sealed class PhysicalTestRunner
{
    private readonly PhysicalTestArtifactWriter _artifacts;
    private readonly FileLogService _log;
    private readonly PhysicalTestRunRecord _record;
    private PhysicalTestObservation _lastObservation = new();

    public PhysicalTestRunner(PhysicalTestScenarioDefinition scenario, FileLogService log)
    {
        Scenario = scenario;
        _log = log;
        _artifacts = new PhysicalTestArtifactWriter(scenario.Id);
        _record = new PhysicalTestRunRecord
        {
            ScenarioId = scenario.Id,
            ScenarioName = scenario.DisplayName,
            RunId = _artifacts.RunId,
            OutputDirectory = _artifacts.OutputDirectory,
            StartedAtUtc = DateTime.UtcNow,
            Steps = scenario.Steps.Select(step => new PhysicalTestStepRecord
            {
                Id = step.Id,
                Title = step.Title,
                Outcome = PhysicalTestStepOutcome.Pending,
            }).ToList(),
        };

        _artifacts.WriteSummary(_record);
        _artifacts.AppendEvent("run-started", new
        {
            scenario = scenario.Id,
            scenarioName = scenario.DisplayName,
            steps = scenario.Steps.Select(step => step.Id).ToArray(),
        });
        if (Scenario.Steps.Count > 0)
        {
            _artifacts.AppendEvent("step-started", new
            {
                stepId = Scenario.Steps[0].Id,
                stepTitle = Scenario.Steps[0].Title,
            });
        }
    }

    public PhysicalTestScenarioDefinition Scenario { get; }

    public string OutputDirectory => _artifacts.OutputDirectory;

    public int CurrentStepIndex { get; private set; }

    public bool IsComplete => CurrentStepIndex >= Scenario.Steps.Count;

    public PhysicalTestStep? CurrentStep => IsComplete ? null : Scenario.Steps[CurrentStepIndex];

    public IReadOnlyList<PhysicalTestStepRecord> Steps => _record.Steps;

    public string OverallStatus => _record.Status;

    public void UpdateObservation(PhysicalTestObservation observation)
    {
        _lastObservation = observation;
        var step = CurrentStep;
        if (step?.AutoPassWhen is null)
        {
            return;
        }

        if (step.AutoPassWhen(observation))
        {
            CompleteCurrentStep(PhysicalTestStepOutcome.Passed, "Auto-observed signal matched expected hardware state.");
        }
    }

    public void MarkPassed(string? notes = null) => CompleteCurrentStep(PhysicalTestStepOutcome.Passed, notes);

    public void MarkFailed(string? notes = null) => CompleteCurrentStep(PhysicalTestStepOutcome.Failed, notes);

    public void MarkSkipped(string? notes = null) => CompleteCurrentStep(PhysicalTestStepOutcome.Skipped, notes);

    public void FinishIfNeeded()
    {
        if (_record.CompletedAtUtc is not null)
        {
            return;
        }

        _record.CompletedAtUtc = DateTime.UtcNow;
        _record.Status = ComputeFinalStatus();
        _artifacts.AppendEvent("run-finished", new
        {
            status = _record.Status,
            lastObservation = _lastObservation,
        });
        _artifacts.WriteSummary(_record);
        _log.Write($"[PHYSICAL] Run finished with status={_record.Status}. Artifacts: {_artifacts.OutputDirectory}");
    }

    private void CompleteCurrentStep(PhysicalTestStepOutcome outcome, string? notes)
    {
        if (IsComplete)
        {
            return;
        }

        var step = CurrentStep!;
        var record = _record.Steps[CurrentStepIndex];
        if (record.Outcome != PhysicalTestStepOutcome.Pending)
        {
            return;
        }

        record.Outcome = outcome;
        record.Notes = notes;
        record.CompletedAtUtc = DateTime.UtcNow;
        _artifacts.AppendEvent("step-completed", new
        {
            stepId = step.Id,
            stepTitle = step.Title,
            outcome,
            notes,
            observation = _lastObservation,
        });
        _artifacts.WriteSummary(_record);
        _log.Write($"[PHYSICAL] Step {step.Id} -> {outcome}. {notes}".Trim());

        CurrentStepIndex++;
        if (IsComplete)
        {
            FinishIfNeeded();
            return;
        }

        var next = CurrentStep!;
        _artifacts.AppendEvent("step-started", new
        {
            stepId = next.Id,
            stepTitle = next.Title,
        });
        _artifacts.WriteSummary(_record);
    }

    private string ComputeFinalStatus()
    {
        if (_record.Steps.Any(step => step.Outcome == PhysicalTestStepOutcome.Failed))
        {
            return "Failed";
        }

        if (_record.Steps.All(step => step.Outcome == PhysicalTestStepOutcome.Passed))
        {
            return "Passed";
        }

        if (_record.Steps.Any(step => step.Outcome == PhysicalTestStepOutcome.Pending))
        {
            return "Aborted";
        }

        return "CompletedWithSkips";
    }
}
