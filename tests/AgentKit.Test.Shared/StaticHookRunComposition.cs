// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>Builds static hook catalog and factory pairs for loop and integration tests.</summary>
public static class StaticHookRunComposition
{
    /// <summary>The default profile key used by static captures.</summary>
    public static HookProfileKey DefaultProfileKey { get; } = new("default");

    /// <summary>Creates one catalog and factory from hook instances registered with default test descriptors.</summary>
    /// <param name="runStartedHooks">Optional run-started observers.</param>
    /// <param name="beforeModelRequestHooks">Optional before-model-request mutators.</param>
    /// <param name="beforeToolInvocationHooks">Optional before-tool-invocation short-circuit hooks.</param>
    /// <returns>A catalog and factory that expose the supplied hooks through the kernel dispatch path.</returns>
    public static (IHookCatalog Catalog, IHookInstanceFactory Factory) Create(
        IEnumerable<IRunStartedHook>? runStartedHooks = null,
        IEnumerable<IBeforeModelRequestHook>? beforeModelRequestHooks = null,
        IEnumerable<IBeforeToolInvocationHook>? beforeToolInvocationHooks = null)
    {
        var descriptors = ImmutableArray.CreateBuilder<HookRegistrationDescriptor>();
        var instances = new Dictionary<HookRegistrationId, object>();

        Add(descriptors, instances, runStartedHooks, AgentHookPointDefinitions.RunStartedRegistration);
        Add(descriptors, instances, beforeModelRequestHooks, AgentHookPointDefinitions.BeforeModelRequestRegistration);
        Add(descriptors, instances, beforeToolInvocationHooks, AgentHookPointDefinitions.BeforeToolInvocationRegistration);

        var snapshot = new HookCatalogSnapshot(
            DefaultProfileKey,
            new HookCatalogVersion("test-static"),
            descriptors.ToImmutable());
        return (new StaticHookCatalog(snapshot), new StaticHookInstanceFactory(instances));
    }

    private static void Add<THook>(
        ImmutableArray<HookRegistrationDescriptor>.Builder descriptors,
        Dictionary<HookRegistrationId, object> instances,
        IEnumerable<THook>? hooks,
        HookPointDefinitionRegistration pointRegistration)
        where THook : class
    {
        if (hooks is null)
        {
            return;
        }

        foreach (var hook in hooks)
        {
            ArgumentNullException.ThrowIfNull(hook);
            var authorId = new HookId(hook.GetType().Name);
            var descriptor = HookRegistrationDescriptors.ForPoint(authorId, pointRegistration);
            descriptors.Add(descriptor);
            instances[descriptor.Id] = hook;
        }
    }
}
