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
#   - change-password go sai mat khau hien tai: 400 gan vao o currentPassword, phien van song.
#
# Ba phan cuoi:
#   - chay dua: ban PUT /api/auth/me song song trong luc quan tri vien khoa hay cap lai mat khau. Mot
#     request nap tai khoan truoc luc khoa ma ghi sau luc khoa tung ghi lai nguyen ban cu (Status,
#     SecurityStamp, PasswordHash) va lam lenh khoa bien mat. Chay tuan tu khong bao gio thay loi nay.
#     Tu khi bang Users co concurrency token (xmin), PUT ghi tren ban da cu nhan 409 thay vi ghi de;
#     con lenh khoa va cap lai mat khau giu khoa dong nen luon 200, du bi ban PUT lien tuc.
#   - hub: mot WebSocket mo TRUOC luc khoa van song va van nhan thong bao, vi OnTokenValidated chi chay
#     tren request HTTP. Khoa, cap lai mat khau, tu doi mat khau va dua vao dien xoa deu phai cat ket noi
#     do ngay, khong cho client goi hub; ket noi cua tai khoan khac thi khong bi dung toi.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")
TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")

# Co ca ngay trong nam: hoc sinh cua lan chay truoc con nam lai (chi khoa, khong xoa duoc).
STAMP=$(date +%j%H%M%S)
USERNAME="sr$STAMP"
GID=""; SID=""

LOCKED_MSG="Tài khoản đã bị khoá, liên hệ quản trị viên."
PENDING_MSG="Tài khoản đang chờ xoá, liên hệ quản trị viên nếu đây là nhầm lẫn."
CHANGED_MSG="Phiên đăng nhập đã kết thúc vì tài khoản vừa thay đổi (đổi mật khẩu, cấp lại mật khẩu hoặc bị khoá). Vui lòng đăng nhập lại."
EXPIRED_MSG="Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
WRONG_CURRENT_MSG="Mật khẩu hiện tại không đúng."

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

# "co" khi so dem lon hon 0 — de expect_status in ra mot cau doc duoc thay vi mot con so.
has_any() { (( $1 > 0 )) && echo co || echo "khong ($1)"; }

# ---------------------------------------------------------------------------
# Chay dua
# ---------------------------------------------------------------------------

# curl tren Git Bash la ban Windows: "/dev/null" voi no la mot duong dan khong ton tai.
NULL_OUT=/dev/null
command -v cygpath > /dev/null 2>&1 && NULL_OUT=NUL

# Ban PUT /api/auth/me bang token $1, 16 request cung bay mot luc, lien tuc cho toi khi file $3 xuat
# hien. Ma HTTP cua tung request ghi vao file $2, moi dong mot ma. Than request giu nguyen ho so hien
# tai: thu can kiem la cot KHAC co bi ghi de khong, khong phai ho so co doi khong.
hammer_profile() {
  local token="$1" out="$2" stop="$3"
  local cfg payload i
  cfg=$(mktemp -t anthill-race.XXXXXX)
  for ((i = 0; i < 48; i++)); do
    printf 'url = "%s/api/auth/me"\noutput = "%s"\n' "$HOST" "$NULL_OUT"
  done > "$cfg"
  payload="{\"fullName\":\"HS phien $STAMP\",\"email\":\"$USERNAME@kiemthu.vn\"}"

  while [[ ! -e "$stop" ]]; do
    curl -s -Z --parallel-max 16 -X PUT \
      -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "$payload" \
      -w '%{http_code}\n' -K "$(to_native_path "$cfg")" >> "$out"
  done
  rm -f "$cfg"
}

