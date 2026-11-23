# INPC G0 블록 세계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 사진 한 장으로 픽셀 블록 파일 맵을 만들고, 둥근 캐릭터로 그 위를 걸어 다닌다. 내장 그래픽(Intel Iris Xe)에서 1920×1080, 중간 화질로 초당 30프레임이 나오는지 잰다(설계 §7 G0, §8 첫 번째 위험).

**Architecture:** 사진 → 파일 맵(높이·색·물) → 블록 저장소 → 청크 메시까지는 Godot에 기대지 않는 `Hangbar.Core`(`Hangbar.Core.World` 네임스페이스)에 두고 xUnit으로 테스트한다. 새 Godot 프로젝트 `game/`(Forward+, C#)은 Core가 만든 메시 데이터를 받아 그리고 충돌을 붙이고, 플레이어 조작·성능 측정·스크린샷 모드를 맡는다.

**Tech Stack:** Godot 4.7.2 .NET(winget `GodotEngine.GodotEngine.Mono`), .NET 8 SDK, xUnit, PowerShell 5.1 스크립트.

**Spec:** `HANGBAR/docs/superpowers/specs/2026-11-23-inpc-design.md` (v1.0). 아이디어 원본 `HANGBAR/docs/inpc/ideas.md`.

## Global Constraints

- 엔진 Godot 4.7.2 .NET, 언어 C#. 3D 세계 프로젝트는 `game/`, 렌더러 Forward+.
- 블록 크기 0.25m. 캐릭터 키 1.75m(블록 7개).
- 사진 한 장의 블록 해상도는 **192×192칸**(설계의 128~256 안에서 정함). 사진은 가운데를 정사각형으로 잘라 줄인다.
- 읽는 이미지 형식: jpg, png, webp. HEIC는 다음 단계.
- 사진은 PC 안에서만 읽는다. 사진·폴더·파일 이름과 경로를 로그·파일·네트워크에 남기지 않는다. 로그에는 크기·개수·시간·시드만 쓴다.
- 입구 폴더(사진이 있는 폴더)에는 아무것도 쓰지 않는다.
- 같은 사진이면 늘 같은 세계가 나온다. 시드는 파일 이름이 아니라 사진 내용으로 만든다.
- 블록 규칙, 세계 생성, 메시 생성처럼 엔진에 기대지 않는 로직은 `Hangbar.Core`에 두고 테스트한다.
- 지형만 네모나다. 캐릭터는 둥근 도형으로 만든다.
- 성능 하한: Iris Xe급 내장 그래픽, 1920×1080, 중간 화질, 평균 30fps 이상.
- `dotnet`은 PATH에 없다. `C:/Program Files/dotnet/dotnet.exe`를 쓴다. Godot은 `$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\`에 있다. 스크립트에 사용자 폴더 경로를 박지 않는다.
- Core 테스트 명령: `"C:/Program Files/dotnet/dotnet.exe" test HANGBAR/client/core.tests`

## Review Focus

1. **생김새가 제각각인 사진**(휴대폰 사진 4000×3000, 50×50 아이콘, 8000×1000 파노라마) — 모두 192×192 맵이 되고, 작은 사진은 늘려서라도 맵이 나와야 한다. → Task 1 `Prepare_*` 테스트.
2. **투명 PNG** — 투명한 픽셀이 검은 구멍이 되지 않고 연회색 바탕으로 채워져야 한다. → Task 1 `Transparent_pixels_become_light_gray`.
3. **아주 어둡거나 밝거나 무늬 없는 사진**(밤 사진, 흰 벽) — NaN이나 범위를 벗어난 높이 없이 걸을 수 있는 평지가 나와야 한다. → Task 2 `Flat_black_photo_*`, `Heights_stay_in_bounds`.
4. **0.25m 계단 지형** — 캐릭터가 한두 칸(0.25~0.5m) 턱에 걸리지 않고 걸어 올라가야 한다. → Task 6 자동 오르기와 수동 확인.
5. **읽을 수 없는 사진 파일, 맵 가장자리** — 깨진 파일이면 기본 사진으로 대신하고 죽지 않는다. 맵 밖으로 떨어지지 않고, 떨어져도 다시 세워진다. → Task 5 대체 경로, Task 6 가장자리·되살리기 수동 확인.

---

## 파일 구조

```
HANGBAR/
  client/core/World/PixelImage.cs        # 엔진 무관 RGBA 이미지
  client/core/World/ImageFit.cs          # 정사각형 자르기, 줄이기, 투명 채우기
  client/core/World/SamplePhotos.cs      # 코드로 그린 기본 사진(초원)
  client/core/World/FileMap.cs           # FileMap, MapGenSettings, FileMapGenerator, WorldSeed
  client/core/World/Block.cs             # 블록 값 인코딩(공기, 물, 색 블록)
  client/core/World/VoxelWorld.cs        # 청크 단위 블록 저장소, FileMap으로 채우기
  client/core/World/ChunkMesher.cs       # MeshData, 면 컬링, 꼭짓점 AO, 물 메시
  client/core.tests/World/*Tests.cs
  game/project.godot                     # Forward+
  game/Inpc.Game.csproj                  # Godot.NET.Sdk/4.7.2, core 참조
  game/Inpc.Game.sln
  game/Main.tscn
  game/src/WorldRoot.cs                  # 인자 처리, 세계 생성, 청크 노드
  game/src/PhotoLoader.cs                # Godot Image → PixelImage
  game/src/Player.cs                     # 이동, 자동 오르기, 카메라
  game/src/Figure.cs                     # 둥근 캐릭터 도형
  game/src/PerfProbe.cs                  # HUD, 벤치 모드, 스크린샷 모드
  scripts/game/run.ps1                   # 빌드 후 실행
  scripts/game/bench.ps1                 # 1920×1080 벤치, JSON 출력
  scripts/game/shots.ps1                 # 고정 시점 스크린샷
  docs/inpc/g0-results.md                # 측정·스크린샷·사용자 확인 목록
```

---

### Task 1: 사진 준비 (PixelImage, ImageFit, SamplePhotos)

**Files:**
- Create: `client/core/World/PixelImage.cs`, `client/core/World/ImageFit.cs`, `client/core/World/SamplePhotos.cs`
- Test: `client/core.tests/World/ImageFitTests.cs`

**Interfaces:**
- Produces:
  - `sealed class PixelImage(int width, int height, uint[] rgba)` — `int Width`, `int Height`, `uint[] Rgba`(행 우선, 픽셀 값 `0xRRGGBBAA`), 인덱서 `uint this[int x, int y]`. 길이가 `width*height`가 아니면 `ArgumentException`.
  - `static class ImageFit` — `const uint TransparentFill = 0xD8D8D8FF`; `PixelImage Prepare(PixelImage src, int size)` = 가운데 정사각형 자르기 → `size×size`로 줄이기(작으면 최근접으로 늘리기) → 알파 128 미만 픽셀은 `TransparentFill`, 나머지는 알파 255로.
  - `static class SamplePhotos` — `PixelImage Meadow(int size = 256)`: 초원, 언덕, 연못이 있는 결정적인 그림(난수 없이 좌표 해시로).

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void Prepare_crops_landscape_to_center_square() { /* 4×2 이미지 왼쪽 1열·오른쪽 1열은 빨강, 가운데 2×2는 초록 → Prepare(size:2) 모든 픽셀 0x00FF00FF */ }
[Fact] public void Prepare_downscales_by_averaging() { /* 2×2 = 검정·흰색·검정·흰색 → Prepare(size:1)[0,0]의 R,G,B 각각 127~128 */ }
[Fact] public void Prepare_upscales_tiny_image() { /* 1×1 파랑 → Prepare(size:192) 크기 192×192, 모든 픽셀 파랑 */ }
[Fact] public void Prepare_handles_panorama_and_phone_sizes() { /* 8000×1000, 4000×3000 단색 → 둘 다 192×192 */ }
[Fact] public void Transparent_pixels_become_light_gray() { /* 알파 0 픽셀 → 0xD8D8D8FF, 알파 200 빨강 → 0xFF0000FF */ }
[Fact] public void Meadow_is_deterministic() => Assert.Equal(SamplePhotos.Meadow(64).Rgba, SamplePhotos.Meadow(64).Rgba);
```

- [ ] **Step 2:** `dotnet test client/core.tests --filter ImageFitTests` → 컴파일 실패 확인.
- [ ] **Step 3:** 세 파일을 구현한다. 줄이기는 영역 평균(박스 필터), 늘리기는 최근접.
- [ ] **Step 4:** 같은 필터로 PASS 확인.
- [ ] **Step 5:** 커밋 `feat(core): photo preparation for file maps`.

---

### Task 2: 파일 맵 만들기 (높이, 색, 물, 시드)

**Files:**
- Create: `client/core/World/FileMap.cs`
- Test: `client/core.tests/World/FileMapGeneratorTests.cs`

**Interfaces:**
- Consumes: Task 1 `PixelImage`, `ImageFit.Prepare`
- Produces:
  - `sealed record MapGenSettings(int Size = 192, int MinGround = 8, int Relief = 24, int WaterLevel = 8)` — 단위는 블록.
  - `sealed class FileMap` — `int Size`, `int WaterLevel`, `ulong Seed`, `int[] Height`(칸마다 단단한 블록 수, 맨 위 블록 y = Height-1), `uint[] Color`(`0xRRGGBB`), `bool[] Water`, `int Index(int x, int z) => z*Size + x`.
  - `static class FileMapGenerator` — `FileMap Generate(PixelImage photo, MapGenSettings s)`. 입력은 아무 크기나 받고 안에서 `ImageFit.Prepare(photo, s.Size)`를 부른다.
  - `static class WorldSeed` — `ulong Of(PixelImage prepared)`: `Rgba` 전체에 대한 FNV-1a 64비트.

높이 규칙(설계 §6-3 "넓게 고른 곳은 평지, 무늬 많은 곳은 언덕"을 이렇게 정한다):

```
L(x,z)      = 밝기 (0.299R + 0.587G + 0.114B) / 255
V(x,z)      = 5×5 창 안 L의 표준편차
detail      = clamp(V / 0.2, 0, 1)
raw         = 0.35·L + 0.65·detail
smooth      = raw에 3×3 박스 블러를 두 번 (가장자리는 안쪽 값 반복)
Height      = MinGround + round(smooth · Relief)
물          = (B > R + 20 && B > G - 5 && detail < 0.15)  [0~255 정수 기준]
물 칸       Height = WaterLevel - 2, Water = true
Color       = 준비된 사진의 그 칸 RGB
Seed        = WorldSeed.Of(준비된 사진)   // 파일 이름·경로는 쓰지 않는다
```

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void Same_photo_gives_same_map() { /* Meadow(256)로 두 번 Generate → Height, Color, Water, Seed 모두 같음 */ }
[Fact] public void Different_photos_give_different_seeds() { /* Meadow(256)과 왼쪽 위 8×8을 검정으로 칠한 사본 → Seed 다름 */ }
[Fact] public void Flat_black_photo_is_flat_ground_at_min() { /* 64×64 검정 → 모든 Height == 8, Water 없음 */ }
[Fact] public void Flat_white_photo_is_flat() { /* 흰색 → 모든 Height == 8 + round(0.35·24) == 16 */ }
[Fact] public void Textured_area_is_higher_than_flat_area() { /* 왼쪽 절반 회색 단색, 오른쪽 절반 체커보드 → 오른쪽 중앙 Height > 왼쪽 중앙 Height */ }
[Fact] public void Flat_blue_area_becomes_water() { /* 단색 (40,90,200) → 모든 칸 Water, Height == 6 */ }
[Fact] public void Heights_stay_in_bounds() { /* 무작위 시드 고정 노이즈 이미지 → 8 ≤ Height ≤ 32, 물 칸은 6 */ }
```

