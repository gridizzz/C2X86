# C2X86 1.0 — downloads and publishing

## Install and launch

Download the archive for your computer and extract the **entire** folder. .NET is included; no SDK or separate runtime installation is required. Model weights and API keys are not included.

| Computer | Download | Launch |
| --- | --- | --- |
| Windows x64 | `C2X86-1.0.0-win-x64.zip` | Open `C2X86.exe` inside the extracted folder |
| Linux x64 | `C2X86-1.0.0-linux-x64.tar.gz` | Run `./C2X86` from the extracted folder in a graphical desktop session |
| Intel Mac | `C2X86-1.0.0-osx-x64.tar.gz` | Open `C2X86.app` (optionally move it to Applications) |
| Apple Silicon Mac | `C2X86-1.0.0-osx-arm64.tar.gz` | Open `C2X86.app` (optionally move it to Applications) |

These are portable archives, not installers. Keep all adjacent libraries and the `runtimes` folder. Linux needs a glibc-based distribution with a graphical desktop/X11 or XWayland and native desktop libraries. On Ubuntu 22.04/24.04, install missing libraries with:

```sh
sudo apt install libfontconfig1 libfreetype6 libx11-6 libxext6 libxrender1 libxi6 libxrandr2 libxcursor1 libxinerama1 libice6 libsm6 libglib2.0-0 libicu-dev libssl-dev libgomp1
```

Windows may also need Microsoft's Visual C++ 2015–2022 x64 Redistributable for the local inference backend. Local models depend on CPU capabilities, memory, and GGUF/backend compatibility. Cloud translation does not require model weights.

The downloads are unsigned. macOS packages are experimental until tested on real Macs; builds produced on macOS are ad-hoc signed, not Developer ID signed or notarized. Gatekeeper/SmartScreen may block an unrecognized download. Review its origin before using your operating system's explicit approval mechanism. No signing credentials are stored in this repository.

A matching `.sha256` file accompanies each archive. Linux: `sha256sum -c <archive>.sha256`; macOS: `shasum -a 256 <archive>`; Windows PowerShell: `Get-FileHash <archive> -Algorithm SHA256`. Compare with the text in the checksum file.

## Build release downloads

Install .NET 8 SDK and Python 3.11 or newer. From the repository root:

```sh
python3 scripts/package_release.py
# Or build one target:
python3 scripts/package_release.py --rid linux-x64
```

On Windows use `python` in place of `python3`. Outputs go to `release-assets/1.0.0`. Packaging preserves native dependencies, includes third-party notices, and uses self-contained folder publishing without trimming or single-file bundling. Building does not test GUI launch or perform a live translation. Test the appropriate archive on each target OS before promoting it as verified.

## Upload to GitHub

Run `python3 scripts/stage_github.py` once to create `.uploadtogithub`. Upload **the contents** of that folder as the repository root, including hidden `.github` and `.gitignore`. Do not upload the enclosing `.uploadtogithub` folder. Staging includes source, tests, docs, notices, and build/release automation; it excludes developer handoffs, temporary files, credentials, build caches, and app archives. It refuses to overwrite an existing staging folder.

Attach the files from `release-assets/1.0.0` to a GitHub **Release**, not to the source repository. Alternatively, push a `v1.0.0` tag after uploading the source. The Release workflow tests and packages on Windows, Linux, Intel macOS, and Apple Silicon macOS, then creates a **draft** GitHub Release with the downloads. Review the draft and publish it. Running the workflow manually creates downloadable Actions artifacts without creating a release. Workflow runs do not include interactive GUI/live-provider testing.

Version tags must match `Directory.Build.props`. Signing/notarization and installers are not configured. The repository does not currently grant an open-source license for C2X86's own code; dependency licenses apply to the respective dependencies only.

Packaging references: [.NET deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [Avalonia macOS deployment](https://docs.avaloniaui.net/docs/deployment/macos), and [GitHub runner platforms](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).
