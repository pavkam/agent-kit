// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

using Microsoft.Extensions.Logging;

/// <summary>Captures one structured test log without relying on formatted text for semantic assertions.</summary>
/// <param name="Category">The emitting logger category.</param><param name="Level">The event severity.</param>
/// <param name="EventId">The stable event identity.</param><param name="State">The captured structured fields.</param>
/// <param name="Message">The formatted message for content-exclusion assertions.</param>
public sealed record RecordingLogEntry(string Category, LogLevel Level, EventId EventId, ImmutableDictionary<string, object?> State, string Message);
