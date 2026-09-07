// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>
/// Exposes the process-wide Microsoft diagnostics sources used by first-party
/// AgentKit components.
/// </summary>
/// <remarks>
/// The sources are observational, thread-safe process-lifetime objects. Hosts
/// subscribe by <see cref="ActivitySourceName"/> and <see cref="MeterName"/>;
/// callers must not dispose the returned instances or use listener state to
/// control AgentKit behavior.
/// </remarks>
public static class AgentKitDiagnostics
{
    /// <summary>Gets the stable name used by AgentKit activities.</summary>
    /// <value>The case-sensitive source name hosts register with tracing providers.</value>
    public const string ActivitySourceName = "AgentKit";

    /// <summary>Gets the stable name used by AgentKit metric instruments.</summary>
    /// <value>The case-sensitive meter name hosts register with metric providers.</value>
    public const string MeterName = "AgentKit";

    /// <summary>Gets the shared activity source for causal AgentKit operations.</summary>
    /// <value>A process-lifetime source that remains valid for the application lifetime.</value>
    public static ActivitySource Activities { get; } = new(ActivitySourceName);

    /// <summary>Gets the shared meter for bounded AgentKit measurements.</summary>
    /// <value>A process-lifetime meter that remains valid for the application lifetime.</value>
    public static Meter Metrics { get; } = new(MeterName);
}
