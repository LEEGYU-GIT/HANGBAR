"""Convert MIT CSAIL's Places365 ResNet18 weights to ONNX, without PyTorch.

INPC G0.5 Task 1 Step 2. Reads models/download/ (see fetch_models.py) and writes:
    models/places365.onnx          input "input" float 1x3x224x224, output "logits" float 1x365 (opset 17)
    models/places365-labels.txt    365 lines, the label of each class index, e.g. "/a/airfield"

The checkpoint is a legacy PyTorch 0.2 file (a pickle stream followed by raw tensor data). It is read by an
unpickler that only knows how to rebuild tensors and refuses every other class, so nothing in the file can
run code. The network is torchvision's ResNet18 with 365 classes, written out node by node; every weight in
the checkpoint must be used, which catches a layer the graph misses.

Input preprocessing (what the game must do): RGB in 0..1, minus mean (0.485, 0.456, 0.406), divided by
std (0.229, 0.224, 0.225), channel-first. --check IMAGE prints the top-5 scenes for one image using the
preprocessing of MIT's run_placesCNN_basic.py (resize 256x256, center crop 224) to compare with its
published output.

Usage: python tools/theme-probe/convert_places365.py [--models DIR] [--check IMAGE]
"""
import argparse
import collections
import itertools
import os
import pickle
import struct
import sys

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "models"))

MAGIC = 0x1950A86A20F9469CFC6C
PROTOCOL = 1001
STORAGE_DTYPES = {
    "FloatStorage": np.float32, "DoubleStorage": np.float64, "HalfStorage": np.float16,
    "LongStorage": np.int64, "IntStorage": np.int32, "ShortStorage": np.int16,
    "CharStorage": np.int8, "ByteStorage": np.uint8,
}
MEAN = np.array([0.485, 0.456, 0.406], np.float32)
STD = np.array([0.229, 0.224, 0.225], np.float32)


# ---------------------------------------------------------------- legacy checkpoint reader

class _StorageType:
    def __init__(self, dtype):
        self.dtype = np.dtype(dtype).newbyteorder("<")


class _Storage:
    def __init__(self, dtype, numel):
        self.dtype, self.numel, self.array = dtype, numel, None

    def elements(self):
        return self.array


class _View:
    def __init__(self, root, offset, size):
        self.root, self.offset, self.size = root, offset, size

    def elements(self):
        return self.root.array[self.offset:self.offset + self.size]


class _LazyTensor:
    """A tensor whose storage bytes come after the pickle; resolved once they are read."""

    def __init__(self, storage, offset, size, stride):
        self.storage, self.offset, self.size, self.stride = storage, offset, tuple(size), tuple(stride)

    def materialize(self):
        base = self.storage.elements()
        if not self.size:
            return base[self.offset].copy()
        last = self.offset + sum((n - 1) * s for n, s in zip(self.size, self.stride))
        if self.offset < 0 or last >= len(base) or min(self.stride) < 0:
            raise ValueError("tensor reaches outside its storage")
        item = base.dtype.itemsize
        view = np.lib.stride_tricks.as_strided(base[self.offset:], shape=self.size,
                                               strides=tuple(s * item for s in self.stride))
        return np.ascontiguousarray(view).astype(base.dtype.newbyteorder("="))


def _rebuild_tensor(storage, storage_offset, size, stride, *_rest):
    return _LazyTensor(storage, storage_offset, size, stride)


class _SafeUnpickler(pickle.Unpickler):
    ALLOWED = {
        ("collections", "OrderedDict"): collections.OrderedDict,
        ("torch._utils", "_rebuild_tensor"): _rebuild_tensor,
        ("torch._utils", "_rebuild_tensor_v2"): _rebuild_tensor,
    }

    def __init__(self, f, storages):
        super().__init__(f, encoding="latin1")  # the file was written by Python 2.7
        self._storages = storages

    def find_class(self, module, name):
        if module == "torch" and name in STORAGE_DTYPES:
            return _StorageType(STORAGE_DTYPES[name])
        found = self.ALLOWED.get((module, name))
        if found is None:
            raise pickle.UnpicklingError(f"refusing to load {module}.{name}")
        return found

    def persistent_load(self, pid):
        if not isinstance(pid, tuple) or pid[0] != "storage":
            raise pickle.UnpicklingError(f"unexpected persistent id {pid!r}")
        storage_type, key, _location, numel, view = pid[1:]
        root = self._storages.setdefault(key, _Storage(storage_type.dtype, numel))
        if view is None:
            return root
        _view_key, offset, size = view
        return _View(root, offset, size)


def _resolve(obj):
    if isinstance(obj, _LazyTensor):
        return obj.materialize()
    if isinstance(obj, dict):
        return type(obj)((k, _resolve(v)) for k, v in obj.items())
    if isinstance(obj, (list, tuple)):
        return type(obj)(_resolve(v) for v in obj)
    return obj


def load_legacy_checkpoint(path):
    storages = {}
    with open(path, "rb") as f:
        def next_pickle():
            return _SafeUnpickler(f, storages).load()

        if next_pickle() != MAGIC:
            raise ValueError("not a legacy PyTorch checkpoint")
        if next_pickle() != PROTOCOL:
            raise ValueError("unknown checkpoint protocol")
        if not next_pickle().get("little_endian", True):
            raise ValueError("big-endian checkpoints are not supported")
        result = next_pickle()
        for key in next_pickle():
            storage = storages[key]
            (count,) = struct.unpack("<q", f.read(8))
            if count != storage.numel:
                raise ValueError("storage size does not match its header")
            data = f.read(count * storage.dtype.itemsize)
            if len(data) != count * storage.dtype.itemsize:
                raise ValueError("checkpoint is truncated")
            storage.array = np.frombuffer(data, dtype=storage.dtype)
    return _resolve(result)


