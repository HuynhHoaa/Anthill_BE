#!/usr/bin/env bash
# Token cu chet tren MOI endpoint ngay khi tai khoan doi trang thai (§5).
#
# Chay:  bash tests/api-checks/session-revocation.sh   (API phai dang chay o $HOST)
#
# Truoc ban sua nay, security_stamp va trang thai tai khoan chi duoc doi chieu o /api/auth/me,
# change-password va luc mo ket noi hub. Moi endpoint khac — lam bai, ket qua, thong bao — van nhan
# token cu cua mot em vua bi khoa hay vua dua vao dien xoa cho toi khi token het han (120 phut),
# trong khi hop thoai khoa tai khoan hua voi quan tri vien la em do bi dang xuat ngay.
#
# Ma mong doi:
#   - endpoint nghiep vu va hub: 401 cho MOI token cu. 401 la tin hieu duy nhat khien frontend dua
#     nguoi dung ve trang dang nhap; 403 chi hien mot thong bao roi de em ngoi tiep trong giao dien.
#   - /api/auth/me: giu hop dong cu — 403 kem ly do khi tai khoan bi khoa hay cho xoa (step2 va
#     students-deletion canh dieu nay), 401 khi chi stamp da xoay.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")
TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")

# Co ca ngay trong nam: hoc sinh cua lan chay truoc con nam lai (chi khoa, khong xoa duoc).
STAMP=$(date +%j%H%M%S)
USERNAME="sr$STAMP"
GID=""; SID=""

LOCKED_MSG="Tài khoản đã bị khoá, liên hệ quản trị viên."
PENDING_MSG="Tài khoản đang chờ xoá theo yêu cầu của quản trị viên. Liên hệ nhà trường nếu đây là nhầm lẫn."
CHANGED_MSG="Phiên đăng nhập đã kết thúc vì tài khoản vừa thay đổi (đổi mật khẩu, cấp lại mật khẩu hoặc bị khoá). Vui lòng đăng nhập lại."
EXPIRED_MSG="Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."

cleanup() {
  section "Don du lieu"
  if [[ -n "$SID" ]]; then
    # Tai khoan cho xoa khong khoa duoc, nen khoi phuc truoc (409 neu dang hoat dong — vo hai).
    call POST "/api/students/$SID/restore" "$ADMIN" > /dev/null
    [[ -n "$GID" ]] && call DELETE "/api/student-groups/$GID/students/$SID" "$ADMIN" > /dev/null
    call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"locked"}' > /dev/null
  fi
  [[ -n "$GID" ]] && call DELETE "/api/student-groups/$GID" "$ADMIN" > /dev/null
  printf '  da don (hoc sinh %s de lai o trang thai khoa — khong xoa duoc bang API, dung thiet ke)\n' "${SID:-?}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

# Hub nhan token qua query access_token — dung duong ma trinh duyet dung.
negotiate() { call POST "/hubs/notifications/negotiate?negotiateVersion=1&access_token=$1" ""; }

# Token con song tren ba endpoint nghiep vu va hub.
token_alive() {
  local label="$1" token="$2"
  expect_status "$label: bai duoc giao → 200" 200 "$(call GET /api/my/exams "$token")"
  expect_status "$label: ket qua → 200" 200 "$(call GET /api/my/results "$token")"
  expect_status "$label: thong bao → 200" 200 "$(call GET /api/notifications "$token")"
  expect_status "$label: negotiate hub → 200" 200 "$(negotiate "$token")"
}

# Token da chet tren ba endpoint nghiep vu va hub, kem cau chu dung ly do.
token_dead() {
  local label="$1" token="$2" message="$3"
  expect_status "$label: bai duoc giao → 401" 401 "$(call GET /api/my/exams "$token")"
  expect_json   "  noi dung ly do" message "$message"
  expect_status "$label: ket qua → 401" 401 "$(call GET /api/my/results "$token")"
  expect_status "$label: thong bao → 401" 401 "$(call GET /api/notifications "$token")"
  expect_json   "  noi dung ly do" message "$message"
  expect_status "$label: negotiate hub → 401" 401 "$(negotiate "$token")"
}

# Token cua giao vien va quan tri vien khong bi anh huong boi thay doi cua hoc sinh.
staff_unaffected() {
  expect_status "giao vien: thong bao → 200" 200 "$(call GET /api/notifications "$TEACHER")"
  expect_status "giao vien: /api/auth/me → 200" 200 "$(call GET /api/auth/me "$TEACHER")"
  expect_status "giao vien: negotiate hub → 200" 200 "$(negotiate "$TEACHER")"
  expect_status "quan tri vien: danh sach hoc sinh → 200" 200 "$(call GET "/api/students?pageSize=1" "$ADMIN")"
  expect_status "quan tri vien: thong bao → 200" 200 "$(call GET /api/notifications "$ADMIN")"
}

