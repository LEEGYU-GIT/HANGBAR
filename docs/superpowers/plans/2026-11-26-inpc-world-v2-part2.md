# INPC 세계 생성기 2판-2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 2판 생성기의 나머지 장면 변형 15종(초원, 농장, 시골길, 도심 대로, 골목, 강·호수, 숲, 공원, 시내, 주택가, 카페·식당, 가게, 사무실·교실, 지하철역, 기차역·터미널)을 만들어 사진의 어떤 장면이든 세계가 되게 한다.

**Architecture:** 1부와 같은 파이프라인(`WorldGenerator` + `IScene`)이다. 장면마다 `Gen/Scenes/<Name>Scene.cs` 하나와 필요한 부품 파일을 더한다. 장면 사이에 반복되는 것은 먼저 공용 도구로 뺀다.
- `SceneTools`: 길 찾기(산 등산로의 A*), 받침 채우기, 맨 위 블록, 울타리 두르기, 소품 흩뿌리기
- `Streets`: 차도·인도·차선·횡단보도, 겉모양 건물 줄 세우기
- `Interior`: 한 층 실내 건물 틀. 방, 문, 창문, 가구, 기계실·창고 액자 벽화를 집 장면 방식으로 만든다.

게임은 이미 `--scene=`으로 만들 수 있는 장면만 연다(`Scenes.IsBuilt`). 끝 Task에서 스크립트와 문서를 19종에 맞춘다.

**Tech Stack:** .NET 8, xUnit, Godot 4.7.2 .NET, PowerShell.

**Spec:** `docs/superpowers/specs/2026-11-24-inpc-world-generator-v2-design.md` (§6-1 장면 변형 표, §6-2 높이, §6-4 실내, §6-8 첫 화면·벽화, §6-11 실내가 아닌 장소의 건물, §9 2판-2). 1부 계획: `docs/superpowers/plans/2026-11-24-inpc-world-v2-part1.md`. 1부 이후 사용자 결정: `docs/inpc/ideas.md` 2026-11-25 항목(그림체, 턱은 점프·계단은 걷기).

## Global Constraints

- 1부 계획의 Global Constraints를 그대로 따른다(블록 0.25m, 192×192×160, 기준 지표 y=56, 바닥 2블록, 길은 높이 차 2블록 이하·머리 위 7블록, 가장자리 16칸, 걸어서 닿는 지표 70% 이상, 실내 크기, 벽화 3규칙, 랜드마크 1개 이상, 같은 사진 같은 세계, 사진 정보를 남기지 않음).
- 실내 크기: 천장 10~12블록(바닥판 1 + 천장 11), 문 4×9, 탁자 3, 의자·침대 2, 선반 8, 벽 1.
- 계단 칸(`ctx.Stairs`): 사용자 결정(2026-11-25)으로 턱은 계단·길·문턱에서만 점프 없이 오른다. 계단과 경사로는 장면이 표시하고, 길과 문 있는 구조물의 문턱은 생성기가 더한다. 건물의 층·옥상·지하는 점프 없이 닿아야 한다.
- 실내가 아닌 장소의 건물은 대부분 겉모양(속이 찬 상자, DarkGlass 창)이고, 세계마다 1~2채는 들어갈 수 있다(§6-11).
- 소품 규칙(`PropRules`): 큰 소품은 첫 시야에서 비키고, 나무 수관은 구조물에 닿지 않는다. 장면은 따로 신경 쓰지 않아도 된다.
- 삼각형 45만 개 이하, 생성 3초 이하(이 PC), 같은 장면 두 시드의 지표 높이 또는 구조물 위치가 30% 이상 다르다.
- 커밋은 Task마다, 브랜치 `inpc-world-v2-2`, 끝나면 main에 병합.

## Review Focus

