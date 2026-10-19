#!/usr/bin/env bash
# Khung kiểm chứng HTTP dùng chung cho các bộ kiểm thử API.
#
# Vì sao tồn tại: unit test chứng minh được logic thuần, nhưng không chạm tới thứ chỉ xuất hiện
# khi có HTTP thật — mã trạng thái, phân quyền theo vai trò, transaction. Bộ kiểm của bước 2 và 3
# từng được chạy tay rồi mất, không để lại gì; những gì ở đây thì chạy lại được.

set -uo pipefail

HOST="${HOST:-http://localhost:5002}"
PASSED=0
FAILED=0
FAILURES=()

# Gọi API: in mã trạng thái ra stdout, ghi phần thân vào $BODY_FILE.
#
# Phần thân đi qua FILE chứ không qua biến, vì call() gần như luôn được gọi trong $( ) — tức là
# trong một subshell, nơi mọi phép gán biến đều mất khi subshell kết thúc. Một file thì sống sót.
BODY_FILE="${BODY_FILE:-$(mktemp -t anthill-body.XXXXXX)}"
body() { cat "$BODY_FILE" 2>/dev/null; }
call() {
  local method="$1" path="$2" token="${3:-}" payload="${4:-}"
  local args=(-s -o "$BODY_FILE" -w '%{http_code}' -X "$method" "$HOST$path")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  if [[ -n "$payload" ]]; then
    args+=(-H 'Content-Type: application/json' -d "$payload")
  fi
  curl "${args[@]}"
}

# Khẳng định mã trạng thái. Dùng cho mọi ca kiểm phân quyền và xung đột.
expect_status() {
  local label="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then
    PASSED=$((PASSED + 1))
    printf '  \033[32mOK\033[0m   %-58s %s\n' "$label" "$actual"
  else
    FAILED=$((FAILED + 1))
    FAILURES+=("$label — chờ $expected, nhận $actual · $(body | head -c 180)")
    printf '  \033[31mFAIL\033[0m %-58s chờ %s, nhận %s\n' "$label" "$expected" "$actual"
  fi
}

