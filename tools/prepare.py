"""v2.2 payload builder with explicit input paths in place of local workspace paths."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import struct
import zipfile


# Original CFSI index reader from install-menu-patch.py.
def index(stream):
    def vint():
        b=stream.read(1)[0]
        return struct.unpack('<H',stream.read(2))[0] if b==252 else b
    def string():return stream.read(stream.read(1)[0]).decode('utf-8')
    entries={}
    stream.seek(0)
    for _ in range(vint()):
        folder=string()
        for _ in range(vint()):
            name=folder+string();pos=stream.tell();off,size=struct.unpack('<II',stream.read(8))
            entries[name]={'index_position':pos,'offset':off*16,'size':size}
    start=(stream.tell()+15)//16*16
    return entries,start


# Original v2.2 RZP1 encoder from prepare.py.
def delta(a,b):
    lookup={a[i:i+32]:i for i in range(0,len(a)-31,32)}
    out=bytearray(b'RZP1'+struct.pack('<i',len(b)));literal=bytearray();i=0
    def flush():
        if literal:out.extend(b'\1'+struct.pack('<i',len(literal))+literal);literal.clear()
    while i<len(b):
        p=lookup.get(b[i:i+32])
        if p is None:literal.append(b[i]);i+=1;continue
        flush();n=32
        while i+n<len(b) and p+n<len(a) and a[p+n]==b[i+n]:n+=1
        out.extend(b'\0'+struct.pack('<ii',p,n));i+=n
    flush();return gzip.compress(out,mtime=0)


def hash_file(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read_asset(stream, base, entry):
    stream.seek(base + entry['offset'])
    data = stream.read(entry['size'])
    if len(data) != entry['size']:
        raise ValueError('Truncated CFSI entry')
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('original-archive', 'localized-archive', 'original-exe',
                 'localized-exe', 'original-movie', 'localized-movie', 'artwork', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    release = json.loads((Path(__file__).resolve().parent.parent / 'release.json').read_text())
    sha = lambda data: hashlib.sha256(data).hexdigest()
    for name, expected in release['payload_builder_inputs'].items():
        if hash_file(getattr(args, name)) != expected:
            parser.error(f'{name} does not match the recorded v2.2 input')

    if args.output.exists():
        parser.error('The output already exists. Choose a fresh output path.')
    plan = {'archiveHash': release['payload_builder_inputs']['original_archive'],
            'assets': [], 'external': []}
    with args.original_archive.open('rb') as original, args.localized_archive.open('rb') as localized:
        entries, base = index(original)
        updated_entries, updated_base = index(localized)
        if base != updated_base or entries.keys() != updated_entries.keys():
            raise ValueError('CFSI index layouts differ')
        plan['base'] = base
        with zipfile.ZipFile(args.output, 'x', zipfile.ZIP_DEFLATED) as package:
            for name, entry in entries.items():
                updated = updated_entries[name]
                if entry == updated:
                    continue
                a = read_asset(original, base, entry)
                b = read_asset(localized, base, updated)
                if a == b:
                    continue
                member = f"archive/{len(plan['assets'])}.delta"
                package.writestr(member, delta(a, b))
                plan['assets'].append({'name': name, 'entry': member,
                                       'offset': base + entry['offset'], 'size': len(a),
                                       'index': entry['index_position'], 'before': sha(a), 'after': sha(b)})
            if len(plan['assets']) != 365:
                raise ValueError('Expected 365 changed archive assets for v2.2')
            a, b = args.original_exe.read_bytes(), args.localized_exe.read_bytes()
            if a != b:
                raise ValueError('v2.2 requires an unchanged game executable')
            package.writestr('exe.delta', delta(a, b))
            plan.update(exeBefore=sha(a), exeAfter=sha(b))
            a, b = args.original_movie.read_bytes(), args.localized_movie.read_bytes()
            package.writestr('movie.delta', delta(a, b))
            plan['external'].append({'name': 'data/root/movie/op_STEAM_en.webm',
                                     'entry': 'movie.delta', 'before': sha(a), 'after': sha(b)})
            package.writestr('plan.json', json.dumps(plan))
            package.write(args.artwork, 'trio-nintendo.jpg')
    print(f'Payload SHA-256: {hash_file(args.output)}')


if __name__ == '__main__':
    main()
