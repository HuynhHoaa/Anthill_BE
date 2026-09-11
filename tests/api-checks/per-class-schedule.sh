#!/usr/bin/env bash
# Mot de, nhieu lop, moi lop mot khung gio rieng (§4.6, ban sua doi).
#
# Chay:  bash tests/api-checks/per-class-schedule.sh   (API phai dang chay o $HOST)
#
# Lich cua de chi la MAC DINH. Tung luot giao co the dat gio rieng, vi mot de thuong duoc dung cho
# nhieu lop ma moi lop hoc vao mot tiet khac nhau — 12A1 tiet 2 sang thu Ba, 12A2 tiet 4 chieu thu
# Nam. Truoc do giao vien phai nhan ban de ra lam hai.
#
# Bo kiem nay canh sau dieu:
#   1. Hai em cung mot de, cung mot thoi diem, thay hai trang thai khac nhau — viec chan la CHAN THAT
#      o server, va status voi lifecycle tren cung mot the bai khong bao gio noi nguoc nhau. Ban cu chi
#      kiem lifecycle nen van qua trong khi status sai.
#   2. Trang tong quan xep bai theo khung gio cua lop, khong theo lich chung cua de.
#   3. Gui lai mot lop kem khung gio moi la sua gio THAT cho em chua bat dau, va bao ra con so; gui lai
#      khong kem khung gio thi khong xoa gi.
#   4. Khung gio rieng ngan hon thoi luong lam bai bi tu choi.
#   5. Diem mo ngay khi cham, nhung dap an tung cau cho toi khi MOI lop da het gio.
#   6. Xoa mot lop da tung duoc giao bai khong lam hong bai cua cac em.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@eduquiz.vn" "eduquiz123")
TEACHER=$(login "an.nv@eduquiz.vn" "eduquiz123")

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

HELPERS=$(mktemp -t eduquiz-helpers.XXXXXX.py)
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


if mode == "iso":
    moment = now() + datetime.timedelta(hours=float(sys.argv[2]))
    print(moment.isoformat().replace("+00:00", "Z"))

elif mode == "field":
    # Mot truong cua de $4 trong danh sach bai cua hoc sinh.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["examId"] == int(sys.argv[4])), None)
    print("none" if row is None else show(row.get(sys.argv[3])))

elif mode == "has":
    rows = load(sys.argv[2])
    print("yes" if any(x["examId"] == int(sys.argv[3]) for x in rows) else "no")

elif mode == "row":
    # Mot truong cua em $4 trong bang theo doi cua giao vien.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["studentId"] == int(sys.argv[4])), None)
    print("none" if row is None else show(row.get(sys.argv[3])))

elif mode == "bucket":
    data = load(sys.argv[2])
    exam = int(sys.argv[3])
    keys = [k for k in ("openExams", "upcomingExams") if any(x["examId"] == exam for x in data[k])]
    print("+".join(keys) if keys else "none")

elif mode == "index":
    data = load(sys.argv[2])
    ids = [x["examId"] for x in data["openExams"]]
    exam = int(sys.argv[3])
    print(ids.index(exam) if exam in ids else -1)

elif mode == "dash_field":
    data = load(sys.argv[2])
    row = next((x for x in data["openExams"] + data["upcomingExams"] if x["examId"] == int(sys.argv[4])), None)
    print("none" if row is None else show(row.get(sys.argv[3])))

elif mode == "has_field":
    print("present" if sys.argv[3] in load(sys.argv[2]) else "absent")

elif mode == "q0":
    print("present" if sys.argv[3] in load(sys.argv[2])["questions"][0] else "absent")

elif mode == "near":
    # Hai moc cach nhau khong qua 2 giay.
    value = load(sys.argv[2]).get(sys.argv[3])
    if value is None:
        print("absent")
    else:
        gap = abs((parse(value) - parse(sys.argv[4])).total_seconds())
        print("true" if gap <= 2 else "false (%s)" % value)

elif mode == "wait":
    # Doi toi khi moc $2 da qua han mot giay.
    target = parse(sys.argv[2]) + datetime.timedelta(seconds=1)
    while now() <= target:
        time.sleep(0.5)

elif mode == "expiry":
    session = load(sys.argv[2])
    print(round((parse(session["expiresAt"]) - now()).total_seconds() / 3600, 2))
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

