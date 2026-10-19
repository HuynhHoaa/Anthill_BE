#!/usr/bin/env bash
# Khoa doi gio: tu luc con duoi 120 phut nua la mo cho toi het gio dong (§4.6).
#
# Chay:  bash tests/api-checks/schedule-lock.sh   (API phai dang chay o $HOST)
#
# Trong khoang do hoc sinh da duoc bao gio va co the dang lam bai, nen doi gio chi gay hai. Het gio dong
# thi khong ai lam do nua, va do la duong cuu em lo gio hoac mo lai de, nen mo khoa tro lai.
#
# Bo kiem nay canh hai duong doi gio deu bi chan CHAN THAT o server, khong chi an nut:
#   1. Sua ca de (PUT) va doi lich de (PATCH) — chi chan khi de da giao cho ai do.
#   2. Gui lai mot lop kem khung gio moi — giao them hoc sinh vao lop thi van duoc.
# Bai chi giao theo lop nen khong con duong doi gio rieng tung em: khung cua mot em luon la khung cua lop em.
# Co khoa (scheduleLocked) tren de, tren tung dong giao bai va tren tung khung gio cua lop khop voi viec
# server tu choi.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); EXAM_IDS=(); STUDENT_IDS=(); MEMBERS=()

cleanup() {
  section "Don du lieu"
  local member
  for id in "${EXAM_IDS[@]}"; do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  # Go hoc sinh khoi lop truoc: lop con thanh vien thi khong xoa duoc (409).
  for member in "${MEMBERS[@]}"; do
    call DELETE "/api/student-groups/${member%/*}/students/${member#*/}" "$ADMIN" "$REMOVE_BODY" > /dev/null
  done
  for id in "${STUDENT_IDS[@]}"; do call PATCH "/api/students/$id/status" "$ADMIN" "$LOCK_BODY" > /dev/null; done
  for id in "${GROUP_IDS[@]}";   do call DELETE "/api/student-groups/$id" "$ADMIN" > /dev/null; done
  printf '  da don\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

HELPERS=$(mktemp -t anthill-helpers.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
"""Vai phep tinh nho cho bo kiem. Tach ra file rieng de khoi long trich dan trong shell."""
import datetime
import json
import sys
import unicodedata

mode = sys.argv[1]

PHRASES = {
    "sap_mo": "còn chưa đầy 2 giờ nữa là mở nên không đổi giờ được",
    "dang_mo": "đang mở nên không đổi giờ được",
    "de": "Lịch chung của đề",
    "lop": "Bài của lớp",
    "em": "Bài của em này",
}


def nfc(text):
    return unicodedata.normalize("NFC", text)


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def show(value):
    if value is None:
        return "absent"
    return str(value).lower() if isinstance(value, bool) else str(value)


if mode == "iso":
    moment = datetime.datetime.now(datetime.UTC) + datetime.timedelta(hours=float(sys.argv[2]))
    print(moment.isoformat().replace("+00:00", "Z"))

elif mode == "says":
    # Than loi co chua cau chu $3 khong.
    with open(sys.argv[2], encoding="utf-8") as handle:
        text = nfc(handle.read())
    print("yes" if nfc(PHRASES[sys.argv[3]]) in text else "no")

elif mode == "assignment":
    # assignmentId cua de $3 trong danh sach bai cua hoc sinh.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["examId"] == int(sys.argv[3])), None)
    print("" if row is None else row["assignmentId"])

elif mode == "row":
    # Mot truong cua em $4 trong bang theo doi cua giao vien.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["studentId"] == int(sys.argv[4])), None)
    print("none" if row is None else show(row.get(sys.argv[3])))
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

at_hours()       { python "$HELPERS" iso "$1"; }
body_says()      { python "$HELPERS" says "$(to_native_path "$BODY_FILE")" "$1"; }
row_of_student() { python "$HELPERS" row "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
my_assignment()  { python "$HELPERS" assignment "$(to_native_path "$BODY_FILE")" "$1"; }

TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

# Mot lop da cong bo, co dung mot hoc sinh, giao vien demo phu trach. Tra ve qua BUILT_GID/BUILT_SID.
# Goi TRUC TIEP, khong trong $( ): subshell nuot mat moi phep gan.
build_class() {
  local name="$1" suffix="$2" code
  code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"$name\",\"teacherId\":$DEMO_TEACHER_ID}")
  [[ "$code" != "200" ]] && fail_fast "tao lop $name: HTTP $code — $(body)"
  BUILT_GID=$(json_field id); GROUP_IDS+=("$BUILT_GID")

  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS $suffix\",\"email\":\"p$suffix@kiemthu.vn\",\"username\":\"p$suffix\",\"code\":\"P$suffix\",\"password\":\"123456\",\"groupId\":$BUILT_GID}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  BUILT_SID=$(json_field id); STUDENT_IDS+=("$BUILT_SID"); MEMBERS+=("$BUILT_GID/$BUILT_SID")

  call POST "/api/student-groups/$BUILT_GID/publish" "$ADMIN" > /dev/null
}

# Them mot hoc sinh vao lop $1. Tra ve qua BUILT_SID.
add_student() {
  local group="$1" suffix="$2" code
  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS $suffix\",\"email\":\"q$suffix@kiemthu.vn\",\"username\":\"q$suffix\",\"code\":\"Q$suffix\",\"password\":\"123456\",\"groupId\":$group}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  BUILT_SID=$(json_field id); STUDENT_IDS+=("$BUILT_SID"); MEMBERS+=("$group/$BUILT_SID")
}

# Than de 9 cau. $1 tieu de, $2/$3 moc mo/dong ISO, $4 thoi luong (phut).
exam_body() {
  printf '{"title":"%s","topic":"","durationMinutes":%s,"totalQuestions":9,"easyPercentage":45,"mediumPercentage":33,"hardPercentage":22,"pointEasy":10,"pointMedium":10,"pointHard":15,"shuffleQuestions":true,"shuffleAnswers":true,"showResultAfterSubmit":true,"openAt":"%s","closeAt":"%s"}' \
    "$1" "$4" "$2" "$3"
}

# Tao de, tra id qua NEW_EID. $1 tieu de, $2/$3 gio mo/dong tinh tu bay gio (gio), $4 thoi luong (mac dinh 45).
new_exam() {
  local title="$1" duration="${4:-45}" code
  NEW_OPEN=$(at_hours "$2"); NEW_CLOSE=$(at_hours "$3")
  code=$(call POST /api/exams "$TEACHER" "$(exam_body "$title" "$NEW_OPEN" "$NEW_CLOSE" "$duration")")
  [[ "$code" != "200" ]] && fail_fast "tao de $title: HTTP $code — $(body)"
  NEW_EID=$(json_field id); EXAM_IDS+=("$NEW_EID")
}

# PATCH lich chung cua de $1 sang khung $2..$3 (gio tu bay gio). In ma HTTP.
patch_exam_schedule() {
  call PATCH "/api/exams/$1/schedule" "$TEACHER" "{\"openAt\":\"$(at_hours "$2")\",\"closeAt\":\"$(at_hours "$3")\"}"
}

# Than PUT dua de ve nhap: $1 tieu de, $2/$3 moc mo/dong ISO. Giu nguyen cau hinh, chi them lifecycle.
draft_body() {
  local body
  body=$(exam_body "$1" "$2" "$3" 45)
  printf '%s,"lifecycle":"draft"}' "${body%\}}"
}

# Gui lai lop $2 trong de $1 kem khung $3..$4 (gio tu bay gio). In ma HTTP.
resend_class() {
  call POST "/api/exams/$1/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[{\"groupId\":$2,\"openAt\":\"$(at_hours "$3")\",\"closeAt\":\"$(at_hours "$4")\"}],\"notify\":false}"
}

# Giao de $1 cho lop $2 theo lich cua de (khong kem gio rieng). In ma HTTP.
assign_class() {
  call POST "/api/exams/$1/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$2],\"notify\":false}"
}

section "Dung hai lop"
build_class "Khoa gio A $STAMP" "${STAMP}a"; G_A=$BUILT_GID; SID_A=$BUILT_SID
build_class "Khoa gio B $STAMP" "${STAMP}b"; G_B=$BUILT_GID; SID_B=$BUILT_SID
printf '  A %s · B %s\n' "$G_A" "$G_B"

section "De chua giao cho ai: sap toi gio mo van xep lai duoc"
new_exam "De chua giao $STAMP" 1 3; E0=$NEW_EID
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E0" "$TEACHER")"
expect_json   "  scheduleLocked = false" scheduleLocked false
expect_status "doi lich qua PATCH, con 1 gio → 200" 200 "$(patch_exam_schedule "$E0" 1.5 4)"

section "De da giao, con xa gio mo: doi duoc"
new_exam "De con xa $STAMP" 5 9; E1=$NEW_EID
expect_status "giao cho lop A → 200" 200 \
  "$(call POST "/api/exams/$E1/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_A],\"notify\":false}")"
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  scheduleLocked = false" scheduleLocked false
expect_status "doi lich qua PATCH, con 5 gio → 200" 200 "$(patch_exam_schedule "$E1" 6 10)"
expect_status "doi lich qua PUT, con 6 gio → 200" 200 \
  "$(call PUT "/api/exams/$E1" "$TEACHER" "$(exam_body "De con xa $STAMP" "$(at_hours 7)" "$(at_hours 11)" 45)")"

