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
# Ba buoc, vi lop khong con la thu giao vien tu tao:
#   1. Quan tri vien lap lop  -> lop ra doi o dang NHAP
#   2. Phan cong giao vien    -> nguoi day moi nhin thay va giao bai duoc
#   3. Cong bo                -> truoc buoc nay giao vien khong thay gi ca
#
# Bo bat ky buoc nao thi giao vien nhan 403 hoac danh sach rong, va script goi no se hong o mot
# cho khac han nguyen nhan that su.
new_class() {
  local admin="$1" teacher="$2" name="$3" description="${4:-}"

  local code
  code=$(call POST /api/student-groups "$admin" "{\"name\":\"$name\",\"description\":\"$description\"}")
  if [[ "$code" != "200" ]]; then
    echo "  new_class: tao lop that bai HTTP $code — $(body)" >&2
    return 1
  fi

  local group_id teacher_id
  group_id=$(json_field id)

  # Danh tinh giao vien lay tu chinh token cua ho, khong hard-code.
  call GET /api/auth/me "$teacher" > /dev/null
  teacher_id=$(json_field id)

  code=$(call POST "/api/student-groups/$group_id/teachers" "$admin" "{\"teacherIds\":[$teacher_id]}")
  if [[ "$code" != "200" ]]; then
    echo "  new_class: phan cong giao vien that bai HTTP $code — $(body)" >&2
    return 1
  fi

  code=$(call POST "/api/student-groups/$group_id/publish" "$admin")
  if [[ "$code" != "200" ]]; then
    echo "  new_class: cong bo that bai HTTP $code — $(body)" >&2
    return 1
  fi

  echo "$group_id"
}
