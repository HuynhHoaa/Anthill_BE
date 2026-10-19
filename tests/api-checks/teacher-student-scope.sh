#!/usr/bin/env bash
# Giao vien chi nhin thay hoc sinh minh phu trach (§4.2, §4.3).
#
# Chay:  bash tests/api-checks/teacher-student-scope.sh   (API phai dang chay o $HOST)
#
# Truoc ban sua nay, GET /api/students va GET /api/students/{id} khong gioi han gi voi giao vien:
# giao vien nao cung doc duoc ho so moi hoc sinh cua ca truong. Quy tac bay gio:
#   - DANH SACH: chi hoc sinh o lop giao vien duoc phan cong VA lop da cong bo. Loc theo lop cua
#     nguoi khac, hay "chua phan nhom", chi ra trang rong — khong loi, khong ro.
#   - HO SO: nhu tren, cong them em da tung nhan de do chinh giao vien nay tao — de lien ket tu
#     trang ket qua van mo duoc ho so em da chuyen lop. Ngoai pham vi → 404, khong phai 403.
#   - Quan tri vien: khong doi gi.
# Bo kiem nay dung mot lop THU HAI khong lien quan de chung minh tung vach ranh gioi.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

# Co ca ngay trong nam: tai khoan cua lan chay truoc con nam lai (khong xoa cung duoc).
STAMP=$(date +%j%H%M%S)
MINE=""; FOREIGN=""; DRAFT=""; EID=""; OTHER_ID=""
STUDENT_IDS=()

cleanup() {
  section "Don du lieu"
  [[ -n "$EID" ]] && call DELETE "/api/exams/$EID" "$TEACHER" > /dev/null
  for id in "${STUDENT_IDS[@]}"; do
    for gid in $MINE $FOREIGN $DRAFT; do call DELETE "/api/student-groups/$gid/students/$id" "$ADMIN" "$REMOVE_BODY" > /dev/null; done
    call PATCH "/api/students/$id/status" "$ADMIN" "$LOCK_BODY" > /dev/null
  done
  for gid in $MINE $FOREIGN $DRAFT; do call DELETE "/api/student-groups/$gid" "$ADMIN" > /dev/null; done
  # Giao vien thu hai khong xoa duoc (so huu lop va de): khoa lai, kem ly do.
  [[ -n "$OTHER_ID" ]] && retire_teacher "$ADMIN" "$OTHER_ID"
  printf '  da don %d hoc sinh (khoa, khong xoa duoc — dung thiet ke), 3 lop, 1 giao vien thu hai (khoa)\n' "${#STUDENT_IDS[@]}"
}

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

HELPERS=$(mktemp -t anthill-scope.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
"""Phep tinh nho cho bo kiem, tach ra file rieng de khoi long trich dan trong shell."""
import json
import sys

mode = sys.argv[1]
with open(sys.argv[2], encoding="utf-8") as handle:
    data = json.load(handle)

rows = data["items"] if isinstance(data, dict) and "items" in data else data

if mode == "contains_id":
    print("yes" if any(row["id"] == int(sys.argv[3]) for row in rows) else "no")
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

contains_id() { python "$HELPERS" contains_id "$(to_native_path "$BODY_FILE")" "$1"; }

new_student() {
  local suffix="$1" group="$2" payload code
  payload="{\"fullName\":\"HS pham vi $suffix\",\"email\":\"sc$suffix@kiemthu.vn\",\"username\":\"sc$suffix\",\"code\":\"SC$suffix\",\"password\":\"123456\""
  [[ -n "$group" ]] && payload="$payload,\"groupId\":$group"
  payload="$payload}"
  code=$(call POST /api/students "$ADMIN" "$payload")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  NEW_ID=$(json_field id); STUDENT_IDS+=("$NEW_ID")
}

section "Dung du lieu nen"
# Lop cua giao vien: lap → phan cong → cong bo.
MINE=$(new_class "$ADMIN" "$TEACHER" "Pham vi cua toi $STAMP") || fail_fast "tao lop cua giao vien"
# Lop khong lien quan: da cong bo nhung do MOT GIAO VIEN KHAC phu trach (moi lop co dung mot giao vien, nen "khong duoc
# phan cong" nghia la lop cua nguoi khac).
OTHER=$(new_teacher "$ADMIN" "$STAMP") || fail_fast "tao giao vien thu hai"
OTHER_ID=${OTHER%%|*}
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Pham vi lop khac $STAMP\",\"teacherId\":$OTHER_ID}")
[[ "$code" != "200" ]] && fail_fast "tao lop khac: HTTP $code — $(body)"
FOREIGN=$(json_field id)
code=$(call POST "/api/student-groups/$FOREIGN/publish" "$ADMIN")
[[ "$code" != "200" ]] && fail_fast "cong bo lop khac: HTTP $code — $(body)"
# Lop nhap: giao vien nay phu trach nhung lop chua cong bo.
call GET /api/auth/me "$TEACHER" > /dev/null; TEACHER_ID=$(json_field id)
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Pham vi lop nhap $STAMP\",\"teacherId\":$TEACHER_ID}")
[[ "$code" != "200" ]] && fail_fast "tao lop nhap: HTTP $code — $(body)"
DRAFT=$(json_field id)

