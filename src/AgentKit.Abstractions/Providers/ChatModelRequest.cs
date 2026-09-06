// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request for one chat model attempt.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="Attempt"/> lets a same-model retry
/// reuse the same <see cref="ChatRequestContext.ModelRequestId"/> across
/// attempts while still distinguishing which attempt produced a given
/// diagnostic or log entry.
/// </remarks>
public sealed record ChatModelRequest
{
    /// <summary>Initializes a new instance of the <see cref="ChatModelRequest"/> record.</summary>
    /// <param name="context">The provider-neutral content of the request.</param>
    /// <param name="attempt">The one-based attempt number for this request's <see cref="ModelRequestId"/>.</param>
    /// <param name="deadline">The instant by which this attempt must complete.</param>
    /// <param name="options">Bounded, provider-specific request options.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attempt"/> is less than one.
    /// </exception>
    public ChatModelRequest(
        ChatRequestContext context,
        int attempt,
        DateTimeOffset deadline,
        ProviderRequestOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(options);

        Context = context;
        Attempt = attempt;
        Deadline = deadline;
        Options = options;
    }

    /// <summary>Gets the provider-neutral content of the request.</summary>
    public ChatRequestContext Context { get; init; }

    /// <summary>Gets the one-based attempt number for this request's <see cref="ChatRequestContext.ModelRequestId"/>.</summary>
    public int Attempt { get; init; }

    /// <summary>Gets the instant by which this attempt must complete.</summary>
    public DateTimeOffset Deadline { get; init; }

    /// <summary>Gets the bounded, provider-specific request options.</summary>
    public ProviderRequestOptions Options { get; init; }
}
