// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Text;

using Microsoft.Extensions.Options;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolResultNormalizer"/> bounds, truncation, and optional externalization of oversized content.</summary>
public sealed class ToolResultNormalizerTests
{
    private static readonly ToolResultNormalizationAlgorithmVersion _algorithm = new(1);

    [Fact]
    public async Task NormalizeAsync_WhenContentFitsTheBound_RetainsItWithoutTransformation()
    {
        var normalizer = Create();

        var result = await normalizer.NormalizeAsync(Validated(), Success("hello"), Snapshot(), new ThrowingSpill(), TestContext.Current.CancellationToken);

        var normalized = result.ShouldBeOfType<ToolResultNormalized>();
        normalized.Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultTextContent>().Text.ShouldBe("hello");
        normalized.Info.Transformations.ShouldBeEmpty();
    }

    [Fact]
    public async Task NormalizeAsync_WhenOversizedAndNoSpillIsSupplied_TruncatesAndRecordsIt()
    {
        var normalizer = Create(maximumBytes: 8);

        var result = await normalizer.NormalizeAsync(Validated(), Success(new string('a', 40)), Snapshot(), spill: null, TestContext.Current.CancellationToken);

        var normalized = result.ShouldBeOfType<ToolResultNormalized>();
        normalized.Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultTextContent>().Text.ShouldBe("aaaaaaaa");
        normalized.Info.Transformations.ShouldBe([ToolResultNormalizationTransformation.Truncated]);
        normalized.Info.OmittedCanonicalBytes.ShouldBe(32);
    }

    [Fact]
    public async Task NormalizeAsync_WhenOversizedAndSpillStores_ReturnsPreviewAndReferenceAndRecordsBothTransformations()
    {
        var reference = Reference();
        var spill = new ScriptedSpill(new ToolResultSpilled(reference));
        var normalizer = Create(maximumBytes: 64, previewBytes: 10);
        var call = Validated();

        var result = await normalizer.NormalizeAsync(call, Success(new string('a', 40) + new string('b', 40)), Snapshot(), spill, TestContext.Current.CancellationToken);

        var normalized = result.ShouldBeOfType<ToolResultNormalized>();
        normalized.Content.Length.ShouldBe(2);
        normalized.Content[0].ShouldBeOfType<ToolResultTextContent>().Text.ShouldBe("aaaaaaaaaa");
        normalized.Content[1].ShouldBeOfType<ToolResultArtifactContent>().Reference.ShouldBe(reference);
        normalized.Info.Transformations.ShouldBe([ToolResultNormalizationTransformation.Truncated, ToolResultNormalizationTransformation.Externalized]);
        normalized.Info.InputCanonicalBytes.ShouldBe(80);
        normalized.Info.OmittedCanonicalBytes.ShouldBe(70);
        var request = spill.Requests.ShouldHaveSingleItem();
        request.Call.ShouldBe(call);
        Encoding.UTF8.GetString([.. request.Content]).ShouldBe(new string('a', 40) + new string('b', 40));
    }

    [Fact]
    public async Task NormalizeAsync_WhenSeveralTextPartsAreSpilled_StoresTheirExactConcatenationAndKeepsTheFirstPartSemanticsForThePreview()
    {
        var spill = new ScriptedSpill(new ToolResultSpilled(Reference()));
        var normalizer = Create(maximumBytes: 16, previewBytes: 6);
        var invocation = new ToolInvocationResult(
            Success("").Outcome,
            [
                new TextPart("first-part-", TextSemantics.Code, ExtensionData.Empty),
                new TextPart("second-part", TextSemantics.Plain, ExtensionData.Empty),
            ]);

        var result = await normalizer.NormalizeAsync(Validated(), invocation, Snapshot(), spill, TestContext.Current.CancellationToken);

        var preview = result.ShouldBeOfType<ToolResultNormalized>().Content[0].ShouldBeOfType<ToolResultTextContent>();
        preview.Text.ShouldBe("first-");
        preview.Semantics.ShouldBe(TextSemantics.Code);
        Encoding.UTF8.GetString([.. spill.Requests.Single().Content]).ShouldBe("first-part-second-part");
    }

    [Fact]
    public async Task NormalizeAsync_WhenThePreviewBudgetEndsInsideAMultiByteCharacter_CutsOnAWholeCharacter()
    {
        var spill = new ScriptedSpill(new ToolResultSpilled(Reference()));
        var normalizer = Create(maximumBytes: 16, previewBytes: 5);

        var result = await normalizer.NormalizeAsync(Validated(), Success(new string('\u00e9', 20)), Snapshot(), spill, TestContext.Current.CancellationToken);

        var preview = result.ShouldBeOfType<ToolResultNormalized>().Content[0].ShouldBeOfType<ToolResultTextContent>().Text;
        preview.ShouldBe(new string('\u00e9', 2));
        Encoding.UTF8.GetByteCount(preview).ShouldBe(4);
    }