- [ ] **Step 2:** `--filter FileMapGeneratorTests` → 컴파일 실패 확인.
- [ ] **Step 3:** 위 규칙대로 구현한다.
- [ ] **Step 4:** PASS 확인.
- [ ] **Step 5:** 커밋 `feat(core): file map from a photo (height, color, water, seed)`.

---

### Task 3: 블록 값과 블록 저장소

**Files:**
- Create: `client/core/World/Block.cs`, `client/core/World/VoxelWorld.cs`
- Test: `client/core.tests/World/VoxelWorldTests.cs`

**Interfaces:**
- Consumes: Task 2 `FileMap`
- Produces:
  - `static class Block` — `const ushort Air = 0, Water = 1`; `ushort Solid(uint rgb)` = `2 + rgb15`(R·G·B 상위 5비트씩); `bool IsSolid(ushort b) => b >= 2`; `uint ColorOf(ushort b)` = 5비트를 8비트로 늘린 `0xRRGGBB`(`v<<3 | v>>2`).
  - `sealed class VoxelWorld(int sizeX, int sizeY, int sizeZ)` — `const int ChunkSize = 16`; `int SizeX, SizeY, SizeZ`; `int ChunksX, ChunksY, ChunksZ`(올림 나눗셈); `ushort Get(int x, int y, int z)`(범위 밖은 `Air`); `void Set(int x, int y, int z, ushort b)`(범위 밖은 무시); 저장은 청크마다 `ushort[16*16*16]`, 비어 있는 청크는 만들지 않는다.
  - `static VoxelWorld VoxelWorld.FromFileMap(FileMap map, int sizeY = 48)` — 칸마다 위에서부터: 맨 위 블록 = 사진 색, 그 아래 3블록 = 흙(사진 색 ×0.6), 나머지 = 바위(사진 색 ×0.35 + 회색 0x50 ×0.65). 물 칸은 `Height`부터 `WaterLevel-1`까지 `Water`.

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void Solid_roundtrips_color_within_5bit_precision() { /* Solid(0xC86432) → ColorOf 결과 각 채널 차이 ≤ 7 */ }
[Fact] public void Out_of_bounds_reads_air() { /* new VoxelWorld(16,16,16).Get(-1,0,0) == Air, Get(0,16,0) == Air */ }
[Fact] public void Set_then_get_across_chunk_boundary() { /* 40×20×40 세계에서 (15,0,0),(16,0,0),(39,19,39)에 서로 다른 값 → 그대로 읽힘 */ }
[Fact] public void FromFileMap_layers_surface_dirt_rock() { /* Height 10, Color 0x80C040 한 칸 → y9 = Solid(0x80C040), y6~8 = 흙 색, y0~5 = 바위 색, y10 = Air */ }
[Fact] public void FromFileMap_fills_water_up_to_level() { /* 물 칸(Height 6, WaterLevel 8) → y6,y7 = Water, y8 = Air */ }
[Fact] public void Empty_chunks_are_not_allocated() { /* FromFileMap(평지 8) 후 y 32~47 청크는 할당 수에 들어가지 않음: AllocatedChunks 속성 */ }
```

  `int AllocatedChunks { get; }`도 Produces에 포함한다.

- [ ] **Step 2:** 컴파일 실패 확인.
- [ ] **Step 3:** 구현.
- [ ] **Step 4:** PASS 확인.
- [ ] **Step 5:** 커밋 `feat(core): block encoding and chunked voxel store`.

---

### Task 4: 청크 메시 (면 컬링, 꼭짓점 AO, 물)

**Files:**
- Create: `client/core/World/ChunkMesher.cs`
- Test: `client/core.tests/World/ChunkMesherTests.cs`

**Interfaces:**
- Consumes: Task 3 `VoxelWorld`, `Block`
- Produces:
  - `sealed class MeshData` — `List<float> Positions`(xyz, 미터), `List<float> Normals`(xyz), `List<float> Colors`(rgba 0~1, AO를 곱한 값), `List<int> Indices`; `int VertexCount => Positions.Count / 3`; `int TriangleCount => Indices.Count / 3`.
  - `static class ChunkMesher` — `const float BlockSize = 0.25f`; `(MeshData Solid, MeshData Water) Build(VoxelWorld w, int cx, int cy, int cz)`.
  - 규칙:
    - 단단한 블록의 면은 이웃 칸이 단단하지 않을 때(공기나 물)만 만든다. 이웃은 `w.Get`으로 보므로 청크 경계 너머도 맞게 가린다.
    - 면 하나 = 꼭짓점 4개, 삼각형 2개. 위치는 `블록 좌표 × BlockSize`.
    - 꼭짓점 AO: 그 꼭짓점을 둘러싼 옆1·옆2·모서리 칸 중 단단한 것으로 단계 계산(옆1과 옆2가 둘 다 단단하면 3, 아니면 개수). 밝기 배율 `{1.0, 0.8, 0.65, 0.5}`.
    - 사각형을 나누는 대각선은 `ao0 + ao2 > ao1 + ao3`이면 뒤집는다(AO 줄무늬 방지).
    - 물은 위가 공기인 물 블록의 윗면만 만든다. 높이는 블록 윗면보다 `0.1 × BlockSize` 낮게. 색 `(0.25, 0.55, 0.8, 0.7)`.

- [ ] **Step 1: 테스트 작성**

```csharp
[Fact] public void Single_block_has_six_faces() { /* 16³ 세계 가운데 블록 1개 → Solid.VertexCount 24, TriangleCount 12 */ }
[Fact] public void Touching_blocks_hide_shared_faces() { /* 나란한 블록 2개 → 면 10개(꼭짓점 40) */ }
[Fact] public void Faces_against_neighbor_chunk_are_culled() { /* 32×16×16 세계에서 (15,5,5),(16,5,5) → 청크 (0,0,0) 메시에 +x 면 없음: 면 5개 */ }
[Fact] public void Fully_exposed_top_face_has_no_occlusion() { /* 블록 1개 윗면 네 꼭짓점 색 = 블록 색 그대로(배율 1.0) */ }
[Fact] public void Corner_between_two_walls_is_darkest() { /* 바닥 블록 위 한 모서리 양옆에 벽 블록 2개 → 그 꼭짓점 배율 0.5 */ }
[Fact] public void Water_makes_only_top_faces() { /* 물 블록 2×1×1 아래 바닥 → Water.VertexCount 8, Solid 메시에 물 쪽 옆면이 생김 */ }
[Fact] public void Positions_are_in_meters() { /* 블록 (4,0,0) → 모든 x 좌표가 1.0 또는 1.25 */ }
```

- [ ] **Step 2:** 컴파일 실패 확인.
- [ ] **Step 3:** 구현.
- [ ] **Step 4:** PASS, 그리고 Core 전체 테스트(`dotnet test client/core.tests`)도 통과 확인.
- [ ] **Step 5:** 커밋 `feat(core): chunk mesher with face culling, vertex AO and water`.

---

### Task 5: Godot 게임 프로젝트와 세계 그리기

**Files:**
- Create: `game/project.godot`, `game/Inpc.Game.csproj`, `game/Inpc.Game.sln`, `game/Main.tscn`, `game/src/WorldRoot.cs`, `game/src/PhotoLoader.cs`, `scripts/game/run.ps1`
- Modify: `HANGBAR/.gitignore`(`game/.godot/`는 이미 `.godot/` 규칙으로 걸리는지 확인만)

**Interfaces:**
- Consumes: Task 1~4 전부
- Produces:
  - 실행 인자(Godot의 `--` 뒤 사용자 인자): `--photo=<경로>`, `--quality=low|medium`(기본 medium), `--bench=<초>`, `--shots=<폴더>`, `--out=<파일>`
  - `static class PhotoLoader` — `PixelImage? Load(string path)`: `Godot.Image.LoadFromFile`(jpg/png/webp)를 RGBA8로 바꾼다. 실패하면 null. 경로는 로그에 쓰지 않는다.
  - `partial class WorldRoot : Node3D` — `VoxelWorld World`, `FileMap Map`, `Vector3 SpawnPoint`(맵 가운데 칸의 맨 위 + 0.5m).

- [ ] **Step 1:** `game/project.godot`: 이름 `INPC`, 메인 장면 `res://Main.tscn`, `renderer/rendering_method="forward_plus"`, 창 1600×900, `config/features=PackedStringArray("4.7", "C#", "Forward Plus")`, `[dotnet] project/assembly_name="Inpc.Game"`. `Inpc.Game.csproj`는 `Godot.NET.Sdk/4.7.2`, net8.0, Nullable·ImplicitUsings 켬, `../client/core/Hangbar.Core.csproj` 참조. `Inpc.Game.sln`에 게임과 core 두 프로젝트.
- [ ] **Step 2:** `WorldRoot._Ready`:
  1. `--photo`가 있으면 `PhotoLoader.Load`. 없거나 실패하면 `SamplePhotos.Meadow()`. 실패했을 때는 `[world] photo unreadable, using sample`만 출력한다.
  2. `FileMapGenerator.Generate(photo, new MapGenSettings())` → `VoxelWorld.FromFileMap(map)`.
  3. 청크마다 `ChunkMesher.Build`. 비어 있지 않은 Solid 메시는 `ArrayMesh`(정점 색 사용 `StandardMaterial3D`, `vertex_color_use_as_albedo`, roughness 0.9) + `StaticBody3D`/`CollisionShape3D`(`ConcavePolygonShape3D`, Solid 삼각형). Water 메시는 투명 재질(알파, 그림자 끄기)로 따로.
  4. 환경: `ProceduralSkyMaterial` 하늘, 해(`DirectionalLight3D`, 그림자는 medium에서만, `directional_shadow_max_distance` 60), 톤맵 ACES, 글로 약하게. SSAO·SDFGI·볼류메트릭 안개는 끈다(AO는 꼭짓점에 구웠음). medium은 FXAA, low는 안티앨리어싱 없음.
  5. 한 줄 로그: `[world] photo=WxH map=192 seed=<16진수> chunks=<개수> tris=<합> gen=<ms> mesh=<ms>`.
