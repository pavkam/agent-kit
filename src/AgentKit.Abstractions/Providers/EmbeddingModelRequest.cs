// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request for one embedding model attempt.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="Attempt"/> lets a same-model retry
/// reuse the same <see cref="EmbeddingRequestContext.RequestId"/> across
/// attempts while still distinguishing which attempt produced a given
/// diagnostic or log entry.
/// </remarks>
public sealed record EmbeddingModelRequest
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingModelRequest"/> record.</summary>
    /// <param name="context">The provider-neutral content of the request.</param>
    /// <param name="attempt">The one-based attempt number for this request's <see cref="EmbeddingRequestContext.RequestId"/>.</param>
    /// <param name="deadline">The instant by which this attempt must complete.</param>
    /// <param name="options">Bounded, provider-specific request options.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attempt"/> is less than one.
    /// </exception>
    public EmbeddingModelRequest(
        EmbeddingRequestContext context,
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
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public EmbeddingRequestContext Context
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the one-based attempt number for this request's <see cref="EmbeddingRequestContext.RequestId"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value assigned during initialization or non-destructive mutation is less than one.
    /// </exception>
    public int Attempt
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    }

    /// <summary>Gets the instant by which this attempt must complete.</summary>
    public DateTimeOffset Deadline { get; init; }

    /// <summary>Gets the bounded, provider-specific request options.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ProviderRequestOptions Options
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
