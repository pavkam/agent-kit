// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The closed outcome of one tool authorization decision: exactly one of
/// <see cref="ToolAuthorizationGranted"/> or <see cref="ToolAuthorizationDenied"/>.
/// </summary>
/// <remarks>
/// <para>
/// This hierarchy is closed to first-party outcomes recognized by
/// <see cref="IToolAuthorizer"/> implementations and their callers;
/// external assemblies cannot derive additional cases. Each instance is
/// immutable and safe to share across threads without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the full allow / deny /
/// require-approval outcome set described by the tools-and-permissions
/// architecture; approval and deferral require an approval broker this
/// package does not yet have, so this reduced authorizer can only ever
/// grant immediately or deny outright.
/// </para>
/// </remarks>
public abstract record ToolAuthorizationDecision
{
    private protected ToolAuthorizationDecision()
    {
    }
}