# Mot vong dua: PUT /me ban nen bang token $2, giua chung goi ham $3 (thao tac thu hoi), cho het request
# roi dem ma. Phai co request 200 (xong truoc luc thu hoi) VA request mang ma $4 (sau luc thu hoi) —
# khong co ca hai thi vong nay khong he dua, va cac phep kiem sau no khong chung minh duoc gi.
# 409 la ma hop le: request doc dong truoc khi mot request khac (PUT song song, hay lenh thu hoi) ghi
# xong, va concurrency token tu choi lan luu tren ban da cu. So 409 cong don vao RACE_CONFLICTS.
RACE_CONFLICTS=0
race_against() {
  local label="$1" token="$2" action="$3" after_code="$4"
  local out stop pid before after conflicts other
  out=$(mktemp -t anthill-race-out.XXXXXX)
  stop="$out.stop"

  hammer_profile "$token" "$out" "$stop" &
  pid=$!
  sleep 0.6      # de luong PUT chay deu truoc luc thu hoi
  "$action"
  sleep 0.4      # de nhung PUT dang bay dung luc thu hoi kip ghi xong
  touch "$stop"
  wait "$pid"

  before=$(grep -c '^200$' "$out")
  after=$(grep -c "^$after_code\$" "$out")
  conflicts=$(grep -c '^409$' "$out")
  other=$(grep -vc -e '^200$' -e '^409$' -e "^$after_code\$" "$out")
  RACE_CONFLICTS=$((RACE_CONFLICTS + conflicts))
  rm -f "$out" "$stop"

  expect_status "$label: co PUT /me xong truoc luc thu hoi" co "$(has_any "$before")"
  expect_status "$label: co PUT /me bi chan sau luc thu hoi ($after_code)" co "$(has_any "$after")"
  expect_status "$label: khong PUT /me nao ra ma khac 200 / 409 / $after_code" 0 "$other"
}

lock_student() {
  expect_status "  quan tri vien khoa giua loat PUT → 200" 200 \
    "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"locked"}')"
}

RACE_PW=""
reset_student_password() {
  expect_status "  quan tri vien cap lai mat khau giua loat PUT → 200" 200 \
    "$(call POST "/api/students/$SID/reset-password" "$ADMIN")"
  RACE_PW=$(json_field password)
}

# ---------------------------------------------------------------------------

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

section "Go sai mat khau hien tai: loi cua o nhap, phien van song"
# Truoc day la 401, ma frontend coi moi 401 tren request co token la phien da chet: nguoi dung bi
# dang xuat va trang dang nhap dua "mat khau hien tai khong dung" ra lam ly do.
expect_status "sai mat khau hien tai → 400" 400 \
  "$(call POST /api/auth/change-password "$T2" "{\"currentPassword\":\"sai$STAMP\",\"newPassword\":\"Khac$STAMP\"}")"
expect_json   "  thong diep" message "$WRONG_CURRENT_MSG"
expect_json   "  gan vao o currentPassword" errors.currentPassword.0 "$WRONG_CURRENT_MSG"
token_alive "token vua go sai" "$T2"
expect_status "mat khau duoc cap van dang nhap duoc → 200" 200 "$(student_login "$RESET_PW")"

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

# Truoc ban sua GenericRepository.Update, vong nao co mot PUT nap tai khoan truoc luc khoa va ghi sau
# luc khoa thi ba phep kiem sau loat PUT deu hong: tai khoan quay ve active, token cu song lai, dang
# nhap lai duoc. Lap nhieu vong vi moi vong chi co mot khoanh khac commit de trung.
RACE_ROUNDS_LOCK=5
CURRENT_PW="$OWN_PW"
section "Chay dua: sua ho so song song voi lenh khoa ($RACE_ROUNDS_LOCK vong)"
for ((round = 1; round <= RACE_ROUNDS_LOCK; round++)); do
  TR=$(login "$USERNAME" "$CURRENT_PW")
  race_against "vong $round" "$TR" lock_student 403
  expect_status "vong $round: quan tri vien doc lai hoc sinh → 200" 200 "$(call GET "/api/students/$SID" "$ADMIN")"
  expect_json   "  van dang khoa" status "locked"
  expect_status "vong $round: token cu → 401" 401 "$(call GET /api/my/exams "$TR")"
  expect_json   "  noi ro la bi khoa" message "$LOCKED_MSG"
  expect_status "vong $round: dang nhap bang mat khau cu → 403" 403 "$(student_login "$CURRENT_PW")"
  expect_status "vong $round: mo khoa cho vong sau → 200" 200 \
    "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"active"}')"
done

# Cung loi do voi cap lai mat khau: PUT ghi lai PasswordHash cu thi mat khau cu dung lai duoc.
RACE_ROUNDS_RESET=3
section "Chay dua: sua ho so song song voi cap lai mat khau ($RACE_ROUNDS_RESET vong)"
for ((round = 1; round <= RACE_ROUNDS_RESET; round++)); do
  TR=$(login "$USERNAME" "$CURRENT_PW")
  race_against "vong $round" "$TR" reset_student_password 401
  expect_status "vong $round: token cu → 401" 401 "$(call GET /api/my/exams "$TR")"
  expect_json   "  noi ro la tai khoan vua thay doi" message "$CHANGED_MSG"
  expect_status "vong $round: mat khau cu → 401" 401 "$(student_login "$CURRENT_PW")"
  expect_status "vong $round: mat khau vua cap → 200" 200 "$(student_login "$RACE_PW")"
  CURRENT_PW="$RACE_PW"
