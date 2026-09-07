// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one portable read-only language-intelligence operation.</summary>
public enum LanguageQueryKind
{
    /// <summary>Returns diagnostics for one document snapshot.</summary>
    Diagnostics,
    /// <summary>Returns bounded hover information at one position.</summary>
    Hover,
    /// <summary>Returns definitions for the symbol at one position.</summary>
    Definitions,
    /// <summary>Returns implementations for the symbol at one position.</summary>
    Implementations,
    /// <summary>Returns references for the symbol at one position.</summary>
    References,
    /// <summary>Returns symbols declared by one document.</summary>
    DocumentSymbols,
    /// <summary>Searches symbols across the captured workspace.</summary>
    WorkspaceSymbols,
}
