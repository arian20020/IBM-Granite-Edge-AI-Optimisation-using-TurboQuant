# Application and System Test Projects

This directory follows the hierarchy required by the supplied project testing standard. Folder creation is preparation only; executable tests and preserved results are the evidence.

```text
tests/
├── Unit/
│   ├── ModelImport/
│   ├── ModelInspection/
│   ├── Classification/
│   ├── Hardware/
│   ├── MemoryEstimation/
│   ├── ConfigurationSelection/
│   ├── Conversion/
│   └── Chat/
├── Contracts/
│   ├── LlamaCpp/
│   ├── OpenVINO/
│   ├── LLMFit/
│   └── ConversionTools/
├── Integration/
│   ├── ImportInspection/
│   ├── HardwareEstimation/
│   ├── SelectorBackend/
│   ├── UiBackend/
│   └── ConversionExport/
├── EndToEnd/
│   ├── CoreJourneys/
│   └── FailureJourneys/
├── Fixtures/
│   ├── GGUF/
│   ├── OpenVINO/
│   ├── Safetensors/
│   ├── Malformed/
│   └── ExpectedMetadata/
├── Performance/
│   ├── LlamaCpp/
│   ├── OpenVINO/
│   ├── TurboQuant/
│   ├── Memory/
│   └── ContextScaling/
├── AIQuality/
│   ├── Prompts/
│   ├── References/
│   ├── Rubrics/
│   └── Outputs/
├── Security/
├── Accessibility/
└── Installation/
```

## Evidence rule

- Unit tests verify deterministic business logic.
- Contract tests verify each runtime adapter against pinned tool behaviour.
- Integration tests verify component boundaries and process communication.
- End-to-end tests verify complete user journeys and important failure journeys.
- Performance and AI-quality results belong under `experiments/granite_turboquant_intel/` and must cite the matching application test where relevant.
- Fixtures must be small, licensed and safe to commit; model weights are never stored here.
