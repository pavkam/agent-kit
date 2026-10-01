// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the validated call and the complete UTF-8 content a spill stores.</summary>
public sealed record ToolResultSpillRequest
{
    /// <summary>Initializes a spill request.</summary>
    /// <param name="call">The validated call whose result is being externalized; its captured authorization authorizes the artifact effects.</param>
    /// <param name="content">The complete canonical text of the result, in source order, as UTF-8 bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="call"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> is uninitialized or empty.</exception>
    public ToolResultSpillRequest(ValidatedToolCall call, ImmutableArray<byte> content)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentException.ThrowIfDefault(content);
        ArgumentOutOfRangeException.ThrowIfZero(content.Length, nameof(content));
        Call = call;
        Content = content;
    }

    /// <summary>Gets the validated call whose result is being externalized.</summary>
    public ValidatedToolCall Call { get; }

    /// <summary>Gets the complete canonical text of the result as UTF-8 bytes.</summary>
    /// <value>A nonempty owned array.</value>
    public ImmutableArray<byte> Content { get; }
}