- [ ] **Step 3:** `scripts/game/run.ps1 [-Photo <경로>] [-Quality low|medium]`: `dotnet build game/Inpc.Game.csproj`가 실패하면 멈추고, 성공하면 Godot 콘솔 실행 파일로 `--path game -- --photo=... --quality=...`. Godot 경로는 `$env:LOCALAPPDATA` 아래에서 찾는다.
- [ ] **Step 4: 확인**
  - `dotnet build game/Inpc.Game.csproj` → 경고 외 오류 0.
  - `scripts/game/run.ps1`(사진 없음)을 10초 띄웠다 닫으면 로그에 `[world] photo=256x256 map=192 seed=` 줄이 있다.
  - 존재하지 않는 경로로 `-Photo`를 주면 `[world] photo unreadable, using sample`이 찍히고 같은 seed가 나온다.
- [ ] **Step 5:** 커밋 `feat(game): Godot project that builds a block world from a photo`.

---

### Task 6: 캐릭터와 걷기

**Files:**
- Create: `game/src/Player.cs`, `game/src/Figure.cs`
- Modify: `game/src/WorldRoot.cs`(플레이어 생성, 맵 가장자리 벽)

**Interfaces:**
- Consumes: Task 5 `WorldRoot.SpawnPoint`, `WorldRoot.World`
- Produces: `partial class Player : CharacterBody3D` — `bool InputEnabled`(벤치 모드에서 끔), `Camera3D Camera`.

