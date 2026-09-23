#!/usr/bin/env python3
"""Create a clean source upload; refuses to overwrite an existing staging folder."""
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[1]
target = root / '.uploadtogithub'
if target.exists():
    raise SystemExit('Staging already exists; move it aside before creating a fresh copy.')
files = ['.gitignore', 'Directory.Build.props', 'JsToCSharp.sln', 'README.md', 'commands.txt']
allowed = {'src': {'.cs', '.csproj', '.axaml', '.xshd'},
           'tests': {'.cs', '.csproj'}, 'docs': {'.md'},
           'scripts': {'.py'}, '.github': {'.yml'}, 'packaging': {'.txt', '.md'}}
for directory, extensions in allowed.items():
    for path in sorted((root / directory).rglob('*')):
        if path.is_file() and path.suffix in extensions and not {'bin', 'obj', '__pycache__'}.intersection(path.relative_to(root).parts):
            files.append(str(path.relative_to(root)))
for relative in files:
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / relative, destination)
print(f'Staged {len(files)} files in {target}')
