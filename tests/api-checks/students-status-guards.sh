#!/usr/bin/env bash
# Hang rao quanh trang thai tai khoan hoc sinh (§4.2, ban sua doi).
#
# Chay:  bash tests/api-checks/students-status-guards.sh   (API phai dang chay o $HOST)
#
# Quy trinh xoa co hai loi di rieng — request-deletion va restore — vi moi loi phai lam them viec:
# ghi ly do, xoay SecurityStamp, xoa dau vet khi khoi phuc. Truoc ban sua nay, PATCH status va PUT
# nhan thang ca bon trang thai, nen:
#   - dat "deleted" la bo qua buoc an danh hoa: tai khoan mang nhan da xoa ma ho ten, email van con;
#   - dat "active" cho tai khoan cho xoa la bo qua buoc khoi phuc, de lai ly do va moc dem nguoc cu;
#   - cap lai mat khau duoc cho tai khoan cho xoa hoac da xoa;
#   - "Them hoc sinh vao lop" liet ke va nhan ca tai khoan da roi truong.
# Bo kiem nay canh tung cua sau do.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@eduquiz.vn" "eduquiz123")
TEACHER=$(login "an.nv@eduquiz.vn" "eduquiz123")

STAMP=$(date +%H%M%S)
SID=""; OTHER=""; GID=""
CID=""; LEAVER=""; STAYER=""; LONE=""; LONE_GID=""; EID=""

