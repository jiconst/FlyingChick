# CLAUDE.md — Flying Chick (Tiny Wings 스타일 Unity 게임)

## 프로젝트 개요

Tiny Wings 스타일의 원버튼 슬라이딩 게임을 Unity(C#)로 개발한다.
참조 자료: HTML5 프로토타입 `../MyProject/sample/flying-chick.html`(핵심 물리·점수 로직
완성본)과 Tiny Wings 스크린샷 17장(UI/메타 시스템 참조).

**핵심 게임 루프**: 화면을 누르면 새가 급강하하고, 떼면 상승한다. 내리막에서 눌러 가속 →
오르막 정점에서 떼서 발사 → 착지 타이밍을 맞춰 "Great Slide" 연출. 하루(제한시간) 동안
최대한 멀리 날아 점수를 쌓는다.

## 개발 환경 및 규칙

- **Unity 6000.5.7f1**, C# 10, 2D URP 템플릿
- **개발 OS**: macOS
- **C# 스크립트만 작성** (에디터에서 수동 설정할 항목은 주석 또는 별도 SETUP.md에 명시)
- 타겟: 모바일 세로 미지원, **가로(Landscape) 고정**, 터치 + 마우스 + 스페이스바(에디터
  테스트용) 입력
- 입력은 New Input System 패키지 기반 (`Active Input Handling` = "Input System Package"로
  설정된 프로젝트라 레거시 `UnityEngine.Input`은 예외를 던짐 — 절대 쓰지 말 것, 항상
  `InputService`를 통해서만 입력을 읽는다)
- 네임스페이스: `FlyingChick` (단일 네임스페이스, 폴더는 조직 용도로만 사용)
- **프로젝트 폴더**: `~/src/FlyingChick` 하나로 통일 — Claude Code 작업 폴더 = 실제 Unity
  프로젝트 폴더 (Unity Hub도 이 경로를 가리킴)
- **IDE**: Rider (`com.unity.ide.rider`). Rider 디버깅은 Unity 에디터를 먼저 켜둔 상태에서
  "Attach to Unity Editor"로 attach
- 폴더 구조:
```
  Assets/
    Scripts/
      Core/         # GameManager, GameState, DayCycle, InputService, ScreenSpace, GameBootstrapper
      Terrain/      # TerrainGenerator, GroundSampler
      Player/       # BirdController, BirdPhysics, BirdVisual
      Scoring/      # ScoreManager, SlideJudge, FeverSystem, NestMultiplier
      Collectibles/ # CoinSpawner, Coin, SpeedBoost, CloudTouch
      Meta/         # CoinWallet, BirdCollection, DailyMissions, SaveSystem
      UI/           # HUD, StartScreen, DayOverScreen, MissionUI, ShopUI
      FX/           # ParticlePool, PopupText, CameraFollow
    Prefabs/
    ScriptableObjects/  # BirdData, IslandPalette, MissionData
```
- **한 스크립트 = 한 책임**. MonoBehaviour 간 통신은 C# event 또는 간단한 static 이벤트 버스
  사용. 싱글톤은 GameManager, ScoreManager, SaveSystem에만 허용.
- 밸런스 수치는 전부 `[SerializeField]` 또는 ScriptableObject로 노출 (하드코딩 금지).

## 좌표계 — 중요, 반드시 이해하고 코드 작성할 것

`flying-chick.html`은 canvas 좌표계(원점 좌상단, y가 아래로 증가)로 작성돼 있다. 이 물리·
지형 수식을 **그대로(상수까지 동일하게) 포팅**해서 검증된 손맛을 잃지 않기 위해,
`GroundSampler`와 `BirdPhysics`는 **canvas 좌표계 그대로("canvas space")** 동작한다.
Unity는 중앙원점·y-up이라, 변환은 딱 한 곳(`ScreenSpace.cs`)에서만 일어난다:

- `ScreenSpace.ToWorldX(canvasX, viewHeight, aspect)` / `ToWorldY(canvasY, viewHeight)`
- `TerrainGenerator`(메시 정점)와 `BirdController.ApplyTransform()`(Transform 대입) 두
  군데에서만 이 변환을 쓴다. 그 외 물리/지형 코드는 전부 canvas space로 계산한다.
- 회전은 y-flip 때문에 방향이 반전되므로 `-physics.Angle`로 부호를 뒤집는다.
- `viewHeight`(기본 720)는 HTML의 `window.innerHeight`에 대응하는 기준값. 카메라
  `orthographicSize = viewHeight/2`로 고정하고, 폭은 `viewHeight * camera.aspect`로 매
  프레임 계산 (HTML의 `resize()`와 동일한 효과를 Unity의 `Camera.aspect`가 자동으로 해줌).
- **월드가 스크롤되고 새는 화면에 고정**된다 (HTML의 `scrollX` 모델과 동일). 새의 화면 X는
  `width * 0.28`로 고정, `GameManager.ScrollX`가 매 프레임 전진하면서 지형이 흘러가는
  것처럼 보인다. 카메라는 이 모델에서 **한 번도 움직이지 않는다**.

## 1단계 — 지형 (Terrain) — ✅ M1 구현 완료

**HTML 원본의 고정 사인파 지형은 플레이테스트 피드백으로 랜덤 지형으로 교체함** (언덕 폭·
높이가 항상 똑같이 반복되는 게 부자연스럽다는 요청). `groundY`/`groundSlope` 수식 자체는
더 이상 원본과 동일하지 않음 — 아래가 현재 방식.

`GroundSampler.cs`: **더 이상 static이 아님** — 시드 기반으로 컨트롤 포인트를 생성/보관하는
인스턴스 클래스. `GameManager.Ground`가 런(run) 전체에서 유일하게 소유하고, `TerrainGenerator`
와 `BirdPhysics`가 **반드시 이 한 인스턴스만** 참조해야 함 (따로 계산하면 화면 지형과 물리
착지선이 어긋남 — 실제로 한 번 이 문제가 나서 고친 적 있음, 아래 "이전 세션 기록" 참고).

- 언덕(valley→peak 또는 peak→valley) 하나마다 폭(half-wavelength)과 높이(amplitude)를
  각각 `[minHalfWave, maxHalfWave]` = `[viewHeight*0.22, viewHeight*0.58]`,
  `[minAmp, maxAmp]` = `[viewHeight*0.10, viewHeight*0.30]` 범위에서 랜덤 추출
  (`System.Random(seed)`, 생성자에서 받은 시드로 결정적)
- 컨트롤 포인트 사이는 코사인 이징으로 보간 (`(1-cos(t*π))/2`) — 각 피크/밸리에서 기울기가
  정확히 0이라 물리에 걸리는 꺾임이 없음
- `EnsureCoverage(x)`로 필요할 때마다 앞으로 늘어남(무한 스크롤 대응), 이진 탐색으로 구간
  찾음
- 시작 지점(x=0) 근처는 평평하게 시드해둬서 새가 안전하게 스폰됨
- `GameBootstrapper`의 `terrainSeed` 필드(0 = 매 실행마다 랜덤, 0이 아니면 그 값으로 고정
  재현 가능)로 시드 조절

`groundSlope`: 여전히 중앙차분(d=2)으로 계산 (인스턴스 메서드로 이동한 것만 다름).

`TerrainGenerator.cs`: 매 `LateUpdate`마다 현재 화면 폭만큼 `ground.GroundY(worldX)`를
6px(=world unit) 간격으로 샘플링해 Mesh 재생성 (MeshFilter+MeshRenderer, vertex color로
3단 그라데이션). **단일 재사용 메시** — 오브젝트 스폰/삭제 없음. 색 밴딩 범위는
`ground.BaseY`/`ground.MaxAmplitude`로 계산 (언덕마다 실제 높이가 달라도 일관된 그라데이션).

**M1에서 안 한 것** (다음 비주얼 패스로 미룸): 하늘 그라데이션, 태양/달, 별, 뒷배경 패럴랙스
언덕, 잔디 tuft, 섬별 10색 팔레트 전환. 지금은 카메라 배경색 단색 + 언덕 3색 그라데이션만.

## 2단계 — 새 물리 (Bird Physics) — ✅ M1 구현 완료

`BirdPhysics.cs` (순수 C# 클래스, MonoBehaviour/Transform 비의존 — `BirdController`가
Transform에 적용). Rigidbody2D 사용 안 함, `BirdController.FixedUpdate`에서 매 스텝 처리.

물리 튜닝 기준값 (canvas-space 단위 = world unit). 다이브/발사 임계값은 HTML 원본에서
플레이테스트 피드백으로 완화됨(원본 값은 표에 괄호로 표기) — "3연속 스트릭 달성이 너무
빡빡하다"는 요청으로, 더 관대한 경사각/속도에서도 다이빙 인정 + 발사가 되도록 낮춤:

| 항목 | 값 | 설명 |
|---|---|---|
| GRAV_AIR | 1500 | 기본 중력 가속도 |
| GRAV_HOLD | 3400 | 홀드(다이브) 중 중력 |
| SPEED_BASE | 360 | 기본 수평 속도 |
| SPEED_MIN / MAX | 300 / 980 | 속도 범위 |
| 다이브 가속 | +520/s | 내리막(slope > **0.04**, 원본 0.06)에서 홀드 중 |
| 속도 감쇠 | −90/s(초과 시), +60/s(미달 시) | SPEED_BASE로 수렴 |
| 발사 조건 | slope < **−0.07**(원본 −0.10) && speed > SPEED_BASE***0.75**(원본 0.9) | 오르막 정점 |
| 발사 수직속도 | slope * speed * 1.25 | |
| 새 반지름 | 15 | 지면 오프셋 계산에 사용 |

언덕 폭도 재차 요청으로 넓힘: `GroundSampler`의 `minHalfWave`/`maxHalfWave`가
`viewHeight*0.30~0.42` → `0.36~0.50`. 높이 범위(`minAmp`/`maxAmp`)는 그대로 둠.

핵심 알고리즘 (HTML `update()`와 동일한 순서):
1. 매 스텝 자유낙하로 y 적분 (`vy += g*dt; y += vy*dt`, g는 홀드 여부로 GRAV_AIR/HOLD)
2. `y >= groundY`면 지면 처리: 이전에 공중이었고 **체공 시간이 `MinAirborneTimeForJudging`
   (0.15초) 이상이었다면** 착지 판정(`holdWasDownhill`로 Great Slide/Miss 결정) 후 지면에
   스냅, `vy = slope*speed`로 경사에 밀착
3. 홀드 중 + 내리막(slope>0.04)이면 `holdWasDownhill=true` + 속도 가속
4. 급경사 오르막(slope<-0.07) + 충분한 속도면 발사(`vy = slope*speed*1.25`, airborne=true)
5. `y < groundY`면 공중 유지, `airborneTime` 누적

**`MinAirborneTimeForJudging` 버그 수정**: 발사 임계값을 완화(-0.10→-0.07)한 뒤, 언덕
정점 근처의 완만한 굴곡만으로도 몇 프레임짜리 미세한 뜸(micro-hop)이 자주 발생했고, 이걸
전부 착지 판정해버리니 다이빙 중이어도 "실패"로 잡혀 스트릭이 계속 리셋되는 문제가 있었음
(→ STREAK RESET이 너무 자주 뜨고, 카운트가 0 근처에서 안 올라가는 것처럼 보임). 체공
0.15초 미만인 비행은 아예 판정하지 않고 무시하도록 고침 — `holdWasDownhill`도 건드리지
않아서, 지형이 살짝 울퉁불퉁해도 진행 중이던 다이빙 상태가 끊기지 않음.

`BirdPhysics`는 `JustLandedGreatSlide`/`JustLandedMiss` 플래그를 노출하고, `BirdController`가
이걸 `OnGreatSlideLanding`/`OnMissedLanding` C# event로 다시 노출한다 — **Scoring 시스템은
이 이벤트만 구독하면 되고, BirdPhysics/BirdController를 건드릴 필요 없음.**

- 새는 X 고정(`width*0.28`), 세계가 스크롤 (`GameManager.AdvanceScroll(speed*dt)`)
- 새 회전각: `atan2(vy, speed)`를 향해 `dt*10` 속도로 lerp (canvas space에서 계산 후
  Unity 적용 시 부호 반전)
- dt는 Unity `Time.fixedDeltaTime` 사용 (HTML의 0.05초 클램프에 대응하는 별도 클램프는
  Fixed Timestep이 고정이라 불필요)

### 새 비주얼 (`BirdVisual.cs`) — 요청하신 "귀여운 병아리"

프로시저럴 드로잉(원/타원/삼각형 조합)으로 노란 몸통 + 크림색 배 + 볏(작은 점 3개, 스크린샷
속 새 머리 장식 참고) + 주황 삼각 부리 + 눈을 런타임 텍스처 1장에 굽는다. 날개만 별도
스프라이트로 분리해 `sin(t*22)` 플랩 애니메이션 (몸통 재굽기 없음, 성능상 유리).
**실제 일러스트 에셋은 아직 없음** — 코드로 그린 병아리이며, 나중에 진짜 아트로 교체하려면
`BirdVisual.BuildBodySprite()`의 리턴값을 갈아끼우면 됨 (SpriteRenderer.sprite만 교체).

## 3단계 — 점수/판정 시스템 — ✅ M2 구현 완료 (팝업/글로우/별 트레일 제외)

### Great Slide 판정 (`Scoring/SlideJudge.cs`)
1. 내리막에서 홀드 → `BirdPhysics`가 내부적으로 `holdWasDownhill = true` 추적
2. 공중에 뜬 후(airborne) 착지 시 `holdWasDownhill`이면 `BirdController.OnGreatSlideLanding`
   발생 → `SlideJudge.SlideStreak++`, 기본점수 `10 * (mult/10)`를 `ScoreManager.AddScore()`로
3. 실패(다이브 없이 착지) 시 `OnMissedLanding` 발생 → streak 리셋 + `FeverSystem.EndImmediately()`

### Fever Mode (`Scoring/FeverSystem.cs`)
- streak 3 도달 시 `SlideJudge`가 `FeverSystem.TriggerOrExtend()` 호출 → 5초 발동
- Fever 중 Great Slide마다(streak≥3 유지되는 한) +2.5초 연장 (최대 20초)
- Fever 중 **모든 점수 2배** (`ScoreManager.AddScore`가 내부에서 `fever.Multiplier` 적용)
- HUD에 남은 시간 표시 (`UI/HUD.cs`)
- **M2에서 안 한 것**: 새 주변 글로우, 별 트레일 파티클 (FX는 M6/M7)

### 섬(Island)과 배수 (`Core/GameManager.cs`)
- `ISLAND_LEN = 2600` 월드 단위마다 다음 섬으로 진행 (`GameManager.AdvanceScroll`이 누적 거리
  판정 후 `OnIslandAdvanced` 이벤트 발생)
- 배수: `GameManager.Multiplier = 10 + (Island - 1) * 2` → 10x, 12x, 14x…
- 섬 도달 시 `ScoreManager`가 보너스 점수(`50 * mult * 0.1`), `BirdController`가 속도 +120
  킥을 각각 이벤트 구독으로 처리 (GameManager는 island 값만 갱신하고 이벤트만 쏨 — score/속도
  로직을 직접 알지 않음)
- **M2에서 안 한 것**: "ISLAND N" 팝업 (FX는 M6/M7)

### Nest Multiplier — 🔲 M5로 이동 (메타 시스템과 함께, 저장 필요)

### 기타 득점원 — ✅ M3 구현 완료 (`Collectibles/`)

- `CoinField.cs`/`CloudField.cs`: 순수 C# 데이터 클래스 (`GroundSampler`와 같은 패턴 —
  시드 기반 `EnsureCoverage(x)`로 앞쪽을 필요할 때마다 채움). HTML의 `ensureCoins`/
  `ensureClouds`를 그대로 포팅: 코인은 90~150 유닛 간격, 10% 확률로 스피드코인 단독 배치,
  65% 확률로 3~7개짜리 코인 러시(아치형 오프셋), 25%는 빈 구간. 구름은 380~800 유닛
  간격으로 화면 상단 10~24% 높이 밴드에 배치
  - **스피드코인 높이 낮춤**: HTML 원본 offset(지면 위 60~100)이 지금 튜닝(낮아진 언덕 높이
    + 완화된 발사 임계값)으로는 새가 닿지 못하는 높이였음 → `28~54`로 낮춤. 언덕 높이나
    발사 관련 상수를 또 바꾸면 이 값도 같이 재확인할 것.