# Khẳng định một giá trị trong thân JSON, đọc bằng đường dẫn kiểu a.b.c
expect_json() {
  local label="$1" path="$2" expected="$3"
  local actual
  actual=$(python -c "
import json,sys
try:
    d = json.loads(sys.stdin.read())
    for key in '$path'.split('.'):
        d = d[int(key)] if key.isdigit() else d[key]
    print(json.dumps(d) if isinstance(d,(dict,list,bool)) else d)
except Exception as exc:
    print('<khong doc duoc: %s>' % exc)
" < "$BODY_FILE")
  if [[ "$actual" == "$expected" ]]; then
    PASSED=$((PASSED + 1))
    printf '  \033[32mOK\033[0m   %-58s %s\n' "$label" "$actual"
  else
    FAILED=$((FAILED + 1))
    FAILURES+=("$label — chờ $expected, nhận $actual")
    printf '  \033[31mFAIL\033[0m %-58s chờ %s, nhận %s\n' "$label" "$expected" "$actual"
  fi
}

json_field() {
  python -c "
import json,sys
try:
    d = json.loads(sys.stdin.read())
    for key in '$1'.split('.'):
        d = d[int(key)] if key.isdigit() else d[key]
    print(d)
except Exception:
    print('')
" < "$BODY_FILE"
}

# Doi duong dan sang dang ma curl hieu duoc.
#
# curl tren Git Bash la ban dung cho Windows (mingw64), nen tham so -F file=@... phai la duong dan
# Windows that. Dua vao mot duong dan kieu /tmp/... thi no khong mo duoc file va tra ve HTTP 000 —
# im lang, khong bao loi gi, rat de tuong la server hong.
to_native_path() {
  if command -v cygpath > /dev/null 2>&1; then
    cygpath -w "$1"
  else
    printf '%s' "$1"
  fi
}

login() {
  local code
  code=$(call POST /api/auth/login "" "{\"username\":\"$1\",\"password\":\"$2\"}")
  [[ "$code" != "200" ]] && { echo "Khong dang nhap duoc $1 (HTTP $code)" >&2; exit 1; }
  json_field accessToken
}

# Dang nhap bang mat khau tam (MustChangePassword=true — tao tai khoan hoac cap lai mat khau deu
# dat co nay), doi ngay sang mat khau moi, roi dang nhap lai. Tra ve token cuoi cung: con hieu luc
# VA khong con bi MustChangePasswordMiddleware han che (Sec.5) — dung cho moi noi chi can mot token
# hoc sinh lam duoc viec nghiep vu, khong tu kiem ban than co MustChangePassword.
#
#   $1 username, $2 mat khau tam dang co, $3 mat khau moi
login_and_clear_must_change() {
  local username="$1" temp_password="$2" new_password="$3"
  local temp_token code
  temp_token=$(login "$username" "$temp_password")
  code=$(call POST /api/auth/change-password "$temp_token" \
    "{\"currentPassword\":\"$temp_password\",\"newPassword\":\"$new_password\"}")
  [[ "$code" != "200" ]] && {
    echo "Khong doi duoc mat khau tam cho $username (HTTP $code) — $(body)" >&2
    exit 1
  }
  login "$username" "$new_password"
}

# Khoa tai khoan va go khoi lop deu BAT BUOC co ly do (10-512 ky tu). Cac buoc don dep cua bo kiem dung chung hai than
# request nay de khong phai lap lai ly do o tung script.
LOCK_BODY='{"status":"locked","reason":"Don dep du lieu kiem thu tu dong"}'
REMOVE_BODY='{"reason":"Don dep du lieu kiem thu tu dong"}'

section() { printf '\n\033[1m%s\033[0m\n' "$1"; }

summary() {
  printf '\n────────────────────────────────────────────────────────────────\n'
  if [[ $FAILED -eq 0 ]]; then
    printf '\033[32mĐạt toàn bộ %d phép.\033[0m\n' "$PASSED"
  else
    printf '\033[31m%d hỏng\033[0m / %d phép:\n' "$FAILED" "$((PASSED + FAILED))"
    for failure in "${FAILURES[@]}"; do printf '  · %s\n' "$failure"; done
  fi
  return $((FAILED > 0))
}

# Tao mot lop DUNG THEO MO HINH MOI va tra ve id.
#
#   $1 admin token, $2 teacher token, $3 ten lop, $4 (tuy chon) mo ta
#
# Hai buoc, vi lop khong con la thu giao vien tu tao:
#   1. Quan tri vien lap lop KEM giao vien phu trach (moi lop co dung mot giao vien, chon ngay luc lap) -> lop ra doi
#      o dang NHAP
#   2. Cong bo                -> truoc buoc nay giao vien khong thay gi ca
#
# Bo buoc nao thi giao vien nhan 403 hoac danh sach rong, va script goi no se hong o mot cho khac han nguyen nhan
# that su.
new_class() {
  local admin="$1" teacher="$2" name="$3" description="${4:-}"

  # Danh tinh giao vien lay tu chinh token cua ho, khong hard-code.
  local teacher_id
  call GET /api/auth/me "$teacher" > /dev/null
  teacher_id=$(json_field id)

  local code
  code=$(call POST /api/student-groups "$admin" "{\"name\":\"$name\",\"description\":\"$description\",\"teacherId\":$teacher_id}")
  if [[ "$code" != "200" ]]; then
    echo "  new_class: tao lop that bai HTTP $code — $(body)" >&2
    return 1
  fi

  local group_id
  group_id=$(json_field id)

  code=$(call POST "/api/student-groups/$group_id/publish" "$admin")
  if [[ "$code" != "200" ]]; then
    echo "  new_class: cong bo that bai HTTP $code — $(body)" >&2
    return 1
  fi

  echo "$group_id"
}

# Id cua giao vien demo (an.nv). Moi lop co dung mot giao vien, chon ngay luc lap lop, nen moi script lap lop truc tiep
# deu can no. Doc mot lan luc nap thu vien (khong trong $( ) cua tung phep kiem) de khoi dang nhap lai moi lan.
_demo_teacher_id() {
  local token
  token=$(curl -s -X POST "$HOST/api/auth/login" -H 'Content-Type: application/json' \
    -d '{"username":"an.nv@anthill.edu.vn","password":"123456"}' \
    | python -c 'import json,sys;print(json.load(sys.stdin)["accessToken"])') || return 1
  curl -s "$HOST/api/auth/me" -H "Authorization: Bearer $token" \
    | python -c 'import json,sys;print(json.load(sys.stdin)["id"])'
}
DEMO_TEACHER_ID=$(_demo_teacher_id 2>/dev/null || true)

# Tao mot giao vien MOI bang API (khong can psql), cung mon voi giao vien demo. In "id|email". Mat khau tam la 123456 va
# phai doi o lan dang nhap dau — dung login_and_clear_must_change neu can token dung duoc.
#
#   $1 admin token, $2 hau to duy nhat (vd "${STAMP}a")
#
# Giao vien khong xoa duoc (so huu cau hoi, de), nen script dung xong thi goi retire_teacher de khoa.
new_teacher() {
  local admin="$1" suffix="$2" code subject
  call GET "/api/teachers/$DEMO_TEACHER_ID" "$admin" > /dev/null
  subject=$(json_field subjectId)
  code=$(call POST /api/teachers "$admin" \
    "{\"fullName\":\"GV kiem thu $suffix\",\"email\":\"gv$suffix@kiemthu.vn\",\"phone\":\"0912345678\",\"subjectId\":$subject,\"password\":\"123456\"}")
  if [[ "$code" != "200" ]]; then
    echo "  new_teacher: HTTP $code — $(body)" >&2
    return 1
  fi
  echo "$(json_field id)|gv$suffix@kiemthu.vn"
}

# Khoa mot giao vien do new_teacher tao ra (kem ly do) — don dep toi da co the vi khong co API xoa giao vien.
retire_teacher() {
  call PATCH "/api/teachers/$2/status" "$1" '{"status":"locked","reason":"Don dep du lieu kiem thu tu dong"}' > /dev/null
}
