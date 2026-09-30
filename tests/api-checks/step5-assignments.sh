#!/usr/bin/env bash
# Bước 5 — Giao bài và sinh đề (§4.6 phần assignment).
#
# Chạy:  bash tests/api-checks/step5-assignments.sh   (API phải đang chạy ở $HOST)
#
# Script tự dựng nhóm lớp, học sinh, câu hỏi và đề riêng của nó rồi tự dọn trong trap EXIT.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

STAMP=$(date +%H%M%S)
# GROUP_IDS chứ không phải GROUPS: bash có sẵn một mảng dựng sẵn tên GROUPS mang numeric group
# id của người dùng hiện tại, và phép gán của script bị nó nuốt mất.
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=()

cleanup() {
  section "Dọn dữ liệu"
  local exams=0 members=0 groups=0 questions=0 locked=0

  for id in "${EXAMS[@]}"; do
    [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]] && exams=$((exams + 1))
  done
  for id in "${QUESTIONS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1))
  done

  # Tài khoản học sinh cố ý KHÔNG có endpoint xoá (§4.2): khoá tài khoản chứ không xoá, để kết quả
  # các em đã làm vẫn tra cứu được. Nên bước dọn ở đây gỡ các em khỏi nhóm rồi khoá lại — nhóm mới
  # xoá được, còn tài khoản thì nằm im và không ảnh hưởng lần chạy sau.
  # Gỡ khỏi lớp và xoá lớp dùng token QUẢN TRỊ VIÊN: lớp do quản trị viên lập, giáo viên gọi hai thao
  # tác này thì nhận 403 và mỗi lần chạy lại để sót một lớp thử.
  for id in "${STUDENTS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$ADMIN")" == "200" ]] && members=$((members + 1))
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}')" == "200" ]] && locked=$((locked + 1))
  done

  for id in "${GROUP_IDS[@]}"; do
    local groupCode; groupCode=$(call DELETE "/api/student-groups/$id" "$ADMIN")
    if [[ "$groupCode" == "200" ]]; then
      groups=$((groups + 1))
    else
      printf '  không xoá được nhóm %s: HTTP %s — %s
' "$id" "$groupCode" "$(body | head -c 160)"
    fi
  done

  printf '  đã xoá %d/%d đề · %d/%d câu hỏi · %d/%d nhóm
'     "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}"
  printf '  học sinh: gỡ khỏi nhóm %d/%d, khoá %d/%d (không xoá được — đúng thiết kế)
'     "$members" "${#STUDENTS[@]}" "$locked" "${#STUDENTS[@]}"
}

trap cleanup EXIT

fail_fast() { echo "  Dựng dữ liệu nền thất bại: $1" >&2; exit 1; }

section "Dựng dữ liệu nền"

# Nhóm lớp do chính giáo viên này quản lý — đó là ranh giới phạm vi mà bước 5 phải tôn trọng.
# Lop giờ do quan tri vien lap, roi phan cong giao vien, roi cong bo — xem new_class trong lib.sh.
GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KiemThu $STAMP" "Nhom kiem thu tu dong")   || fail_fast "khong dung duoc lop nen"
GROUP_IDS+=("$GROUP_ID")

# Học sinh do quản trị viên tạo (§4.2: giáo viên không tạo được tài khoản).
NEW_STUDENT_ID=""
new_student() {
  local suffix="$1" group="$2" payload
  payload="{\"fullName\":\"Hoc sinh $suffix\",\"email\":\"hs$suffix@kiemthu.vn\",\"username\":\"hs$suffix\",\"code\":\"KT$suffix\",\"password\":\"123456\""
  [[ -n "$group" ]] && payload="$payload,\"groupId\":$group"
  payload="$payload}"
  local c; c=$(call POST /api/students "$ADMIN" "$payload")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $c — $(body)"
  NEW_STUDENT_ID=$(json_field id)
  STUDENTS+=("$NEW_STUDENT_ID")
}

