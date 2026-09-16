// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

using System.Text.Json;

/// <summary>Verifies SecurityCanonicalFingerprint behavior and contracts.</summary>
public sealed class SecurityCanonicalFingerprintTests
{
    [Fact]
    public void Create_WhenPayloadHasScalarProperties_ProducesStableFingerprint()
    {
        var payload = new ScalarPayload
        {
            Flag = true,
            Octet = 7,
            Number = 42,
            When = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromSeconds(5),
            Bytes = [1, 2, 3],
            Json = default,
            Link = new Uri("https://example.test/resource"),
        };

        var first = SecurityCanonicalFingerprint.Create(payload);
        var second = SecurityCanonicalFingerprint.Create(payload);

        first.ShouldBe(second);
    }

    [Fact]
    public void Create_WhenPayloadHasParsedJsonElement_ProducesFingerprint()
    {
        using var document = JsonDocument.Parse("""{"key":"value"}""");
        var payload = new ScalarPayload
        {
            Octet = 1,
            Number = 1,
            When = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            Bytes = [],
            Json = document.RootElement,
            Link = new Uri("relative", UriKind.Relative),
        };

        var fingerprint = SecurityCanonicalFingerprint.Create(payload);

        fingerprint.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void Create_WhenGraphIsCyclic_ThrowsArgumentException()
    {
        var node = new CyclicNode();
        node.Next = node;

        _ = Should.Throw<ArgumentException>(() => SecurityCanonicalFingerprint.Create(node));
    }

    [Fact]
    public void Create_WhenDictionaryKeyIsNotString_ThrowsArgumentException()
    {
        var dictionary = new Dictionary<int, string> { [1] = "one" };

        _ = Should.Throw<ArgumentException>(() => SecurityCanonicalFingerprint.Create(dictionary));
    }

    [Fact]
    public void Create_WhenDictionaryHasStringKeys_ProducesFingerprint()
    {
        var dictionary = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };

        var fingerprint = SecurityCanonicalFingerprint.Create(dictionary);

        fingerprint.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void Create_WhenTypeHasNoCanonicalPublicState_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => SecurityCanonicalFingerprint.Create(new EmptyPayload()));

    [Fact]
    public void Create_WhenTypeHasHiddenInstanceState_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => SecurityCanonicalFingerprint.Create(new HiddenStatePayload(3)));

    private sealed class ScalarPayload
    {
        public bool Flag { get; init; }
        public byte Octet { get; init; }
        public int Number { get; init; }
        public DateTime When { get; init; }
        public TimeSpan Duration { get; init; }
        public byte[] Bytes { get; init; } = [];
        public JsonElement Json { get; init; }
        public Uri Link { get; init; } = new("https://example.test");
    }

    private sealed class CyclicNode
    {
        public CyclicNode? Next { get; set; }
    }

    private sealed class EmptyPayload;

#pragma warning disable IDE0032 // The field must remain distinct from the property to exercise hidden-state detection.
    private sealed class HiddenStatePayload
    {
        private readonly int _hidden;

        public HiddenStatePayload(int hidden) => _hidden = hidden;

        public int Visible => _hidden;
    }
#pragma warning restore IDE0032
}
