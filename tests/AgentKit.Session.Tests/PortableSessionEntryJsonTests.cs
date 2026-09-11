// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Buffers;
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

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
