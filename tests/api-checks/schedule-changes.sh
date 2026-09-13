#!/usr/bin/env bash
# Doi khung gio sau khi da giao bai (§4.6, §5 Giao bai).
#
# Chay:  bash tests/api-checks/schedule-changes.sh   (API phai dang chay o $HOST)
#
# per-class-schedule.sh chung minh mot de chay duoc nhieu khung gio. Bo nay canh nhung cach khung gio
# DOI DI sau lan giao dau tien, vi do la luc de sai nhat:
#   1. Giao nhieu lop trong mot lan, moi lop mot khung gio; lop khai "theo lich de" thi dung lich de, va
#      mot lop noi hai lan hay khung qua ngan bi tu choi ma khong tao dong nao.
#   2. Em moi xep vao lop da co gio rieng, duoc giao bo sung ma khong kem gio, lam cung gio voi ca lop.
#   3. Khai lai lop "theo lich de" thi go gio rieng cua em chua bat dau; em da bat dau chi bi dem la
#      "giu khung cu" khi khung gui len that su khac khung em dang giu.
#   4. Em chon dich danh doi duoc gio rieng (PATCH), ve lai lich de duoc; em giao qua lop va em da bat
#      dau bi tu choi 409; khung qua ngan 400; quan tri vien 403.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")
TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); EXAM_IDS=(); STUDENT_IDS=(); MEMBERS=()

cleanup() {
  section "Don du lieu"
  local member
  for id in "${EXAM_IDS[@]}"; do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  # Go hoc sinh khoi lop truoc: lop con thanh vien thi khong xoa duoc (409).
  for member in "${MEMBERS[@]}"; do
    call DELETE "/api/student-groups/${member%/*}/students/${member#*/}" "$ADMIN" > /dev/null
  done
  for id in "${STUDENT_IDS[@]}"; do call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null; done
  for id in "${GROUP_IDS[@]}";   do call DELETE "/api/student-groups/$id" "$ADMIN" > /dev/null; done
  printf '  da don (de da co bai nop thi khong xoa duoc — dung thiet ke, khong phai loi don dep)\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

HELPERS=$(mktemp -t anthill-helpers.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
"""Vai phep tinh nho cho bo kiem. Tach ra file rieng de khoi long trich dan trong shell."""
import datetime
import json
import sys
import time

mode = sys.argv[1]


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def show(value):
    if value is None:
        return "absent"
    return str(value).lower() if isinstance(value, bool) else str(value)


def parse(text):
    return datetime.datetime.fromisoformat(text.replace("Z", "+00:00"))


def now():
    return datetime.datetime.now(datetime.UTC)


def near(value, target):
    if value is None:
        return "absent"
    gap = abs((parse(value) - parse(target)).total_seconds())
    return "true" if gap <= 2 else "false (%s)" % value


if mode == "iso":
    moment = now() + datetime.timedelta(hours=float(sys.argv[2]))
    print(moment.isoformat().replace("+00:00", "Z"))

elif mode == "row":
    # Truong $3 cua em $4 trong bang theo doi cua giao vien.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["studentId"] == int(sys.argv[4])), None)
    print("none" if row is None else show(row.get(sys.argv[3])))

elif mode == "row_near":
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["studentId"] == int(sys.argv[4])), None)
    print("none" if row is None else near(row.get(sys.argv[3]), sys.argv[5]))

elif mode == "field_near":
    print(near(load(sys.argv[2]).get(sys.argv[3]), sys.argv[4]))

elif mode == "my":
    # Truong $3 cua de $4 trong danh sach bai cua hoc sinh.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["examId"] == int(sys.argv[4])), None)
    print("none" if row is None else show(row.get(sys.argv[3])))

elif mode == "wait":
    target = parse(sys.argv[2]) + datetime.timedelta(seconds=1)
    while now() <= target:
        time.sleep(0.5)
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

