// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public sealed class PublicApiExtractorTests
{
    [Fact]
    public void GeneratePublicApi_WhenModernContractIsExtracted_PreservesCompatibilityDetails()
    {
        var api = new[]
        {
            typeof(PublicApiExtractorFixture<>),
            typeof(PublicApiExtractorFixtureExtensions),
        }.GeneratePublicApi();

        api.ShouldContain("where T :");
        api.ShouldContain("class?");
        api.ShouldContain("protected abstract T? Transform(T? value = null)");
        api.ShouldContain("extension(System.Collections.Generic.IReadOnlyList<string> source)");
        api.ShouldContain("PublicCount");
        api.ShouldContain("PublicOrDefault");
        api.ShouldContain("int index = 0");
    }

    [Fact]
    public void GeneratePublicApi_WhenGenericExtensionReturnsNullableReceiverTypeParameter_FailsClosed()
    {
        var exception = Should.Throw<FormatException>(() => typeof(UnsupportedGenericExtensionFixtureExtensions).GeneratePublicApi());

        exception.Message.ShouldContain("0?");
    }
}
