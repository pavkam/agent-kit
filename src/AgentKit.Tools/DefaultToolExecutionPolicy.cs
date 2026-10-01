// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Plans every call under one exact execution-policy reference from the configured <see cref="ToolRuntimeOptions"/>.</summary>
/// <remarks>
/// <para>
/// The policy keeps each descriptor's own scheduling hints, applies the configured per-attempt timeout and retry pacing,
/// and captures the default result-normalization rules for the reference. It is a pure planner: it performs no I/O,
/// authorizes nothing, and cannot widen a descriptor's declared effects. Whether a failed attempt is actually retried is
/// decided by the executor from the tool's declared effects and idempotency, not by this plan.
/// </para>
/// <para>
/// One instance serves one reference. Register it with
/// <see cref="ServiceExtensions.AddToolExecutionPolicy{TPolicy}(IServiceCollection, ToolExecutionPolicyReference)"/>;
/// dependency injection supplies the exact reference through the service key. The instance is immutable and safe for
/// concurrent use.
/// </para>
/// </remarks>
public sealed class DefaultToolExecutionPolicy: IToolExecutionPolicy
{
    private readonly ToolRetryPolicy _retry;
    private readonly TimeSpan _invocationTimeout;

    /// <summary>Initializes the default policy for one exact reference.</summary>
    /// <param name="reference">The exact policy key and revision this instance implements, supplied by the DI service key.</param>
    /// <param name="options">The configured retry pacing and per-attempt timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured retry pacing or timeout is outside its documented range.</exception>
    public DefaultToolExecutionPolicy([ServiceKey] ToolExecutionPolicyReference reference, IOptions<ToolRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Value;
        Reference = reference;
        _retry = new ToolRetryPolicy(
            configured.MaximumAttempts,
            configured.RetryInitialDelay,
            configured.RetryBackoffMultiplier,
            configured.RetryMaximumDelay,
            configured.RetryJitterFraction);
        _invocationTimeout = configured.InvocationTimeout;
    }

    /// <summary>Gets the reference first-party tool descriptors name: the <c>standard</c> family at revision one.</summary>
    /// <value>An immutable reference that <c>AddAgentTools</c> registers this policy under when nothing else already is.</value>
    public static ToolExecutionPolicyReference StandardReference { get; } = new(
        new ToolExecutionPolicyKey("standard"),
        new ToolExecutionPolicyVersion(1));

    /// <inheritdoc/>
    public ToolExecutionPolicyReference Reference { get; }

    /// <inheritdoc/>
    public ValueTask<ToolExecutionPlanResult> PlanAsync(
        ImmutableArray<ValidatedToolCall> calls,
        ToolExecutionPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfDefault(calls);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var normalization = ToolRuntimeNormalizationDefaults.ForResolvedTool(Reference);
        var prepared = ImmutableArray.CreateBuilder<PreparedToolCall>(calls.Length);
        foreach (var call in calls)
        {
            ArgumentNullException.ThrowIfNull(call, nameof(calls));
            ArgumentException.ThrowIfNotEqual(call.ExecutionPolicy, Reference, nameof(calls));
            prepared.Add(new PreparedToolCall(
                call,
                new ToolExecutionPlan(call.Tool.ExecutionHints, _retry, _invocationTimeout, normalization)));
        }

        return ValueTask.FromResult<ToolExecutionPlanResult>(new ToolExecutionPlanned(prepared.ToImmutable()));
    }
}
