// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Portable usage evidence for one semantic operation response.</summary>
public sealed record SemanticOperationUsage
{
    /// <summary>Gets usage that was not reported by the provider.</summary>
    public static SemanticOperationUsage NotReported { get; } = new(null, null, ExtensionData.Empty);

    /// <summary>Initializes usage evidence.</summary>
    /// <param name="inputTokens">Reported input tokens, when known.</param>
    /// <param name="outputTokens">Reported output tokens, when known.</param>
    /// <param name="extensions">Provider-specific usage data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public SemanticOperationUsage(long? inputTokens, long? outputTokens, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        Extensions = extensions;
    }

    /// <summary>Gets reported input tokens, when known.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Gets reported output tokens, when known.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Gets provider-specific usage data.</summary>
    public ExtensionData Extensions { get; init; }
}
