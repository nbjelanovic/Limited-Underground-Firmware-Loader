# Limited Underground Firmware Loader — Preview

One Windows application is intended to service both related product families:

- **Limited Underground Trail**
- **Limited Underground Display**

The first screen asks which system the operator is working with. That choice opens a product-scoped session and clears every prior session revision. It does not inspect hardware, select firmware, or grant permission to write.

## Current increment

The repository contains a buildable .NET 8 WPF shell and a deterministic product-selection core. The shell presents the exact two owner-approved product choices, keeps the full Limited Underground identity visible, and clearly states that firmware installation is unavailable.

Both providers are deliberately unavailable in this new shared shell:

- Trail's accepted inspection-only implementation still lives in the OpenTrail repository and has not been migrated.
- Display has no loader provider or target manifest yet.

A product-bound offline inspector now accepts only a readable, seekable candidate stream containing exactly `manifest.json`, `image.bin`, and `manifest.sig`. Its context is minted with an opaque per-controller identity and the currently selected product-session revision; switching products, returning to the chooser, or presenting the result to another controller makes it stale. It verifies canonical manifest encoding, the exact `opentrail` or `opengauge` product key, bounded target metadata, image length and SHA-256, and a nonempty fixed-size signature field. Stored or deflated entries are permitted only inside the 20 MiB archive limit, and every decompressed entry is independently read through its exact maximum-plus-one boundary. The caller's stream position is restored on success and failure. The inspector does not trust the signature, admit a release, select a device, or enable the disabled UI operation. No file chooser is wired yet.

There is no USB, serial, Bluetooth, WebUSB, esptool, erase, write, reset, recovery, trusted signer, bundle-admission, or device-selection adapter in this repository.

## Why a separate repository

Trail and Display are independent engineering projects. A shared customer utility must not make either repository own the other project's source or evidence. This repository will own only the common application shell, session authority, and provider boundary. Each product will continue to own its exact target manifests, artifacts, compatibility proof, and recovery rules.

## Validate

```powershell
.\tools\Test-Loader.ps1
```

This builds the WPF application and runs the deterministic console acceptance suite without launching the window or accessing hardware.

## License and branding

The source code is licensed under the [Apache License 2.0](LICENSE). The license does not grant permission to use the Limited Underground names or associated branding as trademarks or to imply endorsement. See [BRANDING.md](BRANDING.md).

## Remaining gates

1. Freeze a versioned provider lifecycle that supplies project-owned target rules and independently configured signer trust to the shared inspector.
2. Migrate the existing Trail inspection provider without weakening its privacy or fail-closed rules.
3. Add a Display provider only after OpenGauge owns an accepted target manifest and compatibility boundary.
4. Add signer trust, protected revocation, exact-device authority, writer, readback, boot confirmation, rollback, and recovery one gate at a time.
5. Perform physical write and recovery acceptance independently for every claimed target before removing **Preview** or **inspection only**.
