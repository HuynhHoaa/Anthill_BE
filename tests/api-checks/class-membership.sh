#!/usr/bin/env bash
# Thanh vien lop: mot em hoc duoc NHIEU lop cung luc; khoa tai khoan, go khoi lop, chuyen lop deu BAT BUOC co ly do; lop co the
# bi KHOA (chi doc) ma cac em trong lop van hoc cac lop khac; moi thay doi duoc ghi vao lich su.
#
# Chay:  bash tests/api-checks/class-membership.sh   (API phai dang chay o $HOST, database thu)
#
# Quy tac duoc canh gac o day:
#   - Khoa tai khoan, go khoi lop, chuyen lop, khoa lop: ly do 10-512 ky tu, khong co thi 400. Mo khoa khong can ly do.
#   - Xep them em vao lop khong dong toi cac lop khac cua em. Chuyen lop = go khoi lop nay + xep vao lop kia (ly do bat buoc);
#     em da bat dau lam bai van chuyen duoc. Chuyen sang lop em da hoc roi → 409.
#   - Go / chuyen thu hoi cac bai lop cu giao ma em chua bat dau; bai da bat dau duoc giu. Bai giao qua lop khac khong bi dong toi.
#   - Lop bi khoa la lop chi doc: khong xep them, khong go, khong chuyen vao/ra, khong giao bai, khong sua, khong go cong bo
#     (409). Thanh vien, bai da giao va bang diem giu nguyen; cac em van duoc xep vao lop khac. Mo khoa khong can ly do.
#   - PUT hoc sinh khong con doi lop ngam: groupId gui len phai la mot trong cac lop em dang hoc (hoac 0 khi em chua co lop).

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); STUDENT_IDS=(); EXAM_IDS=()
REASON="Don dep du lieu kiem thu tu dong"

