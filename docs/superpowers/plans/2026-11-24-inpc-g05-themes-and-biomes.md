# INPC G0.5 주제 인식과 바이옴 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 사진에서 큰 배경 주제(바이옴), 대표색, 분위기를 뽑아 그 사진을 닮은 블록 세계를 만든다. 주제는 PC 안에서 도는 로컬 모델로 알아내고, 모델 후보 둘을 사용자 사진으로 비교해 하나를 고른다(설계 §6-3, §6-3-1, §7 G0.5).

**Architecture:** 사진 → (로컬 모델) 주제 라벨 → 바이옴, 사진 → 팔레트·분위기·시드, 이 셋 → 바이옴 생성기 → `FileMap` + 둥근 소품 목록 + 사진 벽화. 엔진·모델에 기대지 않는 로직은 `Hangbar.Core`(`Hangbar.Core.World`)에 두고 테스트한다. ONNX 모델 실행은 새 라이브러리 `Hangbar.Theme`(ONNX Runtime 의존)에 두고, 게임과 비교 도구가 함께 쓴다. G0의 블록 저장소, 메시, 캐릭터, 벤치는 그대로 쓴다.

**Tech Stack:** Godot 4.7.2 .NET, .NET 8, xUnit, `Microsoft.ML.OnnxRuntime`(CPU), 모델 변환이 필요할 때만 Python 3.11+(`torch`, `onnx`).

**Spec:** `HANGBAR/docs/superpowers/specs/2026-11-23-inpc-design.md` (v1.2). G0 결과: `HANGBAR/docs/inpc/g0-results.md`. 아이디어: `HANGBAR/docs/inpc/ideas.md`.

## Global Constraints

- 바이옴 일곱 가지: 들판(기본) `Field`, 도로 `Road`, 물가 `Water`, 숲 `Forest`, 도시 `City`, 실내 `Indoor`, 교통 `Transit`.
- 주제 인식은 PC 안에서만 한다. 사진을 네트워크로 보내지 않는다. 실행은 ONNX Runtime CPU.
- 게임에 넣을 모델은 100MB 이하, 상업 판매 가능 라이선스. 사진 한 장 인식은 1초 안(이 PC의 CPU 기준).
- 사진, 폴더·파일 이름과 경로를 로그·파일·문서에 남기지 않는다. 결과 표에는 "사진 1, 사진 2…" 번호만 쓴다. 사용자 사진과 모델 파일은 저장소에 올리지 않는다(`.gitignore`).
- 같은 사진이면 같은 세계(시드는 `WorldSeed.Of(ImageFit.Prepare(photo, 192))`).
- 지형과 건물은 블록, 나무와 물건은 둥근 모양(설계 §4-2).
- 블록 0.25m, 캐릭터 1.75m, 파일 맵 192×192칸, 세로 48블록.
- 성능 하한: Iris Xe급, 1920×1080, 중간 화질 평균 30fps(G0 `bench.ps1`).
- 명령: Core 테스트 `dotnet test client/core.tests`, 게임 빌드 `dotnet build game/Inpc.Game.sln`. `dotnet`이 PATH에 없으면 `C:\Program Files\dotnet\dotnet.exe`.

## Review Focus

1. **판단이 애매한 사진**(흐린 사진, 클로즈업, 밈 이미지) — 모델이 확신하지 못하면 색 어림짐작으로 넘어가고, 그것도 애매하면 들판으로 간다. 절대 예외로 죽지 않는다. → Task 2 `Low_confidence_falls_back_to_colors`.
2. **같은 사진을 다시 열 때** — 같은 바이옴, 같은 세계가 나와야 하고 모델을 다시 돌리지 않는다(결과 캐시). → Task 2 `ThemeCache` 테스트.
3. **바이옴마다 걸어 다닐 수 있는가** — 도시 건물, 실내 가구, 교통 터널 때문에 캐릭터가 갇히거나 출발점이 막히면 안 된다. 출발점에서 맵 대부분에 걸어서 닿아야 한다. → Task 5 `Every_biome_is_mostly_walkable`.
4. **모델 파일이 없거나 깨졌을 때** — 게임은 색 어림짐작으로 계속 돈다. → Task 6 확인 단계.
5. **어둡거나 흑백인 사진** — 팔레트가 전부 검정·회색이어도 세계가 읽힐 만큼 대비가 있어야 한다. → Task 3 `Gray_photo_palette_keeps_contrast`.

