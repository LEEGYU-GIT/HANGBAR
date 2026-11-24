# INPC 세계 생성기 2판-1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 블록에 재료와 손상 상태를 저장하고, 높이 160블록·지하·숨겨진 벽화·조립 부품을 갖춘 2판 생성기로 장면 4종(해변, 수영장, 집, 산)을 만든다. 서버 미리보기와 Windows 게임 화면으로 사용자가 확인할 수 있게 한다.

**Architecture:** 엔진과 무관한 것은 모두 `Hangbar.Core`에 둔다. 32비트 블록, 질감의 기준 함수(`BlockPattern`), 생성 공통 도구(난수, 노이즈, 경사 제한, 걷기, 시야), 텍스트 파일 부품, 장면 파이프라인(`WorldGenerator` + `IScene` 4종)이 여기에 들어간다. 리눅스 서버에서는 Core 테스트와 미리보기 도구(`tools/world-preview`)로 확인한다. 게임은 `--scene=` 개발 경로와 질감 셰이더를 추가하고 Windows에서 확인한다. 1판 생성기(`BiomeGenerator`)와 G0 게임 경로는 그대로 둔다(2판-2에서 정리).

**Tech Stack:** .NET 8, xUnit, Godot 4.7.2 .NET(셰이더 언어), PowerShell 스크립트(Windows).

**Spec:** `docs/superpowers/specs/2026-11-24-inpc-world-generator-v2-design.md` (v1.0, 2026-11-24 승인). 상위 설계: `docs/superpowers/specs/2026-11-23-inpc-design.md`.

**범위 밖 (설계 §9 순서):** 장면 인식(라벨 표의 변형 열, `ThemeResult`·`ThemeCache`의 장면, CLIP 장면별 문장)은 G0.5 Task 4 계획에서, 나머지 장면 15종은 2판-2에서, 매끄러운 캐릭터와 소품 충돌체는 G0.5 Task 6에서 다룬다.

## Global Constraints

- 블록 0.25m, 캐릭터 키 1.75m(블록 7개). 파일 맵 192×192칸, 세계 높이 160블록.
- 기준 지표 y=56. 산은 지표 위 20m(y=136)까지 오른다.
- 맨 아래 2블록(y=0, 1)은 부서지지 않는 바닥(`Material.Bedrock`)이다.
- 길의 칸은 옆 칸과 높이 차 2블록 이하, 머리 위 7블록 이상 빈 공간을 지킨다. 길이 아닌 곳에는 절벽을 허용한다.
- 맵 가장자리 16칸은 기준 지표 높이로 서서히 내려오고, 가장자리 칸에는 물이 없다.
- 출발점에서 걸어서 닿는 지표 칸이 맵의 70% 이상이다. 출발점은 막혀 있지 않다.
- 실내 크기: 천장 10~12블록, 문 폭 4 × 높이 9블록, 탁자 높이 3, 의자·침대 2, 선반 8, 벽 두께 1.
- 블록 값은 32비트이며 색 24비트, 재료 5비트(16종 이상), 상태 3비트(4종 이상)로 구성한다.
- 질감: 면마다 8×8 픽셀 격자, 압축 얼룩(블록 무늬) 모티프, 같은 재료도 블록 위치에 따라 다른 무늬. 사진 벽화 블록(`Material.Pixel`)은 무늬가 없다. 마인크래프트 질감과 닮으면 실패다.
- 벽화: 맵마다 1개, 출발점에서 보이지 않고, 출발점에서 40칸 이상 떨어져 있으며, 걸어서 닿는다. 위치는 시드로 정한다. 바깥은 최대 64×48블록, 건물 안은 액자다.
- 장면 변형마다 큰 볼거리(랜드마크) 1개 이상.
- 같은 사진이면 같은 PC에서 같은 세계(시드는 `WorldSeed.Of(ImageFit.Prepare(photo, 192))`).
- 사진, 폴더·파일 이름과 경로를 로그·파일·문서에 남기지 않는다. 개인 사진은 공용 개발 서버에 두지 않는다.
- 성능 하한: Iris Xe급 1920×1080 중간 화질 평균 30fps(Windows `bench.ps1`).
- 명령(리눅스): `source ~/sklee/tools/dotnet-env.sh` 후 `dotnet test client/core.tests`, `dotnet build game/Inpc.Game.sln`. Godot은 서버에 없으므로 게임 실행 확인은 사용자의 Windows PC에서 한다.

## Review Focus

1. **극단적인 팔레트(전부 흰색인 눈 사진, 전부 검은 밤 사진)** — 재료끼리 색 구분이 남아야 한다. → Task 6 `Extreme_palettes_keep_materials_apart`.
2. **어떤 시드에서는 벽화 자리를 못 찾는 경우** — 예외 없이 대체 벽을 세워 규칙을 지킨다. → Task 8 `Fallback_wall_when_no_candidate_works`, 장면마다 `Mural_rules_hold_for_ten_seeds`.
3. **아주 작거나 투명한 사진** — 수평선 추정과 생성이 예외 없이 끝난다. → Task 6 `Tiny_photo_generates`.
4. **여러 층 건물에서 소품이 공중에 뜨거나 벽에 묻힘** — 모든 소품은 단단한 블록 위의 빈칸에 선다. → 장면마다 `Props_stand_on_something`.
5. **동굴·유리 면 때문에 그릴 면이 폭증** — 장면마다 삼각형 수 상한을 둔다. → 장면마다 `Triangles_stay_under_budget` (450,000개 이하, G0 기본 사진의 약 2배).

---

## 파일 구조

```
client/core/World/
  Block.cs                 # 32비트 블록, Material, BlockState            (Task 1, 수정)
  VoxelWorld.cs            # uint 저장                                    (Task 1, 수정)
  ChunkMesher.cs           # 불투명·유리·물 메시, 정점별 재료·상태        (Task 2, 수정)
  BlockPattern.cs          # 질감의 기준 함수(셰이더가 그대로 옮김)       (Task 3)
  Horizon.cs               # 사진 수평선 추정, 카메라 숙임 각도            (Task 6)
  Scene.cs                 # enum Scene, Scenes                           (Task 6)
  FileMap.cs               # Prop에 Y, PropKind에 Parasol·Float 추가     (Task 6, 수정)
  BiomeGenerator.cs        # 1판. 공통 도구 사용으로 정리, Mural 오버로드 (Task 4, 8 수정)
  Gen/
    Rng.cs ValueNoise.cs Slope.cs Walkability.cs Sight.cs                (Task 4)
    PaletteRoles.cs Part.cs PartLibrary.cs PartPlacer.cs                  (Task 5)
    GeneratedWorld.cs SceneContext.cs WorldGenerator.cs Strata.cs
    EdgeBand.cs SurfaceMap.cs                                             (Task 6)
    Underground.cs                                                        (Task 7)
    Weathering.cs HiddenMural.cs                                          (Task 8)
    Scenes/MountainScene.cs BeachScene.cs PoolScene.cs HouseScene.cs      (Task 9, 11, 12, 13)
  Parts/*.part             # 부품 텍스트 파일(EmbeddedResource)           (Task 5, 9, 11~13)
client/core.tests/World/   # 위 파일마다 *Tests.cs, Gen/TestScene.cs
tools/world-preview/       # 서버 미리보기 도구                           (Task 10)
game/shaders/block.gdshader game/src/WorldView.cs game/src/PropsView.cs  (Task 14)
game/src/WorldRoot.cs Player.cs PerfProbe.cs, scripts/game/*.ps1         (Task 2, 14 수정)
docs/inpc/g05-v2-1-results.md                                             (Task 14)
```

