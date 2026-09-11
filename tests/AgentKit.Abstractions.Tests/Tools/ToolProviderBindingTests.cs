// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolProviderBindingTests
{
    [Fact]
    public void Constructor_WhenProviderMatchesKey_CapturesIdentityOnceAndBorrowsExactInstance()
    {
        var sourceId = new ToolSourceId("source");
        var provider = new CallbackToolProvider(sourceId);
        var binding = new ToolProviderBinding(sourceId, provider);
        provider.ReadSourceId = static () => throw new InvalidOperationException("no live lookup");
        binding.SourceId.ShouldBe(sourceId);
        binding.Provider.ShouldBeSameAs(provider);
        provider.IdentityReads.ShouldBe(1);
        provider.Discoveries.ShouldBe(0);
        provider.Disposals.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenArgumentsInvalid_RejectsBeforeAssignmentOrDiscovery()
    {
        var sourceId = new ToolSourceId("source");
        var provider = new CallbackToolProvider(sourceId);
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolProviderBinding(default, provider), "sourceId");
        Exact<ArgumentNullException>(() => _ = new ToolProviderBinding(sourceId, null!), "provider");
        provider.IdentityReads.ShouldBe(0);
        Exact<ArgumentException>(() => _ = new ToolProviderBinding(new ToolSourceId("SOURCE"), provider), "provider");
        provider.ReadSourceId = static () => default;
        Exact<ArgumentException>(() => _ = new ToolProviderBinding(sourceId, provider), "provider");
        provider.Discoveries.ShouldBe(0);
        provider.Disposals.ShouldBe(0);
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
