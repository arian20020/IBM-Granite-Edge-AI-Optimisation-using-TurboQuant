# Application and Experimental Testing Strategy

**Version:** 1.0  
**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Effective date:** 13 July 2026  
**Status:** Controlling strategy

## 1. Purpose

This strategy governs the repeatable testing of IBM Granite models across upstream llama.cpp, AtomicBot TurboQuant, animehacker TQ3_0, official OpenVINO, the custom OpenVINO TurboQuant route and the final cross-route comparison.

## 2. Core principles

- Trace every test to a requirement, research question, technical risk or comparison objective.
- Establish a working baseline before testing an optimisation.
- Change one material variable at a time where practical.
- Record exact repositories, commits, build options, model hashes, prompts and commands.
- Separate requested configuration from verified runtime behaviour.
- Preserve raw logs and failures; never replace an unsuccessful run with only the successful retest.
- Separate measured results, calculated values, estimates and published claims.
- Use matched conditions before comparing routes.
- Treat TurboQuant and custom OpenVINO integration as experimental until activation and benefit are directly demonstrated.
- Do not claim universal Granite, Intel, OpenVINO or TurboQuant compatibility from a limited test set.

## 3. Evidence levels

1. **Raw evidence:** stdout, stderr, command files, runtime logs, system snapshots and original model output.
2. **Run manifest:** machine-readable record of environment, model, runtime, configuration and evidence paths.
3. **Processed result:** calculations derived from raw evidence without changing the source files.
4. **Workbook record:** human-readable test result linked to the run ID and evidence commit.
5. **Conclusion:** statement supported by one or more validated runs and bounded to the tested configuration.

## 4. Minimum evidence for every run

- test ID and run ID;
- UTC and local timestamp;
- environment ID;
- repository URL and exact commit;
- build configuration and executable hash where available;
- model source, revision, filename and SHA-256;
- full command and environment variables;
- requested and actual backend/device;
- prompt or dataset version;
- exit code, stdout and stderr;
- result classification and reason;
- evidence directory and Git commit.

## 5. Completion rule

A test is complete only when the run is classified, the evidence is stored, the registers are updated, the relevant workbook section is completed and the evidence commit is recorded.

## 6. Engineering basis

This strategy applies systems-engineering test planning and traceability, software-product testing and code-management practices, and AI-engineering evaluation discipline for models, metrics, data and experimental configuration.