# Moc thoi gian cach bay gio $1 gio, dinh dang ISO — backend chi doc ISO.
at_hours()           { python "$HELPERS" iso "$1"; }
# Mot truong cua de $2 trong danh sach bai cua hoc sinh dang o $BODY_FILE.
field_of_exam()      { python "$HELPERS" field "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
has_exam()           { python "$HELPERS" has "$(to_native_path "$BODY_FILE")" "$1"; }
# Truong $1 cua em co id $2 trong bang theo doi cua giao vien.
row_of_student()     { python "$HELPERS" row "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
# De $1 nam o danh sach nao cua trang tong quan hoc sinh.
bucket_of_exam()     { python "$HELPERS" bucket "$(to_native_path "$BODY_FILE")" "$1"; }
open_index_of_exam() { python "$HELPERS" index "$(to_native_path "$BODY_FILE")" "$1"; }
dash_field()         { python "$HELPERS" dash_field "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
has_field()          { python "$HELPERS" has_field "$(to_native_path "$BODY_FILE")" "$1"; }
q0_has()             { python "$HELPERS" q0 "$(to_native_path "$BODY_FILE")" "$1"; }
near_time()          { python "$HELPERS" near "$(to_native_path "$BODY_FILE")" "$1" "$2"; }
wait_past()          { python "$HELPERS" wait "$1"; }
hours_until_expiry() { python "$HELPERS" expiry "$(to_native_path "$BODY_FILE")"; }
differ()             { if [[ "$1" != "$2" ]]; then echo true; else echo false; fi; }

TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

# Mot lop da cong bo, co dung mot hoc sinh, giao vien demo phu trach. Tra ve qua BUILT_GID/BUILT_SID.
#
# Goi TRUC TIEP, khong trong $( ): subshell nuot mat moi phep gan, va ban truoc cua ham nay vi the
# khong bao gio don duoc lop va hoc sinh no tao ra.
build_class() {
  local name="$1" suffix="$2" code
  code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"$name\"}")
  [[ "$code" != "200" ]] && fail_fast "tao lop $name: HTTP $code — $(body)"
  BUILT_GID=$(json_field id); GROUP_IDS+=("$BUILT_GID")

  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS $suffix\",\"email\":\"p$suffix@kiemthu.vn\",\"username\":\"p$suffix\",\"code\":\"P$suffix\",\"password\":\"eduquiz123\",\"groupId\":$BUILT_GID}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  BUILT_SID=$(json_field id); STUDENT_IDS+=("$BUILT_SID"); MEMBERS+=("$BUILT_GID/$BUILT_SID")

  call POST "/api/student-groups/$BUILT_GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}" > /dev/null
  call POST "/api/student-groups/$BUILT_GID/publish" "$ADMIN" > /dev/null
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

# Than request giao de cho mot lop. $1 lop, $2/$3 moc mo/dong ISO (bo trong = theo lich chung).
assign_body() {
  local window=""
  [[ -n "${2:-}" ]] && window=",\"openAt\":\"$2\",\"closeAt\":\"$3\""
  printf '{"studentIds":[],"groupIds":[%s],"notify":false%s}' "$1" "$window"
}

# Giao de $1 cho lop $2, khung gio $3..$4 tinh bang gio tu bay gio (bo trong = theo lich chung). In ma HTTP.
assign() {
  local exam="$1" group="$2" payload
  if [[ -n "${3:-}" ]]; then
    payload=$(assign_body "$group" "$(at_hours "$3")" "$(at_hours "$4")")
  else
    payload=$(assign_body "$group")
  fi
  call POST "/api/exams/$exam/assignments" "$TEACHER" "$payload"
}

section "Dung hai lop, moi lop mot hoc sinh"
build_class "Lop sang $STAMP" "${STAMP}s";  G_SANG=$BUILT_GID;  SID_SANG=$BUILT_SID
build_class "Lop chieu $STAMP" "${STAMP}c"; G_CHIEU=$BUILT_GID; SID_CHIEU=$BUILT_SID
printf '  lop sang %s · lop chieu %s\n' "$G_SANG" "$G_CHIEU"

section "De co lich chung rong: mo tu hom qua den tuan sau"
new_exam "De hai khung gio $STAMP" -24 168
EID=$NEW_EID; EID_TITLE="De hai khung gio $STAMP"; EID_OPEN=$NEW_OPEN; EID_CLOSE=$NEW_CLOSE
printf '  de %s\n' "$EID"

section "Giao hai lop voi hai khung gio khac nhau"
# Ca hai khung deu nam TRONG lich chung cua de. Neu he thong doc lich de thi ca hai em se cung thay
# "dang mo" — do chinh la loi bo kiem nay tim.
expect_status "lop sang, gio da qua → 200"      200 "$(assign "$EID" "$G_SANG" -4 -2)"
expect_status "lop chieu, dang trong gio → 200" 200 "$(assign "$EID" "$G_CHIEU" -1 3)"

section "Cung mot de, hai em thay hai trang thai khac nhau"
TOK_SANG=$(login "p${STAMP}s" "eduquiz123")
TOK_CHIEU=$(login "p${STAMP}c" "eduquiz123")

expect_status "em lop sang doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_SANG")"
expect_status "  lifecycle = closed" closed "$(field_of_exam lifecycle "$EID")"
expect_status "  canStart = false"   false  "$(field_of_exam canStart "$EID")"
# Lifecycle va status nam canh nhau tren cung mot the bai. Ban cu kiem moi lifecycle nen van qua
# trong khi status doc lich chung cua de va bao "assigned" — nhu the em con kip lam.
expect_status "  status = expired (lo khung gio cua lop)" expired "$(field_of_exam status "$EID")"
OPEN_SANG=$(field_of_exam openAt "$EID")
AID_SANG=$(field_of_exam assignmentId "$EID")

expect_status "em lop chieu doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_CHIEU")"
expect_status "  lifecycle = open"  open     "$(field_of_exam lifecycle "$EID")"
expect_status "  canStart = true"   true     "$(field_of_exam canStart "$EID")"
expect_status "  status = assigned" assigned "$(field_of_exam status "$EID")"
OPEN_CHIEU=$(field_of_exam openAt "$EID")
AID_CHIEU=$(field_of_exam assignmentId "$EID")

expect_status "hai em nhan hai moc mo bai KHAC nhau" true "$(differ "$OPEN_SANG" "$OPEN_CHIEU")"

section "Bo loc trang thai theo khung gio cua lop"
expect_status "loc ?status=expired → 200" 200 "$(call GET "/api/my/exams?status=expired" "$TOK_SANG")"
expect_status "  co de cua lop sang"      yes "$(has_exam "$EID")"
expect_status "loc ?status=assigned → 200" 200 "$(call GET "/api/my/exams?status=assigned" "$TOK_SANG")"
expect_status "  KHONG co de cua lop sang" no "$(has_exam "$EID")"

section "Trang tong quan xep bai theo khung gio cua lop"
expect_status "em lop sang doc dashboard → 200" 200 "$(call GET /api/dashboard/student "$TOK_SANG")"
expect_status "  lop sang het gio rieng: khong o danh sach nao" none "$(bucket_of_exam "$EID")"
expect_status "em lop chieu doc dashboard → 200" 200 "$(call GET /api/dashboard/student "$TOK_CHIEU")"
expect_status "  lop chieu dang trong gio: o dang mo" openExams "$(bucket_of_exam "$EID")"

section "Chan THAT o server, khong chi an nut"
# Chi an nut tren giao dien thi go thang URL van vao lam duoc — day la phep kiem phan biet hai
# chuyen do.
CODE=$(call POST /api/exam-attempts "$TOK_SANG" "{\"assignmentId\":$AID_SANG}")
case "$CODE" in
  403|409|410) BI_CHAN=true ;;
  *)           BI_CHAN=false ;;
