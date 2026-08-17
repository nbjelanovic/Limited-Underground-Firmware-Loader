# Project status

Status: public host-tested preview foundation with provider lifecycle version 1; 2026-08-16.

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
- A canonical three-entry bundle-candidate schema is bounded by product key, target key, release generation, image length, SHA-256, signer identifier, and signature algorithm.
- Offline inspection contexts require an active provider lease and bind exact controller, session, activation, provider identity/generation, target-rule revision, optional trust revision, and context reference.
- Publication additionally requires an exact case-sensitive target in the active project-owned rule set. Signer trust and admission remain false.
- Stored and deflated ZIP entries are independently expansion-limited; exact maximum, maximum-plus-one, forged-size, oversized manifest/signature, stream-position restoration, cross-product, digest-mismatched, noncanonical, extra-entry, empty-signature, and unknown-product cases are covered.
- Fifty-two deterministic groups and source-policy checks pass without launching the UI or accessing hardware.
- The independent public repository is published at <https://github.com/nbjelanovic/Limited-Underground-Firmware-Loader>; `main` is the default branch and GitHub detects Apache License 2.0.

## Not proven

- Production registers no product provider and no signer-trust policy.
- No existing Trail inspection implementation has been migrated.
- No Display loader provider or accepted target manifest exists.
- No UI file selection, trusted signer verification, bundle admission, device match, USB, serial, Bluetooth, writer, readback, boot confirmation, rollback, recovery, installer, release signing, or physical acceptance exists here.
- No signed binary release, update channel, or public firmware package is configured.

## Next gate

Migrate the Trail inspection-only provider into lifecycle version 1 with its OpenTrail-owned target rules, preserving privacy-safe read-only behavior. Keep real signer trust, admission, every device mutation, and every writing action unavailable until their separate gates pass.
