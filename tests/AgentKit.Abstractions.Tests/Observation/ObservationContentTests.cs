// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationContent"/> validation and ownership semantics.</summary>
public sealed class ObservationContentTests
{
    private static readonly ContentFingerprint _fingerprint = new("sha256:abc");

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeExceptionNamingKind()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ObservationContent((ObservationContentKind) 99, DataClassification.Public, [1], _fingerprint));

        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsArgumentOutOfRangeExceptionNamingClassification()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ObservationContent(ObservationContentKind.Prompt, (DataClassification) 99, [1], _fingerprint));

        exception.ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenPayloadIsDefaultArray_ThrowsArgumentExceptionNamingValue()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ObservationContent(ObservationContentKind.Prompt, DataClassification.Public, default, _fingerprint));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenFingerprintIsDefault_ThrowsArgumentExceptionNamingFingerprint()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ObservationContent(ObservationContentKind.Prompt, DataClassification.Public, [1], default));

        exception.ParamName.ShouldBe("fingerprint");
    }

    [Fact]
    public void Constructor_WhenPayloadIsEmpty_AcceptsEmptyContent()
    {
        var content = new ObservationContent(ObservationContentKind.Reasoning, DataClassification.Restricted, [], _fingerprint);

        content.Value.ShouldBeEmpty();
        content.Kind.ShouldBe(ObservationContentKind.Reasoning);
        content.Classification.ShouldBe(DataClassification.Restricted);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesEveryComponent()
    {
        var content = new ObservationContent(ObservationContentKind.ToolResult, DataClassification.Confidential, [1, 2, 3], _fingerprint);

        content.Kind.ShouldBe(ObservationContentKind.ToolResult);
        content.Classification.ShouldBe(DataClassification.Confidential);
        content.Value.SequenceEqual((byte[]) [1, 2, 3]).ShouldBeTrue();
        content.Fingerprint.ShouldBe(_fingerprint);
    }
}
