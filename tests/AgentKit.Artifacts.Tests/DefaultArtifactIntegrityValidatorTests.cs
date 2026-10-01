// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="DefaultArtifactIntegrityValidator"/> length and SHA-256 validation.</summary>
public sealed class DefaultArtifactIntegrityValidatorTests
{
    private static readonly byte[] _bytes = "integrity"u8.ToArray();

    [Fact]
    public void Algorithm_WhenRead_IsSha256() => new DefaultArtifactIntegrityValidator().Algorithm.ShouldBe("sha256");

    [Fact]
    public async Task ValidateAsync_WhenLengthAndHashMatch_ReturnsTheObservedFingerprint()
    {
        var result = await new DefaultArtifactIntegrityValidator().ValidateAsync(
            _bytes, _bytes.Length, FileSecurityBinding.ContentFingerprint(_bytes), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactIntegrityVerified>().ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(_bytes));
    }

    [Fact]
    public async Task ValidateAsync_WhenNoHashIsDeclared_StillComputesTheObservedFingerprint()
    {
        var result = await new DefaultArtifactIntegrityValidator().ValidateAsync(_bytes, _bytes.Length, null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactIntegrityVerified>().ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(_bytes));
    }

    [Theory]
    [InlineData("length")]
    [InlineData("hash")]
    public async Task ValidateAsync_WhenTheDeclarationDiffers_RejectsWithAnIntegrityMismatch(string defect)
    {
        var result = await new DefaultArtifactIntegrityValidator().ValidateAsync(
            _bytes,
            defect == "length" ? _bytes.Length + 1 : _bytes.Length,
            defect == "hash" ? new ContentHash("sha256:other") : FileSecurityBinding.ContentFingerprint(_bytes),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactIntegrityRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.IntegrityMismatch);
    }

    [Fact]
    public async Task ValidateAsync_WhenContentIsEmpty_ValidatesTheEmptyFingerprint()
    {
        var result = await new DefaultArtifactIntegrityValidator().ValidateAsync(
            ReadOnlyMemory<byte>.Empty, 0, FileSecurityBinding.ContentFingerprint([]), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ArtifactIntegrityVerified>();
    }

    [Fact]
    public async Task ValidateAsync_WhenTheLengthIsNegative_ThrowsNamingIt() =>
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await new DefaultArtifactIntegrityValidator().ValidateAsync(
            _bytes, -1, null, TestContext.Current.CancellationToken))).ParamName.ShouldBe("declaredLength");

    [Fact]
    public async Task ValidateAsync_WhenCancelled_ThrowsBeforeHashing()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await new DefaultArtifactIntegrityValidator().ValidateAsync(_bytes, _bytes.Length, null, cancelled.Token));
    }
}
