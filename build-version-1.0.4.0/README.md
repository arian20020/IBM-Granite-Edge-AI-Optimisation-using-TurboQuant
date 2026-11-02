# Granite build 1.0.4.0 for Windows

This folder retains the earlier Windows test build. The current setup guide points to the separate 1.0.5.0 test preview.

## Download and run

**Historical package limitation:** Version 1.0.4.0 failed model inspection on one fresh laptop due to Windows package storage redirection. The later [1.0.5.0 test preview](../build-version-1.0.5.0/README.md) contains the tested fix and uses a different self-signed publisher. Keep existing models when testing it.

For the newer test build, follow [Setup and first use](../README.md#setup-and-first-use). The old [1.0.4.0 ZIP](Granite-Edge-AI-Setup.zip) is retained for reference; the current guide verifies and installs the different 1.0.5.0 ZIP.

Package size: 585,596,470 bytes. SHA-256: `6788E6E293EB1D7129733F4C7246AABA26F8BC5F068751C35806418C17391126`.

Use Windows 11 x64 and allow at least 20 GB of free disk space; optimisation may need more. This is a self-signed test build. Installation and launch do not guarantee that every model workflow will work on every laptop. Follow your school's software and security policies; do not bypass Windows security controls.

## Recorded test hardware and execution

The project's 14 July 2026 environment record identifies a Lenovo Windows 11 laptop with a 12th Gen Intel Core i5-12450H, Intel UHD Graphics, and 16 GiB installed RAM (16,857,817,088 bytes recorded). This identifies the recorded experiment laptop; the repository does not establish that it was the laptop shown in the demonstration video.

Controlled runtime records include CPU execution and Intel UHD integrated-GPU execution through Vulkan. These are separate benchmark experiments and do not establish that every app workflow uses the GPU. No NPU execution test is recorded. See the [environment register](../docs/testing/Environment-Register.csv) and [device verification register](../docs/testing/Device-Verification-Register.csv).

## Package status

The setup ZIP checksum matches the value required by the installation guide. A clean installation test has not been established. Treat this as a test build, not a verified production release. See [release handover](../release-evidence/README.md) for the evidence and outstanding release checks.