- `CoinSpawner.cs`/`CloudSpawner.cs`: 각각 고정 크기 오브젝트 풀(코인 48개, 구름 24개)로
  현재 화면에 보이는 것만 재배치해서 그림 — **Instantiate/Destroy 없음**. 판정도 같은
  스크립트에서: 코인은 `bird.radius+16` 반경 내 접촉, 코인 +3점 / 스피드코인 `speed+260`
  (`BirdController.AddSpeedBoost`), 구름은 **공중(Airborne)일 때만** 터치 인정,
  `20 * (mult/10)`점
- `FX/PickupBurst.cs`: 코인/구름/스피드 전부가 공유하는 단일 Unity `ParticleSystem` —
  루프 없이 `Emit()`으로만 발사해서 매번 새로 만들지 않음 (풀링 요구사항 충족)
- 팝업 텍스트: `UI/HUD.cs`에 월드 좌표 기반 토스트(`PositionedToast`)를 추가해서, 코인/구름을
  먹은 화면 위치 근처에 `+3`/`SPEED!`/`CLOUD TOUCH! +N`이 잠깐 떴다 사라짐 (기존 스트릭/
  Fever용 중앙 고정 토스트와는 별도 리스트로 관리)

## 4단계 — 낮/밤 사이클 — ✅ M4 구현 완료

- **`Core/GameState.cs`**: `Start`/`Playing`/`DayOver` 3상태 enum
- **`Core/GameManager.cs`**: `State` 프로퍼티 + `BeginRun()`/`EndRun()`/`ReturnToStart()`.
  `BeginRun()`이 ScrollX/Island 리셋 + **새 시드로 `Ground` 재생성**(리플레이마다 다른
  지형) 후 `OnRunStart` 이벤트 발생 — 다른 모든 시스템(새 물리, 점수, 스트릭, Fever,
  코인/구름 스포너)은 이 이벤트 하나만 구독해서 각자 스스로 리셋함. GameManager는 그
  시스템들의 내부를 전혀 모름 (완전 이벤트 기반 디커플링)
- **`Core/DayCycle.cs`**: `DAY_LENGTH=90초`, `State==Playing`일 때만 진행, dayTime 0→1,
  1 도달 시 `GameManager.EndRun()` 호출
- **`FX/SkyTint.cs`**: dayTime 기준 카메라 배경색을 낮→노을(0.55 지점)→밤으로 lerp.
  **M4에서 안 한 것**: 태양/달 원판, 별, 실제 그라데이션 하늘(단색만) — M6/M7
- **`Player/BirdController.cs`**: `State != Playing`이면 `FixedUpdate` 자체를 스킵 (물리/
  스크롤 정지, Start/DayOver 화면에서 새가 가만히 앉아있음). `OnRunStart`에서 새
  `GroundSampler`를 참조하는 새 `BirdPhysics`로 완전히 재구성
- **`UI/StartScreen.cs`**: `State==Start`일 때만 표시, 아무 입력(터치/클릭/스페이스)이나
  누르면 `GameManager.BeginRun()`
- **`UI/DayOverScreen.cs`**: `State==DayOver`일 때 최종 통계(Score/Island/Great Slides/
  Cloud Touches/Longest Fever) + Best 표시, "다시하기"(`BeginRun()`)/"홈"(`ReturnToStart()`)
  버튼. Longest Fever는 `FeverSystem.LongestDuration`(신규), Cloud Touches는
  `CloudSpawner.TouchCount`(신규)로 추적
- **`Meta/SaveSystem.cs`**: 싱글톤(스펙상 GameManager/ScoreManager/SaveSystem만 허용),
  **최고점수만** `PlayerPrefs` 저장 — 코인 지갑/새 컬렉션/미션 등 나머지 저장 데이터는
  M5에서 `JsonUtility`+`persistentDataPath` 기반으로 확장
- **`UI/HUD.cs`**: 우상단에 낮 진행바(day clock) 추가, `State==Playing`일 때만 표시되도록
  게이팅(그 외 상태에서는 Start/DayOver 화면이 대신 그려짐)
- **M4에서 안 한 것**: Day Over 화면의 코인 카운트업 애니메이션 (M6/M7 비주얼 폴리시)

## 5단계 — 메타 시스템 — ✅ M5+M6 구현 완료

### 저장 구조 (`Meta/SaveData.cs`, `Meta/SaveSystem.cs`)
- 최고점수: M4부터 그대로 `PlayerPrefs` (스펙: "PlayerPrefs는 최고점수만")
- 그 외 전부(코인, Nest 배수 보너스, 데일리 미션 날짜/진행/완료): `JsonUtility` +
  `Application.persistentDataPath`의 `flyingchick_save.json` 한 파일. `SaveSystem`은
  직렬화/파일 I/O만 담당하고 게임 로직은 전혀 모름 — `CoinWallet`/`NestMultiplier`/
  `DailyMissions`가 `SaveSystem.Instance.Data`를 직접 읽고 쓴 뒤 `Save()` 호출
- **ScriptableObject 대신 일반 C# 데이터**(`Meta/MissionPool.cs`, `Meta/BirdPool.cs`)로
  미션/새 풀을 정의함. 이 프로젝트는 지금까지 완전히 코드로만 조립돼서(에디터에서 수동
  설정 없이 Play만 누르면 됨) 되어 있는데, `.asset` 파일을 에디터 없이 손으로 만드는 건
  깨지기 쉬워서 이번엔 스펙(ScriptableObject)과 다르게 갔음. 나중에 실제 에디터 콘텐츠
  제작 워크플로가 생기면 ScriptableObject로 바꾸는 게 좋음

### 코인 지갑 (`Meta/CoinWallet.cs`)
- `GameManager.OnRunEnd`(day-over 직전, 스탯이 아직 살아있는 시점)를 구독해서 자동으로
  코인 지급 — 다른 시스템이 명시적으로 호출할 필요 없음
- **점수→코인 환산 비율은 발명한 값** (`flying-chick.html`엔 메타 레이어 자체가 없어서
  참고할 원본이 없음): `score / 50` 내림. `CoinWallet.scoreToCoinsRatio` 하나로 조절

### Nest Multiplier (`Meta/NestMultiplier.cs`)
- 매 런(`OnRunStart`)마다 `MissionPool.Nest`(5개 후보) 중 3개를 무작위로 뽑음
- 진행 상황을 별도로 누적 추적하지 않고, **`OnRunEnd` 시점에 그 런의 살아있는 통계
  (`SlideJudge.TotalSlides`, `FeverSystem.LongestDuration`, `CloudSpawner.TouchCount`,
  `ScoreManager.Score`, `GameManager.Island`)를 그대로 조회**해서 통과 여부 판정 — 이 값들이
  이미 다 실시간으로 관리되고 있어서 추가 상태가 필요 없음
- 3개 전부 통과 시 `GameManager.NestBonus` +1 영구 상승 + 저장. `Multiplier` 공식이
  `10 + NestBonus + (Island-1)*2`로 바뀜 (기존 `10 + (Island-1)*2`에서 확장)
- **스펙에서 단순화한 부분**: 원본 예시 목표 중 "1번 섬에서 5000점"(특정 섬으로 범위를
  좁힌 점수)은 점수 서브토탈을 섬별로 추적하는 새 상태가 필요해서, 그냥 "이번 런에서
  5000점 획득"으로 단순화함
- 플레이 중 HUD 좌측에 이번 런의 목표 3개 + 실시간 진행 상황이 항상 보임 (스펙은 "런 시작
  전 제시"였지만, 목표는 `BeginRun()` 시점에 뽑히므로 시작 전엔 아직 없음 — 대신 플레이
  내내 보이도록 해서 "뭘 해야 하는지 모른 채 끝남" 문제는 없앰). Day Over 화면에서 각
  목표 통과(✓)/실패(✗) 결과 확인 가능

### 데일리 미션 (`Meta/DailyMissions.cs`)
- 매일(`DateTime.Today` 기준) `MissionPool.Daily`(5개 후보) 중 3개를 뽑음 — 날짜 문자열을
  시드로 써서, 같은 날 앱을 껐다 켜도 같은 3개가 나옴
- **런 하나로는 안 끝나고 하루 동안 여러 런에 걸쳐 누적** — Nest Multiplier와 다른 점.
  진행 상황은 매번 즉시 저장되므로 앱을 종료해도 유지됨
- 관련 이벤트를 구독해서 진행 카운트: `SlideJudge.OnGreatSlide`, `FeverSystem.OnFeverStart`,
  `CoinSpawner.OnCoinCollected`(신규 — 팝업 텍스트용 이벤트와 분리된 순수 도메인 이벤트),
  `CloudSpawner.OnCloudTouched`(신규, 같은 이유). "섬 도달"류는 누적이 아니라 그날 최고
  기록으로 추적(`SetBest`)
- 완료 시 코인 100개 즉시 지급, 시작 화면 좌하단 패널에 진행 상황 표시

### 새 컬렉션 / 알 가챠 (`Meta/BirdPool.cs`, `Meta/BirdCollection.cs`)
- `BirdPool.All`에 5마리 정의 (기본 노랑 병아리는 무료로 처음부터 보유, 나머지 4마리는
  각각 고유 Perk 보유): 빨강(슬라이드 점수 +10%), 파랑(Fever 지속시간 +1초), 초록(코인
  획득 범위 +20), 보라(시작 속도 +50)
- 알 구매 500코인 → 아직 안 가진 새 중 무작위 하나 부화 (`BirdCollection.BuyEgg()`). 전부
  모았으면 구매 비활성화. 코인 부족하면 그냥 아무 일도 안 일어남(차감 안 됨)
- **Perk 적용 방식**: `BirdCollection`은 어떤 시스템에도 직접 로직을 넣지 않고, 영향받는
  시스템(`SlideJudge`, `FeverSystem`, `CoinSpawner`, `BirdController`)이 각자
  `SetCollection(BirdCollection)`으로 참조를 받아서 자기 계산에 반영 — 예를 들어
  `SlideJudge`는 점수 계산 시 `collection.SelectedBird.Perk == SlideScoreBonus`면 배율을
  곱함. `BirdCollection`은 "지금 선택된 새가 뭔지"만 알고 있음
- `BirdVisual`도 `SetCollection`을 받아서 선택된 새가 바뀌면(`OnSelectionChanged`) 몸통/배/
  날개 색을 다시 그림 (부리·볏·눈은 모든 새 공통이라 안 바뀜)
- 홈 화면(`UI/StartScreen.cs`) 하단에 보유 새 아이콘 줄 — 클릭하면 선택, 안 가진 새는
  회색 "?"로 표시, 선택된 새는 흰 테두리. 알 구매 버튼과 부화 결과 토스트도 여기 있음

### 로컬 리더보드/통계 (`Meta/Leaderboard.cs`)
- `GameManager.OnRunEnd`마다 이번 점수를 Top 10에 삽입(정렬), 총 슬라이드/총 런 수(=스펙의
  "총 비행일 수", 하루 사이클 하나 = 하루) 누적. 전부 저장됨
- 홈 화면 우상단 "기록 보기" 버튼으로 토글되는 패널에서 확인 가능

