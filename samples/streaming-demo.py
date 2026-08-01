"""Streaming demo: run this in CodeForge and watch the output panel.
Lines should appear one at a time (~0.7s apart) over the WebSocket,
not all at once at the end. Mixes stdout and stderr on purpose.
"""
import sys
import time
from datetime import datetime


def log(message, stream=sys.stdout):
    ts = datetime.now().strftime("%H:%M:%S.%f")[:-3]
    print(f"[{ts}] {message}", file=stream, flush=True)


stages = [
    ("Initializing workspace", sys.stdout, 0.7),
    ("Loading dataset: 1,024 rows", sys.stdout, 0.7),
    ("WARNING: column 'legacy_id' has 12 nulls", sys.stderr, 0.7),
    ("Normalizing values", sys.stdout, 0.7),
    ("Training pass 1/3 ... loss=0.842", sys.stdout, 0.8),
    ("Training pass 2/3 ... loss=0.511", sys.stdout, 0.8),
    ("Training pass 3/3 ... loss=0.327", sys.stdout, 0.8),
    ("WARNING: learning rate decayed early", sys.stderr, 0.7),
    ("Evaluating on holdout set", sys.stdout, 0.7),
    ("Writing report to /tmp (container-local only)", sys.stdout, 0.7),
]

log("pipeline started")
for message, stream, delay in stages:
    time.sleep(delay)
    log(message, stream)

log("pipeline finished OK — if you saw these appear gradually, streaming works!")
