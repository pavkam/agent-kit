// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>Creates typed deterministic source publications and invoker bindings for capture contract tests.</summary>
public static class ToolCaptureTestData
{
    /// <summary>Creates an immutable same-source descriptor with an owned canonical input schema.</summary>
    /// <param name="id">The nonblank canonical identity.</param><param name="version">The nonblank exact tool version.</param><param name="description">The descriptor content, optionally containing a redaction sentinel.</param>
    /// <returns>The immutable test descriptor.</returns>
    public static ToolDescriptor Descriptor(string id = "tool.read", string version = "1", string description = "Read captured data")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        using var document = JsonDocument.Parse("{}");
        return new ToolDescriptor(new ToolId(id), new ToolVersion(version), "read", description,
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            null, new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null), new ToolSourceId("source.tests"), ExtensionData.Empty);
    }

    /// <summary>Captures ordered descriptors under one explicitly selected test source version.</summary>
    /// <param name="tools">The initialized ordered descriptors.</param><param name="version">The nonblank exact source version.</param>
    /// <returns>A validated immutable source publication.</returns>
    public static ToolProviderSnapshot Snapshot(ImmutableArray<ToolDescriptor> tools, string version = "source-1")
    {
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new ToolProviderSnapshot(new ToolSourceId("source.tests"), new ToolSourceVersion(version), tools);
    }

    /// <summary>Creates one exact typed binding without invoking or reading metadata from the invoker.</summary>
    /// <param name="tool">The nonnull captured descriptor.</param><param name="invoker">The nonnull bound instance.</param>
    /// <returns>An immutable single-entry binding map with exact domain comparers.</returns>
    public static ImmutableDictionary<ToolIdentity, IToolInvoker> Bindings(ToolDescriptor tool, IToolInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(invoker);
        return ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty.Add(new ToolIdentity(tool.Id, tool.Version), invoker);
    }
}
