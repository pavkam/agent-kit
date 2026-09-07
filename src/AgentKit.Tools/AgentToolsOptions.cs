// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Configures the built-in tool invocation pipeline registered by <c>AddAgentTools</c>.</summary>
/// <remarks>
/// This is a mutable options type bound through <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>;
/// configure it during composition. <see cref="AllowListToolAuthorizer"/>
/// validates and copies the allow-list when that service is constructed, so
/// later mutations do not alter an already composed authority.
/// </remarks>
public sealed class AgentToolsOptions
{
    /// <summary>
    /// Gets the set of tool identities <see cref="AllowListToolAuthorizer"/>
    /// permits to be invoked.
    /// </summary>
    /// <value>
    /// Empty by default: no tool is authorized until explicitly added,
    /// which is a fail-closed default rather than a permissive one. Entries
    /// must be initialized <see cref="ToolId"/> values; the authorizer rejects
    /// a default value while capturing its immutable snapshot.
    /// </value>
    public HashSet<ToolId> AllowedToolIds { get; } = [];
}
