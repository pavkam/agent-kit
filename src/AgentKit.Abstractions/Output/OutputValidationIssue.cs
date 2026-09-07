// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One diagnostic reported while validating an output candidate.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="SafeMessage"/> may be surfaced to the model as part of a
/// retry's corrective instruction, so it must never include secrets,
/// credentials, or internal diagnostic detail.
/// </remarks>
public sealed record OutputValidationIssue
{
    /// <summary>Initializes a new instance of the <see cref="OutputValidationIssue"/> record.</summary>
    /// <param name="code">A stable, machine-readable issue code.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="path">The JSON pointer path within the candidate this issue applies to, when applicable.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> or <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public OutputValidationIssue(string code, string safeMessage, string? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        Code = code;
        SafeMessage = safeMessage;
        Path = path;
    }

    /// <summary>Gets a stable, machine-readable issue code.</summary>
    public string Code { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>Gets the JSON pointer path within the candidate this issue applies to, when applicable.</summary>
    public string? Path { get; init; }
}