---

### Task 1: 32비트 블록 (색·재료·상태)

**Files:**
- Modify: `client/core/World/Block.cs`, `client/core/World/VoxelWorld.cs`, `client/core/World/ChunkMesher.cs`(블록 형만), `client/core/World/BiomeGenerator.cs`(`Mural`의 `ushort Frame` → `uint`)
- Modify: `client/core.tests/World/VoxelWorldTests.cs`, `client/core.tests/World/ChunkMesherTests.cs` (`ushort` → `uint`)
- Test: `client/core.tests/World/BlockTests.cs`

**Interfaces:**
- Produces:
  - `enum Material : byte { Air = 0, Water = 1, Pixel = 2, Soil = 3, Grass = 4, Sand = 5, Stone = 6, DeepStone = 7, Bedrock = 8, Snow = 9, Wood = 10, Brick = 11, Concrete = 12, Tile = 13, Metal = 14, Glass = 15, DarkGlass = 16, Cloth = 17 }` — `Pixel`은 사진 벽화와 1판 블록, `DarkGlass`는 겉모양 건물의 불투명 창(설계 §6-11).
  - `enum BlockState : byte { Intact = 0, Cracked = 1, Mossy = 2, Worn = 3 }` (4~7은 예비)
  - `static class Block`: 비트 배치는 색 0~23, 재료 24~28, 상태 29~31.
    - `const uint Air = 0`; `static readonly uint Water` = `Make(Material.Water, 0)`
    - `uint Make(Material m, uint rgb, BlockState s = Intact)`, `uint Solid(uint rgb, Material m = Material.Pixel, BlockState s = Intact)` — `Solid`은 `Air`·`Water` 재료면 `ArgumentException`
    - `Material MaterialOf(uint b)`, `BlockState StateOf(uint b)`, `uint ColorOf(uint b)`, `uint WithState(uint b, BlockState s)`
    - `bool IsSolid(uint b)` (재료 ≥ Pixel), `bool IsWater(uint b)`, `bool IsOpaque(uint b)` (단단하고 Glass가 아님), `bool IsBreakable(uint b)` (단단하고 Bedrock이 아님)
  - `VoxelWorld`: `uint Get(int x, int y, int z)`, `void Set(int x, int y, int z, uint b)`. 청크 배열은 `uint[]`.

- [ ] **Step 1: 실패하는 테스트 작성** (`BlockTests.cs`)

```csharp
[Fact] public void Color_is_kept_exactly() => Assert.Equal(0xC86432u, Block.ColorOf(Block.Solid(0xC86432)));
[Fact] public void Material_and_state_roundtrip() { /* Pixel..Cloth × 상태 4종: MaterialOf, StateOf, ColorOf(0x123456) 모두 그대로 */ }
[Fact] public void Air_and_water_are_not_solid() { /* IsSolid(Air)=false, IsSolid(Water)=false, IsWater(Make(Water, 0x3366AA))=true */ }
[Fact] public void Glass_is_solid_but_see_through() { /* Glass: IsSolid true, IsOpaque false; DarkGlass: IsOpaque true */ }
[Fact] public void Bedrock_cannot_be_broken() { /* IsBreakable(Bedrock 블록)=false, IsBreakable(Stone 블록)=true */ }
[Fact] public void WithState_changes_only_the_state() { /* Stone 0xABCDEF Intact → Cracked: 재료·색 그대로 */ }
[Fact] public void Solid_refuses_air_and_water() => Assert.Throws<ArgumentException>(() => Block.Solid(0, Material.Water));
```

- [ ] **Step 2:** `dotnet test client/core.tests --filter BlockTests` → 컴파일 실패(Material 없음) 확인.
- [ ] **Step 3:** 위 Interfaces대로 구현한다. `ChunkMesher`의 `b == Block.Water`는 `Block.IsWater(b)`로 바꾼다. 기존 테스트의 `ushort`를 `uint`로 바꾼다.
- [ ] **Step 4:** `dotnet test client/core.tests` → 전부 통과(기존 210 + 새 7). `dotnet build game/Inpc.Game.sln` → 오류 0.
- [ ] **Step 5:** 커밋 `feat(core): 32-bit blocks with material and state`

---

### Task 2: 메시를 불투명·유리·물로 나누고 정점에 재료·상태 기록

**Files:**
- Modify: `client/core/World/ChunkMesher.cs`, `game/src/WorldRoot.cs`(`BuildChunks`, `ToMesh`)
- Test: `client/core.tests/World/ChunkMesherTests.cs`

**Interfaces:**
- Consumes: Task 1 `Block`, `Material`, `BlockState`
- Produces:
  - `MeshData.Uv2` (`List<float>`, 정점마다 2개: `u = (float)Material`, `v = (float)BlockState`). Godot의 `TexUV2`로 전달한다.
  - `sealed record ChunkMeshes(MeshData Opaque, MeshData Glass, MeshData Water)`; `static ChunkMeshes ChunkMesher.Build(VoxelWorld w, int cx, int cy, int cz)`
  - 규칙: 불투명 블록의 면은 이웃이 불투명하지 않을 때(공기·물·유리) 그린다. 유리 면은 이웃이 단단하지 않을 때 그린다. 주변 어둡게 하기(AO)는 불투명 이웃만 집계한다. 물 윗면 규칙은 1판과 같다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Opaque_face_next_to_glass_is_drawn() { /* Stone (5,5,5), Glass (6,5,5): Opaque에 x=6*0.25 위치의 +x 면이 있다 */ }