at_hours()         { python "$HELPERS" iso "$1"; }
row_of_student()   { python "$HELPERS" row "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
row_near()         { python "$HELPERS" row_near "$(to_native_path "$BODY_FILE")" "$1" "$2" "$3"; }
field_near()       { python "$HELPERS" field_near "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
my_field()         { python "$HELPERS" my "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
wait_past()        { python "$HELPERS" wait "$1"; }

TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

# Lop da cong bo, giao vien demo phu trach, co dung mot hoc sinh. Tra ve qua BUILT_GID/BUILT_SID.
# Goi TRUC TIEP, khong trong $( ): subshell nuot mat moi phep gan.
build_class() {
  local name="$1" suffix="$2" code
  code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"$name\"}")
  [[ "$code" != "200" ]] && fail_fast "tao lop $name: HTTP $code — $(body)"
  BUILT_GID=$(json_field id); GROUP_IDS+=("$BUILT_GID")
  add_student "$BUILT_GID" "$suffix"
  call POST "/api/student-groups/$BUILT_GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}" > /dev/null
  call POST "/api/student-groups/$BUILT_GID/publish" "$ADMIN" > /dev/null
}

# Tao mot hoc sinh trong lop $1. Tra ve qua BUILT_SID.
add_student() {
  local group="$1" suffix="$2" code
  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS $suffix\",\"email\":\"q$suffix@kiemthu.vn\",\"username\":\"q$suffix\",\"code\":\"Q$suffix\",\"password\":\"anthill123\",\"groupId\":$group}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  BUILT_SID=$(json_field id); STUDENT_IDS+=("$BUILT_SID"); MEMBERS+=("$group/$BUILT_SID")
}

# Than de 9 cau. $1 tieu de, $2/$3 moc mo/dong ISO, $4 thoi luong (phut).
exam_body() {
  printf '{"title":"%s","topic":"","durationMinutes":%s,"totalQuestions":9,"easyPercentage":45,"mediumPercentage":33,"hardPercentage":22,"pointPerQuestion":1,"shuffleQuestions":true,"shuffleAnswers":true,"showResultAfterSubmit":true,"openAt":"%s","closeAt":"%s"}' \
    "$1" "$4" "$2" "$3"
}

# Tao de, tra id qua NEW_EID. $1 tieu de, $2/$3 gio mo/dong tinh tu bay gio, $4 thoi luong (mac dinh 45).
new_exam() {
  local title="$1" duration="${4:-45}" code
  NEW_OPEN=$(at_hours "$2"); NEW_CLOSE=$(at_hours "$3")
  code=$(call POST /api/exams "$TEACHER" "$(exam_body "$title" "$NEW_OPEN" "$NEW_CLOSE" "$duration")")
  [[ "$code" != "200" ]] && fail_fast "tao de $title: HTTP $code — $(body)"
  NEW_EID=$(json_field id); EXAM_IDS+=("$NEW_EID")
}

# Mot muc trong "classes": $1 lop, $2/$3 moc ISO (bo trong = theo lich de).
class_entry() {
  if [[ -n "${2:-}" ]]; then
    printf '{"groupId":%s,"openAt":"%s","closeAt":"%s"}' "$1" "$2" "$3"
  else
    printf '{"groupId":%s}' "$1"
  fi
}

section "Dung nam lop, moi lop mot hoc sinh"
build_class "Doi gio A $STAMP" "${STAMP}a"; G_A=$BUILT_GID; SID_A=$BUILT_SID
build_class "Doi gio B $STAMP" "${STAMP}b"; G_B=$BUILT_GID; SID_B=$BUILT_SID
build_class "Doi gio C $STAMP" "${STAMP}c"; G_C=$BUILT_GID; SID_C=$BUILT_SID
build_class "Doi gio D $STAMP" "${STAMP}d"; G_D=$BUILT_GID; SID_D=$BUILT_SID
build_class "Doi gio E $STAMP" "${STAMP}e"; G_E=$BUILT_GID; SID_E=$BUILT_SID
printf '  A %s · B %s · C %s · D %s · E %s\n' "$G_A" "$G_B" "$G_C" "$G_D" "$G_E"

new_exam "De doi gio $STAMP" -24 168
EID=$NEW_EID; EID_OPEN=$NEW_OPEN
A_OPEN=$(at_hours 1); A_CLOSE=$(at_hours 3)
B_OPEN=$(at_hours 5); B_CLOSE=$(at_hours 7)

section "Giao nhieu lop mot lan, moi lop mot khung gio"
BODY="{\"studentIds\":[$SID_D],\"classes\":[$(class_entry "$G_A" "$A_OPEN" "$A_CLOSE"),$(class_entry "$G_B" "$B_OPEN" "$B_CLOSE"),$(class_entry "$G_C")],\"notify\":false}"
expect_status "ba lop + mot em chon le → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$BODY")"
expect_json   "  assignedCount = 4"          assignedCount 4
expect_json   "  scheduleUpdatedCount = 0"   scheduleUpdatedCount 0

expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$EID/assignments" "$TEACHER")"
expect_status "  lop A: gio rieng"                 true   "$(row_of_student hasScheduleOverride "$SID_A")"
expect_status "  lop A: mo dung gio cua A"         true   "$(row_near openAt "$SID_A" "$A_OPEN")"
expect_status "  lop B: mo dung gio cua B"         true   "$(row_near openAt "$SID_B" "$B_OPEN")"
expect_status "  lop C khai theo lich de"          false  "$(row_of_student hasScheduleOverride "$SID_C")"
expect_status "  lop C: mo theo lich de"           true   "$(row_near openAt "$SID_C" "$EID_OPEN")"
expect_status "  em chon le: khong gan lop"        absent "$(row_of_student assignedViaGroupId "$SID_D")"
expect_status "  lop B: ghi lai lop da giao qua"   "$G_B" "$(row_of_student assignedViaGroupId "$SID_B")"

section "Request sai hinh dang bi tu choi, khong tao dong nao"
DUP_BODY="{\"studentIds\":[],\"classes\":[$(class_entry "$G_E" "$A_OPEN" "$A_CLOSE"),$(class_entry "$G_E")],\"notify\":false}"
expect_status "mot lop hai lan trong classes → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$DUP_BODY")"
BOTH_BODY="{\"studentIds\":[],\"groupIds\":[$G_E],\"classes\":[$(class_entry "$G_E")],\"notify\":false}"
expect_status "mot lop o ca groupIds va classes → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$BOTH_BODY")"
HALF_BODY="{\"studentIds\":[],\"classes\":[{\"groupId\":$G_E,\"openAt\":\"$A_OPEN\"}],\"notify\":false}"
expect_status "lop chi co gio mo → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$HALF_BODY")"
SHORT_BODY="{\"studentIds\":[],\"classes\":[$(class_entry "$G_E" "$(at_hours 1)" "$(at_hours 1.3333)"),$(class_entry "$G_A" "$A_OPEN" "$A_CLOSE")],\"notify\":false}"
expect_status "mot lop khung 20 phut → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$SHORT_BODY")"
expect_status "  cau tu choi goi dung ten lop" yes "$(body | grep -q "Doi gio E $STAMP" && echo yes || echo no)"
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  em lop E khong duoc giao" none "$(row_of_student status "$SID_E")"

