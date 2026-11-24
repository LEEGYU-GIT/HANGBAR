# world-preview

v2 세계 생성기로 만든 세계를 그림으로 확인하는 개발 도구다. 게임에는 들어가지 않는다.

```
dotnet run --project tools/world-preview -- --scene=mountain|beach|pool|house|all [--photo=사진.ppm] [--seed=N] --out=폴더
```

- 장면마다 그림 4장을 만든다.
  - `<scene>-iso.png`: 비스듬히 위에서 본 전체
  - `<scene>-top.png`: 위에서 본 그림. 빨강은 출발점, 노랑은 랜드마크, 자홍은 벽화 위치다
  - `<scene>-slice.png`: 출발점을 지나는 x 방향 세로 단면(지층, 동굴, 지하수)
  - `<scene>-close.png`: 출발점 주변 48×48칸의 윗면 질감(블록 무늬와 손상 상태)
- 한 줄 통계를 출력한다: `scene= seed= tris= walk=% water=% mural=(x,y,z 방향 폭x높이) landmark=`
- 사진이 없으면 내장 사진(초원)을 쓴다. 사진은 이진 PPM(P6)만 받는다. 사진 이름과 경로는 출력하지 않는다.
- `--out`이 사진과 같은 폴더면 거절한다. 개인 사진으로 만든 그림은 저장소에 올리지 않는다.
- 아직 만들지 않은 장면은 건너뛰고 그 사실을 출력한다.
