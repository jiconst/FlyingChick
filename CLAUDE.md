# Flying Chick (병아리 날다)

타이니윙스(Tiny Wings) 스타일 언덕 슬라이드/글라이드 모바일 게임. 화면 터치만으로 조작 —
하강 경사에서 터치해 가속하고, 상승 경사에서 발사되어 날아간다.

## 개발 환경

- **엔진/언어**: Unity 6000.5.7f1, C#
- **개발 OS**: macOS
- **렌더 파이프라인**: URP (Universal Render Pipeline)
- **입력**: New Input System 패키지 (Active Input Handling이 "Input System Package"로 설정됨 —
  레거시 `UnityEngine.Input` 클래스는 예외를 던지므로 절대 쓰지 말 것. 입력은 반드시
  `InputService.cs`를 통해서만 읽는다)
- **프로젝트 폴더**: `~/src/FlyingChick` 하나로 통일 — Claude Code 작업 폴더 = 실제 Unity
  프로젝트 폴더 (Unity Hub도 이 경로를 가리킴)
- **IDE**: Rider (`com.unity.ide.rider` 설치됨, VS용 패키지는 제거함). Rider에서 디버깅하려면
  Unity 에디터를 먼저 켜둔 상태에서 "Attach to Unity Editor"로 attach

## 확정된 설계 결정

- **스코어 랭킹 백엔드**: Firebase (Firestore) — 아직 미구현
- **다국어 지원 우선순위**: 한국어·영어 → 이후 일본어·중국어(간체/번체) — 아직 미구현
- **AI 기반 난이도 재조정**: 확장 기능으로 검토만, 지금은 설계하지 않음
- **개발 순서**: 코인/네스트/다국어/Firebase보다 **핵심 물리·조작감 검증을 최우선**으로
  진행하기로 함 (재미가 없으면 나머지는 의미 없다는 판단)

## 지금까지 구현된 것 (핵심 물리 프로토타입)

`GameBootstrapper` 하나만 빈 GameObject에 붙이고 Play하면, 씬에 아무것도 없어도 터레인·새·
카메라·타이머가 전부 런타임에 코드로 조립된다 (에디터에서 수작업 씬 세팅 불필요, 임시
프로시저럴 스프라이트만 사용, 외부 아트 에셋 없음).

```
Assets/Scripts/
  Gameplay/
    TerrainGenerator.cs   사인파 3개 합성 함수로 언덕 생성. 이 함수 자체가 시각 메시와
                           물리 판정(HeightAt/TangentAt)의 유일한 소스 — 눈에 보이는 것과
                           충돌이 절대 어긋나지 않음
    BirdController.cs      핵심 물리 (아래 알고리즘 참고) + Great Slide/Fever 콤보 판정
  Camera/
    CameraRig.cs            새를 따라가며, 공중에 높이 뜨면 자동 줌아웃 → 착지 시 줌인
  Core/
    InputService.cs         터치/마우스 입력 추상화 (New Input System 기반, 필수로 이걸 통해서만
                             입력 읽기)
    GameManager.cs           낮/밤 타이머 + 점수 (프로토타입 범위)
    GameBootstrapper.cs      위 컴포넌트들을 런타임에 코드로 조립하는 진입점
    ProceduralSprite.cs      임시 원형 스프라이트 런타임 생성
  Debug/
    SimpleHud.cs             OnGUI 디버그 오버레이 — Speed/State/Input held/Diving/Score/Day
                             표시 (출시용 아님, 물리 검증 끝나면 제거)
```

### 핵심 물리 알고리즘 (`BirdController.FixedUpdate`)

매 스텝마다 새가 **항상 자유낙하 중이라고 가정**하고 중력을 적분해 다음 위치 후보를 계산한
뒤, 그 x좌표에서의 지형 높이와 비교한다.
- 후보 위치가 지형보다 **아래** → 착지: 지형 위로 스냅, 속도를 지형 접선 방향으로 재배치.
  하강 경사 + 입력 홀드 중이면 `diveAcceleration` 추가.
- 후보 위치가 지형보다 **위** → 공중: 그대로 자유낙하 유지.

이 한 번의 비교만으로 착지와 발사(언덕 정상에서 튕겨나가는 것)를 **동시에** 처리한다 —
상태별 분기 코드가 따로 필요 없다. 이 트릭을 다른 방식으로 바꾸지 말 것 (검증된 접근).

