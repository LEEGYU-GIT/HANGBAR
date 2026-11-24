"""Precompute the CLIP text embeddings of the seven biomes (INPC G0.5 Task 1 Step 4).

Reads  models/download/clip-text-model.onnx and models/download/clip-tokenizer.json (see fetch_models.py)
Writes models/clip-text-biomes.json: {"Field": [512 floats], ...} in the order of the Biome enum.

Each biome vector is the mean of its three L2-normalized sentence embeddings, L2-normalized again. The game
then takes the dot product with an L2-normalized image embedding (clip-vision.onnx output "image_embeds")
as the cosine similarity. The text model is only needed here; the game ships the JSON, not the text model.

Usage: python tools/theme-probe/make_text_embeddings.py [--models DIR]
"""
import argparse
import json
import os
import sys

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "models"))

START, END = 49406, 49407  # CLIP's <|startoftext|> and <|endoftext|>

PROMPTS = {
    "Field": ["a photo of a grassy field", "a photo of a landscape under the sky", "a photo of a meadow"],
    "Road": ["a photo of a road", "a photo of a street", "a photo of an alley"],
    "Water": ["a photo of the sea", "a photo of a river or lake", "a photo of a swimming pool"],
    "Forest": ["a photo of a forest", "a photo of a park with trees", "a photo of a mountain"],
    "City": ["a photo of city buildings", "a photo of skyscrapers", "a photo of a downtown"],
    "Indoor": ["a photo of a room in a house", "a photo of a cafe interior",
               "a photo of a shop or restaurant interior"],
    "Transit": ["a photo of a subway", "a photo of inside a bus", "a photo of a train station platform"],
}


def unit(v):
    return v / np.linalg.norm(v)


def main():
    parser = argparse.ArgumentParser(description="Precompute CLIP text embeddings for the seven biomes.")
    parser.add_argument("--models", default=DEFAULT_MODELS, help="models folder (default: the repo's models/)")
    args = parser.parse_args()

    tokenizer = Tokenizer.from_file(os.path.join(args.models, "download", "clip-tokenizer.json"))
    session = ort.InferenceSession(os.path.join(args.models, "download", "clip-text-model.onnx"),
                                   providers=["CPUExecutionProvider"])
    input_names = {i.name for i in session.get_inputs()}
    if "text_embeds" not in {o.name for o in session.get_outputs()}:
        raise SystemExit("the text model has no text_embeds output")

    biomes = {}
    for biome, sentences in PROMPTS.items():
        vectors = []
        for sentence in sentences:
            ids = tokenizer.encode(sentence).ids
            if ids[0] != START or ids[-1] != END:
                raise SystemExit(f"unexpected tokens for {sentence!r}: {ids}")
            feed = {"input_ids": np.array([ids], np.int64)}
            if "attention_mask" in input_names:
                feed["attention_mask"] = np.ones_like(feed["input_ids"])
            vectors.append(unit(session.run(["text_embeds"], feed)[0][0].astype(np.float64)))
        biomes[biome] = unit(np.mean(vectors, axis=0))

    out = os.path.join(args.models, "clip-text-biomes.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump({b: [round(float(x), 7) for x in v] for b, v in biomes.items()}, f, separators=(",", ":"))
        f.write("\n")

    names = list(biomes)
    closest = max(((float(biomes[a] @ biomes[b]), a, b) for i, a in enumerate(names) for b in names[i + 1:]))
    print(f"wrote clip-text-biomes.json: {len(names)} biomes x {len(biomes[names[0]])} values; "
          f"closest pair {closest[1]}-{closest[2]} cosine {closest[0]:.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
