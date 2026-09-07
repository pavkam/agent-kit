// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one policy's non-authoritative allow, deny, or abstain contribution.</summary>
public sealed record SecurityPolicyResult
{
    /// <summary>Initializes a policy contribution.</summary>
    /// <param name="kind">The contribution classification.</param>
    /// <param name="code">A stable policy code for allow or denial evidence.</param>
    /// <param name="safeMessage">A non-sensitive explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A non-abstaining result has a blank code or explanation.</exception>
    public SecurityPolicyResult(SecurityPolicyResultKind kind, string? code, string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        if (kind != SecurityPolicyResultKind.Abstain)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        }

        Kind = kind;
        Code = code;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the contribution classification.</summary>
    public SecurityPolicyResultKind Kind { get; init; }
    /// <summary>Gets the stable policy code when the policy did not abstain.</summary>
    public string? Code { get; init; }
    /// <summary>Gets the non-sensitive explanation when the policy did not abstain.</summary>
    public string? SafeMessage { get; init; }
}
