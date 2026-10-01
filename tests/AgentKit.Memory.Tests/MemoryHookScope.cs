// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Opens a captured hook scope over explicit hooks so memory hook dispatch runs through the real hook kernel.</summary>
internal static class MemoryHookScope
{
    /// <summary>Opens a scope over <paramref name="hooks"/> in the order given as catalog registrations.</summary>
    /// <param name="hooks">The registrations and their instances.</param>
    /// <returns>The open activation scope; the caller disposes it.</returns>
    internal static async ValueTask<HookActivationScope> OpenAsync(params (HookRegistrationDescriptor Descriptor, object Hook)[] hooks)
    {
        var snapshot = new HookCatalogSnapshot(
            HookRegistrationDescriptors.DefaultProfileKey,
            new HookCatalogVersion("memory-test"),
            [.. hooks.Select(static hook => hook.Descriptor)]);
        var factory = new StaticHookInstanceFactory(hooks.ToDictionary(static hook => hook.Descriptor.Id, static hook => hook.Hook));
        var lease = await factory.CreateAsync(snapshot, CancellationToken.None);
        return new HookActivationScope(snapshot, lease);
    }

    /// <summary>Creates the caller's dispatch context, stamped with one fixed correlation and a deadline well after the harness clock.</summary>
    /// <param name="scope">The open scope.</param>
    /// <param name="harness">The harness whose clock stamps the dispatch.</param>
    /// <param name="owner">The owner whose captured correlation the dispatch carries.</param>
    /// <returns>A context whose point is a placeholder the memory runtime re-derives per point.</returns>
    internal static HookDispatchContext Context(HookActivationScope scope, MemoryHarness harness, MemoryTestOwner owner)
    {
        var now = harness.Time.GetUtcNow();
        return scope.CreateDispatch(new HookDispatchMetadata(
            AgentHookPoints.RunStarted,
            new HookDispatchId(Guid.NewGuid()),
            owner.Context.Correlation,
            now,
            now.AddSeconds(30)));
    }

    /// <summary>Creates a catalog registration for one hook at one point.</summary>
    /// <param name="name">The author hook name.</param>
    /// <param name="registration">The memory point registration.</param>
    /// <param name="order">The ordering anchor, or null for normal.</param>
    /// <returns>The registration descriptor.</returns>
    internal static HookRegistrationDescriptor Register(string name, HookPointDefinitionRegistration registration, HookOrder? order = null) =>
        HookRegistrationDescriptors.ForPoint(new HookId(name), registration, order);
}
