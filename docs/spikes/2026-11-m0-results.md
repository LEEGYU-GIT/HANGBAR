# M0 시험 제작 결과

- 계획: `docs/superpowers/plans/2026-11-22-m0-spike.md`
- 설계: `docs/superpowers/specs/2026-11-22-hangbar-design.md` §11
- 판정: 통과 / 조건부 / 실패 / **사용자 확인 대기**(사람이 화면·다른 PC·게임으로 확인해야 하는 항목)

## 환경

| 항목 | 값 |
|---|---|
| Godot | 4.7.2 stable mono (official ed1daf0bf) |
| .NET SDK | 8.0.428 |
| 렌더러 | Compatibility, OpenGL 3.3 |
| 개발 PC | Intel Iris Xe, 모니터 1대 1920×1080 배율 125% |

## 항목별 결과

| # | 위험 | 판정 | 근거 | 설계 영향 |
|---|---|---|---|---|
| 1 | 투명 오버레이·클릭 통과·전체 화면 숨김 | 진행 중 | | |
| 2 | 바탕화면 아이콘 읽기 | 대기 | | |
| 3 | 한글 입력 | 대기 | | |
| 4 | 두 PC 동기화 | 대기 | | |
| 5 | MSIX 포장 | 대기 | | |
| 6 | 자원 사용 | 대기 | | |
| 7 | 창 지형 | 대기 | | |

## 기록

### 항목 5 — MSIX 포장 (Task 12)

- `packaging/build-msix.ps1`이 Godot 내보내기 → MSIX 포장(MakeAppx 10.0.22621) → 테스트 인증서(CN=HangbarSpike) 서명까지 한 번에 한다. 결과 `Hangbar_0.0.1.0_x64.msix` 71MB.
- 매니페스트: 전체 신뢰(`runFullTrust`), `hangbar` 프로토콜, 시작 프로그램 작업(기본 꺼짐).
- 내보낸 `Hangbar.exe`를 MSIX 밖에서 `hangbar://join/fromexport` 인자로 실행하면 HUD에 `join fromexport`가 뜬다. 오버레이, 작업표시줄 바닥도 개발 실행과 같다. 메모리 289MB.
- 함정 2개: ① Godot .NET 내보내기는 프로젝트 폴더에 `<어셈블리 이름>.sln`(`Hangbar.App.sln`)이 있어야 한다. ② PowerShell `-replace`는 대소문자를 가리지 않아서 XML 선언의 `version="1.0"`까지 바꿨다. `-creplace`로 `Identity` 줄만 바꾼다.
- 내보내기 템플릿(4.7.2 mono, 1.2GB)은 공식 SHA512와 일치함을 확인한 뒤 Windows x64 파일만 풀었다.
- **사용자 확인 대기**(관리자 권한 필요): ① `packaging/out/hangbar-test.cer`를 "로컬 컴퓨터 → 신뢰할 수 있는 사람" 저장소에 넣기 ② MSIX 더블클릭 설치 → 시작 메뉴에서 실행 ③ MSIX 안에서도 아이콘 읽기, 클릭 통과, 로그인 루프백이 같은지 ④ 브라우저 주소창에 `hangbar://join/abc123` → 앱이 열리고 HUD에 `abc123` ⑤ Windows App Certification Kit 검사 결과.

### 항목 4 — 동기화 (Task 9, 11)

- 중계 서버(Worker + 방 Durable Object)는 Vitest 6개 통과: 토큰 없음·다른 키·만료 → 401, 잘못된 방 이름 → 400, 보낸 사람 빼고 전달, ping은 보낸 사람에게만.
- 이 PC에서 `wrangler dev`로 띄운 로컬 중계에 앱(사용자 u-app)과 Node 상대(u-peer)를 붙였다. 상대가 보낸 폴짝·채팅이 앱에 도착해 상대 인형이 생기고 말풍선 "안녕! 피어에서 보냄"이 떴다(캡처 확인). 왕복 시간 로컬 p50 2~4ms.
- **찾은 결함과 수정**: 서버가 응답 없이 죽으면 앱이 "연결 중"에서 영원히 멈췄다. 연결 시도 10초 제한, ping 응답 6초 없으면 끊고 재연결을 넣었다. 수정 후 중계를 내렸다 8초 뒤 다시 띄우면 "재연결 대기 → 연결됨"으로 스스로 돌아온다.
- 로컬 시험 방법: `server/.dev.vars`(git 제외)에 테스트 공개키, `node scripts/dev-token.ts <id>`로 개발용 토큰, 앱은 `HANGBAR_RELAY_URL=ws://127.0.0.1:8787` + `HANGBAR_DEV_TOKEN`, 상대는 `node scripts/peer.mjs ws://127.0.0.1:8787 m0-test <token> <초>`.
- **사용자 확인 대기**: Cloudflare 배포(`npx wrangler login` → `npm run deploy`, `wrangler.jsonc`의 `SUPABASE_URL` 교체) 후 PC 두 대(가능하면 다른 네트워크)에서 10분 동안 폴짝·채팅, 실제 왕복 시간(목표 왕복 400ms 안팎), Cloudflare 대시보드의 10분간 요청 수로 사용자당 하루 요청 수 추정, 와이파이를 끊었다 켜서 재연결.

