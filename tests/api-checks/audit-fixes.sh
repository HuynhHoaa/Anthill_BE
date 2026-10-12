#!/usr/bin/env bash
# Cac loi tu dot ra soat: dau vao di lot validator roi vo thanh 500, gio nop bai tu nop, thong bao
# gui sai, khoi phuc tai khoan lam mat trang thai khoa.
#
# Chay:  bash tests/api-checks/audit-fixes.sh   (API phai dang chay o $HOST)
#
# Co mot quang cho ~60 giay o phan "tu nop": tien trinh nen quet moi 30 giay, nen phai cho no chot
# luot het gio roi moi doc duoc gio nop da ghi.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=()
GROUP_ID=""

cleanup() {
  section "Don du lieu"
  local exams=0 kept=0 questions=0 groups=0
  for id in "${EXAMS[@]}"; do
    if [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]]; then exams=$((exams + 1)); else kept=$((kept + 1)); fi
  done
  for id in "${QUESTIONS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1))
  done
  for id in "${STUDENTS[@]}"; do
    [[ -n "$GROUP_ID" ]] && call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$ADMIN" > /dev/null
    call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1))
  done
  printf '  da xoa %d/%d de · %d/%d cau hoi · %d/%d nhom (%d de o lai vi da co bai nop)\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "$kept"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

# Tim trong response (danh sach thong bao) mot muc co type va link khop, in ra 'co' hoac 'khong'.
# Link duoc dung trong python tu hai manh ($2 = vai tro, $3 = id luot): truyen thang "/giao-vien/..."
# lam doi so thi MSYS doi no thanh duong dan Windows truoc khi python kip nhan.
has_notification() {
  python -c "
import json,sys
want_type, want_link = sys.argv[1].lower(), '/%s/ket-qua/%s' % (sys.argv[2], sys.argv[3])
def walk(node):
    if isinstance(node, dict):
        if str(node.get('type','')).lower() == want_type and node.get('link') == want_link:
            return True
        return any(walk(v) for v in node.values())
    if isinstance(node, list):
        return any(walk(v) for v in node)
    return False
print('co' if walk(json.load(open(sys.argv[4], encoding='utf-8'))) else 'khong')
" "$1" "$2" "$3" "$BODY_FILE"
}

# ── Dung du lieu nen ─────────────────────────────────────────────────────────────

section "Dung du lieu nen"

GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KiemThuRaSoat $STAMP" "Nhom kiem thu ra soat") || fail_fast "khong dung duoc lop"
GROUP_IDS+=("$GROUP_ID")

new_student() {
  local suffix="$1" grouped="${2:-yes}" payload
  payload="{\"fullName\":\"Hoc sinh $suffix\",\"email\":\"hs$suffix@kiemthu.vn\",\"username\":\"hs$suffix\",\"code\":\"RS$suffix\",\"password\":\"123456\""
  [[ "$grouped" == "yes" ]] && payload="$payload,\"groupId\":$GROUP_ID"
  payload="$payload}"
  local c; c=$(call POST /api/students "$ADMIN" "$payload")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $c — $(body)"
  NEW_STUDENT_ID=$(json_field id); STUDENTS+=("$NEW_STUDENT_ID")
}

NEW_STUDENT_ID=""
new_student "${STAMP}a"; STUDENT_A_ID=$NEW_STUDENT_ID
new_student "${STAMP}c" no; STUDENT_C_ID=$NEW_STUDENT_ID

STUDENT_A=$(login_and_clear_must_change "hs${STAMP}a" "123456" "Rs$STAMP")

for i in 1 2 3; do
  c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau de $i - $STAMP\",\"topic\":\"RaSoat$STAMP\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi $i: HTTP $c — $(body)"
  QUESTIONS+=("$(json_field id)")
done

# $1 openAt · $2 closeAt · $3 thoi luong (phut) · $4 hau to tieu de · $5 mo ta
exam_body() {
  cat <<JSON
{
  "title": "De ra soat $STAMP${4:-}",
  "description": "${5:-De kiem thu ra soat}",
  "topic": "RaSoat$STAMP",
  "durationMinutes": $3,
  "totalQuestions": 1,
  "easyPercentage": 100,
  "mediumPercentage": 0,
  "hardPercentage": 0,
  "pointPerQuestion": 1,
  "openAt": "$1",
  "closeAt": "$2",
  "shuffleQuestions": false,
  "shuffleAnswers": false,
  "showResultAfterSubmit": true
}
JSON
}

