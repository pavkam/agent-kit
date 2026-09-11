// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Frozen;

/// <summary>Resolves exact projection-policy revisions from an immutable host-configured snapshot set.</summary>
/// <remarks>
/// This thread-safe catalog captures configuration once and has no mutable publication or persistence surface.
/// Equal duplicate snapshots are idempotent; conflicting content under one reference rejects composition.
/// Hosts must retain and register every revision needed by recorded results, or replace the catalog with a
/// retained-policy implementation. Empty configuration is valid and resolves every reference as unavailable.
/// </remarks>
public sealed class ToolResultProjectionPolicyCatalog: IToolResultProjectionPolicyCatalog
{
    private readonly FrozenDictionary<ToolResultProjectionPolicyReference, ToolResultProjectionPolicySnapshot> _policies;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolResultProjectionPolicyCatalog> _logger;

    /// <summary>Captures a complete immutable set of projection-policy revisions.</summary>
    /// <param name="policies">The nonnull configured snapshots, with no null entries or conflicting duplicate references.</param>
    /// <param name="timeProvider">The nonnull replaceable clock used only for diagnostic duration.</param>
    /// <param name="logger">The nonnull type-specific logger receiving content-free lookup events.</param>
    /// <exception cref="ArgumentNullException">A parameter or an element of <paramref name="policies"/> is null.</exception>
    /// <exception cref="ArgumentException">A reference in <paramref name="policies"/> is associated with different policy content.</exception>
    public ToolResultProjectionPolicyCatalog(
        IEnumerable<ToolResultProjectionPolicySnapshot> policies,
        TimeProvider timeProvider,
        ILogger<ToolResultProjectionPolicyCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        var captured = new Dictionary<ToolResultProjectionPolicyReference, ToolResultProjectionPolicySnapshot>();
        foreach (var policy in policies)
        {
            ArgumentNullException.ThrowIfNull(policy, nameof(policies));
            if (captured.TryGetValue(policy.Reference, out var existing))
            {
                ArgumentException.ThrowIfNotEqual(existing, policy, nameof(policies));
            }
            else
            {
                captured.Add(policy.Reference, policy);
            }
        }

        _policies = captured.ToFrozenDictionary();
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<ToolResultProjectionPolicyResolution> ResolveAsync(
        ToolResultProjectionPolicyReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var started = TryGetTimestamp();
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.ToolResultProjectionPolicyResolve,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ToolResultProjectionPolicyResolve },
                { AgentKitTagNames.ToolResultProjectionPolicyKey, reference.Key.Value },
                { AgentKitTagNames.ToolResultProjectionPolicyVersion, reference.Version.Value },
            });
        Observe(() => ToolLog.ProjectionPolicyResolutionStarted(_logger, reference.Key, reference.Version));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ToolResultProjectionPolicyResolution result;
            if (_policies.TryGetValue(reference, out var snapshot))
            {
                result = new ToolResultProjectionPolicyResolved(snapshot);
                Observe(() => ToolLog.ProjectionPolicyResolved(_logger, reference.Key, reference.Version));
                Complete(scope.Activity, "resolved", started);
            }
            else
            {
                result = new ToolResultProjectionPolicyUnavailable(reference);
                Observe(() => ToolLog.ProjectionPolicyUnavailable(_logger, reference.Key, reference.Version));
                Complete(scope.Activity, "unavailable", started);
            }

            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Observe(() => ToolLog.ProjectionPolicyResolutionCancelled(_logger, reference.Key, reference.Version));
            Complete(scope.Activity, "cancelled", started);
            throw;
        }
    }

    private void Complete(Activity? activity, string outcome, long? started)
    {
        Debug.Assert(outcome is "resolved" or "unavailable" or "cancelled", "Policy lookup has a closed diagnostic outcome vocabulary.");
        Observe(() =>
        {
            if (outcome == "resolved")
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
            }
        });
        var tags = new TagList { { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolResultProjectionPolicyMetrics.Count.Add(1, tags));
        if (started is { } timestamp)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(timestamp);
                if (elapsed >= TimeSpan.Zero)
                {
                    ToolResultProjectionPolicyMetrics.Duration.Record(elapsed.TotalSeconds, tags);
                }
            });
        }
    }

    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "Diagnostic work is supplied by the catalog.");
        try
        {
            observation();
        }
        catch
        {
            // Diagnostic clocks, loggers, and listeners never alter exact policy selection.
        }
    }
}
