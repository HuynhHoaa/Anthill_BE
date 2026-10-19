#!/usr/bin/env bash
# Xuat bang diem ra Excel: hoc sinh xuat bang diem CUA MINH, giao vien xuat bang diem LOP MINH DAY. Quan tri vien khong xuat.
#
# Chay:  bash tests/api-checks/grade-sheet.sh   (API phai dang chay o $HOST, database thu; can python co openpyxl)
#
# Quy tac duoc canh gac o day:
#   - GET /api/my/results/export (chi hoc sinh): moi bai da nop mot dong; bai giao vien chua cho xem diem ghi "Chua cong bo"
#     va KHONG co so nao trong file (khong phai duong vong de doc diem som); diem trung binh chi tinh bai da cong bo.
#   - GET /api/student-groups/{id}/grade-sheet (chi giao vien DANG DAY lop da cong bo): moi hoc sinh mot dong, moi de DA GIAO CHO
#     LOP mot cot (cua bat ky giao vien nao — giao vien moi nhan lop van thay diem cac de giao vien cu da giao), diem quy ve
#     thang 10 va mot cot diem trung binh (khong lo so bai da lam); bai duoc giao ma khong lam = 0 diem va van tinh vao trung binh, dau — = khong
#     duoc giao de do (vao lop sau khi de da giao) nen khong tinh; admin 403, lop nguoi khac /
#     lop nhap / lop khong co 404; lop khoa van xuat duoc; de giao qua lop khac khong lan sang bang diem nay.
#   - Ten hoc sinh bat dau bang "=" van la van ban trong file, khong bao gio la cong thuc.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=(); EXTRA_TEACHERS=(); TERM_IDS=()

# Duong dan dang C:/... de ca bash, curl lan python (ban Windows) cung hieu.
WORK=$(cygpath -m "$(mktemp -d -t anthill-gs.XXXXXX)")
TOOL=$(cygpath -m "$(cd "$(dirname "$0")" && pwd)/xlsx_tool.py")
HEADERS="$WORK/headers.txt"

cleanup() {
  section "Don du lieu"
  local exams=0 questions=0 groups=0
  for id in "${EXAMS[@]}";     do [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]] && exams=$((exams + 1)); done
  for id in "${QUESTIONS[@]}"; do [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1)); done
  for gid in "${GROUP_IDS[@]}"; do call POST "/api/student-groups/$gid/unlock" "$ADMIN" > /dev/null; done
  for sid in "${STUDENTS[@]}"; do
    for gid in "${GROUP_IDS[@]}"; do call DELETE "/api/student-groups/$gid/students/$sid" "$ADMIN" "$REMOVE_BODY" > /dev/null; done
    call PATCH "/api/students/$sid/status" "$ADMIN" "$LOCK_BODY" > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1)); done
  for id in "${TERM_IDS[@]}"; do call DELETE "/api/terms/$id" "$ADMIN" > /dev/null; done
  for id in "${EXTRA_TEACHERS[@]}"; do retire_teacher "$ADMIN" "$id"; done
  rm -rf "$WORK"
  printf '  xoa %d/%d de · %d/%d cau hoi · %d/%d lop · khoa %d hoc sinh · khoa %d giao vien\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "${#STUDENTS[@]}" "${#EXTRA_TEACHERS[@]}"
  printf '  de da co bai nop thi khong xoa duoc — dung thiet ke, khong phai loi don dep.\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

xl() { python -X utf8 -W ignore "$TOOL" "$@"; }

# Tai mot file: download OUT PATH [TOKEN] — in ma trang thai, ghi tieu de tra ve vao $HEADERS.
download() {
  local out="$1" path="$2" token="${3:-}"
  local args=(-s -D "$HEADERS" -o "$out" -w '%{http_code}' "$HOST$path")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  curl "${args[@]}"
}
header_has() { if grep -i -q -- "$1" "$HEADERS"; then echo yes; else echo no; fi; }

# Doc file .xlsx vua tai ve ($1) vao $WORK/dump.json.
dump() { xl dump "$1" > "$WORK/dump.json"; }