# Tao de roi giao cho nhom, dat id vao NEW_EXAM_ID. $6 = notify (true/false).
NEW_EXAM_ID=""
new_exam() {
  local c; c=$(call POST /api/exams "$TEACHER" "$(exam_body "$1" "$2" "$3" "${4:-}")")
  [[ "$c" != "200" ]] && fail_fast "tao de: HTTP $c — $(body)"
  NEW_EXAM_ID=$(json_field id); EXAMS+=("$NEW_EXAM_ID")
  c=$(call POST "/api/exams/$NEW_EXAM_ID/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":${6:-false}}")
  [[ "$c" != "200" ]] && fail_fast "giao de $NEW_EXAM_ID: HTTP $c — $(body)"
}

assignment_for() {
  call GET "/api/my/exams" "$1" > /dev/null
  python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((item['assignmentId'] for item in items if item['examId'] == int(sys.argv[2])), ''))
" "$BODY_FILE" "$2"
}

printf '  nhom %s · hoc sinh %s va %s · %d cau hoi\n' "$GROUP_ID" "$STUDENT_A_ID" "$STUDENT_C_ID" "${#QUESTIONS[@]}"

# ── Ky tu NUL ────────────────────────────────────────────────────────────────────

section "Ky tu NUL: 400, khong phai 500"

expect_status "ten dang nhap co NUL → 400" 400 \
  "$(call POST /api/auth/login "" '{"username":"an.nv\u0000@anthill.edu.vn","password":"123456"}')"
expect_status "ten nhom co NUL → 400" 400 \
  "$(call POST /api/student-groups "$ADMIN" '{"name":"Lop\u0000Nul","description":""}')"
expect_status "o tim kiem hoc sinh co NUL → 400" 400 "$(call GET "/api/students?search=%00" "$ADMIN")"
expect_status "o tim kiem hoc sinh binh thuong van → 200" 200 "$(call GET "/api/students?search=hs$STAMP" "$ADMIN")"

# ── Mat khau toan khoang trang ───────────────────────────────────────────────────

section "Mat khau toan khoang trang"

expect_status "tao hoc sinh voi mat khau toan khoang trang → 400" 400 \
  "$(call POST /api/students "$ADMIN" "{\"fullName\":\"Hoc sinh ws\",\"email\":\"ws$STAMP@kiemthu.vn\",\"username\":\"ws$STAMP\",\"code\":\"WS$STAMP\",\"password\":\"      \"}")"
expect_status "doi sang mat khau toan khoang trang → 400" 400 \
  "$(call POST /api/auth/change-password "$STUDENT_A" "{\"currentPassword\":\"Rs$STAMP\",\"newPassword\":\"        \"}")"
expect_status "  phien cua hoc sinh van song" 200 "$(call GET /api/auth/me "$STUDENT_A")"

# ── Tran do dai khop cot ─────────────────────────────────────────────────────────

section "Tran do dai khop cot trong database"

LONG_1001=$(python -c "print('a'*1001)")
LONG_1000=$(python -c "print('a'*1000)")
LONG_EMAIL=$(python -c "print('a'*300 + '@kiemthu.vn')")
LONG_URL=$(python -c "print('/uploads/' + 'a'*520)")
OPEN_NOW=$(date -u -d '-1 hour' +%Y-%m-%dT%H:%M:%SZ)
CLOSE_LATER=$(date -u -d '+2 hours' +%Y-%m-%dT%H:%M:%SZ)

expect_status "mo ta de 1001 ky tu → 400" 400 \
  "$(call POST /api/exams "$TEACHER" "$(exam_body "$OPEN_NOW" "$CLOSE_LATER" 30 " mo ta dai" "$LONG_1001")")"
expect_status "mo ta de 1000 ky tu → 200" 200 \
  "$(call POST /api/exams "$TEACHER" "$(exam_body "$OPEN_NOW" "$CLOSE_LATER" 30 " mo ta vua" "$LONG_1000")")"
EXAMS+=("$(json_field id)")

