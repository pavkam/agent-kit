// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Freezes additive session-entry codecs for exact local-type and wire dispatch.</summary>
public sealed class SessionEntryCodecCatalog: ISessionEntryCodecCatalog
{
    private readonly FrozenDictionary<Type, SessionEntryCodecBinding> _writers;
    private readonly FrozenDictionary<SessionEntryTypeId, SessionEntryCodecBinding> _readers;
    private readonly int _maximumPayloadBytes;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SessionEntryCodecCatalog> _logger;

    /// <summary>Captures and validates the complete codec registration set once.</summary>
    /// <param name="codecs">The non-null additive codec set.</param>
    /// <param name="timeProvider">The required clock used only for best-effort duration observation.</param>
    /// <param name="options">Optional process-wide payload limits; when omitted, the documented one-mebibyte limit applies.</param>
    /// <param name="logger">Optional structured logger; when omitted, logging is disabled.</param>
    /// <exception cref="ArgumentNullException"><paramref name="codecs"/>, <paramref name="timeProvider"/>, or an element is null.</exception>
    /// <exception cref="ArgumentException">Two codecs claim the same local type or wire identity.</exception>
    public SessionEntryCodecCatalog(IEnumerable<ISessionEntryCodec> codecs, TimeProvider timeProvider,
        SessionEntryCodecCatalogOptions? options = null, ILogger<SessionEntryCodecCatalog>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options ??= new SessionEntryCodecCatalogOptions(1_048_576);
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionEntryCodecCatalog>.Instance;
        _maximumPayloadBytes = options.MaximumPayloadBytes;
        var materialized = codecs.ToImmutableArray();
        ArgumentException.ThrowIfContainsNull(materialized, nameof(codecs));
        var bindings = ImmutableArray.CreateBuilder<SessionEntryCodecBinding>(materialized.Length);
        foreach (var codec in materialized)
        {
            var descriptor = codec.Descriptor;
            ArgumentException.ThrowIfNullSessionEntryCodecDescriptor(descriptor, nameof(codecs));
            bindings.Add(new SessionEntryCodecBinding(codec, descriptor));
        }

        var captured = bindings.MoveToImmutable();
        ArgumentException.ThrowIfDuplicateSessionEntryCodecBindings(captured, nameof(codecs));
        _writers = captured.ToFrozenDictionary(static binding => binding.Descriptor.EntryType);
        _readers = captured.ToFrozenDictionary(static binding => binding.Descriptor.TypeId);
    }

