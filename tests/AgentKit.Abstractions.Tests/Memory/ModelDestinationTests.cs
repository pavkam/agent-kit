// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="ModelDestination"/> constraints and value semantics.</summary>
public sealed class ModelDestinationTests
{
    [Fact]
    public void Constructor_WhenAliasIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelDestination(default)).ParamName.ShouldBe("modelAlias");

    [Fact]
    public void Constructor_WhenAliasIsSupplied_PreservesIt() =>
        new ModelDestination(new ModelAlias("fast")).ModelAlias.ShouldBe(new ModelAlias("fast"));

    [Fact]
    public void Equality_WhenAliasesMatch_IsStructural() =>
        new ModelDestination(new ModelAlias("fast")).ShouldBe(new ModelDestination(new ModelAlias("fast")));
}
