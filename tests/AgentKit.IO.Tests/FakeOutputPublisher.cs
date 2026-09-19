// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>An <see cref="IOutputPublisher"/> test double used only to exercise keyed registration and replacement.</summary>
internal sealed class FakeOutputPublisher: IOutputPublisher
{
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask CompleteAsync<TOutput>(AgentRunFinished<TOutput> result, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