section "Em moi vao lop co gio rieng lam cung gio voi ca lop"
add_student "$G_A" "${STAMP}a2"; SID_A2=$BUILT_SID
expect_status "gui lai lop A qua groupIds, khong kem gio → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_A],\"notify\":false}")"
expect_json   "  assignedCount = 1"        assignedCount 1
expect_json   "  scheduleUpdatedCount = 0" scheduleUpdatedCount 0
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  em moi mang gio rieng"      true "$(row_of_student hasScheduleOverride "$SID_A2")"
expect_status "  em moi mo dung gio cua lop" true "$(row_near openAt "$SID_A2" "$A_OPEN")"
expect_status "  em moi dong dung gio cua lop" true "$(row_near closeAt "$SID_A2" "$A_CLOSE")"

section "Khai lai lop theo lich de thi go gio rieng"
expect_status "classes: A khong kem gio → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[$(class_entry "$G_A")],\"notify\":false}")"
expect_json   "  scheduleUpdatedCount = 2" scheduleUpdatedCount 2
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  em A theo lich de"     false "$(row_of_student hasScheduleOverride "$SID_A")"
expect_status "  em A2 theo lich de"    false "$(row_of_student hasScheduleOverride "$SID_A2")"
expect_status "  em B van giu gio rieng" true "$(row_of_student hasScheduleOverride "$SID_B")"

