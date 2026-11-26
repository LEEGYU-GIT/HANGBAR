# 사진 주제 인식 모델 (G0.5 후보)

이 폴더에는 사진의 큰 주제(바이옴)를 알아내는 모델 파일을 둔다(설계 §6-3-1).

- 모델 파일은 git에 올리지 않는다(`.gitignore`). 저장소에는 이 README만 포함한다.
- 파일은 아래 순서대로 내려받거나 생성한다. 모든 단계는 CPU에서 실행되고 PyTorch는 필요하지 않다.

## 준비 순서

저장소 폴더(HANGBAR)에서 실행한다. Python 3.10 이상이 필요하다. 스크립트는 반드시 가상환경(`.venv`)의 Python으로 실행한다.

Windows(PowerShell):

```
python -m venv .venv
.venv\Scripts\python -m pip install -r tools/theme-probe/requirements.txt
.venv\Scripts\python tools/theme-probe/fetch_models.py          # 내려받기와 SHA-256 확인
.venv\Scripts\python tools/theme-probe/convert_places365.py     # Places365 체크포인트 → ONNX, 라벨 파일
.venv\Scripts\python tools/theme-probe/make_text_embeddings.py  # CLIP 바이옴·장면 변형 문장 임베딩
```

Linux:

```
python3 -m venv .venv
.venv/bin/python -m pip install -r tools/theme-probe/requirements.txt
.venv/bin/python tools/theme-probe/fetch_models.py
.venv/bin/python tools/theme-probe/convert_places365.py
.venv/bin/python tools/theme-probe/make_text_embeddings.py
```

- `fetch_models.py`는 해시가 다른 파일을 삭제하고 실패로 종료한다. 이미 받은 파일은 해시만 확인한다.
- 세 스크립트 모두 `--models 폴더`로 다른 폴더를 지정할 수 있다.
- 아래 해시는 2026-11-24 이 저장소를 처음 준비한 PC에서 생성한 값이다. 같은 PC의 새 폴더에서 다시 생성해도 같았다.
  - `places365.onnx`와 `places365-labels.txt`는 계산 없이 값만 옮겨 만들므로, `requirements.txt`의 패키지 판이 같으면 어느 PC에서나 같아야 한다.
  - `clip-text-biomes.json`은 ONNX Runtime 계산 결과라 CPU에 따라 마지막 자리가 달라져 해시가 다를 수 있다.

## 게임이 사용하는 파일

| 파일 | 크기(바이트) | 입력 | 출력 |
|---|---|---|---|
| `places365.onnx` | 45,502,922 | `input` float [1,3,224,224] | `logits` float [1,365] |
| `places365-labels.txt` | 5,484 | | 365줄, 클래스 번호 순서의 라벨(`/a/airfield`) |
| `clip-vision.onnx` | 88,648,915 | `pixel_values` float [N,3,224,224] | `image_embeds` float [N,512] |
| `clip-text-biomes.json` | 37,323 | | 바이옴 7개 × 512개 실수 |
| `clip-text-scenes.json` | 101,357 | | 장면 변형 19개 × 512개 실수(Scene enum 순서). CLIP 실행기가 쓴다 |

SHA-256:

- `places365.onnx`: `361d1f595da8326e63493ee3a57614c340fd5118c5a6714bc85afbad965e32af`
- `places365-labels.txt`: `16bbf645b9098261a1dcf7f5430205eab51eb74a4794675648bdce9b9e37c4ea`
- `clip-vision.onnx`: `b95448754a6ae56964ec80c570c80bb9863787ee85f51b664abed8c0e5f22a7a`
- `clip-text-biomes.json`: `a0ad4d26bdad129254e5d092696d212f22ebf17dc2a90a93a7883bb1d8143ae6`
- `clip-text-scenes.json`: `d9faab4e45c27c609ccdd7706472d724c4792f142d16b20b25ad6aa00c46acac` (2026-11-26, Windows PC. 바이옴 파일처럼 CPU에 따라 다를 수 있다)

