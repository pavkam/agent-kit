// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalScope"/> constraints and equality.</summary>
public sealed class RetrievalScopeTests
{
    [Fact]
    public void Unrestricted_WhenRead_NarrowsNothing()
    {
        RetrievalScope.Unrestricted.Namespaces.ShouldBeEmpty();
        RetrievalScope.Unrestricted.Documents.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenListsAreDefault_TreatsThemAsEmpty() =>
        new RetrievalScope(default, default).ShouldBe(RetrievalScope.Unrestricted);

    [Fact]
    public void Constructor_WhenANamespaceIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalScope([default], default)).ParamName.ShouldBe("namespaces");

    [Fact]
    public void Constructor_WhenADocumentIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalScope(default, [default])).ParamName.ShouldBe("documents");

    [Fact]
    public void Equality_WhenListsMatchByContent_IsStructural()
    {
        var document = new DocumentId(Guid.NewGuid());

        var first = new RetrievalScope([new MemoryNamespace("n")], [document]);
        var second = new RetrievalScope([new MemoryNamespace("n")], [document]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(new RetrievalScope([new MemoryNamespace("other")], [document]));
    }
}
