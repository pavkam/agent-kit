// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityGrantStoreUnavailableException behavior and contracts.</summary>
public sealed class SecurityGrantStoreUnavailableExceptionTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityGrantStoreUnavailableException((SecurityGrantStoreFailureKind) 99, "reason")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityGrantStoreUnavailableException(SecurityGrantStoreFailureKind.OpenFailed, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new SecurityGrantStoreUnavailableException(SecurityGrantStoreFailureKind.OpenFailed, "unavailable", inner);
        exception.Kind.ShouldBe(SecurityGrantStoreFailureKind.OpenFailed);
        exception.SafeMessage.ShouldBe("unavailable");
        exception.Message.ShouldBe("unavailable");
        exception.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Constructor_WhenInnerExceptionOmitted_HasNoInnerException()
    {
        var exception = new SecurityGrantStoreUnavailableException(SecurityGrantStoreFailureKind.OpenFailed, "unavailable");
        exception.InnerException.ShouldBeNull();
    }
}
