# M0 ECC 추가 점검 (2026-11-23)

M0 최종 검토가 끝난 뒤 ECC(everything-claude-code 2.2.3)의 검토 에이전트 4종으로 한 번 더 봤다. 사용한 에이전트는 csharp-reviewer, typescript-reviewer, security-reviewer, silent-failure-hunter다. 이미 미뤄 둔 경미 항목(M0 ledger)은 다시 보고하지 않게 했다.

ECC는 이 프로젝트에만(local scope) 설치했고 훅은 꺼 두었다(`hooks_enabled=false`).

## 고친 것

| 지적 | 등급 | 수정 | 확인 |
|---|---|---|---|
| 토큰 갱신이 네트워크·서버 문제로 실패해도 "다시 로그인 필요"로 영구 정지 | 높음 | 갱신 결과를 성공 / 다시 로그인(400·401·403) / 나중에 재시도로 나눔. 재시도는 백오프 후 다시 요청 | `Transient_token_failure_backs_off_and_asks_again`, `Refresh_rejection_is_only_400_401_403` RED→GREEN |
| 토큰 요청이 예외를 던지면 재연결 루프가 소리 없이 종료 | 높음 | 예외를 재시도로 처리하고 `LastError`에 남김 | `Provider_exception_is_retried_not_fatal` RED→GREEN |
| Supabase 키(JWKS)를 못 받아 와도 401 → 클라이언트가 포기 | 높음 | 토큰 잘못(401)과 키 조회 실패(503 + 로그)를 구분 | `server/test/auth.test.ts` RED→GREEN |
| 브로드캐스트 중 한 연결이 오류를 내면 나머지 멤버가 이벤트를 못 받음 | 중간 | 연결마다 try/catch | `server/test/relay.test.ts` RED→GREEN |
| 가짜 `/callback` 하나로 로그인이 끝남 | 중간 | 세션을 못 만든 콜백은 실패 페이지만 주고 진짜 콜백을 계속 기다림 | `scripts/spike/loopback-bogus-check.ps1` RED(두 번째 콜백 거절)→GREEN(둘 다 200) |
| 갱신 응답 본문이 예상과 다르면 예외가 그대로 터짐 | 중간 | 로그를 남기고 재시도 가능한 실패로 반환 | 코드 확인 |
| 보내기가 실패해도 신호 없음 | 중간 | `SendAsync`가 `bool`을 돌려주고 예외를 던지지 않음 | `Send_while_not_connected_reports_false` |
| 연결 실패 원인이 안 보임(잘못된 URL도 "재연결 대기") | 중간 | `LastError`에 HTTP 상태나 예외 종류 기록 | 코드 확인 |
| 방 멤버가 보낸 이상한 JSON이 그 프레임 처리를 멈춤 | 중간 | 시험 코드(RoomSpike)에서 형식 확인, 채팅 500자 자름 | 코드 확인(시험 코드라 테스트 없음) |
| 사각형 오른쪽·아래 바로 바깥 1px의 거리가 0 | 경미 | 바깥 점은 최소 1 | `DistanceTo_is_never_zero_just_past_the_exclusive_edges` RED→GREEN |
| 지연 통계를 두 스레드가 잠금 없이 사용 | 경미 | lock | 코드 확인 |
| 로그인 오류 로그에 예외 종류만 남음 | 경미 | 메시지도 남김 | 코드 확인 |
| 공개 저장소의 스크립트에 사용자 폴더 경로가 박혀 있음 | 경미 | `$env:LOCALAPPDATA`, 스크립트 기준 상대 경로로 바꿈 | `git grep` 0건 |

테스트하려고 `RoomClient`를 앱에서 Core(`Hangbar.Core.Net`)로 옮겼다. Godot 의존이 없는 코드다.

결과: Core 66/66, 서버 15/15, tsc 깨끗, 앱 빌드 오류 0.

## 판단한 것

- 가짜 콜백 대책으로 `state` 값을 확인하는 대신 "계속 기다리기"를 골랐다. Supabase가 우리 쪽 `state`를 돌려주는지 확인되지 않았기 때문이다. 대가로, 사용자가 Google 화면에서 진짜로 취소해도 3분 제한이 끝날 때까지 기다린다. M1에서 콜백 경로에 일회용 값을 넣는 방법을 검토한다.
- 예전 커밋 기록에는 사용자 폴더 경로가 남아 있다. 기록을 다시 쓰지는 않았다.

## M1으로 넘긴 것

- 방마다 메시지 속도 제한과 사용자별 연결 수 제한(멤버 확인·8명 제한과 함께). 클라이언트 수신 큐 상한도 같이 넣는다.
- 같은 사용자가 재연결하면 예전 소켓을 닫기(지금은 이벤트가 중복 전달될 수 있음).
- 배포 빌드에서 `wss://`·`https://`만 허용.
- 릴레이에서도 메시지 형식 확인, 버린 메시지는 보낸 사람에게 알리거나 로그 남기기.
- `hangbar://join` 코드를 실제로 쓸 때 형식 제한과 참가 확인.
- 시험 인증서 비밀번호를 환경변수로, 시범 운영 전 정식 서명.
- JWT 알고리즘 허용 목록(Supabase 키 종류 확인 후).
- **Supabase 설정 시 이메일 가입·익명 로그인을 꺼 둘 것**(지금 서버는 Supabase 사용자라면 누구든 받음).
- Win32 호출 결과 무시(아이콘 읽기 HRESULT, 창 스타일 변경, 모니터 정보 실패 시 전체 화면 판정), 늦게 온 콜백 소켓 정리, `RoomClient` 종료 경쟁, 시험 코드의 "Fallen" 인형 누수, 서버 테스트 빈틈(크기 초과·잘못된 JSON·`from` 덮어쓰기), `typecheck` 스크립트.
