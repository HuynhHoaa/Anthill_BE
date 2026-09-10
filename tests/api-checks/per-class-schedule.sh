#!/usr/bin/env bash
# Mot de, nhieu lop, moi lop mot khung gio rieng (§4.6, ban sua doi).
#
# Chay:  bash tests/api-checks/per-class-schedule.sh   (API phai dang chay o $HOST)
#
# Lich cua de chi la MAC DINH. Tung luot giao co the dat gio rieng, vi mot de thuong duoc dung cho
# nhieu lop ma moi lop hoc vao mot tiet khac nhau — 12A1 tiet 2 sang thu Ba, 12A2 tiet 4 chieu thu
# Nam. Truoc do giao vien phai nhan ban de ra lam hai.
#
# Bo kiem nay canh dung mot dieu: hai em cung mot de, cung mot thoi diem, phai thay hai trang thai
# khac nhau — va viec chan phai la CHAN THAT o server, khong phai chi an nut tren giao dien.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@eduquiz.vn" "eduquiz123")
TEACHER=$(login "an.nv@eduquiz.vn" "eduquiz123")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); EXAM_IDS=(); STUDENT_IDS=()

cleanup() {
  section "Don du lieu"
  for id in "${EXAM_IDS[@]}";    do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  for id in "${STUDENT_IDS[@]}"; do call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null; done
  for id in "${GROUP_IDS[@]}";   do call DELETE "/api/student-groups/$id" "$ADMIN" > /dev/null; done
  printf '  da don\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

# Moc thoi gian cach bay gio $1 gio, dinh dang ISO — backend chi doc ISO.
at_hours() {
  python "$HELPERS" iso "$1"
}

# Doc mot truong cua de dang xet tu danh sach bai cua hoc sinh dang o $BODY_FILE.
field_of_exam() {
  python "$HELPERS" field "$(to_native_path "$BODY_FILE")" "$1" "$2"
}

# So gio con lai den han nop cua luot lam bai dang o $BODY_FILE.
hours_until_expiry() {
  python "$HELPERS" expiry "$(to_native_path "$BODY_FILE")"
}

HELPERS=$(mktemp -t eduquiz-helpers.XXXXXX.py)
cat > "$HELPERS" <<'PYHELPER'
"""Vai phep tinh nho cho bo kiem. Tach ra file rieng de khoi long trich dan trong shell."""
import datetime
import json
import sys

mode = sys.argv[1]

if mode == "iso":
    moment = datetime.datetime.now(datetime.UTC) + datetime.timedelta(hours=float(sys.argv[2]))
    print(moment.isoformat().replace("+00:00", "Z"))

elif mode == "field":
    with open(sys.argv[2], encoding="utf-8") as handle:
        rows = json.load(handle)
    row = next(x for x in rows if x["examId"] == int(sys.argv[4]))
    value = row[sys.argv[3]]
    print(str(value).lower() if isinstance(value, bool) else value)

elif mode == "expiry":
    with open(sys.argv[2], encoding="utf-8") as handle:
        session = json.load(handle)
    expires = datetime.datetime.fromisoformat(session["expiresAt"].replace("Z", "+00:00"))
    print(round((expires - datetime.datetime.now(datetime.UTC)).total_seconds() / 3600, 2))
PYHELPER
HELPERS=$(to_native_path "$HELPERS")
trap 'rm -f "$HELPERS"; cleanup' EXIT

TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

# Mot lop da cong bo, co dung mot hoc sinh, giao vien demo phu trach.
build_class() {
  local name="$1" suffix="$2" code gid
  code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"$name\"}")
  [[ "$code" != "200" ]] && fail_fast "tao lop $name: HTTP $code — $(body)"
  gid=$(json_field id); GROUP_IDS+=("$gid")

  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS $suffix\",\"email\":\"p$suffix@kiemthu.vn\",\"username\":\"p$suffix\",\"code\":\"P$suffix\",\"password\":\"eduquiz123\",\"groupId\":$gid}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  STUDENT_IDS+=("$(json_field id)")

  call POST "/api/student-groups/$gid/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}" > /dev/null
  call POST "/api/student-groups/$gid/publish" "$ADMIN" > /dev/null
  echo "$gid"
}

