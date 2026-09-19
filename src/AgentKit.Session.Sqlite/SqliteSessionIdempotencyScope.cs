// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Names the idempotency-receipt scopes stored in the two shared receipt tables.</summary>
/// <remarks>
/// Store-scope kinds are keyed by tenant and agent (and, for delete, the established session address) before or
/// without regard to an existing session row. Session-scope kinds are keyed by the addressed session (and, for
/// append, its target branch) and therefore live and die with that session's own rows.
/// </remarks>
internal static class SqliteSessionIdempotencyScope
{
    /// <summary>A successful session-creation receipt, keyed by tenant, agent, and idempotency key.</summary>
    internal const string Create = "create";
    /// <summary>A retained record that a creation retry key's session was later deleted, keyed the same as <see cref="Create"/>.</summary>
    internal const string DeletedCreate = "deleted-create";
    /// <summary>A session-deletion receipt, keyed by tenant, established address, and idempotency key.</summary>
    internal const string Delete = "delete";
    /// <summary>A branch-creation receipt, keyed by session and idempotency key.</summary>
    internal const string Branch = "branch";
    /// <summary>A lane-provisioning receipt, keyed by session and idempotency key.</summary>
    internal const string LaneProvision = "lane-provision";
    /// <summary>An input-admission receipt, keyed by session and idempotency key.</summary>
    internal const string Admission = "admission";
    /// <summary>A run-start (accept) receipt, keyed by session and idempotency key.</summary>
    internal const string RunStart = "run-start";
    /// <summary>A run-release receipt, keyed by session and idempotency key.</summary>
    internal const string RunRelease = "run-release";
    /// <summary>A mid-run input-promotion receipt, keyed by session and idempotency key.</summary>
    internal const string InputPromotion = "input-promotion";
    /// <summary>An append receipt, keyed by session, target branch, and idempotency key.</summary>
    internal const string Append = "append";
}