esac
expect_status "em lop sang vao lam → bi tu choi" true "$BI_CHAN"

expect_status "em lop chieu vao lam → 200" 200 "$(call POST /api/exam-attempts "$TOK_CHIEU" "{\"assignmentId\":$AID_CHIEU}")"
# Han gio phai cat theo moc dong cua LOP, khong phai moc dong cua de (con tron mot tuan).
CON_LAI=$(hours_until_expiry)
if python -c "import sys; sys.exit(0 if float(sys.argv[1]) <= 3.1 else 1)" "$CON_LAI"; then TRONG_KHUNG=true; else TRONG_KHUNG=false; fi
expect_status "  han gio nam trong khung cua lop, khong phai cua de" true "$TRONG_KHUNG"

section "Nua moc gio bi tu choi"
# Dat mot nua la trang thai khong ai co y dinh tao ra.
NUA_MO_BODY="{\"studentIds\":[],\"groupIds\":[$G_CHIEU],\"notify\":false,\"openAt\":\"$(at_hours 1)\"}"
NUA_DONG_BODY="{\"studentIds\":[],\"groupIds\":[$G_CHIEU],\"notify\":false,\"closeAt\":\"$(at_hours 3)\"}"
expect_status "chi gui openAt → 400"         400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$NUA_MO_BODY")"
expect_status "chi gui closeAt → 400"        400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$NUA_DONG_BODY")"
expect_status "gio dong truoc gio mo → 400"  400 "$(assign "$EID" "$G_CHIEU" 3 1)"

