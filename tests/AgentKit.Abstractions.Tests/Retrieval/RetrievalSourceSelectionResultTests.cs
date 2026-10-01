// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalSourceSelectionResult"/> factories.</summary>
public sealed class RetrievalSourceSelectionResultTests
{
    [Fact]
    public void Selected_WhenSourcesAreSupplied_ReportsSelected()
    {
        var source = new StubSource();

        var result = RetrievalSourceSelectionResult.Selected([source]);

        result.IsSelected.ShouldBeTrue();
        result.Sources.ShouldBe([source]);
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void Selected_WhenSourcesAreDefaultEmptyOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => RetrievalSourceSelectionResult.Selected(default)).ParamName.ShouldBe("sources");
        Should.Throw<ArgumentException>(() => RetrievalSourceSelectionResult.Selected([])).ParamName.ShouldBe("sources");
        Should.Throw<ArgumentException>(() => RetrievalSourceSelectionResult.Selected([null!])).ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Rejected_WhenMessageIsSupplied_CarriesNoSources()
    {
        var result = RetrievalSourceSelectionResult.Rejected("none");

        result.IsSelected.ShouldBeFalse();
        result.Sources.ShouldBeEmpty();
        result.SafeMessage.ShouldBe("none");
        Should.Throw<ArgumentException>(() => RetrievalSourceSelectionResult.Rejected(" ")).ParamName.ShouldBe("safeMessage");
    }

    private sealed class StubSource: IRetrievalSource
    {
        public RetrievalSourceDescriptor Descriptor { get; } = new(new RetrievalSourceKey("s"), "1", false, new ComponentId("a"));

        public ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
