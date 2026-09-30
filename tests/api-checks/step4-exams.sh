#!/usr/bin/env bash
# Bước 4 — Bài kiểm tra và blueprint (§4.6).
#
# Chạy:  API phải đang chạy ở $HOST (mặc định http://localhost:5002)
#        bash tests/api-checks/step4-exams.sh
#
# Script tự dựng vài câu hỏi làm dữ liệu nền rồi tự dọn, nên chạy lại bao nhiêu lần cũng được
# và không để lại rác trong database.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

CREATED_QUESTIONS=()
CREATED_EXAMS=()

cleanup() {
  section "Dọn dữ liệu"
  for id in "${CREATED_EXAMS[@]}";     do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  for id in "${CREATED_QUESTIONS[@]}"; do call DELETE "/api/questions/$id" "$TEACHER" > /dev/null; done
  printf '  đã xoá %d đề, %d câu hỏi\n' "${#CREATED_EXAMS[@]}" "${#CREATED_QUESTIONS[@]}"
}
trap cleanup EXIT

# ── Dữ liệu nền ───────────────────────────────────────────────────────────────
# 4 câu Dễ, 3 Trung bình, 2 Khó. Cỡ nhỏ là cố ý: nhờ vậy kiểm được cả nhánh
# "ngân hàng không đủ câu" mà không cần dựng hàng trăm bản ghi.
seed_question() {
  local difficulty="$1" index="$2"
  local payload
  payload=$(cat <<JSON
{
  "content": "Cau kiem thu $difficulty so $index",
  "topic": "KiemThuTuDong",
  "difficulty": "$difficulty",
  "defaultPoint": 1,
  "answers": [
    { "content": "Dap an dung", "isCorrect": true },
    { "content": "Dap an sai",  "isCorrect": false }
  ]
}
JSON
)
  local code
  code=$(call POST /api/questions "$TEACHER" "$payload")
  [[ "$code" != "200" ]] && { echo "Khong tao duoc cau hoi: HTTP $code — $BODY" >&2; exit 1; }
  CREATED_QUESTIONS+=("$(json_field id)")
}

section "Dựng dữ liệu nền — 4 Dễ, 3 Trung bình, 2 Khó"
for i in 1 2 3 4; do seed_question easy "$i"; done
for i in 1 2 3;   do seed_question medium "$i"; done
for i in 1 2;     do seed_question hard "$i"; done
printf '  đã tạo %d câu hỏi\n' "${#CREATED_QUESTIONS[@]}"

# ── Blueprint: backend tự tính số câu ─────────────────────────────────────────
section "Blueprint — backend tính, client chỉ gửi tỷ lệ"

BP='{"totalQuestions":9,"easyPercentage":45,"mediumPercentage":33,"hardPercentage":22,"topic":"KiemThuTuDong"}'
expect_status "xem trước blueprint hợp lệ" 200 "$(call POST /api/exams/blueprint-preview "$TEACHER" "$BP")"
# 45% của 9 = 4.05 → 4 · 33% của 9 = 2.97 → 3 · Khó nhận phần dư = 2
expect_json   "  required.easy = 4"   required.easy   4
expect_json   "  required.medium = 3" required.medium 3
expect_json   "  required.hard = 2"   required.hard   2
expect_json   "  available.easy = 4"  available.easy  4
expect_json   "  isValid = true"      isValid         true

# Mốc .5 — chỗ .NET và JavaScript dễ lệch nhau nhất.
# 5% của 50 = 2.5 → làm tròn chẵn cho 2, JavaScript cho 3. Phải ra 3.
BP_HALF='{"totalQuestions":50,"easyPercentage":5,"mediumPercentage":5,"hardPercentage":90,"topic":"KiemThuTuDong"}'
expect_status "mốc làm tròn .5" 200 "$(call POST /api/exams/blueprint-preview "$TEACHER" "$BP_HALF")"
expect_json   "  required.easy = 3 (không phải 2)" required.easy 3
expect_json   "  required.medium = 3"              required.medium 3
expect_json   "  required.hard = 44"               required.hard 44

# Tổng tỷ lệ khác 100 → vẫn 200 vì đây là endpoint xem trước, nhưng isValid = false.
BP_BAD='{"totalQuestions":9,"easyPercentage":30,"mediumPercentage":30,"hardPercentage":30,"topic":"KiemThuTuDong"}'
expect_status "tổng tỷ lệ 90% vẫn trả 200" 200 "$(call POST /api/exams/blueprint-preview "$TEACHER" "$BP_BAD")"
expect_json   "  isValid = false"                  isValid false

