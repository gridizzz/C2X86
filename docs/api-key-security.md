# API key handling

API keys belong to the user. In this first version, a key is entered into a masked field and kept only in memory for the active translation; it is never saved to disk. A key must never appear in source code, screenshots, diagnostic logs, telemetry, or Git history.

The provider integrations use the providers' official HTTPS APIs from the desktop app. Before a request, the app tells the user which provider will receive their source code. For organizations that cannot send code externally, the Local Model provider runs GGUF inference on the CPU without network requests.

The local preferences file contains only UI options, the selected local-model path, and the draft-recovery opt-in flag, never keys or source/output code. If the user enables draft recovery, a separate plaintext draft file stores source/output code and languages. API keys from the masked field are excluded. Code may itself contain secrets, so recovery is off by default and its disk storage is explicitly labeled; disabling it deletes the saved draft.

A future Settings screen can add opt-in storage through the operating system credential store. It must never fall back to a plaintext file.
