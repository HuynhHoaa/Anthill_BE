#!/usr/bin/env bash
# Quan ly tai khoan giao vien — chi quan tri vien (nha truong cap tai khoan, gan mon, khoa khi giao vien nghi).
#
# Chay:  bash tests/api-checks/teacher-management.sh   (API phai dang chay o $HOST, da migrate)
#
# Khoa lai nhung dieu ma unit test khong cham toi duoc vi can HTTP + database that:
#   - chi quan tri vien goi duoc; giao vien, hoc sinh bi tu choi, khach bi chan;
#   - tao: bat buoc ho ten, email dung dinh dang, mon co that, mat khau toi thieu; email trung → 409 (chung mot khong
#     gian ten voi tai khoan khac); tai khoan moi bi ep doi mat khau;
#   - danh sach: tim theo ten khong dau / email, loc trang thai, gia tri loc la → 400, phan trang;
#   - sua: giu nguyen email cua chinh minh khong bao trung, email cua nguoi khac → 409, doi mon bi chan khi giao vien da
#     soan cau hoi (can mon thu hai nen ghi bang psql — xem duoi);
#   - khoa BAT BUOC ly do (thieu hoac qua ngan → 400), khoa lam token cu chet ngay, khong dang nhap duoc, mo khoa thi
#     dang nhap lai duoc; lich su ghi khoa va mo khoa kem ly do va nguoi thuc hien;
#   - cap lai mat khau: mat khau cu chet, mat khau moi chi hien mot lan va ep doi;
#   - chi tiet giao vien liet ke cac lop dang phu trach; id cua hoc sinh hay quan tri vien tra 404.
#
# Hai phep can mon thu hai nen ghi thang vao database bang psql. psql CHI chay khi nguoi chay tu dat PGHOST/PGPORT/
# PGDATABASE/PGUSER/PGPASSWORD (script KHONG tu doc user-secrets, de khong the ghi nham vao database that) va PGDATABASE
# khac `anthill`. Thieu thi cac phep do bao hong, khong im lang bo qua.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

STAMP=$(date +%H%M%S)
# Số điện thoại riêng của lần chạy: các bộ kiểm khác để lại hàng chục giáo viên cùng một số, tìm theo số chung sẽ trôi khỏi trang đầu.
NEW_PHONE="0912$STAMP"
GROUP_IDS=(); QUESTION_IDS=(); TEACHER_IDS=()
SUBJECT_B=""