# Danh gia mot bieu thuc python tren bang diem vua doc. Cac bien san co:
#   rows    toan bo cac dong cua trang dau
#   header  hang dau cot (dong co o dau tien la "STT")
#   data    cac dong du lieu ngay duoi hang dau cot (dung o dong dau tien co o STT trong)
#   footer  cac dong tong duoi du lieu
#   d       ban dump nguyen ven (freeze, formulas, numbers...)
D() {
  python -X utf8 - "$WORK/dump.json" "$1" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
rows = d['rows']
h = next((i for i, r in enumerate(rows) if r and r[0] == 'STT'), None)
header = rows[h] if h is not None else []
data = []
if h is not None:
    for r in rows[h + 1:]:
        if r[0] is None:
            break
        data.append(r)
footer = [r for r in rows[h + 1 + len(data):] if any(v is not None for v in r)] if h is not None else []
near = lambda a, b: a is not None and abs(a - b) < 0.006
print(eval(sys.argv[2]))
PY
}

new_student() { # $1 hau to, $2 ho ten day du, $3 id lop
  local c; c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"$2\",\"email\":\"gs$1@kiemthu.vn\",\"username\":\"gs$1\",\"code\":\"GS$1\",\"password\":\"123456\",\"groupId\":$3}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $1: HTTP $c — $(body)"
  json_field id
}

section "Dung du lieu nen: hai lop, bon hoc sinh, bon de"
call GET /api/auth/me "$TEACHER" > /dev/null
TEACHER_ID=$(json_field id)

G=$(new_class "$ADMIN" "$TEACHER" "Lop BD $STAMP") || fail_fast "tao lop"
G2=$(new_class "$ADMIN" "$TEACHER" "Lop BD hai $STAMP") || fail_fast "tao lop hai"
GROUP_IDS+=("$G" "$G2")
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop nhap BD $STAMP\",\"teacherId\":$TEACHER_ID}")
[[ "$code" != "200" ]] && fail_fast "tao lop nhap: HTTP $code — $(body)"
GDRAFT=$(json_field id); GROUP_IDS+=("$GDRAFT")

NAME_A="Hoc sinh GS ${STAMP}a"; NAME_B="Hoc sinh GS ${STAMP}b"; NAME_C="Hoc sinh GS ${STAMP}c"
NAME_E="=1+1 Hoc sinh GS ${STAMP}e"
S_A=$(new_student "${STAMP}a" "$NAME_A" "$G"); S_B=$(new_student "${STAMP}b" "$NAME_B" "$G")
S_C=$(new_student "${STAMP}c" "$NAME_C" "$G"); S_E=$(new_student "${STAMP}e" "$NAME_E" "$G")
STUDENTS+=("$S_A" "$S_B" "$S_C" "$S_E")
# Em A hoc ca hai lop: bai lam cua em phai hien o bang diem cua ca hai.
code=$(call POST "/api/student-groups/$G2/students" "$ADMIN" "{\"studentIds\":[$S_A]}")
[[ "$code" != "200" ]] && fail_fast "xep em A vao lop hai: HTTP $code — $(body)"

TOK_A=$(login_and_clear_must_change "gs${STAMP}a" "123456" "Gs${STAMP}a")
TOK_B=$(login_and_clear_must_change "gs${STAMP}b" "123456" "Gs${STAMP}b")
TOK_C=$(login_and_clear_must_change "gs${STAMP}c" "123456" "Gs${STAMP}c")