cleanup() {
  section "Don du lieu"
  for id in "${EXAM_IDS[@]}"; do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  # Lop khoa la lop chi doc: mo khoa truoc (409 neu lop khong khoa — khong sao), roi moi go duoc thanh vien.
  for gid in "${GROUP_IDS[@]}"; do call POST "/api/student-groups/$gid/unlock" "$ADMIN" > /dev/null; done
  # Moi em deu go ra duoc (ke ca em da lam bai) nen lop don duoc, khong de sot lop thu trong database.
  for sid in "${STUDENT_IDS[@]}"; do
    for gid in "${GROUP_IDS[@]}"; do
      call DELETE "/api/student-groups/$gid/students/$sid" "$ADMIN" "{\"reason\":\"$REASON\"}" > /dev/null
    done
    call PATCH "/api/students/$sid/status" "$ADMIN" "{\"status\":\"locked\",\"reason\":\"$REASON\"}" > /dev/null
  done
  for gid in "${GROUP_IDS[@]}"; do call DELETE "/api/student-groups/$gid" "$ADMIN" > /dev/null; done
  printf '  da don (de da co bai lam thi giu lai — dung thiet ke)\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

new_student() {
  local index="$1" gid="$2" c
  c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"Hoc sinh lop $index\",\"email\":\"cm$STAMP$index@kiemthu.vn\",\"username\":\"cm$STAMP$index\",\"code\":\"CM$STAMP$index\",\"password\":\"123456\",\"groupId\":$gid}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $index: HTTP $c — $(body)"
  json_field id
}

# Phan ho so (khong gom lop, trang thai) de PUT lai dung nhu form sua ho so.
profile() {
  echo "\"fullName\":\"Hoc sinh lop $1\",\"email\":\"cm$STAMP$1@kiemthu.vn\",\"username\":\"cm$STAMP$1\",\"code\":\"CM$STAMP$1\""
}

# Ghi tren stdout so phan tu cua mang JSON trong $BODY_FILE thoa dieu kien python.
count_where() {
  python -c "import json,sys;d=json.load(sys.stdin);print(sum(1 for x in d if $1))" < "$BODY_FILE"
}
count_all() {
  python -c "import json,sys;print(len(json.load(sys.stdin)))" < "$BODY_FILE"
}

# Id cac lop cua hoc sinh trong $BODY_FILE (mot StudentDto), sap tang dan, noi bang dau phay; "none" khi khong co lop nao.
groups_of() {
  python -c "import json,sys;d=json.load(sys.stdin);ids=sorted(g['id'] for g in d['groups']);print(','.join(map(str,ids)) if ids else 'none')" < "$BODY_FILE"
}
student_groups() { call GET "/api/students/$1" "$ADMIN" > /dev/null; groups_of; }

# Co hay khong mot chuoi (khong dau hoac co dau) trong than loi tra ve.
body_mentions() {
  python -c "import json,sys;print(str(sys.argv[1] in json.dumps(json.loads(sys.stdin.buffer.read().decode('utf-8')), ensure_ascii=False)).lower())" "$1" < "$BODY_FILE"
}

section "Dung du lieu: ba lop, bon hoc sinh o lop A, mot de dang mo giao cho lop A"
GA=$(new_class "$ADMIN" "$TEACHER" "Lop A CM $STAMP") || fail_fast "tao lop A"
GB=$(new_class "$ADMIN" "$TEACHER" "Lop B CM $STAMP") || fail_fast "tao lop B"
GC=$(new_class "$ADMIN" "$TEACHER" "Lop C CM $STAMP") || fail_fast "tao lop C"
GROUP_IDS+=("$GA" "$GB" "$GC")

S1=$(new_student 1 "$GA"); S2=$(new_student 2 "$GA"); S3=$(new_student 3 "$GA"); S4=$(new_student 4 "$GA")
STUDENT_IDS+=("$S1" "$S2" "$S3" "$S4")

code=$(call POST /api/exams "$TEACHER" "$(cat <<JSON
{
  "title": "De thanh vien lop $STAMP", "topic": "", "durationMinutes": 45, "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22, "pointEasy": 10, "pointMedium": 10, "pointHard": 15,
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": true,
  "openAt": "2020-01-01T00:00:00Z", "closeAt": "2035-01-01T00:00:00Z"
}
JSON
)")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EID=$(json_field id); EXAM_IDS+=("$EID")
code=$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GA],\"notify\":false}")
[[ "$code" != "200" ]] && fail_fast "giao bai: HTTP $code — $(body)"
expect_json "giao bai cho ca lop A: 4 em" assignedCount 4

# ── 1. Khoa tai khoan ────────────────────────────────────────────────────────────────────────────
section "1. Khoa tai khoan bat buoc co ly do"
expect_status "khoa khong ly do → 400" 400 "$(call PATCH "/api/students/$S4/status" "$ADMIN" '{"status":"locked"}')"
expect_status "khoa voi ly do rong → 400" 400 "$(call PATCH "/api/students/$S4/status" "$ADMIN" '{"status":"locked","reason":"           "}')"
expect_status "khoa voi ly do qua ngan → 400" 400 "$(call PATCH "/api/students/$S4/status" "$ADMIN" '{"status":"locked","reason":"nghi hoc"}')"
expect_status "khoa voi ly do dai hon 512 ky tu → 400" 400 \
  "$(call PATCH "/api/students/$S4/status" "$ADMIN" "{\"status\":\"locked\",\"reason\":\"$(python -c "print('a'*513)")\"}")"
expect_status "em van hoat dong sau cac lan bi tu choi" active "$(call GET "/api/students/$S4" "$ADMIN" > /dev/null; json_field status)"
expect_status "khoa voi ly do hop le → 200" 200 \
  "$(call PATCH "/api/students/$S4/status" "$ADMIN" '{"status":"locked","reason":"Nghi hoc khong bao truoc tu dau thang"}')"
expect_json   "  trang thai = locked" status locked
expect_status "mo khoa khong can ly do → 200" 200 "$(call PATCH "/api/students/$S4/status" "$ADMIN" '{"status":"active"}')"
expect_json   "  trang thai = active" status active
expect_status "giao vien khoa → 403" 403 "$(call PATCH "/api/students/$S4/status" "$TEACHER" '{"status":"locked","reason":"Giao vien khong co quyen"}')"