new_student "${STAMP}a" "$GROUP_ID"; S1=$NEW_STUDENT_ID
new_student "${STAMP}b" "$GROUP_ID"; S2=$NEW_STUDENT_ID
new_student "${STAMP}c" "$GROUP_ID"; S3=$NEW_STUDENT_ID
new_student "${STAMP}x" ""; OUTSIDER=$NEW_STUDENT_ID          # không thuộc nhóm nào → ngoài phạm vi
new_student "${STAMP}l" "$GROUP_ID"; LOCKED=$NEW_STUDENT_ID   # thuộc nhóm nhưng sẽ bị khoá

code=$(call PATCH "/api/students/$LOCKED/status" "$ADMIN" '{"status":"locked"}')
[[ "$code" != "200" ]] && fail_fast "khoa tai khoan: HTTP $code — $(body)"

# 4 Dễ, 3 Trung bình, 2 Khó — vừa đủ cho một đề 9 câu theo tỷ lệ 45/33/22.
for pair in easy:1 easy:2 easy:3 easy:4 medium:1 medium:2 medium:3 hard:1 hard:2; do
  d="${pair%%:*}"; i="${pair##*:}"
  c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"KiemThu$STAMP\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi $d$i: HTTP $c — $(body)"
  QUESTIONS+=("$(json_field id)")
done

printf '  nhóm %s · 5 học sinh (1 ngoài nhóm, 1 bị khoá) · %d câu hỏi\n' "$GROUP_ID" "${#QUESTIONS[@]}"

exam_body() {
  local lifecycle_line=""
  [[ -n "${1:-}" ]] && lifecycle_line=$(printf ',\n  "lifecycle": "%s"' "$1")
  cat <<JSON
{
  "title": "De kiem thu $STAMP",
  "topic": "KiemThu$STAMP",
  "durationMinutes": 45,
  "totalQuestions": 9,
  "easyPercentage": 45,
  "mediumPercentage": 33,
  "hardPercentage": 22,
  "pointPerQuestion": 1,
  "openAt": "2020-01-01T00:00:00Z",
  "closeAt": "2035-01-01T00:00:00Z",
  "shuffleQuestions": true,
  "shuffleAnswers": true,
  "showResultAfterSubmit": true$lifecycle_line
}
JSON
}

section "Đề nháp không giao được"
code=$(call POST /api/exams "$TEACHER" "$(exam_body draft)")
[[ "$code" != "200" ]] && fail_fast "tao de nhap: HTTP $code — $(body)"
DRAFT_ID=$(json_field id); EXAMS+=("$DRAFT_ID")
expect_status "giao đề còn là nháp → 409" 409 \
  "$(call POST "/api/exams/$DRAFT_ID/assignments" "$TEACHER" "{\"studentIds\":[$S1],\"groupIds\":[],\"randomizePerStudent\":false,\"notify\":false}")"

section "Giao bài"
code=$(call POST /api/exams "$TEACHER" "$(exam_body)")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EXAM_ID=$(json_field id); EXAMS+=("$EXAM_ID")

expect_status "không chọn ai → 400" 400 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" '{"studentIds":[],"groupIds":[],"randomizePerStudent":false,"notify":false}')"


# Từng trả 500: DTO khai mặc định mảng rỗng nhưng System.Text.Json ghi đè bằng null khi client gửi
# thẳng null, và quy tắc đọc .Count trên đó ném NullReferenceException.
expect_status "studentIds/groupIds = null → 400, không phải 500" 400   "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" '{"studentIds":null,"groupIds":null,"randomizePerStudent":false,"notify":false}')"

expect_status "id âm trong danh sách → 400" 400   "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" '{"studentIds":[-1],"groupIds":[],"randomizePerStudent":false,"notify":false}')"
# Giao cả nhóm: 5 em trong nhóm, nhưng 1 em đang khoá → chỉ 4 em nhận bài.
expect_status "giao cả nhóm → 200" 200 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":true}")"
expect_json "  assignedCount = 3 (4 em trong nhóm trừ 1 khoá)" assignedCount 3
expect_json "  skippedLocked = 1"                   skippedLocked 1

