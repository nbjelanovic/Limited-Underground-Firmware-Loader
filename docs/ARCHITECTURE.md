# Architecture

## Repository boundary

This project is the shared customer-tool boundary. It does not own OpenTrail or OpenGauge firmware. Product projects remain authoritative for compatibility identifiers, target manifests, image hashes, signatures, recovery procedures, and physical acceptance.

## First-screen authority

`LoaderProductCatalog` contains exactly two product families with stable engineering keys:

| Engineering key | Public working name | Current production provider state |
| --- | --- | --- |
| `opentrail` | Limited Underground Trail | Existing inspection provider not migrated |
| `opengauge` | Limited Underground Display | Provider and accepted target manifest do not exist |

The display names are presentation only. They are not wire values, schema keys, cryptographic context, or hardware identifiers.

`LoaderSessionController` owns one scalar selection and monotonically increasing revision. Selecting a different product or returning to the chooser invalidates the previous revision. Unknown keys fail without mutation. Reselecting the exact current product is a no-op.

Every current snapshot reports all operational capabilities false:

- connected-device inspection;
- firmware-bundle selection;
- device/bundle matching;
- firmware writing; and
- recovery.

The disabled UI action is a visible statement of that boundary, not a simulated success path.

## Provider lifecycle version 1

The controller accepts an immutable provider-factory registry with at most one exact provider per catalog product. Registration requires a sanitized lowercase provider key and contract version `1`. Signer-trust policies are injected through a separate application-owned registry and must bind to the exact same product, provider key, and contract version.

Each activation receives an opaque token and nonzero monotonically increasing provider generation. A product may remain visibly selected when its provider is absent or activation fails, but no offline-inspection context can be minted without an active exact lease.

A product switch, chooser return, or owner disposal:

1. revokes and detaches the active lease and its rule/trust state;
2. advances the session revision when the selection changes;
3. closes the detached provider outside the state lock through an interlocked close-once wrapper; and
4. only then permits a replacement provider to open.

A failed close blocks replacement activation. Null, throwing, or identity-mismatched factories remain providerless, and any returned rejected provider is closed once. Transition state rejects reentrant selection, chooser return, context minting, and result publication. Reentrant owner disposal aborts activation and closes a newly returned candidate once. `MainWindow` disposes the controller on window close.

Providers expose only their exact open context, immutable project-owned target rules, and `Close`. The interface contains no enumeration, connection, write, erase, reset, reboot, recovery, signer-trust, or admission method. Provider exception text is never published.

Target rules and signer trust are independent authorities. Rules bind exact target keys to a project-owned manifest identity and source revision. A separately injected signer policy may identify signer IDs and configured public-key fingerprints, but configuration alone never sets `SignerTrusted` or `AdmissionAllowed`; real cryptographic verification and revocation are later gates.

Production currently registers no providers and no signer trust. The lifecycle is proven only with deterministic fake providers.

## Offline bundle-candidate boundary

`FirmwareBundleCandidateInspector` owns one product-neutral candidate schema. The archive must contain exactly `manifest.json`, `image.bin`, and `manifest.sig`; manifest property order and encoding are canonical; archive, manifest, image, signature, identifier, and integer sizes are bounded. The manifest product key must be exactly `opentrail` or `opengauge`.

A caller cannot fabricate accepted authority: the controller mints an immutable context only from an active provider lease. The context binds opaque controller and activation tokens, exact context reference, selected product, session revision, provider key, contract version, provider generation, target-rule source revision, and optional signer-trust source revision. Result construction is internal to the inspector and retains the exact context object.

Publication requires every binding to remain current, verified structure/digest/signature presence, an exact product match, an exact case-sensitive target in the active project-owned rule set, and false signer-trust/admission flags. A switch, chooser return, provider replacement, failed activation, disposal, fabricated public object, or matching numeric revision from another controller fails.

The archive may use stored or deflated entries. The entire candidate is limited to 20 MiB, and each entry is independently read with an exact maximum-plus-one ceiling: 4 KiB manifest, 16 MiB image, and 384-byte signature. The expanded byte count must equal ZIP metadata, so forged central-directory sizes fail closed. The caller's original stream position is restored in `finally` after success or failure.

Inspection verifies structure, image length, SHA-256, and nonempty fixed-size signature presence. It has no file chooser, device input, admission output, or operation authority.

## Write direction

Product selection can never be reused as write approval. A future writer requires, at minimum, exact product provider, exact received-device identity, exact target manifest, admitted signer/release generation, complete address/file/hash plan, bounded attempt count, readback verification, boot confirmation, and accepted recovery plan. Owner authorization remains a separate operation-scoped gate.
