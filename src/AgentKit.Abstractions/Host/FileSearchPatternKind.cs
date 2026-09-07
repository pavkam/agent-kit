// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the pinned interpretation applied to a file-search pattern.</summary>
public enum FileSearchPatternKind
{
    /// <summary>Matches the pattern as literal text.</summary>
    Literal,
    /// <summary>Matches the pattern with the .NET non-backtracking regular-expression engine.</summary>
    RegularExpression,
}
