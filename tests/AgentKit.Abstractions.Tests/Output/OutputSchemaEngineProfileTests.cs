// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;
/// <summary>Verifies OutputSchemaEngineProfile behavior and contracts.</summary>
public sealed class OutputSchemaEngineProfileTests
{
    private static readonly JsonSchemaDialectId _dialect = new("urn:test:dialect");
    [Fact]
    public void OutputSchemaEngineProfile_WhenSetsDifferOnlyByOrder_HasStructuralEqualityAndHash()
    {
        var first = Profile([_dialect, new("urn:test:other")], ["type", "required"], ["title", "description"]);
        var second = Profile([new("urn:test:other"), _dialect], ["required", "type"], ["description", "title"]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.SupportedDialects.ShouldBe([_dialect, new("urn:test:other")]);
    }

    [Theory]
    [InlineData("dialects")]
    [InlineData("assertions")]
    [InlineData("annotations")]
    [InlineData("overlap")]
    [InlineData("default")]
    [InlineData("duplicateDialects")]
    public void OutputSchemaEngineProfile_WhenCapabilitySetsAreInvalid_RejectsExactSet(string invalid)
    {
        ImmutableArray<JsonSchemaDialectId> dialects = invalid switch
        {
            "dialects" => [],
            "default" => [new("urn:test:other")],
            "duplicateDialects" => [_dialect, _dialect],
            _ => [_dialect]
        };
        ImmutableArray<string> assertions = invalid switch
        {
            "assertions" => ["type", "type"],
            "overlap" => ["title"],
            _ => ["type"]
        };
        ImmutableArray<string> annotations = invalid == "annotations" ? ["title", "title"] : ["title"];
        var exception = Should.Throw<ArgumentException>(() => Profile(dialects, assertions, annotations));
        exception.ParamName.ShouldBe(invalid switch
        {
            "dialects" or "duplicateDialects" => "supportedDialects",
            "assertions" => "assertionKeywords",
            "annotations" or "overlap" => "annotationKeywords",
            _ => "defaultDialect"
        });
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Profile();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static OutputSchemaEngineProfile Profile(ImmutableArray<JsonSchemaDialectId>? dialects = null, ImmutableArray<string>? assertions = null, ImmutableArray<string>? annotations = null) => new(new OutputSchemaProfileId("test"), new OutputSchemaProfileVersion(1), _dialect, dialects ?? [_dialect], assertions ?? ["type"], annotations ?? ["title"]);
}
