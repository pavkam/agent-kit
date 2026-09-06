// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable, provider-neutral category of one <see cref="ProviderFailure"/>.
/// </summary>
/// <remarks>
/// Providers describe failures using their own status codes and error
/// bodies. Normalizing them into this closed set lets retry, fallback, and
/// diagnostic code make the same decisions regardless of which provider
/// produced the failure, without hard-coding provider-specific status-code
/// comparisons throughout the runtime. The original status code and
/// provider-specific error text are never discarded; they remain available
/// on <see cref="ProviderFailure"/> as diagnostic context.
/// </remarks>
public enum ProviderFailureKind
{
    /// <summary>
    /// The request could not be authenticated, such as a missing, invalid,
    /// or expired credential.
    /// </summary>
    Authentication,

    /// <summary>
    /// The request was authenticated but is not authorized to perform the
    /// requested operation.
    /// </summary>
    Authorization,

    /// <summary>
    /// The provider rejected the request because a rate or quota limit was
    /// exceeded.
    /// </summary>
    Throttling,

    /// <summary>
    /// The request itself was malformed or violated a provider-side
    /// validation rule.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// The provider is temporarily unavailable, such as a transient service
    /// outage or connectivity failure.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The request did not complete before its deadline elapsed.
    /// </summary>
    Timeout,

    /// <summary>
    /// The request was cancelled by the caller before it completed.
    /// </summary>
    Cancellation,

    /// <summary>
    /// The provider's response violated the expected wire protocol, such as
    /// a truncated stream or malformed payload.
    /// </summary>
    ProtocolViolation,

    /// <summary>The failure does not fit any other defined category.</summary>
    Unknown
}