### Great Slide / Fever

착지 순간(공중→지상 전환) 입력이 정확한 타이밍(하강 경사 + 입력 홀드)이면 콤보 카운트 증가.
2연속 = Great Slide, 3연속 = Fever(점수 2배, 추가 슬라이드마다 지속시간 연장, 실패 시 즉시
해제). `BirdController`가 `OnGreatSlide`/`OnFeverStart`/`OnFeverEnd`/`OnLanded`/`OnLaunched`
이벤트를 노출하므로, 코인/네스트 시스템은 이 이벤트를 구독하는 별도 매니저로 추가하면 된다
(BirdController 자체를 건드릴 필요 없음).

## 겪었던 문제와 해결 (재발 방지용 기록)

1. **레거시 Input 예외**: `InputService`가 처음에 `UnityEngine.Input`을 썼다가
   `InvalidOperationException` 발생 (프로젝트가 New Input System 전용으로 설정돼 있음).
   → `Mouse.current`/`Touchscreen.current` 기반으로 재작성함. 앞으로 입력 관련 코드는
   반드시 New Input System API만 사용.
2. **URP 셰이더 핑크색**: `TerrainGenerator`/`GameBootstrapper`가 기본으로 쓰는
   `Sprites/Default` 셰이더가 URP에서 안 맞으면 핑크로 보일 수 있음. `TerrainGenerator`
   인스펙터의 `Shader Name` 필드로 교체 가능 (`Universal Render Pipeline/2D/Sprite-Lit-Default`
   등). Play 중 Inspector에서 바꿔도 Stop하면 초기화되므로, 문제 생기면 스크립트 기본값
   자체를 수정해야 함.
3. **`.gitignore` 유실로 인한 git 폭탄**: 프로젝트 폴더가 3.2GB까지 불어났던 사건 — 원인은
   `.gitignore`가 사라진 상태에서 `git add`가 `Library` 캐시 폴더(수천 개 파일, 2.3GB+)를
   통째로 스테이징했기 때문. 다행히 원격(`origin/main`)엔 초기 커밋(빈 CLAUDE.md)만 있어서
   `git reset` + `git gc --prune=now`로 안전하게 복구 (`.git` 921M → 160K). **`.gitignore`가
   항상 존재하고 추적되고 있는지 주기적으로 확인할 것.** 현재 `.gitignore`는 `Library/`,
   `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `.idea/` 등을 제외한다.
4. **불필요한 패키지 정리**: Unity 6 "2D" 템플릿이 기본으로 깔아준 패키지 중 실제로 안 쓰는
   12개(2d.animation, 2d.aseprite, 2d.psdimporter, 2d.spriteshape, 2d.tilemap,
   2d.tilemap.extras, timeline, visualscripting, collab-proxy, multiplayer.center,
   test-framework, ide.visualstudio)를 `Packages/manifest.json`에서 제거함. 남긴 것:
   `2d.sprite`, `2d.tooling`, `ide.rider`, `inputsystem`, `render-pipelines.universal`,
   `ugui`. URP가 물고 오는 `burst`/`shadergraph`(합쳐서 1GB+)는 렌더 파이프라인 의존성이라
   손대지 않음 — 없애려면 Built-in RP로 전환하는 별도 논의 필요.

## 아직 구현 안 된 것 (다음 단계, 우선순위 순)

1. 코인 / 스피드코인 / 클라우드터치 / 아일랜드(맵) 분할
2. 둥지(네스트) 미션 시스템 + 배율 테이블 (레벨 1~10, 10x~28x)
3. 스코어 랭킹 — Firebase(Firestore) 연동
4. 다국어 — 한국어/영어 → 일본어/중국어(간체·번체)
5. 사운드 (점프/버튼 SFX, 잔잔한 BGM, 점수 획득 효과음)
6. AI 기반 난이도 재조정 (확장 기능 검토만, 미설계)
7. `SimpleHud`를 실제 UI로 교체

## 게임 이름 후보 (참고)

원안 "Flying Chick (병아리 날다)" 유지하기로 함. 대안: Chick Glide, Wobble Wings, Peep & Soar,
Sunny Slopes.
