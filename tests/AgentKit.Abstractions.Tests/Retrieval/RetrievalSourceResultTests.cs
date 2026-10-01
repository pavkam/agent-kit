// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RetrievalSourceResult"/> factories.</summary>
public sealed class RetrievalSourceResultTests
{
    [Fact]
    public void Succeeded_WhenValid_ReportsCandidatesAndGeneration()
    {
        var candidate = MemoryTestData.Candidate(new RetrievalRequestId(Guid.NewGuid()));

        var result = RetrievalSourceResult.Succeeded([candidate], 4);

        result.IsSucceeded.ShouldBeTrue();
        result.Candidates.ShouldBe([candidate]);
        result.DeletionGeneration.ShouldBe(4);
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Succeeded_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentException>(() => RetrievalSourceResult.Succeeded(default, null)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentException>(() => RetrievalSourceResult.Succeeded([null!], null)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentOutOfRangeException>(() => RetrievalSourceResult.Succeeded([], -1)).ParamName.ShouldBe("deletionGeneration");
    }

    [Fact]
    public void Failed_WhenFailureIsSupplied_CarriesNoCandidates()
    {
        var failure = new RetrievalFailure(RetrievalFailureKind.SourcesUnavailable, "down");

        var result = RetrievalSourceResult.Failed(failure);

        result.IsSucceeded.ShouldBeFalse();
        result.Candidates.ShouldBeEmpty();
        result.Failure.ShouldBe(failure);
        Should.Throw<ArgumentNullException>(() => RetrievalSourceResult.Failed(null!)).ParamName.ShouldBe("failure");
    }
}
