// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>Builds typed immutable toolset/source evidence for catalog merge contract tests.</summary>
public static class ToolCatalogMergeTestData
{
    /// <summary>Publishes a deterministic source with explicit descriptor order and version.</summary>
    /// <param name="source">The nonblank source identity.</param><param name="tools">Initialized nonnull source-owned descriptors.</param><param name="version">The nonblank exact source version.</param>
    /// <returns>The validated immutable source publication.</returns>
    public static ToolProviderSnapshot Source(string source, ImmutableArray<ToolDescriptor> tools, string version = "source-1")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new(new ToolSourceId(source), new ToolSourceVersion(version), tools);
    }

    /// <summary>Publishes a toolset with exact policy and explicit aliases.</summary>
    /// <param name="key">The nonblank toolset key.</param><param name="sources">Initialized nonnull selected source publications.</param><param name="aliases">Initialized explicit alias assignments.</param><param name="policy">The nonblank policy family.</param><param name="version">The positive toolset and execution-policy revision.</param>
    /// <returns>The immutable authored toolset publication.</returns>
    public static ToolsetPublication Toolset(string key, ImmutableArray<ToolProviderSnapshot> sources, ImmutableArray<ToolAliasAssignment> aliases, string policy = "standard", long version = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfContainsNull(sources);
        ArgumentException.ThrowIfContainsNull(aliases);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        return new(new ToolsetKey(key), new ToolsetVersion(version), new(new ToolExecutionPolicyKey(policy), new ToolExecutionPolicyVersion(version)),
            [.. sources.Select(static source => new ToolsetSourceSelection(source.SourceId))], aliases);
    }

    /// <summary>Authors one exact alias assignment to a captured descriptor.</summary>
    /// <param name="alias">The nonblank explicit alias.</param><param name="tool">The nonnull captured descriptor.</param>
    /// <returns>The exact alias and canonical identity pair.</returns>
    public static ToolAliasAssignment Alias(string alias, ToolDescriptor tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentNullException.ThrowIfNull(tool);
        return new(new ToolAlias(alias), new ToolIdentity(tool.Id, tool.Version));
    }

    /// <summary>Creates a coherent run request selecting the supplied toolsets in order.</summary>
    /// <param name="toolsets">Initialized nonnull publications to select.</param><param name="principal">The nonblank principal identity used for diagnostic isolation.</param>
    /// <returns>The exact request with deterministic identity, authorization, and configuration evidence.</returns>
    public static ToolDiscoveryRequest Request(ImmutableArray<ToolsetPublication> toolsets, string principal = "principal")
    {
        ArgumentException.ThrowIfContainsNull(toolsets);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        var request = ToolCaptureTestData.Discovery(principal);
        return new(request.AgentId, request.SessionId, request.RunId, request.Identity, request.Authorization, request.AgentDefinitionRevision,
            request.Configuration, [.. toolsets.Select(static toolset => new ToolsetReference(toolset.Key, toolset.ExecutionPolicy.Key))], request.ModelCapabilities);
    }

    /// <summary>Creates a validated contribution with one explicitly authored alias.</summary>
    /// <param name="key">The nonblank toolset key.</param><param name="source">The nonblank source ID.</param><param name="id">The nonblank tool ID.</param><param name="alias">The nonblank alias.</param><param name="policy">The nonblank policy family.</param>
    /// <returns>A typed candidate retaining all originating evidence.</returns>
    public static ToolCatalogCandidate Candidate(string key = "tools", string source = "source.tests", string id = "tool.read", string alias = "read", string policy = "standard")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        var tool = ToolCaptureTestData.Descriptor(id: id, source: source);
        var publication = Source(source, [tool]);
        return new(Toolset(key, [publication], [Alias(alias, tool)], policy), publication, new(tool.Id, tool.Version));
    }
}
