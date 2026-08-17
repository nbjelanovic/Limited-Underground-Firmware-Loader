# Project status

Status: public host-tested preview with the OpenTrail inspection provider active; 2026-08-17.

## Proven

- A .NET 8 WPF application builds with warnings treated as errors.
- The first application surface presents exactly Limited Underground Trail and Limited Underground Display.
- One session controller permits only one selected product and invalidates its revision on product changes or return to the chooser.
- Unknown products fail without mutation, and exact reselection is a no-op.
- Every device, bundle, write, and recovery capability remains false.
- An immutable provider registry accepts at most one exact lifecycle-v1 provider per catalog product.
- Each accepted activation receives an opaque lease and nonzero generation; switch, chooser return, and application close revoke before an interlocked close-once call.
- Close/open exceptions, null providers, identity mismatches, stale contexts, reentrant operations, and owner disposal remain providerless and expose only generic status.
- Project-owned target rules are immutable and exact. Signer trust is a separate application-owned registry bound to the exact product/provider/version.
- Provider trust configuration cannot set signer verification or release admission.
- Production registers exactly one provider: `opentrail`, lifecycle version `1`, for Limited Underground Trail.
- Its immutable rule set admits only exact target key `heltec_v4_bench` and pins SHA-256 `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776` for the target-contract Git blob bytes from OpenTrail commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`.
- Production signer trust remains empty; candidate inspection can publish bounded structure/product/digest results but never signer trust or admission.
- A canonical three-entry bundle-candidate schema is bounded by product key, target key, release generation, image length, SHA-256, signer identifier, and signature algorithm.
- Offline inspection contexts require an active provider lease and bind exact controller, session, activation, provider identity/generation, target-rule revision, optional trust revision, and context reference.
- Publication additionally requires an exact case-sensitive target in the active project-owned rule set. Signer trust and admission remain false.
- Stored and deflated ZIP entries are independently expansion-limited; exact maximum, maximum-plus-one, forged-size, oversized manifest/signature, stream-position restoration, cross-product, digest-mismatched, noncanonical, extra-entry, empty-signature, and unknown-product cases are covered.
- Fifty-eight deterministic groups and source-policy checks pass without launching the UI or accessing hardware.
- The independent public repository is published at <https://github.com/nbjelanovic/Limited-Underground-Firmware-Loader>; `main` is the default branch and GitHub detects Apache License 2.0.

## Not proven

- Production registers no Display provider and no signer-trust policy.
- No Display loader provider or accepted target manifest exists.
- No UI file selection, trusted signer verification, bundle admission, device match, USB, serial, Bluetooth, writer, readback, boot confirmation, rollback, recovery, installer, release signing, or physical acceptance exists here.
- No signed binary release, update channel, or public firmware package is configured.

## Next gate

Add the Display provider only after OpenGauge owns an accepted public target manifest and evidence boundary. A smaller optional UI increment may expose local file selection for offline inspection, but it must keep signer trust, admission, device access, and installation unavailable.
