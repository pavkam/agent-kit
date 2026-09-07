// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The provider-neutral, ready-to-translate content of one embedding
/// request: the selected model and the request to embed.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. This mirrors <see cref="LlmRequestContext"/>'s
/// role for conversational requests, split from
/// <see cref="EmbeddingModelRequest"/> so a same-model retry can reuse the
/// same <see cref="RequestId"/> across attempts.
/// </remarks>
public sealed record EmbeddingRequestContext
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingRequestContext"/> record.</summary>
    /// <param name="requestId">
    /// The identity of this request, preserved across same-model retry
    /// attempts.
    /// </param>
    /// <param name="model">The selected embedding model descriptor.</param>
    /// <param name="request">The inputs and portable options to embed.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/> or <paramref name="request"/> is null.
    /// </exception>
    public EmbeddingRequestContext(
        EmbeddingRequestId requestId,
        EmbeddingModelDescriptor model,
        EmbeddingRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        RequestId = requestId;
        Model = model;
        Request = request;
    }

    /// <summary>
    /// Gets the identity of this request, preserved across same-model
    /// retry attempts.
    /// </summary>
    public EmbeddingRequestId RequestId { get; init; }

    /// <summary>Gets the selected embedding model descriptor.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public EmbeddingModelDescriptor Model
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the inputs and portable options to embed.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public EmbeddingRequest Request
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