### 홈 화면 클릭과 "탭하면 시작" 겹침 버그 주의
- `StartScreen`은 원래 "아무 데나 탭하면 시작"이었는데, 새 아이콘/알 구매/기록 버튼이
  생기면서 그 버튼들을 누른 탭까지 게임을 시작시켜버리는 문제가 생길 뻔함. 이 버튼들의
  Rect를 `IsBlockingClick()`으로 따로 체크해서, 그 영역 안에서의 탭은 런 시작을 막도록
  처리함 (`InputService.PointerPosition()` 신규 추가 — Update()의 New Input System 체크와
  OnGUI의 IMGUI Rect를 좌표계 맞춰 비교)

### UI
- **`UI/StartScreen.cs`**: 우상단 코인 총액 + 기록 버튼, 좌하단 오늘의 미션 패널, 하단 새
  선택 줄 + 알 구매 버튼
- **`UI/DayOverScreen.cs`**: 이번 런 획득 코인 + 누적 코인 총액, Nest 목표 3개 통과/실패
- **`UI/HUD.cs`**: 플레이 중 좌측에 이번 런 Nest 목표 패널(실시간 진행)

## 6단계 — 비주얼 & 폴리시 — 🚧 M7 진행 중

이미 된 것: 획득 파티클 버스트(`FX/PickupBurst.cs`, M3), 팝업 텍스트(`UI/HUD.cs`의 토스트,
M3), **카메라 줌**(`FX/CameraZoom.cs`), **다이브 먼지/Fever 별 트레일**
(`FX/BirdTrailParticles.cs`), **하늘 그라데이션/태양·달/별/섬별 10색 팔레트**
(`FX/SkyRenderer.cs`, `FX/SkyObjects.cs`, `Terrain/IslandPalette.cs`), **사운드**
(`Audio/`, 아래 상세), **성능(정적 코드 리뷰 기반 GC 할당 제거, 아래 상세)**,
**OnGUI → UGUI/TextMeshPro 교체(아래 상세)**, **뒷배경 패럴랙스 언덕 + 잔디 tuft(아래 상세)
— 전부 플레이 확인 필요** 완료. 남은 것:
- (스킵, 계속 보류) 태양 주변 glow — Unity `SpriteRenderer`로는 HTML의 `ctx.shadowBlur`
  재현이 번거로워 생략, 필요하면 나중에 Bloom 포스트프로세싱으로
- (스킵, 계속 보류) 언덕 크레스트 위 밝은 rim 하이라이트 라인 — `drawHills()`에 있지만 잔디
  tuft만큼 눈에 띄는 효과가 아니라서 이번엔 생략, 필요하면 `GrassTuftGenerator`와 같은
  패턴으로 추가 가능

### OnGUI → UGUI/TextMeshPro 교체
사용자에게 세 가지 선택지(UGUI+TMP / UI Toolkit / 지금은 보류)를 물어봤고 **UGUI(Canvas +
TextMeshPro)**를 골라서 이 방향으로 `UI/HUD.cs`, `UI/StartScreen.cs`, `UI/DayOverScreen.cs`
세 화면을 전부 재작성함. `OnGUI()` 콜백이 완전히 사라지고, 대신 `Bind()` 시점에 한 번
Canvas 계층을 코드로 만들어 두고(에디터 수동 설정 없음, 기존 원칙 유지) `Update()`에서
텍스트/색/위치만 갱신하는 방식.

- **`UI/UIFactory.cs`(신규)**: Canvas/TMP_Text/Image/Button 생성 헬퍼. 위치 지정은 일부러
  기존 OnGUI의 `new Rect(x, y, w, h)`(원점 좌상단, y 아래로 증가) 관례를 그대로 흉내내는
  `SetTopLeft(rt, x, y, w, h)`/`SetTopLeftCentered(...)` 헬퍼로 통일 — 그래서 각 화면의
  포팅이 레이아웃을 새로 설계하는 게 아니라 거의 기계적인 치환(`GUI.Label(new Rect(...))` →
  `text.text=...; UIFactory.SetTopLeft(...)`) 수준으로 끝남.
