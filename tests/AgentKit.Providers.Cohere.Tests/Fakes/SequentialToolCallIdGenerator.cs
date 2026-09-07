// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Tests.Fakes;

/// <summary>
/// A deterministic <see cref="IIdentifierGenerator{ToolCallId}"/> test
/// double that produces predictable, sequential identities instead of
/// random ones.
/// </summary>
internal sealed class SequentialToolCallIdGenerator: IIdentifierGenerator<ToolCallId>
{
    private int _next;

    /// <inheritdoc/>
    public ToolCallId Create()
    {
        _next++;
        var bytes = new byte[16];
        BitConverter.GetBytes(_next).CopyTo(bytes, 0);
        return new ToolCallId(new Guid(bytes));
    }
}
