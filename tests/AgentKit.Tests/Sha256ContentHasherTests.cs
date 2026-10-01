// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Internal;

/// <summary>Verifies <see cref="Sha256ContentHasher"/> behavior.</summary>
public sealed class Sha256ContentHasherTests
{
    private static readonly CanonicalizationProfileId _canonicalization = new("test.canonical");
    private static readonly CanonicalizationProfileVersion _version = new(2);

    // SHA-256 of the three ASCII bytes "abc" (FIPS 180-4 example vector).
    private const string AbcDigest = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Algorithm_WhenRead_IdentifiesSha256VersionOne()
    {
        var hasher = new Sha256ContentHasher();

        hasher.Algorithm.ShouldBe(new ContentHashAlgorithmId("sha256"));
        hasher.AlgorithmVersion.ShouldBe(new ContentHashAlgorithmVersion("1"));
    }

    [Fact]
    public void Compute_WhenContentIsTheKnownVector_ProducesTheSelfDescribingDigest()
    {
        var hash = new Sha256ContentHasher().Compute("abc"u8, _canonicalization, _version);

        hash.Value.ShouldBe($"sha256/1/test.canonical/2:{AbcDigest}");
    }

    [Fact]
    public void Compute_WhenContentIsEmpty_HashesTheEmptyInput()
    {
        var hash = new Sha256ContentHasher().Compute([], _canonicalization, _version);

        hash.Value.ShouldEndWith(":e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void Compute_WhenCanonicalizationOrVersionDiffers_ProducesADifferentHash()
    {
        var hasher = new Sha256ContentHasher();
        var baseline = hasher.Compute("abc"u8, _canonicalization, _version);

        hasher.Compute("abc"u8, new CanonicalizationProfileId("other"), _version).ShouldNotBe(baseline);
        hasher.Compute("abc"u8, _canonicalization, new CanonicalizationProfileVersion(3)).ShouldNotBe(baseline);
    }

    [Fact]
    public void Compute_WhenCanonicalizationIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new Sha256ContentHasher().Compute("abc"u8, default, _version)).ParamName.ShouldBe("canonicalization");

    [Fact]
    public void Compute_WhenVersionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new Sha256ContentHasher().Compute("abc"u8, _canonicalization, default)).ParamName.ShouldBe("canonicalizationVersion");

    [Fact]
    public async Task ComputeAsync_WhenStreamHoldsTheSameBytes_MatchesTheInMemoryHashWithoutDisposingTheStream()
    {
        var hasher = new Sha256ContentHasher();
        await using var stream = new MemoryStream("abc"u8.ToArray());

        var streamed = await hasher.ComputeAsync(stream, _canonicalization, _version, TestContext.Current.CancellationToken);

        streamed.ShouldBe(hasher.Compute("abc"u8, _canonicalization, _version));
        stream.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task ComputeAsync_WhenStreamIsNull_ThrowsExactParameter() =>
        (await Should.ThrowAsync<ArgumentNullException>(async () => await new Sha256ContentHasher().ComputeAsync(null!, _canonicalization, _version, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("canonicalContent");

    [Fact]
    public async Task ComputeAsync_WhenCancellationIsAlreadyRequested_Throws()
    {
        await using var stream = new MemoryStream("abc"u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await new Sha256ContentHasher().ComputeAsync(stream, _canonicalization, _version, cancellation.Token));
    }

    [Fact]
    public async Task ComputeAsync_WhenCanonicalizationIsDefault_ThrowsExactParameter()
    {
        await using var stream = new MemoryStream("abc"u8.ToArray());

        (await Should.ThrowAsync<ArgumentException>(async () =>
            await new Sha256ContentHasher().ComputeAsync(stream, default, _version, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("canonicalization");
    }
}