1. **평지 장면의 걷기** — 시내·대로·실내·역은 평지 위 구조물이라 건물·울타리·선로가 길을 막기 쉽다. 출발점에서 70% 이상 걸어서 닿아야 한다. → 장면마다 `Most_of_the_map_is_walkable`.
2. **지하 출발** — 지하철역은 땅속 승강장에서 시작한다. 출발점이 막히지 않고, 계단으로 지상에 점프 없이 올라가야 한다. → `SubwayStationSceneTests.Street_is_reached_without_jumping`.
3. **실내 장면의 벽화** — 카페·가게·사무실은 건물 하나가 세계의 중심이라 벽화를 숨길 방이 출발점에서 40칸 이상 떨어져야 한다. → 장면마다 `Mural_rules_hold_for_ten_seeds`.
4. **넓은 물** — 강·호수는 물이 맵을 가르기 쉽다. 다리나 얕은 여울로 건너편에 닿아야 한다. → `RiverLakeSceneTests.Both_banks_are_reached`.
5. **겉모양 건물의 그리기 부담** — 시내 고층 건물은 창 블록 때문에 면이 폭증하기 쉽다. → 장면마다 `Triangles_stay_under_budget`.

---

## 파일 구조

```
client/core/World/Gen/Scenes/
  SceneTools.cs          # Task 1: 길 찾기, 받침, 맨 위, 울타리, 소품 흩뿌리기 (Mountain·Beach·Pool·House가 쓰던 것을 옮김)
  Streets.cs             # Task 1: 차도·인도·차선·횡단보도, 겉모양 건물 줄
  Interior.cs            # Task 1: 한 층 실내 건물 틀(방·문·창·가구·액자 벽화)
  MeadowScene.cs … TrainStationScene.cs   # Task 2~16
client/core/World/Parts/ # 장면마다 새 부품
client/core.tests/World/Gen/Scenes/<Name>SceneTests.cs
```

## 공통 테스트 (모든 장면 Task에 포함)

장면 테스트 파일마다 아래를 둔다(1부와 같은 `SceneKit` 도구). 장면만의 테스트는 Task마다 적는다.

```csharp
[Fact] public void Most_of_the_map_is_walkable() { /* SceneKit.WalkableShare ≥ 0.7 */ }
[Fact] public void Landmark_is_one_of_its_kinds() { /* 시드 1~12, 정한 이름 집합의 부분집합, 2종 이상 */ }
[Fact] public void Two_seeds_differ() { /* 지표 높이 또는 구조물 위치가 30% 이상 다름 */ }
[Fact] public void Mural_rules_hold_for_ten_seeds() { /* SceneKit.MuralRulesHold */ }
[Fact] public void Props_stand_on_something() { }
[Fact] public void Triangles_stay_under_budget() { }
[Fact] public void Doors_need_no_jump() { /* SceneKit.DoorsNeedNoJump */ }
[Fact] public void Spawn_faces_its_target() { /* 출발점 눈에서 목표까지 Sight.Clear */ }
// SceneTimingTests에 InlineData(Scene.X) 추가, SceneTests.Built_scenes에 X 추가
```

---

### Task 1: 공용 도구 (SceneTools, Streets, Interior)

**Files:** Create `SceneTools.cs`, `Streets.cs`, `Interior.cs`; Modify `MountainScene.cs`, `BeachScene.cs`, `PoolScene.cs`, `HouseScene.cs`(같은 동작의 사본을 공용 도구 호출로); Test `client/core.tests/World/Gen/Scenes/SceneToolsTests.cs`.

**Interfaces (Produces):**
- `SceneTools.FindPath(int[] surface, (int X,int Z) from, (int X,int Z) to, Func<int,int,bool>? blocked = null, int step = 2) : List<(int X,int Z)>?` — 산 등산로의 A*.
- `SceneTools.Support(SceneContext, BlockPos min, int w, int d, Material)`; `SceneTools.TopFree(VoxelWorld,int,int)`; `SceneTools.MaxSurface/MinSurface(ctx, LocalFrame, a0,b0,wa,wb)`.
- `SceneTools.FenceRing(ctx, LocalFrame, a0,b0,a1,b1, int y, gates: IEnumerable<(side,int from,int width)>)` — `fence_segment`을 늘여 두른다.
- `SceneTools.Scatter(ctx, Func<int,int,Material,double,Prop?> pick)` — 구조물·길·출발점 주변을 피해 소품을 흩뿌린다.
- `Streets.Road(ctx, LocalFrame, a0,b0,a1,b1, lanes, sidewalk)`: 차도(Concrete 어두움), 차선(Tile 밝음), 인도(Tile), 연석 1블록 → `ctx.Path`는 인도에만.
- `Streets.Crosswalk(...)`, `Streets.Facades(ctx, frame, along, from, to, depth, heights, part)`: 겉모양 건물 줄.
- `Interior.Build(ctx, LocalFrame, Plan plan)` — `Plan(L, D, rooms, doors, windows, furniture, muralRoom)`; 바닥판·벽·문·창·가구·`room_*`·`doorway`·`house` 기록, 액자 벽화 후보를 돌려준다.

