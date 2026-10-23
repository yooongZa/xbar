#!/usr/bin/env python3
"""Loopback-only fixture server; create .slow-download in the served folder to test cancellation."""
import argparse, http.server, json, os, time
from pathlib import Path
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('directory',type=Path)
parser.add_argument('--port',type=int,default=0)
args=parser.parse_args()
root=args.directory.resolve()
(root/'served').mkdir(parents=True,exist_ok=True)
class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self,*a,**kw):super().__init__(*a,directory=str(root/'served'),**kw)
    def end_headers(self):
        self.send_header('Cache-Control','no-store')
        super().end_headers()
    def copyfile(self,source,outputfile):
        if self.path.endswith('.zip') and (root/'served/.slow-download').exists():
            try:
                while data := source.read(16384):
                    outputfile.write(data);outputfile.flush();time.sleep(0.1)
            except (BrokenPipeError,ConnectionResetError): pass
        else:super().copyfile(source,outputfile)
server=http.server.ThreadingHTTPServer(('127.0.0.1',args.port),Handler)
(root/'server.json').write_text(json.dumps({'port':server.server_port,'pid':os.getpid()}))
print('Loopback server port:',server.server_port,flush=True)
server.serve_forever()