---

## 파일 구조

```
HANGBAR/
  client/core/World/Biome.cs              # enum Biome, BiomeLabels(라벨→바이옴), ColorGuess(색 어림짐작)
  client/core/World/ThemeChoice.cs        # 모델 결과 + 대체 규칙 → 최종 바이옴, ThemeCache
  client/core/World/PhotoTraits.cs        # Palette(대표색), Mood(밝기·채도·색온도)
  client/core/World/BiomeGenerator.cs     # 바이옴별 지형 + 소품 + 벽화 → BiomeMap
  client/core/World/Preprocess.cs         # 모델 입력 텐서(크기·정규화)
  client/core/World/biome-labels.csv      # Places365 라벨 → 바이옴 표 (EmbeddedResource)
  client/core.tests/World/*Tests.cs
  client/theme/Hangbar.Theme.csproj       # net8.0, Microsoft.ML.OnnxRuntime
  client/theme/PlacesModel.cs             # 장면 분류 후보
  client/theme/ClipModel.cs               # 이미지-글 대응 후보
  tools/theme-probe/                      # 사진 폴더 → 두 후보 결과 표(콘솔)
  models/README.md                        # 모델 받는 법, 라이선스, 해시 (모델 파일은 git 제외)
  game/src/WorldRoot.cs                   # 새 파이프라인 연결
  game/src/Props.cs                       # 둥근 소품(나무, 컵, 화분…) 그리기
  scripts/game/theme-probe.ps1
  docs/inpc/g05-results.md
```

---

### Task 1: 모델 후보 확보와 라이선스 확인

**Files:**
- Create: `models/README.md`, `.gitignore`에 `models/*.onnx`, `models/*.bin`, `models/*.json`(README 제외)

**Interfaces:**
- Produces: `models/places365.onnx`(입력 224×224 RGB, 출력 365 로짓), `models/places365-labels.txt`, `models/clip-vision.onnx`(입력 224×224, 출력 임베딩), `models/clip-text-biomes.json`(바이옴마다 문장 임베딩, L2 정규화)

- [ ] **Step 1:** 후보 A: **Places365 ResNet18**(MIT CSAIL `places365` 저장소의 `resnet18_places365.pth.tar`)과 라벨 파일 `categories_places365.txt`.
  - 저장소와 모델 페이지의 라이선스를 읽고, 상업 게임에 넣어도 되는지 `models/README.md`에 원문 인용과 함께 적는다.
  - 상업 사용이 안 되면 "비교용만"으로 표시한다. 게임 기본값으로는 쓰지 않는다.
- [ ] **Step 2:** 후보 A를 ONNX로 바꾼다. Python 가상환경(`.venv`, git 제외)에서 `torch.onnx.export`를 쓴다. 입력 이름 `input`, 크기 1×3×224×224, opset 17. 크기와 SHA-256을 README에 적는다.
- [ ] **Step 3:** 후보 B: **OpenAI CLIP ViT-B/32**(MIT 라이선스).
  - 비전 인코더와 텍스트 인코더의 ONNX를 구한다. Hugging Face에 공개된 ONNX 변환본(예: `Xenova/clip-vit-base-patch32`의 `onnx/` 폴더)이나 직접 변환한 것을 쓴다. 출처, 파일, 라이선스, 크기, 해시를 README에 적는다.
  - 비전 인코더가 100MB를 넘으면 int8 양자화본을 쓴다.
