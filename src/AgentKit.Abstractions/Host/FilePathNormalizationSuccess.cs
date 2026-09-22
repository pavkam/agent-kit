// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports successful lexical normalization.</summary>
public sealed record FilePathNormalizationSuccess: FilePathNormalizationResult
{
    /// <summary>Initializes a new instance of the <see cref="FilePathNormalizationSuccess"/> record.</summary>
    /// <param name="path">The normalized relative path.</param>
    public FilePathNormalizationSuccess(NormalizedRelativePath path) => Path = path;

    /// <summary>Gets the normalized relative path.</summary>
    public NormalizedRelativePath Path { get; init; }
}