done

# Hang tram PUT cung tai khoan bay song song thi chac chan co cap doc cung mot ban roi cung ghi. Khong
# thay 409 nao nghia la concurrency token khong con tren bang Users va ben ghi sau lai am tham thang.
expect_status "concurrency token: co PUT /me ghi tren ban cu bi tu choi 409" co "$(has_any "$RACE_CONFLICTS")"

section "Hub: ket noi dang mo bi cat ngay khi tai khoan bi thu hoi"
# curl khong noi WebSocket, nen dung mot client toi gian viet bang python (thu vien chuan). Mot tien
# trinh lam tron: mo ket noi bang token $1, goi GetServerTime, thuc hien mot thao tac HTTP ($2 $3 bang
# token $4, than $5) roi KHONG goi gi nua — chi ngoi cho toi 5 giay xem server co tu dong ket noi khong.
# Truoc day ket noi do song tiep va van nhan thong bao ca nhan cho toi lan goi hub ke tiep (luc do bo
# loc hub moi cat) hoac toi khi token het han.
hub_watch() {
  python - "$HOST" "$@" <<'PY'
import base64, json, os, socket, struct, sys, urllib.error, urllib.parse, urllib.request

sys.stdout.reconfigure(encoding="utf-8")
host, token, method, action_path, action_bearer, action_body = sys.argv[1:7]

# Duong dan truyen vao khong mang dau gach cheo dau: Git Bash doi moi doi so bat dau bang "/" thanh
# duong dan Windows truoc khi python nhin thay no, va "/api/students/1/status" toi noi thanh
# "C:/Program Files/Git/api/students/1/status". Gan lai dau gach cheo o day.
action_path = "/" + action_path.lstrip("/")
url = urllib.parse.urlparse(host)

def http(method, path, bearer, body=None):
    data = json.dumps(body).encode() if body is not None else b""
    request = urllib.request.Request(host + path, data=data, method=method)
    request.add_header("Authorization", "Bearer " + bearer)
    request.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()

code, raw = http("POST", "/hubs/notifications/negotiate?negotiateVersion=1", token)
if code != 200:
    print("NEGOTIATE:%d" % code)
    sys.exit(0)
connection_token = json.loads(raw)["connectionToken"]

sock = socket.create_connection((url.hostname, url.port or 80), timeout=10)
path = "/hubs/notifications?id=%s&access_token=%s" % (
    urllib.parse.quote(connection_token), urllib.parse.quote(token))
key = base64.b64encode(os.urandom(16)).decode()
sock.sendall((
    "GET %s HTTP/1.1\r\nHost: %s:%d\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
    "Sec-WebSocket-Key: %s\r\nSec-WebSocket-Version: 13\r\n\r\n"
    % (path, url.hostname, url.port or 80, key)).encode())

buffer = bytearray()
while b"\r\n\r\n" not in buffer:
    chunk = sock.recv(4096)
    if not chunk:
        break
    buffer += chunk
head, _, rest = bytes(buffer).partition(b"\r\n\r\n")
if b" 101 " not in head.split(b"\r\n")[0]:
    print("UPGRADE:" + head.split(b"\r\n")[0].decode(errors="replace"))
    sys.exit(0)
buffer = bytearray(rest)
pending = []

def read_exact(count):
    while len(buffer) < count:
        chunk = sock.recv(4096)
        if not chunk:
            raise EOFError
        buffer.extend(chunk)
    data = bytes(buffer[:count])
    del buffer[:count]
    return data

def send_frame(opcode, data):
    mask = os.urandom(4)
    header = bytearray([0x80 | opcode])
    if len(data) < 126:
        header.append(0x80 | len(data))
    elif len(data) < 65536:
        header.append(0x80 | 126)
        header += struct.pack(">H", len(data))
    else:
        header.append(0x80 | 127)
        header += struct.pack(">Q", len(data))
    sock.sendall(bytes(header) + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(data)))

def next_message():
    while not pending:
        first, second = read_exact(2)
        opcode, length = first & 0x0F, second & 0x7F
        if length == 126:
            length = struct.unpack(">H", read_exact(2))[0]
        elif length == 127:
            length = struct.unpack(">Q", read_exact(8))[0]
        payload = read_exact(length)
        if opcode == 0x8:
            raise EOFError
        if opcode == 0x9:
            send_frame(0xA, payload)
            continue
        for part in payload.decode().split("\x1e"):
            if part:
                pending.append(json.loads(part))
    return pending.pop(0)

