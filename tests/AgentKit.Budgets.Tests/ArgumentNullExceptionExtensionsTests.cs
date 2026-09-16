// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

/// <summary>Verifies the canonical hierarchy-lock guard and its type-qualified invocation.</summary>
public sealed class ArgumentNullExceptionExtensionsTests
{
    /// <summary>Proves direct type-qualified invocation rejects a null lock with exact parameter attribution.</summary>
    [Fact]
    public void ThrowIfNullLock_WhenNull_ThrowsWithExactParameterName()
    {
        Lock? value = null;
        var exception = Should.Throw<ArgumentNullException>(() => ArgumentNullException.ThrowIfNullLock(value));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Proves a supplied lock instance is accepted without throwing.</summary>
    [Fact]
    public void ThrowIfNullLock_WhenNotNull_DoesNotThrow()
    {
        var value = new Lock();
        Should.NotThrow(() => ArgumentNullException.ThrowIfNullLock(value));
    }
}
