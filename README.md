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
    └── GetURL.gs                   #   TSV export URL 생성 Google Apps Script
```

**계층 분리 원칙**: 변환·다운로드·저장 로직은 모두 `JsonImporter.Core`에 두고,
`JsonImporter.App`은 UI만 담당합니다. 테스트는 Core만 참조하므로 UI 없이 검증할 수 있습니다.

## 주요 기능

- 테이블별 Google Sheets TSV export URL 입력
  - UI에서 **행을 동적으로 추가/삭제** (원본의 고정 enum 방식 대체)
  - 입력한 테이블 목록·URL을 **설정 파일에 영구 저장**
- TSV → key 기반 JSON 변환 (`//` 주석 컬럼/행 필터링, 타입 추론)
- 전체 / 선택 테이블 import

> 출력은 **JSON 전용**입니다. 원본에 있던 CSV 출력은 이식하지 않았으며, 추가할 계획도 없습니다.

## 개발

```bash
# 복원 & 빌드
dotnet build

# 테스트
dotnet test

# 앱 실행
dotnet run --project src/JsonImporter.App
```

## 요구 사항

- Windows 10/11
- .NET 9 SDK
