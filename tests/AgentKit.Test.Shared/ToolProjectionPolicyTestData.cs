// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Supplies explicit deterministic projection-policy revisions for contract and implementation tests.</summary>
public static class ToolProjectionPolicyTestData
{
    /// <summary>Creates an independently owned policy value with the requested test evidence.</summary>
    /// <param name="key">The test policy key.</param><param name="version">The positive revision.</param>
    /// <param name="maximumBytes">The positive byte bound.</param><param name="maximumParts">The positive part bound.</param>
    /// <param name="transformations">The permitted test transformations.</param><param name="extensions">Optional immutable extension evidence.</param>
    /// <returns>A validated immutable policy snapshot.</returns>
    public static ToolResultProjectionPolicySnapshot Snapshot(
        string key = "projection.history",
        long version = 1,
        long maximumBytes = 1024,
        int maximumParts = 4,
        ToolResultProjectionTransformations transformations = ToolResultProjectionTransformations.Truncation,
        ExtensionData? extensions = null) => new(
            new(new(key), new(version)), new(maximumBytes, maximumParts), transformations, extensions ?? ExtensionData.Empty);
}
