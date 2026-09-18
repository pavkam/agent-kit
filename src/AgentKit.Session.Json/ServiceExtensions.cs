// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

using AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers the durable local JSON session store and session directory leaves.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds one JSON session store whose bounds and encoding come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="configure">
        /// An optional delegate that mutates a fresh <see cref="JsonSessionStoreOptions"/> before its values are
        /// materialized into immutable <see cref="JsonSessionStoreSettings"/>. Use
        /// <see cref="JsonSessionStoreOptions.Encoding"/> to replace or adjust the JSON contract. When null, the
        /// registration uses <see cref="JsonSessionStoreSettings.CreateDefault"/>-equivalent bounds and the canonical
        /// encoding contract.
        /// </param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options contain a bound or threshold that is not positive.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with a different target or different effective settings.</exception>
        /// <remarks>
        /// The target is a required external persistence fact and is never defaulted. The configure delegate is optional;
        /// it runs exactly once, synchronously, during registration, and the options instance it receives is never
        /// registered in dependency injection. Validation stays eager: an invalid value throws at registration before any
        /// service is added.
        /// </remarks>
        public IServiceCollection AddJsonSessionStore(
            JsonSessionStoreTarget target,
            Action<JsonSessionStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new JsonSessionStoreOptions();
            configure?.Invoke(options);
            return services.AddJsonSessionStore(target, new JsonSessionStoreSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                options.MaximumIssuedReadSnapshots,
                new JsonEncodingSettings(options.Encoding.SerializerOptions)));
        }

        /// <summary>Adds one explicitly configured JSON session store without touching its target.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="settings">The immutable bounds, continuation limits, compaction policy, and frozen encoding contract.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or settings evidence.</exception>
        /// <remarks>
        /// <para>
        /// Repeating the exact registration is idempotent; repeating it with different evidence fails closed. The store
        /// joins the additive <see cref="ISessionStore"/> set, so another leaf registered earlier is neither replaced nor
        /// hidden and registration order never selects a store — the directory route and store key do.
        /// </para>
        /// <para>
        /// The first-party portable entry codecs, the GUID identity generators, the shared observability registration, and
        /// <see cref="TimeProvider.System"/> are added with <c>TryAdd</c> semantics so a host may replace any of them. The
        /// required audit dispatcher and grant store are not created here; composition must supply those security
        /// boundaries. Trusted bootstrap resolves the selected store, verifies it is <see cref="JsonSessionStore"/>, and
        /// calls <see cref="JsonSessionStore.InitializeAsync"/> before first use.
        /// </para>
        /// </remarks>
        public IServiceCollection AddJsonSessionStore(
            JsonSessionStoreTarget target,
            JsonSessionStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            Capture(services, target, settings, "session store");
            AddSessionEntryCodecs(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<BranchId>>(
                static _ => new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                static _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(
                    static value => new SecurityAuditRecordId(value)));
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISessionStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(JsonSessionStore)))
            {
                services.Add(ServiceDescriptor.Singleton<ISessionStore, JsonSessionStore>());
            }

            return services;
        }

        /// <summary>Adds one JSON session directory whose bounds and encoding come from an optional configure delegate.</summary>
        /// <param name="securityAudience">The nonblank identity that will consume directory-specific grants.</param>
        /// <param name="target">The immutable fixed directory root and bootstrap effect policy.</param>
        /// <param name="configure">
        /// An optional delegate that mutates a fresh <see cref="JsonSessionDirectoryOptions"/> before its values are
        /// materialized into immutable <see cref="JsonSessionDirectorySettings"/>.
        /// </param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options contain a bound or threshold that is not positive.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with a different target or different effective settings.</exception>
        public IServiceCollection AddJsonSessionDirectory(
            ComponentId securityAudience,
            JsonSessionDirectoryTarget target,
            Action<JsonSessionDirectoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
            ArgumentNullException.ThrowIfNull(target);
            var options = new JsonSessionDirectoryOptions();
            configure?.Invoke(options);
            return services.AddJsonSessionDirectory(securityAudience, target, new JsonSessionDirectorySettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                new JsonEncodingSettings(options.Encoding.SerializerOptions)));
        }

        /// <summary>Adds one explicitly configured JSON session directory without touching its target.</summary>
        /// <param name="securityAudience">The nonblank identity that will consume directory-specific grants.</param>
        /// <param name="target">The immutable fixed directory root and bootstrap effect policy.</param>
        /// <param name="settings">The immutable bounds, compaction policy, and frozen encoding contract.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or settings evidence.</exception>
        /// <remarks>
        /// <para>
        /// Repeating the exact registration is idempotent; repeating it with different evidence fails closed. The directory
        /// is the singular <see cref="ISessionDirectory"/> registration and uses <c>TryAdd</c> semantics, so a directory
        /// registered earlier by another package wins and this call adds none.
        /// </para>
        /// <para>
        /// The directory root must differ from the session store's root: each leaf owns a complete root, including its own
        /// manifest and its own advisory exclusive lock. Trusted bootstrap resolves <see cref="ISessionDirectory"/>,
        /// verifies it is <see cref="JsonSessionDirectory"/>, and calls
        /// <see cref="JsonSessionDirectory.InitializeAsync"/> before first use.
        /// </para>
        /// </remarks>
        public IServiceCollection AddJsonSessionDirectory(
            ComponentId securityAudience,
            JsonSessionDirectoryTarget target,
            JsonSessionDirectorySettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            Capture(services, target, settings, "session directory");
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                static _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(
                    static value => new SecurityAuditRecordId(value)));
            services.TryAddSingleton<ISessionDirectory>(provider => new JsonSessionDirectory(
                securityAudience,
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<JsonSessionDirectoryTarget>(),
                provider.GetRequiredService<JsonSessionDirectorySettings>(),
                provider.GetRequiredService<ILogger<JsonSessionDirectory>>()));

            return services;
        }
    }

    /// <summary>Registers the first-party portable session-entry codecs and their frozen catalog.</summary>
    /// <param name="services">The composition surface receiving the additive codec registrations.</param>
    /// <remarks>Every registration uses <c>TryAdd</c> semantics so a host may replace the catalog or add its own codecs.</remarks>
    private static void AddSessionEntryCodecs(IServiceCollection services)
    {
        Debug.Assert(services is not null, "A composition surface is required.");
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISessionEntryCodec, ExecutionLaneProvisionedSessionEntryCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, InputPromotedSessionEntryCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, OperationAcceptedSessionEntryCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, MessageSessionEntryCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, InputAdmittedSessionEntryCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, CompactionSessionEntryCodec>());
        services.TryAddSingleton<SessionEntryCodecCatalog>();
        services.TryAddSingleton<ISessionEntryCodecCatalog>(
            static provider => provider.GetRequiredService<SessionEntryCodecCatalog>());
    }

    /// <summary>Captures one leaf's target and settings as singletons, rejecting a second, different configuration.</summary>
    /// <typeparam name="TTarget">The immutable bootstrap target type captured for the leaf.</typeparam>
    /// <typeparam name="TSettings">The immutable settings type captured for the leaf.</typeparam>
    /// <param name="services">The composition surface receiving the captured values.</param>
    /// <param name="target">The validated target to capture.</param>
    /// <param name="settings">The validated settings to capture.</param>
    /// <param name="leaf">The bounded leaf name used in failure messages.</param>
    /// <exception cref="InvalidOperationException">The leaf is already configured with a different target or different settings.</exception>
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
            throw new InvalidOperationException($"The JSON {leaf} leaf is already configured for a different target.");
        }
        if (existingSettings is not null && !existingSettings.Equals(settings))
        {
            throw new InvalidOperationException($"The JSON {leaf} leaf is already configured with different settings.");
        }

        if (existingTarget is null)
        {
            _ = services.AddSingleton(target);
        }
        if (existingSettings is null)
        {
            _ = services.AddSingleton(settings);
        }
    }

    /// <summary>Resolves the single already captured instance of one composition value, if present.</summary>
    /// <typeparam name="T">The captured value type.</typeparam>
    /// <param name="source">The composition surface to inspect.</param>
    /// <param name="leaf">The bounded leaf name used in failure messages.</param>
    /// <returns>The captured instance, or <see langword="null"/> when the leaf is not yet configured.</returns>
    /// <exception cref="InvalidOperationException">More than one registration exists, or the single registration is not a captured instance.</exception>
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