- **`UI/UIFontProvider.cs`(신규, 가장 위험한 부분)**: TMP 텍스트에 폰트를 안 주면 기본값
  `TMP_Settings.defaultFontAsset`을 찾는데, 이건 에디터 메뉴("Import TMP Essential
  Resources")로 한 번 수동 설치해야 하고 그마저도 라틴 문자 전용(Liberation Sans)이라
  이 프로젝트(한글 UI 텍스트가 대부분, 수동 에디터 설정 금지)엔 둘 다 안 맞음. 대신
  **런타임에 macOS 시스템 폰트 파일 경로로부터 `TMP_FontAsset.CreateFontAsset(...)`을
  다이나믹 아틀라스 모드로 직접 생성**해서 모든 `UIFactory.CreateText()` 호출에 명시적으로
  꽂아줌 (`TMP_Settings` 기본값을 아예 안 건드려서 임포트 프롬프트 자체가 뜰 일이 없음).
  **버그 재발 방지**: 첫 버전은 `Font.CreateDynamicFontFromOSFont(...)`로 만든 `Font`를
  `TMP_FontAsset.CreateFontAsset(Font, ...)`에 넘기는 방식이었는데, 이게 항상
  `NullReferenceException`으로 터짐 — `CreateFontAsset(Font)`는 내부적으로
  `FontEngine.LoadFontFace(font, ...)`(HarfBuzz 기반)를 쓰는데 이건 실제 글리프 외곽선
  데이터가 임베드된 폰트가 필요하고, `CreateDynamicFontFromOSFont`가 만든 `Font`는 OS 이름
  참조만 있는 "동적 폰트"라 이 데이터가 없어서 `LoadFontFace`가 예외 없이 조용히 실패하고
  `null`을 반환함(그다음 줄 `.name = ...`에서 NRE). **교훈: `Font.CreateDynamicFontFromOSFont`
  로 만든 `Font`는 TMP의 런타임 SDF 생성 소스로 못 씀 — legacy dynamic-font 텍스트 렌더링
  전용.** 고침: 실제 폰트 **파일 경로**를 바로 받는 별도 오버로드
  (`TMP_FontAsset.CreateFontAsset(string path, int faceIndex, ...)`)로 교체 — 이건 `Font`
  객체를 거치지 않고 경로를 FontEngine에 바로 넘겨서 정상 동작. 현재 후보 경로는
  `/System/Library/Fonts/AppleSDGothicNeo.ttc`, `/System/Library/Fonts/Supplemental/
  AppleGothic.ttf`(둘 다 개발 머신에서 `ls`로 존재 확인함) — 존재하는 첫 파일을 씀, 전부
  없으면 `LegacyRuntime.ttf`(라틴 전용, 실제 임베드 데이터가 있는 진짜 폰트라 이 경로는
  안전하게 성공함)로 폴백해서 최소한 NRE는 안 나게 함.
  **⚠️ 이 환경에서는 실제로 Unity 에디터를 열어 검증할 방법이 없어서, 정적 코드 리뷰로만
  만든 부분** — 이번 OnGUI→UGUI 교체 전체에서 가장 위험도가 높은 지점. **플레이 확인 시
  한글 텍스트가 두부(tofu)/빈 사각형으로 깨지지 않는지부터 확인할 것.** 다른 Mac이나
  Android/iOS 빌드로 넘어가면 이 경로들이 안 맞을 수 있음 — 그때는 `KoreanFontCandidates`에
  해당 플랫폼 폰트 경로를 추가할 것.
- **`IsBlockingClick(Rect)` 패턴 제거**: `StartScreen`이 예전엔 "아무 데나 탭하면 시작"과
  버튼 탭이 겹치는 걸 막으려고 버튼 Rect들을 일일이 손으로 체크했는데, 실제 UGUI
  Button/GraphicRaycaster로 바뀌면서 이 문제가 구조적으로 사라짐 — 화면 전체를 덮는 투명
  "tap catcher" 버튼을 계층에서 맨 처음(가장 아래)에 만들어두고, 진짜 버튼들은 그 뒤에
  만들어서 항상 그 위에 렌더링되게만 하면, UGUI가 알아서 위에 있는(나중에 그려진) 그래픽을
  먼저 레이캐스트로 잡아줌. 아무것도 안 맞았을 때만 tap catcher가 클릭을 받아 `BeginRun()`
  호출.
- **`Core/InputService.cs`에 `IsSpaceDownThisFrame()`(신규) 추가**: 기존
  `IsPointerDownThisFrame()`은 마우스/터치/스페이스바를 한데 묶어 반환했는데, 마우스/터치는
  이제 UGUI 버튼 클릭 파이프라인을 그대로 타므로 그쪽에서 또 폴링하면 같은 클릭이 두 번
  `BeginRun()`을 부를 수 있음. 스페이스바는 UGUI 클릭 경로가 없어서 여전히
  `StartScreen.Update()`에서 직접 폴링 — 새 메서드로 마우스/터치 분기 없이 스페이스바만
  분리.
- **`Core/GameBootstrapper.cs`에 `EventSystem` + `InputSystemUIInputModule` 추가(신규)**:
  씬 전체에 하나만 만듦. `AssignDefaultActions()`를 호출해서 `.inputactions` 에셋 없이도
  New Input System 기반으로 UGUI 클릭/포인터 입력이 라우팅되게 함 (이 프로젝트는 레거시
  `UnityEngine.Input`을 못 씀 — `InputService.cs` 주석 참고).
- **토스트/픽업 토스트/미션 줄/리더보드 줄은 전부 고정 크기 풀**(`HUD`의 `toastPool`/
  `pickupToastPool`, `StartScreen`의 `dailyLines`/`leaderboardLines`, `DayOverScreen`의
  `statLines`/`nestLines`) — `Bind()` 시점에 미리 다 만들어두고 매 프레임 활성/비활성 +
  텍스트만 갱신. 기존 "Instantiate/Destroy 반복 금지" 원칙(코인/구름 스포너와 동일 패턴)을
  UI에도 그대로 적용.
- **컴파일 에러 수정**: `UIFontProvider.cs`에서 `GlyphRenderMode`가 `TMPro`가 아니라
  `UnityEngine.TextCore.LowLevel` 네임스페이스에 있어서 `using` 누락으로 CS0103 에러 발생 →
  추가해서 해결 (`Library/PackageCache/com.unity.ugui@.../Runtime/TMP/TMP_FontAsset.cs`의
  `using`을 직접 대조해서 확인). `AtlasPopulationMode`는 원래대로 `TMPro` 네임스페이스가 맞음.
- **정정: "Import TMP Essential Resources"는 사실 필수임 — 이전 기록이 틀렸음.** 처음엔
  콘솔 경고("TextMesh Pro Essential Resources are missing...")가 무해한 에디터 알림이라고
  판단해서 "무시해도 됨"으로 기록했었는데, 실제로는 `NullReferenceException`으로 이어짐:
  `TMP_FontAsset.CreateFontAssetInstance()`(우리가 어떤 방식으로 폰트를 만들든 항상 거치는
  TMP 공통 내부 경로)가 `TMP_Settings.instance.clearDynamicDataOnBuild`를 조건 없이 읽는데,
  `TMP_Settings.instance`는 `Resources.Load<TMP_Settings>("TMP Settings")` 조회라서 이
  에셋이 없으면 계속 `null`이고 그 프로퍼티 접근에서 NRE남. **`UIFontProvider`가 직접 만든
  폰트를 쓰는 것과는 무관하게, `TMP_Settings` 에셋 자체가 프로젝트에 존재해야만 TMP가
  동작함** — `defaultFontAsset`을 실제로 쓰냐 마냐와 별개 문제였음. 코드만으로(리플렉션으로
  private static 필드에 직접 주입 등) 우회하는 방법도 검토했지만, 사용자에게 확인 후 **정식
  경로인 Window > TextMeshPro > Import TMP Essential Resources를 한 번 실행하는 쪽으로
  결정** — 이 프로젝트의 "에디터 수동 설정 금지" 원칙에 대한 **명시적 예외**로 문서화함
  (`UIFontProvider.cs` 상단 주석에도 기록). 이 임포트가 추가하는 Liberation Sans SDF는
  라틴 전용이라 한글 렌더링에는 어차피 안 쓰임 — 우리 코드는 여전히 항상
  `UIFontProvider.Get()`으로 직접 만든 한글 폰트를 명시적으로 꽂아줌.
- **✓/✗ 딩뱃 기호가 두부(□)로 깨짐 → "O"/"X" 문자로 교체**: 콘솔에 "character ✗ was
  not found in the [Runtime Korean SDF] font asset" 경고 + `TMP_Text`가 자동으로 □(U+25A1)
  대체 글리프로 바꿔치기하는 문제. 원인: `✓`(U+2713)/`✗`(U+2717)는 둘 다 유니코드 Dingbats
  블록인데, `UIFontProvider`가 쓰는 한글 시스템 폰트(AppleSDGothicNeo/AppleGothic)는 한글+
  라틴 위주라 이 블록을 포함 안 함 — 우리가 만든 폰트 자체의 문제가 아니라 그 폰트가 애초에
  커버하지 않는 문자였음. **`✗`만 경고가 떴었지만 `✓`도 같은 블록이라 잠재적으로 똑같이
  깨질 수 있어서(아직 통과 미션을 안 봐서 안 걸렸을 뿐일 수 있음) 코드베이스 전체에서 두
  기호를 다 찾아서 함께 고침** — `HUD.cs`(Nest 패널), `StartScreen.cs`(데일리 미션 패널),
  `DayOverScreen.cs`(Nest 목표 결과)의 `✓`/`✗`를 전부 `"O"`/`"X"`로 교체. 한국어 "OX 퀴즈"
  표기 관례와도 맞아서 자연스러움. **교훈: 런타임 생성 폰트를 쓸 때는 일반 문자(한글/영문/
  숫자/기본 문장부호) 밖의 특수 기호·이모지·딩뱃은 그 폰트가 실제로 커버하는지 별도로
  확인해야 함** — 코드에 새 기호 문자를 추가할 때는 이 점을 염두에 둘 것.
- **`Packages/manifest.json` 변경 없음**: Unity 6(`com.unity.ugui` 2.5.0)부터 TextMeshPro가
  이 패키지에 내장돼서 `com.unity.textmeshpro`를 별도로 추가할 필요가 없음
  (`packages-lock.json`에서 확인 — `com.unity.ugui`만 있고 별도 textmeshpro 항목 없음).
- **`HUD`/`StartScreen`/`DayOverScreen`은 여전히 `GameBootstrapper`가 만드는 같은
  GameObject에 컴포넌트로 붙지만, 실제 화면은 각자 `UIFactory.CreateCanvas(...)`로 만든
  별도의 최상위 Canvas GameObject**(정렬 순서: HUD=0, StartScreen=10, DayOverScreen=20 —
  상태가 서로 배타적이라 실제로 겹칠 일은 거의 없지만 전환 중 깜빡임 방지용 안전장치)
- **플레이 확인 후 가독성 피드백으로 수정**: 좌상단 점수, 우하단 디버그 텍스트 두 곳은
  배경 패널 없이 텍스트만 떠 있어서 하늘/지형 색에 따라 묻히는 문제가 있었음(HUD 초기
  구현 때부터 있던, 이전에도 day-clock 진행바에서 한 번 겪었던 것과 같은 종류의 문제 —
  "이전 세션 기록" 6번 항목 참고). **고침**: 이미 스트릭/네스트 패널에서 쓰던 반투명 검은
  배경(`ScorePanel`/`DebugPanel`, 알파 0.4~0.45) + 밝은 크림색 굵은 글씨 패턴을 그대로
  적용, 폰트 크기도 키움(점수 34→38, 디버그 14→16). 디버그 텍스트는 우측 정렬로 바꿔서
  패널 오른쪽 여백에 자연스럽게 붙게 함.

### 성능 (정적 코드 리뷰 기반 GC 할당 제거)
- **주의**: 이 환경에서는 Unity Profiler를 직접 붙여서 실측할 방법이 없음 — 아래는 코드를
  읽고 "매 프레임 불필요하게 할당하는 지점"을 찾아 고친 정적 리뷰 결과이지, 프로파일러
  실측치가 아님. 실제 프레임타임/GC spike 확인은 에디터에서 Window > Analysis > Profiler로
  직접 확인 필요 (특히 Memory/GC Alloc 트랙).
- **`OnGUI`의 `new GUIStyle(...)` 반복 할당**: `OnGUI`는 Unity가 프레임당 최소 2번(Layout +
  Repaint 이벤트) 호출하는데, `HUD.cs`/`StartScreen.cs`/`DayOverScreen.cs` 전부 그릴 때마다
  `new GUIStyle(GUI.skin.label) {...}`을 새로 만들고 있었음 → 매 프레임 다수의 GUIStyle
  객체가 힙에 쌓였다 버려짐. **고침**: 세 파일 다 `EnsureStyles()`(최초 1회만 빌드) +
  private 필드로 캐싱하는 패턴으로 통일. 색상만 항목마다 달라지는 경우(예:
  `DrawNestPanel`의 미션 통과/실패 색, `DrawDailyMissions`의 완료 색)는 캐싱된 스타일의
  `.normal.textColor`만 그때그때 바꿔씀 (스타일 객체 자체는 재사용).
- **`StartScreen.ComputeLayout()` 중복 계산 + 배열 재할당**: 기존엔 `Update()`와 `OnGUI()`
  양쪽에서 매번 레이아웃 전체(버튼 Rect들)를 재계산하고 있었고, `birdIconRects` 배열도 매번
  `new Rect[...]`로 새로 만들고 있었음 — 레이아웃은 사실 `Screen.width`/`Screen.height`가
  바뀔 때만 달라짐. **고침**: 마지막으로 계산했던 화면 크기를 기억해두고, 크기가 실제로
  바뀌었을 때만 재계산하도록 가드 추가. `birdIconRects`도 길이가 같으면(항상 같음, 새
  종류 수 고정) 배열을 재사용하고 내용만 덮어씀.
- **`TerrainGenerator.RebuildMesh()`의 정점/색/인덱스 배열 매 프레임 재할당**: `LateUpdate`
  마다(즉 매 프레임) `new Vector3[steps*2]`, `new Color[steps*2]`, `new int[(steps-1)*6]`을
  새로 만들어서 `mesh.vertices`/`.colors`/`.triangles`에 대입하고 있었음 — 화면에 지형이
  항상 그려지는 이 게임 특성상 가장 뜨거운 할당 지점이었을 가능성이 높음. **고침**: 재사용
  가능한 `List<Vector3>`/`List<Color>`/`List<int>` 필드로 바꾸고, 매 프레임 `Clear()` 후
  다시 채워서 `mesh.SetVertices/SetColors/SetTriangles`로 넘김 — `List.Clear()`는 내부
  배열을 그대로 유지하므로, 리스트가 한 번 정상 크기까지 자란 뒤로는(줌 레벨이 안정되면
  steps 값도 거의 고정됨) 추가 할당이 발생하지 않음.
- **다루지 않은 것**: `string` 보간(`$"..."`,  `:N0`/`:0.0` 포맷)도 `OnGUI`/HUD 갱신마다
  일어나는 할당원이지만, 매 프레임 텍스트가 실제로 바뀌는 UI(점수, 타이머 등)라 캐싱해도
  이득이 적어 손대지 않음 — 진짜 문제였던 "매 프레임 똑같은 값을 다시 할당"하는 지점
  (GUIStyle, 지형 배열) 위주로만 고침.

### 사운드 (`Audio/ProceduralAudio.cs`, `Audio/AudioManager.cs`)
- **실제 오디오 파일은 이 환경에서 만들거나 구할 방법이 없어서, 지금까지의 프로시저럴
  비주얼(픽셀로 그린 병아리/해/구름 등)과 같은 접근으로 코드에서 사인파를 합성**해
  `AudioClip.Create`로 만듦 (`ProceduralAudio.cs`: `Tone`/`Chime`/`Sweep`/`NoiseBurst`/`Pad`).
  실제 작곡/사운드 디자인이 아니라 삐-소리 수준의 신호음이라는 점 감안할 것 — 나중에 진짜
  오디오 에셋으로 교체하고 싶으면 `AudioManager.BuildClips()`에서 `AudioClip` 할당만
  바꾸면 됨 (재생 로직은 그대로 재사용 가능)
- **SFX 이벤트 연결**: `AudioManager`는 싱글톤이 아님(스펙상 GameManager/ScoreManager/
  SaveSystem만 허용) — 다른 시스템들이 이미 노출하고 있는 이벤트(`BirdController.OnLaunch`
  신규 추가, `SlideJudge.OnGreatSlide`, `FeverSystem.OnFeverStart`,
  `CoinSpawner.OnCoinCollected`/`OnSpeedCoinCollected`, `CloudSpawner.OnCloudTouched`,
  `GameManager.OnIslandAdvanced`, `DayCycle.OnDayOver`)를 구독하기만 함 — HUD/DailyMissions와
  같은 패턴. 버튼 클릭음만 예외로, `StartScreen`/`DayOverScreen`이 `PlayClick()`을 직접 호출
  (버튼 프레스는 이벤트가 없어서)
- **`BirdController.OnLaunch`(신규)**: 착지 이벤트(`OnGreatSlideLanding`/`OnMissedLanding`)는
  있었지만 "방금 떴다"는 이벤트가 없었음 — `BirdPhysics.JustLaunched`(지상→공중 전환된
  바로 그 프레임, 명시적 발사든 자연스러운 크레스팅이든 둘 다 포함)를 추가해서 노출
- **BGM은 자리만 채워둔 임시 앰비언트**(`ProceduralAudio.Pad`, 두 사인파 레이어를 6초
  루프) — "잔잔한 배경음악"을 코드로 진짜 작곡하는 건 이 방식의 한계를 넘어서므로, 나중에
  실제 작곡된 트랙으로 교체하는 걸 권장. 루프 시작/끝을 0으로 페이드시켜서 반복 재생 시
  딸깍 소리는 안 남

### 하늘/팔레트 (`Terrain/IslandPalette.cs`, `FX/SkyRenderer.cs`, `FX/SkyObjects.cs`)
- `IslandPalette.cs`: HTML `PALETTES` 배열 10개를 색상 hex 그대로 포팅
  (`ColorUtility.TryParseHtmlString` 기반 `Core/ColorUtil.cs`로 파싱). `ForIsland(island)`가
  `(island-1) % 10`으로 순환
- `TerrainGenerator`: 이제 언덕 색이 하드코딩 3색이 아니라 `IslandPalettes.ForIsland(gm.Island)`
  기반 4단 그라데이션(HTML `drawHills()`의 CSS 그라데이션 stop 0/0.25/0.55/1과 동일 지점)이고,
  `night = dayTime^1.4`만큼 어두운 색(`#1a1a3a` 등)으로 lerp됨 — 섬이 바뀌면 팔레트가,
  시간이 지나면 어둡기가 자동으로 반영됨. 이걸 위해 `SetDayCycle(DayCycle)`을 새로 받음
  (부트스트랩에서 DayCycle 생성 직후 배선)
- `SkyRenderer.cs`: 기존 `SkyTint.cs`(카메라 배경색 단색 lerp)를 대체 — 화면을 항상 덮는
  큰 정적 quad 메시 하나에 정점색 2개(위/아래)만 매 프레임 갱신하는 방식으로 실제
  그라데이션 하늘을 그림. `dayTime > 0.55`부터는 노을(주황) 색이 아래쪽에 섞여 들어감
  (HTML의 dusk band와 동일 공식). 카메라 `backgroundColor`는 이제 실질적으로 안 보임(항상
  이 quad에 덮임) — 그냥 안전 폴백으로 남겨둠
- `SkyObjects.cs`: 해/달 원판(하루 동안 위→아래로 하강, `dayTime>0.8`부터 달로 교체) +
  반짝이는 별 40개(`night>0.2`부터 페이드인, 개별 트윈클). **HTML 원본처럼 스크롤과
  무관하게 화면에 고정된 위치**에 그림 — 즉 이 좌표들은 `gm.ScrollX`를 더하지 않고
  `ScreenSpace.ToWorldX/Y`에 바로 넣음 (다른 모든 오브젝트는 `gm.ScrollX + canvasX`를
  쓰는 것과 대조적임, 헷갈리지 말 것)
- **M7에서 안 한 것**: 태양 주변 glow(HTML은 `ctx.shadowBlur`로 은은한 빛번짐을 넣지만 Unity
  SpriteRenderer로는 그대로 재현이 번거로워 생략 — 필요하면 Bloom 포스트프로세싱으로 나중에
  처리하는 게 나음), 언덕 크레스트 rim 하이라이트 라인

### 배경 패럴랙스 언덕 / 잔디 tuft (`Terrain/BackgroundHillGenerator.cs`, `Terrain/GrassTuftGenerator.cs`)
M7에서 우선순위 낮다고 스킵했던 두 항목 — `flying-chick.html`의 `drawHills()`에서 그대로 포팅.

- **`BackgroundHillGenerator.cs`**: `drawHills()`의 "background echo hills (parallax,
  lighter)" 부분. **같은** `GameManager.Ground`(`GroundSampler`)를 다시 샘플링하되,
  월드 X를 `ScrollX * 0.55`로 늦게 스크롤시키고(`parallaxFactor`) Y를 `+70`(`verticalOffset`)
  내려서 진짜 전경 언덕(`TerrainGenerator`) 뒤에서 더 느리게 흘러가는 것처럼 보이게 함 —
  **화면상 정점 위치(`canvasX`)는 그대로 두고, `GroundY()`를 샘플링할 때만 다른(느린) 월드
  X를 쓰는 게 패럴랙스 트릭의 핵심** (레퍼런스 `wx = scrollX*0.55 + x; lineTo(x, groundY(wx))`
  와 동일 — `x`는 화면 좌표, `wx`만 느리게 감). 색은 그라데이션 없이 단색
  (`pal.HillTop`을 밤에 `#20204a`로 lerp) + 반투명(`alpha=0.5`). `TerrainGenerator`와
  똑같은 재사용 메시/버퍼 패턴, 줌 대응 좌우/하단 경계 계산까지 동일하게 적용(줌아웃 시
  빈 공간 생기는 버그를 애초에 피함). `sortingOrder=-5`로 하늘(`-15~-20`)보다 앞, 메인
  지형(기본값 0)보다 뒤에 그려지도록 배치.
- **`GrassTuftGenerator.cs`**: `drawHills()`의 "grass tufts along crest" 부분. 26 캔버스
  유닛 간격으로, 경사가 완만한 곳(`|slope| < 0.5`)에서만 짧은 대각선 잔디 하나씩 — 레퍼런스가
  `ctx.lineWidth`로 그리는 선을, Unity에서는 얇은 사각형(쿼드, 삼각형 2개) 메시로 직접
  만들어서 표현함(틴트 없는 단색 채움). 라인렌더러/오브젝트를 잔디마다 하나씩 만드는 대신
  **재사용 메시 하나에 전부 합쳐서** 매 프레임 다시 채움 — `TerrainGenerator` M7 성능 패스
  때와 같은 이유(매 프레임 GameObject/Instantiate 반복 금지 원칙). `sortingOrder=1`로 메인
  지형 위에 그려짐. 색은 `pal.Grass`(`IslandPalette`에 이미 있던 필드, M7 초기에 팔레트
  포팅할 때 미리 넣어둬서 이번에 바로 씀)를 밤에 `#2a2a4a`로 lerp.
