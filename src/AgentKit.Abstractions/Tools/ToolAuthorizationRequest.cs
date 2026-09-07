// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete, immutable input to one tool authorization decision.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the canonical
/// <c>SecurityRequest</c> described by the tools-and-permissions
/// architecture, which additionally carries normalized resources, an input
/// fingerprint, effect and destination classification, and
/// policy/configuration versions, and which is evaluated by a general
/// <c>ISecurityAuthority</c> shared across every protected boundary — file,
/// network, process, provider-egress, memory, session, MCP, and
/// delegation, not only tools. Until that authority exists, tool
/// authorization is decided from exactly the fields on this type by an
/// <see cref="IToolAuthorizer"/> scoped to tools alone.
/// </para>
/// </remarks>
public sealed record ToolAuthorizationRequest
{
    /// <summary>Initializes a new instance of the <see cref="ToolAuthorizationRequest"/> record.</summary>
    /// <param name="context">The execution context for the call being authorized.</param>
    /// <param name="descriptor">The descriptor of the tool being called.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> or <paramref name="descriptor"/> is null.
    /// </exception>
    public ToolAuthorizationRequest(ToolExecutionContext context, ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        Context = context;
        Descriptor = descriptor;
    }

    /// <summary>Gets the execution context for the call being authorized.</summary>
    public ToolExecutionContext Context { get; init; }

    /// <summary>Gets the descriptor of the tool being called.</summary>
    public ToolDescriptor Descriptor { get; init; }
}
