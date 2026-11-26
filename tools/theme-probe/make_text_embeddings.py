"""Precompute the CLIP text embeddings of the seven biomes and the nineteen scene variants (INPC G0.5 Task 1
Step 4, Task 4 with the v2 design's scene variants).

Reads  models/download/clip-text-model.onnx and models/download/clip-tokenizer.json (see fetch_models.py)
Writes models/clip-text-biomes.json: {"Field": [512 floats], ...} in the order of the Biome enum, and
       models/clip-text-scenes.json: {"Beach": [512 floats], ...} in the order of the Scene enum.

Each vector is the mean of its three L2-normalized sentence embeddings, L2-normalized again. The game
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


# In the order of Hangbar.Core.World.Scene (v2 design §6-1). Three sentences each, about what one would see there.
SCENE_PROMPTS = {
    "Beach": ["a photo of a sandy beach by the sea", "a photo of the ocean coast", "a photo of a seaside pier"],
    "Pool": ["a photo of a swimming pool", "a photo of a pool with lane ropes", "a photo of an outdoor swimming pool deck"],
    "House": ["a photo of a living room", "a photo of a bedroom", "a photo of a kitchen in a home"],
    "Mountain": ["a photo of a mountain peak", "a photo of a mountain trail", "a photo of a rocky mountain ridge"],
    "Meadow": ["a photo of a grassy meadow", "a photo of rolling green hills", "a photo of a field with wildflowers"],
    "Farm": ["a photo of a farm field", "a photo of crop rows on farmland", "a photo of a barn and fences"],
    "CountryRoad": ["a photo of a country road", "a photo of a winding rural road", "a photo of a road lined with trees"],
    "Boulevard": ["a photo of a wide city street", "a photo of a crosswalk and traffic lights", "a photo of a busy avenue"],
    "Alley": ["a photo of a narrow alley", "a photo of a small back street", "a photo of a lane between low houses"],
    "RiverLake": ["a photo of a river", "a photo of a lake", "a photo of a bridge over a river"],
    "Forest": ["a photo of a dense forest", "a photo of a forest path", "a photo of tall trees in the woods"],
    "Park": ["a photo of a city park", "a photo of a park with benches and a pond", "a photo of a lawn with a walking path"],
    "Downtown": ["a photo of downtown skyscrapers", "a photo of a city square", "a photo of tall office buildings"],
    "Residential": ["a photo of a residential neighborhood", "a photo of houses with yards", "a photo of apartment buildings"],
    "Cafe": ["a photo of a cafe interior", "a photo of a restaurant with tables and chairs", "a photo of a coffee shop counter"],
    "Shop": ["a photo of a shop interior", "a photo of store shelves", "a photo of a checkout counter"],
    "Office": ["a photo of an office with desks", "a photo of a classroom", "a photo of a meeting room"],
    "SubwayStation": ["a photo of a subway station platform", "a photo of a subway train", "a photo of an underground station"],
    "TrainStation": ["a photo of a train station", "a photo of a railway platform with a roof", "a photo of a bus terminal"],
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

    def embed(prompts):
        out = {}
        for name, sentences in prompts.items():
            vectors = []
            for sentence in sentences:
                ids = tokenizer.encode(sentence).ids
                if ids[0] != START or ids[-1] != END:
                    raise SystemExit(f"unexpected tokens for {sentence!r}: {ids}")
                feed = {"input_ids": np.array([ids], np.int64)}
                if "attention_mask" in input_names:
                    feed["attention_mask"] = np.ones_like(feed["input_ids"])
                vectors.append(unit(session.run(["text_embeds"], feed)[0][0].astype(np.float64)))
            out[name] = unit(np.mean(vectors, axis=0))
        return out

    def write(vectors, file_name, what):
        with open(os.path.join(args.models, file_name), "w", encoding="utf-8", newline="\n") as f:
            json.dump({b: [round(float(x), 7) for x in v] for b, v in vectors.items()}, f, separators=(",", ":"))
            f.write("\n")
        names = list(vectors)
        closest = max(((float(vectors[a] @ vectors[b]), a, b) for i, a in enumerate(names) for b in names[i + 1:]))
        print(f"wrote {file_name}: {len(names)} {what} x {len(vectors[names[0]])} values; "
              f"closest pair {closest[1]}-{closest[2]} cosine {closest[0]:.3f}")

    write(embed(PROMPTS), "clip-text-biomes.json", "biomes")
    write(embed(SCENE_PROMPTS), "clip-text-scenes.json", "scenes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