expect_status "email ho so 300 ky tu → 400" 400 \
  "$(call PUT /api/auth/me "$TEACHER" "{\"fullName\":\"Nguyen Van An\",\"email\":\"$LONG_EMAIL\",\"phone\":\"\"}")"

expect_status "anh dap an dai hon cot → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"x $STAMP\",\"topic\":\"RaSoat$STAMP\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"imageUrl\":\"$LONG_URL\"}]}")"
expect_status "mang dap an co phan tu null → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"x $STAMP\",\"topic\":\"RaSoat$STAMP\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[null,{\"content\":\"A\",\"isCorrect\":true}]}")"
expect_status "goi luu nhap co phan tu null → 400" 400 \
  "$(call PUT /api/exam-attempts/999999/answers "$STUDENT_A" '{"clientSeq":1,"answers":[null]}')"

# ── Ho so hoc sinh ───────────────────────────────────────────────────────────────

section "Hoc sinh khong tu doi ho ten va email"

expect_status "xem ho so cua chinh minh → 200" 200 "$(call GET /api/auth/me "$STUDENT_A")"
MY_NAME=$(json_field fullName); MY_EMAIL=$(json_field email)

expect_status "doi ho ten → 400" 400 \
  "$(call PUT /api/auth/me "$STUDENT_A" "{\"fullName\":\"Ten Khac\",\"email\":\"$MY_EMAIL\",\"phone\":\"\"}")"
expect_json   "  loi gan vao o fullName" errors.fullName.0 "Họ tên và email chỉ quản trị viên mới đổi được."
expect_status "doi email → 400" 400 \
  "$(call PUT /api/auth/me "$STUDENT_A" "{\"fullName\":\"$MY_NAME\",\"email\":\"khac$STAMP@kiemthu.vn\",\"phone\":\"\"}")"
expect_json   "  loi gan vao o email" errors.email.0 "Họ tên và email chỉ quản trị viên mới đổi được."
expect_status "giu nguyen ho ten va email, sua so dien thoai → 200" 200 \
  "$(call PUT /api/auth/me "$STUDENT_A" "{\"fullName\":\"$MY_NAME\",\"email\":\"$MY_EMAIL\",\"phone\":\"0912345678\"}")"
expect_json   "  so dien thoai da luu" phone 0912345678
expect_json   "  ho ten khong doi" fullName "$MY_NAME"

# ── Khoi phuc giu trang thai khoa ────────────────────────────────────────────────

section "Khoi phuc tai khoan giu nguyen trang thai khoa"

expect_status "khoa hoc sinh → 200" 200 "$(call PATCH "/api/students/$STUDENT_C_ID/status" "$ADMIN" '{"status":"locked"}')"
expect_status "dua vao dien xoa → 200" 200 \
  "$(call POST "/api/students/$STUDENT_C_ID/request-deletion" "$ADMIN" '{"reason":"Kiem thu: khoi phuc phai tra ve trang thai khoa."}')"
expect_json   "  status = pendingDeletion" status pendingDeletion
expect_status "khoi phuc → 200" 200 "$(call POST "/api/students/$STUDENT_C_ID/restore" "$ADMIN")"
expect_json   "  van la locked, khong tu mo khoa" status locked
expect_status "mo khoa → 200" 200 "$(call PATCH "/api/students/$STUDENT_C_ID/status" "$ADMIN" '{"status":"active"}')"
expect_json   "  status = active" status active

expect_status "hoc sinh dang hoat dong: xoa roi khoi phuc → active" 200 \
  "$(call POST "/api/students/$STUDENT_C_ID/request-deletion" "$ADMIN" '{"reason":"Kiem thu: em dang hoat dong thi khoi phuc ve hoat dong."}')"
expect_status "  khoi phuc → 200" 200 "$(call POST "/api/students/$STUDENT_C_ID/restore" "$ADMIN")"
expect_json   "  status = active" status active

# ── /health ──────────────────────────────────────────────────────────────────────

section "/health cham database"

expect_status "GET /health → 200" 200 "$(call GET /health "")"
expect_json   "  database = ok" database ok

# ── Thong bao: link va khung gio da dong ─────────────────────────────────────────

section "Thong bao tro dung ket qua, va khong bao bai o khung gio da dong"