section "Khong dat gio rieng thi theo lich chung cua de"
build_class "Lop thuong $STAMP" "${STAMP}t"; G_THUONG=$BUILT_GID; SID_THUONG=$BUILT_SID
expect_status "giao khong kem gio rieng → 200" 200 "$(assign "$EID" "$G_THUONG")"
TOK_THUONG=$(login "p${STAMP}t" "eduquiz123")
expect_status "em lop thuong doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_THUONG")"
expect_status "  lifecycle = open theo lich de" open     "$(field_of_exam lifecycle "$EID")"
expect_status "  status = assigned"             assigned "$(field_of_exam status "$EID")"

section "Giao vien thay dung trang thai va khung gio cua tung em"
expect_status "bang theo doi cua de → 200" 200 "$(call GET "/api/exams/$EID/assignments" "$TEACHER")"
expect_status "  lop sang lo gio → expired"       expired    "$(row_of_student status "$SID_SANG")"
expect_status "  lop chieu dang lam → inprogress" inprogress "$(row_of_student status "$SID_CHIEU")"
expect_status "  lop thuong → assigned"           assigned   "$(row_of_student status "$SID_THUONG")"
expect_status "  lop sang mang khung gio rieng"   true       "$(row_of_student hasScheduleOverride "$SID_SANG")"
expect_status "  lop sang: openAt la gio cua lop" "$OPEN_SANG" "$(row_of_student openAt "$SID_SANG")"
expect_status "  lop thuong theo lich chung"      false      "$(row_of_student hasScheduleOverride "$SID_THUONG")"
expect_status "  ghi lai lop da giao bai qua do"  "$G_SANG"  "$(row_of_student assignedViaGroupId "$SID_SANG")"

section "Lich chung da dong nhung lop con trong gio"
# Lich chung da dong tu hom qua; lop chieu duoc mo lai trong khung rieng. Ban cu bao "expired" ngay
# trong gio cua lop, va trang tong quan danh roi bai khoi ca hai danh sach.
new_exam "De da dong $STAMP" -48 -24; EID2=$NEW_EID
expect_status "giao lop chieu khung dang mo → 200" 200 "$(assign "$EID2" "$G_CHIEU" -1 3)"
expect_status "em lop chieu doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_CHIEU")"
expect_status "  lifecycle = open"  open     "$(field_of_exam lifecycle "$EID2")"
expect_status "  status = assigned" assigned "$(field_of_exam status "$EID2")"
expect_status "  canStart = true"   true     "$(field_of_exam canStart "$EID2")"
expect_status "em lop chieu doc dashboard → 200" 200 "$(call GET /api/dashboard/student "$TOK_CHIEU")"
expect_status "  bai nam o dang mo, khong bien mat" openExams "$(bucket_of_exam "$EID2")"
expect_status "  the bai trong dashboard: canStart = true" true "$(dash_field canStart "$EID2")"

