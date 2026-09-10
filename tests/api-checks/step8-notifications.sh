#!/usr/bin/env bash
# Buoc 8 — Thong bao, SignalR va tien trinh nen (§4.2, §7).
#
# Chay:  bash tests/api-checks/step8-notifications.sh   (API phai dang chay o $HOST)
#
# Phan SignalR khong kiem duoc bang curl — day la giao thuc WebSocket. Nhung thu kiem duoc va dang
# kiem nhat la: BAN GHI thong bao co duoc sinh dung luc khong, va no co thuoc dung mot nguoi khong.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@eduquiz.vn" "eduquiz123")
ADMIN=$(login "huy.pq@eduquiz.vn" "eduquiz123")

STAMP=$(date +%H%M%S)
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=()
GROUP_ID=""

cleanup() {
  section "Don du lieu"
  for id in "${EXAMS[@]}";     do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  for id in "${QUESTIONS[@]}"; do call DELETE "/api/questions/$id" "$TEACHER" > /dev/null; done
  for id in "${STUDENTS[@]}"; do
    call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$TEACHER" > /dev/null
    call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do call DELETE "/api/student-groups/$id" "$TEACHER" > /dev/null; done
  printf '  da don (de co bai nop thi giu lai — dung thiet ke)\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
has_field() { python -c "
import json,sys
print('present' if sys.argv[1] in json.load(sys.stdin) else 'absent')
" "$1" < "$BODY_FILE"; }

section "Dung du lieu nen"
GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KT8 $STAMP" "Nhom kiem thu buoc 8")   || fail_fast "khong dung duoc lop nen"
GROUP_IDS+=("$GROUP_ID")

NEW_STUDENT_ID=""
new_student() {
  local c; c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"Hoc sinh $1\",\"email\":\"n$1@kiemthu.vn\",\"username\":\"n$1\",\"code\":\"N$1\",\"password\":\"eduquiz123\",\"groupId\":$GROUP_ID}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
  NEW_STUDENT_ID=$(json_field id); STUDENTS+=("$NEW_STUDENT_ID")
}
new_student "${STAMP}a"; S_A=$NEW_STUDENT_ID
new_student "${STAMP}b"; S_B=$NEW_STUDENT_ID
STUDENT_A=$(login "n${STAMP}a" "eduquiz123")
STUDENT_B=$(login "n${STAMP}b" "eduquiz123")

for pair in easy:1 easy:2 easy:3 easy:4 medium:1 medium:2 medium:3 hard:1 hard:2; do
  d="${pair%%:*}"; i="${pair##*:}"
  c=$(call POST /api/questions "$TEACHER" \
    "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"KiemThu$STAMP\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
  QUESTIONS+=("$(json_field id)")
done
printf '  nhom %s · 2 hoc sinh · 9 cau hoi\n' "$GROUP_ID"

section "Client KHONG duoc tao thong bao"
# Sinh thong bao la viec cua backend, trong cung transaction voi hanh dong nghiep vu (§7).
# Khong co endpoint nao cho viec do — POST vao goc phai tra 404 hoac 405, khong bao gio 200.
CODE=$(call POST /api/notifications "$STUDENT_A" '{"title":"Tu tao","message":"Khong duoc phep"}')
expect_status "POST /api/notifications khong ton tai" true \
  "$(python -c "print(str('$CODE' in ('404','405')).lower())")"

section "Thong bao sinh ra khi giao bai, va thuoc dung mot nguoi"
BEFORE_A=$(call GET "/api/notifications/unread-count" "$STUDENT_A" > /dev/null; body)
BEFORE_B=$(call GET "/api/notifications/unread-count" "$STUDENT_B" > /dev/null; body)

exam_body() {
  cat <<JSON
{
  "title": "De thong bao $STAMP$1",
  "topic": "KiemThu$STAMP",
  "durationMinutes": 45,
  "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22,
  "pointPerQuestion": 1,
  "openAt": "2020-01-01T00:00:00Z",
  "closeAt": "2035-01-01T00:00:00Z",
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": true
}
JSON
}

code=$(call POST /api/exams "$TEACHER" "$(exam_body " co bao")")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
NOTIFY_EXAM=$(json_field id); EXAMS+=("$NOTIFY_EXAM")

expect_status "giao bai voi notify=true → 200" 200 \
  "$(call POST "/api/exams/$NOTIFY_EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":true}")"

expect_status "so chua doc cua A tang → 200" 200 "$(call GET "/api/notifications/unread-count" "$STUDENT_A")"
AFTER_A=$(body)
expect_status "  A nhan them dung 1 thong bao" true \
  "$(python -c "print(str(int('$AFTER_A') == int('$BEFORE_A') + 1).lower())")"

section "notify=false thi khong ai nhan gi"
BEFORE_QUIET=$(call GET "/api/notifications/unread-count" "$STUDENT_A" > /dev/null; body)
code=$(call POST /api/exams "$TEACHER" "$(exam_body " im lang")")
QUIET_EXAM=$(json_field id); EXAMS+=("$QUIET_EXAM")
expect_status "giao bai voi notify=false → 200" 200 \
  "$(call POST "/api/exams/$QUIET_EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":false}")"
expect_status "so chua doc khong doi → 200" 200 "$(call GET "/api/notifications/unread-count" "$STUDENT_A")"
expect_status "  dung bang truoc do" "$BEFORE_QUIET" "$(body)"

section "Danh sach thong bao cua chinh minh"
expect_status "A xem thong bao → 200" 200 "$(call GET "/api/notifications" "$STUDENT_A")"
expect_status "  co it nhat 1 muc" true \
  "$(body | python -c 'import json,sys;print(str(json.load(sys.stdin)["total"]>=1).lower())')"
NOTIF_ID=$(body | python -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["id"])')
expect_status "  muc dau co type" present \
  "$(body | python -c "import json,sys;print('present' if 'type' in json.load(sys.stdin)['items'][0] else 'absent')")"
expect_status "  type la examAssigned" examAssigned \
  "$(body | python -c 'import json,sys;print(json.load(sys.stdin)["items"][0]["type"])')"
expect_status "  KHONG co truong audience (mock loc theo vai tro, BE thi khong)" absent \
  "$(body | python -c "import json,sys;print('present' if 'audience' in json.load(sys.stdin)['items'][0] else 'absent')")"

expect_status "loc chi chua doc → 200" 200 "$(call GET "/api/notifications?unreadOnly=true" "$STUDENT_A")"
expect_status "  moi muc deu chua doc" true \
  "$(body | python -c 'import json,sys;print(str(all(not i["isRead"] for i in json.load(sys.stdin)["items"])).lower())')"

section "Thong bao cua nguoi khac tra 404, khong phai 403"
# Mock tim theo id tren toan mang, tuc bat ky ai cung doc duoc thong bao cua nguoi khac.
expect_status "B danh dau thong bao cua A → 404" 404 "$(call PATCH "/api/notifications/$NOTIF_ID/read" "$STUDENT_B")"
expect_status "id khong ton tai → 404" 404 "$(call PATCH "/api/notifications/999999/read" "$STUDENT_A")"
expect_status "giao vien danh dau thong bao cua A → 404" 404 "$(call PATCH "/api/notifications/$NOTIF_ID/read" "$TEACHER")"
expect_status "goi khong token → 401" 401 "$(call GET "/api/notifications" "")"

section "Danh dau da doc"
UNREAD_BEFORE=$(call GET "/api/notifications/unread-count" "$STUDENT_A" > /dev/null; body)
expect_status "A danh dau thong bao cua chinh minh → 200" 200 "$(call PATCH "/api/notifications/$NOTIF_ID/read" "$STUDENT_A")"
expect_status "  so chua doc giam 1" true \
  "$(call GET "/api/notifications/unread-count" "$STUDENT_A" > /dev/null; body | python -c "
import sys
print(str(int(sys.stdin.read()) == int('$UNREAD_BEFORE') - 1).lower())")"
# Hai tab cung mo la chuyen thuong: danh dau lai mot muc da doc khong phai loi.
expect_status "danh dau lai chinh muc do → 200" 200 "$(call PATCH "/api/notifications/$NOTIF_ID/read" "$STUDENT_A")"

# Phai co NHIEU HON MOT muc chua doc thi nhanh nay moi that su chay. Lan viet dau chi co dung mot
# thong bao, ma no da duoc danh dau o phep kiem ngay tren — nen mark-all-read khong co gi de lam va
# loi "quen SaveChanges" di lot qua.
for i in 1 2 3; do
  c=$(call POST /api/exams "$TEACHER" "$(exam_body " bao $i")")
  [[ "$c" != "200" ]] && fail_fast "tao de bao $i: HTTP $c"
  extra_exam=$(json_field id); EXAMS+=("$extra_exam")
  call POST "/api/exams/$extra_exam/assignments" "$TEACHER" "{\"studentIds\":[$S_A],\"groupIds\":[],\"notify\":true}" > /dev/null
done
expect_status "A dang co nhieu hon 1 thong bao chua doc" true   "$(call GET "/api/notifications/unread-count" "$STUDENT_A" > /dev/null; body | python -c 'import sys;print(str(int(sys.stdin.read())>1).lower())')"

expect_status "danh dau tat ca da doc → 200" 200 "$(call POST "/api/notifications/mark-all-read" "$STUDENT_A")"
expect_status "  tra ve so muc vua danh dau, lon hon 1" true   "$(body | python -c 'import sys;print(str(int(sys.stdin.read())>1).lower())')"
expect_status "  so chua doc ve 0" 0 "$(call GET "/api/notifications/unread-count" "$STUDENT_A" > /dev/null; body)"
expect_status "danh dau tat ca lan hai → 200" 200 "$(call POST "/api/notifications/mark-all-read" "$STUDENT_A")"
expect_status "  lan nay khong con gi de danh dau" 0 "$(body)"

section "Khong lan sang hop thu nguoi khac"
expect_status "B van con thong bao chua doc → 200" 200 "$(call GET "/api/notifications/unread-count" "$STUDENT_B")"
expect_status "  B khong bi A danh dau ho" true \
  "$(body | python -c 'import sys;print(str(int(sys.stdin.read())>0).lower())')"
expect_status "danh sach cua B → 200" 200 "$(call GET "/api/notifications" "$STUDENT_B")"
expect_status "  khong co muc nao cua A lan sang" true \
  "$(body | python -c "
import json,sys
ids = [i['id'] for i in json.load(sys.stdin)['items']]
print(str($NOTIF_ID not in ids).lower())")"

section "Ca ba vai tro deu co hop thu rieng"
expect_status "giao vien xem thong bao cua minh → 200" 200 "$(call GET "/api/notifications" "$TEACHER")"
expect_status "quan tri vien xem thong bao cua minh → 200" 200 "$(call GET "/api/notifications" "$ADMIN")"
expect_status "pageSize=500 bi kep" 200 "$(call GET "/api/notifications?pageSize=500" "$STUDENT_B")"
expect_json   "  kep ve 200" pageSize 200

summary
