#!/usr/bin/env bash
# Bước 6 — Làm bài và chấm ở server (§4.7).
#
# Chạy:  bash tests/api-checks/step6-attempts.sh   (API phải đang chạy ở $HOST)
#
# Script tự dựng nhóm lớp, học sinh, câu hỏi và các đề riêng của nó rồi tự dọn trong trap EXIT.
# Có một quãng chờ ~30 giây ở phần kiểm hết giờ: đó là cách duy nhất chứng minh hạn giờ thật sự
# tính bằng đồng hồ server chứ không bằng đồng hồ của máy gọi.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")
ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")

STAMP=$(date +%H%M%S)
# GROUP_IDS chứ không phải GROUPS: bash có sẵn một mảng dựng sẵn tên GROUPS mang numeric group id
# của người dùng hiện tại, và phép gán của script bị nó nuốt mất.
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=()

# Khai trước khi đặt trap: cleanup có thể chạy ngay từ lần fail_fast đầu tiên, mà lib.sh bật set -u.
GROUP_ID=""

# Thân response của lời gọi bắt đầu làm bài phải dùng lại qua nhiều lời gọi sau, mà $BODY_FILE thì
# bị ghi đè mỗi lần call. Chép sang một file riêng.
SESSION_FILE=$(mktemp -t anthill-session.XXXXXX)

cleanup() {
  section "Dọn dữ liệu"
  local exams=0 kept=0 members=0 groups=0 questions=0 locked=0

  for id in "${EXAMS[@]}"; do
    if [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]]; then
      exams=$((exams + 1))
    else
      kept=$((kept + 1))
    fi
  done
  for id in "${QUESTIONS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1))
  done

  # Tài khoản học sinh cố ý KHÔNG có endpoint xoá (§4.2): khoá chứ không xoá, để kết quả các em đã
  # làm vẫn tra cứu được. Gỡ khỏi nhóm rồi khoá lại — nhóm mới xoá được, còn tài khoản nằm im.
  for id in "${STUDENTS[@]}"; do
    if [[ -n "$GROUP_ID" ]]; then
      [[ "$(call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$TEACHER")" == "200" ]] && members=$((members + 1))
    fi
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}')" == "200" ]] && locked=$((locked + 1))
  done

  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$TEACHER")" == "200" ]] && groups=$((groups + 1))
  done

  rm -f "$SESSION_FILE"

  printf '  đã xoá %d/%d đề · %d/%d câu hỏi · %d/%d nhóm\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}"
  printf '  học sinh: gỡ khỏi nhóm %d/%d, khoá %d/%d (không xoá được — đúng thiết kế)\n' \
    "$members" "${#STUDENTS[@]}" "$locked" "${#STUDENTS[@]}"
  if [[ $kept -gt 0 ]]; then
    printf '  %d đề ở lại cùng câu hỏi của nó: đề đã có bài nộp thì không xoá được, để kết quả\n' "$kept"
    printf '  của học sinh vẫn tra cứu được — đúng thiết kế, không phải lỗi dọn dẹp.\n'
  fi
}

trap cleanup EXIT

fail_fast() { echo "  Dựng dữ liệu nền thất bại: $1" >&2; exit 1; }

# ── Trợ giúp đọc dữ liệu ─────────────────────────────────────────────────────────

# Id bản ghi giao bài của một học sinh trên một đề. Hàm chạy trong $( ) là subshell nên nó chỉ được
# phép in ra; mọi phép gán biến bên trong đều mất khi subshell kết thúc.
assignment_for() {
  call GET "/api/my/exams" "$1" > /dev/null
  python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((item['assignmentId'] for item in items if item['examId'] == int(sys.argv[2])), ''))
" "$BODY_FILE" "$2"
}

# Id của câu thứ $2 (đếm từ 0) trong phiên đã lưu ở file $1.
question_id() {
  python -c "
import json,sys
print(json.load(open(sys.argv[1], encoding='utf-8'))['questions'][int(sys.argv[2])]['id'])
" "$1" "$2"
}

