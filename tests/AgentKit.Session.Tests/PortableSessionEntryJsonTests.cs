// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Buffers;
using System.Collections.Frozen;
using System.Text.Json;

/// <summary>Verifies PortableSessionEntryJson behavior and contracts.</summary>
public sealed class PortableSessionEntryJsonTests
{
    [Fact]
    public void WriteGuid_WhenWriterOrIdentityIsInvalid_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentNullException>(() => PortableSessionEntryJson.WriteGuid(null!, "id", Id(1))).ParamName.ShouldBe("writer");
        using var writer = new Utf8JsonWriter(new ArrayBufferWriter<byte>());
        Should.Throw<ArgumentOutOfRangeException>(() => PortableSessionEntryJson.WriteGuid(writer, "id", default)).ParamName.ShouldBe("value");
        Should.Throw<ArgumentOutOfRangeException>(() => PortableSessionEntryJson.WriteOptionalGuid(writer, "id", Guid.Empty)).ParamName.ShouldBe("value");
    }

    [Fact]
    public void ValidateObject_WhenValueIsNotAnObject_ReturnsFalse()
    {
        using var document = JsonDocument.Parse("5");
        var count = 0;
        var bytes = 0;

        var result = PortableSessionEntryJson.ValidateObject(document.RootElement,
            FrozenSet<string>.Empty, Limits(), ref count, ref bytes);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateObject_WhenPropertyNameRepeats_ReturnsFalse()
    {
        using var document = JsonDocument.Parse("""{"a":1,"a":2}""");
        var count = 0;
        var bytes = 0;

        var result = PortableSessionEntryJson.ValidateObject(document.RootElement,
            new HashSet<string> { "a" }.ToFrozenSet(StringComparer.Ordinal), Limits(), ref count, ref bytes);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateObject_WhenUnknownFieldExceedsExtensionCount_ReturnsFalse()
    {
        using var document = JsonDocument.Parse("""{"known":1,"extra":2,"extra2":3}""");
        var count = 0;
        var bytes = 0;
        var limits = new SessionEntryCodecLimits(1_048_576, maximumExtensionCount: 1, 65_536, 16);

        var result = PortableSessionEntryJson.ValidateObject(document.RootElement,
            new HashSet<string> { "known" }.ToFrozenSet(StringComparer.Ordinal), limits, ref count, ref bytes);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateObject_WhenEveryFieldIsKnown_ReturnsTrue()
    {
        using var document = JsonDocument.Parse("""{"known":1}""");
        var count = 0;
        var bytes = 0;

        var result = PortableSessionEntryJson.ValidateObject(document.RootElement,
            new HashSet<string> { "known" }.ToFrozenSet(StringComparer.Ordinal), Limits(), ref count, ref bytes);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateObject_WhenUnknownFieldContainsArrayWithDuplicateNestedObjectKeys_ReturnsFalse()
    {
        using var document = JsonDocument.Parse("""{"known":1,"extra":[{"x":1,"x":2}]}""");
        var count = 0;
        var bytes = 0;

        var result = PortableSessionEntryJson.ValidateObject(document.RootElement,
            new HashSet<string> { "known" }.ToFrozenSet(StringComparer.Ordinal), Limits(), ref count, ref bytes);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateObject_WhenUnknownFieldContainsValidNestedStructures_ReturnsTrue()
    {
        using var document = JsonDocument.Parse("""{"known":1,"extra":[{"x":1},[2,3]]}""");
        var count = 0;
        var bytes = 0;

        var result = PortableSessionEntryJson.ValidateObject(document.RootElement,
            new HashSet<string> { "known" }.ToFrozenSet(StringComparer.Ordinal), Limits(), ref count, ref bytes);

        result.ShouldBeTrue();
    }

    private static SessionEntryCodecLimits Limits() => new(1_048_576, 64, 65_536, 16);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