- 둘 다 `GameBootstrapper`에서 `DayCycle` 생성 직후 배선(`SetDayCycle`), `TerrainGenerator`
  와 같은 `Camera.main`/`GameManager.Ground` 참조 패턴.

### 다이브 먼지 / Fever 별 트레일 (`FX/BirdTrailParticles.cs`)
- HTML의 `spawnDust()`(다이빙 중 프레임당 50% 확률)/`spawnStar()`(Fever 중 프레임당 60%
  확률)를 그대로 포팅 — 원본이 60fps 가정 확률이라, `Time.deltaTime*60`으로 스케일해서
  프레임레이트 달라도 기대 발생 빈도가 같도록 함
- `FX/PickupBurst.cs`(한 번 터지고 끝나는 공유 파티클)와 달리, 이건 **새에 매달려 계속
  스스로 판단해서 `Emit(1)`을 호출하는 전용 파티클 시스템 2개**(먼지/별). 새의 자식으로
  붙여서 발사 위치는 새를 따라가지만, `simulationSpace = World`라서 이미 나온 입자는
  새를 따라가지 않고 트레일처럼 뒤에 남음
- 별 모양은 실제 별 폴리곤이 아니라 **작은 원(금색)으로 단순화** — 진짜 5각별 래스터라이즈는
  지금 우선순위 대비 과함, 나중에 원한다면 `BirdVisual`의 픽셀 드로잉 방식으로 추가 가능

### 카메라 줌 (`FX/CameraZoom.cs`)
- 원래 스펙(1단계에서 미룬 항목)은 "최대 뷰포트 15% 줌아웃"이었는데, **"병아리가 화면
  밖으로 사라지면 안 된다"는 요청으로 방식을 바꿈** — 고정 퍼센트 상한 대신, 병아리의
  실제 월드 Y 위치가 항상 카메라 시야 안(`[-orthographicSize, +orthographicSize]`,
  카메라가 Y=0 고정이므로)에 들어오도록 **필요한 만큼** 줌아웃함
  - `neededHalfHeight = max(baseOrthoSize, |birdY| * heightMultiplier + margin)`, 안전장치로
    `baseOrthoSize * 4`를 절대 상한으로 둠 (물리 버그로 비정상적으로 높이 튀는 경우 대비용,
    평소 게임플레이에서 걸릴 일은 없음)
  - 줌아웃/줌인 스무딩 시간을 다르게 줌 (`zoomOutSmoothTime=0.12`로 빠르게 따라잡고,
    `zoomInSmoothTime=0.35`로 천천히 복귀) — 급상승엔 즉각 반응하되 복귀는 부드럽게
  - **튜닝**: "줌아웃이 좀 더 커야 할 것 같아" 피드백으로 `heightMultiplier`(신규, 기본
    1.6)를 추가함 — 원래는 `|birdY| + margin`으로 병아리가 화면에 "딱 맞게만" 들어오는
    수준이었는데, 이제 병아리 높이에 비례해서 여유를 더 두고 줌아웃하도록 배율을 곱함 (플랫
    마진만으로는 높이 올라갈수록 상대적으로 여유가 줄어드는 효과라 배율 방식으로 바꿈).
    더 커야/작아야 하면 이 값 하나로 조절.
- **카메라는 여전히 `transform.position`을 절대 안 움직임** — `orthographicSize`만 바뀜
  (프로젝트 전체가 "카메라 고정 + 월드가 스크롤" 모델이라 위치는 M1부터 고정 원칙 유지)

**첫 구현(고정 임계값 방식)에서 줌이 거의 안 보였던 이유 (지금은 해당 없음, 기록만
남김)**: `BirdController.FixedUpdate`에서 `HeightAboveGround`를 `gm.AdvanceScroll()`
호출 이후의 `gm.ScrollX`로 계산해서, 그 프레임의 `physics.CanvasY`(스크롤 전 X 기준)와
다른 지점을 비교하던 진짜 버그가 하나 있었고(`scrollXBeforeAdvance`로 수정), 임계값
자체도 이 게임의 실제 물리 스케일보다 너무 높게 잡혀 있었음. 지금 방식은 임계값이 아예
없어서(병아리 위치를 직접 기준으로 삼음) 이 클래스의 튜닝 문제 자체가 구조적으로
사라졌지만, 버그 수정은 `HeightAboveGround`가 다른 곳(HUD 디버그 표시 등)에서도 쓰이므로
그대로 유효함
- 검증용으로 `UI/HUD.cs` 우하단 디버그 텍스트에 `height`/`zoom` 실수치를 항상 표시함

### 줌 도입하면서 같이 고친 것 — 화면 가장자리 빈 공간 버그
- `TerrainGenerator`/`CoinSpawner`/`CloudSpawner`는 전부 "기준 줌(orthographicSize =
  viewHeight/2)일 때의 화면 폭"(`ScreenSpace.ViewWidth`)만큼만 지형을 그리고 코인/구름을
  생성/컬링하고 있었음. 줌아웃하면 실제 보이는 영역이 더 넓어지는데 그 계산은 그대로라서,
  화면 가장자리에 지형이 안 그려지는 빈 공간이 생길 뻔했음
- **카메라가 한 위치에 고정돼 있으므로, 줌아웃은 좌우로 "대칭"으로 더 넓게 보여줌** — 오른쪽
  뿐 아니라 왼쪽으로도 기존에 안 그리던 영역이 필요함. `ScreenSpace.LeftEdgeCanvasX`/
  `RightEdgeCanvasX`(신규)가 현재 `orthographicSize` 기준으로 실제 좌/우 경계를
  캔버스좌표로 계산해줌 — 세 스크립트 전부 이걸로 교체
- `BirdController`의 새 시작 X 위치(`width*0.28`) 계산은 **그대로 기준 폭 사용** — 이건
  줌과 무관하게 "화면의 28% 지점"이라는 고정 디자인값이라 안 바꿈
- **세로 방향에도 같은 종류의 버그가 있었음(뒤늦게 발견, `heightMultiplier` 도입 후 눈에
  띔)**: `TerrainGenerator`의 언덕 메시 하단 채움선이 `-viewHeight*0.5f - fillDepth`로
  **기준 줌 기준 고정값**이었음 — 좌우 경계는 이미 줌에 맞춰 고쳤었는데 이 세로 하단선은
  놓치고 있었음. 줌아웃해서 화면이 커지면 이 고정된 하단선이 실제 화면 하단보다 위에 위치할
  수 있어서, 언덕 아래로 배경색이 드러났다가 줌인하면 다시 안 보이는 증상으로 나타남
  ("줌아웃 시 언덕 하단 이미지가 화면 밖으로 나갔다가 들어오는 느낌" 피드백). **고침**:
  `bottomY = -cam.orthographicSize - fillDepth`로, 매 프레임 **현재** 줌 레벨 기준으로
  계산하도록 변경 — 좌우 경계와 동일한 패턴. **교훈: 줌이 가변적인 화면에서는 화면 경계에
  닿는 모든 지오메트리(좌/우뿐 아니라 위/아래도)를 전부 현재 `orthographicSize` 기준으로
  계산해야 함 — 하나라도 기준값(baseline)에 고정해두면 줌 레벨에 따라 드러나는 잠재
  버그가 됨.**

## 7단계 — 다국어(한/영) + 닉네임 — M1~M7 계획 이후 추가 (원래 최초 스펙에 있던 항목)

원래 최초 요청 스펙에 "닉네임 생성, 랭킹, 다국어"가 있었지만 M1~M7 마일스톤 계획에는 포함되지
않았던 항목 — M7까지 다 끝난 뒤에 마저 구현. 시작 전에 두 가지를 확인함: **다국어는 한/영
2개국어 + 설정(시작 화면)에서 전환 가능**, **닉네임은 자동 생성 + 시작 화면에서 재생성/직접
입력 가능**.

### 다국어 (`Core/Localization.cs`)
- **싱글톤 아님** — 이 프로젝트 컨벤션(GameManager/ScoreManager/SaveSystem만 싱글톤 허용)에
  따라 정적 클래스로 구현. `Language` enum(Korean/English) + `Localization.Current`
  (프로퍼티, `SaveSystem.Data.language`에 영구 저장) + `Localization.Get(key)` 조회 함수 +
  `Localization.OnLanguageChanged` 이벤트로 구성
- **범위를 의도적으로 좁힘**: HUD의 토스트/디버그 텍스트("GREAT SLIDE", "STREAK RESET",
  "Island {0} · {1}x" 등)는 애초에 프로젝트 초반부터 영어로 쓰여 있었던 스타일적 선택이라
  한/영 전환해도 똑같이 보임 — 번역 테이블에 넣을 이유가 없어서 그대로 문자열 리터럴로 둠.
  **실제로 번역 테이블에 들어간 건 진짜 한글이었던 것들만**: 시작 화면 부제/버튼/패널 헤더,
  Day Over 화면 타이틀/버튼, HUD의 Nest 헤더, 미션 설명(`MissionPool`), 새 이름/Perk 설명
  (`BirdPool`) — CLAUDE.md보다 `Localization.cs`의 `Table` 딕셔너리 자체가 최신 소스
- **미션/새 설명을 "저장된 문자열"에서 "읽을 때 계산하는 프로퍼티"로 바꿈**:
  `MissionDefinition.Description`은 이제 `Type`만으로 `Localization.Get($"mission.{Type}")`
  포맷 템플릿을 찾아서 `Target`을 채워 넣는 계산된 프로퍼티(`Type`+`Target` 하나당 번역
  키 하나가 아니라 `Type`당 하나 — 어떤 `Target` 값이 와도 재사용됨). `BirdDefinition.Name`/
  `PerkDescription`도 같은 방식(`Id`/`Perk` 기반). **두 struct 다 생성자에서 설명 문자열
  파라미터가 사라짐** — `MissionPool`/`BirdPool`의 배열 초기화 코드도 그에 맞춰 짧아짐
- **언어가 바뀌면 즉시 화면에 반영되는 방식**: HUD/StartScreen/DayOverScreen 대부분의
  텍스트는 이미 매 프레임 다시 쓰이고 있어서(점수, 미션 진행률 등) 언어를 바꾸면 다음
  프레임에 저절로 새 언어로 바뀜. `Build*()` 시점에 딱 한 번만 설정되고 그 뒤로 안 건드리는
  라벨(부제, 버튼 텍스트, 헤더 등)만 별도로 `RefreshStaticLabels()`에 모아서, 빌드 시점 +
  `Localization.OnLanguageChanged` 이벤트 양쪽에서 호출 — 각 화면 파일에 어떤 라벨이 여기
  해당하는지 주석으로 남겨둠
- 시작 화면 우상단에 언어 전환 버튼 추가(코인/기록 버튼 아래) — 버튼 라벨은 **지금 언어가
  아니라 눌렀을 때 바뀔 언어**를 보여줌(한국어 모드에선 "English", 영어 모드에선 "한국어")

### 닉네임 (`Meta/PlayerProfile.cs`, `Meta/NicknameGenerator.cs`)
- `PlayerProfile`도 싱글톤 아님(같은 이유) — `SaveSystem.Data.nickname`에 저장, 최초 실행
  시 `NicknameGenerator.Generate()`로 "형용사+병아리+숫자"(예: "용감한 병아리123" /
  "BraveChick123") 자동 생성. 단어 목록은 **생성되는 그 순간의** `Localization.Current`
  기준(한국어 모드면 한글 형용사, 영어 모드면 영어 형용사) — 이후 언어를 바꿔도 이미 정해진
  닉네임 자체는 안 바뀜(정체성이지 UI 텍스트가 아니므로), 재생성/직접입력으로만 바뀜
- 시작 화면 좌상단에 `TMP_InputField`(신규 `UIFactory.CreateInputField` 헬퍼로 런타임
  조립 — Editor의 "Create > UI > Input Field - TextMeshPro"와 같은 구조를 코드로 직접 만듦)
  + "재생성" 버튼. 입력 필드는 포커스를 잃을 때(`onEndEdit`) `PlayerProfile.SetNickname()`
  호출 — 빈 문자열은 무시, 16자로 잘라서 저장(레이아웃 안 깨지게)
- **스페이스바 버그 주의해서 막음**: `StartScreen.Update()`가 스페이스바를 폴링해서
  런 시작을 트리거하는데, 닉네임에 공백이 들어간 걸 입력하려고 스페이스바를 누르면 동시에
  게임이 시작돼버릴 뻔함 — `nicknameField.isFocused`일 땐 스페이스바 폴링을 건너뛰도록 가드
- **스코프에서 뺀 것**: 닉네임을 로컬 리더보드 각 줄에 붙이지 않음 — 지금 리더보드는 같은
  플레이어 한 명의 기록만 쌓이는 로컬 Top 10이라, 모든 줄에 같은 이름을 반복해서 붙여봤자
  정보가 안 늘어남. 나중에 공유/온라인 랭킹으로 확장되면 그때 다시 볼 것(`PlayerProfile.cs`
  주석에도 기록)

## 8단계 — 온라인 계정 / 점수 랭킹 (`FlyingChick-Server`, Phase A+B 완료 / Phase C 대기)

사용자가 회원가입/로그인(아이디+비밀번호), 서버 발급 고유 닉네임, 점수 랭킹 집계 화면을
요청 — 별도 Python FastAPI + MySQL 백엔드를 Docker로 만들고, Unity 클라이언트에 연동. 계획
문서: `/Users/jiconst/.claude/plans/glowing-floating-owl.md` (Phase A/B/C로 분할해서 승인
받음). **핵심 원칙: 로그인은 항상 선택 사항 — 서버가 죽어 있거나 아예 없어도 게임은 100%
오프라인으로 그대로 동작해야 함.** 이 원칙이 깨지면 안 되는 게 이 8단계 전체에서 가장 중요한
불변 조건.

