// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RetrievalQuery"/> constraints and copy helpers.</summary>
public sealed class RetrievalQueryTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var destination = new ModelDestination(new ModelAlias("fast"));

        var query = MemoryTestData.Query(owner, "text", destination: destination);

        query.Context.ShouldBe(owner.Context);
        query.Query.Text.ShouldBe("text");
        query.ExposureDestination.ShouldBe(destination);
        query.MaximumClassification.ShouldBe(DataClassification.Confidential);
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalQuery(default, owner.Context, new RetrievalQueryContent("q"), RetrievalScope.Unrestricted, new RetrievalBudget(1, 1, 1), DataClassification.Public, null)).ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenAReferenceIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new RetrievalRequestId(Guid.NewGuid());
        var content = new RetrievalQueryContent("q");
        var budget = new RetrievalBudget(1, 1, 1);

        Should.Throw<ArgumentNullException>(() => new RetrievalQuery(id, null!, content, RetrievalScope.Unrestricted, budget, DataClassification.Public, null)).ParamName.ShouldBe("context");
        Should.Throw<ArgumentNullException>(() => new RetrievalQuery(id, owner.Context, null!, RetrievalScope.Unrestricted, budget, DataClassification.Public, null)).ParamName.ShouldBe("query");
        Should.Throw<ArgumentNullException>(() => new RetrievalQuery(id, owner.Context, content, null!, budget, DataClassification.Public, null)).ParamName.ShouldBe("scope");
        Should.Throw<ArgumentNullException>(() => new RetrievalQuery(id, owner.Context, content, RetrievalScope.Unrestricted, null!, DataClassification.Public, null)).ParamName.ShouldBe("budget");
    }

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalQuery(new RetrievalRequestId(Guid.NewGuid()), owner.Context, new RetrievalQueryContent("q"), RetrievalScope.Unrestricted, new RetrievalBudget(1, 1, 1), (DataClassification) 9, null)).ParamName.ShouldBe("maximumClassification");
    }

    [Fact]
    public void WithQuery_WhenContentIsReplaced_KeepsEveryOtherValue()
    {
        var query = MemoryTestData.Query(MemoryTestData.NewOwner());

        var replaced = query.WithQuery(new RetrievalQueryContent("other"));

        replaced.Query.Text.ShouldBe("other");
        replaced.Id.ShouldBe(query.Id);
        replaced.Budget.ShouldBe(query.Budget);
        Should.Throw<ArgumentNullException>(() => query.WithQuery(null!)).ParamName.ShouldBe("query");
    }

    [Fact]
    public void WithBudget_WhenBudgetIsReplaced_KeepsEveryOtherValue()
    {
        var query = MemoryTestData.Query(MemoryTestData.NewOwner());

        var replaced = query.WithBudget(new RetrievalBudget(1, 2, 3));

        replaced.Budget.ShouldBe(new RetrievalBudget(1, 2, 3));
        replaced.Query.ShouldBe(query.Query);
        Should.Throw<ArgumentNullException>(() => query.WithBudget(null!)).ParamName.ShouldBe("budget");
    }
}
