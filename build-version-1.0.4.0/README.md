# Granite build 1.0.4.0 for Windows

This folder contains the Windows test build currently shared with schools. Download the setup ZIP from this folder, then follow the setup guide in the repository root.

## Download and run

1. Download [Granite-Edge-AI-Setup.zip](Granite-Edge-AI-Setup.zip) and save it in your Downloads folder.
2. Follow [Setup and first use](../README.md#setup-and-first-use), starting at **1. Download the application ZIP**. The guide verifies and extracts the package, installs and launches Granite, and prepares the model files.

Package size: 585,596,470 bytes. SHA-256: `6788E6E293EB1D7129733F4C7246AABA26F8BC5F068751C35806418C17391126`.

Use Windows 11 x64 and allow at least 20 GB of free disk space; optimisation may need more. This is a self-signed test build. Installation and launch do not guarantee that every model workflow will work on every laptop. Follow your school's software and security policies; do not bypass Windows security controls.

## Recorded test hardware and execution

The project's 14 July 2026 environment record identifies a Lenovo Windows 11 laptop with a 12th Gen Intel Core i5-12450H, Intel UHD Graphics, and 16 GiB installed RAM (16,857,817,088 bytes recorded). This identifies the recorded experiment laptop; the repository does not establish that it was the laptop shown in the demonstration video.

Controlled runtime records include CPU execution and Intel UHD integrated-GPU execution through Vulkan. These are separate benchmark experiments and do not establish that every app workflow uses the GPU. No NPU execution test is recorded. See the [environment register](../docs/testing/Environment-Register.csv) and [device verification register](../docs/testing/Device-Verification-Register.csv).

## Package status

The setup ZIP checksum matches the value required by the installation guide. A clean installation test has not been established. Treat this as a test build, not a verified production release. See [release handover](../release-evidence/README.md) for the evidence and outstanding release checks.
