// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class HookProfileCatalogTests
{
    [Fact]
    public async Task CaptureAsync_WhenProfileFilterExcludesRegistration_OmitsHookFromSnapshot()
    {
        var registry = new HookProfileRegistry();
        var profileKey = new HookProfileKey("filtered");
        var keepId = HookRegistrationIds.FromAuthorHookId(new HookId("keep-hook"));
        var dropId = HookRegistrationIds.FromAuthorHookId(new HookId("drop-hook"));
        registry.Configure(
            profileKey,
            options => options.RegistrationFilter = descriptor => descriptor.Id.Equals(keepId),
            replace: false);

        var included = CreateDescriptor(profileKey, keepId);
        var excluded = CreateDescriptor(profileKey, dropId);
        var bindings = new HookRegistrationBindingRegistry();
        bindings.Add(new HookRegistrationBinding(included, typeof(FilteredRunStartedHookA), typeof(IRunStartedHook)));
        bindings.Add(new HookRegistrationBinding(excluded, typeof(FilteredRunStartedHookB), typeof(IRunStartedHook)));

        var source = new HookRegistrationBindingSource(
            bindings,
            registry,
            [BuiltInAgentHookPointDefinitions.RunStartedRegistration]);
        var catalog = new HookRegistrationCatalog([source], new HookOrderResolver(), [BuiltInAgentHookPointDefinitions.RunStartedRegistration]);

        var snapshot = await catalog.CaptureAsync(new HookCatalogRequest(profileKey), TestContext.Current.CancellationToken);

        snapshot.Registrations.Length.ShouldBe(1);
        snapshot.Registrations[0].Id.ShouldBe(included.Id);
    }

    private static HookRegistrationDescriptor CreateDescriptor(HookProfileKey profileKey, HookRegistrationId id) =>
        new(
            id,
            AgentHookPoints.RunStarted,
            profileKey,
            HookOrder.Normal,
            HookLifetime.Singleton,
            HookFailureMode.IsolateAndDiagnose,
            HookReentrancyPolicy.Forbidden,
            before: default,
            after: default,
            dependsOn: default);

    private sealed class FilteredRunStartedHookA: IRunStartedHook
    {
        public ValueTask InvokeAsync(RunStartedEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class FilteredRunStartedHookB: IRunStartedHook
    {
        public ValueTask InvokeAsync(RunStartedEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