section "2. Lich su cua hoc sinh ghi lai ly do va nguoi thuc hien"
expect_status "admin doc lich su → 200" 200 "$(call GET "/api/students/$S4/history" "$ADMIN")"
expect_json   "  dong moi nhat la mo khoa" 0.kind unlocked
expect_json   "  dong ke do la khoa" 1.kind locked
expect_json   "  ly do khoa duoc luu nguyen van" 1.reason "Nghi hoc khong bao truoc tu dau thang"
expect_status "  co ten nguoi thuc hien" true "$(body | python -c "import json,sys;print(str(bool(json.load(sys.stdin)[1].get('actorName'))).lower())")"
expect_status "giao vien doc lich su → 403" 403 "$(call GET "/api/students/$S4/history" "$TEACHER")"
expect_status "lich su cua id khong ton tai → 404" 404 "$(call GET "/api/students/999999/history" "$ADMIN")"

section "3. Form sua ho so khong doi duoc lop va trang thai"
PROFILE=$(profile 4)
expect_status "PUT doi sang lop em khong hoc → 400" 400 "$(call PUT "/api/students/$S4" "$ADMIN" "{$PROFILE,\"groupId\":$GB}")"
expect_status "PUT bo khoi lop (groupId 0) khi em dang hoc lop → 400" 400 "$(call PUT "/api/students/$S4" "$ADMIN" "{$PROFILE,\"groupId\":0}")"
expect_status "PUT doi trang thai → 400" 400 "$(call PUT "/api/students/$S4" "$ADMIN" "{$PROFILE,\"status\":\"locked\"}")"
expect_status "PUT gui dung lop va trang thai dang co → 200" 200 \
  "$(call PUT "/api/students/$S4" "$ADMIN" "{$PROFILE,\"groupId\":$GA,\"status\":\"active\"}")"
expect_status "PUT khong gui lop → 200, em van o lop cu" 200 "$(call PUT "/api/students/$S4" "$ADMIN" "{$PROFILE,\"phone\":\"0912345678\"}")"
expect_status "  van chi o lop A" "$GA" "$(groups_of)"

# ── 2. Go khoi lop ───────────────────────────────────────────────────────────────────────────────
section "4. Go khoi lop bat buoc co ly do"
expect_status "go khong gui than request → 400" 400 "$(call DELETE "/api/student-groups/$GA/students/$S3" "$ADMIN")"
expect_status "go voi ly do qua ngan → 400" 400 "$(call DELETE "/api/student-groups/$GA/students/$S3" "$ADMIN" '{"reason":"ngan"}')"
expect_status "giao vien go → 403" 403 "$(call DELETE "/api/student-groups/$GA/students/$S3" "$TEACHER" '{"reason":"Giao vien khong co quyen"}')"
expect_status "em khong thuoc lop nay → 404" 404 "$(call DELETE "/api/student-groups/$GB/students/$S3" "$ADMIN" '{"reason":"Em nay khong o lop B"}')"
expect_status "em van o lop A sau cac lan bi tu choi" "$GA" "$(student_groups "$S3")"

expect_status "go khi chua lam bai, co ly do → 200" 200 \
  "$(call DELETE "/api/student-groups/$GA/students/$S3" "$ADMIN" '{"reason":"Em chuyen truong khong hoc nua"}')"
expect_json   "  thu hoi 1 bai lop cu chua bat dau" revokedExams 1
expect_json   "  khong giu bai nao" keptExams 0
expect_status "  em khong con lop" none "$(student_groups "$S3")"
expect_status "  bai da giao cho em bi thu hoi het" 0 "$(call GET "/api/students/$S3/exams" "$ADMIN" > /dev/null; count_all)"
call GET "/api/students/$S3/history" "$ADMIN" > /dev/null
expect_json   "  lich su: go khoi lop" 0.kind removedFromGroup
expect_json   "  lich su: ly do" 0.reason "Em chuyen truong khong hoc nua"
expect_status "  lich su: ten lop luc go" true "$(body | python -c "import json,sys;print(str('Lop A CM' in json.load(sys.stdin)[0]['fromGroup']).lower())")"
expect_status "em da go khong con trong danh sach lop" 0 \
  "$(call GET "/api/student-groups/$GA/students" "$ADMIN" > /dev/null; count_where "x['id']==$S3")"

