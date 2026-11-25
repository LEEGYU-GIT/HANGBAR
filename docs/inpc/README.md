# INPC 작업 이어가기

다른 PC에서 이어서 작업할 때 이 문서부터 읽는다. Claude에게도 "docs/inpc/README.md부터 읽고 이어서 진행해"라고 하면 된다. Claude의 기억 메모와 Obsidian 보관함은 원래 PC에만 있으므로, 필요한 맥락은 모두 이 저장소의 문서에 있다.

## 지금 어디까지 왔나 (2026-11-25)

- **HANGBAR**(바탕화면 캐릭터 메신저): M0 시험 제작과 ECC 점검 완료. 지금은 게임판을 먼저 만들기로 해서 멈춰 있다.
- **INPC**(게임판)
  - 설계 v1.2: [설계 문서](../superpowers/specs/2026-11-23-inpc-design.md). 바탕화면 2D에서 폴더 속 3D 블록 세계로 빠지는 게임이다.
  - G0 완료: [G0 결과](g0-results.md). 사진 한 장으로 블록 세계를 만들고 걷는다. 내장 그래픽 1080p 평균 47fps.
  - G0을 해 보고 방향을 바꿨다. 사진을 높이 지도로 쓰지 않고 "재료"로 쓴다. 큰 주제는 로컬 AI 모델로 알아내 바이옴 일곱 가지(들판, 도로, 물가, 숲, 도시, 실내, 교통) 중 하나를 고르고, 색과 분위기를 입히고, 사진은 벽화로 세운다.
  - G0.5: [G0.5 계획](../superpowers/plans/2026-11-24-inpc-g05-themes-and-biomes.md). 사용자가 "직접 진행" 방식으로 실행하기로 했다(Claude가 한 세션에서 작업을 차례로 하고, 끝에 새 검토자 한 명이 전체를 본다).
  - G0.5 진행(2026-11-24, 리눅스 서버): 브랜치 `inpc-g05`에서 Task 1·2·3·5를 완료하고 최종 검토 지적을 반영했다. 남은 작업은 Task 4(모델 실행기), Task 6(게임 연결), Task 7(사용자 사진으로 두 후보 비교)이다. 판단 기록은 각 커밋 메시지와 [모델 README](../../models/README.md)에 있다.
  - **세계 생성기 2판-1 완료**(2026-11-25, 브랜치 `inpc-world-v2-1`): [2판 설계](../superpowers/specs/2026-11-24-inpc-world-generator-v2-design.md), [1부 계획](../superpowers/plans/2026-11-24-inpc-world-v2-part1.md), [결과](g05-v2-1-results.md). 장면 변형 4종(해변, 수영장, 집, 산)을 사진의 색·분위기·시드로 만들고 게임에서 실행한다. 블록 질감 셰이더, 둥근 소품, 숨겨진 벽화가 들어갔다. 이 PC(내장 그래픽)에서 네 장면 모두 평균 47~60fps.
  - 2판-1 확인 명령(Windows): `.\scripts\game\run.ps1 -Scene beach`(pool, house, mountain도 같다), `.\scripts\game\bench.ps1 -Scene mountain`, 미리보기 그림은 `dotnet run --project tools/world-preview -- --scene=all --out=폴더`. 확인할 목록은 [결과 문서](g05-v2-1-results.md)의 "사용자 확인 목록"에 있다.
  - **그림체 바꿈**(2026-11-25): 사용자 피드백("마인크래프트 느낌, 인위적, 안 귀여움")으로 블록 무늬를 없앤 부드러운 배경, 흔들리는 콩 모양 협동 캐릭터, 각진 덩어리 나무로 바꿨다. 멍한 얼굴은 나중에 체력·상태 표정으로 쓴다. [결과 문서의 "그림체 바꿈"](g05-v2-1-results.md#그림체-바꿈-2026-11-25), [아이디어 노트](ideas.md). 네 장면 평균 41~46fps.
  - 다음: 사용자 확인 뒤 2판 2부(나머지 장면 15종), G0.5 남은 Task 4·6·7.
- 아이디어와 결정의 원본: [아이디어 노트](ideas.md). 새 아이디어는 날짜별로 여기에 덧붙인다.

## 사용자가 해야 할 일

- G0.5 Task 7: 시험용 사진 20~30장(풍경, 거리, 바다, 숲, 건물, 집, 카페, 가게, 지하철·버스, 음식, 화면 캡처)을 **저장소 밖** 폴더에 모은다. 저장소에는 올리지 않는다.
- G0.5 Task 7 후보 선택 때: CLIP 모델 카드가 배포 용도를 "현재 범위 밖"으로 규정한 점(라이선스는 MIT)과 Places365의 출처 표시 조건(CC BY)을 함께 판단한다. 원문은 [모델 README](../../models/README.md)에 있다.
- HANGBAR M0 쪽 남은 결정: 메모리 목표(200MB 대 실측 약 256MB), Supabase·Google 로그인 설정(이메일 가입과 익명 로그인은 끈다).

## 새 PC 준비

PowerShell에서:

```powershell
winget install Git.Git Microsoft.DotNet.SDK.8 GodotEngine.GodotEngine.Mono OpenJS.NodeJS.LTS Python.Python.3.11
git clone https://github.com/LEEGYU-GIT/HANGBAR.git
cd HANGBAR
dotnet test client/core.tests          # Core 테스트 (실패 0건이 정상)
dotnet build game/Inpc.Game.sln        # 게임 빌드
.\scripts\game\run.ps1                 # 기본 사진 세계 실행
.\scripts\game\bench.ps1               # 성능 측정 (외장 그래픽 수치를 g0-results.md에 보태면 좋다)
```

- Godot은 **4.7.2 .NET판**이어야 한다. 스크립트는 winget 설치 위치(`%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*`)에서 Godot을 찾는다. 다른 곳에 설치했다면 `scripts/game/run.ps1`의 찾는 부분을 고친다.
- 스크립트 실행이 막히면 `powershell -ExecutionPolicy Bypass -File .\scripts\game\run.ps1`.
- `dotnet`이 PATH에 없으면 `C:\Program Files\dotnet\dotnet.exe`로 부른다.
- 서버(`server/`)를 만질 때만 `cd server; npm install; npm test`.
- G0.5 모델 파일은 [모델 README](../../models/README.md)의 순서대로 준비한다. PyTorch는 필요하지 않다. `.venv`와 모델 파일은 git에 올리지 않는다.

## 지켜 온 규칙

- 사용자 사진, 폴더·파일 이름과 경로를 로그·문서·저장소에 남기지 않는다.
- 커밋은 작업 단위로 하고 GitHub(main)에 올린다. 큰 작업은 브랜치에서 하고 끝나면 main에 병합한다.
- 사용자에게 하는 답변과 문서는 한국어로 쓴다. 다른 사람이 읽을 문서는 윤문(humanize-korean) 검토를 거친다.
