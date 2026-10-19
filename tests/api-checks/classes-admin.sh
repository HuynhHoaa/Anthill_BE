#!/usr/bin/env bash
# Lop hoc do quan tri vien lap — mo hinh to chuc (§4.4), moi lop co DUNG MOT giao vien.
#
# Chay:  bash tests/api-checks/classes-admin.sh   (API phai dang chay o $HOST)
#
# Bo kiem nay ra doi cung luc voi viec DAO NGUOC quyen so huu lop. Truoc day giao vien tao lop cua
# rieng minh; gio quan tri vien lap lop (kem giao vien phu trach), xep hoc sinh, roi cong bo.
#
# Ly do doi khong phai phan quyen ma la LUOC DO: User.GroupId la khoa ngoai don nen mot hoc sinh
# thuoc dung mot lop. De giao vien so huu lop thi hai nguoi cung day mot em se gianh nhau em do —
# nguoi them sau keo em ra khoi lop cua nguoi truoc va lam hong moi bai nguoi truoc da giao.
#
# Quy tac moi: mot lop co dung mot giao vien, chon ngay luc lap lop; doi giao vien giua chung phai ghi ly do va
# duoc luu vao lich su cua lop.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
GROUP_IDS=()
EXTRA_TEACHERS=()
TEACHER_ID=""

cleanup() {
  section "Don du lieu"
  local removed=0
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && removed=$((removed + 1))
  done
  for id in "${EXTRA_TEACHERS[@]}"; do retire_teacher "$ADMIN" "$id"; done
  printf '  xoa %d/%d lop, khoa %d giao vien kiem thu\n' "$removed" "${#GROUP_IDS[@]}" "${#EXTRA_TEACHERS[@]}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
has_field() { python -c "
import json,sys
print('present' if sys.argv[1] in json.load(sys.stdin) else 'absent')
" "$1" < "$BODY_FILE"; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }
# Danh sach lop cua giao vien co chua lop $1 khong
teacher_sees() {
  call GET /api/student-groups "$TEACHER" > /dev/null
  python -c "import json,sys;print('yes' if any(g['id'] == int(sys.argv[1]) for g in json.load(sys.stdin)) else 'no')" "$1" < "$BODY_FILE"
}

call GET /api/auth/me "$TEACHER" > /dev/null
TEACHER_ID=$(json_field id)
call GET /api/auth/me "$ADMIN" > /dev/null
ADMIN_ID=$(json_field id)

OTHER=$(new_teacher "$ADMIN" "$STAMP") || fail_fast "tao giao vien thu hai"
OTHER_ID=${OTHER%%|*}; EXTRA_TEACHERS+=("$OTHER_ID")

section "Dao nguoc quyen so huu"
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop $STAMP\",\"description\":\"Kiem thu mo hinh to chuc\",\"teacherId\":$TEACHER_ID}")
[[ "$code" != "200" ]] && fail_fast "admin tao lop: HTTP $code — $(body)"
GID=$(json_field id); GROUP_IDS+=("$GID")
expect_status "admin lap lop kem giao vien → 200" 200 "$code"
expect_json   "  lop moi ra doi o dang NHAP" isPublished false
expect_json   "  co dung mot giao vien" teachers.0.id "$TEACHER_ID"
expect_status "  khong co giao vien thu hai" 1 \
  "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)["teachers"]))')"
expect_status "giao vien lap lop → 403" 403 \
  "$(call POST /api/student-groups "$TEACHER" "{\"name\":\"Lop cua giao vien $STAMP\",\"teacherId\":$TEACHER_ID}")"

section "Lap lop bat buoc co giao vien phu trach"
expect_status "thieu teacherId → 400" 400 "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop thieu gv $STAMP\"}")"
expect_status "  noi phai chon giao vien" yes "$(body_has 'teacherId')"
expect_status "giao vien khong ton tai → 400" 400 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop gv la $STAMP\",\"teacherId\":999999}")"
expect_status "id cua quan tri vien khong phai giao vien → 400" 400 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop gv admin $STAMP\",\"teacherId\":$ADMIN_ID}")"

