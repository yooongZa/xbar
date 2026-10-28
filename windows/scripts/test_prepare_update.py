import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('prepare_update', Path(__file__).with_name('prepare-update.py'))
prepare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prepare)

class PublicReleaseReportTests(unittest.TestCase):
    def test_report_binds_every_release_property(self):
        identity = {'Name':'LIMOD.XBar', 'Publisher':'CN=Example Publisher', 'Version':'0.3.0.0', 'ProcessorArchitecture':'x64'}
        digest = 'a'*64
        good = {'schema':1, 'status':'passed', 'sha256':digest, 'length':123,
                'identity':identity['Name'], 'publisher':identity['Publisher'], 'version':identity['Version'],
                'architecture':'x64', 'timestamped':True}
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'verification.json'
            path.write_text(json.dumps(good),encoding='utf-8-sig')
            prepare.verify_report(path,identity,digest,123)
            for name in good:
                with self.subTest(changed=name):
                    path.write_text(json.dumps(good|{name:'wrong'}))
                    with self.assertRaises(SystemExit): prepare.verify_report(path,identity,digest,123)
                with self.subTest(missing=name):
                    path.write_text(json.dumps({k:v for k,v in good.items() if k!=name}))
                    with self.assertRaises(SystemExit): prepare.verify_report(path,identity,digest,123)
            path.write_text('[]')
            with self.assertRaises(SystemExit): prepare.verify_report(path,identity,digest,123)

class DesktopReleaseReportTests(unittest.TestCase):
    def test_unsigned_report_binds_product_and_file(self):
        good={'schema':1,'status':'passed','format':'exe','identity':'LIMOD.XBar.Desktop','publisher':'LIMOD','version':'0.4.1.0','architecture':'x64','sha256':'b'*64,'length':234,'codeSigned':False}
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'verification.json'
            path.write_text(json.dumps(good))
            self.assertEqual(prepare.verify_desktop_report(path,'0.4.1.0','b'*64,234)['Name'],'LIMOD.XBar.Desktop')
            for k in good:
                with self.subTest(field=k):
                    path.write_text(json.dumps(good|{k:'wrong'}))
                    with self.assertRaises(SystemExit): prepare.verify_desktop_report(path,'0.4.1.0','b'*64,234)

if __name__=='__main__': unittest.main()
