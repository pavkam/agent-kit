// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed validated and rejected outcomes of one tool argument validation attempt.</summary>
/// <remarks>Neither outcome authorizes invocation or invokes the tool.</remarks>
public abstract record ToolArgumentValidationResult
{
    /// <summary>Initializes one of the two supported validation outcomes.</summary>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed validation family.</exception>
    private protected ToolArgumentValidationResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolCallValidated or ToolCallValidationFailed, true, "result");

    /// <summary>Copies the base state of a supported immutable validation outcome.</summary>
    /// <param name="original">The nonnull original result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed validation family.</exception>
    protected ToolArgumentValidationResult(ToolArgumentValidationResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolCallValidated or ToolCallValidationFailed, true, "result");
    }
}
