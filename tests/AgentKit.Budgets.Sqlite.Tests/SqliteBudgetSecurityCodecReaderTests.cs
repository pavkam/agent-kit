// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using System.Buffers.Binary;

/// <summary>Verifies SqliteBudgetSecurityCodecReader behavior and contracts.</summary>
public sealed class SqliteBudgetSecurityCodecReaderTests
{
    private static readonly SqliteBudgetLedgerSettings _settings = SqliteBudgetLedgerSettings.CreateDefault();

    /// <summary>Proves an unsupported magic, version, or kind is rejected as an unsupported envelope.</summary>
    [Fact]
    public void ReadHeader_WhenEnvelopeIsUnsupported_ThrowsInvalidData()
    {
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(payload, 0x414B5347);
        payload[4] = 1;
        payload[5] = 99;

        var reader = new SqliteBudgetSecurityCodecReader(payload, _settings);
        var threw = false;
        try
        {
            reader.ReadHeader(2);
        }
        catch (InvalidDataException)
        {
            threw = true;
        }

        threw.ShouldBeTrue();
    }

    /// <summary>Proves a declared string length exceeding the remaining bytes is rejected as truncated evidence.</summary>
    [Fact]
    public void ReadString_WhenDeclaredLengthExceedsRemainingBytes_ThrowsInvalidData()
    {
        var payload = new byte[4 + 2];
        BinaryPrimitives.WriteInt32BigEndian(payload, 5);

        var reader = new SqliteBudgetSecurityCodecReader(payload, _settings);
        var threw = false;
        try
        {
            _ = reader.ReadString();
        }
        catch (InvalidDataException)
        {
            threw = true;
        }

        threw.ShouldBeTrue();
    }

    /// <summary>Proves an unsupported correlation discriminator byte is rejected.</summary>
    [Fact]
    public void ReadScope_WhenCorrelationDiscriminatorIsUnsupported_ThrowsInvalidData()
    {
        var payload = new byte[16 + 1 + 1];
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(0), 0);
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(8), 1);
        payload[16] = 0;
        payload[17] = 9;

        var reader = new SqliteBudgetSecurityCodecReader(payload, _settings);
        var threw = false;
        try
        {
            _ = reader.ReadScope();
        }
        catch (InvalidDataException)
        {
            threw = true;
        }

        threw.ShouldBeTrue();
    }

    /// <summary>Proves an empty protected-resources count is rejected.</summary>
    [Fact]
    public void ReadResources_WhenCountIsZero_ThrowsInvalidData()
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, 0);

        var reader = new SqliteBudgetSecurityCodecReader(payload, _settings);
        var threw = false;
        try
        {
            _ = reader.ReadResources();
        }
        catch (InvalidDataException)
        {
            threw = true;
        }

        threw.ShouldBeTrue();
    }

    /// <summary>Proves an invalid encoded boolean byte is rejected.</summary>
    [Fact]
    public void ReadAuthorization_WhenPresenceFlagIsInvalid_ThrowsInvalidData()
    {
        var payload = new byte[] { 2 };

        var reader = new SqliteBudgetSecurityCodecReader(payload, _settings);
        var threw = false;
        try
        {
            _ = reader.ReadAuthorization();
        }
        catch (InvalidDataException)
        {
            threw = true;
        }

        threw.ShouldBeTrue();
    }
}
