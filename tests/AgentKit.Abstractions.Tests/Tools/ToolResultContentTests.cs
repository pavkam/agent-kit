// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Text;
using System.Text.Json;

using AgentKit;

public sealed class ToolResultContentTests
{
    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenSourceDisposed_OwnsStructuralValue()
    {
        ToolResultStructuredContent content;
        using (var document = JsonDocument.Parse("{\"b\":2,\"a\":[true]}"))
        {
            content = new ToolResultStructuredContent(document.RootElement, null, ExtensionData.Empty);
        }

        using var equivalent = JsonDocument.Parse("{\"b\":2,\"a\":[true]}");
        var same = new ToolResultStructuredContent(equivalent.RootElement, null, ExtensionData.Empty);

        content.Value.GetProperty("a")[0].GetBoolean().ShouldBeTrue();
        content.ShouldBe(same);
        content.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolResultStructuredContent(default, null, ExtensionData.Empty));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenCopiedSchemaIsInvalid_ThrowsExactException()
    {
        using var document = JsonDocument.Parse("{}");
        var malformed = new JsonSchemaReference("schema", new SchemaVersion("1")) with { Name = null! };

        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultStructuredContent(document.RootElement, malformed, ExtensionData.Empty));

        exception.ParamName.ShouldBe("schema");
    }

    [Fact]
    public void ToolResultTextContent_Constructor_WhenSemanticsUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ToolResultTextContent("text", (TextSemantics) 99, ExtensionData.Empty));

        exception.ParamName.ShouldBe("semantics");
    }

    [Fact]
    public void ToolResultTextContent_Constructor_WhenTextNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultTextContent(null!, TextSemantics.Plain, ExtensionData.Empty));

        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void ToolResultTextContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultTextContent("text", TextSemantics.Plain, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolResultStructuredContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        using var document = JsonDocument.Parse("{}");

        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultStructuredContent(document.RootElement, null, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenReferenceNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultMediaContent(null!, ExtensionData.Empty));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var reference = new MediaReference(
            new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "text/plain", null, [], 0, null,
            ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultMediaContent(reference, null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Theory]
    [InlineData(0, typeof(ArgumentOutOfRangeException))]
    [InlineData(1, typeof(ArgumentOutOfRangeException))]
    [InlineData(2, typeof(ArgumentNullException))]
    [InlineData(3, typeof(ArgumentException))]
    [InlineData(4, typeof(ArgumentOutOfRangeException))]
    [InlineData(5, typeof(ArgumentNullException))]
    [InlineData(6, typeof(ArgumentException))]
    public void ToolResultMediaContent_Constructor_WhenCopiedReferenceInvalid_ThrowsExactException(
        int invalidCase,
        Type exceptionType)
    {
        var valid = Media();
        var malformed = invalidCase switch
        {
            0 => valid with { Id = default },
            1 => valid with { SourceKind = (MediaSourceKind) 99 },
            2 => valid with { MediaType = null! },
            3 => valid with { InlineBytes = default },
            4 => valid with { SizeInBytes = -1 },
            5 => valid with { Extensions = null! },
            6 => valid with { Uri = new Uri("https://example.test/media") },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
        };

        var exception = Should.Throw<ArgumentException>(
            () => new ToolResultMediaContent(malformed, ExtensionData.Empty));

        exception.GetType().ShouldBe(exceptionType);
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultMediaContent_Constructor_WhenFileReferenceRetainsUri_PreservesEvidenceWithoutResolution()
    {
        var reference = Media() with
        {
            SourceKind = MediaSourceKind.FileReference,
            Uri = new Uri("file:///workspace/result.txt"),
            InlineBytes = [],
        };

        var content = new ToolResultMediaContent(reference, ExtensionData.Empty);

        content.Reference.ShouldBe(reference);
    }

    [Fact]
    public void ToolResultArtifactContent_Constructor_WhenReferenceNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultArtifactContent(null!, ExtensionData.Empty));

        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultArtifactContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolResultArtifactContent(Artifact(), null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ToolResultOpaqueContent_Constructor_WhenValid_RetainsOpaqueBytes()
    {
        var payload = new ExtensionValue([.. Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"future\":true}")]);

        var content = new ToolResultOpaqueContent("vendor.future", payload, ExtensionData.Empty);

        content.TypeDiscriminator.ShouldBe("vendor.future");
        content.CanonicalPayload.ShouldBe(payload);
    }

    [Fact]
    public void ToolResultOpaqueContent_Constructor_WhenPayloadDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolResultOpaqueContent("vendor.future", default, ExtensionData.Empty));

        exception.ParamName.ShouldBe("canonicalPayload");
    }

    [Fact]
    public void ToolResultOpaqueContent_Constructor_WhenDiscriminatorBlank_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolResultOpaqueContent(" ", default, ExtensionData.Empty));

        exception.ParamName.ShouldBe("typeDiscriminator");
    }

    [Fact]
    public void ToolResultContent_WhenEnumerated_IsClosedToCanonicalVariants()
    {
        var variants = typeof(ToolResultContent).Assembly.GetTypes()
            .Where(type => type.BaseType == typeof(ToolResultContent))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        variants.ShouldBe(
        [
            nameof(ToolResultArtifactContent),
            nameof(ToolResultMediaContent),
            nameof(ToolResultOpaqueContent),
            nameof(ToolResultStructuredContent),
            nameof(ToolResultTextContent),
        ]);
    }

    [Fact]
    public void ToolResultContent_CopyConstructor_WhenExternalVariantBootstrapsFromBuiltIn_ThrowsExactException()
    {
        var original = new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => new ForeignToolResultContent(original));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void ToolResultContent_CopyConstructor_WhenOriginalNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignToolResultContent(null!));

        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void ToolResultContent_CopyConstructor_WhenBuiltInVariantCopies_PreservesValue()
    {
        var original = new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty);

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(ToolResultTextContent));
    }

    private static ArtifactReference Artifact() => new(
        new ArtifactId(Guid.NewGuid()), new ArtifactVersion("1"), new ArtifactDirectoryId("output"),
        new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), new TenantId("tenant"),
        new ArtifactOwnerId("session:owner"), new PrincipalId("principal"), "text/plain", 1,
        new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch),
        ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);

    private static MediaReference Media() => new(
        new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "text/plain", null, [1], 1,
        new ContentHash("hash"), ExtensionData.Empty);

    private sealed record ForeignToolResultContent(ToolResultContent Original): ToolResultContent(Original);
}