- [ ] **Step 4:** CLIP 텍스트 임베딩을 미리 만든다. 바이옴마다 영어 문장 3개씩이다.
  - Field: "a photo of a grassy field", "a photo of a landscape under the sky", "a photo of a meadow"
  - Road: "a photo of a road", "a photo of a street", "a photo of an alley"
  - Water: "a photo of the sea", "a photo of a river or lake", "a photo of a swimming pool"
  - Forest: "a photo of a forest", "a photo of a park with trees", "a photo of a mountain"
  - City: "a photo of city buildings", "a photo of skyscrapers", "a photo of a downtown"
  - Indoor: "a photo of a room in a house", "a photo of a cafe interior", "a photo of a shop or restaurant interior"
  - Transit: "a photo of a subway", "a photo of inside a bus", "a photo of a train station platform"
  - 문장 임베딩을 L2 정규화해 바이옴별로 평균한다. 결과는 `models/clip-text-biomes.json`(`{ "Field": [512개 실수], … }`)에 저장한다. 만드는 스크립트는 `tools/theme-probe/make_text_embeddings.py`로 남긴다.
- [ ] **Step 5:** 커밋 `chore(models): candidate theme models, licenses and how to fetch them` (README와 스크립트만, 모델 파일은 제외).

---

### Task 2: 바이옴, 라벨 표, 색 어림짐작, 최종 선택

**Files:**
- Create: `client/core/World/Biome.cs`, `client/core/World/ThemeChoice.cs`, `client/core/World/biome-labels.csv`
- Test: `client/core.tests/World/BiomeTests.cs`, `client/core.tests/World/ThemeChoiceTests.cs`

**Interfaces:**
- Consumes: G0 `PixelImage`, `ImageFit`, `WorldSeed`
- Produces:
  - `enum Biome { Field, Road, Water, Forest, City, Indoor, Transit }`
  - `static class BiomeLabels` — `Biome? FromPlacesLabel(string label)`. Places365 라벨(예: `/c/coffee_shop`)에서 앞의 `/x/`와 뒤의 `/indoor` 같은 꼬리를 떼고 표에서 찾는다. 표에 없으면 null. 표는 `biome-labels.csv`(`label,biome`)를 EmbeddedResource로 읽는다.
  - `static class ColorGuess` — `Biome Guess(PixelImage prepared)`: 위쪽 1/3이 파랗고 밝으며 아래쪽이 초록이면 `Field`, 파랑이 전체의 40% 이상이면 `Water`, 초록이 50% 이상이면 `Forest`, 채도 낮은 회색이 60% 이상이면 `City`, 그 밖은 `Field`.
  - `sealed record ThemeResult(Biome Biome, float Confidence, string Source)` — Source는 `"places"`, `"clip"`, `"color"`.
  - `static class ThemeChoice` — `ThemeResult Choose(ThemeResult? model, PixelImage prepared, float minConfidence = 0.35f)`: 모델 결과가 있고 Confidence가 기준 이상이면 그것, 아니면 `ColorGuess`(Confidence 0, Source `"color"`).
  - `sealed class ThemeCache(string path)` — `ThemeResult? Get(ulong seed)`, `void Put(ulong seed, ThemeResult r)`. JSON 파일 하나(`{seed16진수: {biome, confidence, source}}`). 경로는 게임이 `user://`에 둔다.

- [ ] **Step 1: 테스트 작성**

```csharp
[Theory]
[InlineData("/c/coffee_shop", Biome.Indoor)] [InlineData("/l/living_room", Biome.Indoor)] [InlineData("/r/restaurant", Biome.Indoor)]
[InlineData("/h/highway", Biome.Road)] [InlineData("/s/street", Biome.Road)] [InlineData("/a/alley", Biome.Road)]
[InlineData("/b/beach", Biome.Water)] [InlineData("/s/swimming_pool/outdoor", Biome.Water)]
[InlineData("/f/forest_path", Biome.Forest)] [InlineData("/p/park", Biome.Forest)]
[InlineData("/d/downtown", Biome.City)] [InlineData("/s/skyscraper", Biome.City)]
[InlineData("/s/subway_station/platform", Biome.Transit)] [InlineData("/b/bus_interior", Biome.Transit)] [InlineData("/t/train_station/platform", Biome.Transit)]
[InlineData("/p/pasture", Biome.Field)]
public void Places_labels_map_to_biomes(string label, Biome b) => Assert.Equal(b, BiomeLabels.FromPlacesLabel(label));

[Fact] public void Unknown_label_is_null() => Assert.Null(BiomeLabels.FromPlacesLabel("/x/not_a_place"));
[Fact] public void Every_biome_has_labels() { /* 표의 바이옴 종류 수 == 7 */ }
[Fact] public void Blue_photo_guesses_water() { /* 단색 (40,90,200) → Water */ }
[Fact] public void Gray_photo_guesses_city() { /* 단색 (120,120,125) → City */ }
[Fact] public void Sky_over_grass_guesses_field() { /* 위 1/3 (150,190,240), 아래 (80,150,60) → Field */ }
[Fact] public void Low_confidence_falls_back_to_colors() { /* Choose(new(Road, 0.2f, "places"), 파란 사진) → Water, Source "color" */ }
[Fact] public void Missing_model_result_falls_back_to_colors() { /* Choose(null, 회색 사진) → City */ }
[Fact] public void Confident_model_wins() { /* Choose(new(Indoor, 0.8f, "clip"), 파란 사진) → Indoor */ }
[Fact] public void ThemeCache_roundtrips_and_survives_a_missing_file() { /* 없는 파일 Get → null, Put 후 새 ThemeCache로 Get → 같은 값 */ }
```

