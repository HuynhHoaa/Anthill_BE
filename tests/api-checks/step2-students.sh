#!/usr/bin/env bash
# Buoc 2 — Hoc sinh va nhom lop (§4.2, §4.3).
#
# Chay:  bash tests/api-checks/step2-students.sh   (API phai dang chay o $HOST)
#
# Bo kiem cua buoc nay tung duoc chay tay roi mat, khong con gi chay lai duoc. Day la ban dung lai.
# Script tu dung du lieu roi tu don trong trap EXIT.
#
# LUU Y: tai khoan hoc sinh co y KHONG co endpoint xoa (§4.2 — khoa chu khong xoa, de ket qua cu
# van tra cuu duoc). Nen phan don chi go khoi nhom roi khoa lai.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

STAMP=$(date +%H%M%S)
STUDENT_IDS=(); GROUP_IDS=()

cleanup() {
  section "Don du lieu"
  local members=0 groups=0 locked=0
  for id in "${STUDENT_IDS[@]}"; do
    for gid in "${GROUP_IDS[@]}"; do
      [[ "$(call DELETE "/api/student-groups/$gid/students/$id" "$ADMIN" "$REMOVE_BODY")" == "200" ]] && members=$((members + 1))
    done
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" "$LOCK_BODY")" == "200" ]] && locked=$((locked + 1))
  done
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1))
  done
  printf '  go %d thanh vien · khoa %d/%d hoc sinh · xoa %d/%d nhom\n' \
    "$members" "$locked" "${#STUDENT_IDS[@]}" "$groups" "${#GROUP_IDS[@]}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

section "Phan quyen — ai lam duoc gi (§4.2)"
expect_status "hoc sinh goi /students → 403" 403 "$(call GET /api/students "$STUDENT")"
# Doc duoc, nhung chi trong pham vi lop minh day — ranh gioi do do teacher-student-scope.sh canh.
expect_status "giao vien DOC duoc /students (lop minh day) → 200" 200 "$(call GET /api/students "$TEACHER")"
expect_status "giao vien TAO tai khoan → 403" 403 \
  "$(call POST /api/students "$TEACHER" '{"fullName":"X","email":"x@x.vn","username":"xx","code":"XX1","password":"123456"}')"
# DAO NGUOC so voi mo hinh cu: truoc day giao vien so huu lop con quan tri vien chi duoc doc.
# Ly do doi nam o luoc do — User.GroupId la khoa ngoai don nen mot hoc sinh thuoc dung mot lop;
# de giao vien so huu lop thi hai nguoi cung day mot em se gianh nhau em do.
expect_status "quan tri vien TAO lop → 200" 200 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop cua admin $STAMP\",\"teacherId\":$DEMO_TEACHER_ID}")"
expect_status "giao vien TAO lop → 403" 403 \
  "$(call POST /api/student-groups "$TEACHER" "{\"name\":\"Lop cua giao vien $STAMP\"}")"
expect_status "goi khong token → 401" 401 "$(call GET /api/students "")"

section "Dung du lieu nen"
GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KT2 $STAMP" "Kiem thu buoc 2")   || fail_fast "khong dung duoc lop nen"
GROUP_IDS+=("$GROUP_ID")

NEW_ID=""
new_student() {
  local suffix="$1" group="$2" payload
  payload="{\"fullName\":\"Nguyen Thi Kiem Thu $suffix\",\"email\":\"kt$suffix@kiemthu.vn\",\"username\":\"kt$suffix\",\"code\":\"KT$suffix\",\"password\":\"123456\""
  [[ -n "$group" ]] && payload="$payload,\"groupId\":$group"
  payload="$payload}"
  local c; c=$(call POST /api/students "$ADMIN" "$payload")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $c — $(body)"
  NEW_ID=$(json_field id); STUDENT_IDS+=("$NEW_ID")
}

new_student "${STAMP}a" "$GROUP_ID"; S1=$NEW_ID
new_student "${STAMP}b" ""; S2=$NEW_ID
printf '  nhom %s · 2 hoc sinh\n' "$GROUP_ID"

section "Trung dinh danh → 409, khong phai 400"
dup() {
  call POST /api/students "$ADMIN" \
    "{\"fullName\":\"Trung\",\"email\":\"$1\",\"username\":\"$2\",\"code\":\"$3\",\"password\":\"123456\"}"
}
expect_status "trung email → 409"    409 "$(dup "kt${STAMP}a@kiemthu.vn" "khac1$STAMP" "KHAC1$STAMP")"
expect_status "trung username → 409" 409 "$(dup "khac1$STAMP@kiemthu.vn" "kt${STAMP}a" "KHAC2$STAMP")"
expect_status "trung ma hoc sinh → 409" 409 "$(dup "khac2$STAMP@kiemthu.vn" "khac2$STAMP" "KT${STAMP}a")"
# Trim truoc khi so: ma co khoang trang hai dau van phai bi chan.
expect_status "ma hoc sinh co khoang trang hai dau van bi chan → 409" 409 \
  "$(dup "khac3$STAMP@kiemthu.vn" "khac3$STAMP" "  KT${STAMP}a  ")"

section "Sua ho so — khong tu bao trung voi chinh minh"
expect_status "luu lai ma khong doi email → 200" 200 \
  "$(call PUT "/api/students/$S1" "$ADMIN" "{\"fullName\":\"Nguyen Thi Kiem Thu $STAMP\",\"email\":\"kt${STAMP}a@kiemthu.vn\",\"username\":\"kt${STAMP}a\",\"code\":\"KT${STAMP}a\",\"groupId\":$GROUP_ID}")"

