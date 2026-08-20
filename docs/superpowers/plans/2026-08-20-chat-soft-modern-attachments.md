# Chat Soft-Modern Polish and Knowledge Attachments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved soft-modern B visual direction, full Granite Edge AI lockup, structurally centred composer, and honest `.txt`/`.md` knowledge-file selection with removable **Not indexed** chips.

**Architecture:** Keep visual tokens and reusable control styles in the existing chat resource dictionary. Add a small framework-neutral attachment domain under `Features/GgufRuntime/Attachments`, isolate the Windows picker behind `IKnowledgeFilePicker`, and let `ChatComposer` own only presentation and selection orchestration. No file is read, copied, embedded, logged, or added to a prompt in this plan.

**Tech Stack:** .NET 8, C# 12, WinUI 3, Windows App SDK storage pickers, MSTest AppContainer UI tests

---

## File Structure

| Path | Responsibility |
|---|---|
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml` | Direction-B colours, gradients, shadows, and shared button styles |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml` | Refined page surfaces, full lockup sizing, and styled rail actions |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/KnowledgeAttachment.cs` | Immutable safe attachment presentation model |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/KnowledgeAttachmentPolicy.cs` | Allowed extensions, bounded count, duplicate handling, and safe validation results |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/IKnowledgeFilePicker.cs` | Testable picker boundary returning path and bounded file metadata without content |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/WindowsKnowledgeFilePicker.cs` | Windows App SDK `.txt`/`.md` multi-file picker adapter |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml` | Two-row attachment/chip presentation and centred prompt row |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs` | Flyout, picker orchestration, chip removal, and non-indexed state |
| `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Attachments/KnowledgeAttachmentPolicyTests.cs` | Pure policy coverage |
| `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs` | Native WinUI composer interaction and layout coverage |
| `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs` | Source/package visual contract guard |