# Id một đáp án của câu thứ $2: $3 là 'dung' hoặc 'sai'. Đề do chính script tạo nên nội dung hai
# phương án là cố định, còn thứ tự thì không — shuffleAnswers đang bật, phải dò theo nội dung.
answer_id() {
  python -c "
import json,sys
question = json.load(open(sys.argv[1], encoding='utf-8'))['questions'][int(sys.argv[2])]
label = 'Dung' if sys.argv[3] == 'dung' else 'Sai'
print(next(answer['id'] for answer in question['answers'] if answer['content'] == label))
" "$1" "$2" "$3"
}

# Gói lưu nháp cho $4 câu liên tiếp kể từ câu $3, chọn toàn đáp án $5, mang clientSeq $2.
selections() {
  python -c "
import json,sys
session = json.load(open(sys.argv[1], encoding='utf-8'))
seq, start, count, want = int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
label = 'Dung' if want == 'dung' else 'Sai'
answers = []
for question in session['questions'][start:start + count]:
    picked = next(a for a in question['answers'] if a['content'] == label)
    answers.append({'questionId': question['id'], 'answerId': picked['id']})
print(json.dumps({'clientSeq': seq, 'answers': answers}))
" "$1" "$2" "$3" "$4" "$5"
}

saved_count() { python -c "import json,sys;print(len(json.load(sys.stdin).get('savedAnswers',{})))" < "$BODY_FILE"; }

saved_answer() {
  python -c "
import json,sys
print(json.load(sys.stdin).get('savedAnswers', {}).get(sys.argv[1], 'khong-co'))
" "$1" < "$BODY_FILE"
}

# Giá trị một trường của mục ứng với đề $1 trong danh sách vừa đọc. Danh sách sắp theo ngày giao
# nên không được tra theo chỉ số: ba đề tạo trong cùng một giây thì thứ tự giữa chúng là ngẫu nhiên.
item_field() {
  python -c "
import json,sys
item = next(i for i in json.load(sys.stdin) if i['examId'] == int(sys.argv[1]))
value = item.get(sys.argv[2], 'khong-co')
print(json.dumps(value) if isinstance(value, bool) else value)
" "$1" "$2" < "$BODY_FILE"
}

# Có trường nào trong response hay không — dùng cho những phép kiểm mà §3 đòi trường phải VẮNG MẶT
# chứ không phải mang giá trị rỗng.
has_field() {
  python -c "
import json,sys
print('present' if sys.argv[1] in json.load(sys.stdin) else 'absent')
" "$1" < "$BODY_FILE"
}

