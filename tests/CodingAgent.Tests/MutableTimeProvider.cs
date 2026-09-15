// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

/// <summary>Provides a manually advanced clock for deadline-sensitive adapter tests.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset now): TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    internal void Advance(TimeSpan duration) => now += duration;
}