[Fact] public void Faces_between_glass_blocks_are_culled() { /* Glass 2개 나란히: Glass.TriangleCount == 20 */ }
[Fact] public void Glass_goes_to_its_own_mesh() { /* Glass 1개: Opaque.TriangleCount == 0, Glass.TriangleCount == 12 */ }
[Fact] public void Vertices_carry_material_and_state() { /* Stone+Cracked 1개: 모든 정점 Uv2 == (6, 1) */ }
[Fact] public void Glass_does_not_darken_neighbours() { /* 바닥 위 Glass 옆 칸의 윗면 AO가 Glass 없을 때와 같다 */ }
```
기존 테스트는 `.Opaque`, `.Water`로 바꾼다.

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현.
- [ ] **Step 4:** `WorldRoot`를 `ChunkMeshes`에 맞춘다. `Uv2`를 `Mesh.ArrayType.TexUV2`로 넘기고, 유리 메시는 반투명 `StandardMaterial3D`(알파 0.35, 양면)와 충돌체로 만든다. G0 실행 경로의 겉모습은 바뀌지 않는다.
- [ ] **Step 5:** `dotnet test client/core.tests` 통과, `dotnet build game/Inpc.Game.sln` 오류 0.
- [ ] **Step 6:** 커밋 `feat(core): opaque, glass and water chunk meshes with material and state per vertex`

---

### Task 3: 질감의 기준 함수 `BlockPattern`

**Files:**
- Create: `client/core/World/BlockPattern.cs`
- Test: `client/core.tests/World/BlockPatternTests.cs`

**Interfaces:**
- Consumes: Task 1 `Material`, `BlockState`
- Produces:
  - `readonly record struct Texel(float Shade, float Moss)` — 최종 색 = `mix(블록색 × Shade, MossColor, Moss)`. 미리보기(Task 10)와 셰이더(Task 14)가 같은 식을 사용한다.
  - `static class BlockPattern`: `const int Pixels = 8`, `const uint MossColor = 0x4E7A2F`, `uint Hash(int bx, int by, int bz, int face)`, `Texel Sample(Material m, BlockState s, int px, int py, uint hash)`.
  - `Hash`는 셰이더로 옮겨야 하므로 식을 고정한다.

```csharp
uint h = unchecked((uint)bx * 73856093u ^ (uint)by * 19349663u ^ (uint)bz * 83492791u ^ (uint)face * 2654435761u);
h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
```
  - 셰이더로 그대로 옮길 수 있게 정수·실수 계산만 사용한다(LINQ, 컬렉션, `Random` 금지). 재료별 무늬 모양은 빌더 재량이며 아래 테스트를 지키면 된다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Pixel_material_is_flat() { /* Pixel+Intact: 64칸 모두 Shade 1, Moss 0 */ }
[Fact] public void Patterns_keep_the_block_colour() { /* Pixel 외 재료 Intact: 64칸 평균 Shade 0.9~1.1, 각 칸 0.6~1.3 */ }
[Fact] public void Materials_look_different() { /* Stone–Wood, Brick–Tile, Grass–Sand, Soil–Concrete: 같은 hash에서 16칸 이상이 0.03 넘게 다름 */ }
[Fact] public void Stone_like_patterns_are_blocky() { /* Stone, DeepStone, Bedrock, Concrete: 짝수 좌표에서 시작하는 2×2 칸마다 Shade가 같다 */ }
[Fact] public void Same_material_varies_by_block() { /* Stone hash 1 vs 2: 8칸 이상 다름 */ }
[Fact] public void Cracked_adds_dark_pixels() { /* Stone: Intact보다 0.15 이상 어두운 칸 6개 이상 */ }
[Fact] public void Mossy_tints_from_the_top() { /* Stone: Moss>0인 칸 12개 이상, 그중 py 0~1 줄에 8개 이상 */ }
[Fact] public void Worn_lowers_contrast() { /* Stone, Brick, Wood: Worn의 (최대−최소 Shade) < Intact의 값 */ }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. **Step 4:** 통과.
- [ ] **Step 5:** 커밋 `feat(core): block pattern reference for textures`

---

### Task 4: 생성 공통 도구 (난수, 노이즈, 경사, 걷기, 시야)

**Files:**
- Create: `client/core/World/Gen/Rng.cs`, `ValueNoise.cs`, `Slope.cs`, `Walkability.cs`, `Sight.cs`
- Modify: `client/core/World/BiomeGenerator.cs`(안의 `Rng`, `Noise`, `Hash01`, `MinPlus`·`Shape`를 공통 도구로 교체), `client/core.tests/World/BiomeGeneratorTests.cs`(테스트 안 걷기 함수를 `Walkability`로 교체)
- Test: `client/core.tests/World/Gen/GenToolsTests.cs`

**Interfaces:** (namespace `Hangbar.Core.World.Gen`)
- Produces:
  - `readonly record struct BlockPos(int X, int Y, int Z)`
  - `sealed class Rng(ulong seed)`: `static ulong Mix(ulong z)`, `double Next()`, `int NextInt(int min, int maxExclusive)` (1판 SplitMix64 그대로)
  - `sealed class ValueNoise(ulong seed)`: `double Hash01(int x, int z, uint salt)`, `double Sample(double x, double z, int cell, uint salt)` (1판 식 그대로), `double Sample3(double x, double y, double z, int cell, uint salt)` (3차원, 동굴용)
  - `static class Slope`: `const int NoPin = int.MinValue`; `int[] Limit(int[] heights, int[] pins, int size, int step)` — 1판 `Shape`를 일반화(높이 차 상한 `step`). 고정값끼리 충돌하면 `InvalidOperationException`.
  - `static class Walkability`: `const int Step = 2, Headroom = 7`; `bool IsStandable(VoxelWorld w, int x, int y, int z)` (아래가 단단하고 y부터 7칸이 단단하지 않음); `HashSet<BlockPos> ReachCells(VoxelWorld w, BlockPos start)`; `bool[] ReachColumns(VoxelWorld w, BlockPos start)` (칸마다 하나라도 닿으면 참). 물은 바닥을 걷는다(1판과 같음).
  - `static class Sight`: `bool Clear(VoxelWorld w, (double X, double Y, double Z) from, (double X, double Y, double Z) to)` — 블록 좌표, 불투명 블록만 가린다(유리·물은 통과).

- [ ] **Step 1: 1판 결과 고정 테스트 먼저 작성.** 지금 코드로 `BiomeGenerator.Generate(Field|Water|Road, MeadowPalette, Calm, 12345)`의 높이·색·물·건물·소품을 FNV-1a로 해시해 값을 출력하고, 그 값을 리터럴로 넣은 `Version1_maps_are_unchanged_by_the_refactor`를 작성한다(정리 전 통과 확인).
- [ ] **Step 2: 도구 테스트 작성**

```csharp
[Fact] public void Limit_keeps_neighbours_within_step() { }
[Fact] public void Limit_holds_pinned_columns() { }
[Fact] public void Limit_throws_when_pins_conflict() { }
[Fact] public void Flat_ground_is_all_reachable() { /* 32×20×32, 지표 y=8: ReachColumns 전부 참 */ }
[Fact] public void Three_block_wall_stops_walking() { /* 가운데 높이 3 벽: 반대편 칸 거짓 */ }
[Fact] public void Two_block_steps_are_climbed() { }
[Fact] public void Tunnel_lower_than_seven_blocks_is_not_walkable() { /* 천장까지 6칸인 굴: 굴 안쪽 칸 거짓 */ }
[Fact] public void Open_air_is_clear() { }
[Fact] public void Opaque_wall_blocks_sight() { }
[Fact] public void Glass_and_water_do_not_block_sight() { }
```

- [ ] **Step 3:** 실패 확인 → 구현 → `BiomeGenerator`를 공통 도구로 교체 → `dotnet test client/core.tests` 전부 통과(1판 고정 테스트 포함).
- [ ] **Step 4:** 커밋 `refactor(core): shared generation tools (random, noise, slope, walking, sight)`

---

### Task 5: 조립 부품 (텍스트 파일, 색 역할, 회전, 늘이기, 낡음)

**Files:**
- Create: `client/core/World/Gen/PaletteRoles.cs`, `Part.cs`, `PartLibrary.cs`, `PartPlacer.cs`, `client/core/World/Parts/stone_steps.part`, `client/core/World/Parts/fence_segment.part`
- Modify: `client/core/Hangbar.Core.csproj` — `<EmbeddedResource Include="World/Parts/*.part" LogicalName="parts/%(Filename)%(Extension)" />`
- Test: `client/core.tests/World/Gen/PartTests.cs`

**부품 파일 형식** (UTF-8 텍스트, `#` 뒤는 주석):

```
part: fence_segment
size: 4 5 1                  # 폭(x) 높이(y) 깊이(z), 블록
legend:
  M = Metal dark             # 문자 = 재료 색역할
  G = Glass #C8E6F0          # 색역할 대신 고정색(#RRGGBB)
stretch: x 1 2               # 선택. 이 범위(포함)를 반복해 축 방향으로 늘린다
anchor: door 2 0 0           # 선택. 이름 x y z (회전과 함께 옮겨진다)
layer 0:                     # y=0부터 위로. 각 layer는 depth줄 × width글자, 줄 번호 = z, 글자 위치 = x
M..M
layer 1:
MMMM
...
```
- 예약 문자: `.` = 공기로 비움, `_` = 기존 블록 유지.
- 색 역할: `primary`, `secondary`, `accent`, `dark`, `light`.

