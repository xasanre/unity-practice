"""Validate the six locally obtained course clips and restore their Unity references."""
import argparse
import hashlib
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
from datetime import datetime

PROJECT = Path(__file__).resolve().parent
AUDIO = Path('Assets/FPSGamePack/Additional_Sound(2)/Audio')
# Relative clip path: (SHA-256 of the course original, Unity asset GUID used by the prefabs).
CLIPS = {
    'FootStep/Footstep.wav': ('42c22934ad270253759c478b86889b153d97553ffe33d1a5678b23a3ffd0ff4c', 'e2a1fceec28a1c34a8dc25d61d50fa91'),
    'GunShots/ak.mp3': ('8455afc42a0b3b973128f613dddf8d48d348b7a49dd6efee1c49282a847e57e5', 'a97c9053408bbcb49ac3640026a85085'),
    'GunShots/gunshot.ogg': ('cf3576888466c75febbb120ccf02116d6fad24a30521b13872c35002c767d546', '7039243848053c34a89248a7542dc592'),
    'GunShots/long_gun_shoot.wav': ('e142340284f6d00eec04fe923f773a93793f486af099c4951bf3a671aa7b8b1b', '8ca2fc5a43d77a145b14e59fcef5eb79'),
    'GunShots/machine-gun.ogg': ('554a9db3bb4363d250dd4f7a805a3eba0f68860af16a5824708161724e6fb25f', '86abddb24d50daf40a01608e1c130b7e'),
    'Reload&Handing/ak47Reload.wav': ('d9263ee3c0894bc2c562edbc408abd3d9ecdda6a95a1b3d468aed662aa95fcac', '6abb3182bc7434849b63d6cb5a71ee24'),
}
GUID = re.compile(rb'(?m)^guid:[ \t]*([a-fA-F0-9]{32})(?=[ \t]*\r?$)')
NEW_META = """fileFormatVersion: 2
guid: {}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def restore(apply):
    assets = PROJECT / 'Assets'
    if not (PROJECT / 'ProjectSettings/ProjectVersion.txt').is_file():
        raise ValueError('Keep this script in the MultiplayerFPS project root.')
    if apply and (PROJECT / 'Temp/UnityLockfile').exists():
        raise ValueError('Close Unity before restoring clip metadata.')

    wanted, changes = {}, []
    for name, (checksum, guid) in CLIPS.items():
        clip = PROJECT / AUDIO / name
        meta = clip.with_name(clip.name + '.meta')
        for path in (clip, meta):
            if path.resolve() != path:
                raise ValueError('Refusing to follow a redirected asset path: ' + str(path))
        if not clip.is_file() or hashlib.sha256(clip.read_bytes()).hexdigest() != checksum:
            raise ValueError(f'Missing or different course clip: {clip}')
        wanted[guid.encode()] = meta
        if not meta.exists():
            changes.append((meta, NEW_META.format(guid).encode(), False))
            continue
        data = meta.read_bytes()
        matches = list(GUID.finditer(data))
        if len(matches) != 1 or not re.search(rb'(?m)^AudioImporter:\s*$', data):
            raise ValueError('Unexpected AudioImporter metadata: ' + str(meta))
        m = matches[0]
        if m[1].lower() != guid.encode():
            changes.append((meta, data[:m.start(1)] + guid.encode() + data[m.end(1):], True))

    for path in assets.rglob('*.meta'):
        m = GUID.search(path.read_bytes())
        if m and m[1].lower() in wanted and wanted[m[1].lower()] != path:
            raise ValueError(f'Target GUID already belongs to another asset: {path}')
    if not changes:
        print('All six clips and Unity GUIDs match. No changes needed.')
        return 0
    for path, _, _ in changes:
        print('Needs metadata:', path.relative_to(PROJECT))
    if not apply:
        print('Check only; nothing written. Close Unity, then run with --apply.')
        return 1

    # Changed GUIDs can invalidate other user-made references. Keep preexisting metadata.
    backup = PROJECT / 'Library' / 'CourseAudioMetaBackups' / datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    for path, _, exists in changes:
        if exists:
            saved = backup / path.relative_to(PROJECT)
            saved.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, saved)
    for path, data, _ in changes:
        temp = None
        try:
            with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as f:
                temp = Path(f.name)
                f.write(data)
            os.replace(temp, path)
        finally:
            if temp and temp.exists():
                temp.unlink()
    print(f'Restored {len(changes)} metadata file(s); no audio content changed.')
    if backup.exists():
        print('Previous metadata:', backup)
    return 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='write missing or corrected .meta files')
    try:
        sys.exit(restore(parser.parse_args().apply))
    except (OSError, ValueError) as exc:
        print(exc, file=sys.stderr)
        sys.exit(2)
