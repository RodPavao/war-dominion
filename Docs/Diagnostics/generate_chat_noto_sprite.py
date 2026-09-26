"""Deterministic Unity text serialization for the installed TMP 1.1.0 schema.

Run from any directory; --check verifies without writing. Atlas/material untouched.
Field names follow TMP_Asset, TMP_SpriteAsset, TMP_TextElement and Glyph.
"""
import json
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
folder = root / 'Assets/Resources/UI/Chat/Emoji/NotoColor'
entries = json.loads((folder / 'manifest.json').read_text(encoding='utf-8'))['entries']
assert len(entries) == 34
assert [e['index'] for e in entries] == list(range(34))

def guid(path):
    return re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)[1]

script = next((root / 'Library/PackageCache').glob('com.unity.ugui@*/Runtime/TMP/TMP_SpriteAsset.cs.meta'))
lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000',
         'MonoBehaviour:', '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}',
         '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}',
         '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
         f'  m_Script: {{fileID: 11500000, guid: {guid(script)}, type: 3}}',
         '  m_Name: Chat Noto Color', '  m_EditorClassIdentifier:', '  m_Version: 1.1.0',
         # Native sprite face: 120-unit glyph / 100-point em = 1.2 * fontSize.
         # Bearing 108 places the quad at +1.08 em / -0.12 em from baseline.
         # Transparent ink margins stay in the atlas; no per-emoji correction.
         '  m_FaceInfo:', '    m_PointSize: 100', '    m_Scale: 1',
         '    m_LineHeight: 120', '    m_AscentLine: 108', '    m_CapLine: 108',
         '    m_MeanLine: 60', '    m_Baseline: 0', '    m_DescentLine: -12',
         f'  m_Material: {{fileID: 2100000, guid: {guid(folder / "ColorSprite.mat.meta")}, type: 2}}',
         f'  spriteSheet: {{fileID: 2800000, guid: {guid(folder / "Atlas.png.meta")}, type: 3}}',
         '  m_SpriteCharacterTable:']
for e in entries:
    length = len(e['unicode'].encode('utf-16-le')) // 2
    assert length in (1, 2)
    alias = (0xF000 if length == 1 else 0xF0000) + e['index']
    lines += ['  - m_ElementType: 2', f'    m_Unicode: {alias}',
              f'    m_GlyphIndex: {e["index"]}', '    m_Scale: 1', f'    m_Name: noto_{e["index"]}']
lines += ['  m_GlyphTable:']
for e in entries:
    assert 0 <= e['x'] <= 1024-e['width'] and 0 <= e['y'] <= 640-e['height']
    lines += [f'  - m_Index: {e["index"]}', '    m_Metrics:',
              f'      m_Width: {e["width"]}', f'      m_Height: {e["height"]}',
              '      m_HorizontalBearingX: 0', f'      m_HorizontalBearingY: {e["height"] * .9:g}',
              f'      m_HorizontalAdvance: {e["width"]}', '    m_GlyphRect:',
              f'      m_X: {e["x"]}', f'      m_Y: {e["y"]}',
              f'      m_Width: {e["width"]}', f'      m_Height: {e["height"]}',
              '    m_Scale: 1', '    m_AtlasIndex: 0', '    m_ClassDefinitionType: 0',
              '    sprite: {fileID: 0}']
lines += ['  spriteInfoList: []', '  fallbackSpriteAssets: []']
content = '\n'.join(lines) + '\n'
target = folder / 'ChatNotoColor.asset'
if '--check' in sys.argv:
    assert target.read_text(encoding='utf-8') == content, 'Serialized asset differs from manifest/schema'
    print('TMP 1.1.0 serialization: 34 characters / 34 glyphs, deterministic, bounds valid.')
else:
    target.write_text(content, encoding='utf-8', newline='\n')