cleanup() {
  section "Don du lieu"
  [[ -n "$EID" ]] && call DELETE "/api/exams/$EID" "$TEACHER" > /dev/null
  # Tai khoan cho xoa khong khoa duoc nua (dung la dieu bo kiem nay chung minh), nen khoi phuc truoc.
  for id in $SID $OTHER $LEAVER $STAYER $LONE; do
    call POST "/api/students/$id/restore" "$ADMIN" > /dev/null
    for gid in $GID $CID; do call DELETE "/api/student-groups/$gid/students/$id" "$ADMIN" > /dev/null; done
    call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null
  done
  for gid in $GID $CID $LONE_GID; do call DELETE "/api/student-groups/$gid" "$ADMIN" > /dev/null; done
  printf '  da don\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

HELPERS=$(mktemp -t eduquiz-guards.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
"""Phep tinh nho cho bo kiem, tach ra file rieng de khoi long trich dan trong shell."""
import datetime
import json
import sys

mode = sys.argv[1]
with open(sys.argv[2], encoding="utf-8") as handle:
    data = json.load(handle)


def parse(text):
    return datetime.datetime.fromisoformat(text.replace("Z", "+00:00"))


if mode == "has":
    print("present" if data.get(sys.argv[3]) is not None else "absent")

elif mode == "days_between":
    # So ngay giua hai truong thoi gian cua cung mot ban ghi.
    first, second = data.get(sys.argv[3]), data.get(sys.argv[4])
    if first is None or second is None:
        print("absent")
    else:
        print(round((parse(second) - parse(first)).total_seconds() / 86400))

elif mode == "contains_id":
    rows = data["items"] if isinstance(data, dict) else data
    print("yes" if any(row["id"] == int(sys.argv[3]) for row in rows) else "no")

elif mode == "field_by_id":
    # Mot truong cua dong co id cho truoc, trong danh sach co hoac khong phan trang.
    rows = data["items"] if isinstance(data, dict) else data
    match = next((row for row in rows if row["id"] == int(sys.argv[3])), None)
    print("absent" if match is None else match.get(sys.argv[4]))
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

has_field()    { python "$HELPERS" has "$(to_native_path "$BODY_FILE")" "$1"; }
days_between() { python "$HELPERS" days_between "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
contains_id()  { python "$HELPERS" contains_id "$(to_native_path "$BODY_FILE")" "$1"; }
field_by_id()  { python "$HELPERS" field_by_id "$(to_native_path "$BODY_FILE")" "$1" "$2"; }

section "Dung du lieu nen"
code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop rao $STAMP\"}")
[[ "$code" != "200" ]] && fail_fast "tao lop: HTTP $code — $(body)"
GID=$(json_field id)

new_student() {
  local code
  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS rao $1\",\"email\":\"g$1@kiemthu.vn\",\"username\":\"g$1\",\"code\":\"G$1\",\"password\":\"eduquiz123\"}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $1: HTTP $code — $(body)"
  NEW_ID=$(json_field id)
}
new_student "${STAMP}a"; SID=$NEW_ID
new_student "${STAMP}b"; OTHER=$NEW_ID
printf '  lop %s · hoc sinh %s va %s\n' "$GID" "$SID" "$OTHER"

section "Chi dat thang duoc active va locked"
expect_status "PATCH status=deleted → 400" 400 \
  "$(call PATCH "/api/students/$OTHER/status" "$ADMIN" '{"status":"deleted"}')"
expect_status "PATCH status=pendingDeletion → 400" 400 \
  "$(call PATCH "/api/students/$OTHER/status" "$ADMIN" '{"status":"pendingDeletion"}')"
expect_status "tao hoc sinh voi status=deleted → 400" 400 \
  "$(call POST /api/students "$ADMIN" "{\"fullName\":\"X\",\"email\":\"x$STAMP@kiemthu.vn\",\"username\":\"x$STAMP\",\"code\":\"X$STAMP\",\"password\":\"eduquiz123\",\"status\":\"deleted\"}")"
expect_status "khoa van dung binh thuong → 200" 200 \
  "$(call PATCH "/api/students/$OTHER/status" "$ADMIN" '{"status":"locked"}')"
expect_json   "  status = locked" status locked
expect_status "mo khoa van dung binh thuong → 200" 200 \
  "$(call PATCH "/api/students/$OTHER/status" "$ADMIN" '{"status":"active"}')"

section "Tai khoan cho xoa khong di vong qua buoc khoi phuc duoc"
expect_status "yeu cau xoa → 200" 200 \
  "$(call POST "/api/students/$SID/request-deletion" "$ADMIN" '{"reason":"Hoc sinh chuyen truong tu hoc ky hai."}')"
expect_json   "  status = pendingDeletion" status pendingDeletion

# Truoc ban sua: 200, va tai khoan ve active nhung van mang ly do va moc dem nguoc cu.
expect_status "PATCH status=active len tai khoan cho xoa → 409" 409 \
  "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"active"}')"
expect_status "PATCH status=locked len tai khoan cho xoa → 409" 409 \
  "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"locked"}')"
expect_status "sua ho so tai khoan cho xoa → 409" 409 \
  "$(call PUT "/api/students/$SID" "$ADMIN" "{\"fullName\":\"Doi ten\",\"email\":\"g${STAMP}a@kiemthu.vn\",\"username\":\"g${STAMP}a\",\"code\":\"G${STAMP}a\",\"groupId\":0}")"
expect_status "cap lai mat khau cho tai khoan cho xoa → 409" 409 \
  "$(call POST "/api/students/$SID/reset-password" "$ADMIN")"
expect_status "van con dung trang thai cho xoa" 200 "$(call GET "/api/students/$SID" "$ADMIN")"
expect_json   "  status = pendingDeletion" status pendingDeletion

section "StudentDto mang dau vet cua quy trinh xoa"
expect_status "doc ho so → 200" 200 "$(call GET "/api/students/$SID" "$ADMIN")"
expect_status "  co deletionReason"      present "$(has_field deletionReason)"
expect_status "  co deletionRequestedAt" present "$(has_field deletionRequestedAt)"
expect_status "  co deletionScheduledAt" present "$(has_field deletionScheduledAt)"
# Server tinh san moc xoa vinh vien; giao dien khong tu cong 60 ngay.
expect_status "  moc xoa vinh vien = moc yeu cau + 60 ngay" 60 \
  "$(days_between deletionRequestedAt deletionScheduledAt)"
expect_status "hoc sinh binh thuong khong co truong xoa → 200" 200 "$(call GET "/api/students/$OTHER" "$ADMIN")"
expect_status "  khong co deletionScheduledAt" absent "$(has_field deletionScheduledAt)"

section "Tai khoan cho xoa khong quay lai lop duoc"
expect_status "danh sach ung vien them vao lop → 200" 200 \
  "$(call GET "/api/student-groups/$GID/assignable-students" "$ADMIN")"
expect_status "  khong liet ke tai khoan cho xoa" no "$(contains_id "$SID")"
expect_status "  van liet ke tai khoan binh thuong" yes "$(contains_id "$OTHER")"
expect_status "them lo gom ca tai khoan cho xoa → 200" 200 \
  "$(call POST "/api/student-groups/$GID/students" "$ADMIN" "{\"studentIds\":[$SID,$OTHER]}")"
expect_json   "  chi them tai khoan binh thuong" added 1
expect_json   "  dem rieng tai khoan bi bo qua" skippedRetired 1

section "O loc van tim ra duoc moi trang thai"
expect_status "loc status=pendingDeletion → 200" 200 \
  "$(call GET "/api/students?status=pendingDeletion&pageSize=200" "$ADMIN")"
expect_status "  co tai khoan cho xoa" yes "$(contains_id "$SID")"
expect_status "loc gia tri la → 400" 400 "$(call GET "/api/students?status=xyz" "$ADMIN")"

section "Si so khong tinh tai khoan cho xoa"
# Hai em vao lop khi con binh thuong, roi mot em roi truong. Em do van giu GroupId — khoi phuc la
# ve dung lop cu — nhung khong con la si so: dem vao thi cau "giao bai cho N hoc sinh" sai ngay.
CID=$(new_class "$ADMIN" "$TEACHER" "Siso$STAMP") || fail_fast "tao lop cong bo"
new_student "${STAMP}c"; LEAVER=$NEW_ID
new_student "${STAMP}d"; STAYER=$NEW_ID
expect_status "xep hai em vao lop → 200" 200 \
  "$(call POST "/api/student-groups/$CID/students" "$ADMIN" "{\"studentIds\":[$LEAVER,$STAYER]}")"
expect_json   "  si so = 2" studentCount 2
expect_status "mot em roi truong → 200" 200 \
  "$(call POST "/api/students/$LEAVER/request-deletion" "$ADMIN" '{"reason":"Hoc sinh chuyen truong giua hoc ky."}')"
expect_status "doc lop → 200" 200 "$(call GET "/api/student-groups/$CID" "$ADMIN")"
expect_json   "  si so con 1" studentCount 1
expect_status "danh sach lop cua quan tri vien → 200" 200 \
  "$(call GET "/api/student-groups?search=Siso$STAMP&pageSize=100" "$ADMIN")"
expect_status "  si so trong danh sach cung la 1" 1 "$(field_by_id "$CID" studentCount)"
expect_status "danh sach lop cua giao vien → 200" 200 \
  "$(call GET "/api/student-groups?search=Siso$STAMP&pageSize=100" "$TEACHER")"
expect_status "  giao vien cung thay si so 1" 1 "$(field_by_id "$CID" studentCount)"
# Van hien trong danh sach thanh vien: quan tri vien can thay em do de con khoi phuc duoc.
expect_status "thanh vien lop → 200" 200 "$(call GET "/api/student-groups/$CID/students" "$ADMIN")"
expect_status "  van liet ke em cho xoa" yes "$(contains_id "$LEAVER")"

section "Giao bai theo lop bo qua tai khoan cho xoa"
code=$(call POST /api/exams "$TEACHER" "$(cat <<JSON
{
  "title": "De rao $STAMP", "topic": "", "durationMinutes": 45, "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22, "pointPerQuestion": 1,
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": true,
  "openAt": "2020-01-01T00:00:00Z", "closeAt": "2035-01-01T00:00:00Z"
}
JSON
)")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EID=$(json_field id)
expect_status "giao bai cho ca lop → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$CID],\"notify\":false}")"
expect_json   "  chi giao cho em con hoc" assignedCount 1
expect_json   "  em cho xoa bi bo qua" skippedLocked 1

section "Lop chi con tai khoan cho xoa thi xoa duoc"
# Bat quan tri vien cho 60 ngay moi xoa duoc lop chi vi mot em da roi truong la vo ly.
LONE_GID=$(new_class "$ADMIN" "$TEACHER" "Mot$STAMP") || fail_fast "tao lop thu hai"
new_student "${STAMP}e"; LONE=$NEW_ID
expect_status "xep em vao lop → 200" 200 \
  "$(call POST "/api/student-groups/$LONE_GID/students" "$ADMIN" "{\"studentIds\":[$LONE]}")"
expect_status "em roi truong → 200" 200 \
  "$(call POST "/api/students/$LONE/request-deletion" "$ADMIN" '{"reason":"Hoc sinh chuyen truong giua hoc ky."}')"
expect_status "xoa lop chi con em cho xoa → 200" 200 "$(call DELETE "/api/student-groups/$LONE_GID" "$ADMIN")"
expect_status "doc lai em do → 200" 200 "$(call GET "/api/students/$LONE" "$ADMIN")"
expect_json   "  ve chua phan nhom" groupId 0
expect_json   "  van dang cho xoa" status pendingDeletion

section "Khoi phuc la loi duy nhat, va xoa sach dau vet"
expect_status "khoi phuc → 200" 200 "$(call POST "/api/students/$SID/restore" "$ADMIN")"
expect_json   "  status = active" status active
expect_status "  khong con deletionReason" absent "$(has_field deletionReason)"
expect_status "  khong con deletionScheduledAt" absent "$(has_field deletionScheduledAt)"
expect_status "khoi phuc xong thi khoa duoc nhu thuong → 200" 200 \
  "$(call PATCH "/api/students/$SID/status" "$ADMIN" '{"status":"locked"}')"

summary