# ── 3. Chuyen lop ───────────────────────────────────────────────────────────────────────────────
section "5. Chuyen lop: ly do bat buoc, lop dich hop le"
expect_status "chuyen khong ly do → 400" 400 "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GB}")"
expect_status "chuyen voi ly do ngan → 400" 400 "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"xep nham\"}")"
expect_status "chuyen khong chon lop dich → 400" 400 "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" '{"targetGroupId":0,"reason":"Xep nham lop tu dau"}')"
expect_status "chuyen sang chinh lop hien tai → 400" 400 \
  "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GA,\"reason\":\"Xep nham lop tu dau\"}")"
expect_status "chuyen sang lop khong ton tai → 404" 404 \
  "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" '{"targetGroupId":999999,"reason":"Xep nham lop tu dau"}')"
expect_status "chuyen em khong thuoc lop nay → 404" 404 \
  "$(call POST "/api/student-groups/$GB/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GA,\"reason\":\"Em nay khong o lop B\"}")"
expect_status "giao vien chuyen → 403" 403 \
  "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$TEACHER" "{\"targetGroupId\":$GB,\"reason\":\"Giao vien khong co quyen\"}")"
expect_status "em van o lop A sau cac lan bi tu choi" "$GA" "$(student_groups "$S2")"

# ── 4. Mot em bat dau lam bai ───────────────────────────────────────────────────────────────────
section "6. Em S1 bat dau lam bai (chua nop)"
PW1="Cm${STAMP}1"
TOK1=$(login_and_clear_must_change "cm${STAMP}1" "123456" "$PW1")
call GET /api/my/exams "$TOK1" > /dev/null
AID1=$(body | python -c "import json,sys;print(next(x['assignmentId'] for x in json.load(sys.stdin) if x['examId'] == $EID))")
expect_status "bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$TOK1" "{\"assignmentId\":$AID1}")"
call GET "/api/student-groups/$GA/students" "$ADMIN" > /dev/null
expect_status "  S1 co hasStartedExams = true" true "$(body | python -c "import json,sys;print(str(next(x['hasStartedExams'] for x in json.load(sys.stdin) if x['id']==$S1)).lower())")"
expect_status "  S2 chua lam bai: hasStartedExams = false" false "$(body | python -c "import json,sys;print(str(next(x['hasStartedExams'] for x in json.load(sys.stdin) if x['id']==$S2)).lower())")"

section "7. Em da bat dau lam bai van chuyen lop duoc; bai da bat dau duoc giu"
expect_status "chuyen S1 sang lop B → 200" 200 \
  "$(call POST "/api/student-groups/$GA/students/$S1/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"Muon doi sang lop B\"}")"
expect_json   "  khong thu hoi bai nao" revokedExams 0
expect_json   "  giu bai da bat dau" keptExams 1
expect_status "  S1 o lop B" "$GB" "$(student_groups "$S1")"
expect_status "  bai cua S1 van con (khong bi thu hoi)" 1 \
  "$(call GET "/api/students/$S1/exams" "$ADMIN" > /dev/null; count_all)"
call GET "/api/students/$S1/history" "$ADMIN" > /dev/null
expect_json   "  lich su: chuyen lop" 0.kind movedGroup

section "8. Em chua lam bai chuyen duoc, bai lop cu bi thu hoi"
expect_status "chuyen S2 sang lop B → 200" 200 \
  "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"Xep nham lop tu dau nam hoc\"}")"
