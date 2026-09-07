// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.EventStream;

/// <summary>
/// Computes the standard CRC-32 (IEEE 802.3, also known as GZIP CRC-32)
/// checksum used by the AWS event stream binary encoding for its prelude
/// and message checksums.
/// </summary>
internal static class Crc32
{
    private const uint _polynomial = 0xEDB88320;

    private static readonly uint[] _table = BuildTable();

    /// <summary>Computes the CRC-32 checksum of the given bytes.</summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The CRC-32 checksum.</returns>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? _polynomial ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