    /// <summary>Encodes an entry through its exact registered runtime type.</summary>
    /// <param name="entry">The non-null entry to encode.</param>
    /// <returns>A typed rejection when no codec owns the exact runtime type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public SessionEntryEncodeResult Encode(SessionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Observe("encode", () => EncodeCore(entry));
    }

    private SessionEntryEncodeResult EncodeCore(SessionEntry entry)
    {
        Debug.Assert(entry is not null, "Public validation guarantees a non-null entry.");
        if (_writers.GetValueOrDefault(entry.GetType()) is not { } binding)
        {
            return new SessionEntryEncodeRejected("No codec is registered for the entry's exact runtime type.");
        }

        var result = binding.Codec.Encode(entry);
        return result switch
        {
            SessionEntryEncoded { Wire: { } wire }
                when wire.TypeId == binding.Descriptor.TypeId
                && wire.SchemaVersion == binding.Descriptor.WriteVersion
                && wire.Payload.Length <= Math.Min(_maximumPayloadBytes, binding.Descriptor.Limits.MaximumPayloadBytes) => result,
            SessionEntryEncodeRejected => result,
            _ => new SessionEntryEncodeRejected("The codec returned an invalid encoding result."),
        };
    }

    /// <summary>Decodes an envelope through its exact wire identity and declared schema.</summary>
    /// <param name="wire">The non-null envelope to decode.</param>
    /// <returns>Opaque data when no codec owns its type or exact schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wire"/> is null.</exception>
    public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return Observe("decode", () => DecodeCore(wire));
    }

    private SessionEntryDecodeResult DecodeCore(SessionEntryWireEnvelope wire)
    {
        Debug.Assert(wire is not null, "Public validation guarantees a non-null wire envelope.");
        if (wire.Payload.Length > _maximumPayloadBytes)
        {
            return new SessionEntryDecodeRejected("The entry payload exceeds the catalog limit.");
        }
        if (_readers.GetValueOrDefault(wire.TypeId) is not { } binding
            || !binding.Descriptor.ReadableVersions.Contains(wire.SchemaVersion))
        {
            return new SessionEntryOpaque(wire);
        }

        if (wire.Payload.Length > Math.Min(_maximumPayloadBytes, binding.Descriptor.Limits.MaximumPayloadBytes))
        {
            return new SessionEntryDecodeRejected("The entry payload exceeds the codec limit.");
        }

        var result = binding.Codec.Decode(wire);
        return result switch
        {
            SessionEntryDecoded { Decoded: { } decoded }
                when ReferenceEquals(decoded.Wire, wire)
                && decoded.Entry.GetType() == binding.Descriptor.EntryType
                && decoded.Entry.SchemaVersion == wire.SchemaVersion => result,
            SessionEntryDecodeRejected => result,
            _ => new SessionEntryDecodeRejected("The codec returned an invalid decoding result."),
        };
    }

    private T Observe<T>(string operation, Func<T> action)
    {
        Debug.Assert(!string.IsNullOrEmpty(operation), "The caller supplies a bounded operation name.");
        Debug.Assert(action is not null, "The caller supplies a semantic operation delegate.");
        var started = TryGetTimestamp();
        var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.SessionEntryCodec,
            ActivityKind.Internal,
            [new KeyValuePair<string, object?>(AgentKitTagNames.SessionEntryCodecOperation, operation)]);
        var activity = activityScope.Activity;
        var outcome = "faulted";
        try
        {
            var result = action();
            outcome = ResultOutcome(result);
            TryObserve(() => activity?.SetTag(AgentKitTagNames.Outcome, outcome));
            TryObserve(() => activity?.SetStatus(outcome == "rejected" ? ActivityStatusCode.Error : ActivityStatusCode.Ok));
            TryObserve(() => SessionLog.CodecCompleted(_logger, operation, outcome));
            return result;
        }
        catch (Exception exception)
        {
            TryObserve(() => activity?.SetTag(AgentKitTagNames.Outcome, outcome));
            TryObserve(() => activity?.SetTag(AgentKitTagNames.ErrorType, exception.GetType().Name));
            TryObserve(() => activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name));
            TryObserve(() => SessionLog.CodecFaulted(_logger, operation, exception.GetType().Name));
            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { AgentKitTagNames.SessionEntryCodecOperation, operation },
                { AgentKitTagNames.Outcome, outcome },
            };
            TryObserve(() => SessionEntryCodecMetrics.Operations.Add(1, tags));
            if (started is { } timestamp && TryGetElapsedSeconds(timestamp) is { } elapsedSeconds)
            {
                TryObserve(() => SessionEntryCodecMetrics.Duration.Record(elapsedSeconds, tags));
            }

            activityScope.Dispose();
        }
    }

    private static string ResultOutcome<T>(T result) => result switch { SessionEntryEncoded => "encoded", SessionEntryEncodeRejected => "rejected", SessionEntryDecoded => "decoded", SessionEntryOpaque => "opaque", SessionEntryDecodeRejected => "rejected", _ => "unknown" };

    private long? TryGetTimestamp()
    {
        long? timestamp = null;
        TryObserve(() => timestamp = _timeProvider.GetTimestamp());
        return timestamp;
    }

    private double? TryGetElapsedSeconds(long started)
    {
        double? elapsedSeconds = null;
        TryObserve(() => elapsedSeconds = _timeProvider.GetElapsedTime(started).TotalSeconds);
        return elapsedSeconds;
    }

    private static void TryObserve(Action action) { try { action(); } catch { } }
}
