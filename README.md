# C2X86

C2X86 (Code-to-code x86) is a desktop GUI application for translating JavaScript, C#, Python, TypeScript, Java, Go, Rust, C, C++, PHP, Kotlin, Swift, and Ruby source code. Users can paste or import code, select a provider and model, review the generated result, then export it.

## Stack

- **Desktop UI:** .NET 8 + Avalonia UI (Windows, macOS, and Linux)
- **Translation:** OpenAI, Gemini, and local GGUF models through LLamaSharp
- **Architecture:** UI, application logic, translation providers, and file services kept separate

## Repository layout

```text
src/
  JsToCSharp.Desktop/       Avalonia user interface
  JsToCSharp.Application/   Use cases and interfaces
  JsToCSharp.Domain/        Translation models and rules
  JsToCSharp.Infrastructure/AI providers, file I/O, secure settings
tests/
  JsToCSharp.Application.Tests/
  JsToCSharp.Infrastructure.Tests/
docs/                       Product, architecture, and API notes
scripts/                    Release packaging and clean GitHub staging
.github/workflows/          Continuous integration
```

## Translation approach

Choose OpenAI or Gemini to translate through their HTTPS APIs, or **Local Model** to load GGUF weights directly with LLamaSharp's CPU backend. Local translation stays on the computer and does not require Ollama to be running. API keys remain in memory for the active run and are never saved.

The shared translation prompt asks providers to preserve behavior and input scope, include required target-language code, and return code without explanation or Markdown fences. Text inside the source is treated as code data, not as instructions to the model. Generated code is still a draft: review and test it before use. The app never executes source or generated code.

## Run it

For ready-to-run Windows, Linux, and experimental macOS downloads, see [release and launch instructions](docs/release.md). Release archives include .NET. To build from source:

1. Install .NET 8 using [`commands.txt`](commands.txt).
2. From the repository root, run `dotnet restore JsToCSharp.sln`.
3. Run `dotnet run --project src/JsToCSharp.Desktop/JsToCSharp.Desktop.csproj`.
4. Choose OpenAI or Google Gemini and enter an API key, or choose **Local Model** and browse for GGUF weights. Choose the direction and translate.

Keys are held only in the running app; this first version does not save them to disk.

## Current features

- Translation between any two distinct supported languages: JavaScript, C#, Python, TypeScript, Java, Go, Rust, C, C++, PHP, Kotlin, Swift, and Ruby. This is the project's complete planned language set.
- Code editors with syntax highlighting, line numbers, adjustable font size (10–32), and word wrap
- Undo Clear restores the most recently cleared source and result for the current session
- Provider, languages, local-model path, window size/maximized state, font size, and wrap preference are saved locally; API keys are never saved; code is saved only if you enable draft recovery
- OpenAI Responses API and Google Gemini `generateContent` API
- Paste source using the Paste button next to Clear (inserts at the cursor or replaces the selection), import a source file, copy output, and export translated code
- Editor headers separate titles from action buttons with action buttons below the titles so narrowed panes remain usable
- Provider-specific model dropdowns, including current OpenAI GPT-6 options and Gemini 3.8/3.x Flash and Pro options
- Select **Other…** inside the model dropdown to enter an exact API model ID for either provider. Selecting a preset hides the custom field; changing providers clears it. Custom IDs must support the selected provider's text-generation API and be available to your account. No fallback model is substituted.
- An accuracy disclaimer beneath model selection reminds users to review and test AI output; a more capable coding model may reduce errors.
- Status and error messages wrap beside the Translate button, with vertical scrolling for long messages and selectable text for copying details.

