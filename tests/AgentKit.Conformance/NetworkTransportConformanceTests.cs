// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Shared network transport contract scenarios for real and scripted adapters.</summary>
/// <typeparam name="TFixture">The adapter-specific fixture.</typeparam>
public abstract class NetworkTransportConformanceTests<TFixture>
    where TFixture : INetworkTransportConformanceFixture
{
    /// <summary>Creates an isolated fixture for one inherited case.</summary>
    /// <returns>The fixture instance.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies required audit failure closes before connect.</summary>
    [Fact]
    public async Task SendAsync_WhenRequiredAuditUnavailable_DeniesBeforeConnect()
    {
        await using var fixture = CreateFixture();
        fixture.UseRejectingAudit();
        var request = fixture.CreateAuthorizedSend();
        var result = await fixture.Transport.SendAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkDenied>();
    }

    /// <summary>Verifies declared oversize responses return a typed limit outcome.</summary>
    [Fact]
    public async Task SendAsync_WhenDeclaredResponseExceedsBound_ReturnsLimitExceeded()
    {
        await using var fixture = CreateFixture();
        if (fixture is not ISupportsDeclaredOversizeResponseFixture declared)
        {
            return;
        }

        var request = fixture.CreateAuthorizedSend(maximumResponseBytes: 4);
        var result = await fixture.Transport.SendAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<NetworkResponseLimitExceeded>();
        declared.DeclaredOversizeObserved.ShouldBeTrue();
    }
}

/// <summary>Optional capability marker for declared oversize response scenarios.</summary>
public interface ISupportsDeclaredOversizeResponseFixture
{
    /// <summary>Gets whether the fixture observed a declared oversize response.</summary>
    public bool DeclaredOversizeObserved { get; }
}