section "Dung quanh moc 120 phut"
expect_status "con 7 gio, doi sang con 123 phut → 200" 200 "$(patch_exam_schedule "$E1" 2.05 6)"
expect_status "con 123 phut van doi duoc, doi sang con 114 phut → 200" 200 "$(patch_exam_schedule "$E1" 1.9 6)"
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  con 114 phut: scheduleLocked = true" scheduleLocked true
expect_json   "  chua toi gio mo: lifecycle = scheduled" lifecycle scheduled
E1_OPEN=$(json_field openAt); E1_CLOSE=$(json_field closeAt)

section "Con duoi 120 phut: doi lich bi chan"
expect_status "doi lich qua PATCH → 409" 409 "$(patch_exam_schedule "$E1" 8 10)"
expect_status "  cau bao noi de sap mo" yes "$(body_says sap_mo)"
expect_status "  cau bao noi ve de" yes "$(body_says de)"
expect_status "doi lich qua PUT → 409" 409 \
  "$(call PUT "/api/exams/$E1" "$TEACHER" "$(exam_body "De con xa $STAMP" "$(at_hours 8)" "$(at_hours 10)" 45)")"
expect_status "  cau bao noi ve de" yes "$(body_says de)"
expect_status "PUT giu nguyen lich, doi ten → 200" 200 \
  "$(call PUT "/api/exams/$E1" "$TEACHER" "$(exam_body "De con xa doi ten $STAMP" "$E1_OPEN" "$E1_CLOSE" 45)")"
