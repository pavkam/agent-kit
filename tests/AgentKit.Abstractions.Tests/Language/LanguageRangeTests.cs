// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageRange behavior and contracts.</summary>
public sealed class LanguageRangeTests
{
    [Fact]
    public void LanguageRange_WhenEndPrecedesStart_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageRange(new LanguagePosition(2, 0), new LanguagePosition(1, 9)));
        exception.ParamName.ShouldBe("end");
    }
}
