// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates one resolved call's bounded raw arguments against its captured canonical input schema.</summary>
/// <remarks>
/// Validation parses and schema-checks arguments only; it neither authorizes invocation nor invokes the tool.
/// Cancellation propagates; resource exhaustion and invalid arguments are closed validation outcomes rather than
/// exceptions.
/// </remarks>
public interface IToolArgumentValidator
{
    /// <summary>Parses and validates one resolved call's raw arguments.</summary>
    /// <param name="resolvedCall">The resolved call whose raw arguments are validated.</param>
    /// <param name="limits">The bounds applied to parsing and schema validation.</param>
    /// <param name="cancellationToken">Signals cancellation before a closed outcome is returned.</param>
    /// <returns>The closed validated or validation-failed outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resolvedCall"/> is null.</exception>
    public ValueTask<ToolArgumentValidationResult> ValidateAsync(
        ResolvedToolCall resolvedCall,
        ToolSchemaLimits limits,
        CancellationToken cancellationToken = default);
}
