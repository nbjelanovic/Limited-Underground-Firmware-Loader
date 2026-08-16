# Limited Underground Firmware Loader Agent Guide

## Scope

This repository is the independent source boundary for the shared Limited Underground Firmware Loader. OpenTrail and OpenGauge remain separate sibling projects and own their own target manifests, compatibility evidence, firmware artifacts, and recovery decisions. Do not copy project-private records, identifiers, or artifacts into this repository.

## Product boundary

- The public working name is **Limited Underground Firmware Loader — Preview** pending attorney clearance.
- The first interaction selects **Limited Underground Trail** or **Limited Underground Display**.
- Engineering keys remain `opentrail` and `opengauge`; public names never become protocol or compatibility identifiers.
- Until separately accepted physical write and recovery gates exist for a target, the application remains inspection-only and must not expose or imply firmware installation authority.
- A product choice selects a provider namespace only. It never grants device, bundle, signer, write, erase, reset, or recovery authority.

## Safety and privacy

- No automatic device selection.
- No firmware write, erase, reset, reboot, recovery, or eFuse action without an exact reviewed adapter and separately recorded owner authorization.
- Never persist or publish serial ports, USB serials, MAC addresses, pairing data, keys, coordinates, vehicle identifiers, or local paths.
- Fail closed when a product provider, target manifest, signer, device identity, or recovery plan is absent or ambiguous.

## Validation and publication

- Run `tools\Test-Loader.ps1` after source changes.
- Keep warning-as-error builds and deterministic core tests green.
- Update README, architecture, status, and backlog whenever accepted behavior changes.
- Follow `D:\ESP32\AGENTS.md` for commit, push, and website publication. If no remote exists, report `implementation complete; publication pending` and name that blocker.
