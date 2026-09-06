// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One incremental raw JSON fragment of a structured-output result.
/// </summary>
public sealed record StructuredDataContentDelta: ContentDelta
{
    /// <summary>Initializes a new instance of the <see cref="StructuredDataContentDelta"/> record.</summary>
    /// <param name="jsonFragment">The incremental raw JSON text fragment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jsonFragment"/> is null.</exception>
    public StructuredDataContentDelta(string jsonFragment)
    {
        ArgumentNullException.ThrowIfNull(jsonFragment);
        JsonFragment = jsonFragment;
    }

    /// <summary>Gets the incremental raw JSON text fragment.</summary>
    public string JsonFragment { get; init; }
}
