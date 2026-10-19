#!/usr/bin/env bash
# Viec hoc cua chinh hoc sinh: "Lop hoc cua toi" (GET /api/my/groups, /api/my/groups/{id}, GET /api/my/exams?groupId=) va "Bang diem"
# (GET /api/my/transcript: cac mon em co bai kem diem trung binh tung mon), cung bo loc theo ky hoc (GET /api/my/terms, transcript?termId=).
#
# Chay:  bash tests/api-checks/student-learning.sh   (API phai dang chay o $HOST, database thu)
#
# Quy tac duoc canh gac o day:
#   - chi hoc sinh goi duoc (khach 401, giao vien va quan tri vien 403); danh tinh suy tu token, khong co id hoc sinh tren duong dan;
#   - lop nhap (chua cong bo) khong hien voi hoc sinh; lop em khong hoc, lop khong ton tai → 404 (khong phan biet);
#   - moi the bai mang lop da giao (groupId, groupName) va loc theo lop duoc; bai da nop van nam trong danh sach cua lop;
#   - diem trung binh mon tinh tren bai da nop va duoc xem diem, cong bai het han em khong lam (0 diem); KHONG tinh bai con lam duoc
#     va bai da nop nhung giao vien chua cho xem diem (khong lo diem bi giau qua trung binh);
#   - ky hoc: em chi thay cac ky em co lop (cung muc "Chua phan ky", id 0, khi co lop khong ky); bang diem loc duoc theo ky.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=(); TERM_IDS=()

cleanup() {
  section "Don du lieu"
  local exams=0 questions=0 groups=0
  for id in "${EXAMS[@]}";     do [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]] && exams=$((exams + 1)); done
  for id in "${QUESTIONS[@]}"; do [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1)); done
  for sid in "${STUDENTS[@]}"; do
    for gid in "${GROUP_IDS[@]}"; do call DELETE "/api/student-groups/$gid/students/$sid" "$ADMIN" "$REMOVE_BODY" > /dev/null; done
    call PATCH "/api/students/$sid/status" "$ADMIN" "$LOCK_BODY" > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1)); done
  for id in "${TERM_IDS[@]}"; do call DELETE "/api/terms/$id" "$ADMIN" > /dev/null; done
  printf '  xoa %d/%d de · %d/%d cau hoi · %d/%d lop · khoa %d hoc sinh\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "${#STUDENTS[@]}"
  printf '  de da co bai nop thi khong xoa duoc — dung thiet ke, khong phai loi don dep.\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
J() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$(to_native_path "$BODY_FILE")"; }