- [ ] **Step 1:** `Figure`: 둥근 몸통(구 늘림), 큰 머리(구), 검은 점 눈 두 개, 원기둥 팔다리. 키 1.75m. 머티리얼은 몸 주황(0.91,0.38,0.17), 피부(1.0,0.86,0.74). 절경 시험 장면(`spikes/scenic-test/scenic.gd` `_character`)의 비율을 C#으로 옮긴다.
- [ ] **Step 2:** `Player` 이동:
  - 캡슐 충돌체 반지름 0.3m, 높이 1.75m.
  - 걷기 4.5m/s, Shift 달리기 7m/s, Space 점프 속도 5.5m/s, 중력은 프로젝트 기본값.
  - 마우스로 시점을 돌린다. 클릭하면 마우스를 잡고, Esc로 놓는다.
  - 카메라는 `SpringArm3D`(길이 4.5m, 머리 높이 1.6m), 아래로 -70°~위로 30°.
- [ ] **Step 3: 자동 오르기** — 0.25m 계단 지형에서 걸리지 않게 하는 알고리즘이다.

```
매 물리 프레임, 바닥에 서 있고 수평 입력이 있을 때:
  1. 몸을 위로 StepHeight(0.55m, 블록 2칸 + 여유)만큼 TestMove → 막히면 그만.
  2. 올라간 자리에서 수평 이동량만큼 TestMove → 막히면 그만.
  3. 거기서 아래로 StepHeight만큼 내려 바닥을 찾는다(TestMove의 충돌 결과).
  4. 찾은 바닥 높이가 지금 발보다 0.01m 이상 높고 StepHeight 이하이면 그 높이로 옮긴 뒤 평소처럼 MoveAndSlide.
```