# So một số trong thân JSON mà không phụ thuộc số chữ số thập phân: cột điểm khai precision (6,2)
# nên số 7 có thể tới dưới dạng "7.00".
expect_number() {
  local label="$1" path="$2" expected="$3" actual
  actual=$(python -c "
import json,sys
value = json.load(sys.stdin)
for key in sys.argv[1].split('.'):
    value = value[int(key)] if key.isdigit() else value[key]
print('%g' % float(value))
" "$path" < "$BODY_FILE" 2>/dev/null)
  expect_status "$label" "$expected" "${actual:-<khong doc duoc>}"
}

# ── Dựng dữ liệu nền ─────────────────────────────────────────────────────────────

section "Dựng dữ liệu nền"

GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KiemThu6 $STAMP" "Nhom kiem thu buoc 6")   || fail_fast "khong dung duoc lop nen"
GROUP_IDS+=("$GROUP_ID")

NEW_STUDENT_ID=""
new_student() {
  local suffix="$1" payload
  payload="{\"fullName\":\"Hoc sinh $suffix\",\"email\":\"hs$suffix@kiemthu.vn\",\"username\":\"hs$suffix\""
  payload="$payload,\"code\":\"KT$suffix\",\"password\":\"anthill123\",\"groupId\":$GROUP_ID}"
  local c; c=$(call POST /api/students "$ADMIN" "$payload")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $c — $(body)"
  NEW_STUDENT_ID=$(json_field id)
  STUDENTS+=("$NEW_STUDENT_ID")
}

# Id hai em nằm sẵn trong mảng STUDENTS để dọn dẹp; phần kiểm bên dưới chỉ cần token đăng nhập
# và assignmentId, không cần tới id tài khoản.
new_student "${STAMP}a"
new_student "${STAMP}b"

# 4 Dễ, 3 Trung bình, 2 Khó — vừa đủ cho một đề 9 câu theo tỷ lệ 45/33/22.
for pair in easy:1 easy:2 easy:3 easy:4 medium:1 medium:2 medium:3 hard:1 hard:2; do
  d="${pair%%:*}"; i="${pair##*:}"
  c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"KiemThu$STAMP\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi $d$i: HTTP $c — $(body)"
  QUESTIONS+=("$(json_field id)")
done

STUDENT_A=$(login "hs${STAMP}a" "anthill123")
STUDENT_B=$(login "hs${STAMP}b" "anthill123")

printf '  nhóm %s · 2 học sinh · %d câu hỏi\n' "$GROUP_ID" "${#QUESTIONS[@]}"

# $1 openAt · $2 closeAt · $3 thời lượng (phút) · $4 hậu tố tiêu đề
exam_body() {
  cat <<JSON
{
  "title": "De lam bai $STAMP${4:-}",
  "description": "De kiem thu buoc 6",
  "topic": "KiemThu$STAMP",
  "durationMinutes": $3,
  "totalQuestions": 9,
  "easyPercentage": 45,
  "mediumPercentage": 33,
  "hardPercentage": 22,
  "pointPerQuestion": 1,
  "openAt": "$1",
  "closeAt": "$2",
  "shuffleQuestions": true,
  "shuffleAnswers": true,
  "showResultAfterSubmit": ${5:-true}
}
JSON
}

# Tạo đề rồi giao cho cả nhóm, đặt kết quả vào NEW_EXAM_ID. Gọi trực tiếp chứ không trong $( ):
# phép gán vào NEW_EXAM_ID và EXAMS phải sống sót sau khi hàm chạy xong.
NEW_EXAM_ID=""
new_exam() {
  local c; c=$(call POST /api/exams "$TEACHER" "$(exam_body "$1" "$2" "$3" "${4:-}" "${5:-true}")")
  [[ "$c" != "200" ]] && fail_fast "tao de: HTTP $c — $(body)"
  NEW_EXAM_ID=$(json_field id); EXAMS+=("$NEW_EXAM_ID")
  c=$(call POST "/api/exams/$NEW_EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"randomizePerStudent\":false,\"notify\":false}")
  [[ "$c" != "200" ]] && fail_fast "giao de $NEW_EXAM_ID: HTTP $c — $(body)"
}

new_exam "2020-01-01T00:00:00Z" "2035-01-01T00:00:00Z" 45; OPEN_EXAM=$NEW_EXAM_ID
new_exam "2035-01-01T00:00:00Z" "2035-01-02T00:00:00Z" 45 " sap dien ra"; SCHEDULED_EXAM=$NEW_EXAM_ID
new_exam "2020-01-01T00:00:00Z" "2021-01-01T00:00:00Z" 45 " da dong"; CLOSED_EXAM=$NEW_EXAM_ID

printf '  3 đề: đang mở %s · sắp diễn ra %s · đã đóng %s\n' "$OPEN_EXAM" "$SCHEDULED_EXAM" "$CLOSED_EXAM"

ASSIGN_A=$(assignment_for "$STUDENT_A" "$OPEN_EXAM")
ASSIGN_A_SCHEDULED=$(assignment_for "$STUDENT_A" "$SCHEDULED_EXAM")
ASSIGN_A_CLOSED=$(assignment_for "$STUDENT_A" "$CLOSED_EXAM")
ASSIGN_B=$(assignment_for "$STUDENT_B" "$OPEN_EXAM")

# ── Danh sách bài của chính mình ─────────────────────────────────────────────────

section "GET /api/my/exams — danh tính suy từ JWT"

expect_status "học sinh xem bài của mình → 200" 200 "$(call GET /api/my/exams "$STUDENT_A")"
expect_status "  đúng 3 bài được giao" 3 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "  đề đang mở, chưa vào thi → assigned"        assigned "$(item_field "$OPEN_EXAM" status)"
expect_status "  đề sắp diễn ra → assigned"                     assigned "$(item_field "$SCHEDULED_EXAM" status)"
expect_status "  đề đã đóng mà chưa từng vào thi → expired"  expired  "$(item_field "$CLOSED_EXAM" status)"
expect_status "  đề đã đóng thì không bấm vào thi được"       false    "$(item_field "$CLOSED_EXAM" canStart)"
expect_status "  không lộ điểm khi chưa làm" absent \
  "$(body | python -c "import json,sys;print('present' if any('score' in item for item in json.load(sys.stdin)) else 'absent')")"

expect_status "lọc trạng thái hợp lệ → 200" 200 "$(call GET "/api/my/exams?status=assigned" "$STUDENT_A")"
expect_status "lọc trạng thái lạ → 400 chứ không âm thầm bỏ lọc" 400 \
  "$(call GET "/api/my/exams?status=xong-roi" "$STUDENT_A")"

expect_status "giáo viên gọi /api/my/exams → 403" 403 "$(call GET /api/my/exams "$TEACHER")"
expect_status "quản trị viên gọi /api/my/exams → 403" 403 "$(call GET /api/my/exams "$ADMIN")"

section "GET /api/my/exams/{assignmentId} — kiểm quyền sở hữu"

expect_status "xem mục của mình → 200" 200 "$(call GET "/api/my/exams/$ASSIGN_A" "$STUDENT_A")"
expect_json   "  lifecycle = open" lifecycle open
expect_json   "  canStart = true"  canStart true
expect_status "xem mục của bạn cùng lớp → 404 (không phải 403)" 404 \
  "$(call GET "/api/my/exams/$ASSIGN_B" "$STUDENT_A")"
expect_status "mục không tồn tại → 404" 404 "$(call GET "/api/my/exams/999999" "$STUDENT_A")"

# ── Bắt đầu làm bài ──────────────────────────────────────────────────────────────

section "POST /api/exam-attempts — bắt đầu và idempotent"

expect_status "đề chưa tới giờ mở → 409" 409 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_A_SCHEDULED}")"
expect_status "đề đã đóng → 410" 410 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_A_CLOSED}")"
expect_status "assignment của bạn cùng lớp → 404" 404 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_B}")"
expect_status "assignmentId = 0 → 400" 400 "$(call POST /api/exam-attempts "$STUDENT_A" '{"assignmentId":0}')"
expect_status "giáo viên tự bắt đầu làm bài → 403" 403 \
  "$(call POST /api/exam-attempts "$TEACHER" "{\"assignmentId\":$ASSIGN_A}")"

