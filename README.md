# Hilly Wings (병아리 날다)

타이니윙스(Tiny Wings) 스타일 언덕 슬라이드/글라이드 게임의 **핵심 물리 프로토타입**입니다.
지금 이 단계에서는 "터치로 하강 가속 → 상승 경사에서 발사되어 비행"이라는 재미의 본질이
실제로 느껴지는지만 검증합니다. 코인/스피드코인/클라우드터치/네스트 미션/다국어/Firebase
랭킹/사운드는 **의도적으로 아직 구현하지 않았습니다** (아래 "다음 단계" 참고).

이 환경에는 Unity 에디터가 없어 직접 실행/컴파일 확인은 못 했습니다. 표준 Unity API로
작성했지만, 에디터에서 여신 후 한 번 플레이 테스트를 해보시고 이상이 있으면 알려주세요.

## 실행 방법

1. Unity Hub → **New Project** → 2D 템플릿(Built-in RP 권장, URP는 아래 주의사항 참고)으로
   새 프로젝트 생성.
2. 새 프로젝트의 `Assets` 폴더 안에 이 저장소의 `Assets/Scripts` 폴더를 통째로 복사해
   넣습니다 (즉 `YourProject/Assets/Scripts/...`).
3. Hierarchy에서 빈 GameObject 하나 생성 (`GameObject > Create Empty`) → 이름은 아무거나
   (예: `Bootstrap`).
4. 그 GameObject에 `GameBootstrapper` 컴포넌트를 추가 (Inspector에서 `Add Component` →
   `GameBootstrapper` 검색).
5. Play 버튼 클릭. 터레인/새/카메라가 전부 코드로 자동 생성됩니다 — 씬에 아무것도 더
   만들 필요 없습니다.

**조작**: 마우스(에디터) 또는 터치(기기)를 누르고 있으면, 하강 경사를 타는 동안 가속됩니다.
착지 순간에 맞춰 누르면(2연속 = Great Slide, 3연속 = Fever) 콤보가 붙습니다.

화면 좌측 상단에 속도/상태/점수/낮 시간 남은 시간이 표시되는 임시 디버그 HUD가 있습니다
(`SimpleHud.cs` — 출시용 UI가 아니라 물리 검증용입니다).

### URP를 쓰는 경우

`TerrainGenerator`와 `GameBootstrapper`가 사용하는 `Sprites/Default` 셰이더는 URP에서
핑크색으로 보일 수 있습니다. `TerrainGenerator` 인스펙터의 `Shader Name` 필드를
`Universal Render Pipeline/2D/Sprite-Lit-Default` 등으로 바꿔주세요.

## 폴더 구조

```
Assets/Scripts/
  Gameplay/
    TerrainGenerator.cs   각 언덕 형태를 결정하는 사인파 함수 + 시각 메시 생성
    BirdController.cs     핵심 물리 (아래 알고리즘 설명 참고)
  Camera/
    CameraRig.cs           새를 따라가며, 공중에 높이 뜨면 자동 줌아웃/줌인
  Core/
    InputService.cs        터치/마우스 입력 추상화
    GameManager.cs          낮/밤 타이머 + 점수 (프로토타입 범위)
    GameBootstrapper.cs     위 컴포넌트들을 런타임에 코드로 조립
    ProceduralSprite.cs     임시 원형 스프라이트 생성 (아트 에셋 불필요)
  Debug/
    SimpleHud.cs            OnGUI 디버그 오버레이 (임시)
```

## 핵심 물리 알고리즘

`BirdController.FixedUpdate()`에서 매 스텝마다:

1. 새가 항상 **자유낙하 중이라고 가정**하고 중력을 적분해 다음 위치 후보를 계산합니다.
2. 그 x좌표에서의 지형 높이(`TerrainGenerator.HeightAt`)와 후보 y좌표를 비교합니다.
3. 후보 위치가 지형보다 **아래**면 → 착지: 위치를 지형 위로 스냅하고, 속도를 지형의
   접선(tangent) 방향으로 재배치합니다. 하강 경사이고 입력을 누르고 있으면 추가 가속을
   더합니다.
4. 후보 위치가 지형보다 **위**면 → 공중: 그대로 자유낙하를 계속합니다.

이 한 번의 비교만으로 **착지와 발사(공중으로 튕겨나가는 것)를 동시에 처리**합니다.
언덕 정상이 급하게 꺾이면서 지형이 자유낙하 궤적보다 더 빠르게 아래로 꺼지는 구간에서는,
비교 결과가 자연스럽게 "공중"으로 나오면서 새가 붕 뜹니다 — 착지/발사를 위한 별도의
상태 분기 코드가 필요 없습니다.

지형은 사인파 3개를 합성한 순수 함수(`h(x)`)이고, 시각 메시와 물리 판정(`HeightAt`,
`TangentAt`)이 **같은 함수를 공유**하므로 눈에 보이는 것과 실제 충돌이 절대 어긋나지
않습니다.

## 다음 단계 (이번 프로토타입 범위 밖)

앞서 논의한 대로 우선순위는:

1. 코인 / 스피드코인 / 클라우드터치 / 아일랜드(맵) 분할
2. 둥지(네스트) 미션 시스템 + 배율 테이블
3. 스코어 랭킹 — **Firebase**(Firestore) 연동
4. 다국어 — 한국어/영어 우선, 이후 일본어/중국어(간체·번체) 추가
5. 사운드 (점프/버튼 SFX, 잔잔한 BGM)
6. AI 기반 난이도 재조정 — 확장 기능 검토만 (지금은 설계하지 않음)

이 프로토타입의 이벤트 훅(`BirdController.OnGreatSlide`, `OnFeverStart/End`,
`OnLanded/OnLaunched`)은 위 콘텐츠들이 붙기 쉽도록 미리 분리해뒀습니다 — 코인/네스트
시스템은 이 이벤트들을 구독하는 별도 매니저로 추가하면 됩니다.