# De dang mo, giao co thong bao.
new_exam "$OPEN_NOW" "$CLOSE_LATER" 30 " nop bai" "" true; OPEN_EXAM=$NEW_EXAM_ID
ASSIGN_OPEN=$(assignment_for "$STUDENT_A" "$OPEN_EXAM")
expect_status "hoc sinh nhan thong bao giao bai" 200 "$(call GET /api/notifications/unread-count "$STUDENT_A")"
UNREAD_AFTER_OPEN=$(body)
expect_status "  co 1 thong bao chua doc" 1 "$UNREAD_AFTER_OPEN"

expect_status "bat dau lam bai → 200" 200 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_OPEN}")"
ATTEMPT_OPEN=$(json_field attemptId)
expect_status "nop bai → 200" 200 "$(call POST "/api/exam-attempts/$ATTEMPT_OPEN/submit" "$STUDENT_A" '{"answers":[]}')"

expect_status "giao vien doc thong bao → 200" 200 "$(call GET "/api/notifications?pageSize=50" "$TEACHER")"
expect_status "  thong bao nop bai tro thang vao ket qua cua luot do" co \
  "$(has_notification examSubmitted giao-vien "$ATTEMPT_OPEN")"

# De da dong tu truoc, giao co bat thong bao: khong ai con "hoan thanh truoc han" duoc nua.
new_exam "2020-01-01T00:00:00Z" "2021-01-01T00:00:00Z" 30 " da dong" "" true
expect_status "doc lai so chua doc cua hoc sinh" 200 "$(call GET /api/notifications/unread-count "$STUDENT_A")"
expect_status "  giao vao khung da dong khong sinh them thong bao" "$UNREAD_AFTER_OPEN" "$(body)"

# ── Tu nop: gio nop khong muon hon han ───────────────────────────────────────────

section "Bai tu nop ghi gio nop bang han, khong phai luc tien trinh nen quet toi"

SHORT_OPEN=$(date -u -d '-1 hour' +%Y-%m-%dT%H:%M:%SZ)
SHORT_CLOSE=$(date -u -d '+25 seconds' +%Y-%m-%dT%H:%M:%SZ)
new_exam "$SHORT_OPEN" "$SHORT_CLOSE" 1 " tu nop"; SHORT_EXAM=$NEW_EXAM_ID
ASSIGN_SHORT=$(assignment_for "$STUDENT_A" "$SHORT_EXAM")

expect_status "bat dau luot sap het gio → 200" 200 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_SHORT}")"
SHORT_ATTEMPT=$(json_field attemptId)
SHORT_EXPIRES=$(json_field expiresAt)

printf '  cho tien trinh nen chot luot het gio (toi da ~75 giay)...\n'
STATUS=""
for _ in $(seq 1 25); do
  sleep 3
  call GET "/api/results/$SHORT_ATTEMPT" "$TEACHER" > /dev/null
  STATUS=$(json_field status)
  [[ "$STATUS" == "graded" ]] && break
done
expect_status "luot da duoc chot" graded "$STATUS"
expect_json   "  autoSubmitted = true" autoSubmitted true
expect_status "  gio nop bang han cua luot" khop \
  "$(python -c "
import json,sys
from datetime import datetime
read = lambda v: datetime.fromisoformat(v.replace('Z', '+00:00'))
submitted = read(json.load(open(sys.argv[1], encoding='utf-8'))['submittedAt'])
lech = (submitted - read(sys.argv[2])).total_seconds()
print('khop' if abs(lech) < 1 else 'lech %.1f giay' % lech)
" "$BODY_FILE" "$SHORT_EXPIRES")"

expect_status "hoc sinh doc thong bao → 200" 200 "$(call GET "/api/notifications?pageSize=50" "$STUDENT_A")"
expect_status "  thong bao het gio tro vao ket qua cua luot do" co \
  "$(has_notification autoSubmitted hoc-sinh "$SHORT_ATTEMPT")"
expect_status "giao vien doc thong bao → 200" 200 "$(call GET "/api/notifications?pageSize=50" "$TEACHER")"
expect_status "  thong bao tu nop cua giao vien tro vao ket qua cua luot do" co \
  "$(has_notification autoSubmitted giao-vien "$SHORT_ATTEMPT")"

summary