**Interfaces:**
- Consumes: Task 1 `Block`, Task 4 `Rng`, `BlockPos`, Task 3 없음
- Produces:
  - `enum ColorRole { Primary, Secondary, Accent, Dark, Light }`
  - `sealed record PaletteRoles(uint Primary, uint Secondary, uint Accent, uint Dark, uint Light)`: `static PaletteRoles From(uint[] palette)` (팔레트 위치 0.55 / 0.35 / 1판 `LeafColor` 규칙 / 0.1 / 0.9, 이웃 색을 섞음), `uint this[ColorRole r]`
  - `sealed class Part`: `string Name`, `int Width, Height, Depth`, `PartCell? CellAt(int x, int y, int z)` (null = 유지), `IReadOnlyDictionary<string, BlockPos> Anchors`, `(char Axis, int From, int To)? Stretch`; `static Part Parse(string text, string name)` — 잘못된 형식은 `FormatException`(부품 이름과 줄 번호만 포함)
  - `readonly record struct PartCell(Material Material, ColorRole? Role, uint FixedColor)` (`Material.Air` = 비움)
  - `enum Rotation { R0, R90, R180, R270 }` (위에서 볼 때 시계 방향)
  - `sealed record PlaceOptions(int? StretchTo = null, double Missing = 0, BlockState State = BlockState.Intact)`
  - `sealed record PlacedPart(string Name, BlockPos Min, BlockPos Max, IReadOnlyDictionary<string, BlockPos> Anchors)`
  - `static PlacedPart PartPlacer.Place(VoxelWorld w, Part part, BlockPos origin, Rotation rotation, PaletteRoles colors, PlaceOptions options, Rng rng)`
  - `static class PartLibrary`: `Part Get(string name)` (없으면 `KeyNotFoundException`), `IReadOnlyList<string> Names`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Parse_reads_size_legend_and_layers() { /* 문자열 부품: 크기, 셀 재료·역할·고정색 */ }
[Fact] public void Bad_part_reports_its_line() { /* 줄 길이 틀림 → FormatException 메시지에 "line" 과 번호 */ }
[Fact] public void Roles_take_palette_colours() { /* Primary 칸의 색 == colors.Primary */ }
[Fact] public void Fixed_colour_is_kept() { }
[Fact] public void Keep_cells_leave_the_world_alone() { }
[Fact] public void Rotation_turns_the_part_clockwise() { /* 3×1×2 비대칭 부품을 R90으로 두면 x→z 방향 대응이 맞다 */ }
[Fact] public void Anchors_follow_rotation() { }
[Fact] public void Stretch_repeats_the_marked_columns() { /* StretchTo 10: 폭 10, 끝 칸은 원래 양 끝과 같다 */ }
[Fact] public void Missing_drops_about_that_share() { /* Missing 0.2: 빠진 비율 0.15~0.25 */ }
[Fact] public void Library_loads_every_embedded_part() { /* Names의 모든 부품이 Parse 오류 없이 읽히고 크기 > 0 */ }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현과 부품 2개(`stone_steps`: 3계단, 높이 2씩; `fence_segment`: 금속 난간, x 늘이기). **Step 4:** 통과.
- [ ] **Step 5:** 커밋 `feat(core): building parts as text files with roles, rotation, stretch and wear`

---

### Task 6: 2판 파이프라인 (장면, 지층, 가장자리, 수평선)

**Files:**
- Create: `client/core/World/Scene.cs`, `client/core/World/Horizon.cs`, `client/core/World/Gen/GeneratedWorld.cs`, `SceneContext.cs`, `WorldGenerator.cs`, `Strata.cs`, `EdgeBand.cs`, `SurfaceMap.cs`
- Modify: `client/core/World/FileMap.cs` — `Prop`에 마지막 매개변수 `int Y = -1`(−1이면 그 칸의 가장 높은 블록 위), `PropKind`에 `Parasol`, `Float` 추가(기존 값 뒤)
- Test: `client/core.tests/World/Gen/TestScene.cs`(시험용 장면), `WorldGeneratorTests.cs`, `client/core.tests/World/HorizonTests.cs`

**Interfaces:**
- Consumes: Task 1~5 전부, 1판 `Palette`, `Mood`, `PixelImage`, `FileMap`, `Prop`
- Produces:
  - `enum Scene { Beach, Pool, House, Mountain }`; `static class Scenes`: `Biome BiomeOf(Scene s)` (Beach·Pool → Water, House → Indoor, Mountain → Forest), `Scene? FromName(string name)` ("beach" 등, 대소문자 무시), `IScene Create(Scene s)` — 아직 없는 장면은 `NotSupportedException`(각 장면 Task에서 채운다)
  - `enum Facing { North, South, East, West }` (North = −z 쪽을 향함)
  - `sealed record MuralSpot(BlockPos Origin, Facing Facing, int Width, int Height, bool Framed)` — Origin은 그림 앞에 선 사람이 볼 때 왼쪽 아래 블록
  - `sealed record Landmark(string Name, BlockPos Center)`
  - `sealed record SceneView(BlockPos Spawn, BlockPos Target, IReadOnlyList<MuralSpot> MuralCandidates)`
  - `interface IScene { Scene Kind { get; } void Shape(SceneContext ctx); void Build(SceneContext ctx); SceneView View(SceneContext ctx); }`
  - `sealed class SceneContext`: 상수 `Size = 192, Height = 160, BaseGround = 56, EdgeBand = 16, WaterLevel = 56`; `uint[] Palette`, `PaletteRoles Colors`, `Mood Mood`, `PixelImage Photo`, `ulong Seed`, `Rng Rng`, `ValueNoise Noise`; ① 결과 `int[] Surface`(칸마다 처음 공기 y, 기본 56), `int[] WaterTop`(물이 Surface..WaterTop−1을 채움, 0 = 없음), `Material[] TopMaterial`(기본 Grass), `bool[] NoCaves`, `bool[] Path`; 이후 `VoxelWorld World`, `List<Prop> Props`, `List<PlacedPart> Structures`, `List<BlockPos> BurialSpots`, `Landmark? Landmark`, `void MarkOld(PlacedPart p)`, `bool IsOld(int x, int y, int z)`; `int Index(int x, int z)`
  - `sealed record GeneratedWorld(VoxelWorld World, FileMap Surface, Scene Scene, Mood Mood, ulong Seed, BlockPos Spawn, float FacingDegrees, float PitchDegrees, IReadOnlyList<Prop> Props, IReadOnlyList<PlacedPart> Structures, IReadOnlyList<BlockPos> BurialSpots, Landmark Landmark, MuralSpot Mural)` — `FacingDegrees`는 Godot 방식(앞 방향 = (−sin, 0, −cos)), `PitchDegrees`는 음수면 아래를 향한다
  - `static class WorldGenerator`: `GeneratedWorld Generate(Scene scene, uint[] palette, Mood mood, PixelImage prepared, ulong seed, float horizon = 0.5f)`, 같은 서명에 `IScene`을 받는 오버로드(시험용 장면에 사용). 순서: 팔레트 `EnsureContrast` → `Shape` → `EdgeBand.Apply` → `Strata.Fill` → (Task 7 지하) → `Build` → (Task 8 손상) → `View` → (Task 8 벽화) → `SurfaceMap.Of` → 결과. `Landmark`가 없으면 `InvalidOperationException`.
  - `static class Strata`: `void Fill(SceneContext ctx)` — y 0~1 Bedrock(0x2A2A2E), y 2~(19+노이즈 0~4) DeepStone, 그 위 Stone, 지표 아래 3~5블록 Soil, 맨 위 `TopMaterial`, `WaterTop`까지 물. 색은 `PaletteRoles`에서 정한다(빌더 재량, 재료끼리 밝기 차 0.06 이상).
  - `static class EdgeBand`: `void Apply(SceneContext ctx)` — 가장자리까지 거리 d(칸) < 16이면 `Surface = round(56 + (Surface−56) × smoothstep(d/16))`, d = 0이면 물 없음, 띠 안에서 `Surface ≥ WaterTop`이면 물 없음.
  - `static class SurfaceMap`: `FileMap Of(VoxelWorld w, ulong seed)` — 칸마다 맨 위 블록 기준 높이·색·물(게임의 `Map`, 벤치 카메라용)
  - `static class Horizon`: `float Estimate(PixelImage prepared)` (0 = 위 끝, 1 = 아래 끝, 하늘이 위에 없으면 0.5), `float PitchDegrees(float horizon, float verticalFov = 50f)` = `(horizon − 0.5) × verticalFov`, −25~15로 제한.

