// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Runs every policy registered for a profile's policy key in order and combines their decisions monotonically toward refusal.</summary>
/// <remarks>
/// A denial, an unresolvable policy, or a failing policy refuses the proposal immediately. Under the default acceptance mode a
/// proposal is accepted only when at least one registered policy explicitly allowed it; under
/// <see cref="MemoryAcceptanceMode.AllowUnlessPolicyDenies"/> the absence of a denial suffices. Cancellation always propagates.
/// </remarks>
internal sealed class DefaultMemoryPolicyDispatcher: IMemoryPolicyDispatcher
{
    private static readonly ComponentId _dispatcherId = new("agentkit.memory.policy-dispatcher");

    private readonly ImmutableArray<MemoryPolicyDeclaration> _declarations;
    private readonly IServiceProvider _services;
    private readonly IOptions<AgentMemoryOptions> _options;
    private readonly ILogger<DefaultMemoryPolicyDispatcher> _logger;

    /// <summary>Initializes the dispatcher.</summary>
    /// <param name="declarations">Every registered policy declaration.</param>
    /// <param name="services">The container the declared policy types are resolved from.</param>
    /// <param name="options">The engine-wide options carrying the acceptance mode.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultMemoryPolicyDispatcher(
        IEnumerable<MemoryPolicyDeclaration> declarations,
        IServiceProvider services,
        IOptions<AgentMemoryOptions> options,
        ILogger<DefaultMemoryPolicyDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        _declarations = [.. declarations.OrderBy(static declaration => declaration.Registration.Order)];
        _services = services;
        _options = options;
        _logger = logger ?? NullLogger<DefaultMemoryPolicyDispatcher>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        MemoryPolicyAllowed? firstAllow = null;
        using var scope = _services.CreateScope();
        foreach (var declaration in _declarations)
        {
            if (declaration.Registration.Profile != context.Profile.PolicyProfile)
            {
                continue;
            }

            var id = declaration.Registration.Id;
            if (scope.ServiceProvider.GetService(declaration.PolicyType) is not IMemoryPolicy policy)
            {
                MemoryObservation.Safe(() => MemoryLog.PolicyUnavailable(_logger, id.Value));
                return new MemoryPolicyDenied(id, "policy-unavailable", "A configured memory policy is not available.");
            }

            MemoryPolicyDecision decision;
            try
            {
                decision = await policy.EvaluateAsync(proposal, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                MemoryObservation.Safe(() => MemoryLog.PolicyFailed(_logger, id.Value, exception.GetType().Name));
                return new MemoryPolicyDenied(id, "policy-failed", "A configured memory policy failed, so the proposal was refused.");
            }

            switch (decision)
            {
                case MemoryPolicyDenied denied:
                    return denied;
                case MemoryPolicyAllowed allowed:
                    firstAllow ??= allowed;
                    break;
                default:
                    return new MemoryPolicyDenied(id, "policy-invalid", "A configured memory policy returned an unrecognized decision.");
            }
        }

        return firstAllow is not null
            ? firstAllow
            : _options.Value.AcceptanceMode == MemoryAcceptanceMode.AllowUnlessPolicyDenies
                ? new MemoryPolicyAllowed(_dispatcherId)
                : new MemoryPolicyDenied(FailClosedMemoryPolicy.Id, "no-explicit-allow", "No memory policy explicitly allows retention.");
    }
}
