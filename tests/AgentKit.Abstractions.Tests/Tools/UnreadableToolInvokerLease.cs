// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

internal sealed class UnreadableToolInvokerLease: IToolInvokerLease
{
    public ToolDescriptor Tool => throw new InvalidOperationException("Wrapping a lease must not read its metadata.");
    public ToolSourceVersion SourceVersion => throw new InvalidOperationException("Wrapping a lease must not read its metadata.");
    public IToolInvoker Invoker => throw new InvalidOperationException("Wrapping a lease must not read its invoker.");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public override bool Equals(object? obj) => throw new InvalidOperationException("An acquired result must compare lease identity without invoking its implementation.");
    public override int GetHashCode() => throw new InvalidOperationException("An acquired result must hash lease identity without invoking its implementation.");
}
