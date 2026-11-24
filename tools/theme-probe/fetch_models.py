"""Fetch the two theme-model candidates into models/ and check every file's SHA-256.

INPC G0.5 Task 1 (design 6-3-1). Run from anywhere:
    python tools/theme-probe/fetch_models.py               # the repo's models/ folder
    python tools/theme-probe/fetch_models.py --models DIR

A file that already exists with the expected hash is not downloaded again. A download whose hash does not
match is deleted and the script exits with status 1, so a changed or tampered file never reaches the game.
No photo, folder name or user data is involved: these are public model files.
"""
import argparse
import hashlib
import os
import sys
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "models"))

# Pinned sources. MIT CSAIL serves the Places365 weights over plain http only, so that hash was recorded
# on the first download (2026-11-24) and guards every later one.
PLACES_REPO = "https://raw.githubusercontent.com/CSAILVision/places365/8a953ed56438726dc98bdef3796d042e7f1f171e/"
XENOVA = "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/6b7c523703ae26e39be4b03774966f8571c7f523/"

# (path under models/, url, sha256)
FILES = [
    ("download/resnet18_places365.pth.tar",
     "http://places2.csail.mit.edu/models_places365/resnet18_places365.pth.tar",
     "2f4759217d470da2b803f8f66cd4488a066406b555a5fb95ee9a4663f9f05588"),
    ("download/categories_places365.txt",
     PLACES_REPO + "categories_places365.txt",
     "2affba635eb657e7ca95f4e6cc69bd9fac29ef4c32aeb83cafdfcd06ec6a1ea6"),
    ("download/clip-text-model.onnx",
     XENOVA + "onnx/text_model.onnx",
     "3f6571f5bad13a97c469c1622e1cfc4d9aef78b79fdbfcff804ca357bfada8cc"),
    ("download/clip-tokenizer.json",
     XENOVA + "tokenizer.json",
     "f7f3b7af117d467b58374797691a6438d3e6b9e9cef800dfd5dced7f697a90cd"),
    # The 8-bit build that ONNX Runtime's CPU provider can run: the int8 build stops at load time
    # (ConvInteger is implemented for uint8 only), so this is the uint8 one.
    ("clip-vision.onnx",
     XENOVA + "onnx/vision_model_uint8.onnx",
     "b95448754a6ae56964ec80c570c80bb9863787ee85f51b664abed8c0e5f22a7a"),
]


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def download(url, dest):
    part = dest + ".part"
    request = urllib.request.Request(url, headers={"User-Agent": "inpc-theme-probe"})
    try:
        with urllib.request.urlopen(request, timeout=60) as response, open(part, "wb") as out:
            for block in iter(lambda: response.read(1 << 20), b""):
                out.write(block)
    except (OSError, urllib.error.URLError):
        if os.path.exists(part):
            os.remove(part)
        raise
    os.replace(part, dest)


def main():
    parser = argparse.ArgumentParser(description="Fetch the theme models into models/ and check their hashes.")
    parser.add_argument("--models", default=DEFAULT_MODELS, help="models folder (default: the repo's models/)")
    args = parser.parse_args()

    failed = False
    for rel, url, want in FILES:
        dest = os.path.join(args.models, *rel.split("/"))
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        if os.path.exists(dest) and sha256_of(dest) == want:
            print(f"ok     {rel}")
            continue
        print(f"fetch  {rel}")
        try:
            download(url, dest)
        except (OSError, urllib.error.URLError) as e:
            print(f"FAILED {rel}: {e}", file=sys.stderr)
            failed = True
            continue
        got = sha256_of(dest)
        if got != want:
            os.remove(dest)
            print(f"BAD    {rel}: sha256 {got}, expected {want}; deleted", file=sys.stderr)
            failed = True
        else:
            print(f"ok     {rel} ({os.path.getsize(dest):,} bytes)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
