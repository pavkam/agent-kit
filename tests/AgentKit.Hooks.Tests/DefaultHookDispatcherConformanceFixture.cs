// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Composes <see cref="DefaultHookDispatcher"/> through <see cref="ServiceExtensions.AddAgentHooks"/> for conformance.</summary>
public sealed class DefaultHookDispatcherConformanceFixture: IHookDispatcherConformanceFixture
{
    private readonly ServiceProvider _provider;

    public DefaultHookDispatcherConformanceFixture()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentHooks();
        RegisterKernelPoint(services);
        _provider = services.BuildServiceProvider();
        Dispatcher = _provider.GetRequiredService<IHookDispatcher>();
    }

    public IHookDispatcher Dispatcher { get; }

    public async ValueTask<IReadOnlyList<HookRegistrationId>> DispatchOrderedMutatingHooksAsync(CancellationToken cancellationToken)
    {
        var args = CreateMutatingArgs();
        await DispatchMutatingAsync(args, cancellationToken).ConfigureAwait(false);
        return args.InvocationOrder;
    }

    public async ValueTask<IReadOnlyList<HookInvocationId>> DispatchAndCollectInvocationIdsAsync(CancellationToken cancellationToken)
    {
        var args = CreateMutatingArgs();
        await DispatchMutatingAsync(args, cancellationToken).ConfigureAwait(false);
        return args.InvocationIds;
    }

    public async ValueTask AssertPointMismatchFailsBeforeDispatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = await CreateMutatingScopeAsync(cancellationToken).ConfigureAwait(false);
        var dispatch = CreateMutatingDispatch();
        var context = scope.CreateDispatch(dispatch);
        var args = CreateMutatingArgs();
        var mismatchedArgs = new KernelConformanceEventArgs(
            new HookDispatchMetadata(
                BuiltInAgentHookPointDefinitions.RunStarted.Id,
                dispatch.DispatchId,
                dispatch.Correlation,
                dispatch.Timestamp,
                dispatch.Deadline));

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await Dispatcher.DispatchAsync(KernelConformanceHookPoint.Definition, context, mismatchedArgs, cancellationToken: cancellationToken));

        exception.ParamName.ShouldBe("point");
        args.InvocationOrder.ShouldBeEmpty();
    }

    public async ValueTask<string?> DispatchIsolatedObserverAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentHooks();
        RegisterObservingPoint(services);
        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IHookDispatcher>();
        var catalog = await provider.GetRequiredService<IHookCatalog>()
            .CaptureAsync(new HookCatalogRequest(HookProfileOptions.DefaultProfileKey), cancellationToken)
            .ConfigureAwait(false);
        await using var lease = await provider.GetRequiredService<IHookInstanceFactory>()
            .CreateAsync(catalog, cancellationToken)
            .ConfigureAwait(false);
        await using var scope = new HookActivationScope(catalog, lease);
        var timestamp = DateTimeOffset.UtcNow;
        var dispatch = new HookDispatchMetadata(
            KernelConformanceHookPoint.ObservingDefinition.Id,
            new HookDispatchId(Guid.NewGuid()),
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null),
            timestamp,
            timestamp + TimeSpan.FromMinutes(1));
        var context = scope.CreateDispatch(dispatch);
        var args = new KernelObservingEventArgs(dispatch) { Payload = "baseline" };
        await dispatcher.DispatchAsync(KernelConformanceHookPoint.ObservingDefinition, context, args, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return args.Payload;
    }

    private async ValueTask DispatchMutatingAsync(KernelConformanceEventArgs args, CancellationToken cancellationToken)
    {
        await using var scope = await CreateMutatingScopeAsync(cancellationToken).ConfigureAwait(false);
        var context = scope.CreateDispatch(CreateMutatingDispatch());
        await Dispatcher.DispatchAsync(KernelConformanceHookPoint.Definition, context, args, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<HookActivationScope> CreateMutatingScopeAsync(CancellationToken cancellationToken)
    {
        var catalog = await _provider.GetRequiredService<IHookCatalog>()
            .CaptureAsync(new HookCatalogRequest(HookProfileOptions.DefaultProfileKey), cancellationToken)
            .ConfigureAwait(false);
        var lease = await _provider.GetRequiredService<IHookInstanceFactory>()
            .CreateAsync(catalog, cancellationToken)
            .ConfigureAwait(false);
        return new HookActivationScope(catalog, lease);
    }

    private static HookDispatchMetadata CreateMutatingDispatch()
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new HookDispatchMetadata(
            KernelConformanceHookPoint.Id,
            new HookDispatchId(Guid.NewGuid()),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            timestamp,
            timestamp + TimeSpan.FromMinutes(1));
    }

    private static KernelConformanceEventArgs CreateMutatingArgs() =>
        new(CreateMutatingDispatch());

    private static void RegisterKernelPoint(IServiceCollection services)
    {
        _ = services.AddSingleton(KernelConformanceHookPoint.Definition);
        _ = services.AddSingleton<IReadOnlyList<HookPointDefinitionRegistration>>(static _ =>
        [
            BuiltInAgentHookPointDefinitions.RunStartedRegistration,
            BuiltInAgentHookPointDefinitions.BeforeModelRequestRegistration,
            BuiltInAgentHookPointDefinitions.BeforeToolInvocationRegistration,
            KernelConformanceHookPoint.Registration,
        ]);
        RegisterHook<IKernelConformanceHook, KernelFirstHook>(services, KernelConformanceHookPoint.Registration, new HookId("conformance.first"));
        RegisterHook<IKernelConformanceHook, KernelSecondHook>(services, KernelConformanceHookPoint.Registration, new HookId("conformance.second"));
        RegisterHook<IKernelConformanceHook, KernelThirdHook>(services, KernelConformanceHookPoint.Registration, new HookId("conformance.third"));
    }

    private static void RegisterObservingPoint(IServiceCollection services)
    {
        _ = services.AddSingleton(KernelConformanceHookPoint.ObservingDefinition);
        _ = services.AddSingleton<IReadOnlyList<HookPointDefinitionRegistration>>(static _ =>
        [
            BuiltInAgentHookPointDefinitions.RunStartedRegistration,
            BuiltInAgentHookPointDefinitions.BeforeModelRequestRegistration,
            BuiltInAgentHookPointDefinitions.BeforeToolInvocationRegistration,
            KernelConformanceHookPoint.ObservingRegistration,
        ]);
        RegisterHook<IKernelObservingHook, ObservingFailHook>(
            services,
            KernelConformanceHookPoint.ObservingRegistration,
            new HookId("conformance.observing.fail"));
        RegisterHook<IKernelObservingHook, ObservingRecordHook>(
            services,
            KernelConformanceHookPoint.ObservingRegistration,
            new HookId("conformance.observing.record"));
    }

    private static void RegisterHook<THook, TImplementation>(
        IServiceCollection services,
        HookPointDefinitionRegistration pointRegistration,
        HookId authorId)
        where THook : class
        where TImplementation : class, THook
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<THook, TImplementation>());
        var descriptor = HookRegistrationDescriptors.ForPoint(authorId, pointRegistration);
        var binding = new HookRegistrationBinding(descriptor, typeof(TImplementation), typeof(THook));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHookRegistrationBindingContributor, HookRegistrationBindingContributor<TImplementation>>(
            _ => new HookRegistrationBindingContributor<TImplementation>(binding)));
    }

    private sealed class KernelFirstHook: IKernelConformanceHook
    {
        public ValueTask InvokeAsync(KernelConformanceEventArgs args, HookInvocationContext context, CancellationToken cancellationToken)
        {
            args.InvocationOrder.Add(context.RegistrationId);
            args.InvocationIds.Add(context.InvocationId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class KernelSecondHook: IKernelConformanceHook
    {
        public ValueTask InvokeAsync(KernelConformanceEventArgs args, HookInvocationContext context, CancellationToken cancellationToken)
        {
            args.InvocationOrder.Add(context.RegistrationId);
            args.InvocationIds.Add(context.InvocationId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class KernelThirdHook: IKernelConformanceHook
    {
        public ValueTask InvokeAsync(KernelConformanceEventArgs args, HookInvocationContext context, CancellationToken cancellationToken)
        {
            args.InvocationOrder.Add(context.RegistrationId);
            args.InvocationIds.Add(context.InvocationId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ObservingFailHook: IKernelObservingHook
    {
        public ValueTask InvokeAsync(KernelObservingEventArgs args, HookInvocationContext context, CancellationToken cancellationToken)
        {
            args.Payload = "mutated";
            throw new InvalidOperationException("observer failed");
        }
    }

    private sealed class ObservingRecordHook: IKernelObservingHook
    {
        public ValueTask InvokeAsync(KernelObservingEventArgs args, HookInvocationContext context, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
