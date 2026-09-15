// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Formats projected evidence for one exact source-owned tool descriptor without executing or authorizing it.</summary>
public interface IToolPresentationFormatter
{
    /// <summary>Gets the exact descriptor accepted by this formatter.</summary><value>Immutable source, identity, version, and schema evidence.</value>
    public ToolDescriptor Descriptor { get; }
    /// <summary>Formats one bounded source for its exact descriptor.</summary>
    /// <param name="request">The validated request whose descriptor equals <see cref="Descriptor"/>.</param>
    /// <param name="cancellationToken">Cancels formatting without producing fallback.</param>
    /// <returns>A formatter-authored presentation, or null when its source payload is unsupported or malformed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    public ValueTask<ToolPresentation?> FormatAsync(ToolPresentationRequest request, CancellationToken cancellationToken = default);
}
