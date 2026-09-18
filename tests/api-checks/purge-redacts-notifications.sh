#!/usr/bin/env bash
# H22 — sau khi xoá tài khoản học sinh, thông báo nộp bài cũ của giáo viên không được giữ tên thật.
#
# Bài này chạm vào PurgeDeletedStudentsService — job chỉ quét LÚC KHỞI ĐỘNG tiến trình API và mỗi 6
# tiếng sau đó (cố ý chậm, vì ẩn danh hoá không hoàn tác được — xem remarks của chính service đó).
# Không có đường HTTP nào kích nó chạy ngay, nên — giống deadline-reminders.sh/schedule-changes.sh —
# bài này KHÔNG nằm trong KIT_ALL_SUITES và chạy theo HAI bước, xen giữa là khởi động lại API:
#
#   bash tests/api-checks/purge-redacts-notifications.sh setup     # API đang chạy ở $HOST
#   ... khởi động lại API (vd: kit_api_stop 5155 && kit_api_start ... 5155 <db>) ...
#   bash tests/api-checks/purge-redacts-notifications.sh verify    # API vừa khởi động lại ở $HOST
#
# Cần psql để lùi mốc DeletionRequestedAt qua hạn 60 ngày mà không phải chờ thật — xem
# exam-edit-rules.sh cho cùng kỹ thuật lấy thông tin kết nối (PGHOST/... nếu có, không thì đọc
# user-secrets của Anthill.Api).

source "$(dirname "$0")/lib.sh"

# Console Windows mặc định không phải UTF-8; không đặt biến này thì python in tiếng Việt ra qua
# $(...) (không phải terminal thật, nhưng vẫn theo mã trang mbcs của Windows) ném UnicodeEncodeError.
export PYTHONIOENCODING=utf-8

MODE="${1:?dùng: $0 setup|verify}"
case "$MODE" in
  setup|verify) ;;
  *) echo "dùng: $0 setup|verify" >&2; exit 1 ;;
esac
# Đường dẫn của chính repo này có dấu tiếng Việt (D:\Cá nhân\...) — đẩy thẳng chuỗi đó vào một script
# Python chạy qua subprocess của Git Bash trên Windows từng làm hỏng byte (mã trang ANSI khi truyền
# argv), nên trạng thái giữa hai bước "setup"/"verify" nằm ở thư mục tạm hệ thống, không dấu — cùng
# thư mục mktemp đã dùng cho BODY_FILE/HELPERS ở trên, chỉ khác là tên cố định để bước "verify" tìm lại được.
#
# ĐÃ ĐỔI SANG ĐƯỜNG DẪN WINDOWS THẬT (to_native_path) chứ không giữ dạng /tmp/...: python.exe trên
# Windows không hiểu cách MSYS/Git Bash ánh xạ /tmp sang thư mục tạm thật — nó đọc "/tmp/x" như một
# đường dẫn tương đối với gốc Ổ ĐĨA HIỆN TẠI, nên "/tmp/anthill....json" từng bị ghi nhầm vào
# "D:\tmp\anthill....json" (Ổ D vì cwd của script nằm ở D:) thay vì thư mục tạm hệ thống thật — bash
# thì lại tìm đúng /tmp ánh xạ tới C:\Users\...\AppData\Local\Temp, nên "verify" không bao giờ thấy
# file "setup" vừa ghi. Đường dẫn Windows thật (có ổ đĩa) thì cả hai bên hiểu giống nhau.
_STATE_PROBE=$(mktemp -t anthill-purge-state-dir.XXXXXX)
STATE_FILE="$(to_native_path "$(dirname "$_STATE_PROBE")/anthill-purge-redaction-state.json")"
rm -f "$_STATE_PROBE"

fail_fast() { echo "  Dựng dữ liệu nền thất bại: $1" >&2; exit 1; }

ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")
TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")

