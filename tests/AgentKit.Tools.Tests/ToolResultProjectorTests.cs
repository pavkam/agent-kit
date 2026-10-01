// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Immutable;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolResultProjector"/> maps terminal content into bounded model-visible parts, including artifact references.</summary>
public sealed class ToolResultProjectorTests
{
    [Fact]
    public async Task ProjectAsync_WhenContentHoldsAnArtifactReference_RendersABoundedPointerAndRecordsExternalization()
    {
        var reference = Reference();
        var result = Result([new ToolResultTextContent("preview", TextSemantics.Plain, ExtensionData.Empty), new ToolResultArtifactContent(reference, ExtensionData.Empty)]);

        var part = await new ToolResultProjector().ProjectAsync(result, Policy(result), TestContext.Current.CancellationToken);

        part.Content.Length.ShouldBe(2);
        part.Content[0].ShouldBeOfType<TextPart>().Text.ShouldBe("preview");
        var pointer = part.Content[1].ShouldBeOfType<TextPart>().Text;
        pointer.ShouldContain(reference.Id.ToString());
        pointer.ShouldContain("version 1");
        pointer.ShouldContain("1234 bytes");
        pointer.ShouldContain(reference.Integrity.ContentHash.Value);
        pointer.Length.ShouldBeLessThan(512);
        part.Projection.Losses.ShouldBe([ToolResultProjectionLoss.Externalized]);
    }

    [Fact]
    public async Task ProjectAsync_WhenContentIsPlainText_RecordsNoProjectionLoss()
    {
        var result = Result([new ToolResultTextContent("ok", TextSemantics.Plain, ExtensionData.Empty)]);

        var part = await new ToolResultProjector().ProjectAsync(result, Policy(result), TestContext.Current.CancellationToken);

        part.Projection.Losses.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProjectAsync_WhenArgumentsAreNull_ThrowsNamingThem()
    {
        var result = Result([]);
        var projector = new ToolResultProjector();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await projector.ProjectAsync(null!, Policy(result)))).ParamName.ShouldBe("result");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await projector.ProjectAsync(result, null!))).ParamName.ShouldBe("policy");
    }

    private static ToolResultProjectionPolicySnapshot Policy(ToolCallResult result) =>
        new(result.ProjectionPolicy, new ToolResultProjectionBounds(65_536, 64), ToolResultProjectionTransformations.Externalization, ExtensionData.Empty);

    private static ToolCallResult Result(ImmutableArray<ToolResultContent> content)
    {
        var normalization = ToolRuntimeNormalizationDefaults.ForResolvedTool(Standard);
        var acceptance = new ToolCallAcceptanceEvidence(
            new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new InputFingerprint("sha256:test"), DateTimeOffset.UnixEpoch);
        return new ToolCallResult(
            AgentId, SessionId, RunId, TurnId, OperationId, new ToolCallId(Guid.Parse("66666666-6666-6666-6666-666666666661")), Authorization(),
            acceptance.InvocationGrantId, acceptance, new ToolAlias("read"), new ToolId("tool.read"), new ToolVersion("1"),
            new ToolEffects(ToolEffect.ReadOnly, null, null), externalIdempotencyKey: null,
            new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog-1"), 0, ToolInvocationSecurityBinding.RawAdmissionFingerprint([])),
            ToolTerminalStatus.Succeeded, content, error: null, SideEffectCertainty.DefinitelyPerformed, usage: null, retryable: false,
            normalization, new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty), normalization.ProjectionPolicy,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, ExtensionData.Empty);
    }

    private static ArtifactReference Reference() => new(
        new ArtifactId(Guid.Parse("a0000000-0000-0000-0000-0000000000a1")), new ArtifactVersion("1"), new ArtifactDirectoryId("tool-results"),
        new ArtifactProfileKey("artifacts"), new ArtifactProfileVersion(1), new TenantId("tenant"), new ArtifactOwnerId("session:test"),
        new PrincipalId("principal"), "text/plain", 1234, new ArtifactIntegrity(FileSecurityBinding.ContentFingerprint("x"u8), DateTimeOffset.UnixEpoch),
        DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null, DateTimeOffset.UnixEpoch);
}