expect_status "bắt đầu làm bài → 200" 200 "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_A}")"
cp "$BODY_FILE" "$SESSION_FILE"
ATTEMPT_A=$(json_field attemptId)
EXPIRES_FIRST=$(json_field expiresAt)
expect_status "  đủ 9 câu" 9 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)["questions"]))')"
expect_number "  maxScore = 9" maxScore 9
expect_json   "  savedAnswers rỗng" savedAnswers '{}'
expect_json   "  status = inprogress" status inprogress

# §3 quy tắc ①: gán isCorrect = false là không đủ, trường đó phải không có mặt. Quét cả chuỗi thay
# vì đọc từng khoá — một trường tên khác mà mang cùng thông tin cũng là lộ đáp án.
expect_status "  KHÔNG có chữ 'correct' nào trong response" absent \
  "$(body | python -c "import sys;print('present' if 'correct' in sys.stdin.read().lower() else 'absent')")"

expect_status "gọi lại lúc đang làm dở → 200 (idempotent)" 200 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_A}")"
expect_status "  vẫn đúng lượt cũ" "$ATTEMPT_A" "$(json_field attemptId)"
expect_status "  đồng hồ không bị cấp lại" "$EXPIRES_FIRST" "$(json_field expiresAt)"

section "GET /api/exam-attempts/{attemptId}"

