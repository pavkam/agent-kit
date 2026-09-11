// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguagePosition behavior and contracts.</summary>
public sealed class LanguagePositionTests
{
    [Fact]
    public void LanguagePosition_WhenCoordinateNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguagePosition(-1, 0));
        exception.ParamName.ShouldBe("line");
    }
}