expect_json   "  ten da doi" title "De con xa doi ten $STAMP"
expect_json   "  lich khong doi" openAt "$E1_OPEN"

section "Lop theo lich de: khong gui lai gio moi duoc, giao them hoc sinh thi duoc"
add_student "$G_A" "${STAMP}a2"; SID_A2=$BUILT_SID
expect_status "gui lai lop A kem khung moi → 409" 409 "$(resend_class "$E1" "$G_A" 8 10)"
expect_status "  cau bao noi ve lop" yes "$(body_says lop)"
expect_status "gui lai lop A khong kem gio, them em moi → 200" 200 \
  "$(call POST "/api/exams/$E1/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_A],\"notify\":false}")"
expect_json   "  assignedCount = 1"        assignedCount 1
expect_json   "  scheduleUpdatedCount = 0" scheduleUpdatedCount 0
expect_status "gui lai lop A theo lich de nhu cu → 200" 200 \
  "$(call POST "/api/exams/$E1/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_A}],\"notify\":false}")"
expect_json   "  scheduleUpdatedCount = 0" scheduleUpdatedCount 0

section "Co khoa tren dong giao bai va tren khung gio cua lop"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E1/assignments" "$TEACHER")"
expect_status "  em A: khoa"  true "$(row_of_student scheduleLocked "$SID_A")"
expect_status "  em A2: khoa" true "$(row_of_student scheduleLocked "$SID_A2")"
expect_status "chi tiet de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  khung gio cua lop A: khoa" assignmentWindows.0.scheduleLocked true