- [ ] **Step 1: 실패하는 테스트 작성.** `TestScene`: 56~70 사이 완만한 언덕, 한쪽 모서리 물, `stone_steps` 부품 하나(`MarkOld`), 출발점 맵 가운데, 목표 북쪽 30칸, 벽화 후보 1개, `Landmark("test", …)`.

```csharp
[Fact] public void World_is_192_by_160_by_192() { }
[Fact] public void Bottom_two_layers_are_bedrock() { }
[Fact] public void Layers_go_top_then_soil_then_stone_then_deep_stone() { /* 지표 ≥ 60인 칸: 맨 위 TopMaterial, 그 아래 Soil 3개 이상, y=30 Stone, y=10 DeepStone */ }
[Fact] public void Edges_settle_to_base_ground() { /* 가장자리 칸 Surface == 56, 물 없음; d=16 칸은 장면 값 그대로 */ }
[Fact] public void Same_inputs_give_the_same_world() { /* 블록 전체 해시, Spawn, Props, Structures 같음 */ }
[Fact] public void Spawn_is_open_ground() { }
[Fact] public void Facing_points_at_the_view_target() { /* 1° 이내 */ }
[Fact] public void Pitch_follows_the_photo_horizon() { /* horizon 0.3 → −10° */ }
[Fact] public void Extreme_palettes_keep_materials_apart() { /* 흰색·검은색 단색 사진: Grass·Soil·Stone 색 밝기 차 0.06 이상 (Review Focus 1) */ }
[Fact] public void Tiny_photo_generates() { /* 1×1 사진, 알파 0 사진: 예외 없음 (Review Focus 3) */ }
// HorizonTests
[Fact] public void Sky_over_grass_horizon_is_a_third() { /* 0.33 ± 0.05 */ }
[Fact] public void Photo_without_sky_is_the_middle() { /* 0.5 */ }
[Fact] public void Pitch_is_negative_when_the_horizon_is_high() { }
[Fact] public void Pitch_is_clamped() { }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. **Step 4:** `dotnet test client/core.tests` 전부 통과.
- [ ] **Step 5:** 커밋 `feat(core): world generator v2 pipeline (scenes, strata, edge band, horizon)`

---

### Task 7: 지하 (동굴, 지하수, 묻힐 자리)

**Files:**
- Create: `client/core/World/Gen/Underground.cs`
- Modify: `client/core/World/Gen/WorldGenerator.cs`(`Strata.Fill` 다음에 호출)
- Test: `client/core.tests/World/Gen/UndergroundTests.cs`

**Interfaces:**
- Consumes: Task 6 `SceneContext`, Task 4 `ValueNoise.Sample3`, `Rng`
- Produces: `static class Underground { void Carve(SceneContext ctx); }`
  - 동굴: 벌레처럼 지나간 길(worm) 14~20개, 길이 60~140걸음, 반지름 2~4블록, y 6 ~ (지표−8). 2개 이상은 지표에서 시작해 입구가 열린다.
  - `NoCaves` 칸과 가장자리 띠(d < 16)는 파내지 않는다. Bedrock은 파내지 않는다.
  - 지하수: 동굴 가장 낮은 곳 일부에 물 1~3블록.
  - 묻힐 자리: 정확히 12곳, 단단한 블록, 지표 아래 3~20블록, 서로 가로 8칸 이상, `NoCaves`·가장자리 띠 밖 → `ctx.BurialSpots`.

- [ ] **Step 1: 실패하는 테스트 작성** (`TestScene` 사용)

```csharp
[Fact] public void Caves_are_carved() { /* 지표−3 아래 공기 칸 3,000개 이상 */ }
[Fact] public void Some_caves_open_to_the_sky() { /* 지표 아래 공기에서 공기로만 이어져 지표 위로 나가는 길 */ }
[Fact] public void No_caves_under_protected_columns() { /* NoCaves 칸: 지표−1부터 y=2까지 모두 단단함 */ }
[Fact] public void No_caves_in_the_edge_band() { }
[Fact] public void Some_caves_hold_water() { /* 지표−3 아래 물 칸 20개 이상 */ }
[Fact] public void Twelve_burial_spots_inside_solid_ground() { }
[Fact] public void Bedrock_is_never_carved() { }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. **Step 4:** 통과(전체 테스트).
- [ ] **Step 5:** 커밋 `feat(core): caves, groundwater and burial spots`

---

### Task 8: 손상 상태, 숨겨진 벽화, 랜드마크

**Files:**
- Create: `client/core/World/Gen/Weathering.cs`, `client/core/World/Gen/HiddenMural.cs`
- Modify: `client/core/World/BiomeGenerator.cs`(`Mural`에 `void Stamp(VoxelWorld w, PixelImage prepared, MuralSpot spot)` 오버로드 추가, 1판 서명은 유지), `client/core/World/Gen/WorldGenerator.cs`
- Test: `client/core.tests/World/Gen/WeatheringTests.cs`, `HiddenMuralTests.cs`

**Interfaces:**
- Consumes: Task 4 `Walkability`, `Sight`, Task 6 `SceneContext`, `SceneView`, `MuralSpot`
- Produces:
  - `static class Weathering { void Apply(SceneContext ctx); }` — `MarkOld` 부품 블록은 30% Cracked, 공기와 맞닿은 자연 Stone·DeepStone은 5% Cracked, 물에서 2칸 안의 맨 위 블록(길 제외)은 40% Mossy, 길의 맨 위 블록은 70% Worn. Pixel·Glass·Bedrock은 손대지 않는다. 비율은 시드로 정해진다.
  - `static class HiddenMural { MuralSpot Place(SceneContext ctx, SceneView view); }` — 후보를 순서대로 검사해 처음 맞는 것을 찍는다.
    - 규칙 ①: 출발점 눈 높이(Spawn + (0.5, 6.4, 0.5))에서 그림 가운데와 네 모서리가 모두 `Sight.Clear` 거짓이다.
    - 규칙 ②: 출발점에서 가로 40칸 이상 떨어져 있다.
    - 규칙 ③: 그림 앞 1~2칸에 `Walkability.ReachCells`로 닿는 칸이 있다.
    - 맞는 후보가 없으면 닿는 칸 중 가장 먼 쪽에서 위 규칙을 지키는 24×18 홀로 선 벽을 세운다. 이 단계는 반드시 성공한다.
  - `Mural.Stamp(…, MuralSpot)`: 그림 앞에 선 사람이 볼 때 사진이 왼쪽에서 오른쪽으로 읽힌다. `Framed`면 바깥 1블록을 Wood(Dark 역할) 액자로 두른다. 그림 블록은 `Material.Pixel`이다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Old_structures_crack_about_a_third() { /* 0.25~0.35 */ }
