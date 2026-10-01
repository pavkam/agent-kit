// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Selects registered execution policies by exact key and version from an immutable composition-time view.</summary>
/// <remarks>
/// <para>
/// The view is materialized once when the host composes, so selection never touches the container. A reference that is
/// not registered, or that the capability's bindings do not permit, is <see cref="ToolExecutionPolicyUnavailable"/>:
/// there is no default policy, no newer-revision substitution, and no unkeyed fallback. Policies are borrowed host
/// singletons; the selector owns none of them. The class is immutable and safe for concurrent use.
/// </para>
/// </remarks>
internal sealed class ToolExecutionPolicySelector: IToolExecutionPolicySelector
{
    private readonly FrozenDictionary<ToolExecutionPolicyReference, IToolExecutionPolicy> _policies;
    private readonly ILogger<ToolExecutionPolicySelector> _logger;

    /// <summary>Initializes the selector over a complete, validated view.</summary>
    /// <param name="policies">The registered policies by exact reference; each policy's own reference must equal its key.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency or policy is null.</exception>
    /// <exception cref="ArgumentException">A policy's <see cref="IToolExecutionPolicy.Reference"/> differs from the reference it is registered under.</exception>
    internal ToolExecutionPolicySelector(
        IReadOnlyDictionary<ToolExecutionPolicyReference, IToolExecutionPolicy> policies,
        ILogger<ToolExecutionPolicySelector> logger)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(logger);
        foreach (var (reference, policy) in policies)
        {
            ArgumentNullException.ThrowIfNull(policy, nameof(policies));
            ArgumentException.ThrowIfNotEqual(policy.Reference, reference, nameof(policies));
        }

        _policies = policies.ToFrozenDictionary();
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<ToolExecutionPolicySelectionResult> SelectAsync(
        ToolExecutionPolicyReference reference,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(capability);
        cancellationToken.ThrowIfCancellationRequested();
        var permitted = false;
        foreach (var binding in capability.ExecutionPolicies)
        {
            if (binding.Reference == reference)
            {
                permitted = true;
                break;
            }
        }

        if (permitted && _policies.TryGetValue(reference, out var policy))
        {
            Observe(() => ToolLog.ExecutionPolicySelected(_logger, reference.Key, reference.Version));
            return ValueTask.FromResult<ToolExecutionPolicySelectionResult>(new ToolExecutionPolicySelected(policy));
        }

        Observe(() => ToolLog.ExecutionPolicyUnavailable(_logger, reference.Key, reference.Version));
        return ValueTask.FromResult<ToolExecutionPolicySelectionResult>(new ToolExecutionPolicyUnavailable(reference));
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The selector supplies each observation callback.");
        try
        {
            observation();
        }
        catch
        {
            // Instrumentation is observational only and cannot change selection.
        }
    }
}
