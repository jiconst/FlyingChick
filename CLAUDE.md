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
M3), **카메라 줌**(`FX/CameraZoom.cs`, 아래 상세). 남은 것:
- 다이브 먼지, Fever 별 트레일 파티클
- 하늘 그라데이션/태양·달/별 (지금은 `FX/SkyTint.cs`의 단색 lerp만)
- 섬별 10색 팔레트 전환 (지금은 언덕 3색 그라데이션 고정)
- 사운드 훅 (점프/버튼 SFX, BGM, 점수 획득 효과음 — 아직 오디오 자체가 전혀 없음)
- OnGUI를 실제 UI(Canvas/TMP 또는 UI Toolkit)로 교체
- 성능 프로파일링

### 카메라 줌 (`FX/CameraZoom.cs`)
- 원래 스펙(1단계에서 미룬 항목)은 "최대 뷰포트 15% 줌아웃"이었는데, **"병아리가 화면
  밖으로 사라지면 안 된다"는 요청으로 방식을 바꿈** — 고정 퍼센트 상한 대신, 병아리의
  실제 월드 Y 위치가 항상 카메라 시야 안(`[-orthographicSize, +orthographicSize]`,
  카메라가 Y=0 고정이므로)에 들어오도록 **필요한 만큼** 줌아웃함
  - `neededHalfHeight = max(baseOrthoSize, |birdY| + margin)`, 안전장치로
    `baseOrthoSize * 4`를 절대 상한으로 둠 (물리 버그로 비정상적으로 높이 튀는 경우 대비용,
    평소 게임플레이에서 걸릴 일은 없음)
  - 줌아웃/줌인 스무딩 시간을 다르게 줌 (`zoomOutSmoothTime=0.12`로 빠르게 따라잡고,
    `zoomInSmoothTime=0.35`로 천천히 복귀) — 급상승엔 즉각 반응하되 복귀는 부드럽게
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
7. **M7 — 폴리시**: 🚧 진행 중. 카메라 줌(높이 비례 줌아웃, `FX/CameraZoom.cs`) 완료 —
   **플레이 확인 필요**. 남은 것: 파티클(다이브 먼지/Fever 별 트레일), 하늘/팔레트 비주얼,
   사운드, OnGUI→실제 UI 교체, 성능 프로파일링

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

## 실행 방법

빈 GameObject에 `GameBootstrapper` 컴포넌트만 붙이고 Play — 카메라/지형/새/점수 시스템/HUD/
낮밤 사이클/시작·종료 화면이 전부 런타임에 코드로 조립된다.

1. **시작 화면**: 타이틀 + Best 점수, 우상단 코인 총액 + "기록 보기" 버튼(Top 10 + 누적
   통계 패널 토글), 좌하단 오늘의 미션 패널, 하단 보유 새 선택 줄(클릭해서 선택) + 알
   구매 버튼(500코인, 부화 결과 토스트 표시). **이 버튼들이 아닌 빈 곳**을 터치/클릭하거나
   스페이스바를 누르면 시작
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
