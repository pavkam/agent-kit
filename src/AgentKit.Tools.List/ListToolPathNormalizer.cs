// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

/// <summary>Lexical path normalizer used when no host package registers one.</summary>
internal sealed class ListToolPathNormalizer: IFilePathNormalizer
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
