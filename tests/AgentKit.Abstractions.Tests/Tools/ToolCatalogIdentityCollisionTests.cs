// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolCatalogIdentityCollisionTests
{

    [Fact]
    public void Constructor_WhenSameIdentityHasMultipleSources_PreservesOrderedEvidence()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other");
        var collision = new ToolCatalogIdentityCollision([first, second]);
        collision.Candidates.ShouldBe([first, second]);
        collision.ShouldBe(new ToolCatalogIdentityCollision([ToolCatalogMergeTestData.Candidate(), ToolCatalogMergeTestData.Candidate("other", "other")]));
        collision.GetHashCode().ShouldBe(new ToolCatalogIdentityCollision([first, second]).GetHashCode());
        collision.ShouldNotBe(new ToolCatalogIdentityCollision([second, first]));
        collision.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenGroupInvalid_RejectsExactParameter()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other", "different");
        Exact<ArgumentException>(() => _ = new ToolCatalogIdentityCollision(default), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogIdentityCollision([first, null!]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogIdentityCollision([]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogIdentityCollision([first]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogIdentityCollision([first, first]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogIdentityCollision([first, second]), "candidates");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