실행기와 비교 도구(G0.5 Task 4): `client/theme`의 `PlacesModel`, `ClipModel`(ONNX Runtime, CPU). 둘 다 바이옴과 장면 변형을 낸다. 사진 폴더로 두 후보를 비교하려면 `.\scripts\game	heme-probe.ps1 -Photos "폴더"`를 쓴다. 하위 폴더 이름을 장면 이름(beach, pool, house, mountain, cafe 등)으로 하면 정확도도 나온다.

입력 처리(G0.5 Task 4 실행기가 따를 값):

- 두 모델 모두 RGB 0~1 값을 채널 우선(CHW)으로 입력한다.
- Places365: 평균 (0.485, 0.456, 0.406), 표준편차 (0.229, 0.224, 0.225). 출력은 소프트맥스 전 값이다.
- CLIP: 평균 (0.4815, 0.4578, 0.4082), 표준편차 (0.2686, 0.2613, 0.2758). 출력 `image_embeds`는 정규화되지 않은 값이다. L2 정규화한 뒤 `clip-text-biomes.json`의 바이옴 벡터와 내적하면 코사인 유사도가 된다.

## 생성에만 사용하는 파일 (`download/`)

| 파일 | 크기(바이트) | 출처 | SHA-256 |
|---|---|---|---|
| `resnet18_places365.pth.tar` | 45,506,139 | MIT CSAIL 서버(http만 제공) | `2f4759217d470da2b803f8f66cd4488a066406b555a5fb95ee9a4663f9f05588` |
| `categories_places365.txt` | 6,833 | `CSAILVision/places365` 커밋 `8a953ed` | `2affba635eb657e7ca95f4e6cc69bd9fac29ef4c32aeb83cafdfcd06ec6a1ea6` |
| `clip-text-model.onnx` | 254,058,553 | `Xenova/clip-vit-base-patch32` 커밋 `6b7c523`, `onnx/text_model.onnx` | `3f6571f5bad13a97c469c1622e1cfc4d9aef78b79fdbfcff804ca357bfada8cc` |
| `clip-tokenizer.json` | 2,224,119 | 같은 커밋, `tokenizer.json` | `f7f3b7af117d467b58374797691a6438d3e6b9e9cef800dfd5dced7f697a90cd` |

- Places365 가중치는 http로만 내려받을 수 있다. 그래서 2026-11-24 첫 내려받기의 해시를 기록하고 이후에는 그 해시로 확인한다.
- Hugging Face 파일의 해시는 저장소가 공개한 값(LFS)과 같다.

## 후보 A: Places365 ResNet18 (장면 분류)

