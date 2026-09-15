// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

public sealed class ReadFileToolOptionsTests
{
    [Fact]
    public void DefaultMaximumLines_WhenUnconfigured_IsTwoThousand()
    {
        var options = new ReadFileToolOptions();

        options.DefaultMaximumLines.ShouldBe(2_000);
    }

    [Fact]
    public void MaximumLines_WhenUnconfigured_IsTwentyThousand()
    {
        var options = new ReadFileToolOptions();

        options.MaximumLines.ShouldBe(20_000);
    }

    [Fact]
    public void Defaults_WhenUnconfigured_SatisfyRegistrationValidation()
    {
        var options = new ReadFileToolOptions();

        options.DefaultMaximumLines.ShouldBeLessThanOrEqualTo(options.MaximumLines);
    }
}