section "Trang tong quan: dung danh sach, dung thu tu"
new_exam "De tuong lai $STAMP" 24 48;    E_TUONG_LAI=$NEW_EID
new_exam "De sap dong $STAMP" -24 168;   E_SAP=$NEW_EID
new_exam "De dong muon $STAMP" -24 2;    E_MUON=$NEW_EID
new_exam "De lop sau $STAMP" -24 168;    E_SAU=$NEW_EID
expect_status "de mai mo, lop chieu mo tu gio → 200"         200 "$(assign "$E_TUONG_LAI" "$G_CHIEU" -1 3)"
expect_status "de mo ca tuan, lop chieu dong sau 1 gio → 200" 200 "$(assign "$E_SAP" "$G_CHIEU" -1 1)"
expect_status "de dong sau 2 gio, theo lich chung → 200"      200 "$(assign "$E_MUON" "$G_CHIEU")"
expect_status "de dang mo, lop chieu 5 gio nua moi mo → 200"  200 "$(assign "$E_SAU" "$G_CHIEU" 5 7)"
expect_status "em lop chieu doc dashboard → 200" 200 "$(call GET /api/dashboard/student "$TOK_CHIEU")"
expect_status "  de mai mo nhung lop dang trong gio → dang mo" openExams     "$(bucket_of_exam "$E_TUONG_LAI")"
expect_status "  de dang mo nhung lop chua toi gio → sap toi"  upcomingExams "$(bucket_of_exam "$E_SAU")"
I_SAP=$(open_index_of_exam "$E_SAP"); I_MUON=$(open_index_of_exam "$E_MUON")
if (( I_SAP >= 0 && I_SAP < I_MUON )); then SAP_TRUOC=true; else SAP_TRUOC=false; fi
# Frontend lay phan tu dau cua "dang mo" lam canh bao sap het han: lop dong sau 1 gio phai dung
# truoc de dong sau 2 gio, du lich chung cua no con ca tuan.
expect_status "  sap theo gio dong cua lop, khong theo de" true "$SAP_TRUOC"

section "Gui lai mot lop kem khung gio moi thi khung gio duoc sua that"
build_class "Lop sua gio $STAMP" "${STAMP}u"; G_SUA=$BUILT_GID; SID_SUA=$BUILT_SID
TOK_SUA=$(login "p${STAMP}u" "eduquiz123")
expect_status "giao lan dau, 1..3 gio nua → 200" 200 "$(assign "$EID" "$G_SUA" 1 3)"
expect_json   "  assignedCount = 1"        assignedCount 1
expect_json   "  scheduleUpdatedCount = 0" scheduleUpdatedCount 0
call GET /api/my/exams "$TOK_SUA" > /dev/null
OPEN_A=$(field_of_exam openAt "$EID")

# Giu nguyen chuoi than request de lan gui thu hai giong het tung ky tu.
SUA_BODY=$(assign_body "$G_SUA" "$(at_hours 5)" "$(at_hours 7)")
expect_status "gui lai cung lop, 5..7 gio nua → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$SUA_BODY")"
expect_json   "  assignedCount = 0"          assignedCount 0
expect_json   "  skippedAlreadyAssigned = 1" skippedAlreadyAssigned 1
expect_json   "  scheduleUpdatedCount = 1"   scheduleUpdatedCount 1
expect_status "em doc lai danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_SUA")"
OPEN_B=$(field_of_exam openAt "$EID")
expect_status "  moc mo bai da doi"  true      "$(differ "$OPEN_A" "$OPEN_B")"
expect_status "  lifecycle = scheduled" scheduled "$(field_of_exam lifecycle "$EID")"
expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$EID/assignments" "$TEACHER")"
expect_status "  giao vien thay dung moc moi"   "$OPEN_B" "$(row_of_student openAt "$SID_SUA")"
expect_status "  hasScheduleOverride = true"    true      "$(row_of_student hasScheduleOverride "$SID_SUA")"
expect_status "  assignedViaGroupId = lop sua"  "$G_SUA"  "$(row_of_student assignedViaGroupId "$SID_SUA")"

section "Gui lai cung khung gio thi khong dem lai"
expect_status "gui lai y het → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$SUA_BODY")"
expect_json   "  scheduleUpdatedCount = 0" scheduleUpdatedCount 0

section "Gui lai KHONG kem khung gio thi khong xoa khung gio da dat"
expect_status "gui lai khong kem gio → 200" 200 "$(assign "$EID" "$G_SUA")"
expect_json   "  scheduleUpdatedCount = 0" scheduleUpdatedCount 0
call GET /api/my/exams "$TOK_SUA" > /dev/null
expect_status "  em van giu moc da sua" "$OPEN_B" "$(field_of_exam openAt "$EID")"
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  giao vien van thay khung gio rieng" true "$(row_of_student hasScheduleOverride "$SID_SUA")"

