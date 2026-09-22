// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States how host paths are compared within one file root.</summary>
public enum FilePathComparisonKind
{
    /// <summary>Paths are compared with ordinal, case-sensitive rules.</summary>
    Ordinal,

    /// <summary>Paths are compared with ordinal, case-insensitive rules.</summary>
    OrdinalIgnoreCase,
}
