#!/usr/bin/env bash
# Luong xuong song cua ca he thong: giao bai CHO MOT LOP thi MOI EM trong lop lam duoc bai.
#
# Chay:  bash tests/api-checks/flow-class-to-student.sh   (API phai dang chay o $HOST)
#
# Tach rieng khoi step5 va step6 vi day khong phai mot buoc, ma la duong noi giua tat ca cac buoc:
# admin lap lop -> xep hoc sinh -> phan cong giao vien -> cong bo -> giao vien ra de -> giao theo
# LOP (khong liet ke tung em) -> ca lop thay bai -> lam duoc -> giao vien thay ket qua.
#
# Dut o bat ky mat xich nao thi san pham vo dung, du tung phan deu dat. Bo kiem nay canh gac dung
# duong noi do.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")
TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); EXAM_IDS=(); STUDENT_IDS=()

cleanup() {
  section "Don du lieu"
  for id in "${EXAM_IDS[@]}";  do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  for id in "${STUDENT_IDS[@]}"; do
    call DELETE "/api/student-groups/${GROUP_IDS[0]}/students/$id" "$ADMIN" > /dev/null
    call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do call DELETE "/api/student-groups/$id" "$ADMIN" > /dev/null; done
  printf '  da don (de da co bai nop thi giu lai — dung thiet ke)\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

section "1. Quan tri vien lap lop va xep hoc sinh"
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop E2E $STAMP\",\"description\":\"Kiem chung luong giao bai theo lop\"}")
[[ "$code" != "200" ]] && fail_fast "tao lop: HTTP $code — $(body)"
GID=$(json_field id); GROUP_IDS+=("$GID")
expect_status "tao lop → 200" 200 "$code"

for i in 0 1 2; do
  c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"Hoc sinh E2E $i\",\"email\":\"e2e$STAMP$i@kiemthu.vn\",\"username\":\"e2e$STAMP$i\",\"code\":\"E2E$STAMP$i\",\"password\":\"anthill123\",\"groupId\":$GID}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $i: HTTP $c — $(body)"
  STUDENT_IDS+=("$(json_field id)")
done
expect_status "xep du 3 hoc sinh vao lop" 3 "${#STUDENT_IDS[@]}"

section "2. Phan cong giao vien va cong bo lop"
expect_status "phan cong giao vien → 200" 200 \
  "$(call GET /api/auth/me "$TEACHER" > /dev/null; call POST "/api/student-groups/$GID/teachers" "$ADMIN" "{\"teacherIds\":[$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)]}")"
expect_status "cong bo lop → 200" 200 "$(call POST "/api/student-groups/$GID/publish" "$ADMIN")"
expect_json   "  si so = 3" studentCount 3

section "3. Giao vien ra de"
code=$(call POST /api/exams "$TEACHER" "$(cat <<JSON
{
  "title": "De E2E $STAMP", "topic": "", "durationMinutes": 45, "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22, "pointPerQuestion": 1,
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": true,
  "openAt": "2020-01-01T00:00:00Z", "closeAt": "2035-01-01T00:00:00Z"
}
JSON
)")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EID=$(json_field id); EXAM_IDS+=("$EID")
expect_status "tao de → 200" 200 "$code"

section "4. Giao bai CHI bang groupIds, khong liet ke em nao"
expect_status "giao bai theo lop → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GID],\"notify\":true}")"
expect_json   "  giao dung 3 em, tuc ca lop" assignedCount 3

section "5. TUNG em trong lop deu thay bai"
# Tai khoan vua tao mang MustChangePassword=true (Sec.5): doi ngay sang mat khau rieng, dung nhu
# mot hoc sinh that lam sau lan dang nhap dau tien — /api/my/exams khong nam trong danh sach cho
# phep cua tai khoan con giu mat khau tam. Nho lai mat khau moi cua em 0 cho phan 6 dung lai no.
STUDENT0_PW=""
for i in 0 1 2; do
  NEW_PW="E2e${STAMP}${i}"
  TOK=$(login_and_clear_must_change "e2e$STAMP$i" "anthill123" "$NEW_PW")
  [[ "$i" == "0" ]] && STUDENT0_PW="$NEW_PW"
  expect_status "hoc sinh $i doc duoc danh sach bai → 200" 200 "$(call GET /api/my/exams "$TOK")"
  expect_status "  thay de vua duoc giao" true \
    "$(body | python -c "import json,sys;print(str(any(x['examId'] == $EID for x in json.load(sys.stdin))).lower())")"
  expect_status "  canStart = true, vao lam duoc ngay" true \
    "$(body | python -c "import json,sys;print(str(next(x['canStart'] for x in json.load(sys.stdin) if x['examId'] == $EID)).lower())")"
done

section "6. Hoc sinh dau tien lam va nop bai"
STUDENT=$(login "e2e${STAMP}0" "$STUDENT0_PW")
call GET /api/my/exams "$STUDENT" > /dev/null
AID=$(body | python -c "import json,sys;print(next(x['assignmentId'] for x in json.load(sys.stdin) if x['examId'] == $EID))")
expect_status "bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$STUDENT" "{\"assignmentId\":$AID}")"
SESSION=$(mktemp -t anthill-e2e.XXXXXX); cp "$BODY_FILE" "$SESSION"
SESSION_NATIVE=$(to_native_path "$SESSION")
expect_status "  nhan du 9 cau" 9 "$(python -c "import json;print(len(json.load(open(r'''$SESSION_NATIVE''',encoding='utf-8'))['questions']))")"
# §3 quy tac 1: dap an dung khong duoc roi server trong luc dang lam bai.
expect_status "  khong cau nao lo isCorrect" true \
  "$(python -c "
import json
d = json.load(open(r'''$SESSION_NATIVE''', encoding='utf-8'))
print(str(not any('isCorrect' in a for q in d['questions'] for a in q['answers'])).lower())")"

ATTEMPT=$(python -c "import json;print(json.load(open(r'''$SESSION_NATIVE''',encoding='utf-8'))['attemptId'])")
SEQ=$(python -c "import json;print(json.load(open(r'''$SESSION_NATIVE''',encoding='utf-8'))['lastAppliedSeq'] + 1)")
ANSWERS=$(python -c "
import json
d = json.load(open(r'''$SESSION_NATIVE''', encoding='utf-8'))
print(json.dumps([{'questionId': q['id'], 'answerId': q['answers'][0]['id']} for q in d['questions']]))")
rm -f "$SESSION"

expect_status "nop bai → 200" 200 \
  "$(call POST "/api/exam-attempts/$ATTEMPT/submit" "$STUDENT" "{\"clientSeq\":$SEQ,\"answers\":$ANSWERS}")"
expect_status "  de bat hien diem nen co score" present \
  "$(body | python -c "import json,sys;print('present' if 'score' in json.load(sys.stdin) else 'absent')")"

section "7. Giao vien thay ket qua cua lop minh"
expect_status "bang ket qua loc theo de → 200" 200 "$(call GET "/api/results?examId=$EID" "$TEACHER")"
expect_status "  co it nhat mot luot da nop" true \
  "$(body | python -c "import json,sys;print(str(json.load(sys.stdin)['total'] >= 1).lower())")"

section "8. Hoc sinh NGOAI lop khong thay gi"
OUTSIDER=$(login "lan.tt" "anthill123")
expect_status "danh sach cua em ngoai lop → 200" 200 "$(call GET /api/my/exams "$OUTSIDER")"
expect_status "  khong chua de nay" true \
  "$(body | python -c "import json,sys;print(str(all(x['examId'] != $EID for x in json.load(sys.stdin))).lower())")"

summary
