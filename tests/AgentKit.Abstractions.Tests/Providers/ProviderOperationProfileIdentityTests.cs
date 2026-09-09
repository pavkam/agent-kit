// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using System.Globalization;

using AgentKit;

public sealed class ProviderOperationProfileIdentityTests
{
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException()
    {
        foreach (var construct in StringConstructors())
        {
            var exception = Should.Throw<ArgumentNullException>(() => construct(null!));
            exception.ParamName.ShouldBe("value");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextBlank_ThrowsArgumentException(string value)
    {
        foreach (var construct in StringConstructors())
        {
            var exception = Should.Throw<ArgumentException>(() => construct(value));
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("value");
        }
    }

    [Fact]
    public void StringIdentities_WhenTextDiffersOnlyByCase_PreserveOrdinalValues()
    {
        new ProviderEndpointId("Endpoint").ShouldNotBe(new ProviderEndpointId("endpoint"));
        new ProviderApiVersion("v1").Value.ShouldBe("v1");
        default(ProviderEndpointId).ToString().ShouldBe(string.Empty);
    }

    private static IEnumerable<Func<string, object>> StringConstructors()
    {
        yield return value => new ProviderServiceSurfaceId(value);
        yield return value => new ProviderEndpointId(value);
        yield return value => new ProviderEndpointProfileKey(value);
        yield return value => new ProviderCredentialProfileKey(value);
        yield return value => new ProviderCredentialSourceKey(value);
        yield return value => new ProviderAccountId(value);
        yield return value => new ProviderApiVersion(value);
        yield return value => new ProviderModelRevision(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProfileVersions_WhenNotPositive_ThrowArgumentOutOfRangeException(long value)
    {
        var endpoint = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileVersion(value));
        var credential = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileVersion(value));
        endpoint.ParamName.ShouldBe("value");
        credential.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ProfileVersions_WhenMaximum_RetainInvariantValues()
    {
        new ProviderEndpointProfileVersion(long.MaxValue).ToString().ShouldBe(long.MaxValue.ToString(CultureInfo.InvariantCulture));
        new ProviderCredentialProfileVersion(long.MaxValue).Value.ShouldBe(long.MaxValue);
    }
}