### 백엔드 (`~/src/FlyingChick-Server`, Unity 프로젝트와 완전히 별개의 Python 저장소, 자체 git repo)
- FastAPI(최신) + Pydantic v2 + SQLAlchemy 2.0(동기, PyMySQL) + Alembic 마이그레이션 +
  MySQL 8.4(도커, `:latest` 아님 — 최근 `latest` 태그가 불안정한 "innovation" 릴리스
  라인을 가리켜서 명시적으로 `8.4` LTS 고정) + bcrypt(패스워드 해시, 72바이트 제한 직접
  처리) + PyJWT(`>=2.12.0`, CVE 대응) 조합. 세부 근거는 계획 문서의 "Plan-review" 절 참고
  (async SQLAlchemy/asyncmy가 더 최신이지만, 이 규모에서는 동기+PyMySQL로 충분하다고 판단—
  `README.md`에 향후 업그레이드 경로로 메모해둠)
- 엔드포인트: `/auth/signup`(닉네임 자동 발급), `/auth/login`, `/auth/me`,
  `/auth/nickname/reroll`, `/auth/nickname`(PUT, 직접 지정), `/scores`(POST, 인증 필요),
  `/rankings?period=daily|weekly|alltime`(공개), `/rankings/me`(인증 필요) — 표/스키마는
  `README.md`에 정리돼 있음
- **랭킹은 스케줄 집계가 아니라 쿼리 시점에 윈도우 함수(`ROW_NUMBER() OVER (PARTITION BY
  user_id ORDER BY score DESC, created_at ASC)`)로 유저당 최고 점수 1개만 뽑아서 계산** —
  이 규모에서는 별도 집계 테이블/배치잡이 오히려 과함
- **Docker 빌드/실행 중 실제로 발견해서 고친 버그**: MySQL 컨테이너가 최초 부팅 시 내부적으로
  한 번 재시작하는데, 그 사이 healthcheck(`mysqladmin ping`)는 이미 "healthy"라고 보고해서
  `alembic upgrade head`가 "Connection refused"로 죽는 레이스 컨디션이 있었음(`depends_on:
  condition: service_healthy`로도 못 막음). **고침**: `Dockerfile`의 CMD를 `until alembic
  upgrade head; do sleep 2; done && uvicorn ...` 재시도 루프로 변경 — 볼륨까지 지우고
  클린 재빌드해서 재발 확인함(재시도 로그만 찍히고 컨테이너 재시작 없이 한 번에 정상
  기동되는 것 확인)
- **curl로 전체 플로우 직접 검증 완료**: 회원가입/중복가입(409)/로그인/틀린 비밀번호(401)/
  약한 비밀번호(422)/점수 제출/랭킹 3종(daily·weekly·alltime, 유저별 최고점만 반영되고
  순위 역전도 정확)/내 순위/닉네임 재생성·직접변경/닉네임 중복(409)/미인증 접근(401) 전부
  기대한 대로 동작
- Phase A 완료 시점에 `~/src/FlyingChick-Server`를 git init + 최초 커밋함(별도 repo,
  Unity 프로젝트 git과 무관)

### Unity 네트워킹 레이어 (`Assets/Scripts/Network/`, 신규 폴더)
- **`ApiClient.cs`**: `UnityWebRequest` 래퍼. 이 프로젝트 전체가 `async`/`await`를 안 쓰고
  코루틴+이벤트/콜백 스타일이라(`SlideJudge`/`FeverSystem` 등), 여기도 코루틴 +
  `Action<ApiResult<T>>` 콜백 방식으로 통일 — 굳이 새 비동기 스타일을 프로젝트에 들여오지
  않음. 실패는 항상 `ApiResult.Success == false`로만 보고(예외 던지지 않음) — 로그인이
  선택 사항이라 서버 응답 실패를 "정상적으로 있을 수 있는 일"로 다뤄야 함
- **DTO는 `JsonUtility`로 직렬화**(로컬 세이브와 같은 방식, 새 JSON 라이브러리 없음) —
  `JsonUtility`는 필드명을 그대로 JSON 키에 매핑하고 리네이밍 속성을 지원 안 해서,
  `Network/ApiModels.cs`의 DTO 필드들은 일부러 C# 관례(PascalCase) 대신 서버 JSON 키와
  맞춘 snake_case(`login_id`, `access_token` 등)로 씀
- **`AuthService.cs`**: 싱글톤 아님(GameManager/ScoreManager/SaveSystem만 허용 원칙 유지) —
  `GameBootstrapper`가 `CoinWallet`/`BirdCollection`과 같은 패턴으로 생성/배선.
  `IsLoggedIn`/`ServerNickname` 상태 + `OnLoggedIn`/`OnLoggedOut`/`OnAuthError` 이벤트.
  **여기서 관리하는 "서버 닉네임"은 `Meta/PlayerProfile.cs`의 로컬 닉네임과 완전히 다른
  개념** — 일부러 합치지 않음(오프라인 세이브 데이터와 온라인 계정을 얽히게 하고 싶지
  않았음). UI에서도 "온라인: {닉네임}"처럼 구분되게 표시
- **`RankingService.cs`**: `/rankings`, `/rankings/me` 래핑 — 실제 UI 소비(Day Over 랭킹
  패널)는 아직 Phase C 대기 중, 이번엔 네트워킹 레이어만 완성
- **토큰 저장**: `SaveData.authToken`(신규 필드)에 평문 JSON으로 저장 — **보안 저장소
  아님**(Keychain/Keystore 아님), 이 하비 프로젝트 위협 모델에서는 허용 가능한 수준으로
  판단, 나중에 필요해지면 손볼 항목으로 코드 주석에 남김. 앱 시작 시(`GameBootstrapper`)
  저장된 토큰이 있으면 `GET /auth/me`로 유효성 검증 — 성공하면 로그인 상태 복원, 실패
  (만료/서버 다운 등)하면 조용히 로그아웃 상태로 폴백(**절대 시작을 막지 않음**)
- **`UIFactory.CreateInputField`에 비밀번호 모드 추가**(`password: true` 파라미터) —
  `TMP_InputField.ContentType.Password`로 마스킹

### 시작 화면 로그인/회원가입 UI (`StartScreen.cs`)
- 우상단에 코인/기록보기/언어전환 버튼 아래 네 번째 버튼 — **로그아웃 상태일 땐 "로그인"
  버튼(누르면 로그인/회원가입 폼 모달이 뜸), 로그인 상태일 땐 "{서버 닉네임} · 로그아웃"
  으로 라벨과 동작이 같이 바뀜** — 버튼 슬롯 하나로 상태 두 개를 다 처리(상태 텍스트를
  따로 안 두고 단순화)
- 로그인/회원가입 폼은 기존 리더보드 패널과 같은 토글 그룹 패턴(백드롭 + 화면 중앙 고정
  크기 패널, 리사이즈 시 패널만 재중앙정렬) — 아이디/비밀번호 입력 필드(비밀번호는 마스킹),
  "로그인"/"회원가입" 버튼 둘 다 같은 두 필드로 동작(입력값 재사용), 에러 메시지 텍스트(서버
  `{"detail": "..."}` 메시지를 그대로 보여줌, 별도 번역 안 함 — 서버 에러 문자열까지
  다국어화하는 건 이번 스코프 밖), 닫기 버튼
- **닉네임 입력 필드에 이미 적용했던 스페이스바 가드를 아이디/비밀번호 필드에도 확장** —
  두 필드 중 하나라도 포커스 상태면 스페이스바로 런이 시작되지 않도록
- 로그인/회원가입 성공(`AuthService.OnLoggedIn`) 시 폼을 자동으로 닫고 에러 메시지를 지움

### 남은 것 (Phase C, 아직 안 함)
- Day Over에서 로그인 상태면 그 런의 점수를 자동으로 `POST /scores` (실패해도 토스트만
  띄우고 계속 진행 — 오프라인 재시도 큐는 이번 스코프 밖)
- Day Over 화면에 "온라인 랭킹" 토글 패널(일간/주간/전체 탭 + 내 순위 강조), 로그아웃
  상태면 랭킹 대신 로그인 유도 문구

## 9단계 — 시작 화면 메인 메뉴 재구성 + 사운드 설정

시작 화면이 닉네임/코인/새 선택/로그인/언어/기록 버튼까지 한 화면에 다 몰려 있어서
복잡해짐 — 메인 메뉴를 새로 만들고 기존 내용을 하위 화면으로 재배치. **이 기능은 같은
세션에서 두 번 설계가 바뀌었음** — 처음엔 "게임플레이/설정/게임 방법/기록" 4버튼 +
게임플레이 화면(탭하면 시작)이었는데, 사용자가 참고 스크린샷(Tiny Wings류 게임의
PLAY/SETTINGS/STATS/HOW TO PLAY 카드형 메뉴)을 보여주면서 방향을 바꿈: **PLAY는 화면
전환 없이 바로 게임 시작**, 코인/새 선택/일일미션은 **STATS 버튼 하위 탭**으로,
설정(회원가입)은 **로그인/회원가입 선택 → 위자드** 형태로. 아래는 **최종(두 번째) 설계
기준** — 첫 번째 설계는 코드에 안 남아 있음(전면 재작성됨).

시작 전에 확인한 것: 카드형 배경/버튼 배치는 스크린샷과 비슷하게 하되 베벨/하이라이트
같은 질감은 재현 안 함(지금 프로젝트의 플랫 스타일 유지) · PLAY 화면에 있던 메타 콘텐츠는
STATS 하위 탭으로 통합 · 설정 진입 시 로그인/회원가입 선택 먼저.

### 화면 구조 (`StartScreen.cs`, 전면 재작성)
- 바깥쪽 `Panel` enum(MainMenu/Settings/HowToPlay/Stats)이 어느 화면이 떠 있는지 관리 —
  `SwitchTo(Panel)`가 네 그룹 GameObject 중 하나만 활성화. Settings/Stats는 각자 안에
  또 하위 상태(`SettingsView`/`StatsTab`)를 갖는 중첩 구조
- **메인 메뉴**: 타이틀 + Best 점수 + 카드 배경 패널 안에 세로로 쌓인 4개 버튼(PLAY/설정/
  기록/게임 방법). **PLAY만 강조색**(빨간 계열)으로 나머지(주황 계열)와 구분 — 참고
  스크린샷의 색 배치를 흉내냄. 카드+타이틀+Best 전체가 화면 정중앙에 오도록 배치
  (`ReflowMainMenu`)
- **PLAY**: `SwitchTo` 안 씀 — 클릭하면 바로 `GameManager.Instance.BeginRun()`. 예전
  버전에 있던 "빈 곳 탭하면 시작" 캐처(`TapCatcher`)는 완전히 제거함(더 이상 어떤
  화면에서도 탭-시작이 없음 — 항상 버튼으로만 시작) — `UIFactory.CreateFullScreenTapCatcher`
  헬퍼도 이제 아무도 안 써서 같이 삭제. 스페이스바는 메인 메뉴에 있을 때만 PLAY와 동일하게
  동작하도록 유지(`currentPanel == Panel.MainMenu`일 때만 폴링 — 이 프로젝트 스펙의
  "터치+마우스+스페이스바" 요구사항을 버튼 기반 메뉴에서도 지키기 위함)
- **기록(Stats) 화면**: 예전 게임플레이 화면에 있던 콘텐츠가 전부 여기로 옮겨옴. 안에
  탭바 3개(기록/새 선택/일일미션, `StatsTab` enum) + 뒤로가기:
  - **기록 탭**: 기존 로컬 Top 10 리더보드 패널 그대로(이제 모달이 아니라 탭 콘텐츠)
  - **새 선택 탭**: 로컬 닉네임 입력+재생성(`PlayerProfile`), 코인 총액, 새 선택 줄 + 알
    구매, 부화 토스트
  - **일일미션 탭**: 오늘의 미션 진행률
  - 탭 버튼은 선택된 탭만 배경 알파를 밝게 해서 어느 탭이 활성인지 살짝 구분
- **설정 화면**: 내부에 `SettingsView` enum(환경설정/로그인·회원가입 선택/아이디+비번/
  회원가입 닉네임 확인) 하위 상태 머신:
  1. **환경설정**: 음악/효과음 볼륨 슬라이더, 언어 전환, 로그인 상태면 "{서버 닉네임} +
     로그아웃", 로그아웃 상태면 "로그인 / 회원가입" 버튼(→ 2번으로), 뒤로가기(메인 메뉴)
  2. **로그인/회원가입 선택**: "로그인" / "회원가입" 두 버튼 — 어느 쪽을 눌렀는지
     `isSignupFlow` bool에 기억해두고 3번으로
  3. **아이디+비밀번호**: 로그인/회원가입 공용 입력 폼(필드 재사용) — 제출 버튼 라벨이
     `isSignupFlow`에 따라 "로그인"/"회원가입"으로 바뀜. 회원가입이면 `AuthService.Signup`
     호출(서버가 고유 닉네임 자동 발급 + 로그인까지 한 번에 됨) 후 4번으로, 로그인이면
     `AuthService.Login` 성공 시 바로 메인 메뉴로 복귀
  4. **회원가입 닉네임 확인**(회원가입일 때만 거침): 3번에서 이미 로그인된 상태 —
     서버가 자동 발급한 닉네임을 입력 필드에 보여주고 직접 수정 가능(`SetNickname`) +
     "재생성"(`RerollNickname`) 버튼, "완료" 누르면 메인 메뉴로. **서버 엔드포인트를 새로
     만들 필요 없이 기존 signup/reroll/set-nickname 엔드포인트를 순서대로 이어붙인 것뿐**
  - `AuthService.OnLoggedIn` 핸들러(`HandleLoggedIn`)가 `isSignupFlow`를 보고 4번으로
    갈지 바로 메인 메뉴로 갈지 분기 — 이 이벤트는 앱 시작 시 저장된 토큰 검증
    (`ValidateStoredToken`) 성공 때도 울리는데, 그땐 `isSignupFlow`가 기본값 false라서
    자연스럽게 "메인 메뉴로"만 타고, 이미 메인 메뉴에 있는 상태라 실질적으로 아무 일도
    안 일어남(멱등)
  - **`AuthService`에 `OnNicknameChanged` 이벤트 신규 추가**(재생성/직접변경/로그인 성공
    시 발생) — 닉네임 입력 필드 텍스트를 매 프레임 강제로 덮어쓰면 사용자가 타이핑
    중인 값을 지워버리는 버그가 생기므로, `PlayerProfile.OnNicknameChanged`와 동일한
    이벤트 기반 동기화 패턴을 그대로 따름