[Fact] public void Paths_wear() { /* 길 맨 위 Worn ≥ 0.6 */ }
[Fact] public void Ground_by_water_gets_moss() { /* ≥ 0.3 */ }
[Fact] public void Photo_and_glass_blocks_never_weather() { }
[Fact] public void Mural_reads_left_to_right_for_every_facing() { /* 왼쪽 빨강·오른쪽 파랑 사진, Facing 4가지 */ }
[Fact] public void Framed_mural_has_a_wooden_border() { }
[Fact] public void Mural_is_hidden_from_the_spawn() { /* TestScene 시드 20개 */ }
[Fact] public void Mural_is_far_from_the_spawn() { }
[Fact] public void Mural_can_be_reached_on_foot() { }
[Fact] public void Fallback_wall_when_no_candidate_works() { /* 후보 0개인 TestScene 변형에서도 규칙 ①②③ (Review Focus 2) */ }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현. **Step 4:** 통과.
- [ ] **Step 5:** 커밋 `feat(core): weathering, hidden photo mural and landmark`

---

### Task 9: 산 장면

**Files:**
- Create: `client/core/World/Gen/Scenes/MountainScene.cs`, 부품 `client/core/World/Parts/mountain_hut.part`(문 4×9와 들어갈 수 있는 방, 설계 §6-11), `summit_cairn.part`, `rope_bridge.part`, `ruined_tower.part`
- Modify: `client/core/World/Scene.cs`(`Scenes.Create(Mountain)`)
- Test: `client/core.tests/World/Gen/Scenes/MountainSceneTests.cs`

**Interfaces:**
- Consumes: Task 4~8 전부
- Produces: `sealed class MountainScene : IScene`

**장면 규칙**
- **지형.** 봉우리 1~2개를 가장자리에서 40칸 이상 안쪽에 둔다. 가장 높은 지표는 y 116~136이다. 능선 노이즈와 절벽을 둔다(지형 전체에는 경사 제한을 두지 않는다).
- **길.** 등산로는 남쪽 출발 터(z 140~160, 9×9로 평평하게, `NoCaves`)에서 정상까지 A* 길 찾기로 낸다. 갈지자로 높이 차 2블록 이하, `Path`로 표시한다.
- **맨 위 재료.**
  - Stone: 옆 칸과 높이 차 3 이상이거나 y ≥ 116인 곳
  - Snow: y ≥ 128인 곳
  - Soil: 길
  - Grass: 그 밖
- **구조물.** 길 중간에 `mountain_hut`(평평한 터, `NoCaves`), 정상에 `summit_cairn`을 둔다. 길이 골짜기를 건너면 `rope_bridge`를 놓는다.
- **랜드마크.** 시드로 하나를 선택한다.
  - `ruined_tower`(`MarkOld`, Missing 0.2)
  - 돌 아치(Stone, 높이 12블록 이상)
  - 거대한 나무(Tree 소품 크기 4 이상 + Wood 뿌리 블록)
- **소품.** y 110 아래 Grass 경사에 나무 1/80칸, 바위.
- **출발점과 벽화.** 출발점은 출발 터 가운데, 목표는 정상이다. 벽화 후보는 출발점 반대쪽을 향한 높이 20블록 이상 절벽 면과, 동굴 입구 근처 동굴 벽이다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Peak_is_15_to_20_metres_over_the_ground() { /* 가장 높은 지표 y ∈ [116, 136] */ }
[Fact] public void There_are_cliffs() { /* 옆 칸 높이 차 3 이상인 쌍 200개 이상 */ }
[Fact] public void Trail_reaches_the_summit() { /* ReachCells에 가장 높은 칸에서 3칸 안의 꼭대기 칸 */ }
[Fact] public void Most_of_the_map_is_walkable() { /* ReachColumns ≥ 70% */ }
[Fact] public void Spawn_faces_the_summit() { /* 15° 이내, 출발점 눈에서 정상 꼭대기까지 Sight.Clear */ }
[Fact] public void Hut_and_cairn_stand() { /* Structures에 mountain_hut, summit_cairn */ }
[Fact] public void Hut_can_be_entered() { /* 오두막 안쪽 서 있을 수 있는 칸이 ReachCells에 있다 */ }
[Fact] public void Landmark_is_one_of_three() { }
[Fact] public void Two_seeds_differ() { /* 지표 높이가 다른 칸 ≥ 30% */ }
[Fact] public void Mural_rules_hold_for_ten_seeds() { }
[Fact] public void Props_stand_on_something() { /* 모든 소품: 아래 블록 단단, 제 칸 비어 있음 (Review Focus 4) */ }
[Fact] public void Triangles_stay_under_budget() { /* 모든 청크 Opaque+Glass+Water 삼각형 합 ≤ 450,000 (Review Focus 5) */ }
[Fact] public void Generates_within_three_seconds() { }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현과 부품 4개. **Step 4:** 통과(전체 테스트).
- [ ] **Step 5:** 커밋 `feat(core): mountain scene`

---

### Task 10: 서버 미리보기 도구 `tools/world-preview`

**Files:**
- Create: `tools/world-preview/WorldPreview.csproj`(net8.0 콘솔, Core 참조), `Program.cs`, `Png.cs`(PNG 쓰기: ZLibStream + CRC32), `Render.cs`, `README.md`

**Interfaces:**
- Consumes: Task 6 `WorldGenerator`, Task 3 `BlockPattern`, Task 4 `Walkability`, Task 2 `ChunkMesher`
- Produces: 명령 `dotnet run --project tools/world-preview -- --scene=mountain|beach|pool|house|all [--photo=사진.ppm] [--seed=N] --out=폴더`
  - 장면마다 그림 4장:
    - `<scene>-iso.png`: 비스듬히 위에서 본 전체
    - `<scene>-top.png`: 위에서 본 그림
    - `<scene>-slice.png`: 출발점을 지나는 x 방향 세로 단면. 지층과 동굴이 보인다.
    - `<scene>-close.png`: 출발점 주변 48×48칸을 블록 면 8픽셀로 그린 그림. `BlockPattern` 질감과 손상이 보인다.
  - 한 줄 통계(사진 경로·이름 없음): `scene= seed= tris= walk=% water=% mural=(x,y,z facing WxH) landmark=`
  - 사진 없이 실행하면 `SamplePhotos.Meadow()`를 사용한다. `--out`이 사진과 같은 폴더면 거부한다.
  - 아직 없는 장면은 건너뛰고 그 사실을 출력한다.

- [ ] **Step 1:** 도구를 만든다. 서버 임시 폴더의 시험 렌더러(등각 4×4 픽셀 스프라이트, 위에서 본 그림)를 옮기고 `slice`, `close` 보기를 추가한다.
- [ ] **Step 2:** `dotnet run --project tools/world-preview -- --scene=mountain --out=<서버 스크래치 폴더>` → PNG 4장과 통계 줄 확인. 그림을 직접 열어 산·길·동굴·질감이 보이는지 확인한다.
- [ ] **Step 3:** 커밋 `feat(tools): world preview for the v2 generator`

---

### Task 11: 해변 장면

**Files:**
- Create: `client/core/World/Gen/Scenes/BeachScene.cs`, 부품 `pier_deck.part`(x 늘이기), `pier_post.part`, `lifeguard_tower.part`, `beach_hut.part`(DarkGlass 창. 한 채는 문 4×9와 들어갈 수 있는 방, 설계 §6-11), `shipwreck.part`, `lighthouse.part`
- Modify: `Scene.cs`(`Scenes.Create(Beach)`)
- Test: `client/core.tests/World/Gen/Scenes/BeachSceneTests.cs`