### 로그인 (Task 10)

- PKCE(S256)와 루프백 콜백 해석은 Core 테스트로 확인(RFC 7636 부록 B 값 일치).
- 콜백 수신기는 `HttpListener` 대신 127.0.0.1 TCP 수신기로 만들었다(`HttpListener`는 127.0.0.1에 URL 예약이 필요할 수 있음).
- 자체 시험(Supabase 없이): 53682를 다른 프로그램이 쓰고 있으면 53683으로 넘어간다. 콜백이 아닌 요청은 404, 콜백은 200 + 안내 문구. 로그인 거부(`error=access_denied`)는 "로그인 실패"로 처리. 토큰 교환 네트워크 오류도 예외 없이 실패로 처리.
- supabase-csharp는 쓰지 않고 `HttpClient`로 `/auth/v1/token?grant_type=pkce`를 직접 부른다(의존성 하나 줄이고, 필요한 호출이 둘뿐).
- **사용자 준비 필요**: ① Supabase 프로젝트 생성 ② Google Cloud에서 OAuth 클라이언트를 만들어 Supabase의 Google 공급자에 등록 ③ Supabase 리디렉트 허용 목록에 `http://127.0.0.1:53682/callback`, `…53683/callback`, `…53684/callback` ④ `client/app/hangbar.local.example.json`을 `hangbar.local.json`으로 복사해 URL과 publishable key 입력. 그 뒤 채팅 창의 "Google 로그인"으로 실제 로그인을 확인한다.

### 항목 3 — 한글 입력 (Task 8)

- 일반 창(투명 아님) 채팅 창이 뜨고, 맑은 고딕(`SystemFont`)으로 한글이 깨지지 않고 그려진다(캡처로 확인).
- 입력은 500자로 막는다(`LineEdit.MaxLength`).
- 시험용 디버그 표시(아이콘 사각형)가 항상 위 오버레이에 그려져 채팅 창 위에 겹친다. 본 개발에서는 디버그 표시가 없으므로 문제 아님.
- **사용자 확인 대기**(사람이 MS 한글 입력기로 쳐야 함): "안녕하세요 반가워요 ㅋㅋ" 조합 중 글자가 입력창 안에 바로 보이는지, 조합 중 Enter가 마지막 글자를 잃지 않는지, 한/영 전환, 백스페이스 자모 단위 지우기.

### 항목 7 — 창 지형 (Task 7)

- `EnumWindows` + `DWMWA_EXTENDED_FRAME_BOUNDS`로 보이는 창을 Z 순서대로 읽고, 앞 창에 가려진 구간을 잘라 위 가장자리 발판을 만든다(최소화·클로킹·도구 창·우리 창·바탕화면·작업표시줄 제외, 제목은 읽지 않음).
- 시험용 창(700×400)을 띄우자 그 위 가장자리에 노란 발판이 생기고 인형이 올라탔다. 창을 250px 옮기자 인형이 함께 옮겨졌고, 최소화하자 인형이 떨어졌다(자동 확인 + 캡처).
- CPU: 창 목록 1초·탄 창 200ms 조회를 켠 상태 0.4~0.9%, 끈 상태 0.5~1.0%. **차이 없음**(측정 오차 안).
- 작업표시줄 바닥: 주 모니터는 `SHAppBarMessage`, 보조 모니터는 `Shell_SecondaryTrayWnd`. 걷는 인형이 작업표시줄 위에 선다.
- **사용자 확인 대기**: 창 10개 이상일 때 노란 선이 보이는 가장자리와 맞는지, 보조 모니터 작업표시줄의 파란 선.

