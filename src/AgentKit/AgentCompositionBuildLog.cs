// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines content-free structured logs for AgentKit service-provider construction.</summary>
internal static partial class AgentCompositionBuildLog
{
    /// <summary>Writes a successful provider-build outcome.</summary>
    /// <param name="logger">The bootstrap logger supplied directly to the provider factory.</param>
    [LoggerMessage(18004, LogLevel.Debug, "AgentKit service-provider build completed.")]
    internal static partial void Built(ILogger logger);

    /// <summary>Writes a declared-graph or registration-correspondence rejection.</summary>
    /// <param name="logger">The bootstrap logger supplied directly to the provider factory.</param>
    /// <param name="errorType">The safe composition-exception type.</param>
    [LoggerMessage(18005, LogLevel.Warning,
        "AgentKit service-provider build rejected invalid composition metadata with error type {ErrorType}.")]
    internal static partial void Rejected(ILogger logger, string errorType);

    /// <summary>Writes a Microsoft DI provider-construction failure.</summary>
    /// <param name="logger">The bootstrap logger supplied directly to the provider factory.</param>
    /// <param name="errorType">The bounded provider-build error category.</param>
    [LoggerMessage(18006, LogLevel.Error, "AgentKit service-provider build failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string errorType);
}
