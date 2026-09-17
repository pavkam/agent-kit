// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>The outcome of validating one user-entered read-only toolchain folder.</summary>
/// <remarks>
/// Exactly one of <see cref="Path"/> and <see cref="Error"/> is set. An accepted outcome carries the
/// normalized absolute path that the sandbox will expose; a rejected outcome carries a short,
/// user-facing reason suitable for an inline validation line.
/// </remarks>
internal readonly record struct ToolchainRootValidation
{
    private ToolchainRootValidation(string? path, string? error)
    {
        Path = path;
        Error = error;
    }

    /// <summary>Gets the normalized absolute folder path when the input was accepted; otherwise null.</summary>
    public string? Path { get; }

    /// <summary>Gets the user-facing rejection reason when the input was rejected; otherwise null.</summary>
    public string? Error { get; }

    /// <summary>Gets whether the input was accepted and <see cref="Path"/> is set.</summary>
    public bool IsAccepted => Path is not null;

    /// <summary>Creates an accepted outcome.</summary>
    /// <param name="path">The non-blank normalized absolute path.</param>
    /// <returns>An outcome whose <see cref="Path"/> is <paramref name="path"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or whitespace.</exception>
    public static ToolchainRootValidation Accepted(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ToolchainRootValidation(path, null);
    }

    /// <summary>Creates a rejected outcome.</summary>
    /// <param name="error">The non-blank user-facing reason.</param>
    /// <returns>An outcome whose <see cref="Error"/> is <paramref name="error"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="error"/> is null or whitespace.</exception>
    public static ToolchainRootValidation Rejected(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new ToolchainRootValidation(null, error);
    }
}
