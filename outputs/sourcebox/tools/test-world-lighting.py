import importlib.util, pathlib, struct, tempfile, unittest

def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, pathlib.Path(__file__).with_name(filename))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

lighting = module('lighting', 'verify-world-lighting.py')
materials = module('materials', 'prepare-hl1-world-materials.py')

class Tests(unittest.TestCase):
    def test_ldr_only_rejected_and_dual_accepted(self):
        data = bytearray(1044)
        data[:4] = b'VBSP'
        struct.pack_into('<4i', data, 8 + 8 * 16, 1036, 4, 0, 0)
        with tempfile.TemporaryDirectory() as temp:
            file = pathlib.Path(temp) / 'fixture.bsp'
            file.write_bytes(data)
            self.assertFalse(lighting.inspect(file)['complete'])
            struct.pack_into('<4i', data, 8 + 53 * 16, 1040, 4, 0, 0)
            file.write_bytes(data)
            self.assertTrue(lighting.inspect(file)['complete'])
            struct.pack_into('<i', data, 8 + 53 * 16 + 4, 999)
            file.write_bytes(data)
            with self.assertRaises(ValueError):
                lighting.inspect(file)

    def test_geometry_change_rejected(self):
        data = bytearray(1040)
        data[:4] = b'VBSP'
        struct.pack_into('<4i', data, 8 + 3 * 16, 1036, 4, 0, 0)
        with tempfile.TemporaryDirectory() as temp:
            before, after = pathlib.Path(temp) / 'before.bsp', pathlib.Path(temp) / 'after.bsp'
            before.write_bytes(data)
            after.write_bytes(data)
            self.assertTrue(lighting.compare_geometry(before, after))
            data[1036] = 1
            after.write_bytes(data)
            with self.assertRaises(ValueError):
                lighting.compare_geometry(before, after)

    def test_legacy_preserves_diffuse_and_alpha_without_new_shader_combinations(self):
        body = [('$basetexture', 'brick/test'), ('$alphatest', '1'),
                ('$bumpmap', 'new/ssbump'), ('$ssbump', '1'),
                ('$detailblendmode', '10'), ('$envmap', 'new/hdr')]
        result = dict(materials.compatible('LightmappedGeneric', body)[0][1])
        self.assertEqual(result, {'$basetexture': 'brick/test', '$alphatest': '1'})
        self.assertIsNone(materials.compatible('Water', body))

    def test_patch_inheritance(self):
        prior = materials.SOURCE
        try:
            with tempfile.TemporaryDirectory() as temp:
                materials.SOURCE = pathlib.Path(temp)
                (materials.SOURCE / 'base.vmt').write_text('LightmappedGeneric { "$basetexture" "brick/base" "$ssbump" "1" }')
                (materials.SOURCE / 'patch.vmt').write_text('patch { include "base.vmt" replace { "$basetexture" "brick/variant" } }')
                shader, body = materials.resolve('patch')
                self.assertEqual(dict(materials.compatible(shader, body)[0][1])['$basetexture'], 'brick/variant')
                with self.assertRaises(ValueError):
                    materials.resolve('../escape')
        finally:
            materials.SOURCE = prior

if __name__ == '__main__':
    unittest.main()
