// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Records bounded host file-system outcomes without paths, payloads, or security identities.</summary>
internal static class FileSystemMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.FileSystemOperationCount);

    /// <summary>Increments one terminal file-system operation using only bounded operation and outcome dimensions.</summary>
    /// <param name="operation">The normalized operation name.</param>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void Record(string operation, string outcome) =>
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.FileSystemOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