expect_status "nạp lại phiên của mình → 200" 200 "$(call GET "/api/exam-attempts/$ATTEMPT_A" "$STUDENT_A")"
expect_status "phiên của bạn cùng lớp → 404" 404 "$(call GET "/api/exam-attempts/$ATTEMPT_A" "$STUDENT_B")"
expect_status "phiên không tồn tại → 404" 404 "$(call GET "/api/exam-attempts/999999" "$STUDENT_A")"
expect_status "đồng bộ đồng hồ → 200" 200 "$(call GET "/api/exam-attempts/$ATTEMPT_A/time" "$STUDENT_A")"
expect_status "đồng hồ của phiên người khác → 404" 404 \
  "$(call GET "/api/exam-attempts/$ATTEMPT_A/time" "$STUDENT_B")"

# ── Lưu nháp ─────────────────────────────────────────────────────────────────────

section "PUT /answers — MERGE và chống ghi đè ngược thứ tự"

Q0=$(question_id "$SESSION_FILE" 0)
Q1=$(question_id "$SESSION_FILE" 1)
Q0_DUNG=$(answer_id "$SESSION_FILE" 0 dung)
Q0_SAI=$(answer_id "$SESSION_FILE" 0 sai)
Q1_DUNG=$(answer_id "$SESSION_FILE" 1 dung)

expect_status "answers = null → 400, không phải 500" 400 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" '{"clientSeq":1,"answers":null}')"
expect_status "thiếu clientSeq → 400" 400 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"answers\":[{\"questionId\":$Q0,\"answerId\":$Q0_DUNG}]}")"
expect_status "hai dòng cho cùng một câu → 400" 400 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"clientSeq\":1,\"answers\":[{\"questionId\":$Q0,\"answerId\":$Q0_DUNG},{\"questionId\":$Q0,\"answerId\":$Q0_SAI}]}")"

expect_status "câu hỏi không thuộc lượt này → 422, không phải 400" 422 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"clientSeq\":1,\"answers\":[{\"questionId\":999999,\"answerId\":$Q0_DUNG}]}")"
expect_status "đáp án không thuộc câu đó → 422" 422 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"clientSeq\":1,\"answers\":[{\"questionId\":$Q0,\"answerId\":$Q1_DUNG}]}")"

expect_status "lưu câu 1 (clientSeq 1) → 200" 200 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "$(selections "$SESSION_FILE" 1 0 1 dung)")"
expect_status "lưu câu 2 (clientSeq 2) → 200" 200 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "$(selections "$SESSION_FILE" 2 1 1 dung)")"

call GET "/api/exam-attempts/$ATTEMPT_A" "$STUDENT_A" > /dev/null
expect_status "MERGE giữ lại câu của gói trước" 2 "$(saved_count)"
expect_status "  câu 1 vẫn là đáp án đã chọn" "$Q0_DUNG" "$(saved_answer "$Q0")"

# Gói cũ tới sau gói mới: autosave chạy mỗi 800ms nên đây là chuyện thường, không phải ngoại lệ.
expect_status "gói clientSeq nhỏ hơn → 200 nhưng không áp dụng" 200 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"clientSeq\":1,\"answers\":[{\"questionId\":$Q0,\"answerId\":$Q0_SAI}]}")"
call GET "/api/exam-attempts/$ATTEMPT_A" "$STUDENT_A" > /dev/null
expect_status "  câu 1 không bị gói cũ ghi đè" "$Q0_DUNG" "$(saved_answer "$Q0")"

expect_status "bỏ chọn một câu → 200" 200 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"clientSeq\":5,\"answers\":[{\"questionId\":$Q1,\"answerId\":null}]}")"
call GET "/api/exam-attempts/$ATTEMPT_A" "$STUDENT_A" > /dev/null
expect_status "  câu bỏ chọn biến mất khỏi savedAnswers" 1 "$(saved_count)"

expect_status "lưu nháp trên lượt của người khác → 404" 404 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_B" "{\"clientSeq\":9,\"answers\":[]}")"

# ── Nộp bài ──────────────────────────────────────────────────────────────────────

