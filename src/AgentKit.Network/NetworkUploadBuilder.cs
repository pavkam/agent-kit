// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Builds authorized HTTP request content and egress evidence.</summary>
internal static class NetworkUploadBuilder
{
    internal sealed record UploadBuildResult(HttpContent? Content, NetworkEgressEvidence Evidence);

    internal static async ValueTask<UploadBuildResult?> BuildAsync(
        INetworkRequestContent? content,
        NetworkRequestBounds bounds,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return new UploadBuildResult(null, new NetworkEgressEvidence(0, ProcessSecurityBinding.FingerprintBytes([])));
        }

        if (content is NetworkRequestContent buffered)
        {
            if (buffered.Body.Length > bounds.MaximumRequestBytes)
            {
                return null;
            }

            var httpContent = new ByteArrayContent(buffered.Body.ToArray());
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(buffered.ContentType);
            return new UploadBuildResult(httpContent, new NetworkEgressEvidence(buffered.Body.Length, buffered.BodyFingerprint));
        }

        if (content is NetworkStagedRequestContent staged)
        {
            await using var stream = await staged.Spool.OpenReadAsync(
                staged.BodyFingerprint,
                bounds.MaximumRequestBytes,
                cancellationToken).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            long total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > bounds.MaximumRequestBytes)
                {
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            var bytes = memory.ToArray();
            var fingerprint = ProcessSecurityBinding.FingerprintBytes(bytes);
            if (fingerprint != staged.BodyFingerprint)
            {
                return null;
            }

            var httpContent = new ByteArrayContent(bytes);
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(staged.ContentType);
            return new UploadBuildResult(httpContent, new NetworkEgressEvidence(bytes.Length, fingerprint));
        }

        return null;
    }
}
