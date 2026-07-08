# reference

빌드·실행에 포함되지 않는 **참고용 원본 파일** 모음입니다.
어느 솔루션 프로젝트에도 포함되어 있지 않으므로 수정해도 앱 동작에 영향이 없습니다.

## DataImporter.cs

JsonImporter의 원본이 된 **Unity 에디터 확장** (`OTAON.Editor.Data`).
`Tools/Data Import` 메뉴에서 Google Sheets TSV를 받아 JSON·CSV로 변환하던 도구입니다.

이식하면서 달라진 점:

| 항목 | 원본 | JsonImporter |
|---|---|---|
| 대상 테이블 | 고정 `enum DataTable` | UI에서 동적으로 추가/삭제 |
| 설정 저장 | Unity `EditorPrefs` | `%AppData%/JsonImporter/settings.json` |
| 다운로드 | 동기 `WebClient` | 비동기 `HttpClient` + 타임아웃 · HTML 응답 감지 |
| 출력 | JSON + CSV | **JSON 전용** (CSV는 이식하지 않음) |
| 출력 파일명 | enum 이름(소문자) | 시트(탭) 이름 자동 추출, 대소문자 유지 |

변환 규칙(`//` 주석 컬럼·행 필터링, 첫 유효 컬럼을 key로 사용, bool/int/float 타입 추론)은
`JsonImporter.Core`의 `TsvConverter` · `TypeInference` · `KeyedJsonSerializer`에 그대로 옮겨져 있습니다.

## GetURL.gs

스프레드시트에서 **TSV export URL을 자동 생성**하는 Google Apps Script.
시트 확장 프로그램(Apps Script)에 붙여넣고 `WriteCombinedUrlWithRangeToC2()`를 실행하면,

1. A열에서 `//`로 시작하지 않는 첫 행을 데이터 시작 행으로 잡고
2. 그 행의 오른쪽 끝 열, A열 아래쪽 끝 행을 찾아 범위(`A5:C7` 등)를 계산한 뒤
3. `https://docs.google.com/spreadsheets/d/<id>/export?format=tsv&gid=<gid>&range=<범위>` 형태로 조합해

**B1 셀에** 써줍니다. (함수 이름은 `...ToC2`지만 실제 출력 위치는 B1입니다.)

이렇게 만들어진 URL을 JsonImporter의 테이블 URL 칸에 붙여넣으면 됩니다.
시트는 **'링크가 있는 모든 사용자'에게 공개(뷰어)** 상태여야 다운로드가 됩니다.