expect_json   "  thu hoi 1 bai chua bat dau" revokedExams 1
expect_json   "  khong giu bai nao" keptExams 0
expect_status "  S2 o lop B" "$GB" "$(student_groups "$S2")"
expect_status "  S2 hien trong danh sach lop B" 1 "$(call GET "/api/student-groups/$GB/students" "$ADMIN" > /dev/null; count_where "x['id']==$S2")"
expect_status "  S2 khong con trong danh sach lop A" 0 "$(call GET "/api/student-groups/$GA/students" "$ADMIN" > /dev/null; count_where "x['id']==$S2")"
call GET "/api/students/$S2/history" "$ADMIN" > /dev/null
expect_json   "  lich su: chuyen lop" 0.kind movedGroup
expect_json   "  lich su: ly do" 0.reason "Xep nham lop tu dau nam hoc"
expect_status "  lich su: ghi ca lop cu va lop moi" true \
  "$(body | python -c "import json,sys;d=json.load(sys.stdin)[0];print(str('Lop A CM' in d['fromGroup'] and 'Lop B CM' in d['toGroup']).lower())")"
expect_status "chuyen S2 tu lop A khi em da roi lop A → 404" 404 \
  "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"Chuyen lai lan nua cho chac\"}")"

# ── 5. Mot em hoc nhieu lop cung luc ────────────────────────────────────────────────────────────
section "9. Mot em hoc nhieu lop cung luc: xep them khong dong toi lop khac"
expect_status "xep S2 (dang o lop B) them vao lop A → 200" 200 \
  "$(call POST "/api/student-groups/$GA/students" "$ADMIN" "{\"studentIds\":[$S2]}")"
expect_json   "  them duoc 1 em" added 1
expect_json   "  khong em nao bi bo qua" skippedAlreadyMember 0
expect_json   "  si so lop A = S4 + S2" studentCount 2
expect_status "  S2 hoc ca hai lop A va B" "$GA,$GB" "$(student_groups "$S2")"
expect_status "  S2 hien trong danh sach lop A" 1 "$(call GET "/api/student-groups/$GA/students" "$ADMIN" > /dev/null; count_where "x['id']==$S2")"
expect_status "  S2 van hien trong danh sach lop B" 1 "$(call GET "/api/student-groups/$GB/students" "$ADMIN" > /dev/null; count_where "x['id']==$S2")"
expect_status "xep S2 vao lop A lan nua → 200 nhung bo qua" 200 \
  "$(call POST "/api/student-groups/$GA/students" "$ADMIN" "{\"studentIds\":[$S2]}")"
expect_json   "  khong them ai" added 0
expect_json   "  bo qua vi da o trong lop" skippedAlreadyMember 1
call GET "/api/student-groups/$GA/assignable-students" "$ADMIN" > /dev/null
expect_status "danh sach co the them vao lop A: khong co em da o lop A (S2, S4)" 0 "$(count_where "x['id'] in ($S2,$S4)")"
expect_status "  co S1 (dang o lop B) — em o lop khac van them duoc" 1 "$(count_where "x['id']==$S1")"
expect_status "  co S3 (chua co lop)" 1 "$(count_where "x['id']==$S3")"
call GET "/api/student-groups/$GC/assignable-students" "$ADMIN" > /dev/null
expect_status "danh sach co the them vao lop C: co S2 du dang hoc hai lop" 1 "$(count_where "x['id']==$S2")"
expect_status "  PUT S2 gui lop B (mot trong cac lop em hoc) → 200" 200 \
  "$(call PUT "/api/students/$S2" "$ADMIN" "{$(profile 2),\"groupId\":$GB}")"
expect_status "  PUT S2 gui lop C (em khong hoc) → 400" 400 "$(call PUT "/api/students/$S2" "$ADMIN" "{$(profile 2),\"groupId\":$GC}")"
expect_status "  PUT S2 gui groupId 0 → 400 (em dang hoc lop)" 400 "$(call PUT "/api/students/$S2" "$ADMIN" "{$(profile 2),\"groupId\":0}")"
expect_status "  S2 van hoc ca hai lop" "$GA,$GB" "$(student_groups "$S2")"
call GET "/api/students/$S2/history" "$ADMIN" > /dev/null
expect_json   "  lich su: xep them vao lop" 0.kind addedToGroup

