// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolCatalogCandidateTests
{

    [Fact]
    public void Constructor_WhenEvidenceExact_RetainsOriginalPublicationsAndDescriptor()
    {
        var expected = ToolCatalogMergeTestData.Candidate();
        var candidate = new ToolCatalogCandidate(expected.Toolset, expected.Source, expected.Identity);
        candidate.Toolset.ShouldBeSameAs(expected.Toolset);
        candidate.Source.ShouldBeSameAs(expected.Source);
        candidate.Tool.ShouldBeSameAs(expected.Tool);
        candidate.Identity.ShouldBe(expected.Identity);
        candidate.ShouldBe(ToolCatalogMergeTestData.Candidate());
        candidate.GetHashCode().ShouldBe(ToolCatalogMergeTestData.Candidate().GetHashCode());
        (candidate with { }).ShouldBe(candidate);
    }

    [Fact]
    public void Constructor_WhenEvidenceInvalid_RejectsExactParameter()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        Exact<ArgumentNullException>(() => _ = new ToolCatalogCandidate(null!, candidate.Source, candidate.Identity), "toolset");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogCandidate(candidate.Toolset, null!, candidate.Identity), "source");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolCatalogCandidate(candidate.Toolset, candidate.Source, default), "identity");
        Exact<ArgumentException>(() => _ = new ToolCatalogCandidate(ToolCatalogMergeTestData.Toolset("empty", [], []), candidate.Source, candidate.Identity), "source");
        Exact<ArgumentException>(() => _ = new ToolCatalogCandidate(candidate.Toolset, candidate.Source, new ToolIdentity(candidate.Identity.Id, new ToolVersion("absent"))), "identity");
        Exact<ArgumentException>(() => _ = new ToolCatalogCandidate(candidate.Toolset, candidate.Source, new ToolIdentity(new ToolId("absent"), candidate.Identity.Version)), "identity");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