- [ ] **Step 4:** 맵 가장자리: 맵 네 변 바깥에 보이지 않는 `StaticBody3D` 벽(높이 20m). 캐릭터 y가 -5m 아래로 떨어지면 `SpawnPoint`로 되돌린다.
- [ ] **Step 5: 확인** (사람이 직접. 결과를 Task 8 문서의 확인 목록에 적는다)
  - 언덕을 걸어 올라갈 때 0.25m·0.5m 턱에 걸리지 않는다.
  - 0.75m(3칸) 턱은 점프해야 오를 수 있다.
  - 물 칸(1.5m 아래)으로 떨어졌다가 걸어 나올 수 있다. 수영은 G2라 지금은 바닥을 걷는다.
  - 맵 끝에서 벽에 막힌다.
- [ ] **Step 6:** 커밋 `feat(game): round character with walking, step-up and third-person camera`.

---

### Task 7: 성능 측정 (HUD, 벤치 모드)

**Files:**
- Create: `game/src/PerfProbe.cs`, `scripts/game/bench.ps1`
- Modify: `game/src/WorldRoot.cs`(PerfProbe 붙이기)

**Interfaces:**
- Consumes: Task 5 인자, Task 6 `Player.InputEnabled`
- Produces: 벤치 JSON `{ "avgFps", "p1LowFps", "minFps", "frameMsP95", "tris", "drawCalls", "chunks", "resolution", "quality", "gpu" }`

