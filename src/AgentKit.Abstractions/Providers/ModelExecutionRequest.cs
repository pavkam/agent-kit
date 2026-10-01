// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one model execution before the executor starts an attempt.</summary>
/// <remarks>
/// <para>
/// The request body is the <see cref="LlmRequestContext"/> the context
/// assembler produced. <see cref="Budget"/> and <see cref="Hooks"/> are
/// nullable because a caller may execute without a budget scope or hook
/// dispatch (the first-party loop reserves budget per turn itself and
/// dispatches hooks around the call); the executor must not invent either.
/// </para>
/// <para>
/// The selection, context, and retry policy are captured here and are not
/// reread from mutable options during attempts.
/// </para>
/// </remarks>
public sealed record ModelExecutionRequest
{
    /// <summary>Initializes a captured execution request.</summary>
    /// <param name="operation">The protected semantic operation this execution serves.</param>
    /// <param name="selection">The model selection to execute. Fallback does not mutate it.</param>
    /// <param name="context">The provider-neutral request body.</param>
    /// <param name="budget">The live budget capability when one is bound; otherwise null.</param>
    /// <param name="hooks">The hook dispatch when one is active; otherwise null.</param>
    /// <param name="retryPolicy">The same-model retry bound.</param>
    /// <param name="fallback">
    /// The selection-policy fallback mode captured with this execution. The executor uses it only to decide whether a
    /// retryable failure should surface <see cref="ModelFallbackRequired"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/>, <paramref name="selection"/>, <paramref name="context"/>,
    /// or <paramref name="retryPolicy"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fallback"/> is not defined.</exception>
    public ModelExecutionRequest(
        ProtectedSemanticOperationContext operation,
        ModelSelectionDecision selection,
        LlmRequestContext context,
        BudgetExecutionCapability? budget,
        HookDispatchContext? hooks,
        ProviderRetryPolicy retryPolicy,
        ModelFallbackPolicy fallback = ModelFallbackPolicy.FirstCandidateOnly)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentOutOfRangeException.ThrowIfUndefined(fallback);
        Operation = operation;
        Selection = selection;
        Context = context;
        Budget = budget;
        Hooks = hooks;
        RetryPolicy = retryPolicy;
        Fallback = fallback;
    }

    /// <summary>Gets the protected operation.</summary>
    public ProtectedSemanticOperationContext Operation { get; }

    /// <summary>Gets the captured model selection.</summary>
    public ModelSelectionDecision Selection { get; }

    /// <summary>Gets the provider-neutral request body.</summary>
    public LlmRequestContext Context { get; }

    /// <summary>Gets the live budget capability when one is bound.</summary>
    public BudgetExecutionCapability? Budget { get; }

    /// <summary>Gets the active hook dispatch when one exists.</summary>
    public HookDispatchContext? Hooks { get; }

    /// <summary>Gets the same-model retry bound.</summary>
    public ProviderRetryPolicy RetryPolicy { get; }

    /// <summary>Gets the captured selection-policy fallback mode.</summary>
    public ModelFallbackPolicy Fallback { get; }
}
