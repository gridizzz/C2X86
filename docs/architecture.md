# Architecture

The solution will follow a lightweight clean architecture:

- **Domain** owns data structures such as `TranslationRequest`, `TranslationResult`, supported languages, and diagnostics.
- **Application** defines user-facing use cases and interfaces such as `ICodeTranslator` and `IFileService`.
- **Infrastructure** implements the interfaces: AI-provider clients, secure local settings, file import/export, and validation.
- **Desktop** contains Avalonia views and view models only. It calls Application use cases and does not contain provider-specific code.

## AI-provider boundary

`ICodeTranslator` will receive a language pair and source text, returning the translated text and any warnings. An `OpenAiCodeTranslator` implementation can be added first, but the UI will depend only on the interface. This keeps it possible to offer other API providers or offline translation later.

`TranslationPrompt` is shared by cloud adapters and local inference. It asks for behavior-preserving translation, retains the input's program/fragment scope, guards against instructions embedded in source comments or literals, requires complete target-language code, and permits only concise code comments for unavoidable dependency or semantic gaps. A collision-checked source delimiter keeps input from forging the prompt boundary. The output contract remains source code only, with no fences or explanation. Prompt wording cannot guarantee correctness; generated output still needs review and testing.

## Security

- Never commit or log API keys.
- Store a key only in the OS credential store where supported.
- Make the outbound provider and data-sharing behavior clear before the first translation.
- Do not execute imported source code; read it as text only.

## Local inference

`LocalCodeTranslator` implements `ICodeTranslator` using LLamaSharp 0.27.0 and its CPU backend. `TranslationRequest.LocalModelPath` carries the selected weights path; `TranslationService` requires this path for `LocalModel` instead of cloud credentials. Desktop only selects the file and passes a cancellation token; provider construction remains in the existing composition code.

Local loading and generation run on a worker task, with cached weights and a fresh stateless executor for every request. The GGUF magic header is checked regardless of extension. The model's supported chat template formats the shared translation prompt; `/no_think` requests direct answers from Qwen-compatible models. Output cleanup removes leading reasoning and outer Markdown fences. The 8192-token context reserves 4096 output tokens and rejects oversized prompts. No Ollama service or network request is involved. Cancellation is cooperative during native work.

Keys are currently held only in memory; credential-store persistence remains future work.

## Editors, preferences, and model lifecycle

Desktop uses AvaloniaEdit 11.3.0 for highlighting, line numbers, font sizing, and word wrap. Clear retains one in-memory source/result snapshot for Undo Clear; code is persisted only by the separate, opt-in recovery store described below.

`IUserPreferencesStore` stores a `UserPreferences` allowlist through `JsonUserPreferencesStore`, using a temporary file and atomic replacement. Provider, language pair, local-model path, window dimensions/maximized state, and editor options are saved on close. Missing or malformed settings use defaults and numeric/enum values are normalized. API keys and editor contents have no fields in this contract.

`ILocalModelCatalog` resolves friendly names from nearby Ollama manifests without network access, with a filename fallback. `ILocalModelRuntime` exposes loaded-model state and explicit unloading. The local translator's cache serializes inference, replacement, unload, and disposal so weights cannot be released during inference. Closing cancels active work and awaits local disposal; native cancellation remains cooperative. `ICodeTranslator` and `TranslationService` accept optional `IProgress<TranslationStage>` for loading/preparing/generating status without provider logic in the UI.

## Editor tools, file drops, and opt-in draft recovery

`MainWindow.EditorTools.cs` contains the splitter-adjacent editor workflows: literal find/replace, drop routing, and draft autosave. `EditorSearch` in Application implements ordinal literal matching, optional case sensitivity, wrapping, and literal replacement. UI replacements use document edits to retain Undo; result text remains read-only.

`IInputFileService` / `InputFileService` provide shared import/drop validation. Supported source extensions are read as text with BOM detection, UTF-8 validation, a 2,097,152-character limit, and NUL rejection. Model drops use the existing GGUF-header validator, including for extensionless blobs. UI accepts one file, blocks changes while busy, and rechecks shutdown after asynchronous reads. No dropped content is executed.

`UserPreferences.DraftRecoveryEnabled` defaults to false and is persisted immediately when toggled. `IDraftStore` / `JsonDraftStore` use a separate `RecoveryDraft` allowlist containing source, result, and languages only. With opt-in, Desktop saves changed drafts every two seconds and before normal shutdown, and restores the last valid draft on startup. Drafts are plaintext in the local application-data C2X86 directory; keys are excluded. Writing uses a temporary file, flush-to-disk, and replacement so a failed write preserves the preceding snapshot. Disabling deletes the draft and temporary file. Invalid snapshots are ignored; save/deletion errors appear in a dedicated persistent UI message. Recovery keeps one snapshot and cannot guarantee edits since the last successful save. It is intended for one active app instance per local profile; concurrent instances share the same preferences/draft location.

## Language catalog and translation boundaries

Domain `Language` has explicit persisted values: JavaScript=0, CSharp=1, Python=2, TypeScript=3, Java=4, Go=5, Rust=6, C=7, Cpp=8, PHP=9, Kotlin=10, Swift=11, Ruby=12. These are the complete planned language set. Append future values without renumbering; existing numeric preferences and recovery drafts remain compatible. `LanguageCatalog` provides display names, import extensions, and export defaults. Desktop dropdowns hold catalog items and restore by language identity, without relying on dropdown indexes. Infrastructure file validation and Desktop import filters use the same extension allowlist.

Application rejects undefined or identical source/target languages. All providers continue to receive `TranslationRequest` through `TranslationService`; Infrastructure's shared prompt adds source/target semantic guidance. Desktop passes source text without trimming. Shared `TranslationOutput` removes only outer Markdown fence lines for any language, preserving indentation and embedded backticks. Local cleanup removes leading explicit reasoning wrappers and preserves reasoning tags inside code literals; unmarked reasoning prose cannot be reliably distinguished from source and is left for review.

Desktop `EditorHighlighting` maps language identity to bundled or embedded XSHD definitions. Syntax colors use the VS Code Dark+ palette across the AvaloniaEdit definitions. New Desktop tests load actual embedded definitions and exercise lexical highlighting without opening a window. Highlighting is not compiler validation. Routing tests cover all 156 distinct language pairs across the three providers using fakes; mocked HTTP responses verify cloud prompt/cleanup integration. These checks do not measure live model translation quality.

## Release packaging

Version 1.0.0 is defined in `Directory.Build.props`. Desktop publishes as `C2X86`, retaining its existing root namespace so embedded highlighting resources remain compatible. `scripts/package_release.py` publishes self-contained, untrimmed folders for Windows/Linux x64 and macOS x64/arm64, preserving LLamaSharp's RID-specific native library layout. macOS output is wrapped in an app bundle; builds made on macOS add ad-hoc signatures. Archives include dependency notices and SHA-256 sidecars. The release workflow tests/builds each target on its native operating system and creates a draft release for version tags. GUI, native inference, and signing/notarization verification remain separate from automated tests.

`scripts/stage_github.py` creates a source-only `.uploadtogithub` snapshot using an explicit extension/directory allowlist. Release downloads remain outside that source snapshot in `release-assets` for attachment to GitHub Releases. See [release instructions](release.md).
