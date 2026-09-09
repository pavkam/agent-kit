// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using System.Collections.Immutable;

using AgentKit;

public sealed class CompatibilityProfileContractsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenCompatibilityProfileKeyTextInvalid_ThrowsExactException(string? value)
    {
        ArgumentException exception;
        if (value is null)
        {
            exception = Should.Throw<ArgumentNullException>(() => new CompatibilityProfileKey(value!));
        }
        else
        {
            exception = Should.Throw<ArgumentException>(() => new CompatibilityProfileKey(value));
            exception.ShouldNotBeOfType<ArgumentNullException>();
        }

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void CompatibilityProfileKey_WhenOrdinalTextDiffers_PreservesDistinctIdentity()
    {
        var upper = new CompatibilityProfileKey("PROFILE");
        var lower = new CompatibilityProfileKey("profile");

        upper.ShouldNotBe(lower);
        upper.ToString().ShouldBe("PROFILE");
        default(CompatibilityProfileKey).ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Constructor_WhenCompatibilityProfileVersionNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new CompatibilityProfileVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void CompatibilityProfileVersion_WhenMaximumValueProvided_PreservesValue()
    {
        var version = new CompatibilityProfileVersion(long.MaxValue);

        version.Value.ShouldBe(long.MaxValue);
        version.ToString().ShouldBe(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Constructor_WhenModelDescriptorRevisionNotPositive_ThrowsArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ModelDescriptorRevision(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ModelDescriptorRevision_WhenMaximumValueProvided_PreservesValue()
    {
        var revision = new ModelDescriptorRevision(long.MaxValue);

        revision.Value.ShouldBe(long.MaxValue);
        revision.ToString().ShouldBe(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Constructor_WhenRequiredValuesOrEnumsInvalid_ThrowsExactParameter()
    {
        var key = Should.Throw<ArgumentOutOfRangeException>(() => Create(default, Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [Dialect()], ExtensionData.Empty));
        key.ParamName.ShouldBe("key");
        var version = Should.Throw<ArgumentOutOfRangeException>(() => Create(Key(), default, Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [Dialect()], ExtensionData.Empty));
        version.ParamName.ShouldBe("version");
        var fingerprint = Should.Throw<ArgumentOutOfRangeException>(() => Create(Key(), Version(), default, ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [Dialect()], ExtensionData.Empty));
        fingerprint.ParamName.ShouldBe("fingerprint");
        var request = Should.Throw<ArgumentOutOfRangeException>(() => Create(Key(), Version(), Fingerprint(), (ModelCandidateMultiplicity) 42, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [Dialect()], ExtensionData.Empty));
        request.ParamName.ShouldBe("requestMultiplicity");
        var response = Should.Throw<ArgumentOutOfRangeException>(() => Create(Key(), Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, (ModelCandidateMultiplicity) 42, ModelUsageReportingMode.NotReported, [Dialect()], ExtensionData.Empty));
        response.ParamName.ShouldBe("responseMultiplicity");
        var usage = Should.Throw<ArgumentOutOfRangeException>(() => Create(Key(), Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, (ModelUsageReportingMode) 42, [Dialect()], ExtensionData.Empty));
        usage.ParamName.ShouldBe("usageReporting");
        var extensions = Should.Throw<ArgumentNullException>(() => Create(Key(), Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [Dialect()], null!));
        extensions.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Constructor_WhenDialectArrayDefaultDuplicateOrContainsDefault_ThrowsExactParameter()
    {
        var uninitialized = Should.Throw<ArgumentException>(() => Create(Key(), Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, default, ExtensionData.Empty));
        uninitialized.ParamName.ShouldBe("supportedToolSchemaDialects");
        var duplicate = Should.Throw<ArgumentException>(() => Create(Key(), Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [Dialect(), Dialect()], ExtensionData.Empty));
        duplicate.ParamName.ShouldBe("supportedToolSchemaDialects");
        var defaultDialect = Should.Throw<ArgumentOutOfRangeException>(() => Create(Key(), Version(), Fingerprint(), ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne, ModelUsageReportingMode.NotReported, [default], ExtensionData.Empty));
        defaultDialect.ParamName.ShouldBe("supportedToolSchemaDialects");
    }

    [Fact]
    public void Constructor_WhenNoToolDialectIsSupported_PreservesEmptyInitializedArray()
    {
        var profile = Profile(dialects: []);

        profile.SupportedToolSchemaDialects.IsDefault.ShouldBeFalse();
        profile.SupportedToolSchemaDialects.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ModelCandidateMultiplicity.ExactlyOne, ModelCandidateMultiplicity.ExactlyOne)]
    [InlineData(ModelCandidateMultiplicity.Multiple, ModelCandidateMultiplicity.Multiple)]
    public void Constructor_WhenMultiplicityIsDefined_PreservesOperationContract(
        ModelCandidateMultiplicity requestMultiplicity,
        ModelCandidateMultiplicity responseMultiplicity)
    {
        var profile = Profile(requestMultiplicity: requestMultiplicity, responseMultiplicity: responseMultiplicity);

        profile.RequestMultiplicity.ShouldBe(requestMultiplicity);
        profile.ResponseMultiplicity.ShouldBe(responseMultiplicity);
    }

    [Theory]
    [InlineData(ModelUsageReportingMode.NotReported)]
    [InlineData(ModelUsageReportingMode.TerminalOnly)]
    [InlineData(ModelUsageReportingMode.InterimOnly)]
    [InlineData(ModelUsageReportingMode.StreamingAndTerminal)]
    public void Constructor_WhenUsageReportingModeIsDefined_PreservesAvailability(ModelUsageReportingMode usageReporting)
    {
        var profile = Profile(usageReporting: usageReporting);

        profile.UsageReporting.ShouldBe(usageReporting);
    }

    [Fact]
    public void Equality_WhenIndependentlyConstructedOrderedEvidenceMatches_IsStructuralAndCopyPreservesEvidence()
    {
        var first = Profile(dialects: [Dialect(), new JsonSchemaDialectId("urn:example:dialect")]);
        var second = Profile(dialects: [Dialect(), new JsonSchemaDialectId("urn:example:dialect")]);
        var copy = first with { };

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        copy.ShouldBe(first);
        copy.ShouldNotBeSameAs(first);
        copy.Key.ShouldBe(first.Key);
        copy.Version.ShouldBe(first.Version);
        copy.Fingerprint.ShouldBe(first.Fingerprint);
        copy.SupportedToolSchemaDialects.SequenceEqual(first.SupportedToolSchemaDialects).ShouldBeTrue();
        copy.Extensions.ShouldBe(first.Extensions);
    }

    [Fact]
    public void Equality_WhenDialectOrderDiffers_IsNotEqual()
    {
        var first = Profile(dialects: [Dialect(), new JsonSchemaDialectId("urn:example:dialect")]);
        var second = Profile(dialects: [new JsonSchemaDialectId("urn:example:dialect"), Dialect()]);

        first.ShouldNotBe(second);
    }

    private static CompatibilityProfile Profile(
        CompatibilityProfileKey? key = null,
        CompatibilityProfileVersion? version = null,
        ContentHash? fingerprint = null,
        ModelCandidateMultiplicity requestMultiplicity = ModelCandidateMultiplicity.ExactlyOne,
        ModelCandidateMultiplicity responseMultiplicity = ModelCandidateMultiplicity.ExactlyOne,
        ModelUsageReportingMode usageReporting = ModelUsageReportingMode.NotReported,
        ImmutableArray<JsonSchemaDialectId>? dialects = null,
        ExtensionData? extensions = null) =>
        new(
            key ?? new CompatibilityProfileKey("openai-chat"),
            version ?? new CompatibilityProfileVersion(1),
            fingerprint ?? new ContentHash("sha256:profile"),
            requestMultiplicity,
            responseMultiplicity,
            usageReporting,
            dialects ?? [Dialect()],
            extensions ?? ExtensionData.Empty);

    private static CompatibilityProfile Create(
        CompatibilityProfileKey key,
        CompatibilityProfileVersion version,
        ContentHash fingerprint,
        ModelCandidateMultiplicity requestMultiplicity,
        ModelCandidateMultiplicity responseMultiplicity,
        ModelUsageReportingMode usageReporting,
        ImmutableArray<JsonSchemaDialectId> dialects,
        ExtensionData extensions) =>
        new(key, version, fingerprint, requestMultiplicity, responseMultiplicity, usageReporting, dialects, extensions);

    private static CompatibilityProfileKey Key() => new("openai-chat");

    private static CompatibilityProfileVersion Version() => new(1);

    private static ContentHash Fingerprint() => new("sha256:profile");

    private static JsonSchemaDialectId Dialect() => new("https://json-schema.org/draft/2020-12/schema");
}