# Bấm giao lần nữa: không tạo thêm bài nào.
expect_status "giao lại cùng nhóm → 200 (idempotent)" 200 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":true}")"
expect_json "  assignedCount = 0"            assignedCount 0
expect_json "  skippedAlreadyAssigned = 3"   skippedAlreadyAssigned 3

expect_status "học sinh ngoài nhóm + id lạ → 200" 200 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[$OUTSIDER,999999],\"groupIds\":[],\"randomizePerStudent\":false,\"notify\":false}")"
expect_json "  assignedCount = 0"        assignedCount 0
expect_json "  skippedOutOfScope = 2 (em lop khac + id la, gop lam mot)" skippedOutOfScope 2
expect_status "  khong con truong skippedNotFound de doi chieu" absent \
  "$(body | python -c 'import json,sys;print("present" if "skippedNotFound" in json.load(sys.stdin) else "absent")')"

# Doi chung: hai id lac, khong id nao la hoc sinh that, cho ra dung con so nhu tren. Tach hai ly do
# ra thanh hai con so la giao vien ban tung id mot roi doc xem id nao la hoc sinh that cua lop nguoi
# khac — dung thu ma phep kiem pham vi sinh ra de giau.
expect_status "hai id la, khong em nao co that → 200" 200 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" '{"studentIds":[999998,999999],"groupIds":[],"randomizePerStudent":false,"notify":false}')"
expect_json "  skippedOutOfScope = 2, khong phan biet duoc voi truong hop tren" skippedOutOfScope 2

# Chọn một em lẻ đã nằm trong nhóm vừa giao: khử trùng lặp phải nhận ra.
expect_status "chọn lẻ em đã có bài → 200" 200 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[$S1],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":false}")"
expect_json "  assignedCount = 0"          assignedCount 0
expect_json "  skippedAlreadyAssigned = 3" skippedAlreadyAssigned 3

section "Danh sách đã giao và số đếm dẫn xuất"
expect_status "danh sách assignment → 200" 200 "$(call GET "/api/exams/$EXAM_ID/assignments" "$TEACHER")"
ASSIGN_COUNT=$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')
expect_status "  đúng 3 dòng" 3 "$ASSIGN_COUNT"
expect_json   "  status của em đầu = assigned" 0.status assigned
expect_status "  attemptId vắng mặt khi chưa làm bài" absent   "$(body | python -c 'import json,sys;print("present" if "attemptId" in json.load(sys.stdin)[0] else "absent")')"
ASSIGNMENT_ID=$(body | python -c 'import json,sys;print(json.load(sys.stdin)[0]["id"])')

expect_status "assignedCount trên đề = 3" 200 "$(call GET "/api/exams/$EXAM_ID" "$TEACHER")"
expect_json   "  assignedCount = 3"  assignedCount 3
expect_json   "  submittedCount = 0" submittedCount 0

section "Phân quyền"
expect_status "quản trị viên xem danh sách giao → 200" 200 "$(call GET "/api/exams/$EXAM_ID/assignments" "$ADMIN")"
expect_status "quản trị viên giao bài → 403" 403 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$ADMIN" "{\"studentIds\":[$S1],\"groupIds\":[],\"randomizePerStudent\":false,\"notify\":false}")"
expect_status "học sinh xem danh sách giao → 403" 403 "$(call GET "/api/exams/$EXAM_ID/assignments" "$STUDENT")"
expect_status "học sinh tự giao bài cho mình → 403" 403 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$STUDENT" "{\"studentIds\":[$S1],\"groupIds\":[],\"randomizePerStudent\":false,\"notify\":false}")"
expect_status "giao trên đề không tồn tại → 404" 404 \
  "$(call POST "/api/exams/999999/assignments" "$TEACHER" "{\"studentIds\":[$S1],\"groupIds\":[],\"randomizePerStudent\":false,\"notify\":false}")"

