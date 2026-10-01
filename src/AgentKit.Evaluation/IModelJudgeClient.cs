// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Obtains one judge model reply for a rubric prompt.</summary>
/// <remarks>
/// The seam keeps <see cref="ModelJudgeEvaluator"/> independent of how a model is reached. The first-party
/// <see cref="ModelRequestJudgeClient"/> uses the provider-neutral model catalog, selector, and model adapter; a recorded
/// client replays fixtures offline. Implementations are thread-safe, never throw for a provider failure (they return
/// <see cref="ModelJudgeFailed"/>), and propagate cancellation.
/// </remarks>
public interface IModelJudgeClient
{
    /// <summary>Requests one judge sample.</summary>
    /// <param name="request">The sample request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reply, or a typed failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ValueTask<ModelJudgeResponse> JudgeAsync(ModelJudgeRequest request, CancellationToken cancellationToken = default);
}
