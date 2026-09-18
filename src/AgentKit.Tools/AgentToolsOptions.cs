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

    /// <summary>
    /// Gets or sets whether <see cref="AllowListToolAuthorizer"/> grants every
    /// tool call regardless of <see cref="AllowedToolIds"/>.
    /// </summary>
    /// <value>
    /// <see langword="false"/> by default, preserving the fail-closed
    /// allow-list behavior. Setting this to <see langword="true"/> is a
    /// deliberate, explicit opt-out of per-tool authorization intended for a
    /// single-tenant application that already trusts every tool it registers;
    /// it does not affect authorization performed by <see cref="ISecurityPolicy"/>
    /// evaluations that a tool's own effecting boundary still enforces.
    /// </value>
    public bool AllowAllRegisteredTools { get; set; }

    /// <summary>
    /// Gets or sets the bounds <see cref="DefaultToolInvoker"/> uses to compile each resolved tool's declared
    /// <see cref="ToolDescriptor.InputSchema"/> and to validate one call's arguments against it.
    /// </summary>
    /// <value>
    /// 256 KiB of raw UTF-8 JSON, a maximum depth of 64, at most 10,000 total JSON values, and a work budget of
    /// 100,000 deterministic units by default - generous bounds for a single tool call's arguments while still
    /// bounding the local work a hostile or malformed schema or argument payload can force. A compiled schema is
    /// cached per exact tool identity and version, so these limits bound one compilation per distinct descriptor
    /// plus one validation per call, not a cost repeated on every call to the same tool.
    /// </value>
    public ToolSchemaLimits ArgumentValidationLimits { get; set; } = new(
        maximumUtf8Bytes: 262_144, maximumDepth: 64, maximumNodes: 10_000, maximumWork: 100_000);
}
