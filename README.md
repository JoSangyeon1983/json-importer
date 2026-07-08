# JsonImporter

Google Sheets의 **TSV export**를 다운로드해 **key 기반 JSON**으로 변환하는 Windows 데스크톱 애플리케이션.

원본 Unity 에디터 확장([`DataImporter.cs`](reference/DataImporter.cs))의 변환 로직을 Unity 의존성 없이
독립 실행형 WPF 앱으로 재구성한 프로젝트입니다.

## 기술 스택

- **.NET 9** / **C#**
- **WPF** (`net9.0-windows`) — MVVM 패턴
- **xUnit** — 단위 테스트

## 솔루션 구조

```
06_JsonImporter/
├── JsonImporter.sln
├── README.md
├── .gitignore / .editorconfig
│
├── src/
│   ├── JsonImporter.Core/          # UI 비의존 변환 로직 (재사용 · 테스트 대상)
│   │   ├── Models/                 #   데이터 모델 (테이블 정의, 설정 등)
│   │   └── Services/               #   다운로드 · TSV→JSON 변환 · 파일 저장 · 설정 저장
│   │
│   └── JsonImporter.App/           # WPF 프레젠테이션 계층 (net9.0-windows)
│       ├── App.xaml                #   앱 진입점
│       ├── Views/                  #   XAML 화면 (MainWindow 등)
│       ├── ViewModels/             #   MVVM 뷰모델
│       └── app.manifest            #   Per-Monitor V2 DPI 인식
│
├── tests/
│   └── JsonImporter.Tests/         # JsonImporter.Core 단위 테스트
│
└── reference/                      # 빌드에 포함되지 않는 참고용 원본 (자세한 내용은 reference/README.md)
    ├── DataImporter.cs             #   원본 Unity 에디터 도구
    └── GetURL.gs                   #   구버전 URL 생성 스크립트 (v1.1.0부터 불필요)
```

**계층 분리 원칙**: 변환·다운로드·저장 로직은 모두 `JsonImporter.Core`에 두고,
`JsonImporter.App`은 UI만 담당합니다. 테스트는 Core만 참조하므로 UI 없이 검증할 수 있습니다.

## 주요 기능

- 테이블별 Google Sheets URL 입력
  - 브라우저 주소창의 URL을 **그대로 붙여넣기** — export URL로 자동 변환
  - UI에서 **행을 동적으로 추가/삭제** (원본의 고정 enum 방식 대체)
  - 입력한 테이블 목록·URL을 **설정 파일에 영구 저장**
- TSV → key 기반 JSON 변환 (`//` 주석 컬럼/행 필터링, 타입 추론)
- 전체 / 선택 테이블 import

> 출력은 **JSON 전용**입니다. 원본에 있던 CSV 출력은 이식하지 않았으며, 추가할 계획도 없습니다.

## 시트 URL

브라우저 주소창이나 '공유' 버튼에서 복사한 URL을 그대로 붙여넣으면 됩니다.
`SheetUrlNormalizer`가 `format=tsv` export URL로 바꿔줍니다.

| 입력 | 결과 |
|---|---|
| `.../edit?gid=0#gid=0` | `.../export?format=tsv&gid=0` |
| `.../edit?usp=sharing` | `.../export?format=tsv` (첫 번째 시트) |
| `.../export?format=tsv&range=A5:C12` | 그대로 사용 (수동 지정 존중) |

시트는 **'링크가 있는 모든 사용자'에게 공개(뷰어)** 상태여야 합니다.
비공개 시트는 TSV 대신 로그인 HTML을 200 OK로 반환하는데, 이 경우 명확한 오류로 알려줍니다.

## 시트 레이아웃 규칙

```
//  ← 헤더 위 주석 행은 몇 줄이든 무시됩니다
//
textId    //코멘트    //UI 화면        ko        en      ← 헤더 (첫 번째 비주석 행)
// 데모   ...                                            ← 주석 행 무시
3_3_      덜미        시작             불러오는 중  Now Loading
```

- **헤더**: `//`로 시작하지 않는 첫 번째 비어있지 않은 행
- **key**: 헤더의 첫 번째 유효 열 (위 예시의 `textId`) — 값 객체에는 포함되지 않음
- **주석**: 열 이름이 `//`로 시작하는 열, 첫 셀이 `//`로 시작하는 행
- 헤더가 빈 열은 건너뛰며, 그 **뒤의 열은 잘리지 않습니다**

> 첫 번째 열은 key 컬럼이므로 주석 열(`//...`)로 쓸 수 없습니다.

## 개발

```bash
# 복원 & 빌드
dotnet build

# 테스트
dotnet test

# 앱 실행
dotnet run --project src/JsonImporter.App

# 배포용 단일 파일 exe (publish/JsonImporter.exe, 약 60MB)
dotnet publish src/JsonImporter.App -o publish
```

퍼블리시 옵션(단일 파일·자체 포함·압축·네이티브 DLL 포함)은 `JsonImporter.App.csproj`에
고정되어 있으므로 별도 플래그가 필요 없습니다. `dotnet build`는 영향을 받지 않습니다.

> `dotnet build`는 `bin/Debug/`에만 씁니다. **`publish/`의 exe는 `dotnet publish`를 다시 돌려야 갱신됩니다.**

## 요구 사항

- Windows 10/11
- .NET 9 SDK
