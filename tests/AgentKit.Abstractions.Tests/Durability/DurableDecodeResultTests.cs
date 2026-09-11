// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableDecodeResult behavior and contracts.</summary>
public sealed class DurableDecodeResultTests
{
    [Fact]
    public void DurableDecodeResult_Hierarchy_ContainsOnlyTheTwoDeclaredKinds()
    {
        var kinds = typeof(DurableDecodeResult<string>).Assembly.GetTypes().Where(type => type.BaseType is { IsGenericType: true } baseType && baseType.GetGenericTypeDefinition() == typeof(DurableDecodeResult<>)).Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal);
        kinds.ShouldBe(["DurableDecodeIncompatible`1", "DurableDecoded`1"]);
    }
}
