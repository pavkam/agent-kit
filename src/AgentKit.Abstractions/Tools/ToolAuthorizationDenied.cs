// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The call is denied and must not be invoked.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="SafeMessage"/> never contains credentials or unrestricted
/// diagnostic detail; it is safe to surface to the model or an end user.
/// </remarks>
public sealed record ToolAuthorizationDenied: ToolAuthorizationDecision
{
    /// <summary>Initializes a new instance of the <see cref="ToolAuthorizationDenied"/> record.</summary>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation for the denial.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ToolAuthorizationDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation for the denial.</summary>
    public string SafeMessage { get; init; }
}