사진을 장면 365종 중 하나로 분류하는 모델이다. MIT CSAIL이 공개했다(https://github.com/CSAILVision/places365).

**라이선스 원문**

- 모델 가중치(저장소 README "Acknowledgements and License"):
  > The pretrained places-CNN models can be used under the Creative Common License (Attribution CC BY). Please give appropriate credit, such as providing a link to our paper or to the [Places Project Page](http://places2.csail.mit.edu). The copyright of all the images belongs to the image owners.
- 코드(저장소 `LICENSE`): MIT License, "Copyright (c) 2017 Bolei Zhou and MIT CSAIL Computer Vision".

**상업 게임 사용: 가능(출처 표시 조건).** CC BY는 상업 사용을 허용하고 출처 표시를 요구한다. 게임에 포함하면 크레딧 화면에 논문이나 Places 프로젝트 페이지를 표시한다. 학습 데이터(Places 데이터베이스)의 이용 조건은 모델과 별개이며 모델에는 위 문구가 적용된다.

**변환 방법.** 계획은 `torch.onnx.export`였으나 PyTorch 없이 변환했다.

- 이유: 게임에는 결과 그래프만 필요하다. 변환하는 PC에 PyTorch(수백 MB)를 설치하지 않아도 된다.
- `convert_places365.py`는 체크포인트(PyTorch 0.2 형식)를 직접 읽는 변환기다. 텐서 복원 외의 클래스는 거부하므로 파일 안의 코드는 실행되지 않는다.
- 같은 구조(torchvision ResNet18, 365종)를 ONNX opset 17 그래프로 작성한다. 체크포인트의 가중치를 모두 사용하지 않으면 실패한다.

**검증.** MIT 예시 사진(`demo/12.jpg`, 원래 주소가 사라져 인터넷 보관소 사본 사용)의 결과를 저장소 README에 공개된 결과와 비교했다. 공개 결과는 2017년 12월 이전 예시 코드의 전처리(짧은 변 256, 가운데 224)로 만든 값이어서 같은 전처리로 비교했다.

| 순위 | 공개 결과 | 변환본 |
|---|---|---|
| 1 | patio 0.519 | patio 0.524 |
| 2 | restaurant_patio 0.394 | restaurant_patio 0.389 |
| 3 | beer_garden 0.018 | beer_garden 0.018 |
| 4 | diner/outdoor 0.017 | diner/outdoor 0.017 |
| 5 | courtyard 0.016 | courtyard 0.016 |

5순위까지 같고 확률 차이는 0.005 이하다. 남은 차이는 JPEG 해독과 축소 방식의 차이로 추정한다.

## 후보 B: OpenAI CLIP ViT-B/32 (이미지-글 대응)

사진과 문장이 얼마나 닮았는지 계산하는 모델이다. Hugging Face의 ONNX 변환본 `Xenova/clip-vit-base-patch32`(커밋 `6b7c523703ae26e39be4b03774966f8571c7f523`)를 사용한다.

**라이선스 원문**

- OpenAI CLIP 저장소 `LICENSE`: MIT License, "Copyright (c) 2021 OpenAI".
- Xenova 저장소 모델 카드: `license: mit`, `base_model: openai/clip-vit-base-patch32`.
- OpenAI 모델 카드(`model-card.md`) "Out-of-Scope Use Cases":
  > **Any** deployed use case of the model - whether commercial or not - is currently out of scope. Non-deployed use cases such as image search in a constrained environment, are also not recommended unless there is thorough in-domain testing of the model with a specific, fixed class taxonomy.

**상업 게임 사용: 라이선스상 가능, 사용자 판단 필요.** MIT 라이선스는 상업 사용을 허용한다. 다만 OpenAI 모델 카드는 상업용과 비상업용을 가리지 않고 배포 용도 전체를 "현재 범위 밖"으로 규정한다. 라이선스 조건이 아니라 권고다. G0.5 Task 7에서 후보를 선택할 때 함께 판단한다.

**파일 선택.**

- 비전 인코더 원본(fp32)은 351,685,709바이트로 100MB를 넘는다. 그래서 사용하는 파일은 8비트 양자화본이다.
- 계획의 int8 파일(`vision_model_int8.onnx`)은 ONNX Runtime 1.23.2 CPU에서 열리지 않는다(`ConvInteger` int8 미구현). 같은 8비트인 uint8 파일(`vision_model_uint8.onnx`)을 `clip-vision.onnx`로 저장한다.
- 100MB 이하 변환본 3종을 fp32와 비교했다(시험 그림 5장: MIT 예시 사진, G0 기본 사진과 화면, 절경 시험 화면 2장).

  | 변환본 | 크기(바이트) | fp32와 코사인 유사도(최소/평균) | 바이옴 1순위가 fp32와 같은 그림 |
  |---|---|---|---|
  | uint8 (선택) | 88,648,915 | 0.982 / 0.987 | 5장 중 5장 |
  | quantized | 89,117,001 | 0.942 / 0.961 | 5장 중 5장 |
  | q4 | 63,642,858 | 0.965 / 0.976 | 5장 중 4장 |

**텍스트 임베딩.** 바이옴마다 영어 문장 3개의 임베딩을 L2 정규화해 평균하고 다시 L2 정규화한다. 문장은 `make_text_embeddings.py`의 `PROMPTS`에 있다. 텍스트 모델은 이 단계에서만 사용하고 게임에는 포함하지 않는다.

## 처리 시간 (사진 1장)

| 모델 | 스레드 1개 | 기본 스레드 |
|---|---|---|
| Places365 | 29ms | 3ms |
| CLIP 비전(uint8) | 51ms | 10ms |

64코어 CPU에서 측정한 값이라 일반 노트북보다 빠르다. 사용자 PC의 1초 기준(설계 §6-3-1)은 G0.5 Task 7에서 측정한다.
