// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Context assembly produced a complete, provider-ready request.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record ContextReady: ContextAssemblyResult
{
    /// <summary>Initializes a new instance of the <see cref="ContextReady"/> record.</summary>
    /// <param name="context">The assembled, provider-ready request content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public ContextReady(ChatRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>Gets the assembled, provider-ready request content.</summary>
    public ChatRequestContext Context { get; init; }
}
