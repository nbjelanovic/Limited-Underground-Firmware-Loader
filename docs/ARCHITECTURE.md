# Architecture

## Repository boundary

This project is the shared customer-tool boundary. It does not own OpenTrail or OpenGauge firmware. Product projects remain authoritative for compatibility identifiers, target manifests, image hashes, signatures, recovery procedures, and physical acceptance.

## First-screen authority

`LoaderProductCatalog` contains exactly two product families with stable engineering keys:

| Engineering key | Public working name | Current provider state |
| --- | --- | --- |
| `opentrail` | Limited Underground Trail | Existing inspection provider not migrated |
| `opengauge` | Limited Underground Display | Provider not implemented |

The display names are presentation only. They are not wire values, schema keys, cryptographic context, or hardware identifiers.

`LoaderSessionController` owns one scalar selection and monotonically increasing revision. Selecting a different product or returning to the chooser invalidates the previous revision. Unknown keys fail without mutation. Reselecting the exact current product is a no-op.

Every current snapshot reports all operational capabilities false:

- connected-device inspection;
- firmware-bundle selection;
- device/bundle matching;
- firmware writing; and
- recovery.

The disabled UI action is a visible statement of that boundary, not a simulated success path.

## Provider direction

A future provider must be versioned and project-owned. The shared application may consume a sanitized, signed public manifest, but it must not infer a target from a brand name, USB family, installed runtime string, or vendor specifications. Switching products must close the old provider and invalidate its device, bundle, and operation authority before the new provider can publish anything.

## Write direction

Product selection can never be reused as write approval. A future writer requires, at minimum, exact product provider, exact received-device identity, exact target manifest, admitted signer/release generation, complete address/file/hash plan, bounded attempt count, readback verification, boot confirmation, and accepted recovery plan. Owner authorization remains a separate operation-scoped gate.
