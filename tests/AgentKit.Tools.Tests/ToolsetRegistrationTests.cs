// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ToolsetRegistrationTests
{
    [Fact]
    public void Constructor_WhenKeyDefault_RejectsExactParameter()
    {
        var error = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetRegistration(default));
        error.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        error.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenKeyValid_RetainsExactKey()
    {
        var key = new ToolsetKey("Tools");
        var registration = new ToolsetRegistration(key);
        registration.Key.ShouldBe(key);
        registration.ShouldBe(new ToolsetRegistration(key));
        registration.ShouldNotBe(new ToolsetRegistration(new ToolsetKey("tools")));
    }
}