### 항목 2 — 바탕화면 아이콘 (Task 6)

- C#에서 Shell COM(`IShellWindows.FindWindowSW` → `IShellBrowser.QueryActiveShellView` → `IFolderView`)으로 바탕화면 항목 위치를 읽었다. 전용 STA 스레드, 이름은 요청하지 않음.
- 개발 PC 결과: 폴더 5개, 그 밖 19개. 아이콘 칸 크기(`GetSpacing`) 95×111 = 125% 배율의 기본 간격 76×89와 맞다. 첫 항목 위치 (23, 2).
- 실패는 예외 대신 HRESULT가 담긴 빈 결과로 돌아오고, 앱은 그대로 돈다(코드 경로, 단위 테스트로 확인).
- **사용자 확인 대기**: 초록(폴더)·회색(그 밖) 사각형이 실제 아이콘과 겹치는지(바탕화면이 보이게 하고 확인), 아이콘을 끌어 옮기면 2초 안에 따라오는지, 작업 관리자에서 탐색기를 다시 시작해도 앱이 죽지 않는지, **회사 PC 보안 프로그램 반응**.

### 항목 1 — 오버레이 (Task 3)

- 모니터 1대에서 1920×1079 투명 창이 뜨고, 막대 인형과 HUD가 그려진다(자동 캡처로 확인, 2026-11-23).
- 창 확장 스타일 `0x8000098` = 항상 위 + 도구 창 + 활성화 안 함. 작업표시줄 버튼 없음.
- 1×1 메인 창도 작업표시줄 버튼이 생겨서 같은 스타일을 넣었다.
- HUD 첫 값: fps 30, CPU 0.9%, 메모리 293MB. **메모리가 목표 200MB를 넘는다**(Godot .NET 런타임 기본 사용량으로 보임, 항목 6에서 다시 잰다).
- 함정: 자동 캡처 스크립트가 DPI 미인식 프로세스라 엉뚱한 영역을 찍었다. `SetProcessDPIAware` 뒤에 캡처해야 물리 좌표와 맞는다.
- **사용자 확인 대기**: Alt+Tab에 안 나오는지, Windows 알림이 정상으로 뜨는지, 모니터를 뽑고 꽂거나 해상도를 바꿀 때 2초 안에 다시 맞춰지는지, 모니터 2대일 때 HUD 값.

### 항목 1 — 클릭 통과 (Task 4)

- **Godot의 `Window.MousePassthrough`만으로는 다른 앱에 클릭이 가지 않는다.** 다른 프로세스에서 `WindowFromPoint(500,500)`을 조회하면 Godot 창이 나왔다(같은 프로세스 안에서만 통과하는 방식으로 보인다). 설계 §6-2 그대로 `WS_EX_LAYERED`(알파 255) + `WS_EX_TRANSPARENT` 전환으로 바꿨다.
- 바꾼 뒤: 같은 지점의 주인이 뒤의 VS Code 창(`Chrome_WidgetWin_1`)으로 나온다. 투명 그리기는 그대로다.
- 커서를 인형이 걷는 높이로 가로질러 움직이면 인형 위에서만 `WS_EX_TRANSPARENT`가 꺼졌다가(0x80800B8 → 0x8080098) 지나가면 다시 켜진다.
- **사용자 확인 대기**: 인형을 눌렀을 때 폴짝하는지(활성화 안 함 창이 클릭을 받는지), 커서를 빠르게 지나가도 아래 창 클릭이 막히는 일이 없는지.

### 항목 1 — 전체 화면 숨김 (Task 5)

- 우리 오버레이만 떠 있는 동안 전체 화면 판정이 "숨김"을 내지 않는다(1px 축소 효과, 자기 자신 오인 없음).
- 테두리 없는 창이 주 모니터를 덮고 앞으로 오면 1초 안에 `hidden=True`, 닫으면 `hidden=False`(시험용 WinForms 창으로 자동 확인).
- 숨김 동안 최대 fps를 30 → 5로 낮추고 클릭 통과 조회를 멈춘다.
- **사용자 확인 대기**: 독점 전체 화면 게임, 테두리 없는 창 모드 게임, 브라우저 영상 전체 화면(F11), PowerPoint 발표.
