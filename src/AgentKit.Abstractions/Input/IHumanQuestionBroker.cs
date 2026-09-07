// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Publishes authorized human questions and waits for their application-owned resolution.</summary>
/// <remarks>
/// Implementations own delivery and resumption mechanics. Durable hosts must persist the pending request and
/// exactly-one resolution independently of an in-memory task; adapters also authenticate responses as new input.
/// </remarks>
public interface IHumanQuestionBroker
{
    /// <summary>Gets the component audience for question-publication grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Publishes one exact authorized question and waits until it is answered or reaches a terminal condition.</summary>
    /// <param name="request">The bounded request whose grant must be consumed immediately before publication.</param>
    /// <param name="cancellationToken">Cancels this caller's wait without fabricating an answer.</param>
    /// <returns>The answered, timed-out, or unavailable result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<HumanQuestionResult> AskAsync(
        HumanQuestionRequest request,
        CancellationToken cancellationToken = default);
}