section "Dung hai lop, moi lop mot hoc sinh"
G_SANG=$(build_class "Lop sang $STAMP" "${STAMP}s")
G_CHIEU=$(build_class "Lop chieu $STAMP" "${STAMP}c")
printf '  lop sang %s · lop chieu %s\n' "$G_SANG" "$G_CHIEU"

section "De co lich chung rong: mo tu hom qua den tuan sau"
EXAM_BODY="{\"title\":\"De hai khung gio $STAMP\",\"topic\":\"\",\"durationMinutes\":45,\"totalQuestions\":9,\"easyPercentage\":45,\"mediumPercentage\":33,\"hardPercentage\":22,\"pointPerQuestion\":1,\"shuffleQuestions\":true,\"shuffleAnswers\":true,\"showResultAfterSubmit\":true,\"openAt\":\"$(at_hours -24)\",\"closeAt\":\"$(at_hours 168)\"}"
code=$(call POST /api/exams "$TEACHER" "$EXAM_BODY")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EID=$(json_field id); EXAM_IDS+=("$EID")
expect_status "tao de → 200" 200 "$code"

section "Giao hai lop voi hai khung gio khac nhau"
# Ca hai khung deu nam TRONG lich chung cua de. Neu he thong doc lich de thi ca hai em se cung thay
# "dang mo" — do chinh la loi bo kiem nay tim.
SANG_BODY="{\"studentIds\":[],\"groupIds\":[$G_SANG],\"notify\":false,\"openAt\":\"$(at_hours -4)\",\"closeAt\":\"$(at_hours -2)\"}"
CHIEU_BODY="{\"studentIds\":[],\"groupIds\":[$G_CHIEU],\"notify\":false,\"openAt\":\"$(at_hours -1)\",\"closeAt\":\"$(at_hours 3)\"}"
expect_status "lop sang, gio da qua → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$SANG_BODY")"
expect_status "lop chieu, dang trong gio → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$CHIEU_BODY")"

section "Cung mot de, hai em thay hai trang thai khac nhau"
TOK_SANG=$(login "p${STAMP}s" "eduquiz123")
TOK_CHIEU=$(login "p${STAMP}c" "eduquiz123")

expect_status "em lop sang doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_SANG")"
expect_status "  lifecycle = closed" closed "$(field_of_exam lifecycle "$EID")"
expect_status "  canStart = false"   false  "$(field_of_exam canStart "$EID")"
OPEN_SANG=$(field_of_exam openAt "$EID")
AID_SANG=$(field_of_exam assignmentId "$EID")

expect_status "em lop chieu doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_CHIEU")"
expect_status "  lifecycle = open" open "$(field_of_exam lifecycle "$EID")"
expect_status "  canStart = true"  true  "$(field_of_exam canStart "$EID")"
OPEN_CHIEU=$(field_of_exam openAt "$EID")
AID_CHIEU=$(field_of_exam assignmentId "$EID")

if [[ "$OPEN_SANG" != "$OPEN_CHIEU" ]]; then KHAC=true; else KHAC=false; fi
expect_status "hai em nhan hai moc mo bai KHAC nhau" true "$KHAC"

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
NUA_BODY="{\"studentIds\":[],\"groupIds\":[$G_CHIEU],\"notify\":false,\"openAt\":\"$(at_hours 1)\"}"
NGUOC_BODY="{\"studentIds\":[],\"groupIds\":[$G_CHIEU],\"notify\":false,\"openAt\":\"$(at_hours 3)\",\"closeAt\":\"$(at_hours 1)\"}"
expect_status "chi gui openAt → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$NUA_BODY")"
expect_status "gio dong truoc gio mo → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$NGUOC_BODY")"

section "Khong dat gio rieng thi theo lich chung cua de"
G_THUONG=$(build_class "Lop thuong $STAMP" "${STAMP}t")
THUONG_BODY="{\"studentIds\":[],\"groupIds\":[$G_THUONG],\"notify\":false}"
expect_status "giao khong kem gio rieng → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$THUONG_BODY")"
TOK_THUONG=$(login "p${STAMP}t" "eduquiz123")
expect_status "em lop thuong doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_THUONG")"
expect_status "  lifecycle = open theo lich de" open "$(field_of_exam lifecycle "$EID")"

summary
