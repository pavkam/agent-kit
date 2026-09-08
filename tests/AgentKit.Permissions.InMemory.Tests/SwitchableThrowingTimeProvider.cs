// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

/// <summary>Provides deterministic time or throws one stable failure so pre-consumption fault handling can be verified.</summary>
internal sealed class SwitchableThrowingTimeProvider(DateTimeOffset timestamp): TimeProvider
{
    private readonly DateTimeOffset _timestamp = timestamp;

    /// <summary>Gets the stable exception instance thrown while clock reads are disabled.</summary>
    /// <value>The same exception instance on every failing read, allowing exact propagation assertions.</value>
    internal InvalidOperationException Failure { get; } = new("clock failure");

    /// <summary>Gets or sets whether <see cref="GetUtcNow"/> throws <see cref="Failure"/>.</summary>
    /// <value><see langword="true"/> until the test enables successful deterministic reads.</value>
    internal bool ThrowOnRead { get; set; } = true;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => ThrowOnRead ? throw Failure : _timestamp;
}