OpenAI presets are based on the [official model catalog](https://developers.openai.com/api/docs/models), and Gemini presets on Google's [official model catalog](https://ai.google.dev/gemini-api/docs/models), checked on 2026-09-23. GPT-6 Astra, Sol, and Luna are included alongside retained GPT-5-mini and GPT-4.1 choices. Gemini defaults to `gemini-3.8-flash`; older 2.5 choices remain for existing users. Preview IDs include `-preview`; audio/image-only models are omitted. Provider account access varies, and the **Other…** option accepts an exact model ID.

## Local models

Select **Local Model** in the provider dropdown. The API-key and cloud-model controls are replaced by a path field and **Browse…**. Choose a `.gguf` text-generation model supported by the bundled llama.cpp backend, or paste its full path. The picker shows all files so extensionless Ollama weights can be selected. For Ollama, browse to its `models/blobs` folder and select the `sha256-<digest>` file that contains the model weights. Do not select files under `models/manifests` or a `Modelfile`. The selected file's GGUF header is checked, which rejects manifests/configuration files.

Ollama's `models/manifests/registry.ollama.ai/library/<model>/<tag>` JSON identifies the weights layer with media type `application/vnd.ollama.image.model`. Its `sha256:<digest>` maps to `models/blobs/sha256-<digest>`. You can choose this blob directly; no renaming or copying is required.

Inference runs on the CPU in the background. **Cancel** stops cloud or local translation; native operations can take a moment to stop. Weights stay in memory between translations of the same model; each translation uses a fresh context. Selecting a different model replaces the cached weights on the next translation. **Unload model** releases them explicitly, and closing the window cancels work and releases them. Status distinguishes loading, preparing input, and generating output. Ollama manifests are used to display a friendly model name when available; Use the single **Browse…** button beside **Unload model** to choose weights; expand **Model file path** to see or edit the full path. The initial context is 8192 tokens, reserving 4096 for output; oversized inputs are rejected. Long output may be incomplete. Small models can produce poor translations; always review results. Model architecture, chat template, RAM, and CPU/backend compatibility affect which GGUF files can run. GPU acceleration is not configured in this version.

Preferences are stored in `C2X86/preferences.json` under the operating system’s local application-data directory. The local-model path is remembered, but the model is only loaded when you translate. Invalid or missing settings use defaults.

## Editor tools and recovery

- Drag the divider between Source and Translated code to resize the panes. Minimum widths keep both usable. The split resets to equal widths on launch.
- Select **Find / Replace** or press **Ctrl+F** (**Cmd+F** on macOS). Choose Source or Result, enter literal text, optionally enable Match case, then select Find next. Searches wrap. Replace and Replace all modify only Source and support editor Undo; Result stays read-only. Escape closes search.
- Drop one supported source file (extensions listed below) onto the source pane to import it, with Undo available to restore the previous source. Import and drop use the same text validation, reject binary data, and limit source files to 2,097,152 characters.
- Drop one GGUF file (including an extensionless Ollama weights blob) onto the local-model section or provider dropdown. Its GGUF header is validated before selection; dropping on the provider dropdown switches to Local Model. Dropping never starts inference. Folders/multiple files are rejected, and file drops are disabled during translation.
- **Save drafts for recovery (local disk)** is off by default. Enabling it saves source, result, and their languages as **plaintext** in `draft.json` beside `preferences.json`. API keys are never included. Changed drafts are saved every two seconds and on normal close; the latest valid draft restores automatically at launch, including after a normal exit. Only the latest snapshot is kept, not a history. This setting is saved immediately, so an unexpected exit does not lose the opt-in.
- Disabling recovery deletes the saved draft and stops further saves. Persistent messages report save/deletion failures. Draft writes flush to disk before replacing the prior snapshot; a sudden outage can still lose edits since the last successful save. Source code containing embedded secrets would also be saved when recovery is enabled.

## Supported languages

Both language selectors offer all thirteen languages with OpenAI, Gemini, and Local Model. Select the source language yourself when pasting, importing, or dropping a file; filenames do not override your selection (notably `.h` can be C or C++). Same-language requests are rejected.

| Language | Import extensions | Default export |
|---|---|---|
| JavaScript | `.js`, `.mjs`, `.cjs`, `.jsx` | `translated-code.js` |
| C# | `.cs` | `TranslatedCode.cs` |
| Python | `.py`, `.pyw` | `translated_code.py` |
| TypeScript | `.ts`, `.tsx`, `.mts`, `.cts` | `translated-code.ts` |
| Java | `.java` | `Main.java` |
| Go | `.go` | `main.go` |
| Rust | `.rs` | `main.rs` |
| C | `.c`, `.h` | `translated_code.c` |
| C++ | `.cpp`, `.cc`, `.cxx`, `.hpp`, `.hh`, `.hxx`, `.h` | `translated_code.cpp` |
| PHP | `.php`, `.phtml`, `.php3`, `.php4`, `.php5`, `.phps` | `translated_code.php` |
| Kotlin | `.kt`, `.kts` | `Main.kt` |
| Swift | `.swift` | `main.swift` |
| Ruby | `.rb`, `.rake`, `.gemspec` | `translated_code.rb` |

Plain `.txt` source files are also accepted; extension validation is case-insensitive. Export names can be changed in the save dialog. Rename Java library files to match their public class, and use `.tsx`/`.jsx` when exporting JSX content. Source indentation is passed through unchanged, and output cleanup removes outer Markdown fences without deleting backticks inside code.

Highlighting is lexical, not syntax validation. PHP, Kotlin, Swift, Ruby, TypeScript, Go, and Rust use bundled project definitions; Python/Java use bundled AvaloniaEdit definitions; C/C++ share its C-family definition. Syntax colors follow the VS Code Dark+ palette. Advanced constructs such as JSX, template interpolation, Swift raw strings, Ruby heredocs, Rust hash-delimited raw strings, and nested comments may not be fully colored.

Language-specific prompts address types, runtime differences, ownership, resource handling, imports, and entry points. Support means the app can request these translations; it does not guarantee that the chosen model knows every language or produces compiling, equivalent code. No compilers or language runtimes are installed or invoked. Whole-project/framework migration and automatic dependency conversion remain outside the current single-input workflow. Review and test results, especially with small local models.
