// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The call is authorized to proceed.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Unlike
/// the full <c>SecurityGrant</c> this stands in for, this outcome carries no
/// bounded scope, audience, expiry, or use count; it is a same-request,
/// immediate decision only and is never cached or reused for a later call.
/// </remarks>
public sealed record ToolAuthorizationGranted: ToolAuthorizationDecision
{
    /// <summary>Initializes a new instance of the <see cref="ToolAuthorizationGranted"/> record.</summary>
    public ToolAuthorizationGranted()
    {
    }
}
