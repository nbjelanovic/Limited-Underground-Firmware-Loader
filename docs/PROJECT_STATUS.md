# Project status

Status: local host-tested foundation; publication pending; 2026-08-16.

## Proven

- A .NET 8 WPF application builds with warnings treated as errors.
- The first application surface presents exactly Limited Underground Trail and Limited Underground Display.
- One session controller permits only one selected product and invalidates its revision on product changes or return to the chooser.
- Unknown products fail without mutation, and exact reselection is a no-op.
- Every device, bundle, write, and recovery capability remains false.
- Deterministic tests and source-policy checks pass without launching the UI or accessing hardware.

## Not proven

- No existing Trail inspection implementation has been migrated.
- No Display loader provider or target manifest exists.
- No USB, serial, Bluetooth, bundle, signer, device-match, writer, readback, boot-confirmation, rollback, recovery, installer, signing, or physical acceptance exists here.
- No remote repository or public package is configured.

## Next gate

Freeze a provider contract that can import public project-owned manifests without allowing either project to absorb the other's source or evidence. Migrate the Trail inspection-only provider first; keep every writing action unavailable.
