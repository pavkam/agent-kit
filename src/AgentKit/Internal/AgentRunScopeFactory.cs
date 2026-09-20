// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using AgentKit;

/// <summary>
/// The sole owner of <see cref="IServiceScopeFactory"/> for run activation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CreateAsync"/> creates a scope, resolves the scoped <see cref="IAgentRunPlanCompiler"/>, and returns
/// only the plan plus a lifetime lease. Feature packages do not receive the scope provider from this type.
/// </para>
/// <para>
/// <see cref="PrepareAsync"/> is the runtime's admission path. It lets the caller open or create the session from
/// the same scope before compilation, so session coordination and the compiled plan share one scope.
/// </para>
/// </remarks>
internal sealed class AgentRunScopeFactory: IAgentRunScopeFactory
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Initializes the factory over the composition's scope factory.</summary>
    /// <param name="scopes">The non-null scope factory. This type does not dispose it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scopes"/> is null.</exception>
    internal AgentRunScopeFactory(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        _scopes = scopes;
    }

    /// <inheritdoc/>
    public async ValueTask<AgentRunScopeLease> CreateAsync(
        ResolvedAgentDefinition definition,
        AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        var (Result, Lease) = await PrepareAsync(
            definition,
            (_, _) => new ValueTask<AgentRunRequest>(request),
            cancellationToken).ConfigureAwait(false);
        return Lease ?? throw new InvalidOperationException(Describe(Result));
    }

    /// <summary>
    /// Opens one scope, lets the caller build the request from that scope, then compiles the plan.
    /// </summary>
    /// <param name="definition">The resolved definition to compile.</param>
    /// <param name="prepareRequest">
    /// Builds the request from the scope provider. The callback may open or create the session. It must not
    /// retain the provider.
    /// </param>
    /// <param name="cancellationToken">Cancels preparation. The scope is disposed on cancellation and failure.</param>
    /// <returns>
    /// The compilation result. <see cref="AgentRunScopeLease"/> is non-null only when compilation succeeded; the
    /// caller owns that lease. A failed compilation has already disposed the scope.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/> or <paramref name="prepareRequest"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal async ValueTask<(AgentRunPlanCompilationResult Result, AgentRunScopeLease? Lease)> PrepareAsync(
        ResolvedAgentDefinition definition,
        Func<IServiceProvider, CancellationToken, ValueTask<AgentRunRequest>> prepareRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(prepareRequest);

        var scope = _scopes.CreateAsyncScope();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = await prepareRequest(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(request);
            var compiler = scope.ServiceProvider.GetRequiredService<IAgentRunPlanCompiler>();
            var result = await compiler.CompileAsync(definition, request, cancellationToken).ConfigureAwait(false);
            if (result is not CompiledAgentRunPlan compiled)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                return (result, null);
            }

            return (compiled, new AgentRunScopeLease(compiled.Plan, scope, scope.ServiceProvider));
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static string Describe(AgentRunPlanCompilationResult result) =>
        result is InvalidAgentRunPlan invalid
            ? string.Join("; ", invalid.Diagnostics.Select(static diagnostic => diagnostic.SafeMessage))
            : "The run plan could not be compiled.";
}
