# Architecture

## Repository boundary

This project is the shared customer-tool boundary. It does not own OpenTrail or OpenGauge firmware. Product projects remain authoritative for compatibility identifiers, target manifests, image hashes, signatures, recovery procedures, and physical acceptance.

## First-screen authority

`LoaderProductCatalog` contains exactly two product families with stable engineering keys:

| Engineering key | Public working name | Current production provider state |
| --- | --- | --- |
| `opentrail` | Limited Underground Trail | Lifecycle-v1 offline-inspection provider active for exact `heltec_v4_bench` target rules |
| `opengauge` | Limited Underground Display | Provider and accepted target manifest do not exist |

The display names are presentation only. They are not wire values, schema keys, cryptographic context, or hardware identifiers.

`LoaderSessionController` owns one scalar selection and monotonically increasing revision. Selecting a different product or returning to the chooser invalidates the previous revision. Unknown keys fail without mutation. Reselecting the exact current product is a no-op.

Every current snapshot reports all operational capabilities false:

- connected-device inspection;
- firmware-bundle selection;
- device/bundle matching;
- firmware writing; and
- recovery.

These operational capabilities remain false. A separate `OfflineBundleInspectionAvailable` projection becomes true only for an active exact Trail provider lease; it grants access solely to the stream-based inspector and never implies signer, device, admission, or installation authority.

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

Production registers one provider factory for product/provider key `opentrail` and contract version `1`. Its immutable rule set contains only `heltec_v4_bench`; both the rule identity and source revision pin SHA-256 `ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776` for the Git blob bytes of OpenTrail's public target contract at commit `a327104ac67a3f5918a8b0191c96dceb05b5399b`. The provider owns no file, device, transport, signer, admission, or mutation adapter and closes without external work.

Production signer trust remains empty. Display remains providerless. The default controller constructor also remains empty for isolated consumers and lifecycle tests; the WPF composition root explicitly injects the production registry.

## Offline bundle-candidate boundary

`FirmwareBundleCandidateInspector` owns one product-neutral candidate schema. The archive must contain exactly `manifest.json`, `image.bin`, and `manifest.sig`; manifest property order and encoding are canonical; archive, manifest, image, signature, identifier, and integer sizes are bounded. The manifest product key must be exactly `opentrail` or `opengauge`.

A caller cannot fabricate accepted authority: the controller mints an immutable context only from an active provider lease. The context binds opaque controller and activation tokens, exact context reference, selected product, session revision, provider key, contract version, provider generation, target-rule source revision, and optional signer-trust source revision. Result construction is internal to the inspector and retains the exact context object.

Publication requires every binding to remain current, verified structure/digest/signature presence, an exact product match, an exact case-sensitive target in the active project-owned rule set, and false signer-trust/admission flags. A switch, chooser return, provider replacement, failed activation, disposal, fabricated public object, or matching numeric revision from another controller fails.

The archive may use stored or deflated entries. The entire candidate is limited to 20 MiB, and each entry is independently read with an exact maximum-plus-one ceiling: 4 KiB manifest, 16 MiB image, and 384-byte signature. The expanded byte count must equal ZIP metadata, so forged central-directory sizes fail closed. The caller's original stream position is restored in `finally` after success or failure.

Inspection verifies structure, image length, SHA-256, and nonempty fixed-size signature presence. It has no device input, admission output, or operation authority.

## Local offline inspection surface

The WPF composition root enables a single-file chooser only when `OfflineBundleInspectionAvailable` is true. The window disables recent-file registration, opens the selected file with `FileMode.Open`, `FileAccess.Read`, and `FileShare.Read`, and passes only the stream to `OfflineBundleInspectionWorkflow`. The workflow owns no path or filename field, catches malformed candidate data into fixed messages, rechecks current controller publication authority, and returns immutable sanitized display fields. The UI never displays the path or raw exception text and clears every rendered result on a new attempt, product change, chooser return, or close. Canceling the native picker leaves the prior inspected result unchanged because no new candidate was attempted.

## Write direction

Product selection can never be reused as write approval. A future writer requires, at minimum, exact product provider, exact received-device identity, exact target manifest, admitted signer/release generation, complete address/file/hash plan, bounded attempt count, readback verification, boot confirmation, and accepted recovery plan. Owner authorization remains a separate operation-scoped gate.