- [ ] **Step 2:** `dotnet test client/core.tests --filter "BiomeTests|ThemeChoiceTests"` → 컴파일 실패 확인.
- [ ] **Step 3:** 구현. `biome-labels.csv`는 Places365 365개 라벨 중 바이옴과 분명히 맞는 것을 넣는다(위 테스트 라벨 포함, 바이옴마다 최소 10개). 애매한 라벨은 넣지 않는다(→ 색 어림짐작).
- [ ] **Step 4:** PASS 확인.
- [ ] **Step 5:** 커밋 `feat(core): biomes, label map, color guess and theme choice`.

---

### Task 3: 사진 특징 (팔레트, 분위기)

**Files:**
- Create: `client/core/World/PhotoTraits.cs`
- Test: `client/core.tests/World/PhotoTraitsTests.cs`

**Interfaces:**
- Produces:
  - `static class Palette` — `uint[] Extract(PixelImage prepared, int count = 6)`: 미디언 컷으로 대표색 `count`개(`0xRRGGBB`)를 뽑아 밝기 순(어두운 것부터)으로 돌려준다. 결정적.
  - `sealed record Mood(float Brightness, float Saturation, float Warmth)` — 0~1, Warmth는 (평균 R − 평균 B)/255를 0~1로 옮긴 값(0.5가 중립).
  - `static class MoodReader` — `Mood Read(PixelImage prepared)`.
  - `static class Palette` — `uint[] EnsureContrast(uint[] palette)`: 가장 밝은 색과 가장 어두운 색의 밝기 차가 0.35 미만이면 양 끝을 밝게·어둡게 늘린다.

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void Two_color_photo_gives_both_colors() { /* 왼쪽 빨강, 오른쪽 파랑 → Extract(.., 2)가 두 색(각 채널 ±8) */ }
[Fact] public void Palette_is_sorted_dark_to_light() { /* Meadow → 밝기 오름차순 */ }
[Fact] public void Palette_is_deterministic() { /* Meadow 두 번 → 같은 배열 */ }
[Fact] public void Gray_photo_palette_keeps_contrast() { /* 단색 회색 → EnsureContrast 후 최대·최소 밝기 차 ≥ 0.35 */ }
[Fact] public void Night_photo_is_dark_and_warm_photo_is_warm() { /* 단색 (20,20,30) Brightness < 0.15; 단색 (230,150,60) Warmth > 0.6 */ }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. **Step 4:** PASS. **Step 5:** 커밋 `feat(core): photo palette and mood`.

---

### Task 4: 모델 입력과 두 후보 실행기

**Files:**
- Create: `client/core/World/Preprocess.cs`, `client/theme/Hangbar.Theme.csproj`, `client/theme/PlacesModel.cs`, `client/theme/ClipModel.cs`
- Test: `client/core.tests/World/PreprocessTests.cs`