- **게임 방법 화면**: 정적 설명 텍스트(조작법 + Great Slide/Fever/코인/구름/섬 배수 규칙
  요약, 한/영 둘 다 `Localization.cs`의 `howtoplay.body` 키에 있음), 뒤로가기
- **스킵한 것**: 참고 스크린샷에 있던 "MORE GAMES!"/SNS 공유/광고 제거 버튼은 이 프로젝트에
  해당 사항 없어서 구현 안 함

### 사운드 설정 (`Audio/AudioManager.cs`, `Meta/SaveData.cs`)
- 기존엔 볼륨 조절 기능 자체가 없었음(`sfxVolume`/`bgmVolume`이 고정 `[SerializeField]`
  값) — `AudioManager.SetMusicVolume(float)`/`SetSfxVolume(float)` + `MusicVolume`/
  `SfxVolume` 프로퍼티 추가, `SaveData.musicVolume`/`sfxVolume`(기본값은 기존
  `AudioManager` 기본값과 동일하게 0.16f/0.6f)에 저장. `Awake()`에서 오디오 소스를
  만들기 전에 저장된 값부터 불러오도록 순서 조정(안 그러면 `bgmSource.volume`에 저장된
  값이 아니라 인스펙터 기본값이 들어감)
- **`UI/UIFactory.cs`에 `CreateSlider` 헬퍼 신규 추가** — Editor의 "Create > UI >
  Slider"가 만드는 것과 같은 구조(배경 + Fill Area/Fill + Handle Slide Area/Handle)를
  코드로 조립. 0~1 범위 고정(볼륨 용도라 그 외 범위 필요 없음)

### 한글 주석으로 전환
이 단계부터 새로 쓰는 코드 주석은 한글로 작성함(사용자 요청). 기존에 영문으로 쓰여 있던
주석들은 소급 번역하지 않음 — 별도로 요청하지 않는 한 그대로 둠.

## 구현 우선순위 / 진행 상황

1. **M1 — 플레이 코어**: ✅ 완료 (2026-08-13). 지형 메시 + 새 물리 + 입력, 프로시저럴
   병아리 비주얼. 플레이 확인 완료.
2. **M2 — 판정/점수**: ✅ 완료 (2026-08-17). Great Slide, streak, Fever, 섬 진행/배수,
   OnGUI 기반 점수 HUD. 플레이 확인 완료 (임계값/언덕폭 재튜닝 + micro-hop 버그 수정까지
   거쳐 확정).
3. **M3 — 수집물**: ✅ 완료 (2026-08-17). 코인/스피드코인/구름 + 픽업 파티클(공유
   ParticleSystem) + 월드 좌표 팝업 텍스트. 플레이 확인 완료 (스피드코인 배치 높이 재조정
   포함).
4. **M4 — 게임 루프**: ✅ 완료 (2026-08-17). Start/Playing/DayOver 상태 머신, 낮/밤 타이머 +
   하늘색 lerp, 시작/Day Over 화면, 최고점수 저장. 플레이 확인 완료 (진행바 색/해 아이콘
   튜닝 포함).
5. **M5 — 메타**: ✅ 구현 완료 (2026-08-17). 코인 지갑(JSON 저장), 데일리 미션(하루 누적,
   `DailyMissions`), Nest Multiplier(런당 3목표 → 영구 배수 보너스, `NestMultiplier`).
   **플레이 확인은 아직 안 됨** — M6 요청이 바로 이어져서 건너뜀.
6. **M6 — 컬렉션**: ✅ 구현 완료 (2026-08-17). 새 5종 + Perk(`BirdPool`/`BirdCollection`), 알
   가챠(500코인), 홈 화면 새 선택 줄, 로컬 Top 10 리더보드(`Leaderboard`). **여기서 M5+M6
   둘 다 플레이 확인 필요 — 확인 후 M7 진행.**
7. **M7 — 폴리시**: ✅ 완료 (2026-08-18). 카메라 줌, 다이브 먼지/Fever 별 트레일, 하늘
   그라데이션/태양·달·별/섬별 10색 팔레트, 프로시저럴 합성 사운드(`Audio/`, 신호음 수준 —
   실제 오디오 에셋 아님), 성능(GC 할당 정적 리뷰 기반 제거), OnGUI→UGUI/TextMeshPro 교체
   (`UI/UIFactory.cs`/`UI/UIFontProvider.cs` 신규, 한글 렌더링 포함) 전부 플레이 확인 완료.
   **계획했던 M1~M7 전 마일스톤이 이제 완료 상태.** 추가로, M7에서 스킵했던 뒷배경 패럴랙스
   언덕 + 잔디 tuft(`Terrain/BackgroundHillGenerator.cs`/`Terrain/GrassTuftGenerator.cs`
   신규)도 마저 구현함 — 플레이 확인 완료.
8. **포스트-M7 — 다국어/닉네임**: ✅ 구현 완료 (2026-08-18). 한/영 전환(`Core/Localization.cs`,
   시작 화면 우상단 토글 버튼), 자동 생성/재생성/직접입력 가능한 닉네임(`Meta/PlayerProfile.cs`,
   `Meta/NicknameGenerator.cs`, 시작 화면 좌상단 입력 필드). **플레이 확인 필요** — 특히
   언어 전환 시 화면 전체(시작/HUD/Day Over/미션/새 이름) 텍스트가 다 같이 바뀌는지, 닉네임
   입력 중 스페이스바로 런이 실수로 시작되지 않는지.
9. **온라인 계정/랭킹**: 🚧 Phase A(백엔드)+B(네트워킹/로그인 UI) 완료 (2026-08-18), **Phase
   C(점수 자동 제출 + Day Over 랭킹 패널)는 아직 안 함**. 백엔드는 curl로 전체 플로우
   검증 완료. Unity 쪽은 **플레이 확인 전혀 안 됨** — `docker compose up`으로 로컬 서버를
   띄운 상태에서 시작 화면의 로그인/회원가입 폼, 로그인 후 닉네임 표시/로그아웃, 서버가
   꺼져 있을 때도 오프라인 플레이가 100% 정상 동작하는지(가장 중요한 불변 조건)를 반드시
   확인할 것.
10. **시작 화면 메인 메뉴 재구성**: ✅ 구현 완료 (2026-08-18), **같은 세션에서 설계가 한 번
    바뀜** — 최종 버전은 PLAY 버튼이 화면 전환 없이 바로 게임을 시작하고, 코인/새 선택/
    일일미션이 STATS 화면의 탭 3개로 통합되고, 설정에서 로그인/회원가입 선택 → 회원가입
    위자드(아이디+비번 → 닉네임 확인) 흐름으로 계정을 만듦. 음악/효과음 볼륨 슬라이더
    (저장됨). 자세한 구조는 위 "9단계" 절 참고. **플레이 확인 전혀 안 됨** — 특히 PLAY가
    바로 게임을 시작하는지, STATS 탭 3개 전환과 뒤로가기, 설정→로그인/회원가입 선택→
    회원가입 위자드 전체 흐름(계정 생성 → 닉네임 확인/재생성/직접수정 → 완료 → 메인
    메뉴 복귀), 볼륨 슬라이더가 실제로 소리를 바꾸고 재시작 후에도 유지되는지 확인 필요.

## 하지 말 것

- Rigidbody2D / PolygonCollider2D 기반 물리 (지형 샘플링 방식과 충돌함)
- 지형 청크 오브젝트 스폰/삭제 (단일 메시 재사용 — `TerrainGenerator`가 이미 이렇게 함)
- Update에서 물리 적분 (FixedUpdate 사용 — `BirdController`가 이미 이렇게 함)
- Instantiate/Destroy 반복 (코인·구름은 고정 풀 재사용, 파티클은 공유 ParticleSystem —
  `Collectibles/`, `FX/PickupBurst.cs` 참고. 앞으로 추가되는 것도 이 패턴 따를 것)
- 원작 Tiny Wings의 아트 에셋·사운드 복제 (스타일 참고만, 에셋은 전부 자체 제작)
- 인앱 결제/광고/서버 연동 (전부 로컬)
- `groundY`/`groundSlope`/물리 상수를 "정리"한답시고 값 바꾸지 말 것 — HTML 원본과 어긋나면
  검증된 손맛이 깨짐. 바꾸고 싶으면 먼저 `flying-chick.html`에서 값을 바꿔 손맛을 확인한 뒤
  포팅할 것.

## 테스트 체크리스트

- [x] 내리막 다이브 → 정점 발사 → 착지가 HTML 프로토타입과 유사한 손맛인지 (M1)
- [x] streak 3 → Fever 발동, 실패 착지 → 즉시 종료 확인 (M2)
- [x] 섬 전환 시 배수/속도킥 동작, HUD의 Island/배수/점수/Fever뱃지/streak 점이 맞는지 (M2)
- [x] 코인/스피드코인/구름 픽업이 실제로 닿는 높이에 배치되는지 (M3)
- [x] 90초 후 Day Over, 재시작 시 상태 완전 초기화(점수/streak/Fever/코인·구름/지형까지
      전부 새로 시작하는지) (M4)
- [x] Day Over에서 "홈" → 시작 화면 → 다시 시작이 정상 동작하는지 (M4)
- [x] 앱 재시작 후에도 Best 점수가 유지되는지 (M4)
- [ ] 코인이 Day Over에서 실제로 지급되고 시작 화면 총액에 반영되는지 (M5, 플레이 확인
      대기 중)
- [ ] Nest 목표 3개가 런마다 새로 뽑히고, 플레이 중 HUD·Day Over에서 진행/결과가 맞는지,
      3개 전부 통과했을 때 다음 런부터 배수가 실제로 +1 되는지 (M5)
- [ ] 데일리 미션이 여러 런에 걸쳐 누적되고, 완료 시 코인 100개 지급되는지 (M5)
- [ ] 앱을 껐다 켜도(같은 날) 데일리 미션 진행/코인/Nest 배수가 유지되는지 (M5 — JSON
      저장이라 `Application.persistentDataPath`의 `flyingchick_save.json` 확인 가능)
- [ ] 홈 화면 새 아이콘 클릭 시 선택되고(흰 테두리), 실제 플레이에 그 새 색상이 반영되는지
      (M6)
- [ ] 알 구매(500코인) 시 코인이 차감되고, 못 가진 새 중 하나가 무작위로 부화하는지, 코인
      부족할 땐 아무 일도 안 일어나는지 (M6)
- [ ] 각 새의 Perk가 실제로 게임플레이에 반영되는지 (빨강=슬라이드 점수 +10%, 파랑=Fever
      +1초, 초록=코인 반경 +20, 보라=시작 속도 +50) (M6)
- [ ] **홈 화면에서 새 아이콘/알 구매/기록 버튼을 눌렀을 때 실수로 게임이 시작되지 않는지**
      (버튼 영역 클릭 차단 로직, M6 — 가장 깨지기 쉬운 부분이라 꼭 확인)
- [ ] "기록 보기" 패널에 Top 10 점수, 총 슬라이드, 총 비행일 수가 맞는지, 여러 런 이후
      순위가 정렬되는지 (M6)
- [ ] 60fps 유지
- [ ] **한글 UI 텍스트가 전부 정상 렌더링되는지**(두부/빈 사각형 없이) — `UIFontProvider`의
      런타임 폰트 생성이 실제로 동작하는지 확인하는 가장 중요한 체크 (M7)
- [ ] 시작 화면에서 빈 곳 탭/클릭/스페이스바 전부 런 시작이 되는지, 버튼(기록 보기/알
      구매/새 아이콘/닫기) 위를 탭했을 때는 시작되지 않는지 (M7 — tap catcher 레이어링 검증)
- [ ] Day Over 화면 "다시하기"/"홈" 버튼이 정상 동작하는지, Nest 목표 통과/실패 표시가
      맞는지 (M7 — UGUI 포팅 후 재확인)
- [ ] 창 크기/해상도를 바꿔도 HUD·시작 화면 요소들이 화면 밖으로 안 나가는지 (M7 — 새로
      생긴 리사이즈 가드 로직 검증)
- [ ] 뒷배경 언덕이 전경 언덕보다 느리게 스크롤되는지(패럴랙스), 완만한 크레스트 위에 잔디
      tuft가 보이는지, 섬이 바뀌면 색도 같이 바뀌는지 (M7)
- [ ] 시작 화면 우상단 언어 전환 버튼을 누르면 화면 전체(부제/버튼/헤더/미션 설명/새 이름·
      Perk/리더보드/Day Over 화면까지) 텍스트가 즉시 한/영으로 바뀌는지, 앱을 껐다 켜도
      마지막으로 고른 언어가 유지되는지 (포스트-M7)
- [ ] 시작 화면 좌상단 닉네임이 최초 실행 시 자동 생성되는지, "재생성" 버튼으로 새로
      뽑히는지, 입력 필드에 직접 타이핑해서 바꿀 수 있는지(포커스 벗어나면 저장), 앱을
      껐다 켜도 유지되는지 (포스트-M7)