section "Em da bat dau chi bi dem khi khung that su khac"
TOK_C=$(login "q${STAMP}c" "anthill123")
call GET /api/my/exams "$TOK_C" > /dev/null
AID_C=$(my_field assignmentId "$EID")
expect_status "em lop C vao lam → 200" 200 "$(call POST /api/exam-attempts "$TOK_C" "{\"assignmentId\":$AID_C}")"
expect_status "khai lai C dung lich de nhu cu → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[$(class_entry "$G_C")],\"notify\":false}")"
expect_json   "  scheduleKeptStartedCount = 0" scheduleKeptStartedCount 0
expect_status "khai C sang khung khac → 200" 200 \
  "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[$(class_entry "$G_C" "$(at_hours 10)" "$(at_hours 12)")],\"notify\":false}")"
expect_json   "  scheduleKeptStartedCount = 1" scheduleKeptStartedCount 1
expect_json   "  scheduleUpdatedCount = 0"     scheduleUpdatedCount 0

section "Doi gio cho em chon dich danh"
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
AID_D=$(row_of_student id "$SID_D")
AID_B=$(row_of_student id "$SID_B")
D_OPEN=$(at_hours 2); D_CLOSE=$(at_hours 4)
expect_status "PATCH em D, 2..4 gio nua → 200" 200 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"
expect_json   "  tra ve dong cua em: hasScheduleOverride" hasScheduleOverride true
expect_status "  openAt dung gio vua dat" true "$(field_near openAt "$D_OPEN")"
expect_json   "  status = assigned" status assigned
TOK_D=$(login "q${STAMP}d" "anthill123")
expect_status "em D doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_D")"
expect_status "  em thay lifecycle = scheduled" scheduled "$(my_field lifecycle "$EID")"
expect_status "  em thay canStart = false"      false     "$(my_field canStart "$EID")"

expect_status "PATCH lai y het → 200 (khong doi gi)" 200 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"
expect_status "PATCH chi co gio mo → 400" 400 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$D_OPEN\"}")"
expect_status "PATCH khung 20 phut → 400" 400 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$(at_hours 1)\",\"closeAt\":\"$(at_hours 1.3333)\"}")"
expect_status "PATCH em giao qua lop B → 409" 409 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_B/schedule" "$TEACHER" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"
expect_status "  cau tu choi chi toi lop cua em" yes "$(body | grep -q "Doi gio B $STAMP" && echo yes || echo no)"
expect_status "quan tri vien PATCH → 403" 403 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$ADMIN" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"
expect_status "hoc sinh PATCH → 403" 403 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TOK_D" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"

new_exam "De khac $STAMP" -24 168; EID_OTHER=$NEW_EID
expect_status "PATCH dong cua de khac qua duong dan de nay → 404" 404 \
  "$(call PATCH "/api/exams/$EID_OTHER/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"

expect_status "PATCH trong ca cap → 200 (ve lich de)" 200 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{}")"
expect_json   "  hasScheduleOverride = false" hasScheduleOverride false
expect_status "  openAt la lich de" true "$(field_near openAt "$EID_OPEN")"
expect_status "em D doc lai → 200" 200 "$(call GET /api/my/exams "$TOK_D")"
expect_status "  canStart = true theo lich de" true "$(my_field canStart "$EID")"

AID_D_MY=$(my_field assignmentId "$EID")
expect_status "em D vao lam → 200" 200 "$(call POST /api/exam-attempts "$TOK_D" "{\"assignmentId\":$AID_D_MY}")"
expect_status "PATCH em da bat dau → 409" 409 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$D_OPEN\",\"closeAt\":\"$D_CLOSE\"}")"

summary