**장면 규칙**
- **바다와 해안.**
  - 바다는 시드로 정한 한쪽(동서남북 중 하나)에 있고, 해안선은 굽는다.
  - 수면은 y 56이다. 바닥은 해안에서 멀어질수록 깊어져 y 44까지 내려간다.
  - 모래 해변은 y 56~62이고, 그 안쪽은 풀밭이다.
- **구조물.** 해변에서 바다로 `pier_deck`(+ `pier_post`)을 뻗고, `lifeguard_tower`와 `beach_hut` 1~2채를 둔다.
- **소품.** Parasol, Rock, Tree, 부표 줄(Float)을 둔다.
- **랜드마크.** 시드로 하나를 선택한다.
  - `shipwreck`(`MarkOld`, 일부는 물속)
  - 바다 위 돌 아치
  - 바위 곶 위의 `lighthouse`
- **출발점과 벽화.**
  - 출발점은 물가에서 10~20칸 떨어진 모래 위이고, 바다를 향한다(목표는 해안에서 바다로 40칸 나간 수면).
  - 벽화 후보: 해변 한쪽 끝 바위 절벽의 바다 동굴 벽, 사구 뒤편 바위 면, `beach_hut` 뒷벽.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Sea_covers_30_to_60_percent() { }
[Fact] public void Shore_is_sand() { /* 물에서 12칸 안 육지 맨 위 Sand ≥ 80% */ }
[Fact] public void Sea_floor_deepens_away_from_shore() { /* 해안 10칸 거리 평균 깊이 < 40칸 거리 평균 깊이 */ }
[Fact] public void Spawn_stands_on_sand_facing_the_sea() { /* 20° 이내, 출발점 눈에서 목표까지 Sight.Clear */ }
[Fact] public void Pier_reaches_into_the_water() { }
[Fact] public void One_hut_can_be_entered() { }
[Fact] public void Most_of_the_map_is_walkable() { }
[Fact] public void Landmark_is_one_of_three() { }
[Fact] public void Two_seeds_differ() { /* 지표 높이가 다른 칸 ≥ 30% */ }
[Fact] public void Mural_rules_hold_for_ten_seeds() { }
[Fact] public void Props_stand_on_something() { /* 수면 위 Float은 물 블록 위 (예외 규칙) */ }
[Fact] public void Triangles_stay_under_budget() { }
[Fact] public void Generates_within_three_seconds() { }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현과 부품 6개. **Step 4:** 통과. 미리보기 `--scene=beach`로 그림 확인.
- [ ] **Step 5:** 커밋 `feat(core): beach scene`

---

### Task 12: 수영장 장면

**Files:**
- Create: `client/core/World/Gen/Scenes/PoolScene.cs`, 부품 `pool_ladder.part`(Metal 손잡이), `diving_board.part`, `lifeguard_chair.part`, `changing_room.part`(겉모양 + 들어갈 수 있는 기계실 8×11×8, 문 4×9), `water_slide.part`, `high_dive_tower.part`
- Modify: `Scene.cs`(`Scenes.Create(Pool)`)
- Test: `client/core.tests/World/Gen/Scenes/PoolSceneTests.cs`

**장면 규칙**
- **지형.** 평평한 바닥 y 56(Tile 데크)이다.
- **수영장.** 길이 64~80블록, 폭 32~48블록이다. 레인 폭은 8블록이다. 바닥과 벽은 Tile(Light 역할)이고, 레인 선은 Tile(Dark 역할)이다.
- **깊이.** 얕은 쪽 5블록, 깊은 쪽 8블록이다. 수면 높이는 데크와 같다.
- **나오는 길.** 얕은 쪽 모서리에 2블록 높이 계단을 둔다. 사다리를 타는 기능이 없어서 계단으로 나온다. 레인 끝마다 `pool_ladder`도 둔다.
- **구조물.** 깊은 쪽에 `diving_board`, 옆에 `lifeguard_chair`를 둔다. 둘레에 `fence_segment`를 늘여 두르고, `changing_room`을 둔다.
- **소품.**
  - 레인 경계마다 Float를 2블록 간격으로 수면 높이(Y 지정)에 둔다.
  - 데크에 Bench, Parasol, Plant를 둔다.
- **랜드마크.** 시드로 `water_slide` 또는 `high_dive_tower`를 선택한다.
- **출발점과 벽화.**
  - 출발점은 얕은 쪽 데크 가운데이고, 수영장 길이 방향을 향한다(목표는 반대쪽 끝 수면 가운데).
  - 벽화는 `changing_room` 기계실 안의 액자(`Framed`)다. 기계실 문은 출발점에서 보이지 않는 쪽에 둔다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Pool_basin_is_tiled() { /* 수영장 바닥·벽 블록 Tile ≥ 90% */ }
[Fact] public void Water_is_5_to_8_blocks_deep() { }
[Fact] public void Lane_floats_run_the_length_of_the_pool() { /* Float 줄 2개 이상, 각 줄이 길이의 80% 이상 */ }
[Fact] public void You_can_walk_out_of_the_pool() { /* 수영장 바닥 칸에서 ReachCells로 출발점 도달 */ }
[Fact] public void Ladders_stand_at_the_edge() { }
[Fact] public void Spawn_faces_down_the_pool() { /* 10° 이내, Sight.Clear */ }
[Fact] public void Mural_hangs_framed_in_the_plant_room() { /* Framed, 기계실 안, 숨김·거리·도달 규칙 */ }
[Fact] public void Most_of_the_map_is_walkable() { }
[Fact] public void Landmark_is_one_of_two() { }
[Fact] public void Two_seeds_differ() { /* 수영장 크기·위치 또는 랜드마크가 다르고, 소품 위치 30% 이상 다름 */ }
[Fact] public void Mural_rules_hold_for_ten_seeds() { }
[Fact] public void Props_stand_on_something() { }
[Fact] public void Triangles_stay_under_budget() { }
[Fact] public void Generates_within_three_seconds() { }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현과 부품 6개. **Step 4:** 통과. 미리보기 `--scene=pool` 확인.
- [ ] **Step 5:** 커밋 `feat(core): pool scene`

---

### Task 13: 집 장면 (실제 크기 실내)

**Files:**
- Create: `client/core/World/Gen/Scenes/HouseScene.cs`
- Create: 가구 부품 `sofa.part`, `tv_stand.part`, `coffee_table.part`, `kitchen_counter.part`, `fridge.part`, `dining_table.part`, `chair.part`, `bed.part`, `wardrobe.part`, `bathtub.part`, `sink.part`, `bookshelf.part`
- Create: 랜드마크 `treehouse.part`, `greenhouse.part`, 이웃 집 겉모양 `house_shell.part`
- Modify: `Scene.cs`(`Scenes.Create(House)`)
- Test: `client/core.tests/World/Gen/Scenes/HouseSceneTests.cs`

**장면 규칙**
- **집 크기와 층.**
  - 바닥 크기 56~64 × 40~48블록, 2층과 옥상, 지하실 1칸으로 구성한다.
  - 층 높이: 바닥판 1 + 천장 높이 11 = 12블록.
  - 벽(1블록) 재료는 Brick·Concrete·Wood 중 시드로 선택한다.
- **방 배치.** 평면은 2~3가지 틀 중 하나를 시드로 선택하고, 방 크기와 좌우 반전으로 변형한다.
  - 1층: 거실, 부엌·식당, 욕실, 계단실
  - 2층: 침실 1~2, 욕실
  - 지하실: 벽화를 둘 숨은 방