# Đòi nhiều hơn ngân hàng có.
BP_OVER='{"totalQuestions":40,"easyPercentage":100,"mediumPercentage":0,"hardPercentage":0,"topic":"KiemThuTuDong"}'
expect_status "đòi 40 câu Dễ khi chỉ có 4" 200 "$(call POST /api/exams/blueprint-preview "$TEACHER" "$BP_OVER")"
expect_json   "  isValid = false"                  isValid false

expect_status "học sinh gọi blueprint-preview → 403" 403 "$(call POST /api/exams/blueprint-preview "$STUDENT" "$BP")"
expect_status "quản trị viên gọi blueprint-preview → 403" 403 "$(call POST /api/exams/blueprint-preview "$ADMIN" "$BP")"

# ── Nháp là trạng thái được miễn trừ ──────────────────────────────────────────
section "Nháp được miễn hai điều kiện nặng"

exam_payload() {
  # Dòng lifecycle dựng riêng ra biến. Viết thẳng ${9:+, "lifecycle": "$9"} trong heredoc thì bash
  # coi hai dấu nháy kép bên trong là cú pháp trích dẫn của chính phép khai triển và nuốt mất
  # chúng, sinh ra `lifecycle: draft` không có nháy — JSON hỏng.
  local lifecycle_line=""
  [[ -n "${9:-}" ]] && lifecycle_line=$(printf ',
  "lifecycle": "%s"' "$9")

  cat <<JSON
{
  "title": "$1",
  "description": "Sinh boi kiem thu tu dong",
  "topic": "KiemThuTuDong",
  "durationMinutes": ${2:-45},
  "totalQuestions": ${3:-9},
  "easyPercentage": ${4:-45},
  "mediumPercentage": ${5:-33},
  "hardPercentage": ${6:-22},
  "pointPerQuestion": 1,
  "openAt": "${7:-2026-12-01T01:00:00Z}",
  "closeAt": "${8:-2026-12-01T05:00:00Z}",
  "shuffleQuestions": true,
  "shuffleAnswers": true,
  "showResultAfterSubmit": true$lifecycle_line
}
JSON
}

# Tỷ lệ mới cộng 90%, ngân hàng cũng không đủ — nhưng là nháp nên lưu được.
DRAFT=$(exam_payload "Nhap chua chot ty le" 45 40 30 30 30 "" "" draft)
expect_status "lưu nháp với tỷ lệ 90% và ngân hàng thiếu → 200" 200 "$(call POST /api/exams "$TEACHER" "$DRAFT")"
DRAFT_ID=$(json_field id)
CREATED_EXAMS+=("$DRAFT_ID")
expect_json   "  lifecycle = draft"      lifecycle draft
expect_json   "  assignedCount = 0"      assignedCount 0

# Cùng cấu hình đó mà không phải nháp → bị chặn.
NOT_DRAFT=$(exam_payload "De khong hop le" 45 40 30 30 30)
expect_status "cùng cấu hình nhưng không nháp → 400" 400 "$(call POST /api/exams "$TEACHER" "$NOT_DRAFT")"

section "Hình dạng request"
expect_status "thời lượng 90 phút trong cửa sổ 30 phút → 400" 400 \
  "$(call POST /api/exams "$TEACHER" "$(exam_payload "Cua so ngan" 90 9 45 33 22 2026-12-01T01:00:00Z 2026-12-01T01:30:00Z draft)")"
expect_status "lifecycle dẫn xuất 'open' gửi lên → 400" 400 \
  "$(call POST /api/exams "$TEACHER" "$(exam_payload "Ep trang thai" 45 9 45 33 22 "" "" open)")"
expect_status "closeAt trước openAt → 400" 400 \
  "$(call POST /api/exams "$TEACHER" "$(exam_payload "Cua so nguoc" 45 9 45 33 22 2026-12-01T05:00:00Z 2026-12-01T01:00:00Z draft)")"

section "Danh sách và bộ lọc"
expect_status "lọc lifecycle=draft" 200 "$(call GET "/api/exams?lifecycle=draft" "$TEACHER")"
expect_status "lifecycle=42 → 400 (Enum.IsDefined)" 400 "$(call GET "/api/exams?lifecycle=42" "$TEACHER")"
expect_status "lifecycle=published → 400" 400 "$(call GET "/api/exams?lifecycle=published" "$TEACHER")"
expect_status "lifecycle=all → 200" 200 "$(call GET "/api/exams?lifecycle=all" "$TEACHER")"
expect_status "pageSize=500 bị kẹp, không báo lỗi" 200 "$(call GET "/api/exams?pageSize=500" "$TEACHER")"
expect_json   "  pageSize kẹp về 200" pageSize 200

section "Dời lịch — phép kiểm validator không làm hộ được"
expect_status "cửa sổ ngắn hơn thời lượng đề → 400" 400 \
  "$(call PATCH "/api/exams/$DRAFT_ID/schedule" "$TEACHER" '{"openAt":"2026-12-02T01:00:00Z","closeAt":"2026-12-02T01:20:00Z"}')"
expect_status "cửa sổ hợp lệ → 200" 200 \
  "$(call PATCH "/api/exams/$DRAFT_ID/schedule" "$TEACHER" '{"openAt":"2026-12-02T01:00:00Z","closeAt":"2026-12-02T05:00:00Z"}')"

section "Xuất bản — hai điều kiện được miễn phải trả lại đủ"
expect_status "xuất bản nháp còn thiếu điều kiện → 400" 400 "$(call POST "/api/exams/$DRAFT_ID/publish" "$TEACHER")"

# Sửa cho hợp lệ: 9 câu theo 45/33/22 thì cần 4 Dễ, 3 TB, 2 Khó — vừa khít ngân hàng.
expect_status "sửa đề cho hợp lệ → 200" 200 \
  "$(call PUT "/api/exams/$DRAFT_ID" "$TEACHER" "$(exam_payload "De da chot" 45 9 45 33 22 2026-12-02T01:00:00Z 2026-12-02T05:00:00Z draft)")"
expect_status "xuất bản → 200" 200 "$(call POST "/api/exams/$DRAFT_ID/publish" "$TEACHER")"
expect_json   "  lifecycle chuyển sang scheduled" lifecycle scheduled
expect_status "xuất bản lần hai → 409" 409 "$(call POST "/api/exams/$DRAFT_ID/publish" "$TEACHER")"

section "Vòng đời là dữ liệu dẫn xuất, tính lúc đọc"
# Đề mở từ quá khứ tới tương lai phải tự hiện là 'open' mà không ai gọi endpoint nào.
OPEN_NOW=$(exam_payload "De dang mo" 45 9 45 33 22 2020-01-01T00:00:00Z 2035-01-01T00:00:00Z)
expect_status "tạo đề có lịch bao trùm hiện tại → 200" 200 "$(call POST /api/exams "$TEACHER" "$OPEN_NOW")"
OPEN_ID=$(json_field id); CREATED_EXAMS+=("$OPEN_ID")
expect_json   "  lifecycle = open (không cần gọi gì thêm)" lifecycle open

PAST=$(exam_payload "De da dong" 45 9 45 33 22 2020-01-01T00:00:00Z 2020-01-02T00:00:00Z)
expect_status "tạo đề có lịch đã qua → 200" 200 "$(call POST /api/exams "$TEACHER" "$PAST")"
PAST_ID=$(json_field id); CREATED_EXAMS+=("$PAST_ID")
expect_json   "  lifecycle = closed" lifecycle closed

section "Phân quyền và phạm vi"
expect_status "quản trị viên ĐỌC được đề của giáo viên → 200" 200 "$(call GET "/api/exams/$DRAFT_ID" "$ADMIN")"
expect_status "quản trị viên tạo đề → 403" 403 "$(call POST /api/exams "$ADMIN" "$(exam_payload "Admin ra de")")"
expect_status "quản trị viên sửa đề → 403" 403 "$(call PUT "/api/exams/$DRAFT_ID" "$ADMIN" "$(exam_payload "Admin sua")")"
expect_status "quản trị viên xoá đề → 403" 403 "$(call DELETE "/api/exams/$DRAFT_ID" "$ADMIN")"
expect_status "học sinh xem danh sách đề → 403" 403 "$(call GET /api/exams "$STUDENT")"
expect_status "học sinh xem chi tiết đề → 403" 403 "$(call GET "/api/exams/$DRAFT_ID" "$STUDENT")"
expect_status "gọi không token → 401" 401 "$(call GET /api/exams "")"
expect_status "đề không tồn tại → 404" 404 "$(call GET /api/exams/999999 "$TEACHER")"
expect_status "blueprint của đề không tồn tại → 404" 404 "$(call GET /api/exams/999999/blueprint "$TEACHER")"

section "Xoá"
expect_status "xoá đề chưa ai nộp → 200" 200 "$(call DELETE "/api/exams/$PAST_ID" "$TEACHER")"
CREATED_EXAMS=("${CREATED_EXAMS[@]/$PAST_ID}")
expect_status "xoá lại chính đề đó → 404" 404 "$(call DELETE "/api/exams/$PAST_ID" "$TEACHER")"

summary