section "Thu hồi"
expect_status "thu hồi assignment không thuộc đề này → 404" 404 \
  "$(call DELETE "/api/exams/$DRAFT_ID/assignments/$ASSIGNMENT_ID" "$TEACHER")"
expect_status "thu hồi assignment không tồn tại → 404" 404 \
  "$(call DELETE "/api/exams/$EXAM_ID/assignments/999999" "$TEACHER")"
expect_status "quản trị viên thu hồi → 403" 403 \
  "$(call DELETE "/api/exams/$EXAM_ID/assignments/$ASSIGNMENT_ID" "$ADMIN")"
expect_status "thu hồi hợp lệ → 200" 200 \
  "$(call DELETE "/api/exams/$EXAM_ID/assignments/$ASSIGNMENT_ID" "$TEACHER")"
expect_status "thu hồi lại chính nó → 404" 404 \
  "$(call DELETE "/api/exams/$EXAM_ID/assignments/$ASSIGNMENT_ID" "$TEACHER")"

expect_status "sau thu hồi, assignedCount còn 2" 200 "$(call GET "/api/exams/$EXAM_ID" "$TEACHER")"
expect_json   "  assignedCount = 2" assignedCount 2

# Giao lại chính em vừa bị thu hồi: phải nhận bài mới, chứng minh thu hồi thật sự xoá bản ghi.
expect_status "giao lại em vừa thu hồi → 200" 200 \
  "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":false}")"
expect_json   "  assignedCount = 1" assignedCount 1

section "randomizePerStudent — cờ thật, không phải điều khiển giả"
code=$(call POST /api/exams "$TEACHER" "$(exam_body)")
PER_STUDENT_ID=$(json_field id); EXAMS+=("$PER_STUDENT_ID")
expect_status "giao với randomizePerStudent = true → 200" 200 \
  "$(call POST "/api/exams/$PER_STUDENT_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":true,\"notify\":false}")"
expect_json   "  assignedCount = 3" assignedCount 3
expect_status "cờ được ghi lên đề" 200 "$(call GET "/api/exams/$PER_STUDENT_ID" "$TEACHER")"
expect_json   "  randomizePerStudent = true" randomizePerStudent true

section "Bo trong randomizePerStudent = giu nguyen, khong phai false"

# Kieu bool thuong thi "bo trong" va "gui false" den server deu thanh false. Voi de da giao theo
# kieu de rieng, moi lan giao them du chi mot em cung bi tu choi 409, va cach duy nhat de them
# nguoi la thu hoi ca lop roi giao lai tu dau.
expect_status "giao them ma KHONG gui randomizePerStudent → 200" 200   "$(call POST "/api/exams/$PER_STUDENT_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":false}")"
expect_status "co gang doi co tren de da giao → 409" 409   "$(call POST "/api/exams/$PER_STUDENT_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":false}")"
expect_status "co van giu nguyen sau lan bi tu choi" 200 "$(call GET "/api/exams/$PER_STUDENT_ID" "$TEACHER")"
expect_json   "  randomizePerStudent van = true" randomizePerStudent true

section "Dem nhom ngoai pham vi tach khoi dem hoc sinh"

# Gop chung hai con so khac don vi thi mot lop 40 em bi tu choi hien ra la "bo qua 1", va giao vien
# doc con so do se tuong chi mot em gap van de.
expect_status "chon mot id nhom khong thuoc minh → 200" 200   "$(call POST "/api/exams/$EXAM_ID/assignments" "$TEACHER" '{"studentIds":[],"groupIds":[999999],"notify":false}')"
expect_json   "  skippedOutOfScopeGroups = 1" skippedOutOfScopeGroups 1
expect_json   "  skippedOutOfScope = 0 (khong co hoc sinh nao bi loai)" skippedOutOfScope 0

summary
