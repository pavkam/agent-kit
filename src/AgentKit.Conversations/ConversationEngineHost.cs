// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Binds the singleton conversation session when <see cref="AgentEngine.OpenSessionAsync"/> runs.</summary>
internal sealed class ConversationEngineHost(
    IConversationSession session,
    IOptions<ConversationSessionOptions> options): IConversationEngineHost
{
    private readonly IConversationSession _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly ConversationSessionOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public async ValueTask<AgentConversationOpenResult> OpenAsync(
        AgentConversationOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfEqual(request.AgentId, default);
        if (request.AgentId != _options.AgentId)
        {
            return new AgentConversationOpenRejected(
                request.AgentId,
                "The composed conversation session drives a different agent than the one requested.");
        }

        if (!ReferenceEquals(request.Identity, _options.Identity) && !request.Identity.Equals(_options.Identity))
        {
            return new AgentConversationOpenRejected(
                request.AgentId,
                "The composed conversation session was configured for a different identity.");
        }

        if (request.SessionId is not { } sessionId)
        {
            if (_session.SessionId is not { } current || _session.BranchId is not { } branch)
            {
                return new AgentConversationOpenRejected(
                    request.AgentId,
                    "No session is bound yet. Send a message or call OpenAsync on the conversation session first.");
            }

            return new AgentConversationOpened(request.AgentId, current, branch);
        }

        var open = await _session.OpenAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return open switch
        {
            ConversationSessionOpened opened => new AgentConversationOpened(request.AgentId, opened.SessionId, opened.BranchId),
            ConversationSessionOpenRejected rejected => new AgentConversationOpenRejected(request.AgentId, rejected.SafeMessage),
            _ => new AgentConversationOpenRejected(request.AgentId, "The conversation session returned an unsupported open outcome."),
        };
    }
}