for pair in easy:1 easy:2 easy:3 easy:4 medium:1 medium:2 medium:3 hard:1 hard:2; do
  d="${pair%%:*}"; i="${pair##*:}"
  c=$(call POST /api/questions "$TEACHER" \
    "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"KiemThu$STAMP\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
  QUESTIONS+=("$(json_field id)")
done

new_exam() { # $1 tieu de, $2 showResultAfterSubmit, $3 id lop can giao (bo trong = khong giao)
  local c; c=$(call POST /api/exams "$TEACHER" "$(cat <<JSON
{
  "title": "$1", "topic": "KiemThu$STAMP", "durationMinutes": 45, "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22, "pointEasy": 10, "pointMedium": 10, "pointHard": 15,
  "openAt": "2020-01-01T00:00:00Z", "closeAt": "2035-01-01T00:00:00Z",
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": $2
}
JSON
)")
  [[ "$c" != "200" ]] && fail_fast "tao de: HTTP $c — $(body)"
  NEW_EXAM_ID=$(json_field id); EXAMS+=("$NEW_EXAM_ID")
  if [[ -n "${3:-}" ]]; then
    c=$(call POST "/api/exams/$NEW_EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$3],\"notify\":false}")
    [[ "$c" != "200" ]] && fail_fast "giao de: HTTP $c — $(body)"
  fi
}

T_OPEN="De mo $STAMP"; T_HIDDEN="De kin $STAMP"; T_G2="De lop hai $STAMP"; T_UNUSED="De chua giao $STAMP"
new_exam "$T_OPEN" true "$G";    E_OPEN=$NEW_EXAM_ID
new_exam "$T_HIDDEN" false "$G"; E_HIDDEN=$NEW_EXAM_ID
new_exam "$T_G2" true "$G2";     E_G2=$NEW_EXAM_ID
new_exam "$T_UNUSED" true;       E_UNUSED=$NEW_EXAM_ID

assignment_for() {
  call GET "/api/my/exams" "$1" > /dev/null
  python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((item['assignmentId'] for item in items if item['examId'] == int(sys.argv[2])), ''))
" "$BODY_FILE" "$2"
}

