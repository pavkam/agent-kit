// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies a stable portable error category using exact ordinal machine text.</summary>
/// <remarks>
/// Known framework declarations are exposed by <see cref="AgentErrorCodes"/>. Custom and future codes remain valid and
/// preserve their exact text; consumers must not parse a safe message to recover a code.
/// </remarks>
public readonly record struct AgentErrorCode
{
    /// <summary>Creates an error code without case folding or whitespace normalization.</summary>
    /// <param name="value">The nonblank ordinal machine code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or whitespace.</exception>
    public AgentErrorCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the exact machine code supplied by its mapper.</summary>
    /// <value>Nonblank ordinal text, or null only for the CLR default value.</value>
    public string? Value { get; }

    /// <summary>Formats the exact machine code without culture-sensitive conversion.</summary>
    /// <returns>The code text, or empty text for the CLR default value.</returns>
    public override string ToString() => Value ?? string.Empty;
}
