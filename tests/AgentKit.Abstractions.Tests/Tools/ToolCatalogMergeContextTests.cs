// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolCatalogMergeContextTests
{

    [Fact]
    public void Constructor_WhenEvidenceValid_PreservesOrderAndStructuralEquality()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other");
        var request = ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]);
        var context = new ToolCatalogMergeContext(request, [first, second], [new ToolCatalogIdentityCollision([first, second])]);
        var copy = new ToolCatalogMergeContext(request, [first, second], [new ToolCatalogIdentityCollision([first, second])]);
        context.Request.ShouldBeSameAs(request);
        context.Candidates.ShouldBe([first, second]);
        context.ShouldBe(copy);
        context.GetHashCode().ShouldBe(copy.GetHashCode());
        context.ShouldNotBe(new ToolCatalogMergeContext(request, [second, first], copy.Collisions));
        context.ShouldNotBe(new ToolCatalogMergeContext(request, copy.Candidates, []));
        context.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenEvidenceInvalid_RejectsExactParameter()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other");
        var request = ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]);
        Exact<ArgumentNullException>(() => _ = new ToolCatalogMergeContext(null!, [], []), "request");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, default, []), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, [null!], []), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, [first, first], []), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, [], default), "collisions");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, [], [null!]), "collisions");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, [first], [new ToolCatalogIdentityCollision([first, second])]), "collisions");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeContext(request, [first], [new ToolCatalogAliasCollision(new ToolAlias("read"), [first, second])]), "collisions");
    }

    [Fact]
    public void Constructor_WhenCollisionIsMissingAliasTarget_AcceptsEmptyMembership()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var request = ToolCatalogMergeTestData.Request([candidate.Toolset]);
        var assignment = candidate.Toolset.Aliases[0];
        var missing = new ToolCatalogMissingAliasTarget(candidate.Toolset, assignment);
        var context = new ToolCatalogMergeContext(request, [candidate], [missing]);
        context.Collisions.ShouldBe([missing]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other");
        var request = ToolCatalogMergeTestData.Request([first.Toolset, second.Toolset]);
        var original = new ToolCatalogMergeContext(request, [first, second], [new ToolCatalogIdentityCollision([first, second])]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