- [ ] **닉네임 입력 중 스페이스바를 눌러도 게임이 실수로 시작되지 않는지** (포스트-M7 —
      입력 필드 포커스 가드 확인)
- [ ] **`FlyingChick-Server`를 끈 상태(또는 `apiBaseUrl`을 일부러 틀리게)에서도 게임이
      아무 문제 없이 완전히 오프라인으로 플레이되는지** — 온라인 계정 기능 전체에서 가장
      중요한 불변 조건. 시작 화면 진입, 로그인 버튼 클릭 시 서버 에러가 나도 오프라인 UI가
      멀쩡한지 확인
- [ ] `docker compose up`으로 로컬 서버를 띄운 상태에서: 회원가입 → 자동 로그인 →
      우상단 버튼이 "{닉네임} · 로그아웃"으로 바뀌는지, 앱을 껐다 켜도 로그인 상태가
      유지되는지(`GET /auth/me` 토큰 검증), 로그아웃이 정상 동작하는지, 아이디/비밀번호
      입력 중 스페이스바로 런이 시작되지 않는지 (온라인 계정 Phase B)
- [ ] 틀린 비밀번호/중복 아이디로 로그인·회원가입 시도 시 서버 에러 메시지가 폼에 그대로
      뜨는지 (온라인 계정 Phase B)
- [ ] 시작 화면이 카드형 메인 메뉴(PLAY/설정/기록/게임 방법 4개 버튼, 화면 정중앙)로
      뜨는지, PLAY를 누르면 다른 화면 거치지 않고 바로 게임이 시작되는지 (메인 메뉴 재구성)
- [ ] **메인 메뉴에 있을 때만 스페이스바로 런이 시작되는지 — 설정/게임 방법/기록 화면에서는
      스페이스바를 눌러도 아무 일 없는지** (메인 메뉴 재구성 — "빈 곳 탭하면 시작" 캐처는
      완전히 제거되고 항상 버튼으로만 시작하도록 바뀐 것 검증)
- [ ] 기록(STATS) 화면의 탭 3개(기록/새 선택/일일미션)가 전환되는지, 새 선택 탭에서
      로컬 닉네임 수정/재생성·코인 표시·알 구매·새 선택이 예전과 똑같이 동작하는지, 뒤로가기로
      메인 메뉴 복귀가 되는지 (메인 메뉴 재구성)
- [ ] 설정 화면의 음악/효과음 슬라이더를 움직이면 실제로 소리 크기가 바뀌는지, 앱을 껐다
      켜도 조절한 볼륨이 유지되는지 (메인 메뉴 재구성)
- [ ] **설정 → 로그인/회원가입 선택 → 회원가입 전체 흐름**: 아이디+비밀번호 입력 →
      제출하면 계정이 생성되고 자동 로그인되는지 → 서버가 자동 발급한 닉네임이 다음
      화면에 보이는지 → 재생성/직접입력으로 바꿀 수 있는지(입력 필드가 타이핑 중에
      강제로 안 지워지는지도 함께) → "완료" 누르면 메인 메뉴로 돌아오고 설정 화면에
      로그인 상태가 반영되는지 (메인 메뉴 재구성)
- [ ] 설정 → 로그인/회원가입 선택 → **로그인**(기존 계정)으로 아이디+비밀번호 제출 시
      성공하면 바로 메인 메뉴로 돌아오는지, 실패(틀린 비밀번호 등) 시 에러 메시지가 뜨는지
      (메인 메뉴 재구성)
- [ ] 게임 방법 화면 텍스트가 한/영 전환 시 같이 바뀌는지 (메인 메뉴 재구성)

## 실행 방법

**필수 1회 에디터 설정 (M7부터, "에디터 수동 설정 금지" 원칙의 명시적 예외)**: Play하기 전에
**Window > TextMeshPro > Import TMP Essential Resources**를 한 번 실행해야 함. TMP 내부
코드가 `TMP_Settings` 에셋의 존재 자체를 조건 없이 전제해서, 이게 없으면 우리가 직접 만든
런타임 폰트(`UIFontProvider`)를 쓰든 안 쓰든 상관없이 `NullReferenceException`이 남 — 자세한
경위는 `UIFontProvider.cs` 상단 주석과 위 "OnGUI → UGUI/TextMeshPro 교체" 절 참고. 이때
같이 설치되는 Liberation Sans SDF는 라틴 전용이라 한글 텍스트에는 안 쓰임(우리 코드는 항상
자체 한글 폰트를 명시적으로 꽂음).

**온라인 계정/랭킹 기능을 테스트하려면(선택 사항 — 안 띄워도 게임은 정상 플레이됨)**:
`~/src/FlyingChick-Server`에서 `docker compose up --build`로 로컬 백엔드를 먼저 띄울 것.
`GameBootstrapper`의 `apiBaseUrl` 필드(기본값 `http://localhost:8000`)가 이 서버를 가리킴.
서버를 안 띄워도 로그인 버튼을 누르면 에러 메시지만 뜨고 오프라인 플레이는 그대로 정상
동작해야 함 — 이게 깨지면 버그.

빈 GameObject에 `GameBootstrapper` 컴포넌트만 붙이고 Play — 카메라/지형/새/점수 시스템/HUD/
낮밤 사이클/시작·종료 화면이 전부 런타임에 코드로 조립된다.

1. **시작 화면 — 메인 메뉴**: 타이틀 + Best 점수 + 화면 정중앙 카드 안에 4개 버튼
   (PLAY 강조색 / 설정 / 기록 / 게임 방법). **PLAY를 누르면 다른 화면 거치지 않고 바로
   게임이 시작됨** — 메인 메뉴에 있을 때는 스페이스바로도 시작됨(설정/게임 방법/기록
   화면에서는 스페이스바 눌러도 아무 일 없음). "빈 곳 탭하면 시작"은 더 이상 없음(항상
   버튼/스페이스바로만 시작)
   - **기록(STATS)**: 탭 3개(기록/새 선택/일일미션)
     - 기록 탭: Top 10 + 누적 통계
     - 새 선택 탭: 로컬 닉네임 입력(직접 수정 가능) + "재생성" 버튼, 코인 총액, 보유 새
       선택 줄(클릭해서 선택) + 알 구매 버튼(500코인, 부화 결과 토스트 표시)
     - 일일미션 탭: 오늘의 미션 진행률
     - 뒤로가기로 메인 메뉴 복귀
   - **설정**: 음악/효과음 볼륨 슬라이더(저장됨), 언어 전환 버튼(한/영, 지금 언어가 아니라
     눌렀을 때 바뀔 언어를 표시), 계정 영역 — 로그아웃 상태면 "로그인 / 회원가입" 버튼 →
     로그인/회원가입 선택 → 아이디+비밀번호 입력. **회원가입이면** 서버가 자동 발급한
     닉네임(게임플레이 화면 새 선택 탭의 로컬 닉네임과는 다른 별개의 온라인 계정 닉네임)을
     확인/재생성/직접수정하는 화면을 거친 뒤 "완료"로 메인 메뉴 복귀. **로그인이면** 성공
     즉시 메인 메뉴로 복귀. 로그인 상태면 "{서버 닉네임} · 로그아웃" 표시, 뒤로가기
   - **게임 방법**: 조작법 + 핵심 규칙 요약 정적 텍스트, 뒤로가기
2. **플레이 중**: 마우스 클릭/터치/스페이스바를 내리막에서 누르고 있으면 가속. 좌상단 점수,
   우상단 Island·배수 + 낮 진행바, 좌하단 "STREAK n/3" 라벨 + 점 3개, 좌측에 이번 런 Nest
   목표 3개 + 진행률, 화면 중앙 상단(Fever 중이면) 펄스하는 분홍 FEVER 뱃지, 중앙에
   SLIDE!/GREAT SLIDE/STREAK RESET/FEVER! 토스트가 잠깐 떴다 사라짐. 노란 코인/파란
   스피드코인이 지형 위에, 하늘엔 흰 뭉게구름(공중에서 닿으면 터치 인정)이 흘러감 — 먹을
   때마다 파티클 버스트 + 작은 팝업 텍스트(+3/SPEED!/CLOUD TOUCH!). 하늘색이 90초에 걸쳐
   낮→노을→밤으로 서서히 변함. 우하단 작은 회색 텍스트는 물리 디버그용(속도/상태/다이빙
   여부)
3. **90초 경과(밤이 되면)**: Day Over 화면 — 최종 점수/Island/Great Slides/Cloud Touches/
   Longest Fever + Best, 이번 런 획득 코인 + 누적 코인, Nest 목표 3개 통과/실패, New
   Highscore 표시(경신 시), "다시하기"(새 지형으로 바로 재시작)/"홈"(시작 화면으로)

## 이전 세션 기록 (재발 방지용)

1. **레거시 Input 예외**: `InputService`는 반드시 New Input System(`Mouse`/`Touchscreen`/
   `Keyboard`) 기반이어야 함 — `UnityEngine.Input`은 `InvalidOperationException`.
2. **URP 셰이더 핑크색**: `Sprites/Default`가 URP에서 안 맞으면 핑크로 보일 수 있음.
   `TerrainGenerator`의 셰이더를 `Universal Render Pipeline/2D/Sprite-Lit-Default` 등으로
   교체 가능하게 필드로 뒀는지 확인.
3. **`.gitignore` 유실 사건**: `.git`이 921M까지 불어난 적 있음 — 원인은 `.gitignore`가
   사라진 상태에서 `git add`가 `Library` 캐시(2.3GB+)를 통째로 스테이징했기 때문. `git reset`
   + `git gc --prune=now`로 복구. **`.gitignore`가 항상 존재/추적되는지 주기적으로 확인.**
   현재 제외 대상: `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `*.csproj`,
   `*.sln`, `.idea/` 등.
4. **패키지 정리**: 안 쓰는 12개(2d.animation, 2d.aseprite, 2d.psdimporter, 2d.spriteshape,
   2d.tilemap, 2d.tilemap.extras, timeline, visualscripting, collab-proxy,
   multiplayer.center, test-framework, ide.visualstudio) 제거함. 남긴 것: `2d.sprite`,
   `2d.tooling`, `ide.rider`, `inputsystem`, `render-pipelines.universal`, `ugui`. URP가
   물고 오는 `burst`/`shadergraph`(1GB+)는 렌더 파이프라인 의존성이라 손대지 않음.
5. **지형-물리 island 어긋남 버그**: `BirdPhysics`가 생성 시점에 `island` 값을 한 번만
   캡처해서 계속 재사용했는데, `TerrainGenerator`는 매 프레임 최신 `GameManager.Island`로
   그림 → 섬이 넘어간 뒤 화면 언덕과 실제 착지선이 어긋남 (점프 후 착지가 언덕 라인을
   벗어나 보이는 증상). **교훈: 지형/물리처럼 같은 값을 공유해야 하는 두 시스템은 값을
   캡처해서 각자 들고 있지 말고, 매번 같은 소스(지금은 `GameManager.Ground` 단일
   인스턴스)를 직접 참조할 것.** 이후 지형을 랜덤 컨트롤포인트 방식으로 바꾸면서
   `GroundSampler`가 인스턴스화됐고, 구조적으로 이 버그 클래스 자체가 재발 불가능해짐
   (더 이상 island 파라미터를 아무도 캡처하지 않음).
6. **발사 임계값 완화 후 스트릭 스팸 리셋**: `LaunchSlopeThreshold`를 완화(-0.10→-0.07)한
   뒤 언덕 정점 근처에서 미세한 micro-hop이 자주 발생 → 전부 착지 판정되면서 STREAK RESET이
   과도하게 자주 뜸. **교훈: 판정 임계값을 완화할 때, 그 임계값이 "이벤트가 얼마나 자주
   발생하는가"에도 영향을 준다는 걸 같이 고려할 것 — 그냥 조건을 낮추는 것만으로 부작용이
   생길 수 있음.** `MinAirborneTimeForJudging`(0.15초) 미만 체공은 판정 자체를 스킵하는
   디바운스로 해결.
7. **메인 메뉴 타이틀/버튼이 화면 우측 하단에 표시되는 버그**: `StartScreen`의
   `mainMenuGroup`/`settingsGroup` 등 "순수 레이아웃 그룹 컨테이너"들을
   `UIFactory.CreateChild(...)`로만 만들고 별도 크기 지정을 안 함 →
   새로 생성된 RectTransform은 부모 중앙에 작은 기본 크기로 놓이는데, 그 자식들이
   `SetTopLeft`/`SetTopLeftCentered`로 쓰는 "화면 절대좌표" 계산은 부모 rect의
   좌상단을 기준으로 하기 때문에, 기준점이 실제 캔버스 좌상단이 아니라 이 작은
   기본 박스의 좌상단이 되어버려 타이틀/버튼 전체가 우측 하단으로 쏠려 보임.
   **교훈: 그 자체는 안 그려지고 자식들을 절대좌표로 배치하기 위한 용도로만 쓰는
   그룹 컨테이너는 반드시 부모(보통 캔버스) 크기에 맞춰 명시적으로 늘려야 함.**
   `UIFactory.CreateFullStretchChild`(`CreateChild` + `StretchFull`)를 추가하고
   `StartScreen`의 모든 그룹 컨테이너 생성 지점(`mainMenuGroup`, `settingsGroup`,
   `howToPlayGroup`, `statsGroup`, `settingsPreferencesGroup`,
   `settingsAuthChoiceGroup`, `settingsCredentialsGroup`,
   `settingsSignupNicknameGroup`, `statsLeaderboardGroup`, `statsBirdsGroup`,
   `statsMissionsGroup`)를 이걸로 교체해 해결. 개별 요소(버튼 하나 등)처럼 스스로
   `SetTopLeft`로 크기까지 지정하는 자식은 이 문제와 무관하므로 그대로 `CreateChild`
   사용.
