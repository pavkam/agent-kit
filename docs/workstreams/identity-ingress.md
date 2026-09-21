# WS16: Identity ingress and revalidation

Goal: identity evidence is revalidated at admission and before protected
effects, the facade can accept an `IdentityAssertion` and resolve it through the
scoped resolver, and issuers, validation policies, and derivers have reusable
conformance suites.

Owning documents: [Identity](../architecture/identity.md),
[Testing and evaluation](../architecture/testing-and-evaluation.md) for the
generic conformance fixture.

## Progress

- [x] WS16-C1 `IConformanceFixture<T>` and `ConformanceCapabilities`
- [x] WS16-C2 issuer, validation-policy, deriver conformance suites
- [x] WS16-C3 admission revalidation in the facade
- [x] WS16-C4 `IdentityAssertion` ingress overloads
- [ ] WS16-C5 protected-boundary revalidation

## Verified current state

| Item                                                           | State                         | Evidence                                                                                                                           |
| -------------------------------------------------------------- | ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `IExecutionIdentityResolver` / `ExecutionIdentityResolver`     | EXISTS-UNWIRED                | `Abstractions/Identity/IExecutionIdentityResolver.cs`; scoped at `Identity/ServiceExtensions.cs:29`; no caller outside the package |
| channel adapters                                               | MISSING                       | only `IHumanQuestionChannel` exists                                                                                                |
| `IIdentityValidationPolicy`, `DefaultIdentityValidationPolicy` | used inside the resolver only | `Identity/DefaultIdentityValidationPolicy.cs:51-52`                                                                                |
| admission revalidation                                         | MISSING                       | no `IIdentityValidationPolicy` use in `AgentEngine`                                                                                |
| protected-boundary revalidation                                | MISSING                       | `Permissions/SecurityAuthority.cs:282,298` checks approval binding expiry only                                                     |
| revocation source                                              | MISSING, NO-SPEC              | `identity.md:118-120` prose                                                                                                        |
| normalizer/resolver conformance                                | EXISTS                        | `Conformance/IdentityNormalizerConformanceTests.cs` (5 cases)                                                                      |
| issuer, validation-policy, deriver conformance                 | MISSING                       | –                                                                                                                                  |
| `IConformanceFixture<TContract>`, `ConformanceCapabilities`    | EXISTS-UNWIRED (WS16-C1)      | `Conformance/IConformanceFixture.cs`, `ConformanceCapabilities.cs`; existing fixtures are not retrofitted                          |

Test doubles: issuers (`ConformanceIssuer`, `GatedIssuer`,
`MutableDescriptorIssuer`, `TestIssuer`), validation policies (3), normalization
policies (12), `Test.Shared/TestExecutionIdentity.cs` used by nearly every
project.

## Hidden prerequisites

1. No ingress exists to wire; the only realistic first step is facade overloads
   accepting `IdentityAssertion`.
2. Admission revalidation must be optional when `AddAgentIdentity` is absent, or
   `CompositionTestData` registers a fake.
3. Protected-boundary revalidation edits `SecurityAuthority` (WS3 file).

## Spec coverage

| Contract                                            | Spec                                                              |
| --------------------------------------------------- | ----------------------------------------------------------------- |
| resolver, options, DI                               | `identity.md:73,163-224`                                          |
| evidence lifetime and skew                          | `identity.md:106-112` (prose)                                     |
| admission/protected revalidation, revocation        | `identity.md:118-120`; NO-SPEC                                    |
| facade `IdentityAssertion` ingress                  | NO-SPEC                                                           |
| `IConformanceFixture<T>`, `ConformanceCapabilities` | `testing-and-evaluation.md:134-141`; capabilities members NO-SPEC |

## Chunks

### WS16-C1: Generic conformance fixture

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `tests/AgentKit.Conformance/IConformanceFixture.cs`,
  `ConformanceCapabilities.cs`; document in `testing-and-evaluation.md`. No
  retrofit of existing fixtures.
- Landed: both optional flags default to true. Suites may skip only a case
  documented as optional for a flag the fixture set false before use. No
  existing fixture was converted.

### WS16-C2: Issuer, validation-policy, deriver suites

- Depends on: C1. Risk: ADDITIVE. Size: M.
- Deliverables: `IdentityIssuerConformanceTests`,
  `IdentityValidationPolicyConformanceTests`,
  `DelegatedIdentityDeriverConformanceTests` with fixtures run in
  `AgentKit.Identity.Tests`: expiry equality is expired, skew bound, lifetime
  bound, depth limit, assurance never raised, chain narrowing.

### WS16-C3: Admission revalidation in the facade

- Depends on: WS1-C9 (or land against `SendAgentAsync` after
  `ValidatePinnedDefinitionAsync` and migrate). Risk: small DENSE-MODIFY. Size:
  M.
- Deliverables: optional `IIdentityValidationPolicy` resolved at admission;
  rejection becomes `AgentRunRejected<T>` with `AuthenticationFailed` (pre-C9:
  the admission exception); log event 18104; test with a controllable clock
  proving rejection before any session mutation; `identity.md` gains a
  revalidation C# block.

### WS16-C4: `IdentityAssertion` ingress overloads

- Depends on: WS1-C8/C9, C3. Risk: ADDITIVE. Size: M.
- Deliverables: `Agent.RunAsync<T>(SessionId, IdentityAssertion, AgentInput, …)`
  resolving through the scoped resolver inside the run scope; rejected identity
  → `AgentRunRejected<T>`; spec block in `composition-and-configuration.md`;
  identity skill.

### WS16-C5: Protected-boundary revalidation

- Depends on: C3, WS3. Risk: DENSE-MODIFY `SecurityAuthority` decision path (~20
  lines). Size: M.
- Deliverables: optional `IIdentityValidationPolicy`; expired evidence →
  `AuthorizationDenied`; tests in `SecurityAuthorityTests`.

## Totals

S 1, M 4. Confidence medium: the main uncertainty is whether facade assertion
overloads are meaningful before any channel adapter package exists.
