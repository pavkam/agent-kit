// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ProcessOutputArtifactRequest behavior and contracts.</summary>
public sealed class ProcessOutputArtifactRequestTests
{
    [Fact]
    public void Constructor_WhenIntentIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessOutputArtifactRequest(
            null!, HostTestData.Scope(), HostTestData.Identity(),
            ProcessOutputKind.StandardOutput, [1], new IdempotencyKey("replay-key"))).ParamName.ShouldBe("intent");

    [Fact]
    public void Constructor_WhenScopeIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessOutputArtifactRequest(
            HostTestData.ResolvedIntent(), null!, HostTestData.Identity(),
            ProcessOutputKind.StandardOutput, [1], new IdempotencyKey("replay-key"))).ParamName.ShouldBe("scope");

    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ProcessOutputArtifactRequest(
            HostTestData.ResolvedIntent(), HostTestData.Scope(), null!,
            ProcessOutputKind.StandardOutput, [1], new IdempotencyKey("replay-key"))).ParamName.ShouldBe("identity");

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ProcessOutputArtifactRequest(
            HostTestData.ResolvedIntent(), HostTestData.Scope(), HostTestData.Identity(),
            (ProcessOutputKind) 99, [1], new IdempotencyKey("replay-key"))).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenContentIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessOutputArtifactRequest(
            HostTestData.ResolvedIntent(), HostTestData.Scope(), HostTestData.Identity(),
            ProcessOutputKind.StandardOutput, default, new IdempotencyKey("replay-key"))).ParamName.ShouldBe("content");

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ProcessOutputArtifactRequest(
            HostTestData.ResolvedIntent(), HostTestData.Scope(), HostTestData.Identity(),
            ProcessOutputKind.StandardOutput, [1], default)).ParamName.ShouldBe("idempotencyKey");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var intent = HostTestData.ResolvedIntent();
        var scope = HostTestData.Scope();
        var identity = HostTestData.Identity();
        var key = new IdempotencyKey("replay-key");
        var request = new ProcessOutputArtifactRequest(intent, scope, identity, ProcessOutputKind.StandardOutput, [1, 2, 3], key);
        request.Intent.ShouldBeSameAs(intent);
        request.Scope.ShouldBeSameAs(scope);
        request.Identity.ShouldBeSameAs(identity);
        request.Kind.ShouldBe(ProcessOutputKind.StandardOutput);
        request.Content.SequenceEqual((byte[]) [1, 2, 3]).ShouldBeTrue();
        request.IdempotencyKey.ShouldBe(key);
    }
}
