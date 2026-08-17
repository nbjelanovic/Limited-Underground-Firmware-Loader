# Limited Underground Firmware Loader — Preview

Public repository: <https://github.com/nbjelanovic/Limited-Underground-Firmware-Loader>

One Windows application is intended to service both related product families:

- **Limited Underground Trail**
- **Limited Underground Display**

The first screen asks which system the operator is working with. That choice opens a product-scoped session and clears every prior session revision. It does not inspect hardware, select firmware, or grant permission to write.

## Current increment

The repository contains a buildable .NET 8 WPF shell, a deterministic product-selection core, the host-tested version 1 product-provider lifecycle, and the migrated OpenTrail offline-inspection provider. The shell presents the exact two owner-approved product choices, keeps the full Limited Underground identity visible, and clearly states that firmware installation is unavailable.

Production registers one inspection-only provider and no signer-trust policy:

- Trail activates provider key `opentrail`, lifecycle contract version `1`, and the exact `heltec_v4_bench` target rule from OpenTrail's public target contract at commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`.
- The pinned SHA-256 of the target-contract Git blob bytes is `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776`.
- Display still has no loader provider or accepted target manifest.
- Signer trust, cryptographic admission, file selection, device access, and firmware installation remain unavailable.

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

This performs a clean warning-as-error Release build and runs 58 deterministic host groups without launching the window or accessing hardware.

## License and branding

The source code is licensed under the [Apache License 2.0](LICENSE). The license does not grant permission to use the Limited Underground names or associated branding as trademarks or to imply endorsement. See [BRANDING.md](BRANDING.md).

## Remaining gates

1. Add a Display provider only after OpenGauge owns an accepted target manifest and compatibility boundary.
2. Add a disabled-by-default file-selection surface only after its review keeps inspection separate from admission and installation.
3. Add real signer verification, protected revocation, exact-device authority, writer, readback, boot confirmation, rollback, and recovery one gate at a time.
4. Perform physical write and recovery acceptance independently for every claimed target before removing **Preview** or **inspection only**.
