#!/usr/bin/env bash
# Xoa mem tai khoan hoc sinh, kem ly do va thoi gian cho (§4.2, ban sua doi).
#
# Chay:  bash tests/api-checks/students-deletion.sh   (API phai dang chay o $HOST)
#
# Truoc day he thong CO Y khong co endpoint xoa hoc sinh — chi khoa — voi ly do "ket qua cac bai da
# lam phai tra cuu duoc ve sau". Yeu cau xoa that va yeu cau giu bang diem la hai dieu trai nhau,
# va cach dung hoa la: GIU dong du lieu de moi khoa ngoai con nguyen, nhung XOA SACH du lieu ca
# nhan trong do. Bo kiem nay canh gac dung ranh gioi ay.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@eduquiz.vn" "eduquiz123")
TEACHER=$(login "an.nv@eduquiz.vn" "eduquiz123")

STAMP=$(date +%H%M%S)
STUDENT_IDS=()

cleanup() {
  section "Don du lieu"
  # Tai khoan cho xoa hoac da xoa thi khong khoa duoc nua — de nguyen la dung, chung se tu bien mat.
  printf '  %d tai khoan kiem thu de lai trong he thong (khong xoa duoc bang API — dung thiet ke)\n' \
    "${#STUDENT_IDS[@]}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

new_student() {
  local c; c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"Hoc sinh $1\",\"email\":\"d$1@kiemthu.vn\",\"username\":\"d$1\",\"code\":\"D$1\",\"password\":\"eduquiz123\"}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
  echo "$(json_field id)"
}

section "Dung du lieu nen"
SID=$(new_student "${STAMP}a"); STUDENT_IDS+=("$SID")
printf '  hoc sinh %s\n' "$SID"

section "Ly do la BAT BUOC"
# Mot o ghi chu khong bat buoc thi ai cung bo trong, va tinh nang thanh vo dung dung luc can toi no.
expect_status "thieu ly do → 400" 400 "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{}')"
expect_status "ly do rong → 400" 400 "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":""}')"
expect_status "ly do qua ngan → 400" 400 "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"xoa"}')"
expect_status "ly do 9 ky tu → 400" 400 "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"123456789"}')"

section "Phan quyen"
expect_status "giao vien yeu cau xoa → 403" 403 \
  "$(call POST "/api/students/$SID/request-deletion" "$TEACHER" '{"reason":"Giao vien khong duoc phep lam viec nay"}')"
expect_status "goi khong token → 401" 401 \
  "$(call POST "/api/students/$SID/request-deletion" "" '{"reason":"Khong co token thi khong lam gi duoc"}')"

section "Yeu cau xoa"
STUDENT_TOKEN=$(login "d${STAMP}a" "eduquiz123")
expect_status "truoc khi xoa, hoc sinh dang nhap duoc" 200 "$(call GET /api/auth/me "$STUDENT_TOKEN")"
expect_status "admin yeu cau xoa → 200" 200 \
  "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"Hoc sinh chuyen truong tu dau hoc ky hai."}')"
expect_json   "  status = pendingDeletion" status pendingDeletion
# Xoay SecurityStamp: khong xoay thi mot em vua bi dua vao dien xoa van lam bai tiep den khi token het han.
#
# 403 chu khong 401, va do la co y: token van hop le ve mat mat ma, nguoi goi van duoc dinh danh —
# thu chan ho lai la TRANG THAI tai khoan. Tra 401 trong o day se chi noi "phien het han", con 403
# kem cau chu cu the cho hoc sinh biet phai hoi ai. Cung khuon voi nhanh khoa tai khoan o step2.
expect_status "  token dang cam chet ngay → 403 kem thong diep ro rang" 403 "$(call GET /api/auth/me "$STUDENT_TOKEN")"
expect_status "  dang nhap lai → 403" 403 \
  "$(call POST /api/auth/login "" "{\"username\":\"d${STAMP}a\",\"password\":\"eduquiz123\"}")"
expect_status "yeu cau xoa lan hai → 409" 409 \
  "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"Lan nay la thao tac trung lap"}')"

section "Khoi phuc trong thoi gian cho"
expect_status "admin khoi phuc → 200" 200 "$(call POST "/api/students/$SID/restore" "$ADMIN")"
expect_json   "  status ve active" status active
expect_status "  dang nhap lai duoc → 200" 200 \
  "$(call POST /api/auth/login "" "{\"username\":\"d${STAMP}a\",\"password\":\"eduquiz123\"}")"
expect_status "khoi phuc tai khoan dang hoat dong → 409" 409 "$(call POST "/api/students/$SID/restore" "$ADMIN")"
expect_status "giao vien khoi phuc → 403" 403 "$(call POST "/api/students/$SID/restore" "$TEACHER")"

section "Xoa lai de kiem trang thai cuoi"
expect_status "yeu cau xoa lan nua → 200" 200 \
  "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"Xac nhan lai quyet dinh chuyen truong."}')"
expect_json   "  status = pendingDeletion" status pendingDeletion
# Ly do cua lan truoc phai bi xoa khi khoi phuc, neu khong lan xoa sau mang theo ly do cu.
expect_status "  doc lai ho so → 200" 200 "$(call GET "/api/students/$SID" "$ADMIN")"

section "Tai khoan khong ton tai"
expect_status "yeu cau xoa id la → 404" 404 \
  "$(call POST "/api/students/999999/request-deletion" "$ADMIN" '{"reason":"Id nay khong ton tai trong he thong"}')"
expect_status "khoi phuc id la → 404" 404 "$(call POST "/api/students/999999/restore" "$ADMIN")"

summary