LOCKED=$(new_teacher "$ADMIN" "${STAMP}k") || fail_fast "tao giao vien de khoa"
LOCKED_ID=${LOCKED%%|*}; EXTRA_TEACHERS+=("$LOCKED_ID")
call PATCH "/api/teachers/$LOCKED_ID/status" "$ADMIN" "$LOCK_BODY" > /dev/null
expect_status "giao vien dang bi khoa → 409" 409 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop gv khoa $STAMP\",\"teacherId\":$LOCKED_ID}")"
expect_status "  khong de lai lop nao" 200 "$(call GET "/api/student-groups?search=Lop%20gv%20khoa%20$STAMP" "$ADMIN")"
expect_status "    danh sach tim ten ra rong" 0 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"

section "Ban nhap khong ro sang giao vien"
expect_status "danh sach cua giao vien → 200" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "  khong chua lop dang nhap" true \
  "$(body | python -c "import json,sys;print(str(all(g['id'] != $GID for g in json.load(sys.stdin))).lower())")"
# 404 chu khong 403: 403 la xac nhan id do co that, du de do dan danh sach lop dang dung do.
expect_status "doc thang id lop nhap → 404" 404 "$(call GET "/api/student-groups/$GID" "$TEACHER")"

section "Danh sach giao vien de chon"
expect_status "danh sach giao vien → 200" 200 "$(call GET /api/student-groups/assignable-teachers "$ADMIN")"
expect_status "  moi muc deu co trang thai tai khoan" true \
  "$(body | python -c "import json,sys;d=json.load(sys.stdin);print(str(all('status' in t for t in d)).lower())")"
expect_status "  co giao vien demo va ca giao vien dang bi khoa" true \
  "$(body | python -c "import json,sys;d=json.load(sys.stdin);print(str(any(t['id'] == $TEACHER_ID for t in d) and any(t['id'] == $LOCKED_ID and t['status'] == 'locked' for t in d)).lower())")"
expect_status "giao vien xem danh sach → 403" 403 "$(call GET /api/student-groups/assignable-teachers "$TEACHER")"

section "Doi giao vien: giua chung phai ghi ly do"
expect_status "chon lai dung nguoi dang phu trach, khong ly do → 200 (khong co gi de doi)" 200 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$TEACHER_ID}")"
expect_status "doi sang giao vien khac KHONG ly do → 400" 400 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$OTHER_ID}")"
expect_status "  noi phai nhap ly do" yes "$(body_has 'lý do')"
expect_status "ly do qua ngan → 400" 400 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$OTHER_ID,\"reason\":\"ngan\"}")"
expect_status "  van la giao vien cu (khong doi am tham)" 200 "$(call GET "/api/student-groups/$GID" "$ADMIN")"
expect_json   "    giao vien van la nguoi dau" teachers.0.id "$TEACHER_ID"
expect_status "doi sang giao vien dang bi khoa → 409" 409 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$LOCKED_ID,\"reason\":\"Doi sang nguoi dang bi khoa de thu\"}")"
expect_status "giao vien khong ton tai → 400" 400 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":999999,\"reason\":\"Doi sang nguoi khong ton tai\"}")"
expect_status "lop khong ton tai → 404" 404 \
  "$(call PUT "/api/student-groups/999999/teacher" "$ADMIN" "{\"teacherId\":$OTHER_ID,\"reason\":\"Lop khong co that nen khong doi duoc\"}")"
expect_status "giao vien tu doi → 403" 403 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$TEACHER" "{\"teacherId\":$OTHER_ID,\"reason\":\"Giao vien tu doi lop cua minh\"}")"
expect_status "doi sang giao vien khac KEM ly do → 200" 200 \
  "$(call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$OTHER_ID,\"reason\":\"Giao vien cu chuyen truong tu dau thang\"}")"
expect_json   "  giao vien moi" teachers.0.id "$OTHER_ID"
expect_status "  van dung mot giao vien" 1 \
  "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)["teachers"]))')"

section "Lich su cua lop ghi lai tung lan doi, kem ly do"
expect_status "doc lich su → 200" 200 "$(call GET "/api/student-groups/$GID/history" "$ADMIN")"
expect_status "  hai dong: lap lop va doi giao vien" 2 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "  dong moi nhat la lan doi, kem ly do va ten hai giao vien" true \
  "$(body | python -c "
import json,sys
d = json.load(sys.stdin)[0]
print(str(d['kind'] == 'teacherChanged' and d.get('reason') == 'Giao vien cu chuyen truong tu dau thang'
          and bool(d.get('fromTeacher')) and bool(d.get('toTeacher')) and bool(d.get('actorName'))).lower())")"