- [ ] **Step 1:** HUD(F3로 켜고 끔): fps, 프레임 ms, `RenderingServer.GetRenderingInfo(TotalPrimitivesInFrame)`, `TotalDrawCallsInFrame`, 청크 수.
- [ ] **Step 2:** `--bench=<초>`:
  - 플레이어 입력을 끄고 카메라가 정해진 경로를 돈다. 앞 절반은 맵 둘레를 캐릭터 눈높이(지면 + 1.6m)로, 뒤 절반은 맵 위 20m에서 내려다보며 돈다.
  - 처음 2초는 버리고 나머지 프레임 시간을 모은다.
  - 끝나면 `--out` 파일에 JSON을 쓰고 종료한다.
  - `p1LowFps` = 가장 느린 1% 프레임들의 평균 fps. `gpu`는 `RenderingServer.GetVideoAdapterName()`.
- [ ] **Step 3:** `scripts/game/bench.ps1 [-Photo] [-Seconds 30] [-Quality medium] [-Out <파일>]`: 빌드 후 `--resolution 1920x1080`, 창 모드, `--bench`로 실행하고 JSON을 출력한다.
- [ ] **Step 4: 측정과 판단**
  - 이 PC에서 기본 사진과 medium으로 `bench.ps1`을 돌린다.
  - `avgFps ≥ 30`이면 통과다.
  - 30 미만이면 아래 순서로 하나씩 적용하고 다시 잰다. 각 단계와 수치를 결과 문서에 남긴다.
    1. 그림자 거리 60m → 30m.
    2. 보이는 거리 제한: 카메라에서 48m보다 먼 청크 숨기기(`visibility_range_end`).
    3. 같은 색 면 합치기(greedy meshing)를 `ChunkMesher`에 추가한다. 테스트 `Equal_color_flat_area_merges_into_one_quad`(4×4 같은 색 평지 → 윗면 사각형 1개)를 먼저 쓰고 실패를 본 뒤 구현한다. AO 값이 다른 면은 합치지 않는다.
  - 3단계까지 해도 30 미만이면 멈추고 수치를 사용자에게 보고한다(설계 재검토 필요).
