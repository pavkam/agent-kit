// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides a stable denial code and a non-sensitive caller-facing explanation.</summary>
public sealed record SecurityDenial
{
    /// <summary>Initializes a security denial.</summary>
    /// <param name="code">The stable machine-readable denial code.</param>
    /// <param name="safeMessage">The non-sensitive explanation.</param>
    /// <exception cref="ArgumentException">Either value is blank.</exception>
    public SecurityDenial(string code, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Code = code;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the stable denial code.</summary>
    public string Code { get; init; }
    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