    [Fact]
    public async Task NormalizeAsync_WhenThePreviewBudgetIsZero_KeepsOnlyTheReference()
    {
        var spill = new ScriptedSpill(new ToolResultSpilled(Reference()));
        var normalizer = Create(maximumBytes: 16, previewBytes: 0);

        var result = await normalizer.NormalizeAsync(Validated(), Success(new string('a', 40)), Snapshot(), spill, TestContext.Current.CancellationToken);

        var normalized = result.ShouldBeOfType<ToolResultNormalized>();
        _ = normalized.Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultArtifactContent>();
        normalized.Info.OmittedCanonicalBytes.ShouldBe(40);
    }

    [Fact]
    public async Task NormalizeAsync_WhenTheSpillRefuses_FallsBackToTruncation()
    {
        var spill = new ScriptedSpill(new ToolResultNotSpilled("refused"));
        var normalizer = Create(maximumBytes: 8);

        var result = await normalizer.NormalizeAsync(Validated(), Success(new string('a', 40)), Snapshot(), spill, TestContext.Current.CancellationToken);

        var normalized = result.ShouldBeOfType<ToolResultNormalized>();
        normalized.Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultTextContent>().Text.ShouldBe("aaaaaaaa");
        normalized.Info.Transformations.ShouldBe([ToolResultNormalizationTransformation.Truncated]);
        _ = spill.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task NormalizeAsync_WhenTheSnapshotDoesNotPermitExternalization_NeverCallsTheSpill()
    {
        var normalizer = Create(maximumBytes: 8);

        var result = await normalizer.NormalizeAsync(
            Validated(), Success(new string('a', 40)), Snapshot(ToolResultProjectionTransformations.Truncation), new ThrowingSpill(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolResultNormalized>().Info.Transformations.ShouldBe([ToolResultNormalizationTransformation.Truncated]);
    }

    [Fact]
    public async Task NormalizeAsync_WhenContentHoldsANonTextPart_NeverCallsTheSpill()
    {
        var normalizer = Create(maximumBytes: 8);
        var invocation = new ToolInvocationResult(
            Success("").Outcome,
            [
                new TextPart(new string('a', 40), TextSemantics.Plain, ExtensionData.Empty),
                new UnknownContentPart("vendor.thing", JsonDocument.Parse("{}").RootElement, ExtensionData.Empty),
            ]);

        var result = await normalizer.NormalizeAsync(Validated(), invocation, Snapshot(), new ThrowingSpill(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolResultNormalized>().Info.Transformations.ShouldBe([ToolResultNormalizationTransformation.Truncated]);
    }

    [Fact]
    public async Task NormalizeAsync_WhenTheInvocationFailed_RetainsNoContentAndNeverCallsTheSpill()
    {
        var failed = new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, retryable: false, "failed", ExtensionData.Empty),
            [new TextPart(new string('a', 40), TextSemantics.Plain, ExtensionData.Empty)]);

        var result = await Create(maximumBytes: 8).NormalizeAsync(Validated(), failed, Snapshot(), new ThrowingSpill(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolResultNormalized>().Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task NormalizeAsync_WhenAnArgumentIsNull_ThrowsNamingIt()
    {
        var normalizer = Create();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await normalizer.NormalizeAsync(null!, Success("x"), Snapshot()))).ParamName.ShouldBe("validatedCall");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await normalizer.NormalizeAsync(Validated(), null!, Snapshot()))).ParamName.ShouldBe("invocation");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await normalizer.NormalizeAsync(Validated(), Success("x"), null!))).ParamName.ShouldBe("snapshot");
    }

    private static ToolResultNormalizer Create(int maximumBytes = 4_194_304, int previewBytes = 2_048) =>
        new(Options.Create(new ToolRuntimeOptions { MaximumResultBytes = maximumBytes, ResultSpillPreviewBytes = previewBytes }));

    private static ToolResultNormalizationSnapshot Snapshot(
        ToolResultProjectionTransformations allowed = ToolResultProjectionTransformations.Truncation | ToolResultProjectionTransformations.Externalization) =>
        new(
            new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("reject"), new ToolResultRejectionPolicyVersion(1)),
            ToolResultProjectionPolicyReference.Default,
            Standard,
            _algorithm,
            new ToolResultBounds(1_048_576, 64),
            allowed,
            ExtensionData.Empty);

    private static ToolInvocationResult Success(string text) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
        text.Length == 0 ? [] : [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ArtifactReference Reference() => new(
        new ArtifactId(Guid.Parse("a0000000-0000-0000-0000-0000000000a1")), new ArtifactVersion("1"), new ArtifactDirectoryId("tool-results"),
        new ArtifactProfileKey("artifacts"), new ArtifactProfileVersion(1), new TenantId("tenant"), new ArtifactOwnerId("session:test"),
        new PrincipalId("principal"), "text/plain", 80, new ArtifactIntegrity(FileSecurityBinding.ContentFingerprint("x"u8), DateTimeOffset.UnixEpoch),
        DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null, DateTimeOffset.UnixEpoch);

    private sealed class ScriptedSpill(ToolResultSpillResult outcome): IToolResultSpill
    {
        public List<ToolResultSpillRequest> Requests { get; } = [];

        public ValueTask<ToolResultSpillResult> SpillAsync(ToolResultSpillRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(outcome);
        }
    }

    private sealed class ThrowingSpill: IToolResultSpill
    {
        public ValueTask<ToolResultSpillResult> SpillAsync(ToolResultSpillRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The spill must not be consulted.");
    }
}
