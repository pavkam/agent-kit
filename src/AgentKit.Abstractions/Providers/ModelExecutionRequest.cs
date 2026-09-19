// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one model execution before the executor starts an attempt.</summary>
/// <remarks>
/// <para>
/// The architecture names the request body <c>ModelRequestContext</c>. This
/// record uses the shipped <see cref="LlmRequestContext"/> until context
/// assembly replaces that stand-in. <see cref="Budget"/> and <see cref="Hooks"/>
/// are nullable because a caller may execute before a budget scope or hook
/// dispatch exists; the executor must not invent either.
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/>, <paramref name="selection"/>, <paramref name="context"/>,
    /// or <paramref name="retryPolicy"/> is null.
    /// </exception>
    public ModelExecutionRequest(
        ProtectedSemanticOperationContext operation,
        ModelSelectionDecision selection,
        LlmRequestContext context,
        BudgetExecutionCapability? budget,
        HookDispatchContext? hooks,
        ProviderRetryPolicy retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        Operation = operation;
        Selection = selection;
        Context = context;
        Budget = budget;
        Hooks = hooks;
        RetryPolicy = retryPolicy;
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
}