section "9b. Chuyen sang lop em da hoc → 409; goi mot lop khong dong toi lop kia"
expect_status "chuyen S2 tu lop A sang lop B (da hoc lop B roi) → 409" 409 \
  "$(call POST "/api/student-groups/$GA/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"Da hoc lop B nen khong chuyen duoc\"}")"
expect_status "  S2 van hoc ca hai lop" "$GA,$GB" "$(student_groups "$S2")"
expect_status "go S2 khoi lop A (ly do) → 200" 200 \
  "$(call DELETE "/api/student-groups/$GA/students/$S2" "$ADMIN" '{"reason":"Em chi con hoc lop B tu thang nay"}')"
expect_json   "  khong co bai nao qua lop A de thu hoi" revokedExams 0
expect_status "  S2 con lai lop B" "$GB" "$(student_groups "$S2")"

# ── 6. Em da lam bai: nghi hoc roi quay lai ─────────────────────────────────────────────────────
section "10. Em da lam bai van go duoc (nghi hoc) va xep lai lop nao cung duoc"
expect_status "go S1 (da bat dau lam bai) khoi lop B voi ly do → 200" 200 \
  "$(call DELETE "/api/student-groups/$GB/students/$S1" "$ADMIN" '{"reason":"Nghi hoc khong bao truoc, gia dinh da xin thoi hoc"}')"
expect_json   "  bai giao qua lop A khong bi dong toi" revokedExams 0
expect_json   "  lop B chua giao bai nao cho em" keptExams 0
expect_status "  bai da bat dau van o ho so em" 1 \
  "$(call GET "/api/students/$S1/exams" "$ADMIN" > /dev/null; count_all)"
expect_status "  S1 khong con lop" none "$(student_groups "$S1")"
expect_status "xep S1 vao lop C (lop chua tung hoc) → 200" 200 \
  "$(call POST "/api/student-groups/$GC/students" "$ADMIN" "{\"studentIds\":[$S1]}")"
expect_json   "  them duoc 1 em" added 1
expect_status "xep S1 lai vao lop A (lop cu) → 200" 200 \
  "$(call POST "/api/student-groups/$GA/students" "$ADMIN" "{\"studentIds\":[$S1]}")"
expect_json   "  them duoc 1 em" added 1
expect_status "  S1 hoc ca lop A va lop C" "$GA,$GC" "$(student_groups "$S1")"
call GET "/api/students/$S1/history" "$ADMIN" > /dev/null
expect_json   "  lich su: xep lai vao lop" 0.kind addedToGroup
expect_json   "  lich su truoc do: xep vao lop C" 1.kind addedToGroup
expect_json   "  lich su truoc nua: go khoi lop" 2.kind removedFromGroup

# ── 7. Admin xem bai cua hoc sinh ───────────────────────────────────────────────────────────────
section "11. Quan tri vien xem bai cua hoc sinh (chi doc)"
expect_status "danh sach bai cua S1 → 200" 200 "$(call GET "/api/students/$S1/exams" "$ADMIN")"
expect_json   "  bai dang lam do" 0.status inprogress
expect_status "  co ma luot lam bai" true "$(body | python -c "import json,sys;print(str(json.load(sys.stdin)[0].get('attemptId') is not None).lower())")"
ATT1=$(json_field 0.attemptId)
expect_status "  ten lop giao bai" true "$(body | python -c "import json,sys;print(str('Lop A CM' in json.load(sys.stdin)[0]['group']).lower())")"
expect_status "giao vien xem danh sach bai cua hoc sinh → 403" 403 "$(call GET "/api/students/$S1/exams" "$TEACHER")"
expect_status "admin mo chi tiet bai lam → 200" 200 "$(call GET "/api/results/$ATT1" "$ADMIN")"
expect_status "admin mo tom tat bai lam → 200" 200 "$(call GET "/api/results/$ATT1/summary" "$ADMIN")"
expect_status "bang ket qua chung van la cua giao vien: admin → 403" 403 "$(call GET "/api/results" "$ADMIN")"
expect_status "hoc sinh khac khong mo duoc bai cua S1 → 404" 404 "$(call GET "/api/results/$ATT1" "$(login "lan.tt" "123456")")"