take_exam() { # $1 token, $2 examId, $3 'dung' hoac 'sai'
  local assignment; assignment=$(assignment_for "$1" "$2")
  call POST /api/exam-attempts "$1" "{\"assignmentId\":$assignment}" > /dev/null
  local attempt; attempt=$(json_field attemptId)
  local session; session=$(mktemp -t anthill-gs.XXXXXX)
  cp "$BODY_FILE" "$session"
  local answers; answers=$(python -c "
import json,sys
label = 'Dung' if sys.argv[2] == 'dung' else 'Sai'
questions = json.load(open(sys.argv[1], encoding='utf-8'))['questions']
print(json.dumps([{'questionId': q['id'], 'answerId': next(a['id'] for a in q['answers'] if a['content'] == label)} for q in questions]))
" "$session" "$3")
  rm -f "$session"
  call POST "/api/exam-attempts/$attempt/submit" "$1" "{\"answers\":$answers}" > /dev/null
}

# Thu tu nop quyet dinh thu tu dong cua em A (moi nop truoc). Bai KIN em A lam SAI: neu cong diem bi ro thi se thay so 0.
take_exam "$TOK_A" "$E_OPEN" dung
take_exam "$TOK_A" "$E_HIDDEN" sai
take_exam "$TOK_A" "$E_G2" dung
take_exam "$TOK_B" "$E_OPEN" sai
# Em F vao lop SAU khi cac de da giao nen khong co dong giao bai nao: o cua em F la dau —, khac voi em C (duoc giao, chua lam).
S_F=$(new_student "${STAMP}f" "Hoc sinh GS ${STAMP}f" "$G"); STUDENTS+=("$S_F")
printf '  lop %s (5 em) · lop %s (em A) · A lam 3 de, B lam 1 de, C va E khong lam, F vao lop sau khi giao\n' "$G" "$G2"

# ── Hoc sinh xuat bang diem cua minh ─────────────────────────────────────────────────────────────
section "Hoc sinh: chi hoc sinh xuat duoc bang diem cua minh"
expect_status "khong dang nhap → 401" 401 "$(download "$WORK/x.xlsx" /api/my/results/export)"
expect_status "giao vien xuat bang diem ca nhan → 403" 403 "$(download "$WORK/x.xlsx" /api/my/results/export "$TEACHER")"
expect_status "quan tri vien xuat bang diem ca nhan → 403" 403 "$(download "$WORK/x.xlsx" /api/my/results/export "$ADMIN")"

section "Hoc sinh A: ba bai, mot bai chua cong bo diem"
expect_status "em A xuat → 200" 200 "$(download "$WORK/a.xlsx" /api/my/results/export "$TOK_A")"
expect_status "  la file Excel" yes "$(header_has 'spreadsheetml.sheet')"
expect_status "  ten file chi gom chu ASCII, theo ma hoc sinh" yes "$(header_has "filename=bang-diem-gs${STAMP}a.xlsx")"
dump "$WORK/a.xlsx"
expect_status "  trang tinh ten Bang diem" "['Bảng điểm']" "$(D "d['sheets']")"
expect_status "  dong dau la tieu de" "BẢNG ĐIỂM CÁ NHÂN" "$(D "rows[0][0]")"
expect_status "  ghi ten va ma em" yes "$(D "'yes' if '$NAME_A' in rows[1][0] and 'GS${STAMP}a' in rows[1][0] else 'no'")"
expect_status "  ba dong du lieu" 3 "$(D "len(data)")"
expect_status "  moi nop truoc: de lop hai, de kin, de mo" "['$T_G2', '$T_HIDDEN', '$T_OPEN']" "$(D "[r[1] for r in data]")"
expect_status "  de mo: 9 cau dung, 100/100, thang 10 = 10, Gioi" "[9, 100, 100, 10, 'Giỏi']" "$(D "[r for r in data if r[1] == '$T_OPEN'][0][5:10]")"
expect_status "  de kin: ghi Chua cong bo, khong so nao" "[None, 'Chưa công bố', None, None]" "$(D "[v or None for v in [r for r in data if r[1] == '$T_HIDDEN'][0][5:9]]")"
expect_status "  de kin: khong co xep loai" "[]" "$(D "[v for v in [r for r in data if r[1] == '$T_HIDDEN'][0][9:] if v]")"
expect_status "  ten lop giao bai" yes "$(D "'yes' if all(r[3] for r in data) else 'no'")"
expect_status "  diem trung binh chi tinh bai da cong bo (10, khong phai 6.67)" True "$(D "len(footer) == 1 and near(footer[0][8], 10)")"
expect_status "  khong co cong thuc nao" 0 "$(D "d['formulas']")"
expect_status "  khong lo diem hoc sinh khac" no "$(D "'yes' if any('GS ${STAMP}b' in str(v) for r in rows for v in r if v) else 'no'")"

section "Hoc sinh B: mot bai lam sai het"
expect_status "em B xuat → 200" 200 "$(download "$WORK/b.xlsx" /api/my/results/export "$TOK_B")"
dump "$WORK/b.xlsx"
expect_status "  mot dong du lieu" 1 "$(D "len(data)")"
expect_status "  0 diem thang 10, xep loai Yeu" "[0, 'Yếu']" "$(D "[data[0][8], data[0][9]]")"
expect_status "  trung binh = 0 (co so, khong bo trong)" True "$(D "len(footer) == 1 and footer[0][8] == 0")"

section "Hoc sinh C: chua nop bai nao"
expect_status "em C xuat → 200" 200 "$(download "$WORK/c.xlsx" /api/my/results/export "$TOK_C")"
dump "$WORK/c.xlsx"
expect_status "  chi co tieu de va hang dau cot" "0 0" "$(D "str(len(data)) + ' ' + str(len(footer))")"

section "Hoc sinh A theo ky: ?termId= loc nhu bang diem tren man hinh (0 = lop chua phan ky)"
# Ky khong dat la ky hien tai: khong dung vao ky hien tai cua database thu.
expect_status "tao ky cho bo kiem → 200" 200 "$(call POST /api/terms "$ADMIN" "{\"name\":\"Ky GS $STAMP\"}")"
TERM_GS=$(json_field id); TERM_IDS+=("$TERM_GS")
expect_status "gan lop mot vao ky → 200" 200 "$(call PUT "/api/student-groups/$G" "$ADMIN" "{\"name\":\"Lop BD $STAMP\",\"termId\":$TERM_GS}")"
expect_status "em A xuat theo ky → 200" 200 "$(download "$WORK/a-ky.xlsx" "/api/my/results/export?termId=$TERM_GS" "$TOK_A")"
dump "$WORK/a-ky.xlsx"
expect_status "  chi hai bai cua lop trong ky (de kin va de mo)" "['$T_HIDDEN', '$T_OPEN']" "$(D "sorted([r[1] for r in data])")"
expect_status "  ghi ten ky o phan dau file" yes "$(D "'yes' if any('Ky GS $STAMP' in str(r[0]) for r in rows[:5]) else 'no'")"
expect_status "  trung binh chi tinh bai da cong bo trong ky (10)" True "$(D "len(footer) == 1 and near(footer[0][8], 10)")"
expect_status "em A xuat lop chua phan ky (termId=0) → 200" 200 "$(download "$WORK/a-0.xlsx" /api/my/results/export?termId=0 "$TOK_A")"
dump "$WORK/a-0.xlsx"
expect_status "  chi bai cua lop hai" "['$T_G2']" "$(D "[r[1] for r in data]")"
expect_status "em A xuat ky khong ton tai → 200, file khong co dong du lieu" "0" "$(download "$WORK/a-x.xlsx" /api/my/results/export?termId=999999 "$TOK_A" > /dev/null; dump "$WORK/a-x.xlsx"; D "len(data)")"
expect_status "em A xuat khong tham so van la tat ca (3 bai)" 3 "$(download "$WORK/a-all.xlsx" /api/my/results/export "$TOK_A" > /dev/null; dump "$WORK/a-all.xlsx"; D "len(data)")"
expect_status "bo ky khoi lop mot → 200" 200 "$(call PUT "/api/student-groups/$G" "$ADMIN" "{\"name\":\"Lop BD $STAMP\",\"termId\":null}")"

# ── Giao vien xuat bang diem lop ─────────────────────────────────────────────────────────────────
section "Giao vien: chi giao vien dang day lop da cong bo xuat duoc"
expect_status "khong dang nhap → 401" 401 "$(download "$WORK/x.xlsx" "/api/student-groups/$G/grade-sheet")"
expect_status "hoc sinh → 403" 403 "$(download "$WORK/x.xlsx" "/api/student-groups/$G/grade-sheet" "$TOK_A")"
expect_status "quan tri vien → 403" 403 "$(download "$WORK/x.xlsx" "/api/student-groups/$G/grade-sheet" "$ADMIN")"
expect_status "lop khong ton tai → 404" 404 "$(download "$WORK/x.xlsx" /api/student-groups/999999/grade-sheet "$TEACHER")"
expect_status "lop con la ban nhap → 404" 404 "$(download "$WORK/x.xlsx" "/api/student-groups/$GDRAFT/grade-sheet" "$TEACHER")"
OTHER=$(new_teacher "$ADMIN" "$STAMP") || fail_fast "tao giao vien thu hai"
OTHER_ID=${OTHER%%|*}; EXTRA_TEACHERS+=("$OTHER_ID")
TOK_T2=$(login_and_clear_must_change "gv${STAMP}@kiemthu.vn" "123456" "Gv${STAMP}x")
expect_status "giao vien khac (khong day lop nay) → 404" 404 "$(download "$WORK/x.xlsx" "/api/student-groups/$G/grade-sheet" "$TOK_T2")"

section "Bang diem lop: mot dong moi hoc sinh, mot cot moi de cua giao vien"
expect_status "giao vien xuat lop → 200" 200 "$(download "$WORK/g.xlsx" "/api/student-groups/$G/grade-sheet" "$TEACHER")"
expect_status "  la file Excel" yes "$(header_has 'spreadsheetml.sheet')"
expect_status "  ten file theo ten lop, chi chu ASCII" yes "$(header_has "filename=bang-diem-lop-lop-bd-${STAMP}.xlsx")"
dump "$WORK/g.xlsx"
expect_status "  tieu de ghi ten lop" yes "$(D "'yes' if rows[0][0].startswith('BẢNG ĐIỂM LỚP') and 'Lop BD $STAMP' in rows[0][0] else 'no'")"
expect_status "  hang dau cot: ba cot dau, cac de giao cho lop theo thu tu soan, mot cot diem trung binh (khong co cot dem bai)" \
  "['STT', 'Mã học sinh', 'Họ và tên', '$T_OPEN', '$T_HIDDEN', 'Điểm trung bình']" "$(D "header")"
expect_status "  de giao qua lop khac (lop hai) khong co mat" no "$(D "'yes' if '$T_G2' in header else 'no'")"
expect_status "  de chua giao cho ai khong co mat" no "$(D "'yes' if '$T_UNUSED' in header else 'no'")"
expect_status "  nam hoc sinh theo ma" "['GS${STAMP}a', 'GS${STAMP}b', 'GS${STAMP}c', 'GS${STAMP}e', 'GS${STAMP}f']" "$(D "[r[1] for r in data]")"
expect_status "  cac cot dau co dinh khi cuon ngang" yes "$(D "'yes' if d['freeze'] and d['freeze'].startswith('D') else 'no'")"

section "Diem trong bang: quy ve thang 10, giao vien thay ca de kin"
expect_status "  A: de mo 10, de kin 0 (giao vien thay het)" "[10, 0]" "$(D "data[0][3:5]")"
expect_status "  A: trung binh 5" True "$(D "near(data[0][5], 5)")"
expect_status "  B: de mo 0 (lam sai het), de kin chua lam cung la 0 diem (khong de trong)" "[0, 0]" "$(D "data[1][3:5]")"
expect_status "  B: trung binh 0" True "$(D "data[1][5] == 0")"
expect_status "  C: duoc giao ca hai de nhung chua lam: 0 diem moi de" "[0, 0]" "$(D "data[2][3:5]")"
expect_status "  C: trung binh = 0 (0 diem cua hai de deu duoc tinh)" True "$(D "data[2][5] == 0")"
expect_status "  F vao lop sau khi giao: khong duoc giao de nao (dau —)" "['—', '—']" "$(D "data[4][3:5]")"
expect_status "  mot dong tong duy nhat: trung binh lop (khong co dong dem so em)" 1 "$(D "len(footer)")"
expect_status "  trung binh lop theo de: 2.5 va 0 (4 em duoc giao, em khong lam tinh 0)" True "$(D "near(footer[0][3], 2.5) and footer[0][4] == 0")"
expect_status "  trung binh chung = trung binh cac em (5, 0, 0, 0 → 1.25)" True "$(D "near(footer[0][5], 1.25)")"
expect_status "  em F (vao lop sau khi giao) khong co trung binh vi khong co bai nao de tinh" True "$(D "data[4][5] is None")"

section "Ten bat dau bang dau = van la van ban"
expect_status "  khong o nao la cong thuc" 0 "$(D "d['formulas']")"
expect_status "  ten giu nguyen nhu da go" yes "$(D "'yes' if data[3][2] == '$NAME_E' else 'no'")"

section "Tab Bai kiem tra trong lop: giao vien dang day thay cac de da giao cho lop"
expect_status "khong dang nhap → 401" 401 "$(call GET "/api/student-groups/$G/exams")"
expect_status "hoc sinh → 403" 403 "$(call GET "/api/student-groups/$G/exams" "$TOK_A")"
expect_status "giao vien khac (khong day lop nay) → 404" 404 "$(call GET "/api/student-groups/$G/exams" "$TOK_T2")"
expect_status "lop nhap cua giao vien → 404" 404 "$(call GET "/api/student-groups/$GDRAFT/exams" "$TEACHER")"
expect_status "lop khong ton tai → 404" 404 "$(call GET /api/student-groups/999999/exams "$TEACHER")"
expect_status "giao vien dang day doc → 200" 200 "$(call GET "/api/student-groups/$G/exams" "$TEACHER")"
expect_status "  hai de da giao cho lop (de chua giao cho ai khong co mat)" 2 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "  moi de ghi so em duoc giao: lop co 4 em dang hoc luc giao, em F vao sau nen khong tinh" True \
  "$(body | python -c "import json,sys;d=json.load(sys.stdin);print(str(all(x['assignedCount']==4 for x in d)).lower() == 'true')")"
expect_status "  so em da nop theo de: mo 2 (A, B), kin 1 (A)" "[2, 1]" \
  "$(body | python -c "import json,sys;d={x['title']:x['submittedCount'] for x in json.load(sys.stdin)};print([d['$T_OPEN'], d['$T_HIDDEN']])")"
expect_status "  moi de ghi giao vien soan" True \
  "$(body | python -c "import json,sys;print(all(x.get('teacherName') for x in json.load(sys.stdin)))")"
expect_status "  diem trung binh theo de chia cho SO EM DUOC GIAO (em chua lam tinh 0): de mo (10+0+0+0)/4 = 2.5, de kin 0" True \
  "$(body | python -c "import json,sys;d={x['title']:x for x in json.load(sys.stdin)};o=d['$T_OPEN'];print(abs(o['averageScore']/o['maxScore']*10-2.5)<0.01 and d['$T_HIDDEN']['averageScore']==0)")"
expect_status "quan tri vien van doc duoc → 200" 200 "$(call GET "/api/student-groups/$G/exams" "$ADMIN")"

section "Diem trung binh trong danh sach lop: bai da giao ma chua lam tinh 0, chua duoc giao bai nao thi chua co"
call GET "/api/student-groups/$G/students" "$TEACHER" > /dev/null
stu() { body | python -c "import json,sys;r=next(x for x in json.load(sys.stdin) if x['id']==$1);print(r['$2'])"; }
expect_status "  em A: duoc giao 3 bai (hai de lop nay, mot de lop hai)" 3 "$(stu "$S_A" assignedExams)"
expect_status "  em A: trung binh (10 + 0 + 10)/3 = 6.67, chia cho so bai da giao" True "$(body | python -c "import json,sys;r=next(x for x in json.load(sys.stdin) if x['id']==$S_A);print(abs(r['averageScore']-6.67)<0.01)")"
expect_status "  em B: lam sai het mot bai, bai kia chua lam: 2 bai da giao, trung binh 0" "2 True" "$(echo "$(stu "$S_B" assignedExams) $(body | python -c "import json,sys;print(next(x for x in json.load(sys.stdin) if x['id']==$S_B)['averageScore']==0)")")"
expect_status "  em C: chua lam bai nao nhung duoc giao 2 bai: trung binh 0 (khong phai 'chua co')" "2 True" "$(echo "$(stu "$S_C" assignedExams) $(body | python -c "import json,sys;print(next(x for x in json.load(sys.stdin) if x['id']==$S_C)['averageScore']==0)")")"
expect_status "  em F vao lop sau khi giao: chua duoc giao bai nao (giao dien hien 'chua co')" 0 "$(stu "$S_F" assignedExams)"

section "Bam vao mot bai trong tab: ket qua tung em cua lop, roi mo lai bai lam"
RES="/api/student-groups/$G/exams/$E_OPEN/results"
expect_status "khong dang nhap → 401" 401 "$(call GET "$RES")"
expect_status "hoc sinh → 403" 403 "$(call GET "$RES" "$TOK_A")"
expect_status "giao vien khac (khong day lop nay) → 404" 404 "$(call GET "$RES" "$TOK_T2")"
expect_status "giao vien dang day → 200" 200 "$(call GET "$RES" "$TEACHER")"
expect_status "  bon em duoc giao qua lop (em F vao sau nen khong co)" 4 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "  em A: da cham, co ma luot lam bai, gio bat dau va gio nop" True \
  "$(body | python -c "import json,sys;r=next(x for x in json.load(sys.stdin) if x['studentId']==$S_A);print(r['status']=='graded' and bool(r.get('attemptId')) and bool(r.get('startedAt')) and bool(r.get('submittedAt')))")"
expect_status "  em C chua lam: chua co ma luot lam bai" True \
  "$(body | python -c "import json,sys;r=next(x for x in json.load(sys.stdin) if x['studentId']==$S_C);print(not r.get('attemptId'))")"
ATT_A=$(body | python -c "import json,sys;print(next(x['attemptId'] for x in json.load(sys.stdin) if x['studentId']==$S_A))")
expect_status "de giao qua lop khac (lop hai): danh sach rong, khong lo diem lop khac" 0 \
  "$(call GET "/api/student-groups/$G/exams/$E_G2/results" "$TEACHER" > /dev/null; body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "quan tri vien doc duoc → 200" 200 "$(call GET "$RES" "$ADMIN")"
expect_status "giao vien khac chua day lop: mo bai lam cua em A → 404" 404 "$(call GET "/api/results/$ATT_A" "$TOK_T2")"
expect_status "giao vien soan de mo bai lam → 200" 200 "$(call GET "/api/results/$ATT_A" "$TEACHER")"

section "Lop khoa van xuat duoc"
expect_status "khoa lop → 200" 200 "$(call POST "/api/student-groups/$G/lock" "$ADMIN" '{"reason":"Ket thuc hoc ky, giu nguyen bang diem"}')"
expect_status "giao vien xuat lop khoa → 200" 200 "$(download "$WORK/g-locked.xlsx" "/api/student-groups/$G/grade-sheet" "$TEACHER")"
expect_status "mo khoa lop → 200" 200 "$(call POST "/api/student-groups/$G/unlock" "$ADMIN")"

section "Moi lop mot bang diem rieng: de giao qua lop khac khong lan sang"
expect_status "xuat lop hai → 200" 200 "$(download "$WORK/g2.xlsx" "/api/student-groups/$G2/grade-sheet" "$TEACHER")"
dump "$WORK/g2.xlsx"
expect_status "  lop hai chi co em A" "['GS${STAMP}a']" "$(D "[r[1] for r in data]")"
expect_status "  chi co de giao cho lop hai" "['$T_G2']" "$(D "header[3:4]")"
expect_status "  diem cua A o de lop hai" "[10]" "$(D "data[0][3:4]")"

section "Doi giao vien phu trach: nguoi moi van thay diem cac de nguoi cu da giao cho lop"
expect_status "admin doi giao vien lop sang giao vien moi → 200" 200 \
  "$(call PUT "/api/student-groups/$G/teacher" "$ADMIN" "{\"teacherId\":$OTHER_ID,\"reason\":\"Giao vien cu chuyen truong tu dau thang\"}")"
expect_status "giao vien cu khong con day lop → 404" 404 "$(download "$WORK/x.xlsx" "/api/student-groups/$G/grade-sheet" "$TEACHER")"
expect_status "giao vien moi xuat lop → 200" 200 "$(download "$WORK/g-new.xlsx" "/api/student-groups/$G/grade-sheet" "$TOK_T2")"
dump "$WORK/g-new.xlsx"
expect_status "  van du hai de cua giao vien cu" \
  "['STT', 'Mã học sinh', 'Họ và tên', '$T_OPEN', '$T_HIDDEN', 'Điểm trung bình']" "$(D "header")"
expect_status "  van du nam hoc sinh" 5 "$(D "len(data)")"
expect_status "  giao vien moi thay diem that cua em A (10 va 0)" "[10, 0]" "$(D "data[0][3:5]")"
expect_status "  giao vien moi mo duoc ket qua tung em cua de giao vien cu" 200 "$(call GET "$RES" "$TOK_T2")"
expect_status "  giao vien moi mo duoc bai lam cua em A o de giao vien cu, thay du dap an" 200 "$(call GET "/api/results/$ATT_A" "$TOK_T2")"
expect_json   "    canSeeAnswers = true" canSeeAnswers true
expect_status "  ... ke ca phan tom tat" 200 "$(call GET "/api/results/$ATT_A/summary" "$TOK_T2")"
expect_status "  giao vien moi cung thay tab Bai kiem tra trong lop, kem ten giao vien cu" 200 "$(call GET "/api/student-groups/$G/exams" "$TOK_T2")"
expect_status "    hai de cua giao vien cu" True "$(body | python -c "import json,sys;d=json.load(sys.stdin);print(len(d)==2 and all(x['teacherName']!='GV kiem thu $STAMP' for x in d))")"
expect_status "  va diem cua em B (0)" True "$(D "data[1][3] == 0")"

summary