- [ ] **Step 1:** `SceneToolsTests` 작성: `FindPath_climbs_steps_of_two_at_most`, `FindPath_goes_round_blocked_cells`, `FenceRing_leaves_its_gates_open`, `Road_marks_sidewalks_as_path`, `Interior_rooms_have_real_ceilings_and_doors`.
- [ ] **Step 2:** 실패 확인 → 구현 → 통과. 1부 장면 테스트 모두 그대로 통과(동작이 같음).
- [ ] **Step 3:** 커밋 `refactor(core): shared scene tools for part 2`.

---

### Task 2: 초원 (Meadow, 들판 기본)

- **지형:** 완만한 언덕(높이 56~70, 경사 제한 2), 작은 연못 하나, 구불구불한 흙길 고리(`Path`).
- **소품:** 꽃(Plant, 들판 색), 바위, 나무 몇 그루, 덤불.
- **구조물:** 나무 울타리 조각, 작은 헛간 겉모양(`shed_shell.part`, 들어갈 수 없음).
- **랜드마크(시드):** 풍차(`windmill.part`), 홀로 선 큰 나무, 선돌 무리(코드로 세운 돌기둥 원).
- **첫 화면:** 언덕 위 출발점에서 랜드마크 쪽 넓은 초원을 본다.
- **벽화:** 언덕 옆 바위 면, 작은 동굴 안쪽 벽.
- **장면 테스트:** `Rolling_hills_stay_gentle`(길 아닌 곳도 높이 차 3 이하인 칸 ≥ 95%), `Flowers_cover_the_grass`(Plant ≥ 60).

### Task 3: 농장 (Farm)

- **지형:** 평평한 밭 구획 4~6개(이랑: 흙 Soil 줄과 작물 Plant 소품 줄), 구획 사이 흙길(`Path`), 울타리.
- **구조물:** 들어갈 수 있는 헛간(`barn.part`, 문 4×9, 2층 다락은 계단, 다락 벽에 액자 벽화 후보), 농가 겉모양(`house_shell`), 사일로.
- **랜드마크:** 사일로(`silo.part`), 풍차, 허수아비 밭(`scarecrow.part` 여러 개).
- **첫 화면:** 농장 문(게이트)에서 헛간을 본다.
- **장면 테스트:** `Fields_have_furrows`(밭 칸 중 Soil 이랑 비율 40~60%), `Barn_loft_is_reached_without_jumping`.

### Task 4: 시골길 (CountryRoad, 도로 기본)

- **지형:** 완만한 언덕 사이로 구불구불한 2차선 길(아스팔트: Concrete 어두움, 가운데 점선), 길가 가로수 줄, 양옆 밭·풀밭.
- **구조물:** 버스 정류장(`bus_stop.part`), 길가 농가 겉모양, 돌담.
- **랜드마크:** 오래된 돌다리(개울 위), 작은 예배당 겉모양(`chapel_shell.part`), 이정표 나무.
- **첫 화면:** 길가에서 길이 멀어지는 쪽을 본다.
- **벽화:** 정류장 뒷벽, 길가 바위 절개면.
- **장면 테스트:** `Road_runs_edge_to_edge`(길이 맵 양끝 가장자리 띠에 닿음), `Road_is_gentle`(길 칸 높이 차 ≤ 1).

### Task 5: 도심 대로 (Boulevard)

- **지형:** 평지. 4~6차선 대로가 맵을 가로지르고, 교차로 하나, 횡단보도, 넓은 인도, 가로수 화단.
- **구조물:** 양옆 겉모양 건물 줄(`shopfront_shell.part`, `office_shell.part`, 높이 4~8층 = 코드로 쌓음), 신호등(`traffic_light.part`), 가로등(Lamp 소품 크게), 들어갈 수 있는 가게 1채(Interior 틀, 창고 액자 벽화).
- **랜드마크:** 시계탑, 큰 광고탑, 개선문 모양 문.
- **첫 화면:** 인도에서 대로가 멀어지는 쪽을 본다.
- **장면 테스트:** `Crosswalks_cross_the_road`, `Sidewalks_are_paths`, `Shop_can_be_entered`.