section "De dang mo: khoa"
new_exam "De dang mo $STAMP" -1 3; E2=$NEW_EID
expect_status "giao cho lop B → 200" 200 \
  "$(call POST "/api/exams/$E2/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_B],\"notify\":false}")"
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E2" "$TEACHER")"
expect_json   "  scheduleLocked = true" scheduleLocked true
expect_json   "  lifecycle = open"      lifecycle open
expect_status "doi lich qua PATCH → 409" 409 "$(patch_exam_schedule "$E2" 6 9)"
expect_status "  cau bao noi de dang mo" yes "$(body_says dang_mo)"
expect_status "gui lai lop B kem khung moi → 409" 409 "$(resend_class "$E2" "$G_B" 6 9)"
expect_status "  cau bao noi dang mo" yes "$(body_says dang_mo)"

section "Lop co gio rieng: dong giao bai tu khoa theo gio cua lop"
new_exam "De giao lop $STAMP" -24 168; E3=$NEW_EID
expect_status "giao lop B, khung 5..7 gio nua → 200" 200 "$(resend_class "$E3" "$G_B" 5 7)"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E3/assignments" "$TEACHER")"
expect_status "  con xa: khong khoa" false "$(row_of_student scheduleLocked "$SID_B")"
expect_status "doi gio lop, con 5 gio → 200" 200 "$(resend_class "$E3" "$G_B" 8 10)"
expect_status "doi sang con 114 phut → 200" 200 "$(resend_class "$E3" "$G_B" 1.9 4)"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E3/assignments" "$TEACHER")"
expect_status "  con 114 phut: khoa" true "$(row_of_student scheduleLocked "$SID_B")"
B_OPEN=$(row_of_student openAt "$SID_B"); B_CLOSE=$(row_of_student closeAt "$SID_B")
expect_status "doi gio lop → 409" 409 "$(resend_class "$E3" "$G_B" 8 10)"
expect_status "  cau bao noi ve lop" yes "$(body_says lop)"
expect_status "  cau bao noi sap mo" yes "$(body_says sap_mo)"
expect_status "ve lich de (lop dang khoa) → 409" 409 \
  "$(call POST "/api/exams/$E3/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_B}],\"notify\":false}")"
expect_status "gui lai dung khung dang giu → 200 (khong doi gi)" 200 \
  "$(call POST "/api/exams/$E3/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_B,\"openAt\":\"$B_OPEN\",\"closeAt\":\"$B_CLOSE\"}],\"notify\":false}")"

section "Het gio dong: mo khoa lai de cuu em lo gio va mo lai de"
new_exam "De da dong $STAMP" -5 -1; E4=$NEW_EID
expect_status "giao lop A (theo lich de da dong) → 200" 200 "$(assign_class "$E4" "$G_A")"
expect_status "giao lop B → 200" 200 "$(assign_class "$E4" "$G_B")"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E4/assignments" "$TEACHER")"
expect_status "  em A2 khong khoa" false "$(row_of_student scheduleLocked "$SID_A2")"
expect_status "cuu lop A: dat gio rieng sau 5 gio → 200" 200 "$(resend_class "$E4" "$G_A" 5 7)"
expect_json   "  hai em cua lop A doi gio" scheduleUpdatedCount 2
expect_status "gui lai lop B kem khung moi → 200" 200 "$(resend_class "$E4" "$G_B" 6 9)"
expect_json   "  scheduleUpdatedCount = 1" scheduleUpdatedCount 1
expect_status "chi tiet de → 200" 200 "$(call GET "/api/exams/$E4" "$TEACHER")"
expect_json   "  de da dong: scheduleLocked = false" scheduleLocked false
expect_status "mo lai de qua PATCH → 200" 200 "$(patch_exam_schedule "$E4" 5 9)"

section "Dua de ve nhap roi doi lich: van bi khoa"
# De da giao ma chua ai lam thi dua ve nhap duoc. Neu nhap duoc mien khoa thi dua ve nhap, doi lich, xuat ban
# lai la lach duoc ma khong em nao duoc bao.
new_exam "De ve nhap $STAMP" 1 3; E5=$NEW_EID
expect_status "giao cho lop A → 200" 200 \
  "$(call POST "/api/exams/$E5/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_A],\"notify\":false}")"
