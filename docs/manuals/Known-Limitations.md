# Known limitations and troubleshooting

This page describes the supported boundary and recorded gaps. It is not a list of every historical bug. Earlier package failures were followed by passing inspection tests; they are not presented as current failures on every installation.

## What the evidence supports

| Area | What is recorded | Limit |
| --- | --- | --- |
| GGUF inspection | A real local import reached compatibility. | Not every GGUF architecture or quantisation is supported. |
| OpenVINO inspection | Raw 3B reached an enabled configuration slider and Start action. | This does not test every band or execute optimisation. |
| Invalid input | Replacing a rejected GGUF with a valid one completed inspection. | Not every recovery scenario is covered. |
| Offline use | The developer reported inspection, optimisation, export and chat without Wi-Fi. | Exact route/build details and a network trace were not recorded for that manual check. |
| Hardware fit | The app uses inspection facts and memory/disk checks. | Estimates were not calibrated across physical 4-GB and 8-GB devices. |
| TurboVec | A separate demonstration was evaluated. | It did not meet retrieval-quality rules and is not part of the application workflow. |

The recorded release choices include GGUF TurboQuant3Bit, TurboQuant4Bit and Q8_0. OpenVINO Raw 3B mappings include INT4/TBQ3, INT4/TBQ4, INT4/U4, INT4/U8 and INT8/Automatic. These are not a promise that all five or all three will be offered for every source or free-memory level. Candidate admission and current resource checks still apply.

## Open gaps

- Three chat-scrolling checks and four inspection presentation/accessibility checks remain deferred.
- A source-conversion recovery test was removed after an invalid worker manifest stopped the test host. Removing it did not fix recovery.
- An ordered export-cleanup test does not cover the earlier overlapping-timeout case.
- The three automated inspection journeys do not cover optimisation, export, chat, every slider choice or changing RAM conditions.
- Clean-machine installation, full keyboard/text-scale acceptance, a formal user study and a full security assessment were not confirmed.

See [test scope](../testing/CI-Test-Scope.md) for exact cases and [recorded results](../testing/application-verification/README.md) for evidence. Excluded and deferred tests are not passes.

## Troubleshooting

The actions below are recovery suggestions, not proof of a cause. In particular, reopening after stalled download navigation is an unverified workaround. Missing slider choices can result from resource checks or a fault; do not assume every missing choice is correct.

| What you see | What it means / what to do |
| --- | --- |
| Setup ZIP checksum mismatch | Use the current [starter guide](Granite-Start-Here.md) and complete Step 1. Step 2 accepts numbered browser filenames but only the expected checksum. Do not change the checksum to match an unknown ZIP. |
| Old extraction has missing or extra files | The current guide uses `C:\Downloads\Granite-Edge-AI-Setup-1.0.4`, leaving older setup folders alone. If that versioned folder differs, rename only that setup folder before repeating Step 2. Preserve models. |
| OpenVINO package_unsafe_path after browser download | Complete Step 6 of the starter guide. It removes Windows download marks only after verifying the model files and prepares the correct folder. If the error persists, report it; do not bypass unsafe-path checks. |
| OpenVINO asks to import the model again | Free up storage if needed, then use **Import model again** and repeat inspection. Keep the original model folder. |
| Continue is disabled after local selection | Wait for the quick scan. Read any input failure and choose a complete supported source. |
| Model inspection could not start / runtime_load_failed | A required runtime could not be used. Record the code and check that the installed package is the expected complete package. Do not replace DLLs or bypass verification. |
| Invalid evidence / runtime_protocol_failed | The runtime result was rejected. Record the message and package identity; use the offered recovery. A different model is not guaranteed to fix a package problem. |
| Inspection tool could not be verified | The hardware tool was not accepted. No conclusion about the computer was made. Ask the maintainer to check the runtime package. |
| We can't answer this yet | Required model or runtime facts were missing or unusable. Read the details and use the offered recovery; do not treat it as a confirmed memory failure. |
| Not enough free RAM | Close memory-heavy programs, then run a fresh check. Never disable the safety reserve to force a pass. |
| Slider returns to another choice or selection disappears | The requested candidate may no longer pass current checks. Return to compatibility. Persistent behaviour with the same inputs should be reported. |
| Save or optimisation fails | Check the displayed reason and free disk space. Keep the original model; do not use partial outputs as completed exports. |
| Download reports verified but navigation does not continue | This was reported after returning to import; it was not confirmed resolved by the current tests. Once no operation is active, reopen the app and retry, recording the package and message if it recurs. |

Use the recovery button shown by the current screen. Not every state has a retry button. Avoid running several inspection or optimisation sessions at once when reproducing a problem.
