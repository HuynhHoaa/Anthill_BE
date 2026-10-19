#!/usr/bin/env bash
# De soan theo DANH SACH CAU HOI: giao vien chon tung cau (kem diem rieng tung cau), duyet xong moi luu,
# thay cho viec chi luu ma tran roi de server boc ngau nhien luc giao bai.
#
# Script nay khoa lai nhung dieu ma unit test khong cham toi duoc vi can HTTP + database that:
#   - luu va doc lai danh sach (thu tu, diem, dap an dung); cac cot ma tran dan xuat nhat quan;
#   - cau khong ton tai / cau bi an / danh sach rong: nhap duoc nhung xuat ban thi khong;
#   - "soan tu dong": rut dung so cau tung muc, khong trung cau da co, bao thieu, KHONG ghi gi;
#   - nhan ban chep ca danh sach; xoa cau dang nam trong de bi chan;
#   - ban de cua hoc sinh la dung bo cau do, dung diem tung cau, dung thu tu khi tat tron;
#   - da co em lam thi khoa danh sach (409, goi dung ten truong), doi ten van luu duoc;
#   - de cu theo ma tran van chay nhu truoc; sinh de rieng cho tung em tu choi voi de theo danh sach.
#
# Chay:  bash tests/api-checks/exam-question-list.sh   (API phai dang chay o $HOST, da migrate)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
TOPIC="DanhSach$STAMP"
QUESTIONS=(); EXAMS=(); STUDENTS=(); GROUP_IDS=()
GROUP_ID=""

cleanup() {
  section "Don du lieu"
  local exams=0 kept=0 questions=0 groups=0 locked=0
  for id in "${EXAMS[@]}"; do
    if [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]]; then exams=$((exams + 1)); else kept=$((kept + 1)); fi
  done
  for id in "${QUESTIONS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1))
  done
  for id in "${STUDENTS[@]}"; do
    [[ -n "$GROUP_ID" ]] && call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$ADMIN" "$REMOVE_BODY" > /dev/null
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" "$LOCK_BODY")" == "200" ]] && locked=$((locked + 1))
  done
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1))
  done
  printf '  da xoa %d/%d de · %d/%d cau hoi · %d/%d lop · khoa %d/%d hoc sinh\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "$locked" "${#STUDENTS[@]}"
  [[ $kept -gt 0 ]] && printf '  %d de o lai (da co em lam bai nen khong xoa duoc, va cau hoi cua no cung o lai) — dung thiet ke\n' "$kept"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
track() { local id; id=$(json_field id); [[ -n "$id" ]] && EXAMS+=("$id"); }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }

# Doc mot bieu thuc python tren response hien tai. $1 la bieu thuc theo bien `d` (JSON da parse).
py() { python -c "import json,sys; d=json.load(open(sys.argv[2], encoding='utf-8')); print($1)" "" "$BODY_FILE"; }

# ── Du lieu nen: 4 De, 3 Trung binh, 3 Kho, cung mot chu de + mot cau de dung cho nhap/an ──────────
section "Dung du lieu nen"
declare -A QID   # QID[easy1]=<id> ...
for pair in easy:4 medium:3 hard:3; do
  d="${pair%%:*}"
  for i in $(seq 1 "${pair##*:}"); do
    c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
    [[ "$c" != "200" ]] && fail_fast "tao cau hoi $d$i: HTTP $c — $(body)"
    QID[$d$i]=$(json_field id); QUESTIONS+=("${QID[$d$i]}")
  done
done
printf '  %d cau hoi chu de %s\n' "${#QUESTIONS[@]}" "$TOPIC"

GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KiemThuDS $STAMP" "Nhom kiem thu danh sach cau") || fail_fast "khong dung duoc lop nen"
GROUP_IDS+=("$GROUP_ID")
payload="{\"fullName\":\"Hoc sinh DS $STAMP\",\"email\":\"ds$STAMP@kiemthu.vn\",\"username\":\"ds$STAMP\",\"code\":\"DS$STAMP\",\"password\":\"123456\",\"groupId\":$GROUP_ID}"
c=$(call POST /api/students "$ADMIN" "$payload"); [[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
STUDENTS+=("$(json_field id)")
STUDENT=$(login_and_clear_must_change "ds$STAMP" "123456" "Ds$STAMP")

# $1 tieu de · $2 mang JSON cac {questionId, point} · $3 lifecycle (tuy chon) · $4/$5 shuffle cau/dap an (mac dinh false)
list_body() {
  local life=""
  [[ -n "${3:-}" ]] && life=",\"lifecycle\":\"$3\""
  printf '{"title":"%s","description":"","durationMinutes":30,"shuffleQuestions":%s,"shuffleAnswers":%s,"showResultAfterSubmit":true,"openAt":"2020-01-01T00:00:00Z","closeAt":"2035-01-01T00:00:00Z","questions":%s%s}' \
    "$1" "${4:-false}" "${5:-false}" "$2" "$life"
}
create_list() { call POST /api/exams "$TEACHER" "$(list_body "$@")"; }

E1=${QID[easy1]}; E2=${QID[easy2]}; E3=${QID[easy3]}; E4=${QID[easy4]}
M1=${QID[medium1]}; M2=${QID[medium2]}; M3=${QID[medium3]}
H1=${QID[hard1]}; H2=${QID[hard2]}; H3=${QID[hard3]}

# ── Tao va doc lai ────────────────────────────────────────────────────────────────────────────────
section "Tao de theo danh sach: 2 De 10 diem, 1 Trung binh 30, 1 Kho 50 = 100 (khong gui cot ma tran nao)"
LIST="[{\"questionId\":$H1,\"point\":50},{\"questionId\":$E1,\"point\":10},{\"questionId\":$M1,\"point\":30},{\"questionId\":$E2,\"point\":10}]"
expect_status "tao de xuat ban → 200" 200 "$(create_list "DS chinh $STAMP" "$LIST")"
EXAM=$(json_field id); track
expect_json "  usesQuestionList = true" usesQuestionList true
expect_json "  totalQuestions dan xuat = 4" totalQuestions 4
expect_json "  totalScore = 100" totalScore 100
expect_json "  lifecycle = open" lifecycle open
expect_json "  tyle De 50 (2/4)" easyPercentage 50
expect_json "  tyle Trung binh 25" mediumPercentage 25
expect_json "  tyle Kho 25" hardPercentage 25

section "GET /api/exams/{id}/questions — dung thu tu giao vien xep, kem diem va dap an dung"
expect_status "doc danh sach → 200" 200 "$(call GET "/api/exams/$EXAM/questions" "$TEACHER")"
expect_status "  cau dau la cau Kho vua xep dau" "$H1" "$(py "d[0]['question']['id']")"
expect_status "  diem cau dau = 50" 50 "$(py "int(d[0]['point'])")"
expect_status "  thu tu 0,1,2,3" "0,1,2,3" "$(py "','.join(str(r['ordinal']) for r in d)")"
expect_status "  cau thu 2 la cau De thu nhat" "$E1" "$(py "d[1]['question']['id']")"
expect_status "  co dap an dung de duyet" yes "$(py "'yes' if any(a['isCorrect'] for a in d[0]['question']['answers']) else 'no'")"
expect_status "  quan tri vien doc duoc → 200" 200 "$(call GET "/api/exams/$EXAM/questions" "$ADMIN")"

# ── Nhap, danh sach rong, cau khong hop le ────────────────────────────────────────────────────────
section "Nhap duoc danh sach rong; xuat ban thi khong"
expect_status "nhap rong → 200" 200 "$(create_list "DS rong $STAMP" "[]" draft)"
DRAFT=$(json_field id); track
expect_json "  lifecycle = draft" lifecycle draft
expect_json "  usesQuestionList = true" usesQuestionList true
expect_status "xuat ban de rong (POST /publish) → 400" 400 "$(call POST "/api/exams/$DRAFT/publish" "$TEACHER")"
expect_status "  noi phai co cau hoi" yes "$(body_has 'chưa có câu hỏi')"
expect_status "tao thang de rong co hieu luc → 400" 400 "$(create_list "DS rong 2 $STAMP" "[]")"

section "Cau khong ton tai / bi an / trung / diem sai"
expect_status "cau khong ton tai → 400" 400 "$(create_list "DS ma sai $STAMP" "[{\"questionId\":99999999,\"point\":10}]")"
expect_status "  noi cau khong con trong ngan hang" yes "$(body_has 'không còn trong ngân hàng')"
expect_status "cung mot cau hai lan → 400" 400 "$(create_list "DS trung $STAMP" "[{\"questionId\":$E1,\"point\":5},{\"questionId\":$E1,\"point\":5}]")"
expect_status "diem am → 400" 400 "$(create_list "DS diem am $STAMP" "[{\"questionId\":$E1,\"point\":-1}]")"
expect_status "diem 3 chu so thap phan → 400" 400 "$(create_list "DS diem le $STAMP" "[{\"questionId\":$E1,\"point\":1.234}]")"
expect_status "mot muc rong trong danh sach (questions: [null]) → 400, khong phai 500" 400 \
  "$(create_list "DS null $STAMP" "[{\"questionId\":$E1,\"point\":5},null]")"
expect_status "  noi co muc rong" yes "$(body_has 'mục rỗng')"
expect_status "tong diem 100 cau x 100 vuot tran → 400" 400 \
  "$(create_list "DS qua tran $STAMP" "[$(python -c "print(','.join('{\"questionId\":%d,\"point\":100}' % i for i in range(1,102)))")]")"
expect_status "tong 0 diem xuat ban → 400" 400 "$(create_list "DS 0 diem $STAMP" "[{\"questionId\":$E1,\"point\":0}]")"
expect_status "  noi tong diem phai lon hon 0" yes "$(body_has 'lớn hơn 0')"

# Cau bi an: giu duoc trong ban nhap, khong xuat ban duoc.
call PATCH "/api/questions/$E4/status" "$TEACHER" '{"status":"hidden"}' > /dev/null
expect_status "nhap co cau bi an → 200" 200 "$(create_list "DS an nhap $STAMP" "[{\"questionId\":$E4,\"point\":10}]" draft)"
HIDDEN_DRAFT=$(json_field id); track
expect_status "xuat ban de chua cau bi an → 400" 400 "$(call POST "/api/exams/$HIDDEN_DRAFT/publish" "$TEACHER")"
expect_status "  noi cau bi an" yes "$(body_has 'đang bị ẩn')"
expect_status "tao thang de co hieu luc chua cau bi an → 400" 400 \
  "$(create_list "DS an $STAMP" "[{\"questionId\":$E4,\"point\":10}]")"

# Doi chieu cua de theo danh sach: "can" la chinh cac cau da chon, "dang co" la so cau trong do con bat.
section "GET /api/exams/{id}/blueprint cua de theo danh sach"
expect_status "de binh thuong → 200" 200 "$(call GET "/api/exams/$EXAM/blueprint" "$TEACHER")"
expect_json "  hop le" isValid true
expect_status "  can 2 De, 1 Trung binh, 1 Kho (dung cac cau da chon)" "2,1,1" \
  "$(py "','.join(str(d['required'][k]) for k in ('easy','medium','hard'))")"
expect_status "de co cau bi an → 200" 200 "$(call GET "/api/exams/$HIDDEN_DRAFT/blueprint" "$TEACHER")"
expect_json "  khong hop le" isValid false
expect_status "  noi cau bi an" yes "$(body_has 'đang bị ẩn')"
expect_status "  can 1 De, dang co 0" "1,0" "$(py "str(d['required']['easy']) + ',' + str(d['available']['easy'])")"

# ── Soan tu dong ──────────────────────────────────────────────────────────────────────────────────
section "POST /api/exams/compose-questions"
BEFORE=$(call GET "/api/exams/$EXAM/questions" "$TEACHER" > /dev/null; py "len(d)")
expect_status "2 De + 1 Trung binh loai tru cau da co → 200" 200 \
  "$(call POST /api/exams/compose-questions "$TEACHER" "{\"easy\":2,\"medium\":1,\"hard\":0,\"topic\":\"$TOPIC\",\"excludeQuestionIds\":[$E1,$M1]}")"
expect_status "  rut 3 cau" 3 "$(py "len(d['questions'])")"
expect_status "  khong thieu muc nao" "0,0,0" "$(py "','.join(str(d['missing'][k]) for k in ('easy','medium','hard'))")"
expect_status "  khong rut trung cau da loai tru" no \
  "$(py "'yes' if {$E1,$M1} & {q['id'] for q in d['questions']} else 'no'")"
expect_status "  khong rut cau bi an" no "$(py "'yes' if $E4 in {q['id'] for q in d['questions']} else 'no'")"
expect_status "  dung 2 De 1 Trung binh" "easy,easy,medium" "$(py "','.join(q['difficulty'] for q in d['questions'])")"

expect_status "xin 5 Kho trong khi chi con 2 (loai H3 nua) → 200" 200 \
  "$(call POST /api/exams/compose-questions "$TEACHER" "{\"easy\":0,\"medium\":0,\"hard\":5,\"topic\":\"$TOPIC\",\"excludeQuestionIds\":[$H1]}")"
expect_status "  rut duoc 2 cau" 2 "$(py "len(d['questions'])")"
expect_status "  bao thieu 3 cau Kho" 3 "$(py "d['missing']['hard']")"

expect_status "khong xin cau nao → 400" 400 "$(call POST /api/exams/compose-questions "$TEACHER" '{"easy":0,"medium":0,"hard":0}')"
expect_status "hoc sinh goi → 403" 403 "$(call POST /api/exams/compose-questions "$STUDENT" '{"easy":1}')"
expect_status "quan tri vien goi → 403" 403 "$(call POST /api/exams/compose-questions "$ADMIN" '{"easy":1}')"
expect_status "soan tu dong khong ghi gi: de van con nguyen so cau" "$BEFORE" \
  "$(call GET "/api/exams/$EXAM/questions" "$TEACHER" > /dev/null; py "len(d)")"

# ── Sua danh sach ─────────────────────────────────────────────────────────────────────────────────
section "Sua: thay mot cau, doi thu tu, doi diem — chua giao ai nen ghi thang"
LIST2="[{\"questionId\":$E1,\"point\":20},{\"questionId\":$H2,\"point\":40},{\"questionId\":$M2,\"point\":30},{\"questionId\":$E3,\"point\":10}]"
expect_status "thay H1→H2, M1→M2, E2→E3, doi thu tu → 200" 200 \
  "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(list_body "DS chinh $STAMP" "$LIST2")")"
expect_json "  totalScore = 100" totalScore 100
expect_status "  danh sach moi, dung thu tu" "$E1,$H2,$M2,$E3" \
  "$(call GET "/api/exams/$EXAM/questions" "$TEACHER" > /dev/null; py "','.join(str(r['question']['id']) for r in d)")"
expect_status "  diem tung cau" "20,40,30,10" \
  "$(py "','.join(str(int(r['point'])) for r in d)")"

# Gui du cac cot ma tran nhung bo `questions`: de theo danh sach khong nhan, vi ma tran trong request se bi
# hieu nham la cau hinh moi cua mot de dang co cau hoi.
expect_status "PUT de theo danh sach ma bo 'questions' → 400" 400 \
  "$(call PUT "/api/exams/$EXAM" "$TEACHER" '{"title":"x","durationMinutes":30,"totalQuestions":4,"easyPercentage":50,"mediumPercentage":25,"hardPercentage":25,"pointEasy":10,"pointMedium":20,"pointHard":40,"openAt":"2020-01-01T00:00:00Z","closeAt":"2035-01-01T00:00:00Z"}')"
expect_status "  noi phai gui kem danh sach" yes "$(body_has 'gửi kèm danh sách')"
expect_status "  danh sach cua de khong bi dong toi" "$E1,$H2,$M2,$E3" \
  "$(call GET "/api/exams/$EXAM/questions" "$TEACHER" > /dev/null; py "','.join(str(r['question']['id']) for r in d)")"

# ── Nhan ban ──────────────────────────────────────────────────────────────────────────────────────
section "Nhan ban chep ca danh sach"
expect_status "nhan ban → 200" 200 "$(call POST "/api/exams/$EXAM/duplicate" "$TEACHER")"
COPY=$(json_field id); track
expect_json "  ban sao la nhap" lifecycle draft
expect_json "  ban sao theo danh sach" usesQuestionList true
expect_status "  totalScore = 100" 100 "$(py "int(d['totalScore'])")"
expect_status "  ban sao co du 4 cau dung thu tu" "$E1,$H2,$M2,$E3" \
  "$(call GET "/api/exams/$COPY/questions" "$TEACHER" > /dev/null; py "','.join(str(r['question']['id']) for r in d)")"

# ── Xoa cau dang nam trong de ─────────────────────────────────────────────────────────────────────
section "Cau hoi dang nam trong mot de thi khong xoa duoc"
expect_status "xoa cau E1 dang nam trong de → 409" 409 "$(call DELETE "/api/questions/$E1" "$TEACHER")"
expect_status "  goi y an cau hoi thay vi xoa" yes "$(body_has 'ẩn câu hỏi')"

# ── Giao bai: ban de la dung bo cau, dung diem ────────────────────────────────────────────────────
section "Giao bai va ban de cua hoc sinh"
expect_status "sinh de rieng cho tung em voi de theo danh sach → 400" 400 \
  "$(call POST "/api/exams/$EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":true,\"notify\":false}")"
expect_status "giao cho lop (de chung) → 200" 200 \
  "$(call POST "/api/exams/$EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":false}")"
expect_json "  giao cho 1 em" assignedCount 1

call GET /api/my/exams "$STUDENT" > /dev/null
ASSIGNMENT=$(python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((i['assignmentId'] for i in items if i['examId'] == int(sys.argv[2])), ''))" "$BODY_FILE" "$EXAM")
expect_status "bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$STUDENT" "{\"assignmentId\":$ASSIGNMENT}")"
expect_status "  ban de 4 cau" 4 "$(py "len(d['questions'])")"
expect_status "  tat tron: dung thu tu giao vien xep" "Cau easy 1 - $STAMP|Cau hard 2 - $STAMP|Cau medium 2 - $STAMP|Cau easy 3 - $STAMP" \
  "$(py "'|'.join(q['content'] for q in d['questions'])")"
expect_status "  diem tung cau 20,40,30,10" "20,40,30,10" "$(py "','.join(str(int(q['point'])) for q in d['questions'])")"
expect_status "  diem toi da cua bai lam 100" 100 "$(py "int(d['maxScore'])")"

# ── Da co em lam: khoa danh sach ──────────────────────────────────────────────────────────────────
section "Da co em bat dau lam: doi danh sach / diem bi khoa, doi ten van luu"
LIST3="[{\"questionId\":$E1,\"point\":25},{\"questionId\":$H2,\"point\":35},{\"questionId\":$M2,\"point\":30},{\"questionId\":$E3,\"point\":10}]"
expect_status "doi diem mot cau → 409" 409 "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(list_body "DS chinh $STAMP" "$LIST3")")"
expect_status "  goi dung ten truong bi khoa" yes "$(body_has 'danh sách câu hỏi và điểm từng câu')"
LIST4="[{\"questionId\":$E1,\"point\":20},{\"questionId\":$H2,\"point\":40},{\"questionId\":$M2,\"point\":30},{\"questionId\":$E4,\"point\":10}]"
expect_status "thay mot cau bang cau khac cung muc → 409" 409 "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(list_body "DS chinh $STAMP" "$LIST4")")"
expect_status "chi doi ten, danh sach y nguyen → 200" 200 "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(list_body "DS doi ten $STAMP" "$LIST2")")"
expect_json "  ten moi" title "DS doi ten $STAMP"
expect_status "  danh sach van nguyen" "$E1,$H2,$M2,$E3" \
  "$(call GET "/api/exams/$EXAM/questions" "$TEACHER" > /dev/null; py "','.join(str(r['question']['id']) for r in d)")"

# ── De cu theo ma tran van chay ───────────────────────────────────────────────────────────────────
section "De theo ma tran khong bi anh huong"
expect_status "tao de ma tran → 200" 200 "$(call POST /api/exams "$TEACHER" "{\"title\":\"Ma tran $STAMP\",\"description\":\"\",\"topic\":\"$TOPIC\",\"durationMinutes\":30,\"totalQuestions\":4,\"easyPercentage\":50,\"mediumPercentage\":25,\"hardPercentage\":25,\"pointEasy\":10,\"pointMedium\":20,\"pointHard\":40,\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":true,\"openAt\":\"2020-01-01T00:00:00Z\",\"closeAt\":\"2035-01-01T00:00:00Z\"}")"
MATRIX=$(json_field id); track
expect_json "  usesQuestionList = false" usesQuestionList false
expect_json "  totalScore = 2x10 + 20 + 40 = 80" totalScore 80
expect_status "GET questions cua de ma tran → 200, rong" 200 "$(call GET "/api/exams/$MATRIX/questions" "$TEACHER")"
expect_status "  danh sach rong" 0 "$(py "len(d)")"
expect_status "chuyen de ma tran sang danh sach khi chua giao ai → 200" 200 \
  "$(call PUT "/api/exams/$MATRIX" "$TEACHER" "$(list_body "Ma tran $STAMP" "[{\"questionId\":$E1,\"point\":60},{\"questionId\":$H3,\"point\":40}]")")"
expect_json "  gio theo danh sach" usesQuestionList true
expect_json "  totalScore = 100" totalScore 100

summary
