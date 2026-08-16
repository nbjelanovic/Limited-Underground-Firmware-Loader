# Project status

Status: local host-tested foundation; publication pending; 2026-08-16.

## Proven

- A .NET 8 WPF application builds with warnings treated as errors.
- The first application surface presents exactly Limited Underground Trail and Limited Underground Display.
- One session controller permits only one selected product and invalidates its revision on product changes or return to the chooser.
- Unknown products fail without mutation, and exact reselection is a no-op.
- Every device, bundle, write, and recovery capability remains false.
- A canonical three-entry bundle-candidate schema is bounded by product key, target key, release generation, image length, SHA-256, signer identifier, and signature algorithm.
- Offline inspection verifies structure, product binding, image digest, and signature presence while always leaving signer trust and release admission false.
- Inspection contexts carry opaque controller identity plus the selected session revision; results have no public constructor and bind to the exact context. Exact reselection remains current, while switching, returning to the chooser, fabrication, or cross-controller reuse suppresses the result.
- Stored and deflated ZIP entries are independently expansion-limited; exact maximum, maximum-plus-one, forged-size, oversized manifest/signature, stream-position restoration, cross-product, digest-mismatched, noncanonical, extra-entry, empty-signature, and unknown-product cases are covered.
- Thirty-one deterministic groups and source-policy checks pass without launching the UI or accessing hardware.

## Not proven

- No existing Trail inspection implementation has been migrated.
- No Display loader provider or target manifest exists.
- No UI file selection, project target provider, trusted signer, bundle admission, device match, USB, serial, Bluetooth, writer, readback, boot confirmation, rollback, recovery, installer, release signing, or physical acceptance exists here.
- No remote repository or public package is configured.

## Next gate

Freeze a provider lifecycle that can supply public project-owned target rules and signer trust without allowing either project to absorb the other's source or evidence. Migrate the Trail inspection-only provider first; keep bundle admission and every writing action unavailable until their separate gates pass.