section "POST /submit — chấm ở server"

# Câu đầu đã lưu đúng ở trên. Gửi kèm 6 câu đúng và 2 câu sai: điểm phải là 7/9.
SUBMIT_BODY=$(python -c "
import json,sys
session = json.load(open(sys.argv[1], encoding='utf-8'))
answers = []
for index, question in enumerate(session['questions'][1:], start=1):
    label = 'Sai' if index >= 7 else 'Dung'
    picked = next(a for a in question['answers'] if a['content'] == label)
    answers.append({'questionId': question['id'], 'answerId': picked['id']})
print(json.dumps({'clientSeq': 10, 'answers': answers}))
" "$SESSION_FILE")

expect_status "nộp bài của người khác → 404" 404 \
  "$(call POST "/api/exam-attempts/$ATTEMPT_A/submit" "$STUDENT_B" '{"answers":[]}')"
expect_status "nộp bài kèm đáp án lạ → 422" 422 \
  "$(call POST "/api/exam-attempts/$ATTEMPT_A/submit" "$STUDENT_A" "{\"clientSeq\":10,\"answers\":[{\"questionId\":$Q0,\"answerId\":$Q1_DUNG}]}")"

expect_status "nộp bài → 200" 200 "$(call POST "/api/exam-attempts/$ATTEMPT_A/submit" "$STUDENT_A" "$SUBMIT_BODY")"
expect_json   "  status = graded"       status graded
expect_json   "  autoSubmitted = false" autoSubmitted false
expect_number "  score = 7"             score 7
expect_number "  maxScore = 9"          maxScore 9
expect_status "  correctCount = 7" 7 "$(json_field correctCount)"
expect_status "  answeredCount = 9" 9 "$(json_field answeredCount)"

# Retry sau timeout mạng vẫn tới server hai lần, dù frontend có cờ chống bấm đúp.
expect_status "nộp lại → 200 (idempotent)" 200 \
  "$(call POST "/api/exam-attempts/$ATTEMPT_A/submit" "$STUDENT_A" '{"answers":[]}')"
expect_number "  vẫn đúng điểm cũ" score 7
expect_status "  vẫn đúng lượt cũ" "$ATTEMPT_A" "$(json_field attemptId)"

expect_status "lưu nháp sau khi nộp → 409" 409 \
  "$(call PUT "/api/exam-attempts/$ATTEMPT_A/answers" "$STUDENT_A" "{\"clientSeq\":20,\"answers\":[]}")"
expect_status "nạp lại phiên đã nộp → 409" 409 "$(call GET "/api/exam-attempts/$ATTEMPT_A" "$STUDENT_A")"
expect_status "bắt đầu lại đúng bài đó → 409 (v1 không cho làm lại)" 409 \
  "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_A}")"

section "Điểm lan tới hai màn hình còn lại"

expect_status "danh sách của học sinh → 200" 200 "$(call GET "/api/my/exams?status=graded" "$STUDENT_A")"
expect_number "  score = 7" 0.score 7
expect_status "  correctCount = 7" 7 "$(json_field 0.correctCount)"

expect_status "bảng theo dõi của giáo viên → 200" 200 "$(call GET "/api/exams/$OPEN_EXAM" "$TEACHER")"
expect_json   "  submittedCount = 1" submittedCount 1

# ── Hết giờ ──────────────────────────────────────────────────────────────────────

section "Hạn giờ tính bằng đồng hồ server"

# Cửa sổ mở đóng lại sau 25 giây, còn thời lượng là 1 phút: hạn giờ phải bị kẹp về giờ đóng đề,
# nếu không một em bấm bắt đầu sát giờ đóng vẫn làm bài thêm 35 giây sau khi cả lớp đã dừng.
# date -d là cú pháp GNU, có sẵn ở Git Bash và mọi bản Linux.
SHORT_OPEN=$(date -u -d '-1 hour' +%Y-%m-%dT%H:%M:%SZ)
SHORT_CLOSE=$(date -u -d '+25 seconds' +%Y-%m-%dT%H:%M:%SZ)
new_exam "$SHORT_OPEN" "$SHORT_CLOSE" 1 " sap het gio"; SHORT_EXAM=$NEW_EXAM_ID

