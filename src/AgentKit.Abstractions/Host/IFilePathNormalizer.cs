// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs deterministic lexical path normalization without host inspection.</summary>
public interface IFilePathNormalizer
{
    /// <summary>Normalizes one relative path under the supplied policy.</summary>
    /// <param name="input">The caller-supplied path text.</param>
    /// <param name="policy">The lexical policy to apply.</param>
    /// <returns>A closed normalization outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public FilePathNormalizationResult Normalize(FilePathInput input, FilePathPolicy policy);
}
