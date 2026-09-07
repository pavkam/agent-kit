// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Defines allocation-efficient network events without destinations, headers, or body content.</summary>
internal static partial class NetworkLog
{
    /// <summary>Records one typed terminal network result using only operation identity and bounded state.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="stage">The bounded network stage name.</param>
    /// <param name="networkOperationId">The causal network operation identity.</param>
    /// <param name="outcome">The normalized terminal result kind.</param>
    [LoggerMessage(14000, LogLevel.Debug, "Network stage {Stage} for operation {NetworkOperationId} completed with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger,
        string stage,
        NetworkOperationId networkOperationId,
        string outcome);

    /// <summary>Records an unexpected network exception type without potentially sensitive exception content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="stage">The bounded network stage name.</param>
    /// <param name="networkOperationId">The causal network operation identity.</param>
    /// <param name="errorType">The stable exception type name.</param>
    [LoggerMessage(14001, LogLevel.Error, "Network stage {Stage} for operation {NetworkOperationId} failed with error type {ErrorType}.")]
    internal static partial void Failed(
        ILogger logger,
        string stage,
        NetworkOperationId networkOperationId,
        string errorType);
}