**Interfaces:**
- Consumes: Task 1 모델 파일, Task 2 `ThemeResult`, `BiomeLabels`
- Produces:
  - `static class Preprocess` — `float[] ToChw(PixelImage img, int size, float[] mean, float[] std)`: 가운데 정사각형 → size×size(`ImageFit.Prepare`) → 채널 우선(CHW) float, `(v/255 − mean)/std`.
  - `interface IThemeModel : IDisposable { string Name { get; } ThemeResult? Classify(PixelImage photo); }` (Hangbar.Theme)
  - `PlacesModel(string onnxPath, string labelsPath)`: 소프트맥스 상위 5개 라벨을 `BiomeLabels`로 바꿔 바이옴별 확률을 더하고, 가장 큰 바이옴과 그 합을 Confidence로 낸다. 바이옴으로 못 바꾼 라벨만 있으면 null.
  - `ClipModel(string visionOnnxPath, string textEmbeddingsJson)`: 이미지 임베딩(L2 정규화)과 바이옴 임베딩의 코사인 유사도 ×100 → 소프트맥스. 가장 큰 바이옴의 확률을 Confidence로 낸다.
  - 둘 다 모델 파일이 없거나 열리지 않으면 생성자가 `FileNotFoundException`/`OnnxRuntimeException`을 던진다(호출하는 쪽이 색 어림짐작으로 간다).
  - 정규화 값: Places365 `mean {0.485,0.456,0.406}`, `std {0.229,0.224,0.225}`; CLIP `mean {0.4815,0.4578,0.4082}`, `std {0.2686,0.2613,0.2758}`.

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void ToChw_has_3_planes_of_size_squared() { /* 10×10 → Prepare 224 → 길이 3·224·224 */ }
[Fact] public void ToChw_normalizes_white() { /* 흰색, mean 0.5 std 0.5 → 모든 값 1.0 */ }
[Fact] public void ToChw_is_channel_first() { /* 빨강 단색 → 첫 평면 값 > 둘째·셋째 평면 값 */ }
```

- [ ] **Step 2~4:** 실패 확인 → `Preprocess` 구현 → PASS.
- [ ] **Step 5:** `Hangbar.Theme` 라이브러리(net8.0, `Microsoft.ML.OnnxRuntime` 최신 안정판, core 참조)에 두 실행기를 구현한다. `client/Hangbar.sln`에 추가하고 `dotnet build`가 오류 없이 되는지 확인한다.
- [ ] **Step 6:** 커밋 `feat(theme): ONNX runners for the two theme model candidates`.

---

### Task 5: 바이옴 생성기와 사진 벽화

**Files:**
- Create: `client/core/World/BiomeGenerator.cs`
- Modify: `client/core/World/FileMap.cs`(아래 `BiomeMap` 추가)
- Test: `client/core.tests/World/BiomeGeneratorTests.cs`

**Interfaces:**
- Consumes: Task 2 `Biome`, Task 3 `Palette`, `Mood`, G0 `FileMap`, `VoxelWorld`
- Produces:
  - `enum PropKind { Tree, Bush, Rock, Cup, Plant, Lamp, Bench }` — 둥근 소품.
  - `sealed record Prop(PropKind Kind, int X, int Z, float Scale, uint Color)`
  - `sealed record Building(int X, int Z, int W, int D, int Height, uint Color)` — 블록으로 세울 상자(도시 건물, 실내 가구, 승강장).
  - `sealed class BiomeMap(FileMap Terrain, IReadOnlyList<Building> Buildings, IReadOnlyList<Prop> Props, Biome Biome, Mood Mood)`
  - `static class BiomeGenerator` — `BiomeMap Generate(Biome biome, uint[] palette, Mood mood, ulong seed, int size = 192)`.
  - `static class Mural` — `void Stamp(VoxelWorld w, PixelImage prepared, int x0, int z0, int width = 64, int height = 48)`: 사진을 width×height로 줄여 x0..x0+width, z0 줄에 바닥에서 위로 세운 벽(두께 1블록)으로 찍는다. 아래쪽 사진 줄이 땅에 닿는다.
  - `VoxelWorld.FromBiomeMap(BiomeMap m, PixelImage prepared, int sizeY = 48)`: 지형(G0 `FromFileMap` 규칙) + 건물 상자 + 벽화. 벽화 위치는 출발점에서 북쪽(−z)으로 30칸 떨어진 곳, 가운데 정렬.
  - 바이옴 규칙(높이는 블록 단위, 기본 지면 8):
    - 공통: 값 노이즈 두 겹(큰 언덕 + 잔물결)으로 높이를 만들고, 칸 사이 높이 차는 최대 2블록으로 깎는다(걷기 보장). 색은 팔레트에서 높이·바이옴에 따라 고른다.
    - Field: 언덕 진폭 6. 나무·덤불 소품 드문드문.
    - Road: 진폭 5 + 맵 한쪽 끝에서 반대쪽 끝까지 이어지는 폭 12칸 구불구불한 길(평평, 팔레트에서 가장 어두운 회색 계열 색), 길가 가로등 소품.
    - Water: 맵의 40~60%가 물(물 높이 8, 바닥 5), 나머지는 모래·풀 해안.
    - Forest: 진폭 10, 나무 소품 촘촘히(맵 칸 1/60 정도).
    - City: 평지 + 격자 길(폭 8) + 건물 상자(높이 12~36블록) 블록마다 여러 개. 길은 출발점과 맵 가장자리를 잇는다.
    - Indoor: 바닥 평평 + 맵 가장자리 벽(높이 40) + 거대한 가구 상자(탁자 고원 높이 16~24, 오르는 계단 블록 포함) + 컵·화분 소품(큰 크기).
    - Transit: 평지 + 철로 두 줄(맵을 가로지름) + 승강장 상자(높이 4) + 터널 아치 하나.

- [ ] **Step 1: 테스트 작성**

```csharp
[Theory, MemberData(nameof(AllBiomes))]
public void Same_input_gives_same_map(Biome b) { /* Generate 두 번 → Height, Color, Water, Buildings, Props 같음 */ }

