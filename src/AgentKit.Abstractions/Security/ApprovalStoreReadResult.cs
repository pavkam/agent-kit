// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns retained approval request state without fabricating absent evidence.</summary>
public sealed record ApprovalStoreReadResult
{
    /// <summary>Initializes retained approval state.</summary>
    /// <param name="request">The retained request, or null when absent.</param>
    /// <param name="response">Its terminal response, when resolved.</param>
    /// <exception cref="ArgumentException"><paramref name="response"/> is present without <paramref name="request"/> or targets another request.</exception>
    public ApprovalStoreReadResult(ApprovalRequest? request, ApprovalResponse? response)
    {
        if (response is not null)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNotEqual(response.RequestId, request.Id);
        }
        Request = request;
        Response = response;
    }

    /// <summary>Gets the retained request, or null when absent.</summary>
    public ApprovalRequest? Request { get; }
    /// <summary>Gets the terminal response, or null while pending or absent.</summary>
    public ApprovalResponse? Response { get; }
}
