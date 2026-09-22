// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

using Microsoft.Extensions.Options;

/// <summary>Registers the durable local JSON security-grant and approval storage leaves.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds one JSON grant-store adapter whose bounds and encoding come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="configure">
        /// An optional delegate that mutates a fresh <see cref="JsonSecurityGrantStoreOptions"/> before its values are
        /// materialized into immutable <see cref="JsonSecurityGrantStoreSettings"/>. Use
        /// <see cref="JsonSecurityGrantStoreOptions.Encoding"/> to replace or adjust the JSON contract. When null, the
        /// registration uses <see cref="JsonSecurityGrantStoreSettings.CreateDefault"/>-equivalent bounds and the canonical
        /// encoding contract.
        /// </param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options contain a byte bound or compaction threshold that is not positive.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with a different target or different effective settings.</exception>
        /// <remarks>
        /// <para>
        /// The target is a required external persistence fact and is never defaulted. The configure delegate is optional; it
        /// runs exactly once, synchronously, during registration, and the options instance it receives is never registered
        /// in dependency injection. This is a security control-plane store, so validation stays eager: invalid values throw
        /// at registration before any service is added.
        /// </para>
        /// <para>
        /// Because the encoding contract is fully replaceable, the effective contract's fingerprint becomes part of the
        /// store's persisted identity. A later composition that changes the contract fails when it opens a root written
        /// under the previous one rather than decoding existing evidence under incompatible rules.
        /// </para>
        /// </remarks>
        public IServiceCollection AddJsonSecurityGrantStore(
            JsonSecurityGrantStoreTarget target,
            Action<JsonSecurityGrantStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new JsonSecurityGrantStoreOptions();
            configure?.Invoke(options);
            return services.AddJsonSecurityGrantStore(target, new JsonSecurityGrantStoreSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                new JsonEncodingSettings(options.Encoding.SerializerOptions)));
        }

        /// <summary>Adds one explicitly configured JSON grant-store adapter without touching its target.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="settings">The immutable evidence bounds, compaction policy, and frozen encoding contract.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or settings evidence.</exception>
        /// <remarks>
        /// Repeating the exact registration is idempotent. Other leaf or custom <see cref="ISecurityGrantStore"/>
        /// registrations remain visible so composition rejects ambiguity. Trusted bootstrap resolves the selected interface,
        /// verifies it is <see cref="JsonSecurityGrantStore"/>, and calls
        /// bootstrap initialization before first use.
        /// </remarks>
        public IServiceCollection AddJsonSecurityGrantStore(
            JsonSecurityGrantStoreTarget target,
            JsonSecurityGrantStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            Capture(services, target, settings, "grant-store");
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISecurityGrantStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && (descriptor.ImplementationType == typeof(JsonSecurityGrantStore)
                        || descriptor.ImplementationFactory is not null)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityGrantStore>(static provider =>
                    new JsonSecurityGrantStore(
                        provider.GetRequiredService<JsonSecurityGrantStoreTarget>(),
                        provider.GetRequiredService<JsonSecurityGrantStoreSettings>(),
                        provider.GetRequiredService<TimeProvider>(),
                        provider.GetService<ILogger<JsonSecurityGrantStore>>(),
                        provider.GetService<ISecurityAuditDispatcher>(),
                        provider.GetService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                        provider.GetService<IOptions<AgentPermissionOptions>>())));
            }

            return services;
        }

        /// <summary>Adds one JSON approval-store adapter whose bounds and encoding come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="configure">
        /// An optional delegate that mutates a fresh <see cref="JsonApprovalStoreOptions"/> before its values are
        /// materialized into immutable <see cref="JsonApprovalStoreSettings"/>.
        /// </param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options contain a byte bound or compaction threshold that is not positive.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with a different target or different effective settings.</exception>
        /// <remarks>
        /// The approval store may share a root with the grant store or use its own. Sharing one root is supported because
        /// each store owns a distinct record log, but the two stores still commit independently and a deferral is durable
        /// only once its own append is acknowledged.
        /// </remarks>
        public IServiceCollection AddJsonApprovalStore(
            JsonApprovalStoreTarget target,
            Action<JsonApprovalStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new JsonApprovalStoreOptions();
            configure?.Invoke(options);
            return services.AddJsonApprovalStore(target, new JsonApprovalStoreSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                new JsonEncodingSettings(options.Encoding.SerializerOptions)));
        }

        /// <summary>Adds one explicitly configured JSON approval-store adapter without touching its target.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="settings">The immutable evidence bounds, compaction policy, and frozen encoding contract.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or settings evidence.</exception>
        /// <remarks>
        /// Repeating the exact registration is idempotent. Trusted bootstrap resolves <see cref="IApprovalStore"/>, verifies
        /// it is <see cref="JsonApprovalStore"/>, and completes bootstrap initialization before first use.
        /// </remarks>
        public IServiceCollection AddJsonApprovalStore(
            JsonApprovalStoreTarget target,
            JsonApprovalStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            Capture(services, target, settings, "approval-store");
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(IApprovalStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(JsonApprovalStore)))
            {
                services.Add(ServiceDescriptor.Singleton<IApprovalStore, JsonApprovalStore>());
            }

            return services;
        }

        /// <summary>Adds one JSON security-decision-store adapter whose bounds and encoding come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="configure">An optional delegate that mutates a fresh <see cref="JsonSecurityDecisionStoreOptions"/>.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options contain a byte bound or compaction threshold that is not positive.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with a different target or different effective settings.</exception>
        public IServiceCollection AddJsonSecurityDecisionStore(
            JsonSecurityDecisionStoreTarget target,
            Action<JsonSecurityDecisionStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new JsonSecurityDecisionStoreOptions();
            configure?.Invoke(options);
            return services.AddJsonSecurityDecisionStore(target, new JsonSecurityDecisionStoreSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                new JsonEncodingSettings(options.Encoding.SerializerOptions)));
        }

        /// <summary>Adds one explicitly configured JSON security-decision-store adapter without touching its target.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="settings">The immutable evidence bounds, compaction policy, and frozen encoding contract.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or settings evidence.</exception>
        public IServiceCollection AddJsonSecurityDecisionStore(
            JsonSecurityDecisionStoreTarget target,
            JsonSecurityDecisionStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            Capture(services, target, settings, "decision-store");
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISecurityDecisionStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(JsonSecurityDecisionStore)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityDecisionStore, JsonSecurityDecisionStore>());
            }

            return services;
        }
    }

    private static void Capture<TTarget, TSettings>(
        IServiceCollection services, TTarget target, TSettings settings, string leaf)
        where TTarget : class
        where TSettings : class
    {
        Debug.Assert(services is not null, "A composition surface is required.");
        Debug.Assert(target is not null, "A validated target is required.");
        Debug.Assert(settings is not null, "Validated settings are required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(leaf), "A bounded leaf name is required.");
        var existingTarget = GetCaptured<TTarget>(services, leaf);
        var existingSettings = GetCaptured<TSettings>(services, leaf);
        if (existingTarget is not null && !existingTarget.Equals(target))
        {
            throw new InvalidOperationException(
                $"The JSON {leaf} leaf is already configured for a different target.");
        }
        if (existingSettings is not null && !existingSettings.Equals(settings))
        {
            throw new InvalidOperationException(
                $"The JSON {leaf} leaf is already configured with different settings.");
        }

        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        if (existingTarget is null)
        {
            _ = services.AddSingleton(target);
        }
        if (existingSettings is null)
        {
            _ = services.AddSingleton(settings);
        }
    }

    private static T? GetCaptured<T>(IServiceCollection source, string leaf)
        where T : class
    {
        Debug.Assert(source is not null, "A composition surface is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(leaf), "A bounded leaf name is required.");
        var matches = source.Where(static descriptor =>
            descriptor.ServiceType == typeof(T) && descriptor.ServiceKey is null).ToArray();
        return matches.Length switch
        {
            0 => null,
            1 when GetInstance(matches[0]) is T value => value,
            _ => throw new InvalidOperationException(
                $"The JSON {leaf} {typeof(T).Name} registration is not one exact captured instance."),
        };

        static object? GetInstance(ServiceDescriptor descriptor) => descriptor.IsKeyedService
            ? descriptor.KeyedImplementationInstance
            : descriptor.ImplementationInstance;
    }
}