- **문·창문·계단.**
  - 문 구멍은 폭 4 × 높이 9이다.
  - 바깥벽 창문은 Glass로, 바닥 위 4~8블록 높이에 둔다.
  - 계단은 한 단 1블록씩 올라가는 곧은 계단으로, 1층↔2층↔옥상과 1층↔지하실을 잇는다.
- **가구.** 방 종류마다 가구 부품 2개 이상을 벽을 따라 둔다. 크기 기준을 지킨다(탁자 3, 의자·침대 2, 선반 8).
- **소품.** 탁자 위 Cup, Plant, Lamp를 높이(Y)를 지정해 둔다.
- **마당.** 집 둘레는 풀밭 마당이다.
  - 대문에서 현관까지 길(`Path`)을 낸다.
  - `fence_segment` 울타리, 나무를 둔다.
  - 가장자리 쪽에 `house_shell` 1~2채를 겉모양으로 둔다.
- **랜드마크.** 시드로 마당의 `treehouse`(+ Tree 소품) 또는 `greenhouse`(Glass)를 선택한다.
- **출발점과 벽화.**
  - 출발점은 거실 안이고, 창문 벽을 향한다(목표는 거실 창 가운데).
  - 벽화는 지하실 숨은 방의 액자다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
[Fact] public void Rooms_have_real_ceilings() { /* 실내 바닥 칸의 천장까지 10~12블록 */ }
[Fact] public void Doors_are_4_by_9() { }
[Fact] public void Outer_windows_are_glass() { }
[Fact] public void Every_room_can_be_reached() { /* 실내 바닥 칸 ≥ 95% ReachCells */ }
[Fact] public void Upstairs_rooftop_and_basement_can_be_reached() { }
[Fact] public void Spawn_is_inside_facing_the_window() { /* 머리 위 12블록 안에 천장, 목표까지 Sight.Clear */ }
[Fact] public void Yard_is_open_to_the_sky() { /* 마당 칸 위로 블록 없음 */ }
[Fact] public void Each_room_has_furniture() { /* 방 종류마다 가구 부품 2개 이상 */ }
[Fact] public void Furniture_follows_real_sizes() { /* 탁자 높이 3, 의자·침대 2, 선반 8 */ }
[Fact] public void Mural_hangs_framed_in_the_basement() { }
[Fact] public void Most_of_the_map_is_walkable() { }
[Fact] public void Landmark_is_one_of_two() { }
[Fact] public void Two_seeds_differ() { /* 평면 틀·방 크기·가구 위치 중 30% 이상 다름 */ }
[Fact] public void Mural_rules_hold_for_ten_seeds() { }
[Fact] public void Props_stand_on_something() { }
[Fact] public void Triangles_stay_under_budget() { }
[Fact] public void Generates_within_three_seconds() { }
```

- [ ] **Step 2:** 실패 확인. **Step 3:** 구현과 부품 15개. **Step 4:** 통과. 미리보기 `--scene=house` 확인(`slice`로 층과 지하실 확인).
- [ ] **Step 5:** 커밋 `feat(core): house scene`

---

### Task 14: 게임에 연결 (질감 셰이더, `--scene=`, 소품, 벤치) + 결과 문서

**Files:**
- Create: `game/shaders/block.gdshader`, `game/src/WorldView.cs`, `game/src/PropsView.cs`, `docs/inpc/g05-v2-1-results.md`
- Modify: `game/src/WorldRoot.cs`, `game/src/Player.cs`, `game/src/PerfProbe.cs`, `scripts/game/run.ps1`, `scripts/game/bench.ps1`, `scripts/game/shots.ps1`, `docs/inpc/README.md`

**Interfaces:**
- Consumes: Task 2 `ChunkMeshes`, Task 3 `BlockPattern`, Task 6 `WorldGenerator`, `GeneratedWorld`, `Horizon`, `Scenes`
- Produces:
  - `block.gdshader`: `BlockPattern.Hash`와 `Sample`을 한 줄씩 옮긴다.
    - 입력: `COLOR`(블록색 × AO), `UV2`(재료, 상태), 세계 좌표와 법선.
    - 면 안의 8×8 픽셀 위치와 블록 좌표를 세계 좌표에서 계산한다.
    - `ALBEDO = mix(COLOR.rgb × Shade, MossColor, Moss)`
  - `WorldView`: `VoxelWorld`를 청크 노드로 만든다.
    - 불투명: 셰이더 재질 + 충돌체
    - 유리: 반투명 + 충돌체
    - 물: G0와 같다
    - G0 경로와 2판 경로가 함께 사용한다(`WorldRoot.BuildChunks`를 옮긴다).
  - `PropsView`: `PropKind`마다 둥근 도형 `MultiMeshInstance3D`를 둔다. Parasol은 기둥 + 납작한 반구, Float는 작은 구다. 인스턴스 색을 지정하고, Y는 `Prop.Y` 또는 가장 높은 블록 위에 둔다. 충돌체는 없다(G0.5 Task 6에서 정한다).
  - `WorldRoot`: `--scene=NAME`이면 2판으로 만든다.
    - 사진(`--photo`, 없으면 `SamplePhotos.Meadow()`) → 준비 → 팔레트·분위기·시드·`Horizon.Estimate` → `WorldGenerator.Generate`
    - `Map = GeneratedWorld.Surface`
    - 출발점, 방향·숙임은 `Player.SetView`로 지정한다.
    - 로그: `[world] scene= seed= tris= props= mural=` (사진 이름·경로 없음)
    - `--scene`이 없으면 G0 동작 그대로다.
  - `Player.SetView(float yawDegrees, float pitchDegrees)`
  - `PerfProbe`: 위쪽 경로 높이 = max(20m, 가장 높은 지표 + 5m)
  - 스크립트: `run.ps1`·`bench.ps1`·`shots.ps1`에 `-Scene` 매개변수(`--scene=`로 전달)

- [ ] **Step 1:** 구현한다.
- [ ] **Step 2:** `dotnet build game/Inpc.Game.sln` → 오류 0, 경고 0. `dotnet test client/core.tests` 통과.
- [ ] **Step 3:** `docs/inpc/g05-v2-1-results.md`를 만든다(`g0-results.md`와 같은 짜임).
  - 장면별 미리보기 그림 위치: 저장소에 사용자 사진으로 만든 그림은 올리지 않는다. 기본 사진(`SamplePhotos.Meadow`) 그림만 둔다.
  - 장면별 삼각형 수
  - 사용자 확인 목록(Windows):
    - 장면 4종 실행
    - 질감이 마인크래프트처럼 보이지 않는지
    - 실내 카메라가 벽에 끼지 않는지
    - 숨겨진 벽화를 찾을 수 있는지
    - `bench.ps1 -Scene mountain` 평균 30fps 이상
  - 벤치 결과 칸(빈칸)
- [ ] **Step 4:** `docs/inpc/README.md`의 "지금 어디까지 왔나"에 2판-1 상태와 Windows 확인 명령을 적는다.
  - 예: `.\scripts\game\run.ps1 -Scene beach`, `.\scripts\game\bench.ps1 -Scene mountain`
- [ ] **Step 5:** 커밋 `feat(game): v2 worlds in the game (block shader, --scene, props, bench) and results template`

**사용자 확인(Windows, 이 계획 밖에서 사용자가 한다):** 위 확인 목록. 결과는 `g05-v2-1-results.md`에 적고, 문제가 있으면 2판-2 전에 고친다.