student_login() {
  call POST /api/auth/login "" "{\"username\":\"$USERNAME\",\"password\":\"$1\"}"
}

section "Dung du lieu nen"
GID=$(new_class "$ADMIN" "$TEACHER" "Phien$STAMP") || fail_fast "tao lop"
code=$(call POST /api/students "$ADMIN" \
  "{\"fullName\":\"HS phien $STAMP\",\"email\":\"$USERNAME@kiemthu.vn\",\"username\":\"$USERNAME\",\"code\":\"SR$STAMP\",\"password\":\"anthill123\"}")
[[ "$code" != "200" ]] && fail_fast "tao hoc sinh: HTTP $code — $(body)"
SID=$(json_field id)
code=$(call POST "/api/student-groups/$GID/students" "$ADMIN" "{\"studentIds\":[$SID]}")
[[ "$code" != "200" ]] && fail_fast "xep vao lop: HTTP $code — $(body)"
printf '  lop %s · hoc sinh %s (%s)\n' "$GID" "$SID" "$USERNAME"

section "Token vua cap dung duoc ca ngoai /api/auth"
T0=$(login "$USERNAME" "anthill123")
token_alive "token moi" "$T0"
expect_status "khong token: bai duoc giao → 401" 401 "$(call GET /api/my/exams "")"
expect_json   "  van la cau het han chung" message "$EXPIRED_MSG"

section "Khoa tai khoan"
expect_status "khoa → 200" 200 "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"locked"}')"
token_dead "token cu" "$T0" "$LOCKED_MSG"
# Request mo ket noi WebSocket di qua cung buoc xac thuc; bi chan truoc khi SignalR tra id.
expect_status "token cu: mo ket noi hub → 401" 401 \
  "$(call GET "/hubs/notifications?id=khongco&access_token=$T0" "")"
expect_status "token cu: /api/auth/me van tra 403 kem ly do" 403 "$(call GET /api/auth/me "$T0")"
expect_json   "  noi ro la bi khoa" message "$LOCKED_MSG"
expect_status "dang nhap lai → 403" 403 "$(student_login anthill123)"
staff_unaffected

section "Mo khoa: token cu van chet, dang nhap moi thi dung duoc"
expect_status "mo khoa → 200" 200 "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"active"}')"
token_dead "token truoc khi khoa" "$T0" "$CHANGED_MSG"
expect_status "token truoc khi khoa: /api/auth/me → 401" 401 "$(call GET /api/auth/me "$T0")"
T1=$(login "$USERNAME" "anthill123")
token_alive "dang nhap sau mo khoa" "$T1"

section "Quan tri vien cap lai mat khau"
expect_status "cap lai mat khau → 200" 200 "$(call POST "/api/students/$SID/reset-password" "$ADMIN")"
RESET_PW=$(json_field password)
token_dead "token cu" "$T1" "$CHANGED_MSG"
expect_status "mat khau cu → 401" 401 "$(student_login anthill123)"
T2=$(login "$USERNAME" "$RESET_PW")
token_alive "dang nhap bang mat khau moi" "$T2"
staff_unaffected

section "Hoc sinh tu doi mat khau"
OWN_PW="Doimk$STAMP"
expect_status "doi mat khau → 200" 200 \
  "$(call POST /api/auth/change-password "$T2" "{\"currentPassword\":\"$RESET_PW\",\"newPassword\":\"$OWN_PW\"}")"
# Token dang cam cung chet — ke ca o chinh endpoint vua goi.
token_dead "token vua dung de doi" "$T2" "$CHANGED_MSG"
expect_status "token vua dung de doi: /api/auth/me → 401" 401 "$(call GET /api/auth/me "$T2")"
expect_status "mat khau duoc cap → 401" 401 "$(student_login "$RESET_PW")"
T3=$(login "$USERNAME" "$OWN_PW")
token_alive "dang nhap bang mat khau tu dat" "$T3"
staff_unaffected

section "Dua vao dien xoa"
expect_status "yeu cau xoa → 200" 200 \
  "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"Kiem tra phien dang nhap bi cat khi xoa."}')"
token_dead "token cu" "$T3" "$PENDING_MSG"
expect_status "token cu: /api/auth/me van tra 403 kem ly do" 403 "$(call GET /api/auth/me "$T3")"
expect_json   "  noi ro la cho xoa" message "$PENDING_MSG"
expect_status "dang nhap → 403" 403 "$(student_login "$OWN_PW")"
staff_unaffected

section "Khoi phuc"
expect_status "khoi phuc → 200" 200 "$(call POST "/api/students/$SID/restore" "$ADMIN")"
token_dead "token truoc khi xoa" "$T3" "$CHANGED_MSG"
T4=$(login "$USERNAME" "$OWN_PW")
token_alive "dang nhap sau khoi phuc" "$T4"
expect_status "dang nhap sau khoi phuc: /api/auth/me → 200" 200 "$(call GET /api/auth/me "$T4")"
staff_unaffected

summary
