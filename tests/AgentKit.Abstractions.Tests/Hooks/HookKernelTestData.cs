// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

/// <summary>Shared builders and test doubles for the WS2 hook kernel contract types.</summary>
internal static class HookKernelTestData
{
    public static HookPointId Point { get; } = new("test.point");

    public static HookProfileKey Profile { get; } = new("default");

    public static InRunOperationCorrelation Correlation { get; } =
        new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    public static HookDispatchMetadata Dispatch(HookPointId? point = null, DateTimeOffset? timestamp = null) =>
        new(
            point ?? Point,
            new HookDispatchId(Guid.NewGuid()),
            Correlation,
            timestamp ?? DateTimeOffset.UnixEpoch,
            (timestamp ?? DateTimeOffset.UnixEpoch) + TimeSpan.FromSeconds(10));

    public static HookRegistrationDescriptor Registration(HookRegistrationId? id = null, HookPointId? point = null) =>
        new(
            id ?? new HookRegistrationId(Guid.NewGuid()),
            point ?? Point,
            Profile,
            HookOrder.Normal,
            HookLifetime.Singleton,
            HookFailureMode.FailOperation,
            HookReentrancyPolicy.Forbidden,
            [],
            [],
            []);

    public interface ITestHook
    {
        public ValueTask InvokeAsync(TestEventArgs eventArgs, HookInvocationContext invocation, CancellationToken cancellationToken);
    }

    public sealed class TestHookImplementation: ITestHook
    {
        public ValueTask InvokeAsync(TestEventArgs eventArgs, HookInvocationContext invocation, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    public sealed class TestEventArgs(OperationCorrelation correlation, DateTimeOffset timestamp, HookInvocationId invocationId)
        : AgentHookEventArgs(correlation, timestamp, invocationId);

    public sealed class TestValidator: IHookMutationValidator<TestEventArgs>
    {
        public void Validate(TestEventArgs eventArgs)
        {
        }
    }

    public static HookInvoker<ITestHook, TestEventArgs> Invoke { get; } =
        (hook, eventArgs, invocation, cancellationToken) => hook.InvokeAsync(eventArgs, invocation, cancellationToken);

    public sealed class FakeActivationLease: IHookActivationLease
    {
        public int DisposeCount { get; private set; }

        public IHookInvocationTracker InvocationTracker { get; } = new FakeInvocationTracker();

        public ValueTask<HookInstanceResolution<THook>> ResolveAsync<THook>(HookRegistrationId registrationId, CancellationToken cancellationToken)
            where THook : class =>
            ValueTask.FromResult<HookInstanceResolution<THook>>(new HookInstanceUnavailable<THook>("not configured"));

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeInvocationTracker: IHookInvocationTracker
    {
        public HookInvocationTrackingResult TryEnter(HookInvocationAttempt attempt)
        {
            ArgumentNullException.ThrowIfNull(attempt);
            return new HookInvocationTrackingEntered(1);
        }

        public void Leave(HookDispatchId dispatchId)
        {
        }
    }
}
