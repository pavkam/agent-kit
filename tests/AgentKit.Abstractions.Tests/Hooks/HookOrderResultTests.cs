// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookOrderResultTests
{
    [Fact]
    public void ResolvedConstructor_WhenOrderedContainsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookOrderResolved([null!]));
        exception.ParamName.ShouldBe("ordered");
    }

    [Fact]
    public void ResolvedConstructor_WhenValid_RoundTripsOrdered()
    {
        var registration = HookKernelTestData.Registration();
        new HookOrderResolved([registration]).Ordered.ShouldBe([registration]);
    }

    [Fact]
    public void InvalidConstructor_WhenDiagnosticsIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookOrderInvalid([]));
        exception.ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void InvalidConstructor_WhenDiagnosticsContainsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookOrderInvalid([null!]));
        exception.ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void InvalidConstructor_WhenValid_RoundTripsDiagnostics()
    {
        var diagnostic = new CompositionDiagnostic("code", "message");
        new HookOrderInvalid([diagnostic]).Diagnostics.ShouldBe([diagnostic]);
    }

    [Fact]
    public void Hierarchy_IsClosedToResolvedAndInvalid()
    {
        HookOrderResult resolved = new HookOrderResolved([]);
        HookOrderResult invalid = new HookOrderInvalid([new CompositionDiagnostic("code", "message")]);

        _ = resolved.ShouldBeOfType<HookOrderResolved>();
        _ = invalid.ShouldBeOfType<HookOrderInvalid>();
    }
}
