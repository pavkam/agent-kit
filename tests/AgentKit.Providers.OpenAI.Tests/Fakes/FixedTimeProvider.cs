// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI.Tests.Fakes;

/// <summary>
/// A <see cref="TimeProvider"/> test double that always reports one fixed
/// instant, so deadline and credential-expiry logic is deterministic.
/// </summary>
internal sealed class FixedTimeProvider: TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>Initializes a new instance of the <see cref="FixedTimeProvider"/> class.</summary>
    /// <param name="now">The fixed instant every call to <see cref="GetUtcNow"/> returns.</param>
    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;
}
