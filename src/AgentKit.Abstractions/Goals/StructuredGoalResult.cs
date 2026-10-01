// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the typed result an agent reported for a goal.</summary>
/// <remarks>The value is untrusted agent-produced data until the parent validates its shape and evidence. It is content, so it is never logged or used as a metric dimension.</remarks>
public sealed record StructuredGoalResult
{
    /// <summary>Initializes a validated result.</summary>
    /// <param name="summary">The non-blank bounded summary.</param>
    /// <param name="data">The typed result fields.</param>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> or <paramref name="data"/> is null.</exception>
    public StructuredGoalResult(string summary, ExtensionData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(data);
        Summary = summary;
        Data = data;
    }

    /// <summary>Gets the bounded summary.</summary>
    public string Summary { get; }

    /// <summary>Gets the typed result fields.</summary>
    public ExtensionData Data { get; }
}