section "Em da bat dau lam bai thi giu nguyen khung gio cu"
expect_status "gui lai lop chieu, 10..12 gio nua → 200" 200 "$(assign "$EID" "$G_CHIEU" 10 12)"
expect_json   "  scheduleUpdatedCount = 0"     scheduleUpdatedCount 0
expect_json   "  scheduleKeptStartedCount = 1" scheduleKeptStartedCount 1
call GET /api/my/exams "$TOK_CHIEU" > /dev/null
expect_status "  em lop chieu van giu moc cu" "$OPEN_CHIEU" "$(field_of_exam openAt "$EID")"

section "Khung gio rieng ngan hon thoi luong lam bai bi tu choi"
# De cho lam 45 phut. Khung 20 phut thi AttemptDeadline se cat bai ca lop o phut thu 20.
build_class "Lop ngan $STAMP" "${STAMP}n"; G_NGAN=$BUILT_GID
expect_status "khung 20 phut → 400"               400 "$(assign "$EID" "$G_NGAN" 1 1.3333)"
expect_status "khung vua dung 45 phut → 200"      200 "$(assign "$EID" "$G_NGAN" 1 1.75)"
expect_json   "  assignedCount = 1 (lan 400 khong tao dong nao)" assignedCount 1
expect_status "sua gio lop da giao thanh 20 phut → 400" 400 "$(assign "$EID" "$G_SUA" 1 1.3333)"
call GET /api/my/exams "$TOK_SUA" > /dev/null
expect_status "  lan 400 khong doi khung gio nao" "$OPEN_B" "$(field_of_exam openAt "$EID")"
# Chieu nguoc lai: tang thoi luong vuot khung rieng cua mot lop chua ai bat dau.
expect_status "tang thoi luong len 150 phut → 400" 400 \
  "$(call PUT "/api/exams/$EID" "$TEACHER" "$(exam_body "$EID_TITLE" "$EID_OPEN" "$EID_CLOSE" 150)")"
expect_status "giu 45 phut → 200" 200 \
  "$(call PUT "/api/exams/$EID" "$TEACHER" "$(exam_body "$EID_TITLE" "$EID_OPEN" "$EID_CLOSE" 45)")"

section "Giao kem gio rieng va thong bao: buoc ban su kien sau commit khong hong"
build_class "Lop bao $STAMP" "${STAMP}b"; G_BAO=$BUILT_GID
BAO_BODY="{\"studentIds\":[],\"groupIds\":[$G_BAO],\"notify\":true,\"openAt\":\"$(at_hours -1)\",\"closeAt\":\"$(at_hours 3)\"}"
expect_status "giao kem gio rieng, notify=true → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$BAO_BODY")"
TOK_BAO=$(login "p${STAMP}b" "eduquiz123")
expect_status "em doc so chua doc → 200" 200 "$(call GET /api/notifications/unread-count "$TOK_BAO")"
expect_status "  dung 1 thong bao" 1 "$(body)"

section "Diem mo ngay, dap an cho lop dong muon nhat"
# Lop A dang trong gio va sap dong; lop B mai moi thi. Em lop A nop xong thay diem ngay, nhung dap an
# tung cau chua duoc roi server — ke ca khi chinh gio cua lop A da het — cho toi khi lop B cung het gio.
build_class "Lop A $STAMP" "${STAMP}ka"; G_KA=$BUILT_GID
build_class "Lop B $STAMP" "${STAMP}kb"; G_KB=$BUILT_GID
new_exam "De dap an $STAMP" -24 168 1; E_DA=$NEW_EID
KA_CLOSE=$(at_hours 0.0125)
KB_CLOSE=$(at_hours 26)
expect_status "lop A, dong sau 45 giay → 200" 200 \
  "$(call POST "/api/exams/$E_DA/assignments" "$TEACHER" "$(assign_body "$G_KA" "$(at_hours -1)" "$KA_CLOSE")")"
expect_status "lop B, mai moi thi → 200" 200 \
  "$(call POST "/api/exams/$E_DA/assignments" "$TEACHER" "$(assign_body "$G_KB" "$(at_hours 24)" "$KB_CLOSE")")"