# ── 8. Khoa lop ─────────────────────────────────────────────────────────────────────────────────
# Luc nay: lop A co S1, S4; lop B co S2; lop C co S1; S3 chua co lop. De giao cho lop A (S4 va S1 da co bai).
section "12. Khoa lop: bat buoc co ly do, chi quan tri vien"
expect_status "khoa khong gui than request → 400" 400 "$(call POST "/api/student-groups/$GB/lock" "$ADMIN")"
expect_status "khoa voi ly do rong → 400" 400 "$(call POST "/api/student-groups/$GB/lock" "$ADMIN" '{"reason":"          "}')"
expect_status "khoa voi ly do qua ngan → 400" 400 "$(call POST "/api/student-groups/$GB/lock" "$ADMIN" '{"reason":"het nam"}')"
expect_status "khoa voi ly do dai hon 512 ky tu → 400" 400 \
  "$(call POST "/api/student-groups/$GB/lock" "$ADMIN" "{\"reason\":\"$(python -c "print('a'*513)")\"}")"
expect_status "giao vien khoa lop → 403" 403 "$(call POST "/api/student-groups/$GB/lock" "$TEACHER" '{"reason":"Giao vien khong co quyen"}')"
expect_status "khoa lop khong ton tai → 404" 404 "$(call POST "/api/student-groups/999999/lock" "$ADMIN" '{"reason":"Lop nay khong co that"}')"
call GET "/api/student-groups/$GB" "$ADMIN" > /dev/null
expect_json   "lop B van mo sau cac lan bi tu choi" isLocked false
expect_status "khoa lop B voi ly do hop le → 200" 200 \
  "$(call POST "/api/student-groups/$GB/lock" "$ADMIN" '{"reason":"Lop B ket thuc hoc ky, giu nguyen bang diem"}')"
expect_json   "  isLocked = true" isLocked true
expect_status "  co moc lockedAt" present "$(python -c "import json,sys;print('present' if json.load(sys.stdin).get('lockedAt') else 'missing')" < "$BODY_FILE")"
expect_json   "  thanh vien giu nguyen: van 1 em" studentCount 1
expect_status "khoa lop da khoa → 409" 409 "$(call POST "/api/student-groups/$GB/lock" "$ADMIN" '{"reason":"Khoa lai lan nua cho chac"}')"
call GET "/api/student-groups/$GB/history" "$ADMIN" > /dev/null
expect_json   "  lich su lop: dong moi nhat la khoa" 0.kind locked
expect_json   "  lich su lop: ly do duoc luu" 0.reason "Lop B ket thuc hoc ky, giu nguyen bang diem"

section "13. Lop khoa la lop chi doc"
expect_status "xep S3 vao lop khoa → 409" 409 "$(call POST "/api/student-groups/$GB/students" "$ADMIN" "{\"studentIds\":[$S3]}")"
expect_status "  cau tu choi noi ro lop da khoa" true "$(body_mentions 'khoá')"
expect_status "  S3 van chua co lop" none "$(student_groups "$S3")"
expect_status "go S2 khoi lop khoa → 409" 409 "$(call DELETE "/api/student-groups/$GB/students/$S2" "$ADMIN" '{"reason":"Go khi lop da khoa se bi chan"}')"
expect_status "chuyen S2 ra khoi lop khoa → 409" 409 \
  "$(call POST "/api/student-groups/$GB/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GC,\"reason\":\"Chuyen khi lop da khoa se bi chan\"}")"
expect_status "chuyen S4 vao lop khoa → 409" 409 \
  "$(call POST "/api/student-groups/$GA/students/$S4/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"Chuyen vao lop khoa se bi chan\"}")"
