# Granite test build 1.0.5.0 for Windows

This is the **Granite Local Preview** package tested on one Windows 11 laptop. It was assembled from the 1.0.4.0 package with the model-inspection storage fix; it is not a complete source rebuild or a clean-machine certification. It uses a new, self-signed test certificate and installs alongside 1.0.4.0 under a separate publisher identity.

Follow the [main setup instructions](../README.md#setup-and-first-use), including Step 6A to install the separately downloaded pinned `llmfit` tool needed for hardware compatibility. The installer asks before trusting the certificate for the computer. Keep the previous installation and models until you have verified the preview on your own computer.

Download [Granite-Edge-AI-Setup.zip](Granite-Edge-AI-Setup.zip). Size: 584,521,838 bytes. SHA-256: `87AD9CE4E829F8DD4CF3709D6851B693DCA38B635AB9F5D768619FFB64A44AAC`.

The test package was used to reach model and hardware compatibility with GGUF and to test an OpenVINO model on the same laptop. Results on other computers and clean installs remain unverified. Do not bypass Windows or organisational security policies.
