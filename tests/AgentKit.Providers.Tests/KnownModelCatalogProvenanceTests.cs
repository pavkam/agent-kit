// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

/// <summary>Verifies KnownModelCatalogProvenance behavior and contracts.</summary>
public sealed class KnownModelCatalogProvenanceTests
{
    private static readonly Uri _url = new("https://example.test/catalog.json");
    private static readonly DateTimeOffset _generated = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenSourceNameIsBlank_ThrowsArgumentException(string? name)
    {
        var exception = Should.Throw<ArgumentException>(() => new KnownModelCatalogProvenance(name!, _url, null, _generated, _generated));

        exception.ParamName.ShouldBe("sourceName");
    }

    [Fact]
    public void Constructor_WhenSourceUrlIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new KnownModelCatalogProvenance("feed", null!, null, _generated, _generated));

        exception.ParamName.ShouldBe("sourceUrl");
    }

    [Fact]
    public void Constructor_WhenValid_ExposesValuesAndAllowsAbsentCommit()
    {
        var imported = _generated.AddHours(1);

        var provenance = new KnownModelCatalogProvenance("feed", _url, null, _generated, imported);

        provenance.SourceName.ShouldBe("feed");
        provenance.SourceUrl.ShouldBe(_url);
        provenance.SourceCommit.ShouldBeNull();
        provenance.GeneratedAt.ShouldBe(_generated);
        provenance.ImportedAt.ShouldBe(imported);
    }

    [Fact]
    public void Equals_WhenValuesMatch_InstancesAreEqual()
    {
        var left = new KnownModelCatalogProvenance("feed", _url, "abc", _generated, _generated);
        var right = new KnownModelCatalogProvenance("feed", new Uri("https://example.test/catalog.json"), "abc", _generated, _generated);

        left.ShouldBe(right);
    }
}
