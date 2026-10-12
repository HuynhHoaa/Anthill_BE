#!/usr/bin/env bash
# Nhac hoc sinh khi gio lam bai sap het (§7, su kien DeadlineSoon).
#
# Chay:  bash tests/api-checks/deadline-reminders.sh   (API phai dang chay o $HOST)
#
# Bo nay cham vao mot tien trinh NEN, nen no cho thay: moi lan cho toi doi mot nhip quet (mac dinh 30
# giay). Bon dieu can chung minh:
#   1. Em chua bat dau, con vua du gio lam bai cong bien -> duoc nhac dung mot lan, kem duong dan toi bai.
#   2. Quet lai khong nhac lan thu hai — dau da nhac nam trong database nen khoi dong lai cung khong nhac lai.
#   3. Khung gio dang mo bi khoa gio nen giao vien khong gia han duoc (409), va em khong bi nhac lan thu hai.
#   4. Em da vao thi thi khong bi nhac; lop co khung vua khit mot tiet hoc cung khong (giao vien dang o do).

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); EXAM_IDS=(); STUDENT_IDS=(); MEMBERS=()

cleanup() {
  section "Don du lieu"
  local member
  for id in "${EXAM_IDS[@]}"; do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  for member in "${MEMBERS[@]}"; do
    call DELETE "/api/student-groups/${member%/*}/students/${member#*/}" "$ADMIN" > /dev/null
  done
  for id in "${STUDENT_IDS[@]}"; do call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null; done
  for id in "${GROUP_IDS[@]}";   do call DELETE "/api/student-groups/$id" "$ADMIN" > /dev/null; done
  printf '  da don\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

HELPERS=$(mktemp -t anthill-deadline.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
"""Vai phep tinh nho cho bo kiem nhac sap het gio."""
import datetime
import json
import sys

mode = sys.argv[1]


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


if mode == "iso":
    moment = datetime.datetime.now(datetime.UTC) + datetime.timedelta(minutes=float(sys.argv[2]))
    print(moment.isoformat().replace("+00:00", "Z"))

elif mode == "count":
    print(len([x for x in load(sys.argv[2])["items"] if x["type"] == "deadlineSoon"]))

elif mode == "field":
    rows = [x for x in load(sys.argv[2])["items"] if x["type"] == "deadlineSoon"]
    print("none" if not rows else str(rows[0].get(sys.argv[3])))

elif mode == "my":
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["examId"] == int(sys.argv[3])), None)
    print("none" if row is None else str(row.get("assignmentId")))
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

at_minutes()  { python "$HELPERS" iso "$1"; }
notice_field(){ PYTHONIOENCODING=utf-8 python "$HELPERS" field "$(to_native_path "$BODY_FILE")" "$1"; }
my_assignment(){ python "$HELPERS" my "$(to_native_path "$BODY_FILE")" "$1"; }

# So loi nhac dang nam trong hop thu cua hoc sinh co token $1.
reminders_of() { call GET /api/notifications "$1" > /dev/null; python "$HELPERS" count "$(to_native_path "$BODY_FILE")"; }

# Cho toi khi hop thu cua $1 co du $2 loi nhac, toi da $3 giay. In ra so dem duoc.
wait_for_reminders() {
  local token="$1" wanted="$2" limit="$3" waited=0 count
  while :; do
    count=$(reminders_of "$token")
    if [[ "$count" -ge "$wanted" || "$waited" -ge "$limit" ]]; then
      echo "$count"
      return
    fi
    sleep 3
    waited=$((waited + 3))
  done
}

TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

build_class() {
  local name="$1" suffix="$2" code
  code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"$name\"}")
  [[ "$code" != "200" ]] && fail_fast "tao lop $name: HTTP $code — $(body)"
  BUILT_GID=$(json_field id); GROUP_IDS+=("$BUILT_GID")
  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS $suffix\",\"email\":\"r$suffix@kiemthu.vn\",\"username\":\"r$suffix\",\"code\":\"R$suffix\",\"password\":\"123456\",\"groupId\":$BUILT_GID}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  BUILT_SID=$(json_field id); STUDENT_IDS+=("$BUILT_SID"); MEMBERS+=("$BUILT_GID/$BUILT_SID")
  call POST "/api/student-groups/$BUILT_GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}" > /dev/null
  call POST "/api/student-groups/$BUILT_GID/publish" "$ADMIN" > /dev/null
}

# De 9 cau, lam bai 1 phut: moc nhac la 31 phut truoc gio dong (1 phut lam bai + bien 30 phut mac dinh).
new_exam() {
  local title="$1" code
  code=$(call POST /api/exams "$TEACHER" "$(printf '{"title":"%s","topic":"","durationMinutes":1,"totalQuestions":9,"easyPercentage":45,"mediumPercentage":33,"hardPercentage":22,"pointPerQuestion":1,"shuffleQuestions":true,"shuffleAnswers":true,"showResultAfterSubmit":true,"openAt":"%s","closeAt":"%s"}' \
    "$title" "$(at_minutes -1440)" "$(at_minutes 10080)")")
  [[ "$code" != "200" ]] && fail_fast "tao de $title: HTTP $code — $(body)"
  NEW_EID=$(json_field id); EXAM_IDS+=("$NEW_EID")
}

