// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;

/// <summary>Returns a fixed resolution result regardless of the request presented.</summary>
internal sealed class FakeProcessIntentResolver(Func<ProcessResolveRequest, ProcessResolutionResult> resolve): IProcessIntentResolver
{
    public ValueTask<ProcessResolutionResult> ResolveAsync(ProcessResolveRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(resolve(request));
}

/// <summary>Throws from resolution to exercise the generic observability failure path.</summary>
internal sealed class ThrowingProcessIntentResolver(Exception exception): IProcessIntentResolver
{
    public ValueTask<ProcessResolutionResult> ResolveAsync(ProcessResolveRequest request, CancellationToken cancellationToken = default) =>
        throw exception;
}