TOK_KA=$(login "p${STAMP}ka" "eduquiz123")
call GET /api/my/exams "$TOK_KA" > /dev/null
AID_KA=$(field_of_exam assignmentId "$E_DA")
expect_status "em lop A vao lam → 200" 200 "$(call POST /api/exam-attempts "$TOK_KA" "{\"assignmentId\":$AID_KA}")"
ATT_KA=$(json_field attemptId)
expect_status "em lop A nop bai → 200" 200 "$(call POST "/api/exam-attempts/$ATT_KA/submit" "$TOK_KA" '{"answers":[]}')"

expect_status "em lop A xem chi tiet → 200" 200 "$(call GET "/api/results/$ATT_KA" "$TOK_KA")"
expect_status "  co score"                         present "$(has_field score)"
expect_json   "  canSeeAnswers = false"            canSeeAnswers false
expect_status "  cau dau KHONG co correctAnswerId" absent "$(q0_has correctAnswerId)"
expect_status "  cau dau KHONG co isCorrect"       absent "$(q0_has isCorrect)"
expect_status "  answersVisibleAt = gio dong cua lop B" true "$(near_time answersVisibleAt "$KB_CLOSE")"

expect_status "giao vien xem chinh bai do → 200" 200 "$(call GET "/api/results/$ATT_KA" "$TEACHER")"
expect_json   "  canSeeAnswers = true voi giao vien" canSeeAnswers true
expect_status "  cau dau co correctAnswerId"         present "$(q0_has correctAnswerId)"
expect_status "  answersVisibleAt vang mat"          absent  "$(has_field answersVisibleAt)"

printf '  doi khung gio cua lop A dong...\n'
wait_past "$KA_CLOSE"
expect_status "lop A da het gio, lop B chua thi → 200" 200 "$(call GET "/api/results/$ATT_KA" "$TOK_KA")"
expect_json   "  canSeeAnswers van false"             canSeeAnswers false
expect_status "  cau dau van KHONG co correctAnswerId" absent "$(q0_has correctAnswerId)"

# Giao vien doi lop B ve mot khung gio da qua. Em lop B chua bat dau nen khung moi ap ngay.
expect_status "doi lop B ve khung da qua → 200" 200 "$(assign "$E_DA" "$G_KB" -3 -2)"
expect_json   "  scheduleUpdatedCount = 1" scheduleUpdatedCount 1
expect_status "moi lop da het gio → 200" 200 "$(call GET "/api/results/$ATT_KA" "$TOK_KA")"
expect_json   "  canSeeAnswers = true"            canSeeAnswers true
expect_status "  cau dau co correctAnswerId"       present "$(q0_has correctAnswerId)"
expect_status "  cau dau co isCorrect"             present "$(q0_has isCorrect)"
expect_status "  answersVisibleAt vang mat"        absent  "$(has_field answersVisibleAt)"

section "Xoa lop da tung giao bai khong lam hong bai cua em"
build_class "Lop xoa $STAMP" "${STAMP}x"; G_XOA=$BUILT_GID; SID_XOA=$BUILT_SID
TOK_XOA=$(login "p${STAMP}x" "eduquiz123")
expect_status "giao lop xoa, 2..4 gio nua → 200" 200 "$(assign "$EID" "$G_XOA" 2 4)"
call GET /api/my/exams "$TOK_XOA" > /dev/null
OPEN_XOA=$(field_of_exam openAt "$EID")
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  cot lop giao bai duoc ghi va doc lai" "$G_XOA" "$(row_of_student assignedViaGroupId "$SID_XOA")"
expect_status "go em khoi lop → 200" 200 "$(call DELETE "/api/student-groups/$G_XOA/students/$SID_XOA" "$ADMIN")"
# Khoa ngoai Restrict thi day la 500 tu vi pham 23503; SET NULL thi lop xoa duoc.
expect_status "xoa lop da tung giao bai → 200" 200 "$(call DELETE "/api/student-groups/$G_XOA" "$ADMIN")"
expect_status "em doc lai danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_XOA")"
expect_status "  khung gio rieng con nguyen" "$OPEN_XOA" "$(field_of_exam openAt "$EID")"
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  giao vien van thay khung gio rieng"   true   "$(row_of_student hasScheduleOverride "$SID_XOA")"
expect_status "  dau lop giao bai da go (vang mat)"    absent "$(row_of_student assignedViaGroupId "$SID_XOA")"

summary