### Task 6: 골목 (Alley)

- **지형:** 낮은 언덕 비탈. 폭 3~4m(12~16블록) 골목이 갈라지고 이어지며, 높이 차는 계단(`stone_steps`, 계단 칸 표시)으로 잇는다.
- **구조물:** 낮은 집 겉모양(`house_shell` 변형, 담장), 화분, 들어갈 수 있는 작은 집 1채(Interior).
- **랜드마크:** 오래된 큰 나무가 있는 작은 마당, 우물, 골목 끝 전망대.
- **첫 화면:** 골목 입구에서 골목 안쪽을 본다.
- **장면 테스트:** `Alleys_are_narrow`(골목 칸의 폭 12~16블록), `Steps_need_no_jump`(골목 전체가 점프 없이 이어짐).

### Task 7: 강·호수 (RiverLake)

- **지형:** 시드로 강(맵을 가로지르는 굽은 물줄기, 폭 20~40) 또는 호수(가운데 큰 물). 둑은 완만하게, 깊이 3~8.
- **구조물:** 다리(`bridge.part`, 늘이기) 1~2개, 나루(`dock.part`), 갈대(Plant), 호숫가 정자.
- **랜드마크:** 물레방아(`watermill.part`), 작은 폭포(코드), 섬 위 정자.
- **첫 화면:** 둑에서 물과 다리를 본다.
- **벽화:** 다리 교대(받침) 안쪽 면, 건너편 둑의 바위 면.
- **장면 테스트:** `Both_banks_are_reached`(물 양쪽 땅 모두 점프 없이 닿음), `Water_covers_15_to_40_percent`.

### Task 8: 숲 (Forest, 숲 기본)

- **지형:** 완만한 숲 바닥, 개울 하나, 바위 언덕. 숲길(`Path`)이 맵을 지난다.
- **소품:** 빽빽한 나무(1/25), 덤불, 쓰러진 통나무(`log.part`), 버섯(Plant 작게).
- **구조물:** 야영지(텐트 `tent.part`, 모닥불).
- **랜드마크:** 거대한 나무, 오두막 폐허(`ruined_cabin.part`, MarkOld), 이끼 낀 돌탑.
- **첫 화면:** 숲길 시작에서 길을 따라 본다(소품 규칙이 시야를 비움).
- **벽화:** 바위 언덕의 협곡 벽, 동굴.
- **장면 테스트:** `Trees_are_dense`(나무 ≥ 400), `Forest_path_needs_no_jumping`.

### Task 9: 공원 (Park)

- **지형:** 완만한 언덕과 연못, 산책로 고리(`Path`, Tile/Concrete 밝음).
- **구조물:** 벤치(Bench 소품), 가로등(Lamp), 놀이터(`playground.part`), 연못 위 나무다리, 화장실 건물(`restroom.part`, 들어갈 수 있음, 뒤 창고에 액자 벽화 후보).
- **랜드마크:** 분수(`fountain.part`), 정자(`gazebo.part`), 동상(코드).
- **첫 화면:** 공원 입구에서 분수/랜드마크를 본다.
- **장면 테스트:** `Walkways_loop`(산책로가 끊기지 않은 고리), `Pond_has_a_bridge`.

### Task 10: 시내 (Downtown, 도시 기본)

- **지형:** 평지. 큰 길 격자(Streets), 가운데 광장.
- **구조물:** 고층 건물 겉모양(코드로 층을 쌓고 창 띠는 DarkGlass 줄; 높이 30~90블록), 들어갈 수 있는 로비 건물 1채(Interior, 액자 벽화).
- **랜드마크:** 유리 탑, 광장 시계탑, 큰 조형물.
- **첫 화면:** 광장 가장자리에서 랜드마크를 본다.
- **장면 테스트:** `Towers_are_tall`(가장 높은 건물 ≥ 60블록), `Plaza_is_open`(광장 칸 위가 비어 있음).

### Task 11: 주택가 (Residential)