expect_status "dua ve nhap, giu nguyen lich → 200" 200 \
  "$(call PUT "/api/exams/$E5" "$TEACHER" "$(draft_body "De ve nhap $STAMP" "$NEW_OPEN" "$NEW_CLOSE")")"
expect_json   "  lifecycle = draft"     lifecycle draft
expect_json   "  van khoa lich"         scheduleLocked true
expect_status "doi lich qua PATCH luc dang nhap → 409" 409 "$(patch_exam_schedule "$E5" 8 10)"
expect_status "doi lich qua PUT luc dang nhap → 409" 409 \
  "$(call PUT "/api/exams/$E5" "$TEACHER" "$(draft_body "De ve nhap $STAMP" "$(at_hours 8)" "$(at_hours 10)")")"

section "Mot request hai lop, mot lop khoa: khong lop nao bi doi"
new_exam "De hai lop $STAMP" -24 168; E6=$NEW_EID
expect_status "giao lop A khung 1..3 gio nua, lop B khung 6..8 gio nua → 200" 200 \
  "$(call POST "/api/exams/$E6/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_A,\"openAt\":\"$(at_hours 1)\",\"closeAt\":\"$(at_hours 3)\"},{\"groupId\":$G_B,\"openAt\":\"$(at_hours 6)\",\"closeAt\":\"$(at_hours 8)\"}],\"notify\":false}")"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E6/assignments" "$TEACHER")"
B_BEFORE=$(row_of_student openAt "$SID_B")
expect_status "gui lai ca hai lop kem khung moi → 409" 409 \
  "$(call POST "/api/exams/$E6/assignments" "$TEACHER" "{\"studentIds\":[],\"classes\":[{\"groupId\":$G_A,\"openAt\":\"$(at_hours 9)\",\"closeAt\":\"$(at_hours 11)\"},{\"groupId\":$G_B,\"openAt\":\"$(at_hours 9)\",\"closeAt\":\"$(at_hours 11)\"}],\"notify\":false}")"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E6/assignments" "$TEACHER")"
expect_status "  lop B (con xa) khong bi doi do lop A khoa" "$B_BEFORE" "$(row_of_student openAt "$SID_B")"

section "Cap gio chung cua request cung bi khoa khi kem groupIds"
expect_status "gui lai lop B qua groupIds kem cap gio chung, de dang mo → 409" 409 \
  "$(call POST "/api/exams/$E2/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_B],\"openAt\":\"$(at_hours 6)\",\"closeAt\":\"$(at_hours 9)\",\"notify\":false}")"

section "Lop theo lich de tren de dang mo: dat gio rieng cung bi chan"
expect_status "giao lop A (theo lich de dang mo) → 200" 200 "$(assign_class "$E2" "$G_A")"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$E2/assignments" "$TEACHER")"
expect_status "  em A2 khoa" true "$(row_of_student scheduleLocked "$SID_A2")"
expect_status "dat gio rieng cho lop A → 409" 409 "$(resend_class "$E2" "$G_A" 8 10)"
expect_status "  cau bao noi dang mo" yes "$(body_says dang_mo)"

section "Em da bat dau lam bai: gui lai lop (chi con em do) khong bi chan va khong doi gio em"
new_exam "De da bat dau $STAMP" -1 3; E7=$NEW_EID
expect_status "giao lop B (mot em) → 200" 200 \
  "$(call POST "/api/exams/$E7/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_B],\"notify\":false}")"
TOK_B=$(login_and_clear_must_change "p${STAMP}b" "123456" "Pb$STAMP")
call GET /api/my/exams "$TOK_B" > /dev/null
AID_STARTED=$(my_assignment "$E7")
expect_status "em B vao thi → 200" 200 "$(call POST /api/exam-attempts "$TOK_B" "{\"assignmentId\":$AID_STARTED}")"
expect_status "gui lai lop B kem khung moi → 200" 200 "$(resend_class "$E7" "$G_B" 8 10)"
expect_json   "  scheduleUpdatedCount = 0"     scheduleUpdatedCount 0
expect_json   "  scheduleKeptStartedCount = 1" scheduleKeptStartedCount 1

summary
