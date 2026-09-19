// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures proof that a principal authenticated to a trusted approval channel before responding.</summary>
/// <remarks>This evidence identifies a responder to the channel. It never itself authorizes the operation being approved; the authority still revalidates the resulting response and its binding.</remarks>
public sealed record ApprovalAuthenticationEvidence
{
    /// <summary>Initializes captured channel-authentication evidence.</summary>
    /// <param name="id">The stable evidence identity.</param>
    /// <param name="channelId">The channel that performed authentication.</param>
    /// <param name="requestId">The approval request the responder was answering.</param>
    /// <param name="correlation">The causal operation correlation for the authentication event.</param>
    /// <param name="authenticatedPrincipalId">The principal the channel authenticated.</param>
    /// <param name="method">The authentication method used.</param>
    /// <param name="authenticatedAt">The authentication instant.</param>
    /// <param name="channelBinding">The fingerprint binding this evidence to the exact channel session.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestId"/> is default or <paramref name="method"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="authenticatedPrincipalId"/> or <paramref name="channelBinding"/> is blank.</exception>
    public ApprovalAuthenticationEvidence(
        ApprovalAuthenticationEvidenceId id,
        ApprovalChannelId channelId,
        ApprovalRequestId requestId,
        OperationCorrelation correlation,
        PrincipalId authenticatedPrincipalId,
        ApprovalAuthenticationMethod method,
        DateTimeOffset authenticatedAt,
        ContentHash channelBinding)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId.Value, Guid.Empty, nameof(requestId));
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedPrincipalId.Value, nameof(authenticatedPrincipalId));
        ArgumentOutOfRangeException.ThrowIfUndefined(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelBinding.Value, nameof(channelBinding));
        Id = id;
        ChannelId = channelId;
        RequestId = requestId;
        Correlation = correlation;
        AuthenticatedPrincipalId = authenticatedPrincipalId;
        Method = method;
        AuthenticatedAt = authenticatedAt;
        ChannelBinding = channelBinding;
    }

    /// <summary>Gets the evidence identity.</summary>
    public ApprovalAuthenticationEvidenceId Id { get; }
    /// <summary>Gets the authenticating channel.</summary>
    public ApprovalChannelId ChannelId { get; }
    /// <summary>Gets the approval request being answered.</summary>
    public ApprovalRequestId RequestId { get; }
    /// <summary>Gets the causal operation correlation for the authentication event.</summary>
    public OperationCorrelation Correlation { get; }
    /// <summary>Gets the authenticated principal.</summary>
    public PrincipalId AuthenticatedPrincipalId { get; }
    /// <summary>Gets the authentication method.</summary>
    public ApprovalAuthenticationMethod Method { get; }
    /// <summary>Gets the authentication instant.</summary>
    public DateTimeOffset AuthenticatedAt { get; }
    /// <summary>Gets the exact channel-session binding fingerprint.</summary>
    public ContentHash ChannelBinding { get; }
}
