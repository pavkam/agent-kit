// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests;

using AgentKit;

public sealed class ArgumentExceptionExtensionsTests
{
    [Fact]
    public void ThrowIfDefault_WhenArrayIsDefault_ThrowsArgumentException()
    {
        ImmutableArray<int> array = default;

        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDefault(array));

        exception.ParamName.ShouldBe("array");
    }

    [Fact]
    public void ThrowIfDefault_WhenArrayIsEmpty_DoesNotThrow() => Should.NotThrow(() => ArgumentException.ThrowIfDefault(ImmutableArray<int>.Empty));

    [Fact]
    public void ThrowIfDefault_WhenArrayIsPopulated_DoesNotThrow()
    {
        ImmutableArray<int> array = [1, 2, 3];

        Should.NotThrow(() => ArgumentException.ThrowIfDefault(array));
    }

    [Fact]
    public void ThrowIfDefault_WhenParamNameSuppliedExplicitly_UsesSuppliedName()
    {
        ImmutableArray<int> array = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfDefault(array, "customParam"));

        exception.ParamName.ShouldBe("customParam");
    }

    [Fact]
    public void ThrowIfDefault_WhenUsedByExtensionValue_CoversProductionCallSite()
    {
        // AgentKit.Extensibility.ExtensionValue is a production call site for
        // this guard; this proves it is actually wired up end to end rather
        // than only exercised directly.
        var exception = Should.Throw<ArgumentException>(() => new ExtensionValue(default));

        exception.ParamName.ShouldBe("canonicalJson");
    }
}
