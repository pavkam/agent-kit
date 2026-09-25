// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one rerank execution before attempts begin.</summary>
public sealed record RerankExecutionRequest
{
    /// <summary>Initializes a rerank execution request.</summary>
    /// <param name="operation">The protected operation.</param>
    /// <param name="selection">The captured selection.</param>
    /// <param name="request">The rerank request body.</param>
    /// <param name="budget">The budget capability when bound.</param>
    /// <param name="hooks">The hook dispatch when active.</param>
    /// <param name="retryPolicy">The retry policy.</param>
    /// <param name="fallback">The semantic fallback mode.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fallback"/> is undefined.</exception>
    public RerankExecutionRequest(
        ProtectedSemanticOperationContext operation,
        RerankerSelectionDecision selection,
        RerankRequest request,
        BudgetExecutionCapability? budget,
        HookDispatchContext? hooks,
        SemanticOperationRetryPolicy retryPolicy,
        SemanticFallbackPolicy fallback = SemanticFallbackPolicy.FirstCandidateOnly)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentOutOfRangeException.ThrowIfUndefined(fallback);
        Operation = operation;
        Selection = selection;
        Request = request;
        Budget = budget;
        Hooks = hooks;
        RetryPolicy = retryPolicy;
        Fallback = fallback;
    }

    /// <summary>Gets the protected operation.</summary>
    public ProtectedSemanticOperationContext Operation { get; }

    /// <summary>Gets the captured selection.</summary>
    public RerankerSelectionDecision Selection { get; }

    /// <summary>Gets the rerank request body.</summary>
    public RerankRequest Request { get; }

    /// <summary>Gets the budget capability when bound.</summary>
    public BudgetExecutionCapability? Budget { get; }

    /// <summary>Gets the hook dispatch when active.</summary>
    public HookDispatchContext? Hooks { get; }

    /// <summary>Gets the retry policy.</summary>
    public SemanticOperationRetryPolicy RetryPolicy { get; }

    /// <summary>Gets the fallback mode.</summary>
    public SemanticFallbackPolicy Fallback { get; }
}
