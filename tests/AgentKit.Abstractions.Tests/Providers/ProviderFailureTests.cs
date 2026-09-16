// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderFailure behavior and contracts.</summary>
public sealed class ProviderFailureTests
{
    [Fact]
    public void ProviderFailure_Constructor_WhenSafeMessageInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => Failure(safeMessage: " "));
    [Fact]
    public void ProviderFailure_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderFailure(ProviderFailureKind.Unknown, new ProviderId("openai"), null, null, null, null, "failed", null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderFailure_Equality_WhenSameValues_InstancesAreEqual() => Failure().ShouldBe(Failure());

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Failure();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ProviderFailure Failure(ProviderFailureKind kind = ProviderFailureKind.Unknown, string safeMessage = "failed") => new(kind, new ProviderId("openai"), null, null, null, null, safeMessage, null, ExtensionData.Empty);
}