def invoke(invocation_id):
    message = {"type": 1, "invocationId": invocation_id, "target": "GetServerTime", "arguments": []}
    send_frame(0x1, (json.dumps(message) + "\x1e").encode())
    while True:
        reply = next_message()
        if reply.get("type") == 7:
            raise EOFError
        if reply.get("type") == 3 and reply.get("invocationId") == invocation_id:
            return reply

send_frame(0x1, (json.dumps({"protocol": "json", "version": 1}) + "\x1e").encode())
handshake = next_message()
if handshake.get("error"):
    print("HANDSHAKE:" + handshake["error"])
    sys.exit(0)

reply = invoke("1")
print("TRUOC:" + ("loi" if "error" in reply else "ok"))

code, raw = http(method, action_path, action_bearer, json.loads(action_body) if action_body else None)
print("HANH_DONG:%d" % code)
print("BODY:" + raw.decode(errors="replace").replace("\n", " "))

# Chi doc, khong goi phuong thuc nao: ping (type 6) bo qua, close (type 7) hay dong socket la bi cat.
sock.settimeout(5)
try:
    while True:
        message = next_message()
        if message.get("type") == 7:
            raise EOFError
except socket.timeout:
    print("SAU:mo")
    sock.settimeout(10)
    try:
        print("GOI_LAI:" + ("loi" if "error" in invoke("2") else "ok"))
    except (EOFError, OSError):
        print("GOI_LAI:dong")
except (EOFError, OSError):
    print("SAU:dong")
PY
}
hub_line() { printf '%s\n' "$HUB_OUT" | sed -n "s/^$1://p" | head -1; }

# $1 nhan, $2 token cua ket noi; phan con lai la thao tac thu hoi truyen cho hub_watch.
expect_hub_closed() {
  local label="$1" token="$2"; shift 2
  HUB_OUT=$(hub_watch "$token" "$@")
  expect_status "$label: ket noi mo khi con hop le → ok" ok "$(hub_line TRUOC)"
  expect_status "$label: thao tac thu hoi → 200" 200 "$(hub_line HANH_DONG)"
  expect_status "$label: server cat ket noi ngay, khong can goi hub" dong "$(hub_line SAU)"
  expect_status "$label: token cua ket noi do: negotiate lai → 401" 401 "$(negotiate "$token")"
}

TH=$(login "$USERNAME" "$CURRENT_PW")
expect_hub_closed "khoa" "$TH" PATCH "api/students/$SID/status" "$ADMIN" '{"status":"locked"}'
expect_status "  mo khoa cho buoc sau → 200" 200 "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"active"}')"

TH=$(login "$USERNAME" "$CURRENT_PW")
expect_hub_closed "cap lai mat khau" "$TH" POST "api/students/$SID/reset-password" "$ADMIN" ""
CURRENT_PW=$(hub_line BODY | python -c 'import json,sys; print(json.load(sys.stdin)["password"])')

TH=$(login "$USERNAME" "$CURRENT_PW")
HUB_PW="Hub$STAMP"
expect_hub_closed "tu doi mat khau" "$TH" POST api/auth/change-password "$TH" \
  "{\"currentPassword\":\"$CURRENT_PW\",\"newPassword\":\"$HUB_PW\"}"
CURRENT_PW="$HUB_PW"

TH=$(login "$USERNAME" "$CURRENT_PW")
expect_hub_closed "dua vao dien xoa" "$TH" POST "api/students/$SID/request-deletion" "$ADMIN" \
  '{"reason":"Kiem tra ket noi hub bi cat khi xoa."}'
expect_status "  khoi phuc cho buoc sau → 200" 200 "$(call POST "/api/students/$SID/restore" "$ADMIN")"

# Doi chung: cat theo tai khoan, khong cat bua. Ket noi cua giao vien van song khi hoc sinh bi khoa.
HUB_OUT=$(hub_watch "$TEACHER" PATCH "api/students/$SID/status" "$ADMIN" '{"status":"locked"}')
expect_status "giao vien: ket noi mo → ok" ok "$(hub_line TRUOC)"
expect_status "giao vien: hoc sinh bi khoa → 200" 200 "$(hub_line HANH_DONG)"
expect_status "giao vien: ket noi van mo sau 5 giay" mo "$(hub_line SAU)"
expect_status "giao vien: van goi hub duoc" ok "$(hub_line GOI_LAI)"

summary
