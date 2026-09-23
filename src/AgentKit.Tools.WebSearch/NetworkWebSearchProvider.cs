// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

using System.Net.Http.Headers;
using System.Text.Json;

/// <summary>
/// Executes bounded search requests against one configured HTTPS endpoint using a direct HTTP client after
/// re-validating the tool-issued egress grant.
/// </summary>
/// <remarks>
/// This provider validates the <see cref="WebSearchSecurityBinding"/> fingerprint carried on the grant. Hosts that
/// require full <see cref="INetworkTransport"/> enforcement should register a custom <see cref="IWebSearchProvider"/>
/// until search send binding is unified with the network stack.
/// </remarks>
public sealed class NetworkWebSearchProvider: IWebSearchProvider, IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Initializes the provider over one configured endpoint.</summary>
    /// <param name="httpClient">The nonnull client used for exactly one search service.</param>
    /// <param name="options">The validated host options.</param>
    /// <param name="timeProvider">The replaceable clock used for deadline checks.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The endpoint is missing or not HTTPS.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A bound is invalid.</exception>
    public NetworkWebSearchProvider(
        HttpClient httpClient,
        NetworkWebSearchProviderOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ValidateOptions(options);
        HttpClient = httpClient;
        Clock = timeProvider;
        ProviderIdentity = options.ProviderId;
        ServiceEndpoint = options.Endpoint!;
        ResponseByteLimit = options.MaximumResponseBytes;
        ServiceDestination = new ProtectedResource(
            ProtectedResourceKind.NetworkEndpoint,
            ServiceEndpoint.GetLeftPart(UriPartial.Path));
        EffectAudience = new ComponentId("agentkit.tools.websearch.network");
    }

    private HttpClient HttpClient { get; }

    private ProviderId ProviderIdentity { get; }

    private ProtectedResource ServiceDestination { get; }

    private ComponentId EffectAudience { get; }

    private Uri ServiceEndpoint { get; }

    private int ResponseByteLimit { get; }

    private TimeProvider Clock { get; }

    /// <inheritdoc/>
    public ProviderId ProviderId => ProviderIdentity;

    /// <inheritdoc/>
    public ComponentId SecurityAudience => EffectAudience;

    /// <inheritdoc/>
    public ProtectedResource Destination => ServiceDestination;

    /// <inheritdoc/>
    public async Task<WebSearchProviderResult> SearchAsync(
        WebSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidateGrant(request))
        {
            return new WebSearchDenied(request.Id, "The egress grant did not match this search attempt.");
        }

        if (Clock.GetUtcNow() >= request.Deadline)
        {
            return new WebSearchFailed(request.Id, "The search attempt exceeded its deadline.");
        }

        var url = BuildRequestUri(request);
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new WebSearchFailed(request.Id, "The search service did not answer.");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new WebSearchFailed(request.Id, "The search service did not answer in time.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new WebSearchFailed(request.Id, "The search service returned an error.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(capacity: Math.Min(ResponseByteLimit, 65_536));
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > ResponseByteLimit)
            {
                return new WebSearchFailed(request.Id, "The search response exceeded the configured size limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        WireResponse? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WireResponse>(buffer.ToArray(), _jsonOptions);
        }
        catch (JsonException)
        {
            return new WebSearchFailed(request.Id, "The search service returned an invalid response.");
        }

        if (payload?.Results is null)
        {
            return new WebSearchFailed(request.Id, "The search service returned an invalid response.");
        }

        var items = ImmutableArray.CreateBuilder<WebSearchItem>(Math.Min(payload.Results.Count, request.MaximumResults));
        foreach (var entry in payload.Results.Take(request.MaximumResults))
        {
            if (string.IsNullOrWhiteSpace(entry.Title)
                || string.IsNullOrWhiteSpace(entry.Url)
                || string.IsNullOrWhiteSpace(entry.Snippet))
            {
                return new WebSearchFailed(request.Id, "The search service returned an invalid result entry.");
            }

            if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri))
            {
                return new WebSearchFailed(request.Id, "The search service returned an invalid result URL.");
            }

            DateTimeOffset? publishedAt = null;
            if (!string.IsNullOrWhiteSpace(entry.PublishedAt)
                && DateTimeOffset.TryParse(entry.PublishedAt, out var parsed))
            {
                publishedAt = parsed.ToUniversalTime();
            }

            items.Add(new WebSearchItem(entry.Title, uri, entry.Snippet, publishedAt));
        }

        var complete = payload.Complete && items.Count == payload.Results.Count;
        return new WebSearchSucceeded(request.Id, items.ToImmutable(), complete);
    }

    /// <inheritdoc/>
    public void Dispose() => HttpClient.Dispose();

    private bool ValidateGrant(WebSearchRequest request)
    {
        var expected = WebSearchSecurityBinding.Fingerprint(
            request.Id,
            ProviderIdentity,
            ServiceDestination,
            request.Query,
            request.Domains,
            request.Freshness,
            request.MaximumResults,
            request.Deadline);
        return request.Grant.Audience == EffectAudience
            && request.Grant.Kind == SecurityOperationKind.Network
            && request.Grant.Effect == SecurityEffect.Egress
            && request.Grant.Resources.SequenceEqual([ServiceDestination])
            && request.Grant.InputFingerprint == expected;
    }

    private Uri BuildRequestUri(WebSearchRequest request)
    {
        var builder = new UriBuilder(ServiceEndpoint);
        var query = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}",
            $"maximum_results={request.MaximumResults}",
            $"freshness={FreshnessToken(request.Freshness)}",
        };
        if (!request.Domains.IsDefaultOrEmpty)
        {
            query.Add($"domains={Uri.EscapeDataString(string.Join(',', request.Domains.Select(static d => d.Value)))}");
        }

        var separator = string.IsNullOrEmpty(builder.Query) ? "?" : "&";
        builder.Query = string.IsNullOrEmpty(builder.Query)
            ? string.Join('&', query)
            : builder.Query.TrimStart('?') + separator + string.Join('&', query);
        return builder.Uri;
    }

    private static string FreshnessToken(WebSearchFreshness freshness) => freshness switch
    {
        WebSearchFreshness.Any => "any",
        WebSearchFreshness.Day => "day",
        WebSearchFreshness.Week => "week",
        WebSearchFreshness.Month => "month",
        WebSearchFreshness.Year => "year",
        _ => throw new ArgumentOutOfRangeException(nameof(freshness), freshness, "The freshness value is undefined."),
    };

    private static void ValidateOptions(NetworkWebSearchProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Endpoint);
        if (!options.Endpoint.IsAbsoluteUri || options.Endpoint.Scheme is not "https")
        {
            throw new ArgumentException("Endpoint must be an absolute HTTPS URI.", nameof(options));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumResponseBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConnectTimeout, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProviderId.Value, nameof(options));
    }

    private sealed class WireResponse
    {
        public bool Complete { get; init; }
        public List<WireResult>? Results { get; init; }
    }

    private sealed class WireResult
    {
        public string? Title { get; init; }
        public string? Url { get; init; }
        public string? Snippet { get; init; }
        public string? PublishedAt { get; init; }
    }
}
