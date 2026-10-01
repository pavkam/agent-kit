// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

/// <summary>Verifies the fail-closed default redactor registered by <c>AddAgentKitObservability</c>.</summary>
public sealed class OmissionOnlyObservationRedactorTests
{
    private static readonly ObservationContent _content = new(
        ObservationContentKind.Prompt, DataClassification.Public, [1, 2, 3], new ContentFingerprint("sha256:abc"));

    private static readonly ObservationPolicy _policy = new(new ObservationBounds(64), [DataClassification.Public]);

    [Theory]
    [InlineData(DataClassification.Public)]
    [InlineData(DataClassification.Restricted)]
    public async Task RedactAsync_WhenAnyContentIsSupplied_OmitsEveryPayload(DataClassification classification)
    {
        var redactor = new OmissionOnlyObservationRedactor();
        var content = new ObservationContent(ObservationContentKind.ModelOutput, classification, [9], new ContentFingerprint("f"));

        var result = await redactor.RedactAsync(content, _policy, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ContentOmitted>();
    }

    [Fact]
    public async Task RedactAsync_WhenContentIsNull_ThrowsArgumentNullExceptionNamingContent()
    {
        var redactor = new OmissionOnlyObservationRedactor();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await redactor.RedactAsync(null!, _policy, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public async Task RedactAsync_WhenPolicyIsNull_ThrowsArgumentNullExceptionNamingPolicy()
    {
        var redactor = new OmissionOnlyObservationRedactor();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await redactor.RedactAsync(_content, null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("policy");
    }

    [Fact]
    public async Task RedactAsync_WhenTokenIsCancelled_ThrowsOperationCanceledException()
    {
        var redactor = new OmissionOnlyObservationRedactor();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await redactor.RedactAsync(_content, _policy, cts.Token));
    }
}
