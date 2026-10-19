"""Reject incomplete native lighting without changing a BSP or game settings."""
import argparse, json, pathlib, struct

def inspect(file):
    data = pathlib.Path(file).read_bytes()
    if len(data) < 1036 or data[:4] != b'VBSP':
        raise ValueError('Not a Source BSP')
    result = {}
    for name, index in [('ldr', 8), ('hdr', 53)]:
        offset, size, version, compressed = struct.unpack_from('<4i', data, 8 + index * 16)
        if offset < 0 or size < 0 or offset + size > len(data):
            raise ValueError('Invalid ' + name + ' lighting bounds')
        result[name] = {'bytes': size, 'present': size > 0}
    result['complete'] = all(v['present'] for v in result.values())
    return result

def compare_geometry(original, rebuilt):
    before, after = pathlib.Path(original).read_bytes(), pathlib.Path(rebuilt).read_bytes()
    if before[:8] != after[:8]:
        raise ValueError('BSP engine/version changed during lighting repair')
    # Lighting may rewrite face-light offsets and ambient samples. Structural
    # planes, vertices, BSP nodes, edges, brushes and displacements must remain.
    for index in (1, 3, 4, 5, 12, 13, 18, 19, 26):
        start, size = struct.unpack_from('<2i', before, 8 + index * 16)
        new_start, new_size = struct.unpack_from('<2i', after, 8 + index * 16)
        if before[start:start + size] != after[new_start:new_start + new_size]:
            raise ValueError('Structural lump changed: ' + str(index))
    return True

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('bsp')
    parser.add_argument('--original')
    args = parser.parse_args()
    result = inspect(args.bsp)
    if args.original:
        result['geometryUnchanged'] = compare_geometry(args.original, args.bsp)
    print(json.dumps(result))
    raise SystemExit(0 if result['complete'] else 1)
