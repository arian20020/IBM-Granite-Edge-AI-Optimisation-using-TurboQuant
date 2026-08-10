# Model Inspection Visual Studio Debug guide

This beginner guide exercises the existing packaged WinUI Model Inspection
journey on the x64 GGUF, CPU-only, LLamaSharp/llama.cpp `VocabOnly` path. It
does not add or validate OpenVINO, TurboQuant, GPU execution, full inference,
context creation, conversion, report export, or a benchmark.

The current Task 12 evidence did not perform this manual journey. When an
operator follows it, record the commit and non-private observations separately;
do not paste local usernames, absolute model paths, or raw test reports into
durable evidence.

## Step 1: Open the solution, not Folder View

In Visual Studio choose **File > Open > Project or Solution**, select
`IBM Granite with TurboQuant (Intel).slnx`, and click **Open**. If Solution
Explorer says **Folder View**, close it and open the `.slnx` again.

## Step 2: Select Debug and x64

In the main toolbar set **Solution Configurations** to `Debug` and **Solution
Platforms** to `x64`. Do not use Any CPU, x86, or ARM for this journey.

## Step 3: Set the WinUI application as startup project

In Solution Explorer right-click the `IBM Granite with TurboQuant (Intel)`
WinUI project, then choose **Set as Startup Project**. Its name should appear
beside the Start button.

## Step 4: Choose the actual packaged profile

Open the Start target list and select
`IBM Granite with TurboQuant (Intel) (Package)`. This is the profile declared
in `launchSettings.json` with command name `MsixPackage`; Visual Studio uses it
for the packaged Local Machine launch semantics required by this journey. Do
not select `IBM Granite with TurboQuant (Intel) (Unpackaged)`: the worker
manifest, application resources, and package identity are part of the journey
being checked.

## Step 5: Stop stale processes if a DLL is locked

First choose **Debug > Stop Debugging**. If Restore or Rebuild still reports a
locked DLL, close stale app/test windows and use Task Manager to end only the
stale `MSBuild.exe`, `GraniteEdgeAI.WinUI.UnitTests`,
`GraniteEdgeAI.ModelInspection.Worker`, or
`GraniteEdgeAI.ModelInspection.ProtocolTestWorker` process from this checkout.
Do not stop unrelated editor or system processes.

## Step 6: Restore and Rebuild

Choose **Build > Restore NuGet Packages**, wait for Restore to finish, then
choose **Build > Rebuild Solution**. Fix build errors before continuing; a
warning or failed build is not acceptance evidence.

## Step 7: Start with F5

Press **F5** or choose **Debug > Start Debugging**. Wait for the packaged WinUI
window to appear and for the onboarding shell to become responsive.

## Step 8: Select a valid GGUF

Use the model-selection journey to choose a valid local `.gguf` file whose use
you are authorized to test. Record only a safe scenario label, never its full
path or private metadata. This check does not require an external Granite model.

## Step 9: Observe the five factual stages

Confirm the progress surface advances in this exact order and reports only
facts emitted by the worker:

1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility

The rows may advance quickly. Do not infer a missing stage from animation or
invent a value that the selected file did not report.

## Step 10: Expand and collapse available details

On the terminal card reached by the valid file, activate the details disclosure.
Verify **Expand** exposes the current bounded rows and **Collapse** hides them
without replacing the surrounding page, losing focus, or changing the outcome.

## Step 11: Exercise current recovery and navigation actions

During a run verify **Cancel** becomes unavailable after it is invoked. Where
the reached outcome offers them, verify **Retry** starts a new attempt and
**Choose another** returns to model selection. Do not claim an action for a
state the selected file did not reach.

## Step 12: Repeat with animation effects disabled

Stop debugging, open **Windows Settings > Accessibility > Visual effects**, and
turn **Animation effects** off. Start again with F5 and repeat the reached path.
The same state and focus endpoints must appear immediately without a motion-only
loss of meaning. Restore the operator's setting afterward.

## Step 13: Use keyboard-only navigation

Without the mouse, use Tab and Shift+Tab to reach disclosures and current
actions; use Enter or Space to activate them. Verify focus stays visible,
keyboard-only disclosure navigation works in both directions, and disabled
future actions do not enter the tab sequence.

## Step 14: Repeat at 200% Windows text scale

Set **Windows Settings > Accessibility > Text size** to `200%`, sign out or
restart the application if Windows requests it, and repeat the reached journey.
Inspect required text, action labels, progress rows, details, and scrolling for
critical clipping. Restore the operator's text setting afterward.

## Step 15: Repeat in Windows High Contrast

Enable an actual Windows **High Contrast** theme, restart the packaged app, and
repeat the reached journey. Verify visible focus, status meaning beyond color,
system-resolved text/background contrast, and usable disclosures/actions.
Restore the operator's theme afterward.

## Step 16: Record one Narrator pass

Start Narrator and perform one keyboard journey. Record whether the polite
progress region announces genuine stage changes and whether the terminal region
announces the outcome once per attempt. Stop Narrator afterward. This guide asks
for a recorded pass; the current Task 12 evidence says it was **NOT RUN**.

## Step 17: Verify future actions remain non-executing

On the Ready or Ready-with-warnings state actually reached, verify visible
future actions are disabled and expose `Coming later` help. Hardware Fit,
conversion, and report actions must not claim execution. The
conversion/report-only states were not exercised by this manual path; use the
packaged presentation tests referenced in
`docs/testing/Model-Inspection-Test-Completeness-Matrix.md` for their current
deterministic presentation coverage, not debug state injection.

## What this journey cannot close

One developer-machine pass cannot prove exact Figma pixels, actual controlled
High Contrast or 200% evidence on an approved pinned runner, Narrator acceptance
that was not recorded, or hosted exact-head CI. It also cannot be used to claim
OpenVINO, TurboQuant, GPU, full inference, context creation, Hardware Fit,
conversion execution, report export, or benchmark support.
