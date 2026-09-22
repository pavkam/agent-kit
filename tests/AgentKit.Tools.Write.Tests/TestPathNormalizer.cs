// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

/// <summary>Lexical path normalizer matching production rules for tool tests.</summary>
internal sealed class TestPathNormalizer: IFilePathNormalizer
{
    /// <inheritdoc/>
    public FilePathNormalizationResult Normalize(FilePathInput input, FilePathPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(input);
        _ = policy;
        try
        {
            return new FilePathNormalizationSuccess(new NormalizedRelativePath(input.RelativePath));
        }
        catch (ArgumentException exception)
        {
            return new FilePathNormalizationFailed(exception.Message);
        }
    }
}
