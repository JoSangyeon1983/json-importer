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

> **더 이상 사용하지 않습니다 (v1.1.0부터).** 브라우저 주소창의 URL을 그대로 붙여넣으면 됩니다.
> 기록 목적으로만 남겨둡니다.

스프레드시트에서 **TSV export URL을 자동 생성**하던 Google Apps Script.
Apps Script에 붙여넣고 `WriteCombinedUrlWithRangeToC2()`를 실행하면,

1. A열에서 `//`로 시작하지 않는 첫 행을 데이터 시작 행으로 잡고
2. 그 행의 오른쪽 끝 열, A열 아래쪽 끝 행을 찾아 범위(`A5:C12` 등)를 계산한 뒤
3. `.../export?format=tsv&gid=<gid>&range=<범위>` 형태로 조합해

**B1 셀에** 써줍니다. (함수 이름은 `...ToC2`지만 실제 출력 위치는 B1입니다.)

### 왜 폐기했나 — `range=`가 데이터를 잘라먹었습니다

`endCol`을 구하는 루프가 헤더 행을 왼쪽부터 훑다가 **첫 빈 칸에서 `break`** 합니다:

```js
for (let c = 0; c < data[startRow - 1].length; c++) {
  if (data[startRow - 1][c] === '') break;   // ← 여기
  endCol = c + 1;
}
```

Localization 시트의 헤더는 `textId | //코멘트 | //UI 화면 | (빈칸) | ko | en` 이라
D열의 빈 칸에서 멈춰 `range=A5:C12`가 생성되고, **실제 값인 `ko`/`en` 열이 아예 다운로드되지 않았습니다.**
그 결과 변환 JSON은 값이 전부 빈 객체(`{"3_3_": {}, "3": {}}`)였습니다.

지금은 툴이 그 역할을 대신하며, 더 정확합니다.

| `GetURL.gs`의 역할 | 대체 위치 |
|---|---|
| `startRow` — 헤더 위 `//` 주석 행 건너뛰기 | `TsvConverter.FindHeaderRowIndex` |
| `endCol` — 오른쪽 경계 | `TsvConverter.FilterCommentColumns` (빈 헤더 열만 건너뛰고, 뒤 열은 유지) |
| export URL 조합 | `SheetUrlNormalizer.Normalize` |

시트는 여전히 **'링크가 있는 모든 사용자'에게 공개(뷰어)** 상태여야 다운로드가 됩니다.
