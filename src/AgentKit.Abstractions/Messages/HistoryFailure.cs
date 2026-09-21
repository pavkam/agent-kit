// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A typed, content-safe history-preparation failure.</summary>
public sealed record HistoryFailure
{
    /// <summary>Initializes one history failure.</summary>
    /// <param name="kind">The failure category.</param>
    /// <param name="safeMessage">A content-safe explanation suitable for diagnostics.</param>
    /// <param name="extensions">Forward-compatible diagnostic evidence.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public HistoryFailure(HistoryFailureKind kind, string safeMessage, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(extensions);
        Kind = kind;
        SafeMessage = safeMessage;
        Extensions = extensions;
    }

    /// <summary>Gets the failure category.</summary>
    public HistoryFailureKind Kind { get; }

    /// <summary>Gets the content-safe explanation.</summary>
    public string SafeMessage { get; }

    /// <summary>Gets forward-compatible diagnostic evidence.</summary>
    public ExtensionData Extensions { get; }

    /// <summary>Projects this failure to the context-preparation taxonomy used by <see cref="IContextAssembler"/>.</summary>
    /// <returns>A matching <see cref="ContextPreparationFailure"/>.</returns>
    public ContextPreparationFailure ToContextPreparationFailure() =>
        new(
            Kind switch
            {
                HistoryFailureKind.EmptyHistory => ContextPreparationFailureKind.EmptyHistory,
                HistoryFailureKind.BrokenToolCallCausality => ContextPreparationFailureKind.BrokenToolCallCausality,
                HistoryFailureKind.InvalidRolePartCombination => ContextPreparationFailureKind.InvalidRolePartCombination,
                _ => ContextPreparationFailureKind.Unknown,
            },
            SafeMessage,
            Extensions);
}
