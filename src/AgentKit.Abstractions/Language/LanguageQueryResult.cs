// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one bounded immutable language-service snapshot without flattening empty and failed outcomes.</summary>
public sealed record LanguageQueryResult
{
    /// <summary>Initializes one terminal language query result.</summary>
    /// <param name="status">The terminal outcome.</param>
    /// <param name="kind">The operation this result answers.</param>
    /// <param name="hoverText">Bounded untrusted hover text when returned.</param>
    /// <param name="locations">Bounded definition, implementation, or reference locations.</param>
    /// <param name="symbols">Bounded document or workspace symbols.</param>
    /// <param name="diagnostics">Bounded diagnostics.</param>
    /// <param name="complete">Whether no provider results were omitted by the requested bound.</param>
    /// <param name="safeMessage">A safe failure or staleness explanation.</param>
    /// <exception cref="ArgumentException">An immutable array is default or failure/result shapes are inconsistent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined.</exception>
    public LanguageQueryResult(
        LanguageQueryStatus status,
        LanguageQueryKind kind,
        string? hoverText,
        ImmutableArray<LanguageLocation> locations,
        ImmutableArray<LanguageSymbol> symbols,
        ImmutableArray<LanguageDiagnostic> diagnostics,
        bool complete,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfContainsNull(locations);
        ArgumentException.ThrowIfContainsNull(symbols);
        ArgumentException.ThrowIfContainsNull(diagnostics);
        if (status == LanguageQueryStatus.Success == !string.IsNullOrWhiteSpace(safeMessage))
        {
            throw new ArgumentException("Successful results cannot have a failure message; failed results require one.", nameof(safeMessage));
        }

        Status = status;
        Kind = kind;
        HoverText = hoverText;
        Locations = locations;
        Symbols = symbols;
        Diagnostics = diagnostics;
        Complete = complete;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal outcome.</summary>
    public LanguageQueryStatus Status { get; }
    /// <summary>Gets the operation this result answers.</summary>
    public LanguageQueryKind Kind { get; }
    /// <summary>Gets bounded untrusted hover text when returned.</summary>
    public string? HoverText { get; }
    /// <summary>Gets bounded relationship locations.</summary>
    public ImmutableArray<LanguageLocation> Locations { get; }
    /// <summary>Gets bounded document or workspace symbols.</summary>
    public ImmutableArray<LanguageSymbol> Symbols { get; }
    /// <summary>Gets bounded diagnostics.</summary>
    public ImmutableArray<LanguageDiagnostic> Diagnostics { get; }
    /// <summary>Gets whether the requested result bound omitted no provider results.</summary>
    public bool Complete { get; }
    /// <summary>Gets a safe failure or staleness explanation.</summary>
    public string? SafeMessage { get; }
}
