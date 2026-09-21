// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>A first-party <see cref="ISecurityPolicy"/> that allows every request regardless of operation kind, effect, or resources.</summary>
/// <remarks>
/// <para>
/// <see cref="SecurityAuthority"/> denies by default when no policy allows a request, so registering this policy
/// turns that fail-closed default into "every operation is allowed" for every <see cref="SecurityOperationKind"/>
/// this authority evaluates, including <see cref="SecurityOperationKind.Process"/> and
/// <see cref="SecurityOperationKind.StateMutation"/>/<see cref="SecurityOperationKind.StateRead"/> requests that
/// no other first-party policy covers.
/// </para>
/// <para>
/// This is deliberately a blunt instrument intended for a local, single-tenant application (a demo, an example,
/// or a developer's own machine) that already trusts every operation its own composition can request. It grants
/// no more scope than <see cref="SecurityAuthority"/> itself enforces elsewhere (deadline, revocation, grant
/// lifetime and use count, and captured-context matching all still apply), but it removes per-operation
/// discrimination entirely. A multi-tenant host, or any host that executes untrusted instructions, must not
/// register this policy; compose narrower policies such as <see cref="WorkspaceScopedFileAccessPolicy"/> instead.
/// </para>
/// </remarks>
public sealed class AllowAllSecurityPolicy: ISecurityPolicy
{
    private static readonly SecurityPolicyResult _allowResult = new(
        SecurityPolicyResultKind.Allow,
        "allow-all",
        "This composition trusts every operation its own agent can request.");

    /// <inheritdoc/>
    public ValueTask<SecurityPolicyResult> EvaluateAsync(
        SecurityRequest request,
        SecurityPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_allowResult);
    }
}
