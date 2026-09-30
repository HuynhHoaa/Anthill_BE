#!/usr/bin/env bash
# Lop hoc do quan tri vien lap — mo hinh to chuc (§4.3, ban sua doi).
#
# Chay:  bash tests/api-checks/classes-admin.sh   (API phai dang chay o $HOST)
#
# Bo kiem nay ra doi cung luc voi viec DAO NGUOC quyen so huu lop. Truoc day giao vien tao lop cua
# rieng minh; gio quan tri vien lap lop, phan cong giao vien, roi cong bo.
#
# Ly do doi khong phai phan quyen ma la LUOC DO: User.GroupId la khoa ngoai don nen mot hoc sinh
# thuoc dung mot lop. De giao vien so huu lop thi hai nguoi cung day mot em se gianh nhau em do —
# nguoi them sau keo em ra khoi lop cua nguoi truoc va lam hong moi bai nguoi truoc da giao.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
GROUP_IDS=()
TEACHER_ID=""

cleanup() {
  section "Don du lieu"
  local removed=0
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && removed=$((removed + 1))
  done
  printf '  xoa %d/%d lop\n' "$removed" "${#GROUP_IDS[@]}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
has_field() { python -c "
import json,sys
print('present' if sys.argv[1] in json.load(sys.stdin) else 'absent')
" "$1" < "$BODY_FILE"; }

call GET /api/auth/me "$TEACHER" > /dev/null
TEACHER_ID=$(json_field id)

section "Dao nguoc quyen so huu"
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop $STAMP\",\"description\":\"Kiem thu mo hinh to chuc\"}")
[[ "$code" != "200" ]] && fail_fast "admin tao lop: HTTP $code — $(body)"
GID=$(json_field id); GROUP_IDS+=("$GID")
expect_status "admin lap lop → 200" 200 "$code"
expect_json   "  lop moi ra doi o dang NHAP" isPublished false
expect_status "  chua co giao vien nao" 0 \
  "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)["teachers"]))')"
expect_status "giao vien lap lop → 403" 403 \
  "$(call POST /api/student-groups "$TEACHER" "{\"name\":\"Lop cua giao vien $STAMP\"}")"

section "Ban nhap khong ro sang giao vien"
expect_status "danh sach cua giao vien → 200" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "  khong chua lop dang nhap" true \
  "$(body | python -c "import json,sys;print(str(all(g['id'] != $GID for g in json.load(sys.stdin))).lower())")"
# 404 chu khong 403: 403 la xac nhan id do co that, du de do dan danh sach lop dang dung do.
expect_status "doc thang id lop nhap → 404" 404 "$(call GET "/api/student-groups/$GID" "$TEACHER")"

section "Phan cong giao vien"
expect_status "danh sach giao vien kha dung → 200" 200 "$(call GET "/api/student-groups/$GID/assignable-teachers" "$ADMIN")"
expect_status "  moi muc deu co trang thai tai khoan" true \
  "$(body | python -c "import json,sys;d=json.load(sys.stdin);print(str(all('status' in t for t in d)).lower())")"
expect_status "  co giao vien demo trong do" true \
  "$(body | python -c "import json,sys;print(str(any(t['id'] == $TEACHER_ID for t in json.load(sys.stdin))).lower())")"

# 1 nguoi that + 1 id lap bi khu + 1 id la.
expect_status "phan cong theo lo → 200" 200 \
  "$(call POST "/api/student-groups/$GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID,$TEACHER_ID,999999]}")"
expect_json   "  id lap bi khu, chi phan cong 1" assigned 1
expect_json   "  id la duoc dem rieng"           skippedNotTeacher 1
expect_status "phan cong lai nguoi da co → 200" 200 \
  "$(call POST "/api/student-groups/$GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}")"
expect_json   "  bo qua, khong nhan doi" skippedAlreadyAssigned 1
expect_status "danh sach rong → 400" 400 \
  "$(call POST "/api/student-groups/$GID/teachers" "$ADMIN" '{"teacherIds":[]}')"
expect_status "giao vien tu phan cong minh → 403" 403 \
  "$(call POST "/api/student-groups/$GID/teachers" "$TEACHER" "{\"teacherIds\":[$TEACHER_ID]}")"

section "Da phan cong nhung chua cong bo thi van chua thay"
expect_status "danh sach cua giao vien → 200" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "  van khong chua lop do" true \
  "$(body | python -c "import json,sys;print(str(all(g['id'] != $GID for g in json.load(sys.stdin))).lower())")"