expect_status "  S4 van o lop A" "$GA" "$(student_groups "$S4")"
expect_status "  S2 van o lop khoa B" "$GB" "$(student_groups "$S2")"
expect_status "sua ten lop khoa → 409" 409 "$(call PUT "/api/student-groups/$GB" "$ADMIN" '{"name":"Ten moi cua lop khoa","description":""}')"
expect_status "go cong bo lop khoa → 409" 409 "$(call POST "/api/student-groups/$GB/unpublish" "$ADMIN")"
expect_status "giao bai cho lop khoa → 409" 409 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GB],\"notify\":false}")"
expect_status "  cau tu choi noi ro lop da khoa" true "$(body_mentions 'đã khoá')"
expect_status "  lop khoa van doc duoc danh sach thanh vien" 1 "$(call GET "/api/student-groups/$GB/students" "$ADMIN" > /dev/null; count_all)"

section "14. Em trong lop khoa van hoc cac lop khac"
expect_status "xep S2 (o lop khoa B) vao lop C → 200" 200 \
  "$(call POST "/api/student-groups/$GC/students" "$ADMIN" "{\"studentIds\":[$S2]}")"
expect_json   "  them duoc 1 em" added 1
expect_status "  S2 hoc ca lop B (khoa) va lop C" "$GB,$GC" "$(student_groups "$S2")"
expect_status "giao bai cho lop C van duoc → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GC],\"notify\":false}")"
expect_json   "  S2 nhan bai qua lop C" assignedCount 1
expect_json   "  S1 da co bai tu lop A nen khong giao lai" skippedAlreadyAssigned 1
call GET "/api/students/$S2/exams" "$ADMIN" > /dev/null
expect_status "  bai cua S2 ghi theo lop C (lop dau tien giao cho em)" true \
  "$(body | python -c "import json,sys;d=json.load(sys.stdin);print(str(len(d)==1 and 'Lop C CM' in d[0]['group']).lower())")"
expect_status "chuyen S2 tu lop C sang lop khoa B → 409" 409 \
  "$(call POST "/api/student-groups/$GC/students/$S2/transfer" "$ADMIN" "{\"targetGroupId\":$GB,\"reason\":\"Lop B dang khoa nen khong nhan\"}")"

section "15. Mo khoa lop"
expect_status "giao vien mo khoa → 403" 403 "$(call POST "/api/student-groups/$GB/unlock" "$TEACHER")"
expect_status "mo khoa lop khong ton tai → 404" 404 "$(call POST "/api/student-groups/999999/unlock" "$ADMIN")"
expect_status "mo khoa lop dang mo (lop A) → 409" 409 "$(call POST "/api/student-groups/$GA/unlock" "$ADMIN")"
expect_status "mo khoa lop B khong can ly do → 200" 200 "$(call POST "/api/student-groups/$GB/unlock" "$ADMIN")"
expect_json   "  isLocked = false" isLocked false
expect_status "  lockedAt het hieu luc" missing "$(python -c "import json,sys;print('present' if json.load(sys.stdin).get('lockedAt') else 'missing')" < "$BODY_FILE")"
call GET "/api/student-groups/$GB/history" "$ADMIN" > /dev/null
expect_json   "  lich su lop: dong moi nhat la mo khoa" 0.kind unlocked
expect_json   "  lich su lop: lan khoa truoc do van con" 1.kind locked
expect_status "xep S3 vao lop B sau khi mo khoa → 200" 200 "$(call POST "/api/student-groups/$GB/students" "$ADMIN" "{\"studentIds\":[$S3]}")"
expect_json   "  them duoc 1 em" added 1

section "16. Lop khoa rong van xoa duoc"
GD=$(new_class "$ADMIN" "$TEACHER" "Lop D CM $STAMP") || fail_fast "tao lop D"
GROUP_IDS+=("$GD")
expect_status "khoa lop D (rong) → 200" 200 "$(call POST "/api/student-groups/$GD/lock" "$ADMIN" '{"reason":"Lop mo nham, khong dung nua"}')"
expect_status "xoa lop rong dang khoa → 200" 200 "$(call DELETE "/api/student-groups/$GD" "$ADMIN")"
expect_status "  lop khong con nua" 404 "$(call GET "/api/student-groups/$GD" "$ADMIN")"
expect_status "xoa lop B (con thanh vien, da mo khoa) → 409" 409 "$(call DELETE "/api/student-groups/$GB" "$ADMIN")"

summary