section "Nhom lop"
# Ten lop duy nhat tren TOAN he thong, khong con trong pham vi tung giao vien.
expect_status "trung ten lop tren toan he thong → 409" 409 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"KT2 $STAMP\",\"teacherId\":$DEMO_TEACHER_ID}")"
expect_status "xoa lop con nguoi → 409" 409 "$(call DELETE "/api/student-groups/$GROUP_ID" "$ADMIN")"
expect_status "giao vien xoa lop → 403" 403 "$(call DELETE "/api/student-groups/$GROUP_ID" "$TEACHER")"
# §4.2: nhom cua giao vien khac tra 404 chu khong 403 — 403 la xac nhan id do co that.
expect_status "nhom khong ton tai → 404" 404 "$(call GET /api/student-groups/999999 "$TEACHER")"
expect_status "danh sach thanh vien → 200" 200 "$(call GET "/api/student-groups/$GROUP_ID/students" "$TEACHER")"

section "Them thanh vien theo lo — hong mot phan van chay"
# 1 them duoc + 1 id lap bi khu + 1 id la.
expect_status "them lo → 200" 200 \
  "$(call POST "/api/student-groups/$GROUP_ID/students" "$ADMIN" "{\"studentIds\":[$S2,$S2,999999]}")"
expect_json   "  added = 1"           added 1
expect_json   "  skippedNotFound = 1" skippedNotFound 1
expect_status "danh sach rong → 400" 400 \
  "$(call POST "/api/student-groups/$GROUP_ID/students" "$ADMIN" '{"studentIds":[]}')"
expect_status "studentIds = null → 400, khong phai 500" 400 \
  "$(call POST "/api/student-groups/$GROUP_ID/students" "$ADMIN" '{"studentIds":null}')"

section "Phan trang va tim kiem"
expect_status "pageSize=500 bi kep, khong bao loi" 200 "$(call GET "/api/students?pageSize=500" "$ADMIN")"
expect_json   "  pageSize kep ve 200" pageSize 200
expect_status "page=0 duoc keo ve 1" 200 "$(call GET "/api/students?page=0" "$ADMIN")"
expect_json   "  page = 1" page 1
# Tim tieng Viet bo dau: go 'kiem thu' phai ra 'Nguyen Thi Kiem Thu'.
expect_status "tim khong dau → 200" 200 "$(call GET "/api/students?search=kiem%20thu" "$ADMIN")"
expect_status "  co ket qua" true \
  "$(body | python -c 'import json,sys;print(str(len(json.load(sys.stdin)["items"])>0).lower())')"

section "Chua phan nhom — groupId 0 doi ung NULL"
expect_status "loc groupId=0 → 200" 200 "$(call GET "/api/students?groupId=0" "$ADMIN")"

section "Khoa tai khoan lam token cu chet ngay"
VICTIM_TOKEN=$(login "kt${STAMP}b" "123456")
expect_status "token vua cap dung duoc → 200" 200 "$(call GET /api/auth/me "$VICTIM_TOKEN")"
expect_status "khoa tai khoan → 200" 200 "$(call PATCH "/api/students/$S2/status" "$ADMIN" "$LOCK_BODY")"
# 403 chu khong 401, va day la phan biet co chu dich:
#   - Tai khoan bi khoa  -> 403. Server BIET ban la ai, chi la ban khong duoc vao. Thong diep noi
#     ro phai lien he quan tri vien, vi dang nhap lai cung khong giai quyet duoc gi.
#   - Doi mat khau       -> 401 (xem muc duoi). Token khong con hop le, dang nhap lai la xong.
# ErrorCode.Forbidden trong Result.cs ghi thang: "403 ... ke ca tai khoan bi khoa".
expect_status "  token cu chet ngay → 403 kem thong diep ro rang" 403 "$(call GET /api/auth/me "$VICTIM_TOKEN")"
expect_json   "    noi ro la bi khoa" message "Tài khoản đã bị khoá, liên hệ quản trị viên."
expect_status "  dang nhap lai → 403" 403 \
  "$(call POST /api/auth/login "" "{\"username\":\"kt${STAMP}b\",\"password\":\"123456\"}")"
expect_status "mo khoa lai → 200" 200 "$(call PATCH "/api/students/$S2/status" "$ADMIN" '{"status":"active"}')"

section "Doi mat khau xoay SecurityStamp trong khi tai khoan van active"
LIVE_TOKEN=$(login "kt${STAMP}a" "123456")
expect_status "token dung duoc → 200" 200 "$(call GET /api/auth/me "$LIVE_TOKEN")"
expect_status "reset mat khau → 200" 200 "$(call POST "/api/students/$S1/reset-password" "$ADMIN")"
NEW_PASSWORD=$(json_field password)
expect_status "  token cu chet → 401" 401 "$(call GET /api/auth/me "$LIVE_TOKEN")"
expect_status "  mat khau cu → 401" 401 \
  "$(call POST /api/auth/login "" "{\"username\":\"kt${STAMP}a\",\"password\":\"123456\"}")"
expect_status "  mat khau moi dung duoc → 200" 200 \
  "$(call POST /api/auth/login "" "{\"username\":\"kt${STAMP}a\",\"password\":\"$NEW_PASSWORD\"}")"

summary