section "Dung ba lop: sap het gio, da vao thi, va khung vua khit mot tiet"
build_class "Nhac A $STAMP" "${STAMP}a"; G_A=$BUILT_GID; SID_A=$BUILT_SID
build_class "Nhac B $STAMP" "${STAMP}b"; G_B=$BUILT_GID; SID_B=$BUILT_SID
build_class "Nhac C $STAMP" "${STAMP}c"; G_C=$BUILT_GID; SID_C=$BUILT_SID

new_exam "De nhac gio $STAMP"; EID=$NEW_EID

A_OPEN=$(at_minutes -40); A_CLOSE=$(at_minutes 5)
B_OPEN=$(at_minutes -40); B_CLOSE=$(at_minutes 20)
# Khung 30 phut cho bai 1 phut: ngan hon moc nhac 31 phut, tuc khung cua mot tiet hoc co giao vien ngoi day.
C_OPEN=$(at_minutes -5);  C_CLOSE=$(at_minutes 25)

expect_status "giao ba lop, moi lop mot khung → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_A,\"openAt\":\"$A_OPEN\",\"closeAt\":\"$A_CLOSE\"},{\"groupId\":$G_B,\"openAt\":\"$B_OPEN\",\"closeAt\":\"$B_CLOSE\"},{\"groupId\":$G_C,\"openAt\":\"$C_OPEN\",\"closeAt\":\"$C_CLOSE\"}],\"notify\":false}")"
expect_json "  assignedCount = 3" assignedCount 3

# Tai khoan vua tao mang MustChangePassword=true (Sec.5) — doi ngay sang mat khau rieng truoc khi
# dung token cho nghiep vu, dung nhu mot hoc sinh that lam sau lan dang nhap dau tien.
TOK_A=$(login_and_clear_must_change "r${STAMP}a" "123456" "Ra$STAMP")
TOK_B=$(login_and_clear_must_change "r${STAMP}b" "123456" "Rb$STAMP")
TOK_C=$(login_and_clear_must_change "r${STAMP}c" "123456" "Rc$STAMP")

call GET /api/my/exams "$TOK_B" > /dev/null
AID_B=$(my_assignment "$EID")
expect_status "em lop B vao thi ngay → 200" 200 "$(call POST /api/exam-attempts "$TOK_B" "{\"assignmentId\":$AID_B}")"

section "Em con 5 phut ma chua vao thi thi duoc nhac"
printf '  cho tien trinh nen quet (toi da 45 giay)...\n'
expect_status "em lop A nhan dung mot loi nhac" 1 "$(wait_for_reminders "$TOK_A" 1 45)"
call GET /api/notifications "$TOK_A" > /dev/null
call GET /api/my/exams "$TOK_A" > /dev/null
AID_A=$(my_assignment "$EID")
call GET /api/notifications "$TOK_A" > /dev/null
expect_status "  dan toi bai cua chinh em"   "/hoc-sinh/bai-kiem-tra/$AID_A" "$(notice_field link)"
expect_status "  tieu de noi ro chuyen gi"   "Bài kiểm tra sắp đóng" "$(notice_field title)"
expect_status "  noi con bao nhieu phut"     yes "$(notice_field message | grep -qE 'còn khoảng [0-9]+ phút' && echo yes || echo no)"
expect_status "  chua doc"                   False "$(notice_field isRead)"

section "Quet lai khong nhac lan thu hai"
printf '  cho them mot nhip quet...\n'
sleep 35
expect_status "em lop A van chi co mot loi nhac" 1 "$(reminders_of "$TOK_A")"
expect_status "em lop B da vao thi: khong bi nhac" 0 "$(reminders_of "$TOK_B")"
expect_status "lop C khung vua khit mot tiet: khong bi nhac" 0 "$(reminders_of "$TOK_C")"

section "Khung gio dang mo khong gia han duoc, nen khong co loi nhac thu hai"
# Loi nhac chi toi luc khung da mo, ma khung dang mo thi bi khoa gio (xem schedule-lock.sh): khong con cach
# nao keo gio dong ra de sinh mot khung moi giua luc em dang duoc nhac.
expect_status "keo gio dong cua lop A ra them → 409" 409 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_A,\"openAt\":\"$A_OPEN\",\"closeAt\":\"$(at_minutes 12)\"}],\"notify\":false}")"
printf '  cho them mot nhip quet...\n'
sleep 35
expect_status "em lop A van chi co mot loi nhac" 1 "$(reminders_of "$TOK_A")"
expect_status "em lop C van khong bi nhac"       0 "$(reminders_of "$TOK_C")"

summary