### Task 1: Add the soft-modern theme contract

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`

- [ ] **Step 1: Write the failing visual-token test**

Add a test that loads `GgufChatTheme.xaml` and requires the new named resources:

```csharp
[TestMethod]
public void ChatThemeDefinesSoftModernSurfaceAndControlResources()
{
    string root = FindRepositoryRoot();
    string theme = File.ReadAllText(Path.Combine(root,
        "IBM Granite with TurboQuant (Intel)", "Features", "GgufRuntime",
        "Presentation", "GgufChatTheme.xaml"));

    StringAssert.Contains(theme, "GgufChatPrimaryGradientBrush");
    StringAssert.Contains(theme, "GgufChatPanelBorderBrush");
    StringAssert.Contains(theme, "GgufChatSecondaryButtonStyle");
    StringAssert.Contains(theme, "GgufChatPrimaryButtonStyle");
    StringAssert.Contains(theme, "PointerOver");
    StringAssert.Contains(theme, "Pressed");
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test "tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" -c Release --filter "ChatThemeDefinesSoftModernSurfaceAndControlResources"
```

Expected: FAIL because `GgufChatPrimaryGradientBrush` is absent.

- [ ] **Step 3: Add the minimal direction-B tokens and styles**

Keep the forced light route and add resources equivalent to:

```xml
<LinearGradientBrush x:Key="GgufChatPrimaryGradientBrush" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#3979F6" Offset="0" />
    <GradientStop Color="#6558E8" Offset="1" />
</LinearGradientBrush>
<SolidColorBrush x:Key="GgufChatPanelBorderBrush" Color="#DCE7F7" />
<SolidColorBrush x:Key="GgufChatSecondarySurfaceBrush" Color="#F8FAFE" />
<SolidColorBrush x:Key="GgufChatFocusBrush" Color="#155EEF" />
<ThemeShadow x:Key="GgufChatPanelShadow" />
<Style x:Key="GgufChatPrimaryButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="{StaticResource GgufChatPrimaryGradientBrush}" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="CornerRadius" Value="12" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
</Style>
<Style x:Key="GgufChatSecondaryButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="{ThemeResource GgufChatSecondarySurfaceBrush}" />
    <Setter Property="BorderBrush" Value="{ThemeResource GgufChatPanelBorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="12" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
</Style>
```

Define explicit default, pointer-over, pressed, disabled, and focus visuals in each control template. Retain High Contrast theme resources and do not replace system focus cues with colour-only cues.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml" "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs"
git commit -m "style(chat): add soft modern control theme"
```

### Task 2: Apply the polished page surfaces and full lockup

**Files:**
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Verify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`

- [ ] **Step 1: Extend the failing visual contract**

Require the complete lockup, named elevated panel, and shared button styles:

```csharp
StringAssert.Contains(page.ToString(), "granite-edge-ai-lockup.svg");
StringAssert.Contains(page.ToString(), "x:Name=\"ConversationPanel\"");
StringAssert.Contains(page.ToString(), "GgufChatPrimaryButtonStyle");
StringAssert.Contains(page.ToString(), "GgufChatSecondaryButtonStyle");
StringAssert.Contains(project, "docs\\Logo\\granite-edge-ai-lockup.svg");
```

- [ ] **Step 2: Run the focused test and verify RED**

Run the visual contract test class. Expected: FAIL because the named conversation panel and styles are absent.

- [ ] **Step 3: Refine `ChatPage.xaml`**

Use the existing full lockup image and align it to the same left margin as the controls:

```xml
<Image x:Name="BrandLockup"
       Width="232" Height="64"
       HorizontalAlignment="Left"
       Stretch="Uniform"
       Source="ms-appx:///Assets/Branding/granite-edge-ai-lockup.svg" />
```

Apply `GgufChatPrimaryButtonStyle` to New Chat, `GgufChatSecondaryButtonStyle` to Import Model and Settings, and place the main content in:

```xml
<Border x:Name="ConversationPanel"
        Background="{ThemeResource GgufChatSurfaceBrush}"
        BorderBrush="{ThemeResource GgufChatPanelBorderBrush}"
        BorderThickness="1"
        CornerRadius="24"
        Shadow="{StaticResource GgufChatPanelShadow}"
        Translation="0,0,16">
    <!-- existing header, transcript, empty state, and composer -->
</Border>
```

Preserve all existing named elements and behavior bindings.

- [ ] **Step 4: Run the test and build**

```powershell
dotnet test "tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" -c Release --filter "GgufChatVisualContractTests"
dotnet build "IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj" -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --no-restore
```

Expected: tests pass; build succeeds with zero errors.

- [ ] **Step 5: Commit**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml" "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs"
git commit -m "style(chat): refine the conversation workspace"
```

### Task 3: Add bounded attachment domain policy

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/KnowledgeAttachment.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/KnowledgeAttachmentPolicy.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Attachments/KnowledgeAttachmentPolicyTests.cs`

- [ ] **Step 1: Write failing policy tests**

Cover accepted case-insensitive extensions, duplicates, and the eight-file bound:

```csharp
[TestMethod]
public void Validate_AcceptsTextAndMarkdownWithoutReadingContent()
{
    var result = KnowledgeAttachmentPolicy.Validate(
        new[]
        {
            new KnowledgeFileCandidate(@"C:\docs\notes.TXT", 120, true),
            new KnowledgeFileCandidate(@"C:\docs\design.md", 240, true)
        },
        Array.Empty<KnowledgeAttachment>());

    Assert.AreEqual(2, result.Accepted.Count);
    Assert.IsTrue(result.Accepted.All(item => item.StateText == "Not indexed"));
    Assert.AreEqual(0, result.Rejections.Count);
}

[TestMethod]
public void Validate_RejectsUnsupportedDuplicateAndNinthFile()
{
    var existing = new[] { new KnowledgeAttachment(@"C:\docs\a.txt") };
    KnowledgeFileCandidate[] selected =
    {
        new(@"C:\docs\a.txt", 10, true), new(@"C:\docs\b.pdf", 10, true),
        new(@"C:\docs\2.txt", 10, true), new(@"C:\docs\3.txt", 10, true),
        new(@"C:\docs\4.txt", 10, true), new(@"C:\docs\5.txt", 10, true),
        new(@"C:\docs\6.txt", 10, true), new(@"C:\docs\7.txt", 10, true),
        new(@"C:\docs\8.txt", 10, true), new(@"C:\docs\9.txt", 10, true)
    };

    KnowledgeAttachmentValidationResult result =
        KnowledgeAttachmentPolicy.Validate(selected, existing);

    CollectionAssert.Contains(result.Rejections.Select(x => x.Code).ToList(),
        "attachment-unsupported-type");
    CollectionAssert.Contains(result.Rejections.Select(x => x.Code).ToList(),
        "attachment-duplicate");
    CollectionAssert.Contains(result.Rejections.Select(x => x.Code).ToList(),
        "attachment-count-exceeded");
}
```

- [ ] **Step 2: Run the tests and verify RED**

```powershell
dotnet test "tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj" -c Release -p:Platform=x64 --filter "FullyQualifiedName~KnowledgeAttachmentPolicyTests"
```

Expected: compile failure because the attachment types do not exist.

- [ ] **Step 3: Implement immutable models and policy**

Use these public shapes:

```csharp
internal sealed record KnowledgeAttachment(string Path)
{
    internal string FileName => System.IO.Path.GetFileName(Path);
    internal string StateText => "Not indexed";
}

internal sealed record KnowledgeFileCandidate(
    string Path,
    long SizeInBytes,
    bool IsAccessible);

internal sealed record KnowledgeAttachmentRejection(string Code, string FileName);

internal sealed record KnowledgeAttachmentValidationResult(
    IReadOnlyList<KnowledgeAttachment> Accepted,
    IReadOnlyList<KnowledgeAttachmentRejection> Rejections);
```

`KnowledgeAttachmentPolicy.Validate` must use ordinal-ignore-case path and extension comparisons, accept only `.txt` and `.md`, preserve picker order, cap the combined list at eight, reject inaccessible candidates, reject zero-byte and over-8-MiB candidates, expose filenames rather than full paths in rejections, and never open a file.

- [ ] **Step 4: Run the tests and verify GREEN**

Run the command from Step 2. Expected: all policy tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Attachments"
git commit -m "feat(chat): add bounded knowledge attachment policy"
```

### Task 4: Add the Windows knowledge-file picker boundary

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/IKnowledgeFilePicker.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments/WindowsKnowledgeFilePicker.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs`

- [ ] **Step 1: Write the failing source contract**

Require `.txt`, `.md`, and multi-file picking without file reads:

```csharp
string picker = File.ReadAllText(Path.Combine(root,
    "IBM Granite with TurboQuant (Intel)", "Features", "GgufRuntime",
    "Attachments", "WindowsKnowledgeFilePicker.cs"));
StringAssert.Contains(picker, "PickMultipleFilesAsync");
StringAssert.Contains(picker, "\".txt\"");
StringAssert.Contains(picker, "\".md\"");
Assert.IsFalse(picker.Contains("ReadAllText", StringComparison.Ordinal));
Assert.IsFalse(picker.Contains("OpenRead", StringComparison.Ordinal));
```

- [ ] **Step 2: Run the focused test and verify RED**

Expected: FAIL because the picker file is absent.

- [ ] **Step 3: Implement the interface and adapter**

```csharp
internal interface IKnowledgeFilePicker
{
    Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync();
}

internal sealed class WindowsKnowledgeFilePicker : IKnowledgeFilePicker
{
    public async Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync()
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(
            App.MainWindow.AppWindow.Id)
        {
            Title = "Add knowledge files",
            CommitButtonText = "Attach"
        };
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");
        IReadOnlyList<Microsoft.Windows.Storage.Pickers.PickFileResult> results =
            await picker.PickMultipleFilesAsync();
        return results.Select(result =>
        {
            try
            {
                var info = new FileInfo(result.Path);
                return new KnowledgeFileCandidate(result.Path, info.Length, true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return new KnowledgeFileCandidate(result.Path, 0, false);
            }
        }).ToArray();
    }
}
```

An empty picker result represents cancellation and is not an error.

- [ ] **Step 4: Run the visual contract and app build**

Expected: contract passes and the Windows App SDK API compiles.

- [ ] **Step 5: Commit**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Attachments" "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufChatVisualContractTests.cs"
git commit -m "feat(chat): add knowledge file picker adapter"
```

### Task 5: Build the attachment flyout, chips, and centred composer

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`

- [ ] **Step 1: Write failing native UI tests**

Add tests for the active attachment button, menu item, chip presentation, removal, and centring:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public void ComposerExposesKnowledgeAttachmentActionAndCenteredPrompt()
{
    var composer = new ChatComposer();
    var attachment = Assert.IsInstanceOfType<Button>(composer.FindName("AttachmentButton"));
    var prompt = Assert.IsInstanceOfType<TextBox>(composer.FindName("PromptTextBox"));
    var flyout = Assert.IsInstanceOfType<MenuFlyout>(attachment.Flyout);
    var add = Assert.IsInstanceOfType<MenuFlyoutItem>(flyout.Items.Single());

    Assert.IsTrue(attachment.IsEnabled);
    Assert.AreEqual("Add knowledge files", AutomationProperties.GetName(attachment));
    Assert.AreEqual("Add knowledge files...", add.Text);
    Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalAlignment);
    Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalContentAlignment);
}
```

Add an injected fake picker test that returns two paths, invokes the internal selection method, asserts two chips with **Not indexed**, removes one chip, and verifies prompt submission contains only typed prompt text.

- [ ] **Step 2: Run the focused tests and verify RED**

Run `ChatComposerTests`. Expected: FAIL because the attachment button is disabled and has no flyout.

- [ ] **Step 3: Restructure the composer XAML**

Use an outer border containing two rows:

```xml
<Grid RowSpacing="8">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <ItemsControl x:Name="AttachmentItems"
                  Visibility="Collapsed"
                  AutomationProperties.Name="Selected knowledge files" />
    <Grid Grid.Row="1" Height="52" ColumnSpacing="10" VerticalAlignment="Center">
        <Button x:Name="AttachmentButton"
                Width="40" Height="40"
                AutomationProperties.Name="Add knowledge files">
            <Button.Flyout>
                <MenuFlyout>
                    <MenuFlyoutItem Text="Add knowledge files..."
                                    Click="AddKnowledgeFiles_Click" />
                </MenuFlyout>
            </Button.Flyout>
            <FontIcon Glyph="&#xE710;" />
        </Button>
        <TextBox x:Name="PromptTextBox" Grid.Column="1"
                 MinHeight="44" MaxHeight="160"
                 Padding="12,0"
                 VerticalAlignment="Center"
                 VerticalContentAlignment="Center"
                 AcceptsReturn="True" TextWrapping="Wrap"
                 PlaceholderText="Type a message..." />
        <!-- existing send and stop controls in column 2 -->
    </Grid>
</Grid>
```

Render each attachment as a rounded chip with filename, **Not indexed**, and an accessible remove button. Do not bind or display the full path.

- [ ] **Step 4: Add picker injection and safe state orchestration**

Keep the public XAML constructor and add an internal constructor for tests:

```csharp
private readonly IKnowledgeFilePicker knowledgeFilePicker;
private readonly ObservableCollection<KnowledgeAttachment> attachments = new();

public ChatComposer() : this(new WindowsKnowledgeFilePicker()) { }

internal ChatComposer(IKnowledgeFilePicker knowledgeFilePicker)
{
    this.knowledgeFilePicker = knowledgeFilePicker;
    InitializeComponent();
    AttachmentItems.ItemsSource = attachments;
    ApplyGeneratingState();
}
```

`AddKnowledgeFiles_Click` awaits the picker, validates against current items, adds accepted items, updates visibility, and presents only a fixed safe rejection summary. Remove uses the attachment object as `CommandParameter`. A `TextChanged` handler keeps `VerticalContentAlignment=Center` for one visual line and switches to `Top` only when the text contains a newline or wrapping makes the TextBox exceed its minimum height. `SendButton_Click` continues to raise only the trimmed prompt string; attachments remain selected and are not appended.

- [ ] **Step 5: Run focused tests and verify GREEN**

```powershell
dotnet test "tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj" -c Release -p:Platform=x64 --filter "FullyQualifiedName~ChatComposerTests"
```

Expected: composer tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml" "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs"
git commit -m "feat(chat): add honest knowledge attachments"
```

### Task 6: Verify accessibility, packaging, and the runnable preview

**Files:**
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/README.md`

- [ ] **Step 1: Add failing accessibility assertions**

Assert attachment action, attachment collection, remove buttons, Import Model, Settings, Send, and Stop expose unique names and keyboard focus. Assert the lockup is decorative or has one non-duplicated accessible brand name.

- [ ] **Step 2: Run accessibility tests and verify RED**

Expected: at least the new attachment collection/remove name assertion fails.

- [ ] **Step 3: Add minimal automation properties and documentation**

Document that `.txt` and `.md` selections are presentation-only and **Not indexed**. Add the exact preview command and state that selected files do not influence prompts yet.

- [ ] **Step 4: Run complete UI and integration verification**

Close any running Debug preview first, then run:

```powershell
dotnet test "tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj" -c Release -p:Platform=x64
dotnet test "tests\IntegrationTests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests\GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" -c Release
dotnet build "IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj" -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-ChatPreview.ps1
```

Expected: zero failed tests; model-dependent smoke may remain explicitly skipped; Release build succeeds; preview visibly matches direction B and picker cancellation works.

- [ ] **Step 5: Commit**

```powershell
git add -- "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs" "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/README.md"
git commit -m "docs(chat): verify polished attachment preview"
```