ASSIGN_B_SHORT=$(assignment_for "$STUDENT_B" "$SHORT_EXAM")

expect_status "bắt đầu lượt sắp hết giờ → 200" 200 \
  "$(call POST /api/exam-attempts "$STUDENT_B" "{\"assignmentId\":$ASSIGN_B_SHORT}")"
SHORT_ATTEMPT=$(json_field attemptId)
expect_status "  hạn giờ bị kẹp về giờ đóng đề" khop \
  "$(python -c "
import json,sys
from datetime import datetime
read = lambda value: datetime.fromisoformat(value.replace('Z', '+00:00'))
expires = read(json.load(open(sys.argv[1], encoding='utf-8'))['expiresAt'])
lech = (expires - read(sys.argv[2])).total_seconds()
print('khop' if abs(lech) < 1 else 'lech %.0f giay' % lech)
" "$BODY_FILE" "$SHORT_CLOSE")"

printf '  chờ 30 giây cho tới khi hết giờ...\n'
sleep 30

expect_status "lưu nháp sau khi hết giờ → 410" 410 \
  "$(call PUT "/api/exam-attempts/$SHORT_ATTEMPT/answers" "$STUDENT_B" '{"clientSeq":1,"answers":[]}')"

# Lượt quá hạn đã bị chốt sổ ngay ở lời gọi trên, nên nộp lại chỉ đọc lại kết quả đã chấm.
expect_status "nộp sau khi hết giờ → 200, đọc lại bản đã chốt" 200 \
  "$(call POST "/api/exam-attempts/$SHORT_ATTEMPT/submit" "$STUDENT_B" '{"answers":[]}')"
expect_json   "  status = graded"      status graded
expect_json   "  autoSubmitted = true" autoSubmitted true
expect_number "  score = 0 (chưa kịp trả lời câu nào)" score 0
expect_status "  answeredCount = 0" 0 "$(json_field answeredCount)"

expect_status "vào lại lượt đã hết giờ → 409 (đã nộp rồi)" 409 \
  "$(call POST /api/exam-attempts "$STUDENT_B" "{\"assignmentId\":$ASSIGN_B_SHORT}")"

# ── Không cho xem điểm ───────────────────────────────────────────────────────────

section "showResultAfterSubmit = false thì điểm không rời server"

new_exam "2020-01-01T00:00:00Z" "2035-01-01T00:00:00Z" 45 " kin" false; HIDDEN_EXAM=$NEW_EXAM_ID
ASSIGN_B_HIDDEN=$(assignment_for "$STUDENT_B" "$HIDDEN_EXAM")

expect_status "bắt đầu → 200" 200 "$(call POST /api/exam-attempts "$STUDENT_B" "{\"assignmentId\":$ASSIGN_B_HIDDEN}")"
HIDDEN_ATTEMPT=$(json_field attemptId)
expect_status "nộp → 200" 200 "$(call POST "/api/exam-attempts/$HIDDEN_ATTEMPT/submit" "$STUDENT_B" '{"answers":[]}')"
expect_json   "  showResultAfterSubmit = false" showResultAfterSubmit false
expect_status "  score vắng mặt khỏi JSON"        absent  "$(has_field score)"
expect_status "  correctCount vắng mặt khỏi JSON" absent  "$(has_field correctCount)"
expect_status "  maxScore vẫn có (biết thang điểm không lộ bài ai)" present "$(has_field maxScore)"

expect_status "màn giới thiệu của chính bài đó → 200" 200 "$(call GET "/api/my/exams/$ASSIGN_B_HIDDEN" "$STUDENT_B")"
expect_json   "  status = graded" status graded
expect_status "  vẫn không kèm score" absent "$(has_field score)"

# ── Bộ đếm gói lưu nháp: chốt chặn cho lỗi mất bài sau khi tải lại trang ────────

section "ClientSeq — client sau F5 không được mất bài trong im lặng"

