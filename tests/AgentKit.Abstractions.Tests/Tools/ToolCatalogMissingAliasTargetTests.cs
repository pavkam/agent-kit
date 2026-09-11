// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolCatalogMissingAliasTargetTests
{

    [Fact]
    public void Constructor_WhenAssignmentAuthored_RetainsUnresolvedEvidence()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var assignment = candidate.Toolset.Aliases[0];
        var collision = new ToolCatalogMissingAliasTarget(candidate.Toolset, assignment);
        collision.Toolset.ShouldBeSameAs(candidate.Toolset);
        collision.Assignment.ShouldBeSameAs(assignment);
        collision.ShouldBe(new ToolCatalogMissingAliasTarget(candidate.Toolset, assignment));
    }

    [Fact]
    public void Constructor_WhenEvidenceInvalid_RejectsExactParameter()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var assignment = candidate.Toolset.Aliases[0];
        Exact<ArgumentNullException>(() => _ = new ToolCatalogMissingAliasTarget(null!, assignment), "toolset");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogMissingAliasTarget(candidate.Toolset, null!), "assignment");
        Exact<ArgumentException>(() => _ = new ToolCatalogMissingAliasTarget(candidate.Toolset, new(new ToolAlias("absent"), candidate.Identity)), "assignment");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
