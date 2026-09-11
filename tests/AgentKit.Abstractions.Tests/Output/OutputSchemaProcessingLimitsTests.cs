// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaProcessingLimits behavior and contracts.</summary>
public sealed class OutputSchemaProcessingLimitsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OutputSchemaProcessingLimits_WhenLimitIsNotPositive_RejectsExactValue(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaProcessingLimits(value, 1, 1)).ParamName.ShouldBe("maximumUtf8Bytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaProcessingLimits(1, value, 1)).ParamName.ShouldBe("maximumDepth");
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputSchemaProcessingLimits(1, 1, value)).ParamName.ShouldBe("maximumNodes");
    }
}