[Theory, MemberData(nameof(AllBiomes))]
public void Every_biome_is_mostly_walkable(Biome b)
{
    /* FromBiomeMap 후, 출발점(맵 가운데의 StandHeight)에서 시작해 "옆 칸으로 2블록 이하 오르내림, 머리 위 7블록 비어 있음"으로
       번지는 BFS가 맵 칸의 70% 이상에 닿는다. 출발점 칸 자체가 건물 안이 아니다. */
}

[Fact] public void Road_crosses_the_map() { /* 도로 칸(평평하고 길 색)이 x=0 또는 z=0 가장자리에서 반대 가장자리까지 이어짐 */ }
[Fact] public void Water_biome_is_40_to_60_percent_water() { }
[Fact] public void City_has_buildings_and_streets() { /* 건물 ≥ 8개, 높이 12~36 */ }
[Fact] public void Indoor_has_walls_and_climbable_furniture() { /* 가장자리 벽, 가구 상자 ≥ 3개, 각 가구 옆에 2블록 이하 계단 */ }
[Fact] public void Transit_has_rails_across_the_map() { }
[Fact] public void Forest_has_many_trees() { /* Tree 소품 ≥ 192·192/120 */ }
[Fact] public void Mural_shows_the_photo() { /* 2색 사진 → 벽 왼쪽 위 블록 색이 빨강 계열, 오른쪽 위가 파랑 계열 */ }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. **Step 4:** PASS, Core 전체 테스트도 통과.
- [ ] **Step 5:** G0의 `FileMapGenerator` 높이 규칙은 쓰지 않으므로 지우고, 그 테스트도 지운다. `ImageFit`, `WorldSeed`, `FileMap.StandHeight`는 남긴다. 지운 이유를 커밋 메시지에 적는다.
- [ ] **Step 6:** 커밋 `feat(core): biome generator and photo mural`.

---

### Task 6: 게임에 연결

**Files:**
- Create: `game/src/Props.cs`
- Modify: `game/src/WorldRoot.cs`, `game/Inpc.Game.csproj`(Hangbar.Theme 참조), `game/project.godot` 필요 시
- Test: 수동 + 스크린샷

**Interfaces:**
- Consumes: Task 2~5 전부
- Produces: 실행 인자 `--theme=places|clip|color`(기본은 Task 7에서 고른 후보, 그 전에는 `clip`), 로그 `[world] biome=<이름> source=<places|clip|color> conf=<0.00> palette=<개수> buildings=<n> props=<n>`(사진 이름·경로 없음)