- **지형:** 완만한 평지, 동네 길 격자(2차선, 인도).
- **구조물:** 마당 있는 집 겉모양 줄(`house_shell`, 울타리, 우편함 소품), 들어갈 수 있는 집 1채(Interior 한 층, 차고에 액자 벽화 후보), 놀이터.
- **랜드마크:** 동네 놀이터, 큰 나무, 급수탑.
- **첫 화면:** 길에서 집 줄을 본다.
- **장면 테스트:** `Every_house_has_a_yard`, `House_can_be_entered`.

### Task 12: 카페·식당 (Cafe)

- **실내(Interior):** 한 층 건물 40~56 × 32~40. 손님 홀(탁자 3 + 의자 2, 2~4인석), 계산대(`counter.part`), 주방(조리대·냉장고), 화장실, 창고(액자 벽화). 바깥은 테라스와 길.
- **랜드마크:** 큰 창이 있는 홀, 간판, 테라스 파라솔.
- **첫 화면:** 홀 안에서 계산대를 본다.
- **장면 테스트:** `Tables_have_chairs`, `Every_room_can_be_reached`(실내 바닥 칸 ≥ 95%), `Rooms_have_real_ceilings`.

### Task 13: 가게 (Shop)

- **실내:** 한 층 48~64 × 36~44. 진열대 줄(`shop_shelf.part`, 높이 8), 계산대 2개, 냉장 진열장(`fridge` 변형), 입구, 창고(액자 벽화).
- **첫 화면:** 입구 안쪽에서 진열대 사이 통로를 본다.
- **장면 테스트:** `Aisles_are_wide_enough`(진열대 사이 ≥ 8블록), `Shelves_are_two_metres`.

### Task 14: 사무실·교실 (Office)

- **실내:** 시드로 사무실(책상 줄 `desk.part`, 회의실, 탕비실) 또는 교실(책걸상 줄, 칠판 `blackboard.part`, 교탁). 두 층 + 옥상, 계단은 집 장면처럼. 문서 보관실(액자 벽화).
- **첫 화면:** 방 뒤쪽에서 칠판·화면 쪽을 본다.
- **장면 테스트:** `Desks_stand_in_rows`, `Every_floor_can_be_reached_without_jumping`.

### Task 15: 지하철역 (SubwayStation, 교통 기본)

- **지하:** 지표 아래 승강장 층(천장 높이 16블록), 선로 2줄(`rail` 블록 Metal), 섬식 또는 상대식 승강장, 기둥, 전동차(`subway_car.part`, 겉모양) 1대, 개찰구(`turnstile.part`), 계단과 경사로로 지상 출구 2곳. 지상은 짧은 거리와 출입구 지붕.
- **첫 화면:** 승강장에서 선로와 전동차를 본다(땅속 시작, §6-8).
- **벽화:** 승강장 끝 관리실 액자, 터널 옆 대피 통로 벽.
- **장면 테스트:** `Street_is_reached_without_jumping`, `Platform_has_tracks_on_its_side`, `Spawn_is_underground`.

### Task 16: 기차역·터미널 (TrainStation)

- **지형:** 평지. 선로 3~4줄, 지상 승강장 2개, 승강장 지붕(`platform_canopy.part`), 승강장을 잇는 육교(계단), 역사(대합실, 들어갈 수 있음, 역무실 액자 벽화), 버스 승강장.
- **첫 화면:** 승강장에서 선로가 멀어지는 쪽을 본다.
- **장면 테스트:** `Platforms_are_linked_by_the_footbridge`(점프 없이), `Waiting_hall_can_be_entered`.

### Task 17: 게임·도구·문서 연결

- `scripts/game/run.ps1`·`bench.ps1`·`shots.ps1`의 `-Scene`을 19종으로(`ValidateSet`).
- `tools/world-preview --scene=all`로 19종 그림, 장면마다 기본 사진 미리보기 그림을 `docs/inpc/v2-2/`에 둔다.
- 게임에서 장면 19종 스크린샷과 벤치(평균 30fps 이상, 가장 무거운 3종).
- `docs/inpc/g05-v2-2-results.md`, README "지금 어디까지 왔나".
- 커밋 `feat(game): all nineteen scenes in the game; part 2 results`.
