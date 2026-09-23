#!/usr/bin/env python3
"""Build self-contained desktop archives; requires .NET 8 SDK and Python 3."""
import argparse
import hashlib
import json
import platform
from pathlib import Path
import plistlib
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
RIDS = ('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')
VERSION = ET.parse(ROOT / 'Directory.Build.props').findtext('.//Version')


def package(rid, output):
    work = ROOT / 'artifacts'
    work.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=rid + '-', dir=work) as temp:
        base = Path(temp)
        name = f'C2X86-{VERSION}-{rid}'
        folder = base / name
        publish = folder / 'C2X86.app/Contents/MacOS' if rid.startswith('osx') else folder
        subprocess.run(['dotnet', 'publish', str(ROOT / 'src/JsToCSharp.Desktop/JsToCSharp.Desktop.csproj'),
                        '-c', 'Release', '-r', rid, '--self-contained', 'true', '-m:1',
                        '-p:PublishTrimmed=false', '-p:PublishSingleFile=false',
                        '-p:DebugType=None', '-p:DebugSymbols=false', '-o', str(publish)], check=True, cwd=ROOT)
        executable = publish / ('C2X86.exe' if rid.startswith('win') else 'C2X86')
        if not executable.is_file():
            raise RuntimeError(f'Missing executable: {executable}')
        executable.chmod(0o755)
        native = 'llama.dll' if rid.startswith('win') else ('libllama.dylib' if rid.startswith('osx') else 'libllama.so')
        if not list(publish.rglob(native)):
            raise RuntimeError('Local inference backend missing')
        config = json.loads((publish / 'C2X86.runtimeconfig.json').read_text())
        if 'includedFrameworks' not in config['runtimeOptions']:
            raise RuntimeError('Runtime was not bundled')
        if rid.startswith('osx'):
            contents = publish.parent
            with (contents / 'Info.plist').open('wb') as stream:
                plistlib.dump(dict(CFBundleName='C2X86', CFBundleDisplayName='C2X86',
                    CFBundleIdentifier='app.c2x86.desktop', CFBundleExecutable='C2X86',
                    CFBundlePackageType='APPL', CFBundleShortVersionString=VERSION,
                    CFBundleVersion=VERSION, NSHighResolutionCapable=True), stream)
            if platform.system() == 'Darwin':
                # Ad-hoc signing is not Apple Developer ID signing or notarization.
                for library in sorted(publish.rglob('*.dylib')):
                    subprocess.run(['codesign', '--force', '--sign', '-', str(library)], check=True)
                entitlements = base / 'entitlements.plist'
                with entitlements.open('wb') as stream:
                    plistlib.dump({'com.apple.security.cs.allow-jit': True}, stream)
                subprocess.run(['codesign', '--force', '--entitlements', str(entitlements),
                                '--sign', '-', str(contents.parent)], check=True)
                subprocess.run(['codesign', '--verify', '--deep', '--strict', str(contents.parent)], check=True)
        shutil.copy2(ROOT / 'docs/release.md', folder / 'READ-ME.md')
        notices = folder / 'third-party-notices'
        shutil.copytree(ROOT / 'packaging/licenses', notices)
        # Include exact notices shipped by restored packages, plus a dependency inventory.
        assets = json.loads((ROOT / 'src/JsToCSharp.Desktop/obj/project.assets.json').read_text())
        inventory = []
        for identity, library in sorted(assets['libraries'].items()):
            if library['type'] != 'package':
                continue
            inventory.append(identity)
            for cache in assets['packageFolders']:
                package_dir = Path(cache) / library['path']
                if not package_dir.is_dir():
                    continue
                for item in package_dir.iterdir():
                    if item.is_file() and any(word in item.name.lower() for word in ('license', 'notice', 'copying')):
                        destination = notices / identity.replace('/', '-')
                        destination.mkdir(exist_ok=True)
                        shutil.copy2(item, destination / item.name)
                break
        (notices / 'DEPENDENCIES.txt').write_text('\n'.join(inventory) + '\n', encoding='utf-8')
        suffix = '.zip' if rid.startswith('win') else '.tar.gz'
        archive = output / (name + suffix)
        if suffix == '.zip':
            with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as target:
                for item in sorted(folder.rglob('*')):
                    if item.is_file():
                        target.write(item, item.relative_to(base))
        else:
            with tarfile.open(archive, 'w:gz') as target:
                target.add(folder, arcname=name)
        with archive.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        (output / (archive.name + '.sha256')).write_text(f'{digest}  {archive.name}\n', encoding='ascii')
        print(f'Created {archive}', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rid', choices=RIDS, action='append', help='Repeat to build several targets; defaults to all four')
    parser.add_argument('--output', type=Path, default=ROOT / 'release-assets' / VERSION)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for target in args.rid or RIDS:
        package(target, args.output.resolve())