# ── psql ────────────────────────────────────────────────────────────────────────────────────────
PSQL=$(command -v psql 2>/dev/null || ls /c/Program\ Files/PostgreSQL/*/bin/psql.exe 2>/dev/null | tail -n 1)
CONN=""
if [[ -z "${PGPASSWORD:-}" ]]; then
  CONN=$(dotnet user-secrets list --project "$(dirname "$0")/../../src/Anthill.Api" 2>/dev/null \
    | grep -m1 'DefaultConnection' | sed 's/^[^=]*= //')
fi

HELPERS=$(mktemp -t anthill-purge.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
import json
import sys

mode = sys.argv[1]


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


if mode == "conn":
    # Tách chuỗi kết nối kiểu Npgsql thành host, port, db, user, password — mỗi thứ một dòng.
    parts = dict(
        (key.strip().lower(), value.strip())
        for key, _, value in (item.partition("=") for item in sys.argv[2].split(";") if "=" in item)
    )
    for key in ("host", "port", "database", "username", "password"):
        print(parts.get(key, ""))

elif mode == "start_ids":
    # Câu hỏi/đáp án 'Đúng' của câu đầu tiên trong phiên làm bài vừa bắt đầu.
    session = load(sys.argv[2])
    question = session["questions"][0]
    answer = next(a for a in question["answers"] if a["content"] == "Dung")
    print(question["id"])
    print(answer["id"])

elif mode == "find_notification":
    # Thông báo examSubmitted của giáo viên có nhắc tới STAMP — dùng làm dấu vân tay của đợt chạy này
    # thay vì giả định nó luôn nằm ở đầu danh sách (giáo viên có thể có thông báo cũ từ lần chạy khác).
    items = load(sys.argv[2])["items"]
    stamp = sys.argv[3]
    row = next((i for i in items if i["type"] == "examSubmitted" and stamp in i["message"]), None)
    print(json.dumps(row) if row else "")

elif mode == "message_of":
    items = load(sys.argv[2])["items"]
    target_id = int(sys.argv[3])
    row = next((i for i in items if i["id"] == target_id), None)
    print(row["message"] if row else "<khong tim thay>")

elif mode == "field":
    print(json.loads(sys.argv[2])[sys.argv[3]])

elif mode == "write_state":
    # argv: write_state <path> <studentId> <notificationId> <fullName>
    json.dump(
        {"studentId": int(sys.argv[3]), "notificationId": int(sys.argv[4]), "fullName": sys.argv[5]},
        open(sys.argv[2], "w", encoding="utf-8"),
    )

elif mode == "read_state":
    # argv: read_state <path> <field>. Đọc đường dẫn qua argv, không nhúng vào mã nguồn: đường dẫn
    # Windows thật có gạch chéo ngược, nhúng thẳng vào một chuỗi Python thì \U, \A... bị hiểu nhầm
    # thành ký tự thoát (\U đặc biệt nguy hiểm — nó đợi đúng 8 số hex theo sau, "Users..." thì không).
    print(load(sys.argv[2])[sys.argv[3]])
PYHELPER
HELPERS=$(to_native_path "$HELPERS")

if [[ -n "$CONN" ]]; then
  mapfile -t PG < <(python "$HELPERS" conn "$CONN" | tr -d '\r')
  export PGHOST="${PG[0]}" PGPORT="${PG[1]:-5432}" PGDATABASE="${PG[2]}" PGUSER="${PG[3]}" PGPASSWORD="${PG[4]}"
fi
sql() { "$PSQL" -q -At -v ON_ERROR_STOP=1 -c "$1"; }
echo "  (psql -> ${PGDATABASE:-<mac dinh trong connection string>}@${PGHOST:-<mac dinh>}; dat PGHOST/PGDATABASE/... truoc" \
     "khi goi neu muon nham vao mot database khac, vi du: kit_pg_env anthill_t55)" >&2

if [[ "$MODE" == "setup" ]]; then
  [[ -z "$PSQL" ]] && fail_fast "không tìm thấy psql — xem chú thích đầu file để cấp kết nối"

  STAMP=$(date +%H%M%S)
  FULL_NAME="Hoc Sinh Xoa Thu $STAMP"

  section "Dựng dữ liệu nền"
  c=$(call POST /api/questions "$TEACHER" \
    "{\"content\":\"Cau hoi $STAMP\",\"topic\":\"XoaHS$STAMP\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
  QID=$(json_field id)
  printf '  câu hỏi %s\n' "$QID"

  GID=$(new_class "$ADMIN" "$TEACHER" "Xoa HS $STAMP" "Lop kiem tra xoa dau vet thong bao") \
    || fail_fast "khong dung duoc lop nen"

  c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"$FULL_NAME\",\"email\":\"px$STAMP@kiemthu.vn\",\"username\":\"px$STAMP\",\"code\":\"PX$STAMP\",\"password\":\"anthill123\",\"groupId\":$GID}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
  SID=$(json_field id)
  STUDENT=$(login "px$STAMP" "anthill123")
  printf '  học sinh %s (%s) trong lớp %s\n' "$SID" "$FULL_NAME" "$GID"

  c=$(call POST /api/exams "$TEACHER" \
    "{\"title\":\"De xoa HS $STAMP\",\"topic\":\"XoaHS$STAMP\",\"durationMinutes\":45,\"totalQuestions\":1,\"easyPercentage\":100,\"mediumPercentage\":0,\"hardPercentage\":0,\"pointPerQuestion\":1,\"openAt\":\"2020-01-01T00:00:00Z\",\"closeAt\":\"2035-01-01T00:00:00Z\",\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":true}")
  [[ "$c" != "200" ]] && fail_fast "tao de: HTTP $c — $(body)"
  EID=$(json_field id)

  expect_status "giao bài → 200" 200 \
    "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[$SID],\"groupIds\":[],\"notify\":true}")"

  call GET /api/my/exams "$STUDENT" > /dev/null
  AID=$(python -c "
import json, sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next(i['assignmentId'] for i in items if i['examId'] == int(sys.argv[2])))
" "$(to_native_path "$BODY_FILE")" "$EID")

  c=$(call POST /api/exam-attempts "$STUDENT" "{\"assignmentId\":$AID}")
  [[ "$c" != "200" ]] && fail_fast "bat dau lam bai: HTTP $c — $(body)"
  read -r QUESTION_ID ANSWER_ID <<< "$(python "$HELPERS" start_ids "$(to_native_path "$BODY_FILE")" | tr '\n' ' ')"
  ATTID=$(json_field attemptId)

  expect_status "nộp bài → 200" 200 \
    "$(call POST "/api/exam-attempts/$ATTID/submit" "$STUDENT" "{\"answers\":[{\"questionId\":$QUESTION_ID,\"answerId\":$ANSWER_ID}]}")"

  section "Xác nhận giáo viên nhận thông báo có tên thật (trước khi xoá)"
  expect_status "giáo viên xem thông báo → 200" 200 "$(call GET "/api/notifications?pageSize=100" "$TEACHER")"
  ROW=$(python "$HELPERS" find_notification "$(to_native_path "$BODY_FILE")" "$STAMP")
  [[ -z "$ROW" ]] && fail_fast "không tìm thấy thông báo nộp bài của giáo viên"
  NOTIF_ID=$(python "$HELPERS" field "$ROW" id)
  MSG_BEFORE=$(python "$HELPERS" field "$ROW" message)
  expect_status "  message chứa tên thật" true \
    "$(python -c "print(str('''$FULL_NAME''' in '''$MSG_BEFORE''').lower())")"

  section "Yêu cầu xoá và lùi mốc 61 ngày qua psql"
  expect_status "yêu cầu xoá → 200" 200 \
    "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"Kiem tra xoa dau vet trong thong bao."}')"
  sql "UPDATE \"Users\" SET \"DeletionRequestedAt\" = now() - interval '61 days' WHERE \"Id\" = $SID;" > /dev/null

  python "$HELPERS" write_state "$STATE_FILE" "$SID" "$NOTIF_ID" "$FULL_NAME"
  printf '\n  setup xong. Khởi động lại API rồi chạy: bash %s verify\n' "$0"
  rm -f "$HELPERS"
  exit 0
fi

if [[ "$MODE" == "verify" ]]; then
  [[ -f "$STATE_FILE" ]] || fail_fast "không thấy $STATE_FILE — chạy 'setup' trước, rồi khởi động lại API"
  SID=$(python "$HELPERS" read_state "$STATE_FILE" studentId)
  NOTIF_ID=$(python "$HELPERS" read_state "$STATE_FILE" notificationId)
  FULL_NAME=$(python "$HELPERS" read_state "$STATE_FILE" fullName)

  section "Tài khoản đã bị ẩn danh hoá"
  expect_status "admin xem hồ sơ → 200" 200 "$(call GET "/api/students/$SID" "$ADMIN")"
  expect_json   "  status = deleted" status deleted

  section "Thông báo cũ của giáo viên không còn tên thật"
  expect_status "giáo viên xem thông báo → 200" 200 "$(call GET "/api/notifications?pageSize=100" "$TEACHER")"
  MSG_AFTER=$(python "$HELPERS" message_of "$(to_native_path "$BODY_FILE")" "$NOTIF_ID")

  expect_status "  không còn tên thật" true \
    "$(python -c "print(str('''$FULL_NAME''' not in '''$MSG_AFTER''').lower())")"
  expect_status "  đã đổi sang tên ẩn danh" true \
    "$(python -c "print(str('Học sinh đã xoá' in '''$MSG_AFTER''').lower())")"
  expect_status "  vẫn còn phần nội dung còn lại (không bị xoá trắng)" true \
    "$(python -c "print(str('vừa nộp bài' in '''$MSG_AFTER''').lower())")"

  rm -f "$STATE_FILE" "$HELPERS"
  summary
fi
