# Build guide

Introductory text that is not a command.

## Windows build

```powershell
git clone --recursive https://example.invalid/repository.git
cmake -S . -B build
```

## Verification

```text
cmake --build build --config Release
```