# ── psql: chi khi nguoi chay chu dong tro vao database thu ───────────────────────────────────────
PSQL=$(command -v psql 2>/dev/null || ls /c/Program\ Files/PostgreSQL/*/bin/psql.exe 2>/dev/null | tail -n 1)
HAS_PSQL=no
if [[ -n "$PSQL" && -n "${PGPASSWORD:-}" && -n "${PGDATABASE:-}" ]]; then
  if [[ "$PGDATABASE" == "anthill" ]]; then
    echo "  Tu choi chay: PGDATABASE=anthill la database that. Tro vao database thu (vd anthill_score100)." >&2
    exit 1
  fi
  HAS_PSQL=yes
fi
sql() { "$PSQL" -q -At -v ON_ERROR_STOP=1 -c "$1" | tr -d '\r'; }

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }
J() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$(to_native_path "$BODY_FILE")"; }
has_id() { J "'yes' if any(i['id']==$1 for i in d['items']) else 'no'"; }

cleanup() {
  section "Don du lieu"
  local removed=0
  for id in "${QUESTION_IDS[@]}"; do call DELETE "/api/questions/$id" "$ADMIN" > /dev/null; done
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && removed=$((removed + 1))
  done
  for id in "${TEACHER_IDS[@]}"; do retire_teacher "$ADMIN" "$id"; done
  [[ -n "$SUBJECT_B" ]] && { sql "DELETE FROM \"Subjects\" WHERE \"Id\" = $SUBJECT_B;" > /dev/null 2>&1 || printf '  khong xoa duoc mon kiem thu %s\n' "$SUBJECT_B"; }
  printf '  xoa %d/%d lop, khoa %d giao vien kiem thu\n' "$removed" "${#GROUP_IDS[@]}" "${#TEACHER_IDS[@]}"
}
trap cleanup EXIT

call GET "/api/teachers/$DEMO_TEACHER_ID" "$ADMIN" > /dev/null
SUBJECT_A=$(json_field subjectId)
NEW_EMAIL="gv.moi$STAMP@kiemthu.vn"
# Tham so 3 la so dien thoai: bo trong = so hop le mac dinh (so dien thoai bat buoc); muon thu thieu so thi truyen NONE.
create_body() {
  local phone="${3:-0912345678}"; [[ "$phone" == "NONE" ]] && phone=""
  printf '{"fullName":"%s","email":"%s","phone":"%s","subjectId":%s,"password":"%s"}' "$1" "$2" "$phone" "${4:-$SUBJECT_A}" "${5:-123456}"
}

# ── Phan quyen ───────────────────────────────────────────────────────────────────────────────────
section "Chi quan tri vien quan ly giao vien"
expect_status "quan tri vien liet ke → 200" 200 "$(call GET /api/teachers "$ADMIN")"
expect_status "giao vien liet ke → 403" 403 "$(call GET /api/teachers "$TEACHER")"
expect_status "hoc sinh liet ke → 403" 403 "$(call GET /api/teachers "$STUDENT")"
expect_status "khach → 401" 401 "$(call GET /api/teachers)"
expect_status "giao vien tao giao vien → 403" 403 "$(call POST /api/teachers "$TEACHER" "$(create_body "GV x" "x$STAMP@kiemthu.vn")")"
expect_status "giao vien khoa giao vien → 403" 403 "$(call PATCH "/api/teachers/$DEMO_TEACHER_ID/status" "$TEACHER" "$LOCK_BODY")"

# ── Tao ──────────────────────────────────────────────────────────────────────────────────────────
section "Tao tai khoan giao vien: kiem tra dau vao"
expect_status "thieu ho ten → 400" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "" "a$STAMP@kiemthu.vn")")"
expect_status "email sai dinh dang → 400" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "GV A" "khong-phai-email")")"
expect_status "mat khau qua ngan → 400" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "GV A" "a$STAMP@kiemthu.vn" "" "$SUBJECT_A" "123")")"
expect_status "thieu so dien thoai → 400 (bat buoc)" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "GV A" "a$STAMP@kiemthu.vn" NONE "$SUBJECT_A")")"
expect_status "  noi phai nhap so dien thoai" yes "$(body | grep -q 'số điện thoại' && echo yes || echo no)"
expect_status "so dien thoai sai → 400" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "GV A" "a$STAMP@kiemthu.vn" "abc" "$SUBJECT_A")")"
expect_status "chua chon mon → 400" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "GV A" "a$STAMP@kiemthu.vn" "" 0)")"
expect_status "mon khong ton tai → 400" 400 "$(call POST /api/teachers "$ADMIN" "$(create_body "GV A" "a$STAMP@kiemthu.vn" "" 999999)")"

section "Tao tai khoan giao vien"
expect_status "tao dung → 200" 200 "$(call POST /api/teachers "$ADMIN" "$(create_body "Nguyen Thi Moi $STAMP" "$NEW_EMAIL" "$NEW_PHONE")")"
NEW_ID=$(json_field id); TEACHER_IDS+=("$NEW_ID")
expect_json   "  dang hoat dong" status active
expect_json   "  chua phu trach lop nao" groupCount 0
expect_json   "  bi ep doi mat khau o lan dang nhap dau" mustChangePassword true
expect_json   "  gan dung mon" subjectId "$SUBJECT_A"
expect_status "  co ten mon" yes "$(J "'yes' if d['subject'] else 'no'")"
expect_status "email trung → 409" 409 "$(call POST /api/teachers "$ADMIN" "$(create_body "Nguoi khac" "$NEW_EMAIL")")"
expect_status "email khac hoa thuong van trung → 409" 409 "$(call POST /api/teachers "$ADMIN" "$(create_body "Nguoi khac" "${NEW_EMAIL^^}")")"
expect_status "email trung voi tai khoan khac (giao vien demo) → 409" 409 "$(call POST /api/teachers "$ADMIN" "$(create_body "Nguoi khac" "an.nv@anthill.edu.vn")")"

section "Giao vien moi dang nhap: mat khau tam, ep doi"
TOKEN_TEMP=$(login "$NEW_EMAIL" "123456")
expect_status "dang nhap bang mat khau tam → co token" yes "$([[ -n "$TOKEN_TEMP" ]] && echo yes || echo no)"
expect_status "chua doi mat khau thi chua dung duoc ngan hang cau hoi → 403" 403 "$(call GET /api/questions "$TOKEN_TEMP")"
TOKEN_NEW=$(login_and_clear_must_change "$NEW_EMAIL" "123456" "Moi$STAMP")
expect_status "doi xong mat khau → dung duoc ngan hang cau hoi" 200 "$(call GET /api/questions "$TOKEN_NEW")"
expect_status "giao vien moi khong vao duoc khu quan tri → 403" 403 "$(call GET /api/teachers "$TOKEN_NEW")"

# ── Danh sach ────────────────────────────────────────────────────────────────────────────────────
section "Danh sach, tim kiem, loc"
expect_status "tim theo email → 200" 200 "$(call GET "/api/teachers?search=gv.moi$STAMP" "$ADMIN")"
expect_json   "  dung mot ket qua" total 1
expect_status "tim theo ten KHONG DAU → 200" 200 "$(call GET "/api/teachers?search=Nguyen%20Thi%20Moi%20$STAMP" "$ADMIN")"
expect_status "  thay giao vien vua tao" yes "$(has_id "$NEW_ID")"
expect_status "tim theo so dien thoai → 200" 200 "$(call GET "/api/teachers?search=$NEW_PHONE" "$ADMIN")"
expect_status "  thay giao vien vua tao" yes "$(has_id "$NEW_ID")"
expect_status "loc status=locked → 200" 200 "$(call GET "/api/teachers?status=locked&search=gv.moi$STAMP" "$ADMIN")"
expect_json   "  chua ai bi khoa" total 0
expect_status "loc status=active → 200" 200 "$(call GET "/api/teachers?status=active&search=gv.moi$STAMP" "$ADMIN")"
expect_json   "  thay giao vien dang hoat dong" total 1
expect_status "gia tri loc la → 400" 400 "$(call GET "/api/teachers?status=xyz" "$ADMIN")"
expect_status "loc theo mon → 200" 200 "$(call GET "/api/teachers?subjectId=$SUBJECT_A&search=gv.moi$STAMP" "$ADMIN")"
expect_json   "  thay giao vien trong mon do" total 1
expect_status "loc theo mon khac → 200" 200 "$(call GET "/api/teachers?subjectId=999999&search=gv.moi$STAMP" "$ADMIN")"
expect_json   "  khong thay" total 0
expect_status "phan trang → 200" 200 "$(call GET "/api/teachers?page=1&pageSize=1" "$ADMIN")"
expect_status "  tra dung mot dong, tong lon hon mot" yes "$(J "'yes' if len(d['items']) == 1 and d['total'] >= 2 and d['pageSize'] == 1 else 'no'")"

# ── Chi tiet ─────────────────────────────────────────────────────────────────────────────────────
section "Chi tiet va cac lop dang phu trach"
LOP=$(new_class "$ADMIN" "$TOKEN_NEW" "GV Moi lop $STAMP") || fail_fast "tao lop cho giao vien moi"
GROUP_IDS+=("$LOP")
expect_status "chi tiet → 200" 200 "$(call GET "/api/teachers/$NEW_ID" "$ADMIN")"
expect_json   "  groupCount = 1" groupCount 1
expect_status "  liet ke lop dang phu trach" yes "$(J "'yes' if [g['id'] for g in d['groups']] == [$LOP] else 'no'")"
expect_status "id hoc sinh → 404" 404 "$(call GET "/api/teachers/$(
  call GET /api/auth/me "$STUDENT" >/dev/null; json_field id)" "$ADMIN")"
expect_status "id khong ton tai → 404" 404 "$(call GET /api/teachers/999999 "$ADMIN")"

# ── Sua ──────────────────────────────────────────────────────────────────────────────────────────
section "Sua thong tin"
expect_status "doi ten va so dien thoai → 200" 200 \
  "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" "0987654321" "$SUBJECT_A")")"
expect_json   "  ten moi" fullName "Nguyen Thi Moi Da Sua"
expect_json   "  so dien thoai moi" phone 0987654321
expect_status "giu nguyen email cua chinh minh khong bao trung → 200" 200 \
  "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" "0987654321" "$SUBJECT_A")")"
expect_status "sua ma de trong so dien thoai → 400 (bat buoc ca khi sua)" 400 \
  "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" NONE "$SUBJECT_A")")"
expect_status "  so dien thoai cu van con nguyen" 0987654321 "$(call GET "/api/teachers/$NEW_ID" "$ADMIN" > /dev/null; json_field phone)"
expect_status "email cua nguoi khac → 409" 409 \
  "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "an.nv@anthill.edu.vn" "" "$SUBJECT_A")")"
expect_status "mon khong ton tai → 400 hoac 409" yes \
  "$([[ "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" "" 999999)")" =~ ^(400|409)$ ]] && echo yes || echo no)"
expect_status "sua id hoc sinh → 404" 404 \
  "$(call PUT "/api/teachers/$(call GET /api/auth/me "$STUDENT" >/dev/null; json_field id)" "$ADMIN" "$(create_body "X Y" "xy$STAMP@kiemthu.vn")")"

section "Doi email: tai khoan dang nhap bang email moi"
NEW_EMAIL2="gv.moi2$STAMP@kiemthu.vn"
expect_status "doi email → 200" 200 "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL2" "0987654321" "$SUBJECT_A")")"
expect_status "  dang nhap bang email moi → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"$NEW_EMAIL2\",\"password\":\"Moi$STAMP\"}")"
expect_status "  email cu khong con dang nhap duoc → 401" 401 "$(call POST /api/auth/login "" "{\"username\":\"$NEW_EMAIL\",\"password\":\"Moi$STAMP\"}")"
NEW_EMAIL="$NEW_EMAIL2"

section "Doi mon bi chan khi giao vien da soan cau hoi hoac de (can mon thu hai)"
if [[ "$HAS_PSQL" != yes ]]; then
  expect_status "dung mon thu hai (can psql va PG* tro vao database thu)" ok "thieu"
else
  SUBJECT_B=$(sql "INSERT INTO \"Subjects\" (\"Name\",\"Code\",\"CreatedAt\") VALUES ('Mon kiem thu $STAMP','GV$STAMP', now()) RETURNING \"Id\";")
  if [[ -z "$SUBJECT_B" ]]; then
    expect_status "dung mon thu hai qua psql" ok "hong"
  else
    expect_status "giao vien chua soan gi → doi mon duoc → 200" 200 \
      "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" "" "$SUBJECT_B")")"
    expect_json   "  da sang mon moi" subjectId "$SUBJECT_B"
    call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" "" "$SUBJECT_A")" > /dev/null
    TOKEN_NEW=$(login "$NEW_EMAIL" "Moi$STAMP")
    c=$(call POST /api/questions "$TOKEN_NEW" "{\"content\":\"Cau cua giao vien moi $STAMP\",\"topic\":\"GVMoi$STAMP\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
    [[ "$c" != "200" ]] && fail_fast "giao vien moi tao cau hoi: HTTP $c — $(body)"
    QUESTION_IDS+=("$(json_field id)")
    expect_status "da soan cau hoi → doi mon bi chan → 409" 409 \
      "$(call PUT "/api/teachers/$NEW_ID" "$ADMIN" "$(create_body "Nguyen Thi Moi Da Sua" "$NEW_EMAIL" "" "$SUBJECT_B")")"
    expect_status "  noi ro ly do" yes "$(body_has 'chưa đổi môn')"
    expect_status "  van o mon cu" 200 "$(call GET "/api/teachers/$NEW_ID" "$ADMIN")"
    expect_json   "    subjectId khong doi" subjectId "$SUBJECT_A"
  fi
fi

# ── Khoa ─────────────────────────────────────────────────────────────────────────────────────────
section "Khoa tai khoan: ly do bat buoc"
TOKEN_NEW=$(login "$NEW_EMAIL" "Moi$STAMP")
expect_status "khoa thieu ly do → 400" 400 "$(call PATCH "/api/teachers/$NEW_ID/status" "$ADMIN" '{"status":"locked"}')"
expect_status "khoa voi ly do qua ngan → 400" 400 "$(call PATCH "/api/teachers/$NEW_ID/status" "$ADMIN" '{"status":"locked","reason":"ngan"}')"
expect_status "trang thai la → 400" 400 "$(call PATCH "/api/teachers/$NEW_ID/status" "$ADMIN" '{"status":"xyz","reason":"Gia tri trang thai khong hop le"}')"
expect_status "van dang hoat dong sau cac lan bi tu choi" 200 "$(call GET "/api/teachers/$NEW_ID" "$ADMIN")"
expect_json   "  status = active" status active
expect_status "token cu van dung duoc truoc khi khoa" 200 "$(call GET /api/questions "$TOKEN_NEW")"

section "Khoa tai khoan"
expect_status "khoa kem ly do → 200" 200 "$(call PATCH "/api/teachers/$NEW_ID/status" "$ADMIN" '{"status":"locked","reason":"Giao vien nghi viec tu dau thang"}')"
expect_json   "  status = locked" status locked
expect_status "token cu chet ngay → 401" 401 "$(call GET /api/questions "$TOKEN_NEW")"
expect_status "dang nhap lai khi bi khoa → 403" 403 "$(call POST /api/auth/login "" "{\"username\":\"$NEW_EMAIL\",\"password\":\"Moi$STAMP\"}")"
expect_status "khoa lan hai → 200 (khong co gi de doi)" 200 "$(call PATCH "/api/teachers/$NEW_ID/status" "$ADMIN" '{"status":"locked","reason":"Khoa lai lan nua cho chac chan"}')"
expect_status "loc status=locked → 200" 200 "$(call GET "/api/teachers?status=locked&search=gv.moi2$STAMP" "$ADMIN")"
expect_status "  thay giao vien bi khoa" yes "$(has_id "$NEW_ID")"
expect_status "lop van mang ten giao vien bi khoa → 200" 200 "$(call GET "/api/student-groups/$LOP" "$ADMIN")"
expect_json   "  trang thai giao vien trong lop = locked" teachers.0.status locked
expect_status "khong chon lai giao vien bi khoa cho lop moi → 409" 409 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop gv khoa $STAMP\",\"teacherId\":$NEW_ID}")"

section "Mo khoa"
expect_status "mo khoa khong can ly do → 200" 200 "$(call PATCH "/api/teachers/$NEW_ID/status" "$ADMIN" '{"status":"active"}')"
expect_json   "  status = active" status active
expect_status "dang nhap lai duoc → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"$NEW_EMAIL\",\"password\":\"Moi$STAMP\"}")"

section "Lich su khoa va mo khoa"
expect_status "doc lich su → 200" 200 "$(call GET "/api/teachers/$NEW_ID/history" "$ADMIN")"
expect_status "  co ba dong: khoa, khoa lai khong doi (khong ghi), mo khoa → thuc te hai dong" 2 "$(J "len(d)")"
expect_status "  dong moi nhat la mo khoa, khong ly do" true "$(J "str(d[0]['kind'] == 'unlocked' and 'reason' not in d[0]).lower()")"
expect_status "  dong cu hon la khoa kem ly do va nguoi thuc hien" true \
  "$(J "str(d[1]['kind'] == 'locked' and d[1]['reason'] == 'Giao vien nghi viec tu dau thang' and bool(d[1].get('actorName'))).lower()")"
expect_status "giao vien doc lich su → 403" 403 "$(call GET "/api/teachers/$NEW_ID/history" "$TEACHER")"
expect_status "lich su cua id hoc sinh → 404" 404 "$(call GET "/api/teachers/$(call GET /api/auth/me "$STUDENT" >/dev/null; json_field id)/history" "$ADMIN")"

# ── Cap lai mat khau ─────────────────────────────────────────────────────────────────────────────
section "Cap lai mat khau"
expect_status "cap lai → 200" 200 "$(call POST "/api/teachers/$NEW_ID/reset-password" "$ADMIN")"
RESET_PASSWORD=$(json_field password)
expect_status "  mat khau moi du dai" yes "$([[ ${#RESET_PASSWORD} -ge 8 ]] && echo yes || echo no)"
expect_json   "  ep doi o lan dang nhap dau" mustChangePassword true
expect_status "mat khau cu khong con dung → 401" 401 "$(call POST /api/auth/login "" "{\"username\":\"$NEW_EMAIL\",\"password\":\"Moi$STAMP\"}")"
expect_status "mat khau moi dang nhap duoc → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"$NEW_EMAIL\",\"password\":\"$RESET_PASSWORD\"}")"
expect_status "giao vien tu cap lai → 403" 403 "$(call POST "/api/teachers/$NEW_ID/reset-password" "$TEACHER")"
expect_status "cap lai cho id hoc sinh → 404" 404 "$(call POST "/api/teachers/$(call GET /api/auth/me "$STUDENT" >/dev/null; json_field id)/reset-password" "$ADMIN")"

summary
