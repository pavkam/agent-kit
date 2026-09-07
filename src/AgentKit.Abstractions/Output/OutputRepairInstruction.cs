// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounded, safe guidance for a model retry after a failed output validation attempt.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller corrective-input
/// shape described by the structured-output architecture, which composes a
/// corrective model input that also preserves the original causal response
/// through session and message contracts. Until the caller (an agent loop)
/// wires this instruction into a committed message itself, this type
/// carries only the bounded, safe guidance text; it never widens the
/// declared output contract or grants any new authority.
/// </para>
/// </remarks>
public sealed record OutputRepairInstruction
{
    /// <summary>Initializes a new instance of the <see cref="OutputRepairInstruction"/> record.</summary>
    /// <param name="safeMessage">A bounded, safe description of what the retry must correct.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public OutputRepairInstruction(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a bounded, safe description of what the retry must correct.</summary>
    public string SafeMessage { get; init; }
}