new_student "${STAMP}a" "$MINE";    S_MINE=$NEW_ID
new_student "${STAMP}b" "$MINE";    S_MOVER=$NEW_ID
new_student "${STAMP}c" "$FOREIGN"; S_FOREIGN=$NEW_ID
new_student "${STAMP}d" "$DRAFT";   S_DRAFT=$NEW_ID
new_student "${STAMP}e" "";         S_NONE=$NEW_ID
printf '  lop cua toi %s · lop khac %s · lop nhap %s · 5 hoc sinh\n' "$MINE" "$FOREIGN" "$DRAFT"

LIST="/api/students?search=SC$STAMP&pageSize=200"

section "Danh sach cua giao vien chi gom lop minh day"
expect_status "giao vien doc danh sach → 200" 200 "$(call GET "$LIST" "$TEACHER")"
expect_status "  co em o lop cua minh"                yes "$(contains_id "$S_MINE")"
expect_status "  co em thu hai o lop cua minh"        yes "$(contains_id "$S_MOVER")"
expect_status "  khong co em o lop khac"              no  "$(contains_id "$S_FOREIGN")"
expect_status "  khong co em o lop con la ban nhap"   no  "$(contains_id "$S_DRAFT")"
expect_status "  khong co em chua phan lop"           no  "$(contains_id "$S_NONE")"
expect_json   "  total dem dung 2 em, khong dem lan ngoai pham vi" total 2

section "Loc ngoai pham vi → trang rong, khong loi"
expect_status "groupId cua lop khac → 200" 200 "$(call GET "/api/students?groupId=$FOREIGN" "$TEACHER")"
expect_json   "  total = 0" total 0
expect_status "groupId cua lop nhap → 200" 200 "$(call GET "/api/students?groupId=$DRAFT" "$TEACHER")"
expect_json   "  total = 0" total 0
expect_status "groupId = 0 (chua phan nhom) → 200" 200 "$(call GET "$LIST&groupId=0" "$TEACHER")"
expect_json   "  total = 0" total 0
expect_status "groupId cua lop minh → 200" 200 "$(call GET "/api/students?groupId=$MINE" "$TEACHER")"
expect_json   "  total = 2" total 2

section "Loc trang thai va tu khoa van chay trong pham vi"
code=$(call PATCH "/api/students/$S_FOREIGN/status" "$ADMIN" "$LOCK_BODY")
[[ "$code" != "200" ]] && fail_fast "khoa em lop khac: HTTP $code"
code=$(call PATCH "/api/students/$S_MINE/status" "$ADMIN" "$LOCK_BODY")
[[ "$code" != "200" ]] && fail_fast "khoa em lop minh: HTTP $code"
expect_status "loc status=locked → 200" 200 "$(call GET "$LIST&status=locked" "$TEACHER")"
expect_status "  co em bi khoa o lop minh"             yes "$(contains_id "$S_MINE")"
expect_status "  khong co em bi khoa o lop khac"       no  "$(contains_id "$S_FOREIGN")"
expect_status "loc status=active → 200" 200 "$(call GET "$LIST&status=active" "$TEACHER")"
expect_status "  chi con em dang hoat dong o lop minh" yes "$(contains_id "$S_MOVER")"
expect_json   "  total = 1" total 1
expect_status "tu khoa trung em lop khac → 200" 200 "$(call GET "/api/students?search=SC${STAMP}c" "$TEACHER")"
expect_json   "  total = 0" total 0
expect_status "loc gia tri la van → 400" 400 "$(call GET "/api/students?status=xyz" "$TEACHER")"