- [ ] **Step 1:** `WorldRoot`:
  1. 사진 준비
  2. `ThemeCache`(`user://theme-cache.json`) 확인
  3. 없으면 모델 실행. 모델 파일 위치는 실행 파일 옆 `models/`, 개발 중에는 저장소 `models/`. 실패하면 null.
  4. `ThemeChoice.Choose` → `Palette.Extract` + `EnsureContrast` → `MoodReader.Read`
  5. `BiomeGenerator.Generate` → `VoxelWorld.FromBiomeMap`
  6. 청크(G0와 같다)
- [ ] **Step 2:** 분위기 → 환경: Brightness로 노출과 하늘 밝기, Warmth로 해 색(따뜻하면 주황, 차가우면 푸름), Saturation 낮으면 옅은 안개.
- [ ] **Step 3:** `Props.cs`: `PropKind`마다 둥근 도형 묶음(Tree = 원기둥 줄기 + 구 두세 개, Cup = 원기둥 몸통 + 토러스 손잡이, Lamp = 가는 원기둥 + 구 …). `MultiMeshInstance3D`로 종류별 한 번에 그리고, 나무·컵처럼 큰 것에는 원기둥 충돌체를 붙인다.
- [ ] **Step 4: 확인**
  - 모델 파일을 치운 상태로 실행 → `source=color` 로그, 죽지 않는다(Review Focus 4).
  - 같은 사진을 두 번 실행 → 두 번째는 캐시에서 같은 `biome`이 나온다.
  - 바이옴마다 강제 인자 `--biome=<이름>`(개발용)으로 `shots.ps1`을 돌려 일곱 장면을 눈으로 본다.
  - `bench.ps1`로 가장 무거운 바이옴(도시, 숲)이 평균 30fps 이상인지 잰다. 안 되면 소품 수를 줄이고 수치를 결과 문서에 남긴다.
- [ ] **Step 5:** 커밋 `feat(game): theme recognition, biomes, props and mural in the world`.

---

### Task 7: 두 후보 비교와 선택 (사용자와 함께)

**Files:**
- Create: `tools/theme-probe/Program.cs`, `tools/theme-probe/theme-probe.csproj`, `scripts/game/theme-probe.ps1`, `docs/inpc/g05-results.md`

**Interfaces:**
- Consumes: Task 4 실행기
- Produces: `theme-probe <사진 폴더>` → 사진마다 한 줄: `번호, places 바이옴(확률), clip 바이옴(확률), 색 어림짐작, places ms, clip ms`. 출력에 파일 이름을 넣지 않는다. 번호와 파일 이름의 짝은 사진 폴더 밖의 임시 파일(`%TEMP%\inpc-probe-index.txt`)에만 쓴다.

- [ ] **Step 1:** 도구와 스크립트를 만든다. 폴더에서 jpg/png/webp만 읽는다.
- [ ] **Step 2:** 사용자가 사진 20~30장(풍경, 거리, 바다, 숲, 건물, 집, 카페, 가게, 지하철·버스, 음식, 화면 캡처)을 저장소 밖 폴더에 모은다. 공개 사진을 보탤 때는 Unsplash·Pexels를 쓴다.
- [ ] **Step 3:** 사용자가 사진마다 "정답 바이옴"을 적는다. 표는 번호로만 만든다.
- [ ] **Step 4:** 정확도(정답과 같은 비율), 평균 인식 시간, 모델 크기, 라이선스로 두 후보를 비교해 `g05-results.md`에 적는다.
  - 선택 기준: 정확도 80% 이상이면서 상업 사용 가능한 쪽.
  - 둘 다 되면 정확도가 높은 쪽, 같으면 작은 쪽.
  - 둘 다 80% 미만이면 멈추고 사용자에게 보고한다(라벨 표 보강, 문장 다듬기, 다른 후보 중 무엇을 할지).
- [ ] **Step 5:** 고른 후보를 게임 기본값으로 바꾸고, 사용자 사진 서너 장의 스크린샷 소감을 결과 문서의 "사용자 확인"에 적는다(스크린샷 자체는 올리지 않는다).
- [ ] **Step 6:** 커밋 `docs(inpc): G0.5 results and the chosen theme model`.