section "Cong bo"
expect_status "admin cong bo → 200" 200 "$(call POST "/api/student-groups/$GID/publish" "$ADMIN")"
expect_json   "  isPublished = true" isPublished true
expect_status "  co moc publishedAt" present "$(has_field publishedAt)"
expect_status "  teachers day du ten/email/mon/trang thai" true \
  "$(body | python -c "
import json,sys
t = json.load(sys.stdin)['teachers'][0]
print(str(all(k in t for k in ('fullName','email','subject','avatarColor','status'))).lower())")"
expect_status "cong bo lan hai → 409" 409 "$(call POST "/api/student-groups/$GID/publish" "$ADMIN")"
expect_status "giao vien cong bo → 403" 403 "$(call POST "/api/student-groups/$GID/publish" "$TEACHER")"

section "Cong bo xong giao vien moi thay"
expect_status "danh sach cua giao vien → 200" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "  da co lop trong danh sach" true \
  "$(body | python -c "import json,sys;print(str(any(g['id'] == $GID for g in json.load(sys.stdin))).lower())")"
expect_status "doc chi tiet → 200" 200 "$(call GET "/api/student-groups/$GID" "$TEACHER")"

section "Duong lui: go cong bo"
# Cong bo nham la chuyen xay ra. Khong co duong lui thi lop ket vinh vien o trang thai sai.
expect_status "go cong bo → 200" 200 "$(call POST "/api/student-groups/$GID/unpublish" "$ADMIN")"
expect_json   "  isPublished ve false" isPublished false
expect_status "  van giu moc cong bo lan dau" present "$(has_field publishedAt)"
expect_status "  giao vien khong con thay" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "    danh sach khong chua lop do" true \
  "$(body | python -c "import json,sys;print(str(all(g['id'] != $GID for g in json.load(sys.stdin))).lower())")"
expect_status "go cong bo lan hai → 409" 409 "$(call POST "/api/student-groups/$GID/unpublish" "$ADMIN")"
expect_status "giao vien go cong bo → 403" 403 "$(call POST "/api/student-groups/$GID/unpublish" "$TEACHER")"

section "Bo loc trang thai (chi co nghia voi quan tri vien)"
call POST "/api/student-groups/$GID/publish" "$ADMIN" > /dev/null
expect_status "loc status=published → 200" 200 "$(call GET "/api/student-groups?status=published" "$ADMIN")"
expect_status "  moi lop tra ve deu da cong bo" true \
  "$(body | python -c "import json,sys;print(str(all(g['isPublished'] for g in json.load(sys.stdin))).lower())")"
expect_status "loc status=draft → 200" 200 "$(call GET "/api/student-groups?status=draft" "$ADMIN")"
expect_status "  moi lop tra ve deu la nhap" true \
  "$(body | python -c "import json,sys;print(str(all(not g['isPublished'] for g in json.load(sys.stdin))).lower())")"
# Tu choi gia tri la thay vi am tham bo loc: go sai mot chu ma van nhan du danh sach thi nguoi dung
# tin la minh da loc, trong khi khong co bo loc nao chay ca.
expect_status "gia tri loc la → 400" 400 "$(call GET "/api/student-groups?status=xyz" "$ADMIN")"

section "Go phan cong"
expect_status "go giao vien → 200" 200 "$(call DELETE "/api/student-groups/$GID/teachers/$TEACHER_ID" "$ADMIN")"
expect_status "go lan hai → 404" 404 "$(call DELETE "/api/student-groups/$GID/teachers/$TEACHER_ID" "$ADMIN")"
expect_status "giao vien tu go minh → 403" 403 "$(call DELETE "/api/student-groups/$GID/teachers/$TEACHER_ID" "$TEACHER")"
expect_status "go xong, giao vien khong con thay lop" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "  danh sach khong chua lop do" true \
  "$(body | python -c "import json,sys;print(str(all(g['id'] != $GID for g in json.load(sys.stdin))).lower())")"

section "Ten lop duy nhat tren toan he thong"
# Truoc day ten chi duy nhat trong pham vi mot giao vien nen hai nguoi deu co the co "Lop 8A".
# Gio lop la don vi hanh chinh cua ca trung tam: hai lop trung ten la hai thu khong ai phan biet
# noi luc giao bai, ma hoc sinh thi chi thuoc duoc mot trong hai.
expect_status "trung ten → 409" 409 "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop $STAMP\"}")"

summary