section "Ho so ngoai pham vi → 404, giong het id khong ton tai"
expect_status "ho so em lop minh → 200" 200 "$(call GET "/api/students/$S_MINE" "$TEACHER")"
expect_json   "  dung em do" id "$S_MINE"
expect_status "id khong ton tai → 404" 404 "$(call GET /api/students/999999 "$TEACHER")"
MISSING_MESSAGE=$(json_field message)
expect_status "ho so em lop khac → 404" 404 "$(call GET "/api/students/$S_FOREIGN" "$TEACHER")"
expect_json   "  cung thong diep voi id khong ton tai" message "$MISSING_MESSAGE"
expect_status "ho so em lop nhap → 404" 404 "$(call GET "/api/students/$S_DRAFT" "$TEACHER")"
expect_status "ho so em chua phan lop → 404" 404 "$(call GET "/api/students/$S_NONE" "$TEACHER")"
# Id cua mot giao vien khong bao gio la hoc sinh — cung la 404.
expect_status "id cua chinh giao vien → 404" 404 "$(call GET "/api/students/$TEACHER_ID" "$TEACHER")"

section "Em da nhan de cua minh roi chuyen lop: ho so van mo, danh sach thi khong"
code=$(call POST /api/exams "$TEACHER" "$(cat <<JSON
{
  "title": "De pham vi $STAMP", "topic": "", "durationMinutes": 45, "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22, "pointEasy": 10, "pointMedium": 10, "pointHard": 15,
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": true,
  "openAt": "2020-01-01T00:00:00Z", "closeAt": "2035-01-01T00:00:00Z"
}
JSON
)")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EID=$(json_field id)
# Khung gio cua lop da dong tu 4 gio truoc: em chua lam bai nao nen bai hien la "het han", va chuyen lop KHONG thu hoi
# no (chi thu hoi bai chua bat dau va chua het han). Bai het han van la bai em da nhan tu giao vien nay, nen ho so em van
# mo duoc sau khi em chuyen di. Bai con mo thi chuyen lop se thu hoi, giao vien khong con duoc thay em — xem class-membership.sh.
OPEN_PAST=$(date -u -d '-4 hours' +%Y-%m-%dT%H:%M:%SZ); CLOSE_PAST=$(date -u -d '-2 hours' +%Y-%m-%dT%H:%M:%SZ)
expect_status "giao bai cho lop minh (khung da dong) → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[{\"groupId\":$MINE,\"openAt\":\"$OPEN_PAST\",\"closeAt\":\"$CLOSE_PAST\"}],\"notify\":false}")"
expect_json   "  giao cho em dang hoat dong" assignedCount 1
expect_status "quan tri vien chuyen em sang lop khac (co ly do) → 200" 200 \
  "$(call POST "/api/student-groups/$MINE/students/$S_MOVER/transfer" "$ADMIN" "{\"targetGroupId\":$FOREIGN,\"reason\":\"Xep nham lop tu dau nam hoc\"}")"
expect_status "  da o lop khac" "$FOREIGN" "$(call GET "/api/students/$S_MOVER" "$ADMIN" > /dev/null; json_field groups.0.id)"
expect_status "giao vien van mo duoc ho so em do → 200" 200 "$(call GET "/api/students/$S_MOVER" "$TEACHER")"
# Giao vien chi thay nhung lop CUA MINH trong danh sach lop cua em; lop moi la lop cua dong nghiep nen khong hien ra.
expect_json   "  ho so khong lo lop cua dong nghiep" groups "[]"
expect_status "danh sach cua giao vien → 200" 200 "$(call GET "$LIST" "$TEACHER")"
expect_status "  em da chuyen di khong con trong danh sach" no "$(contains_id "$S_MOVER")"
# Luat giao bai chi mo ho so, khong mo lop: loc theo lop moi cua em van rong.
expect_status "loc theo lop moi cua em → 200" 200 "$(call GET "/api/students?groupId=$FOREIGN" "$TEACHER")"
expect_json   "  total = 0" total 0
# Em cung lop moi nhung chua tung nhan de cua giao vien nay thi van dong cua.
expect_status "ban cung lop moi chua nhan de → 404" 404 "$(call GET "/api/students/$S_FOREIGN" "$TEACHER")"

section "Quan tri vien van thay tat ca"
expect_status "danh sach cua quan tri vien → 200" 200 "$(call GET "$LIST" "$ADMIN")"
expect_json   "  du 5 em" total 5
expect_status "  co em chua phan lop" yes "$(contains_id "$S_NONE")"
expect_status "  co em lop nhap"       yes "$(contains_id "$S_DRAFT")"
expect_status "loc groupId = 0 → 200" 200 "$(call GET "$LIST&groupId=0" "$ADMIN")"
expect_status "  co em chua phan lop" yes "$(contains_id "$S_NONE")"
expect_status "ho so em lop khac → 200" 200 "$(call GET "/api/students/$S_FOREIGN" "$ADMIN")"
expect_status "ho so em lop nhap → 200" 200 "$(call GET "/api/students/$S_DRAFT" "$ADMIN")"

summary
