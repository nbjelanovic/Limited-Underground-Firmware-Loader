# Limited Underground Firmware Loader — Preview

Public repository: <https://github.com/nbjelanovic/Limited-Underground-Firmware-Loader>

One Windows application is intended to service both related product families:

- **Limited Underground Trail**
- **Limited Underground Display**

The first screen asks which system the operator is working with. That choice opens a product-scoped session and clears every prior session revision. It does not inspect hardware, select firmware, or grant permission to write.

## Current increment

The repository contains a buildable .NET 8 WPF shell, a deterministic product-selection core, and the host-tested version 1 product-provider lifecycle. The shell presents the exact two owner-approved product choices, keeps the full Limited Underground identity visible, and clearly states that firmware installation is unavailable.

Production provider and signer-trust registries are deliberately empty:

- Trail's accepted inspection-only implementation still lives in the OpenTrail repository and has not been migrated.
- Display has no loader provider or accepted target manifest yet.

A provider registration is accepted only for one catalog product with an exact lowercase provider key and lifecycle contract version 1. A successful activation receives a nonzero generation and opaque lease. Switching products, returning to the chooser, or closing the application revokes and detaches that lease before closing the provider exactly once. Close/open failures, mismatched provider identities, reentrant callbacks, stale contexts, and owner disposal fail closed without exposing provider exception details or opening replacement authority.

Providers may supply only immutable, sanitized project-owned target rules. Signer trust is a separate application-owned registry bound to the exact product, provider, and contract version. A provider cannot declare a signer trusted or allow admission. Merely configuring signer metadata does not perform cryptographic verification.

The product-bound offline inspector accepts only a readable, seekable candidate stream containing exactly `manifest.json`, `image.bin`, and `manifest.sig`. An inspection context can be minted only for an active exact provider lease and binds the controller, session revision, activation token, provider generation and identity, target-rule revision, optional trust revision, and exact context object. The inspector verifies canonical manifest encoding, bounded metadata, image length and SHA-256, and a nonempty fixed-size signature field. The caller's stream position is restored on success and failure. Publication additionally requires an exact project-owned target rule. Signer trust and admission remain false.

There is no USB, serial, Bluetooth, WebUSB, esptool, erase, write, reset, recovery, trusted signer verification, bundle admission, or device-selection adapter in this repository. No file chooser is wired yet.

## Why a separate repository

Trail and Display are independent engineering projects. A shared customer utility must not make either repository own the other project's source or evidence. This repository owns only the common application shell, session authority, provider lifecycle, and inspection boundary. Each product continues to own its exact target manifests, artifacts, compatibility proof, and recovery rules.

## Validate

```powershell
.\tools\Test-Loader.ps1
```

This performs a clean warning-as-error Release build and runs 52 deterministic host groups without launching the window or accessing hardware.

## License and branding

The source code is licensed under the [Apache License 2.0](LICENSE). The license does not grant permission to use the Limited Underground names or associated branding as trademarks or to imply endorsement. See [BRANDING.md](BRANDING.md).

## Remaining gates

1. Migrate the existing Trail inspection provider without weakening its privacy or fail-closed rules.
2. Add a Display provider only after OpenGauge owns an accepted target manifest and compatibility boundary.
3. Add real signer verification, protected revocation, exact-device authority, writer, readback, boot confirmation, rollback, and recovery one gate at a time.
4. Perform physical write and recovery acceptance independently for every claimed target before removing **Preview** or **inspection only**.
