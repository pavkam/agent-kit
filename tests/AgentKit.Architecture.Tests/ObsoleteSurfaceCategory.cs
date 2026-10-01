// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Architecture.Tests;

/// <summary>Names one kind of superseded-shape evidence the obsolete-surface guard counts.</summary>
internal enum ObsoleteSurfaceCategory
{
    /// <summary>An <c>[Obsolete]</c> attribute application.</summary>
    ObsoleteAttribute,

    /// <summary>An identifier containing <c>Legacy</c> followed by an upper-case letter.</summary>
    LegacyIdentifier,

    /// <summary>A <c>#pragma warning disable</c> directive for <c>CS0612</c> or <c>CS0618</c>.</summary>
    ObsoleteSuppression,

    /// <summary>A compatibility-created or unpinned remark describing a nullable fallback for a superseded caller.</summary>
    CompatibilityText,
}
