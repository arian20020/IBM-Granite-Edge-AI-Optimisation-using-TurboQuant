# Model Inspection infrastructure composition

This folder is the only WinUI source boundary allowed to depend on
`GraniteEdgeAI.ModelInspection.WorkerClient`. It composes the protected client;
it never references the worker host, LLamaSharp runtime, or native libraries.

## Approved root selection

- A packaged process uses `Package.Current.InstalledLocation.Path` after the
  native package-identity probe confirms package identity.
- A controlled unpackaged development/test process uses
  `AppContext.BaseDirectory`. This route is explicitly non-adversarial: the
  static integrity snapshot rejects pre-existing tampering, but it does not
  claim protection from a concurrent writer after verification.
- An invalid packaged root fails closed; it never falls back to the unpackaged
  root.
- Current directory and `PATH` search never participate.

The client then resolves exactly:

```text
ModelInspection\Worker\GraniteEdgeAI.ModelInspection.Worker.exe
```

The detached closure manifest is stored at
`ModelInspection\worker-manifest.json`. The x64 application build and packaged
test layout contain 44 worker files beneath the Worker directory. LLamaSharp
and the 20 CPU native DLLs remain in that subtree and are not loaded by WinUI.
The same manifest is embedded in the application assembly. Immediately before
each execution, the composition compares the detached manifest to that trusted
copy, rejects reparse points or missing/extra files, and verifies every declared
length and SHA-256 digest. Any mismatch returns a fixed integrity failure before
the process client can launch the worker. The supported installed-package route
combines this check with Windows package immutability; only that route carries
the production closure-integrity guarantee.

## Scope

This composition does not start inspection by itself. The application service
will own request mapping, progress, cancellation, stale-run suppression, and
terminal-result classification in the next production gate.