new_student() { # $1 hau to, $2 ho ten, $3 id lop
  local c; c=$(call POST /api/students "$ADMIN" "{\"fullName\":\"$2\",\"email\":\"sl$1@kiemthu.vn\",\"username\":\"sl$1\",\"code\":\"SL$1\",\"password\":\"123456\",\"groupId\":$3}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $1: HTTP $c — $(body)"
  json_field id
}

section "Dung du lieu nen: hai lop da cong bo, mot lop nhap, hai hoc sinh, bon de"
G1=$(new_class "$ADMIN" "$TEACHER" "Lop SL mot $STAMP") || fail_fast "tao lop mot"; GROUP_IDS+=("$G1")
G2=$(new_class "$ADMIN" "$TEACHER" "Lop SL hai $STAMP") || fail_fast "tao lop hai"; GROUP_IDS+=("$G2")
call GET /api/auth/me "$TEACHER" > /dev/null; TEACHER_ID=$(json_field id)
[[ "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop SL nhap $STAMP\",\"teacherId\":$TEACHER_ID}")" != "200" ]] && fail_fast "tao lop nhap"
GD=$(json_field id); GROUP_IDS+=("$GD")

S1=$(new_student "${STAMP}a" "Hoc sinh SL A" "$G1"); S2=$(new_student "${STAMP}b" "Hoc sinh SL B" "$G1"); STUDENTS+=("$S1" "$S2")
for g in "$G2" "$GD"; do [[ "$(call POST "/api/student-groups/$g/students" "$ADMIN" "{\"studentIds\":[$S1]}")" != "200" ]] && fail_fast "xep em A vao lop $g"; done
TOK1=$(login_and_clear_must_change "sl${STAMP}a" "123456" "Sl${STAMP}a")
TOK2=$(login_and_clear_must_change "sl${STAMP}b" "123456" "Sl${STAMP}b")

for pair in easy:1 easy:2 easy:3 easy:4 medium:1 medium:2 medium:3 hard:1 hard:2; do
  d="${pair%%:*}"; i="${pair##*:}"
  [[ "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"HocTap$STAMP\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")" != "200" ]] && fail_fast "tao cau hoi"
  QUESTIONS+=("$(json_field id)")
done

new_exam() { # $1 tieu de  $2 lop  $3 showResultAfterSubmit  $4 openAt  $5 closeAt
  [[ "$(call POST /api/exams "$TEACHER" "{\"title\":\"$1\",\"topic\":\"HocTap$STAMP\",\"durationMinutes\":45,\"totalQuestions\":9,\"easyPercentage\":45,\"mediumPercentage\":33,\"hardPercentage\":22,\"pointEasy\":10,\"pointMedium\":10,\"pointHard\":15,\"openAt\":\"$4\",\"closeAt\":\"$5\",\"shuffleQuestions\":true,\"shuffleAnswers\":true,\"showResultAfterSubmit\":$3}")" != "200" ]] && fail_fast "tao de $1: $(body)"
  NEW_EXAM_ID=$(json_field id); EXAMS+=("$NEW_EXAM_ID")
  [[ "$(call POST "/api/exams/$NEW_EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$2],\"notify\":false}")" != "200" ]] && fail_fast "giao de $1: $(body)"
}

take_exam() { # $1 token  $2 examId  $3 dung|sai
  call GET /api/my/exams "$1" > /dev/null
  local assignment; assignment=$(J "next(i['assignmentId'] for i in d if i['examId'] == $2)")
  call POST /api/exam-attempts "$1" "{\"assignmentId\":$assignment}" > /dev/null
  local attempt; attempt=$(json_field attemptId)
  local session; session=$(mktemp -t anthill-sl.XXXXXX); cp "$BODY_FILE" "$session"
  local answers; answers=$(python -c "
import json,sys
label = 'Dung' if sys.argv[2] == 'dung' else 'Sai'
questions = json.load(open(sys.argv[1], encoding='utf-8'))['questions']
print(json.dumps([{'questionId': q['id'], 'answerId': next(a['id'] for a in q['answers'] if a['content'] == label)} for q in questions]))
" "$(to_native_path "$session")" "$3")
  rm -f "$session"
  call POST "/api/exam-attempts/$attempt/submit" "$1" "{\"answers\":$answers}" > /dev/null
}

OPEN="2020-01-01T00:00:00Z"; CLOSE="2035-01-01T00:00:00Z"
new_exam "SL de dung $STAMP" "$G1" true "$OPEN" "$CLOSE";             E_RIGHT=$NEW_EXAM_ID     # em A lam dung het: 10
new_exam "SL de kin $STAMP" "$G1" false "$OPEN" "$CLOSE";             E_HIDDEN=$NEW_EXAM_ID    # em A lam, giao vien chua cho xem diem
new_exam "SL de het han $STAMP" "$G1" true "2020-01-01T00:00:00Z" "2020-01-02T00:00:00Z"; E_MISSED=$NEW_EXAM_ID   # khung gio da dong, khong ai lam
new_exam "SL lop hai $STAMP" "$G2" true "$OPEN" "$CLOSE";             E_G2=$NEW_EXAM_ID        # em A lam sai het: 0
take_exam "$TOK1" "$E_RIGHT" dung
take_exam "$TOK1" "$E_HIDDEN" dung
take_exam "$TOK1" "$E_G2" sai

section "Chi hoc sinh goi duoc"
for path in /api/my/groups "/api/my/groups/$G1" /api/my/transcript /api/my/terms; do
  expect_status "khach → 401 ($path)" 401 "$(call GET "$path")"
  expect_status "giao vien → 403 ($path)" 403 "$(call GET "$path" "$TEACHER")"
  expect_status "quan tri vien → 403 ($path)" 403 "$(call GET "$path" "$ADMIN")"
done

section "Lop hoc cua toi: chi lop da cong bo em dang hoc"
expect_status "em A: danh sach lop → 200" 200 "$(call GET /api/my/groups "$TOK1")"
expect_status "  hai lop (lop nhap khong hien)" "Lop SL hai $STAMP,Lop SL mot $STAMP" "$(J "','.join(sorted(g['name'] for g in d))")"
expect_status "  moi lop ghi giao vien phu trach" True "$(J "all(g['teacherName'] for g in d)")"
expect_status "  lop mot: 3 bai da giao, 2 da nop, 1 het han chua lam, 0 dang mo" "3 2 1 0" \
  "$(J "' '.join(str(next(g for g in d if g['id'] == $G1)[k]) for k in ('assignedCount','completedCount','expiredCount','openCount'))")"
expect_status "  si so lop mot = 2" 2 "$(J "next(g for g in d if g['id'] == $G1)['studentCount']")"
expect_status "em B: chi lop mot" "Lop SL mot $STAMP" "$(call GET /api/my/groups "$TOK2" > /dev/null; J "','.join(g['name'] for g in d)")"
expect_status "mot lop cua em → 200" 200 "$(call GET "/api/my/groups/$G1" "$TOK1")"
expect_status "lop em khong hoc → 404" 404 "$(call GET "/api/my/groups/$G2" "$TOK2")"
expect_status "lop nhap (em A co ten nhung chua cong bo) → 404" 404 "$(call GET "/api/my/groups/$GD" "$TOK1")"
expect_status "lop khong ton tai → 404" 404 "$(call GET /api/my/groups/999999 "$TOK1")"

section "Bai kiem tra cua mot lop: the bai ghi lop, loc theo lop, ke ca bai da nop"
expect_status "danh sach bai khong loc → 200" 200 "$(call GET /api/my/exams "$TOK1")"
expect_status "  moi the bai ghi ten lop da giao" True "$(J "all(i.get('groupName') and i.get('groupId') for i in d)")"
expect_status "loc theo lop mot → 200" 200 "$(call GET "/api/my/exams?groupId=$G1" "$TOK1")"
expect_status "  ba bai cua lop mot (ke ca bai da nop va bai het han)" 3 "$(J "len(d)")"
expect_status "  toan bo thuoc lop mot" True "$(J "all(i['groupId'] == $G1 for i in d)")"
expect_status "loc theo lop hai: mot bai" 1 "$(call GET "/api/my/exams?groupId=$G2" "$TOK1" > /dev/null; J "len(d)")"
expect_status "loc theo lop em khong hoc: danh sach rong (khong lo bai lop khac)" 0 "$(call GET "/api/my/exams?groupId=$G2" "$TOK2" > /dev/null; J "len(d)")"

section "Bang diem: diem trung binh tung mon, bai het han tinh 0, bai giau diem khong tinh"
expect_status "em A: bang diem → 200" 200 "$(call GET /api/my/transcript "$TOK1")"
expect_status "  mot mon (giao vien an.nv chi day mot mon)" 1 "$(J "len(d['subjects'])")"
expect_status "  bon bai trong mon (3 bai lop mot + 1 bai lop hai)" 4 "$(J "d['subjects'][0]['totalExams']")"
expect_status "  duoc tinh 3 bai: de dung 10, de lop hai 0, de het han 0" 3 "$(J "d['subjects'][0]['countedExams']")"
expect_status "  diem trung binh = (10 + 0 + 0) / 3 = 3.33" True "$(J "abs(d['subjects'][0]['average'] - 3.33) < 0.01")"
expect_status "  diem chung bang diem trung binh mon duy nhat" True "$(J "abs(d['overallAverage'] - d['subjects'][0]['average']) < 0.001")"
expect_status "  1 bai da nop nhung chua cong bo diem (khong tinh)" 1 "$(J "d['subjects'][0]['hiddenExams']")"
expect_status "  1 bai bo lo (het han, tinh 0)" 1 "$(J "d['subjects'][0]['missedExams']")"
expect_status "  bai giau diem khong co so nao trong bang diem" True \
  "$(J "all(e.get('score') is None for e in d['subjects'][0]['exams'] if e['title'] == 'SL de kin $STAMP')")"
expect_status "  bai giau diem khong nam trong trung binh" False "$(J "next(e for e in d['subjects'][0]['exams'] if e['title'] == 'SL de kin $STAMP')['counted']")"
expect_status "  bai lam dung het co diem 10 va co ma luot lam (de mo trang ket qua)" True \
  "$(J "(lambda e: e.get('score') == 10 and bool(e.get('attemptId')))(next(e for e in d['subjects'][0]['exams'] if e['title'] == 'SL de dung $STAMP'))")"
expect_status "  moi bai ghi lop cua no" True "$(J "all(e.get('groupName') for e in d['subjects'][0]['exams'])")"
expect_status "em B (chua nop bai nao): bai con lam duoc chua tinh, bai het han tinh 0" "2 1 0" \
  "$(call GET /api/my/transcript "$TOK2" > /dev/null; J "' '.join(str(d['subjects'][0][k]) for k in ('pendingExams','countedExams','hiddenExams'))")"
expect_status "  em B: trung binh 0 (chi bai het han duoc tinh)" True "$(J "d['subjects'][0]['average'] == 0")"
expect_status "  em B khong thay bai cua lop hai" 3 "$(J "d['subjects'][0]['totalExams']")"

section "Ky hoc: em chi thay ky em co lop, loc bang diem theo ky"
# Ky khong dat la ky hien tai: khong dung vao ky hien tai cua database thu.
expect_status "tao ky cho bo kiem → 200" 200 "$(call POST /api/terms "$ADMIN" "{\"name\":\"Ky SL $STAMP\"}")"
TERM_SL=$(json_field id); TERM_IDS+=("$TERM_SL")
expect_status "em A: /api/my/terms → 200" 200 "$(call GET /api/my/terms "$TOK1")"
expect_status "  chua lop nao co ky: chi co muc chua phan ky (id 0)" "0" "$(J "' '.join(str(t['id']) for t in d)")"
expect_status "gan lop mot vao ky → 200" 200 "$(call PUT "/api/student-groups/$G1" "$ADMIN" "{\"name\":\"Lop SL mot $STAMP\",\"termId\":$TERM_SL}")"

call GET /api/my/terms "$TOK1" > /dev/null
expect_status "em A: hai muc ky — ky cua lop mot va muc 'chua phan ky' (id 0)" "$TERM_SL 0" "$(J "' '.join(str(t['id']) for t in d)")"
expect_status "  ky cua lop mot dem 1 lop" 1 "$(J "d[0]['groupCount']")"
expect_status "  muc chua phan ky dem 1 lop (lop nhap khong tinh) va khong la ky hien tai" "1 False" "$(J "str(d[1]['groupCount']) + ' ' + str(d[1]['isCurrent'])")"
expect_status "  muc chua phan ky dung cuoi danh sach" True "$(J "d[-1]['id'] == 0")"
call GET /api/my/groups "$TOK1" > /dev/null
expect_status "  the lop mot ghi ky" "$TERM_SL" "$(J "next(g for g in d if g['id'] == $G1)['termId']")"
expect_status "  the lop hai khong ghi ky" True "$(J "next(g for g in d if g['id'] == $G2).get('termId') is None")"
expect_status "em B: chi mot ky (lop mot), khong co muc chua phan ky" "$TERM_SL" "$(call GET /api/my/terms "$TOK2" > /dev/null; J "' '.join(str(t['id']) for t in d)")"

section "Bang diem theo ky: loc ky, chua phan ky, moi ky"
expect_status "tat ca cac ky: 4 bai, trung binh 3.33" "4 3.33" "$(call GET /api/my/transcript "$TOK1" > /dev/null; J "str(d['subjects'][0]['totalExams']) + ' ' + str(round(d['subjects'][0]['average'], 2))")"
expect_status "ky cua lop mot: 3 bai, duoc tinh 2, trung binh 5" "3 2 5" \
  "$(call GET "/api/my/transcript?termId=$TERM_SL" "$TOK1" > /dev/null; J "' '.join(str(x) for x in (d['subjects'][0]['totalExams'], d['subjects'][0]['countedExams'], d['subjects'][0]['average']))")"
expect_status "  diem chung cua bang diem loc bang trung binh mon duy nhat" True "$(J "d['overallAverage'] == d['subjects'][0]['average']")"
expect_status "  chi bai cua lop mot" True "$(J "all(e['groupName'] == 'Lop SL mot $STAMP' for e in d['subjects'][0]['exams'])")"
expect_status "chua phan ky (termId=0): chi bai lop hai, 1 bai tinh 0 diem" "1 0" \
  "$(call GET "/api/my/transcript?termId=0" "$TOK1" > /dev/null; J "str(d['subjects'][0]['totalExams']) + ' ' + str(d['subjects'][0]['average'] if d['subjects'][0]['average'] == 0 else -1).replace('.0','')")"
expect_status "ky khong ton tai: bang diem rong (200, khong loi)" 0 "$(call GET "/api/my/transcript?termId=999999" "$TOK1" > /dev/null; J "len(d['subjects'])")"
expect_status "  trung binh chung cua bang diem rong khong co so" True "$(J "d.get('overallAverage') is None")"
expect_status "em B loc ky cua lop mot: van thay bai cua minh (3 bai)" 3 "$(call GET "/api/my/transcript?termId=$TERM_SL" "$TOK2" > /dev/null; J "d['subjects'][0]['totalExams']")"
expect_status "em B loc chua phan ky: khong co lop nao → rong" 0 "$(call GET "/api/my/transcript?termId=0" "$TOK2" > /dev/null; J "len(d['subjects'])")"
expect_status "em A bo ky khoi lop mot → 200" 200 "$(call PUT "/api/student-groups/$G1" "$ADMIN" "{\"name\":\"Lop SL mot $STAMP\",\"termId\":null}")"
expect_status "  /api/my/terms chi con muc chua phan ky" "0" "$(call GET /api/my/terms "$TOK1" > /dev/null; J "' '.join(str(t['id']) for t in d)")"

summary
