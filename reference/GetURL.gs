function WriteCombinedUrlWithRangeToC2() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  const sheet = ss.getActiveSheet();
  const data = sheet.getDataRange().getValues();

  // 1. A열 검사 - '//'로 시작하지 않는 첫 행 찾기
  let startRow = null;
  for (let r = 0; r < data.length; r++) {
    const cell = data[r][0];
    if (!(typeof cell === 'string' && cell.startsWith('//'))) {
      startRow = r + 1; // 1-based
      break;
    }
  }
  if (startRow === null) {
    sheet.getRange("C2").setValue("유효한 시작 행 없음");
    return;
  }

  // 2. 해당 행에서 오른쪽으로 비어있지 않은 마지막 열 찾기
  let endCol = 1;
  for (let c = 0; c < data[startRow - 1].length; c++) {
    if (data[startRow - 1][c] === '') break;
    endCol = c + 1;
  }

  // 3. A열 아래로 비어있지 않은 마지막 행 찾기
  let endRow = startRow;
  for (let r = startRow - 1; r < data.length; r++) {
    if (data[r][0] === '') break;
    endRow = r + 1;
  }

  // 열 번호 → 문자 변환 함수
  function getColumnLetter(col) {
    let letter = '';
    while (col > 0) {
      let remainder = (col - 1) % 26;
      letter = String.fromCharCode(65 + remainder) + letter;
      col = Math.floor((col - 1) / 26);
    }
    return letter;
  }

  const colLetter = getColumnLetter(endCol);
  const rangeString = `A${startRow}:${colLetter}${endRow}`;

  // 문서 기본 URL (edit 이전까지)
  const fullUrl = ss.getUrl();
  const baseUrlMatch = fullUrl.match(/https:\/\/docs\.google\.com\/spreadsheets\/d\/[a-zA-Z0-9-_]+/);
  const baseUrl = baseUrlMatch ? baseUrlMatch[0] : '';

  // 고정 문자열
  const fixedStr = "/export?format=tsv&";

  // gid=숫자 추출
  const sheetId = sheet.getSheetId();
  const gidStr = "gid=" + sheetId;

  // 범위 앞에 &range= 붙이기
  const rangePart = "&range=" + rangeString;

  // 최종 조합
  const combined = baseUrl + fixedStr + gidStr + rangePart;

  // C2에 결과 쓰기
  sheet.getRange("B1").setValue(combined);
}
