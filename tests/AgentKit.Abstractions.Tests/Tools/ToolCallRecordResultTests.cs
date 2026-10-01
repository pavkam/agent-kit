// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolCallRecordResult"/>.</summary>
public sealed class ToolCallRecordResultTests
{
    [Fact]
    public void Constructor_WhenForeignOutcomeIsCreated_RejectsTheUnsupportedFamily() =>
        Should.Throw<ArgumentException>(() => new ForeignToolCallRecordResult()).ParamName.ShouldBe("result");

    [Fact]
    public void CopyConstructor_WhenForeignOutcomeCopiesABuiltIn_RejectsTheUnsupportedFamily() =>
        Should.Throw<ArgumentException>(() => new ForeignToolCallRecordResult(new ToolCallRecorded())).ParamName.ShouldBe("result");

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ForeignToolCallRecordResult(null!)).ParamName.ShouldBe("original");
}
