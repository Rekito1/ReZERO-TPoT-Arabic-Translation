"""Compare ZIP member order, uncompressed bytes and metadata without extracting files."""
import argparse
import hashlib
import zipfile


def member_hash(package, member):
    with package.open(member) as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference')
    parser.add_argument('candidate')
    args = parser.parse_args()
    with zipfile.ZipFile(args.reference) as reference, zipfile.ZipFile(args.candidate) as candidate:
        original = reference.infolist()
        regenerated = candidate.infolist()
        if [entry.filename for entry in original] != [entry.filename for entry in regenerated]:
            raise SystemExit('ZIP member names or ordering differ.')
        for left, right in zip(original, regenerated):
            if member_hash(reference, left) != member_hash(candidate, right):
                raise SystemExit(f'ZIP member contents differ: {left.filename}')
        timestamp_changes = sum(a.date_time != b.date_time for a, b in zip(original, regenerated))
        print(f'{len(original)} ordered ZIP members have identical uncompressed bytes.')
        print(f'ZIP timestamps differ for {timestamp_changes} members.')


if __name__ == '__main__':
    main()