expect_status "  dong cu nhat la lap lop, khong co ly do" true \
  "$(body | python -c "
import json,sys
d = json.load(sys.stdin)[-1]
print(str(d['kind'] == 'created' and 'reason' not in d and bool(d.get('toTeacher'))).lower())")"
expect_status "giao vien doc lich su → 403" 403 "$(call GET "/api/student-groups/$GID/history" "$TEACHER")"
expect_status "lop khong ton tai → 404" 404 "$(call GET /api/student-groups/999999/history "$ADMIN")"

section "Hai endpoint phan cong nhieu giao vien da bo"
expect_status "POST /teachers → khong con (404/405)" yes \
  "$([[ "$(call POST "/api/student-groups/$GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}")" =~ ^(404|405)$ ]] && echo yes || echo no)"
expect_status "DELETE /teachers/{id} → khong con (404/405)" yes \
  "$([[ "$(call DELETE "/api/student-groups/$GID/teachers/$OTHER_ID" "$ADMIN")" =~ ^(404|405)$ ]] && echo yes || echo no)"

# Doi lai giao vien demo de cac phep kiem sau (giao vien thay lop sau khi cong bo) co nguoi nhin thay.
call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$TEACHER_ID,\"reason\":\"Tra lop ve giao vien demo de kiem tiep\"}" > /dev/null

section "Da phan cong nhung chua cong bo thi van chua thay"
expect_status "  giao vien chua thay lop nhap" no "$(teacher_sees "$GID")"

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

section "Cong bo xong giao vien phu trach moi thay"
expect_status "  giao vien phu trach thay lop" yes "$(teacher_sees "$GID")"
expect_status "doc chi tiet → 200" 200 "$(call GET "/api/student-groups/$GID" "$TEACHER")"
# Doi giao vien sau khi cong bo: nguoi cu mat lop ngay, nguoi moi thay lop (khong can cong bo lai).
call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$OTHER_ID,\"reason\":\"Doi sang giao vien thu hai de kiem pham vi\"}" > /dev/null
expect_status "  giao vien cu khong con thay lop sau khi bi doi" no "$(teacher_sees "$GID")"
expect_status "  doc thang id lop → 404" 404 "$(call GET "/api/student-groups/$GID" "$TEACHER")"
call PUT "/api/student-groups/$GID/teacher" "$ADMIN" "{\"teacherId\":$TEACHER_ID,\"reason\":\"Tra lop ve giao vien demo de kiem tiep\"}" > /dev/null
expect_status "  tra lop ve giao vien demo thi thay lai" yes "$(teacher_sees "$GID")"

section "Duong lui: go cong bo"
# Cong bo nham la chuyen xay ra. Khong co duong lui thi lop ket vinh vien o trang thai sai.
expect_status "go cong bo → 200" 200 "$(call POST "/api/student-groups/$GID/unpublish" "$ADMIN")"
expect_json   "  isPublished ve false" isPublished false
expect_status "  van giu moc cong bo lan dau" present "$(has_field publishedAt)"
expect_status "  giao vien khong con thay" no "$(teacher_sees "$GID")"
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

section "Bai kiem tra cua lop (lich su hoc cua lop: quan tri vien, va giao vien dang day lop da cong bo)"
expect_status "lop moi chua giao bai → 200" 200 "$(call GET "/api/student-groups/$GID/exams" "$ADMIN")"
expect_status "  danh sach rong" 0 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "giao vien dang day lop da cong bo doc → 200, lop chua giao bai nen rong" 200 "$(call GET "/api/student-groups/$GID/exams" "$TEACHER")"
expect_status "  danh sach rong" 0 "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)))')"
expect_status "lop khong ton tai → 404" 404 "$(call GET /api/student-groups/999999/exams "$ADMIN")"

section "Ten lop duy nhat tren toan he thong"
# Truoc day ten chi duy nhat trong pham vi mot giao vien nen hai nguoi deu co the co "Lop 8A".
# Gio lop la don vi hanh chinh cua ca trung tam: hai lop trung ten la hai thu khong ai phan biet
# noi luc giao bai, ma hoc sinh thi chi thuoc duoc mot trong hai.
expect_status "trung ten → 409" 409 "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop $STAMP\",\"teacherId\":$TEACHER_ID}")"

summary
