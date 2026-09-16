// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionInputLookupRequest behavior and contracts.</summary>
public sealed class SessionInputLookupRequestTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputLookupRequest(null!, SessionsTestData.Input(), new InputFingerprint("sha256:original")));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenInputIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionInputLookupRequest(SessionsTestData.BeforeRunContext(), null!, new InputFingerprint("sha256:original")));
        exception.ParamName.ShouldBe("input");
    }

    [Fact]
    public void Constructor_WhenOriginalFingerprintIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionInputLookupRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.Input(), default));
        exception.ParamName.ShouldBe("originalFingerprint");
    }

    [Fact]
    public void Constructor_WhenContextIsNotLaneBound_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SessionInputLookupRequest(SessionsTestData.BeforeRunContext(laneBound: false), SessionsTestData.Input(), new InputFingerprint("sha256:original")));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsNotBeforeRun_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SessionInputLookupRequest(SessionsTestData.InRunContext(), SessionsTestData.Input(), new InputFingerprint("sha256:original")));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var context = SessionsTestData.BeforeRunContext();
        var input = SessionsTestData.Input();
        var fingerprint = new InputFingerprint("sha256:original");
        var request = new SessionInputLookupRequest(context, input, fingerprint);
        request.Context.ShouldBe(context);
        request.Input.ShouldBe(input);
        request.OriginalFingerprint.ShouldBe(fingerprint);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionInputLookupRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.Input(), new InputFingerprint("sha256:original"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
