// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Registers the durable host-local JSON budget-ledger leaf.</summary>
/// <remarks>The budget runtime and the application-selected dimension catalog remain separate registrations; this leaf supplies only storage and its replaceable clock and identity collaborators.</remarks>
public static class ServiceExtensions
{
    /// <summary>Adds JSON budget-ledger registration operations to a caller-owned service collection.</summary>
    /// <param name="services">The mutable application composition receiving the explicit adapter selection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one JSON budget-ledger adapter whose bounds and encoding come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="configure">
        /// An optional delegate that mutates a fresh <see cref="JsonBudgetLedgerOptions"/> before its values are
        /// materialized into immutable <see cref="JsonBudgetLedgerSettings"/>. Use
        /// <see cref="JsonBudgetLedgerOptions.Encoding"/> to replace or adjust the JSON contract. When null, the
        /// registration uses <see cref="JsonBudgetLedgerSettings.CreateDefault"/>-equivalent bounds and the canonical
        /// encoding contract.
        /// </param>
        /// <returns>The same caller-owned collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options contain a byte bound that is not positive.</exception>
        /// <exception cref="InvalidOperationException">This leaf was already configured with a different target or different effective settings.</exception>
        /// <remarks>
        /// <para>
        /// The target is a required external persistence fact and is never defaulted. The configure delegate is optional;
        /// it runs exactly once, synchronously, during registration, and the options instance it receives is never
        /// registered in dependency injection, so the captured bounds cannot drift after composition. Validation stays
        /// eager: invalid values throw here and no service is added.
        /// </para>
        /// <para>
        /// Because the encoding contract is fully replaceable, the effective contract's fingerprint becomes part of the
        /// store's persisted identity. A later composition that changes the contract fails when it opens a root written
        /// under the previous one rather than decoding existing accounting under incompatible rules.
        /// </para>
        /// </remarks>
        public IServiceCollection AddJsonBudgetLedger(
            JsonBudgetLedgerTarget target, Action<JsonBudgetLedgerOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new JsonBudgetLedgerOptions();
            configure?.Invoke(options);
            return services.AddJsonBudgetLedger(target, new JsonBudgetLedgerSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                new JsonEncodingSettings(options.Encoding.SerializerOptions)));
        }

        /// <summary>Adds one explicitly configured JSON budget-ledger adapter without touching its target.</summary>
        /// <param name="target">The immutable fixed store root and bootstrap effect policy.</param>
        /// <param name="settings">The immutable evidence bounds and frozen encoding contract.</param>
        /// <returns>The same caller-owned collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="InvalidOperationException">This leaf was already configured with different target or settings evidence, or its captured registration is not one exact instance.</exception>
        /// <remarks>
        /// Repeating the exact registration is idempotent. Existing clocks and typed identity generators are preserved,
        /// and other leaf or custom <see cref="IBudgetLedger"/> registrations remain visible so composition rejects
        /// ambiguity. The caller separately selects the budget runtime and one <see cref="IBudgetDimensionCatalog"/>,
        /// then resolves the selected interface, verifies it is <see cref="JsonBudgetLedger"/>, and calls
        /// <see cref="JsonBudgetLedger.InitializeAsync"/> during trusted bootstrap before first use.
        /// </remarks>
        public IServiceCollection AddJsonBudgetLedger(
            JsonBudgetLedgerTarget target, JsonBudgetLedgerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            var existingTarget = GetCaptured<JsonBudgetLedgerTarget>(services);
            var existingSettings = GetCaptured<JsonBudgetLedgerSettings>(services);
            if (existingTarget is not null && existingTarget != target)
            {
                throw new InvalidOperationException(
                    "The JSON budget-ledger leaf is already configured for a different target.");
            }
            if (existingSettings is not null && existingSettings != settings)
            {
                throw new InvalidOperationException(
                    "The JSON budget-ledger leaf is already configured with different settings.");
            }

            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<BudgetScopeId>, GuidBudgetScopeIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<BudgetReservationId>, GuidBudgetReservationIdGenerator>();
            if (existingTarget is null)
            {
                _ = services.AddSingleton(target);
            }
            if (existingSettings is null)
            {
                _ = services.AddSingleton(settings);
            }
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(IBudgetLedger)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(JsonBudgetLedger)))
            {
                services.Add(ServiceDescriptor.Singleton<IBudgetLedger, JsonBudgetLedger>());
            }

            return services;

            static T? GetCaptured<T>(IServiceCollection source)
                where T : class
            {
                var matches = source
                    .Where(static descriptor => descriptor.ServiceType == typeof(T) && descriptor.ServiceKey is null)
                    .ToArray();
                return matches.Length switch
                {
                    0 => null,
                    1 when matches[0].ImplementationInstance is T value => value,
                    _ => throw new InvalidOperationException(
                        $"The JSON budget-ledger {typeof(T).Name} registration is not one exact captured instance."),
                };
            }
        }
    }
}
