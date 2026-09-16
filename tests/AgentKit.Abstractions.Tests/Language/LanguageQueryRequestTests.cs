// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageQueryRequest behavior and contracts.</summary>
public sealed class LanguageQueryRequestTests
{
    [Theory]
    [InlineData(LanguageQueryKind.Hover, false, true, false)]
    [InlineData(LanguageQueryKind.Diagnostics, true, true, false)]
    [InlineData(LanguageQueryKind.WorkspaceSymbols, true, false, false)]
    public void LanguageQueryRequest_WhenOperationShapeInvalid_ThrowsKind(LanguageQueryKind kind, bool hasPath, bool hasPosition, bool hasQuery)
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryRequest(new LanguageQueryId(Guid.Parse("10000000-0000-0000-0000-000000000001")), kind, hasPath ? new FileSystemPath("src/a.cs") : null, hasPosition ? new LanguagePosition(0, 0) : null, hasQuery ? "Agent" : null, 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = new LanguageQueryId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var path = new FileSystemPath("src/a.cs");
        var position = new LanguagePosition(0, 0);
        var grant = SecurityTestData.Grant();
        var request = new LanguageQueryRequest(id, LanguageQueryKind.Hover, path, position, null, 10, TimeSpan.FromSeconds(1), grant);
        request.Id.ShouldBe(id);
        request.Kind.ShouldBe(LanguageQueryKind.Hover);
        request.Path.ShouldBe(path);
        request.Position.ShouldBe(position);
        request.Query.ShouldBeNull();
        request.MaximumResults.ShouldBe(10);
        request.Timeout.ShouldBe(TimeSpan.FromSeconds(1));
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenWorkspaceSymbolsQueryIsValid_InitializesProperties()
    {
        var id = new LanguageQueryId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var request = new LanguageQueryRequest(id, LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant());
        request.Query.ShouldBe("symbol");
        request.Path.ShouldBeNull();
        request.Position.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenIdIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageQueryRequest(default, LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageQueryRequest(new LanguageQueryId(Guid.NewGuid()), (LanguageQueryKind) 999, null, null, "symbol", 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenMaximumResultsIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageQueryRequest(new LanguageQueryId(Guid.NewGuid()), LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 0, TimeSpan.FromSeconds(1), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("maximumResults");
    }

    [Fact]
    public void Constructor_WhenTimeoutIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageQueryRequest(new LanguageQueryId(Guid.NewGuid()), LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.Zero, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("timeout");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new LanguageQueryRequest(new LanguageQueryId(Guid.NewGuid()), LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.FromSeconds(1), null!));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LanguageQueryRequest(new LanguageQueryId(Guid.NewGuid()), LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
