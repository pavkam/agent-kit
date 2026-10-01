// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RetrievalResult"/> factories.</summary>
public sealed class RetrievalResultTests
{
    private static readonly RetrievalRequestId _request = new(Guid.NewGuid());
    private static readonly RetrievalSummary _summary = new(1, 0, 0, 0, 0, 0, null);

    [Fact]
    public void Completed_WhenValid_ReportsCandidatesAndSummary()
    {
        var candidate = MemoryTestData.Candidate(_request);

        var result = RetrievalResult.Completed(_request, [candidate], _summary);

        result.IsCompleted.ShouldBeTrue();
        result.RequestId.ShouldBe(_request);
        result.Candidates.ShouldBe([candidate]);
        result.Summary.ShouldBe(_summary);
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void Completed_WhenNoCandidatesMatched_IsAValidAnswer() =>
        RetrievalResult.Completed(_request, [], _summary).Candidates.ShouldBeEmpty();

    [Fact]
    public void Completed_WhenACandidateBelongsToAnotherRequest_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => RetrievalResult.Completed(_request, [MemoryTestData.Candidate(new RetrievalRequestId(Guid.NewGuid()))], _summary)).ParamName.ShouldBe("candidates");

    [Fact]
    public void Completed_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RetrievalResult.Completed(default, [], _summary)).ParamName.ShouldBe("requestId");
        Should.Throw<ArgumentException>(() => RetrievalResult.Completed(_request, default, _summary)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentException>(() => RetrievalResult.Completed(_request, [null!], _summary)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentNullException>(() => RetrievalResult.Completed(_request, [], null!)).ParamName.ShouldBe("summary");
    }

    [Fact]
    public void Failed_WhenFailureIsSupplied_CarriesNoCandidates()
    {
        var failure = new RetrievalFailure(RetrievalFailureKind.Denied, "no");

        var result = RetrievalResult.Failed(_request, failure);

        result.IsCompleted.ShouldBeFalse();
        result.Candidates.ShouldBeEmpty();
        result.Summary.ShouldBeNull();
        result.Failure.ShouldBe(failure);
    }

    [Fact]
    public void Failed_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RetrievalResult.Failed(default, new RetrievalFailure(RetrievalFailureKind.Denied, "m"))).ParamName.ShouldBe("requestId");
        Should.Throw<ArgumentNullException>(() => RetrievalResult.Failed(_request, null!)).ParamName.ShouldBe("failure");
    }
}