- [ ] **Step 5:** 커밋 `feat(game): performance HUD and benchmark mode`.

---

### Task 8: 스크린샷 모드와 결과 정리

**Files:**
- Create: `scripts/game/shots.ps1`, `docs/inpc/g0-results.md`, `docs/inpc/g0/*.png`
- Modify: `game/src/PerfProbe.cs`(스크린샷 모드)

**Interfaces:**
- Consumes: Task 5 `--shots`, Task 7 벤치 JSON

- [ ] **Step 1:** `--shots=<폴더>`: 세 시점을 찍는다. 3초 기다린 뒤 PNG로 저장하고 종료한다.
  - `overview.png`: 맵 위 30m에서 비스듬히
  - `ground.png`: 캐릭터 뒤 눈높이
  - `top.png`: 바로 위에서 내려다보기. 원래 사진과 나란히 놓고 비교하는 용도다.
- [ ] **Step 2:** `scripts/game/shots.ps1 [-Photo] -Out <폴더>`. 사진 경로는 출력 파일 이름에 쓰지 않는다.
- [ ] **Step 3:** 기본 사진으로 찍은 세 장을 `docs/inpc/g0/`에 넣는다. 사용자 사진은 사용자 PC에만 두고 저장소에 올리지 않는다.
- [ ] **Step 4:** `docs/inpc/g0-results.md` 작성:
  - 벤치 수치 표: GPU, 해상도, 화질, avg/1% low/min fps, 삼각형, 드로콜. Task 7에서 적용한 최적화 단계별 수치도 넣는다.
  - 생성 시간(gen, mesh ms).
  - 스크린샷.
  - **사용자 확인 목록**
    - 본인 사진 서너 장(풍경, 인물, 음식, 화면 캡처)으로 `shots.ps1`을 돌려 "내 사진으로 만든 세계" 같은지 본다.
    - `run.ps1`로 걸어 보고 계단 오르기와 조작감을 본다.
    - 외장 그래픽 PC가 있으면 벤치를 한 번 더 돌린다.
- [ ] **Step 5:** Core 전체 테스트와 게임 빌드를 다시 돌려 통과를 확인하고 커밋 `docs(inpc): G0 results`.
