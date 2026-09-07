// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Adapts an application UI, protocol, or durable workflow to authenticated human-question resolution.</summary>
/// <remarks>
/// A durable implementation persists the prompt and exactly one authenticated resolution independently of the
/// caller's in-memory wait. The protected broker invokes this channel only after consuming publication authority.
/// </remarks>
public interface IHumanQuestionChannel
{
    /// <summary>Publishes one authorized prompt and waits for its terminal resolution.</summary>
    /// <param name="prompt">The bounded grant-free question projection.</param>
    /// <param name="cancellationToken">Cancels this caller's wait without fabricating a response.</param>
    /// <returns>The answered, timed-out, or unavailable terminal result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prompt"/> is null.</exception>
    public ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionPrompt prompt,
        CancellationToken cancellationToken = default);
}