# ---------------------------------------------------------------- ONNX graph

def build_resnet18(weights):
    """torchvision ResNet18 (BasicBlock x [2, 2, 2, 2]) as an ONNX graph, every weight taken from `weights`."""
    nodes, inits, used = [], [], set()
    names = (f"t{i}" for i in itertools.count())

    def param(key):
        used.add(key)
        inits.append(numpy_helper.from_array(np.asarray(weights[key], np.float32), key))
        return key

    def node(op, inputs, **attrs):
        out = next(names)
        nodes.append(helper.make_node(op, inputs, [out], **attrs))
        return out

    def conv(x, key, stride, pad):
        k = list(weights[key].shape[2:])
        return node("Conv", [x, param(key)], kernel_shape=k, strides=[stride, stride], pads=[pad] * 4)

    def bn(x, prefix):
        keys = [f"{prefix}.{p}" for p in ("weight", "bias", "running_mean", "running_var")]
        return node("BatchNormalization", [x] + [param(k) for k in keys], epsilon=1e-5)

    def relu(x):
        return node("Relu", [x])

    x = relu(bn(conv("input", "conv1.weight", 2, 3), "bn1"))
    x = node("MaxPool", [x], kernel_shape=[3, 3], strides=[2, 2], pads=[1, 1, 1, 1])
    for layer in range(1, 5):
        for block in range(2):
            p = f"layer{layer}.{block}"
            stride = 2 if layer > 1 and block == 0 else 1
            out = relu(bn(conv(x, f"{p}.conv1.weight", stride, 1), f"{p}.bn1"))
            out = bn(conv(out, f"{p}.conv2.weight", 1, 1), f"{p}.bn2")
            shortcut = x
            if f"{p}.downsample.0.weight" in weights:
                shortcut = bn(conv(x, f"{p}.downsample.0.weight", stride, 0), f"{p}.downsample.1")
            x = relu(node("Add", [out, shortcut]))
    x = node("Flatten", [node("GlobalAveragePool", [x])], axis=1)
    nodes.append(helper.make_node("Gemm", [x, param("fc.weight"), param("fc.bias")], ["logits"], transB=1))

    unused = sorted(k for k in weights if k not in used and not k.endswith("num_batches_tracked"))
    if unused:
        raise ValueError(f"checkpoint weights the graph does not use: {unused[:5]}")

    graph = helper.make_graph(
        nodes, "places365_resnet18",
        [helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 3, 224, 224])],
        [helper.make_tensor_value_info("logits", TensorProto.FLOAT, [1, 365])],
        inits)
    model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 17)], producer_name="inpc-theme-probe")
    model.ir_version = 8  # the IR that introduced opset 17; old enough for any ONNX Runtime the game ships
    onnx.checker.check_model(model, full_check=True)
    return model


def read_labels(path):
    labels = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.strip():
                name, index = line.split()
                labels[int(index)] = name
    if sorted(labels) != list(range(365)):
        raise ValueError("expected 365 labels numbered 0..364")
    return [labels[i] for i in range(365)]


# ---------------------------------------------------------------- check

def check(onnx_path, labels, image_path):
    import onnxruntime as ort
    from PIL import Image

    img = Image.open(image_path).convert("RGB").resize((256, 256), Image.BILINEAR)
    img = img.crop((16, 16, 240, 240))
    x = ((np.asarray(img, np.float32) / 255.0 - MEAN) / STD).transpose(2, 0, 1)[None]
    session = ort.InferenceSession(onnx_path, providers=["CPUExecutionProvider"])
    logits = session.run(["logits"], {"input": x.astype(np.float32)})[0][0]
    p = np.exp(logits - logits.max())
    p /= p.sum()
    for i in np.argsort(-p)[:5]:
        print(f"{p[i]:.3f} -> {labels[i][3:]}")


def main():
    parser = argparse.ArgumentParser(description="Convert the Places365 ResNet18 checkpoint to ONNX.")
    parser.add_argument("--models", default=DEFAULT_MODELS, help="models folder (default: the repo's models/)")
    parser.add_argument("--check", metavar="IMAGE", help="print the top-5 scenes for one image")
    args = parser.parse_args()

    checkpoint = load_legacy_checkpoint(os.path.join(args.models, "download", "resnet18_places365.pth.tar"))
    weights = {k.replace("module.", "", 1): v for k, v in checkpoint["state_dict"].items()}
    model = build_resnet18(weights)
    onnx_path = os.path.join(args.models, "places365.onnx")
    onnx.save(model, onnx_path)

    labels = read_labels(os.path.join(args.models, "download", "categories_places365.txt"))
    with open(os.path.join(args.models, "places365-labels.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(labels) + "\n")
    print(f"wrote places365.onnx ({os.path.getsize(onnx_path):,} bytes) and places365-labels.txt ({len(labels)} labels)")

    if args.check:
        check(onnx_path, labels, args.check)
    return 0


if __name__ == "__main__":
    sys.exit(main())
