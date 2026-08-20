# GGUF runtime worker client

`GgufRuntimeClient.CreateFromPackage` is the production construction boundary.
It requires trusted manifest bytes, a fixed package root, and an explicit
absolute model file. The detached manifest must match the trusted bytes before
the package and model inputs are accepted.

The client launches the supervisor with an explicit executable path, inherited
pipe allowlist, stripped environment, and kill-on-close process containment.
