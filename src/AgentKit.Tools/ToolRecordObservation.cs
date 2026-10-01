// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Isolates content-free observation of one tool-call recorder write.</summary>
/// <remarks>
/// The scope opens one activity, writes start and completion log events carrying only safe correlation identities, and
/// records one bounded outcome counter and duration. Arguments, results, and exception text never enter a signal. Every
/// instrumentation failure is contained and never changes the recording outcome.
/// </remarks>
internal sealed class ToolRecordObservation: IDisposable
{
    private readonly string _stage;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly AgentId _agentId;
    private readonly SessionId _sessionId;
    private readonly RunId _runId;
    private readonly TurnId _turnId;
    private readonly ToolCallId _callId;
    private readonly AgentKitActivityScope _scope;
    private readonly long? _started;
    private bool _completed;

    /// <summary>Starts observation of one recorder write.</summary>
    /// <param name="stage">Exactly <c>accepted</c> or <c>terminal</c>.</param>
    /// <param name="agentId">The owning agent.</param><param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param><param name="turnId">The active turn.</param><param name="callId">The correlated call.</param>
    /// <param name="clock">The observation-only clock.</param><param name="logger">The recorder's logger.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stage"/> is not a defined record stage.</exception>
    internal ToolRecordObservation(
        string stage,
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        ToolCallId callId,
        TimeProvider clock,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNotEqual(stage is "accepted" or "terminal", true, nameof(stage));
        _stage = stage;
        _agentId = agentId;
        _sessionId = sessionId;
        _runId = runId;
        _turnId = turnId;
        _callId = callId;
        _clock = clock;
        _logger = logger;
        try
        {
            _started = clock.GetTimestamp();
        }
        catch
        {
            // Missing observation time remains unknown.
        }

        var name = stage == "accepted" ? AgentKitActivityNames.ToolCallRecordAccepted : AgentKitActivityNames.ToolCallRecordTerminal;
        _scope = AgentKitActivityScope.Start(
            name,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, name },
                { AgentKitTagNames.AgentId, agentId.ToString() },
                { AgentKitTagNames.SessionId, sessionId.ToString() },
                { AgentKitTagNames.RunId, runId.ToString() },
                { AgentKitTagNames.TurnId, turnId.ToString() },
                { AgentKitTagNames.ToolCallId, callId.ToString() },
            });
        Observe(() => ToolLog.CallRecordStarted(logger, stage, agentId, sessionId, runId, turnId, callId));
    }

    /// <summary>Records the first valid terminal outcome.</summary>
    /// <param name="outcome">Exactly recorded, conflict, invalid, unavailable, cancelled, or failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is undefined.</exception>
    internal void Complete(string outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNotEqual(outcome is "recorded" or "conflict" or "invalid" or "unavailable" or "cancelled" or "failed", true, nameof(outcome));
        if (_completed)
        {
            return;
        }

        _completed = true;
        var level = outcome switch
        {
            "recorded" => LogLevel.Debug,
            "cancelled" => LogLevel.Information,
            "failed" => LogLevel.Error,
            _ => LogLevel.Warning,
        };
        Observe(() => ToolLog.CallRecordCompleted(_logger, level, _stage, _agentId, _sessionId, _runId, _turnId, _callId, outcome));
        Observe(() =>
        {
            if (outcome == "recorded")
            {
                _scope.Activity.SetSuccessful(outcome);
            }
            else
            {
                _scope.Activity.SetFailed(outcome, outcome);
            }
        });
        var tags = new TagList { { AgentKitTagNames.GenAiOperationName, _stage }, { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolRecordingMetrics.RecordCount.Add(1, tags));
        if (_started is { } started)
        {
            Observe(() =>
            {
                var elapsed = _clock.GetElapsedTime(started);
                if (elapsed >= TimeSpan.Zero)
                {
                    ToolRecordingMetrics.RecordDuration.Record(elapsed.TotalSeconds, tags);
                }
            });
        }
    }

    /// <summary>Records uncompleted work as failure and restores the captured activity parent.</summary>
    /// <remarks>Repeated disposal is harmless; instrumentation failure never changes the recording.</remarks>
    public void Dispose()
    {
        if (!_completed)
        {
            Complete("failed");
        }

        _scope.Dispose();
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The recorder supplies each observation callback.");
        try
        {
            observation();
        }
        catch
        {
            // Instrumentation is observational only.
        }
    }
}
