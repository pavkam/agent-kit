// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies InputPayloadFingerprint behavior and contracts.</summary>
public sealed class InputPayloadFingerprintTests
{
    [Fact]
    public void Create_WhenInputIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => InputPayloadFingerprint.Create(null!)).ParamName.ShouldBe("input");

    [Fact]
    public void Create_WhenPayloadsAreStructurallyEqual_ProducesTheSameDigest() =>
        InputPayloadFingerprint.Create(Payload()).ShouldBe(InputPayloadFingerprint.Create(Payload()));

    [Fact]
    public void Create_WhenAnyRetainedContentDiffers_ProducesADifferentDigest()
    {
        var baseline = InputPayloadFingerprint.Create(Payload());

        InputPayloadFingerprint.Create(Payload(text: "changed")).ShouldNotBe(baseline);
        InputPayloadFingerprint.Create(Payload(id: 2)).ShouldNotBe(baseline);
        InputPayloadFingerprint.Create(Payload(delivery: InputDelivery.FollowUp)).ShouldNotBe(baseline);
    }

    [Fact]
    public void Create_WhenPartOrderDiffers_ProducesADifferentDigest()
    {
        var forward = new AgentInput(Input(1), InputDelivery.Steer, [Part("first"), Part("second")], ExtensionData.Empty);
        var reversed = new AgentInput(Input(1), InputDelivery.Steer, [Part("second"), Part("first")], ExtensionData.Empty);

        InputPayloadFingerprint.Create(forward).ShouldNotBe(InputPayloadFingerprint.Create(reversed));
    }

    [Fact]
    public void Create_WhenExtensionEvidenceDiffers_ProducesADifferentDigest()
    {
        var extensions = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty
            .Add("key", new ExtensionValue([.. "1"u8])));
        var withExtensions = new AgentInput(Input(1), InputDelivery.Steer, [Part("text")], extensions);

        InputPayloadFingerprint.Create(withExtensions).ShouldNotBe(InputPayloadFingerprint.Create(Payload(text: "text")));
    }

    [Fact]
    public void Create_WhenDigestIsProduced_IsAlgorithmQualified() =>
        InputPayloadFingerprint.Create(Payload()).Value.ShouldStartWith("sha256:");

    private static AgentInput Payload(long id = 1, string text = "input", InputDelivery delivery = InputDelivery.Steer) =>
        new(Input(id), delivery, [Part(text)], ExtensionData.Empty);

    private static TextPart Part(string text) => new(text, TextSemantics.Plain, ExtensionData.Empty);

    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
}