new_exam "2020-01-01T00:00:00Z" "2035-01-01T00:00:00Z" 45 " seq" true; SEQ_EXAM=$NEW_EXAM_ID
ASSIGN_SEQ=$(assignment_for "$STUDENT_A" "$SEQ_EXAM")

expect_status "bắt đầu lượt → 200" 200 "$(call POST /api/exam-attempts "$STUDENT_A" "{\"assignmentId\":$ASSIGN_SEQ}")"
SEQ_ATTEMPT=$(json_field attemptId)
cp "$BODY_FILE" "$SESSION_FILE"
SEQ_Q0=$(question_id "$SESSION_FILE" 0)
SEQ_A0=$(answer_id "$SESSION_FILE" 0 dung)
SEQ_A1=$(answer_id "$SESSION_FILE" 0 sai)

# Session phải nói cho client biết server đang giữ số nào. Không có nó thì client sau F5 bắt đầu
# lại từ 0 và mọi gói lưu sau đó đều bị coi là cũ.
expect_status "  session có lastAppliedSeq" present "$(has_field lastAppliedSeq)"
expect_json   "  lượt mới thì lastAppliedSeq = 0" lastAppliedSeq 0

expect_status "lưu nháp seq=5 → 200" 200   "$(call PUT "/api/exam-attempts/$SEQ_ATTEMPT/answers" "$STUDENT_A" "{\"clientSeq\":5,\"answers\":[{\"questionId\":$SEQ_Q0,\"answerId\":$SEQ_A0}]}")"
expect_json   "  applied = true"       applied true
expect_json   "  lastAppliedSeq = 5"   lastAppliedSeq 5

# Gói cũ hơn: vẫn 200 nhưng PHẢI nói rõ là không ghi. Trước đây nó trả đúng câu "Đã lưu bài làm."
# như nhánh ghi thật, nên client có bộ đếm lệch thấy "đã lưu" ở mọi lần gọi mà database trống trơn.
expect_status "gói cũ seq=3 → 200 nhưng applied=false" 200   "$(call PUT "/api/exam-attempts/$SEQ_ATTEMPT/answers" "$STUDENT_A" "{\"clientSeq\":3,\"answers\":[{\"questionId\":$SEQ_Q0,\"answerId\":$SEQ_A1}]}")"
expect_json   "  applied = false"                    applied false
expect_json   "  tra lai so server dang giu = 5"     lastAppliedSeq 5

expect_status "nạp lại phiên → 200" 200 "$(call GET "/api/exam-attempts/$SEQ_ATTEMPT" "$STUDENT_A")"
expect_json   "  lastAppliedSeq = 5 de client noi tiep" lastAppliedSeq 5
expect_status "  dap an cu van la cai da ghi luc seq=5" "$SEQ_A0"   "$(body | python -c "import json,sys;print(json.load(sys.stdin)['savedAnswers'].get('$SEQ_Q0',''))")"

# Nộp bài KHÔNG được chặn theo seq: đây là lời tuyên bố cuối cùng của học sinh, không có gói nào
# mới hơn đang trên đường tới. Chặn ở đây nghĩa là chấm trên đáp án CŨ rồi trả về 200 kèm điểm.
#
# Cách kiểm: đã lưu đáp án ĐÚNG ở seq=5, giờ nộp kèm đáp án SAI với seq=1 (bộ đếm vừa khởi động
# lại như sau một lần F5). Chấm đúng thì correctCount = 0. Nếu nó ra 1 nghĩa là server đã bỏ qua
# phần gửi kèm và chấm trên bản cũ — học sinh mất bài mà vẫn nhận về 200 kèm một con điểm.
expect_status "nộp với seq thấp vẫn phải ăn đáp án gửi kèm → 200" 200   "$(call POST "/api/exam-attempts/$SEQ_ATTEMPT/submit" "$STUDENT_A" "{\"clientSeq\":1,\"answers\":[{\"questionId\":$SEQ_Q0,\"answerId\":$SEQ_A1}]}")"
expect_json   "  chấm trên đáp án vừa gửi (sai) nên correctCount = 0" correctCount 0

summary
