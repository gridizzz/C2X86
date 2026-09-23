# Product notes

## Current workflow

1. Choose any two distinct languages from JavaScript, C#, Python, TypeScript, Java, Go, Rust, C, C++, PHP, Kotlin, Swift, and Ruby. This is the final planned language set.
2. Paste, import, or drop one source file, selecting its language manually.
3. Translate using OpenAI, Gemini, or a local GGUF model.
4. Review the result with lexical highlighting, then copy or export using the target extension.
5. Surface provider errors and translation warnings clearly.

Preferences remember languages and editor options. Optional plaintext draft recovery is off by default. API keys are never persisted. Source and generated code are never executed.

## Later improvements

- Compiler/parser-backed syntax validation and formatting
- Incomplete-output detection across providers
- Side-by-side diff view
- Translation quality benchmarks and conversion hints
- Multi-file/project translation with dependency mapping
