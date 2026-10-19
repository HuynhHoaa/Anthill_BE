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
#   4. Bai chi giao theo lop: request mang studentIds khac rong bi tu choi 400 va khong tao dong nao; moi
#      lop trong mot request giu gio rieng cua chinh no.
#   5. Trang de cua giao vien mang khung gio THAT cua tung lop (assignmentWindows), voi trang thai mo/dong
#      tinh cung phep so voi phia hoc sinh — trong khi vong doi cua de van theo lich chung.
#   6. Doi gio thi bao cho DUNG nhung em bi doi, moi lan doi mot thong bao, em da bat dau thi khong.
#   7. Dap an chi mo khi moi khung gio cua de deu da dong, va mot khi da mo thi khong dong lai (H10): gia
#      han mot lop khong giau lai dap an, va khong them duoc em vao de dung chung mot ban de.
#   8. Thu hoi ca lop: go em chua bat dau, giu em da bat dau; lop chua duoc giao 404; moi em da bat dau 409.

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

elif mode == "window":
    # Truong $3 cua khung gio thuoc lop $4 ("le" = hoc sinh chon le) trong assignmentWindows cua de.
    data = load(sys.argv[2])
    if "items" in data:
        data = next((x for x in data["items"] if x["id"] == int(sys.argv[5])), {"assignmentWindows": []})
    key = None if sys.argv[4] == "le" else int(sys.argv[4])
    rows = [x for x in data["assignmentWindows"] if x.get("groupId") == key]
    print("none" if not rows else show(rows[0].get(sys.argv[3])))

elif mode == "notice":
    # So thong bao loai $3 trong hop thu, hoac truong $4 cua thong bao moi nhat loai do.
    items = load(sys.argv[2])["items"]
    rows = [x for x in items if x["type"] == sys.argv[3]]
    if len(sys.argv) > 4:
        print("none" if not rows else show(rows[0].get(sys.argv[4])))
    else:
        print(len(rows))

elif mode == "window_count":
    print(len(load(sys.argv[2])["assignmentWindows"]))

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
# Truong $1 cua khung gio lop $2 ("le" = chon le) tren de dang o $BODY_FILE; $3 = id de khi doc trang danh sach.
window_of()        { python "$HELPERS" window "$(to_native_path "$BODY_FILE")" "$1" "$2" "${3:-0}"; }
window_count()     { python "$HELPERS" window_count "$(to_native_path "$BODY_FILE")"; }
# So thong bao loai $1 trong hop thu dang o $BODY_FILE; them $2 de doc mot truong cua thong bao moi nhat.
notice()           { PYTHONIOENCODING=utf-8 python "$HELPERS" notice "$(to_native_path "$BODY_FILE")" "$1" ${2:+"$2"}; }
# So thong bao doi lich cua hoc sinh co token $1.
notice_count()     { call GET /api/notifications "$1" > /dev/null; notice examUpdated; }

TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

# Lop da cong bo, giao vien demo phu trach, co dung mot hoc sinh. Tra ve qua BUILT_GID/BUILT_SID.
# Goi TRUC TIEP, khong trong $( ): subshell nuot mat moi phep gan.
build_class() {
  local name="$1" suffix="$2" code
  code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"$name\",\"teacherId\":$DEMO_TEACHER_ID}")
  [[ "$code" != "200" ]] && fail_fast "tao lop $name: HTTP $code — $(body)"
  BUILT_GID=$(json_field id); GROUP_IDS+=("$BUILT_GID")
  add_student "$BUILT_GID" "$suffix"
  call POST "/api/student-groups/$BUILT_GID/publish" "$ADMIN" > /dev/null
}

# Tao mot hoc sinh trong lop $1. Tra ve qua BUILT_SID.
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
# Moi khung rieng phai con xa gio mo (tren 120 phut) thi moi sua duoc ve sau — xem schedule-lock.sh.
A_OPEN=$(at_hours 3); A_CLOSE=$(at_hours 5)
B_OPEN=$(at_hours 6); B_CLOSE=$(at_hours 8)

section "Giao nhieu lop mot lan, moi lop mot khung gio"
# Lop D kem gio rieng 10..12 gio nua: de dang mo, neu lop theo lich de thi gio cua em bi khoa.
BODY="{\"studentIds\":[],\"classes\":[$(class_entry "$G_A" "$A_OPEN" "$A_CLOSE"),$(class_entry "$G_B" "$B_OPEN" "$B_CLOSE"),$(class_entry "$G_C"),$(class_entry "$G_D" "$(at_hours 10)" "$(at_hours 12)")],\"notify\":false}"
expect_status "bon lop, ba lop co gio rieng va mot lop theo lich de → 200" 200 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$BODY")"
expect_json   "  assignedCount = 4"          assignedCount 4
expect_json   "  scheduleUpdatedCount = 0"   scheduleUpdatedCount 0

expect_status "bang theo doi → 200" 200 "$(call GET "/api/exams/$EID/assignments" "$TEACHER")"
expect_status "  lop A: gio rieng"                 true   "$(row_of_student hasScheduleOverride "$SID_A")"
expect_status "  lop A: mo dung gio cua A"         true   "$(row_near openAt "$SID_A" "$A_OPEN")"
expect_status "  lop B: mo dung gio cua B"         true   "$(row_near openAt "$SID_B" "$B_OPEN")"
expect_status "  lop C khai theo lich de"          false  "$(row_of_student hasScheduleOverride "$SID_C")"
expect_status "  lop C: mo theo lich de"           true   "$(row_near openAt "$SID_C" "$EID_OPEN")"
expect_status "  lop D: gio rieng"                 true   "$(row_of_student hasScheduleOverride "$SID_D")"
expect_status "  lop D: ghi lai lop da giao qua"   "$G_D" "$(row_of_student assignedViaGroupId "$SID_D")"
expect_status "  lop B: ghi lai lop da giao qua"   "$G_B" "$(row_of_student assignedViaGroupId "$SID_B")"

section "Giao vien thay khung gio cua tung lop tren de"
expect_status "chi tiet de → 200" 200 "$(call GET "/api/exams/$EID" "$TEACHER")"
expect_status "  bon lop, bon khung gio" 4 "$(window_count)"
expect_status "  lop A: ten lop"            "Doi gio A $STAMP" "$(window_of groupName "$G_A")"
expect_status "  lop A: chua toi gio"       scheduled "$(window_of lifecycle "$G_A")"
expect_status "  lop A: gio rieng"          true      "$(window_of hasScheduleOverride "$G_A")"
expect_status "  lop C: theo lich de, dang mo" open   "$(window_of lifecycle "$G_C")"
expect_status "  lop C: hasScheduleOverride = false" false "$(window_of hasScheduleOverride "$G_C")"
expect_status "  lop D: ten lop"            "Doi gio D $STAMP" "$(window_of groupName "$G_D")"
expect_status "  lop D: mot em"            1         "$(window_of assignedCount "$G_D")"
expect_status "  khong con khung gio cua hoc sinh chon le" none "$(window_of assignedCount le)"
expect_status "  vong doi cua de van theo lich chung" open "$(json_field lifecycle)"
expect_status "danh sach de → 200" 200 "$(call GET "/api/exams?search=De%20doi%20gio%20$STAMP" "$TEACHER")"
expect_status "  lop B tren trang danh sach: chua toi gio" scheduled "$(window_of lifecycle "$G_B" "$EID")"

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
# Bai chi giao theo lop: studentIds khac rong bi tu choi, kem hay khong kem groupIds.
STUDENT_BODY="{\"studentIds\":[$SID_E],\"groupIds\":[],\"notify\":false}"
expect_status "chon dich danh mot hoc sinh (studentIds) → 400" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$STUDENT_BODY")"
MIXED_BODY="{\"studentIds\":[$SID_E],\"groupIds\":[$G_E],\"notify\":false}"
expect_status "studentIds kem groupIds → 400, khong giao lop nao" 400 "$(call POST "/api/exams/$EID/assignments" "$TEACHER" "$MIXED_BODY")"
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
expect_status "chi tiet de → 200" 200 "$(call GET "/api/exams/$EID" "$TEACHER")"
expect_status "  lop A gio la lich de, dang mo" open  "$(window_of lifecycle "$G_A")"
expect_status "  lop A: hai em"                 2     "$(window_of assignedCount "$G_A")"

section "Em da bat dau chi bi dem khi khung that su khac"
# Tai khoan vua tao mang MustChangePassword=true (Sec.5) — doi ngay sang mat khau rieng truoc khi
# dung token cho nghiep vu, dung nhu mot hoc sinh that lam sau lan dang nhap dau tien.
TOK_C=$(login_and_clear_must_change "q${STAMP}c" "123456" "Qc$STAMP")
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

section "Em lop D thay dung khung gio rieng cua lop"
# Khong con duong doi gio rieng tung em: PATCH .../assignments/{id}/schedule khong ton tai nua.
TOK_D=$(login_and_clear_must_change "q${STAMP}d" "123456" "Qd$STAMP")
expect_status "em lop D doc danh sach → 200" 200 "$(call GET /api/my/exams "$TOK_D")"
expect_status "  em thay lifecycle = scheduled" scheduled "$(my_field lifecycle "$EID")"
expect_status "  em thay canStart = false"      false     "$(my_field canStart "$EID")"
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
AID_D=$(row_of_student id "$SID_D")
expect_status "PATCH gio rieng cua mot em → khong con duong nay (404)" 404 \
  "$(call PATCH "/api/exams/$EID/assignments/$AID_D/schedule" "$TEACHER" "{\"openAt\":\"$(at_hours 3)\",\"closeAt\":\"$(at_hours 5)\"}")"
expect_status "DELETE thu hoi rieng mot em → khong con duong nay (404)" 404 \
  "$(call DELETE "/api/exams/$EID/assignments/$AID_D" "$TEACHER")"

section "Thu hoi ca lop: em da bat dau o lai"
# Bai chi giao theo lop nen thu hoi cung theo lop. Lop C chi co mot em va em do da vao lam (xem tren).
expect_status "thu hoi lop C, em duy nhat da bat dau → 409" 409 "$(call DELETE "/api/exams/$EID/assignments/classes/$G_C" "$TEACHER")"
expect_status "  cau noi ro khong con gi de go" yes "$(body | grep -q 'đã bắt đầu' && echo yes || echo no)"
expect_status "thu hoi lop chua duoc giao (E) → 404" 404 "$(call DELETE "/api/exams/$EID/assignments/classes/$G_E" "$TEACHER")"
# Lop A co hai em (A va A2): cho A2 vao lam roi thu hoi lop — chi em A bi go.
TOK_A2=$(login_and_clear_must_change "q${STAMP}a2" "123456" "Qa2$STAMP")
call GET /api/my/exams "$TOK_A2" > /dev/null
AID_A2=$(my_field assignmentId "$EID")
expect_status "em A2 vao lam → 200" 200 "$(call POST /api/exam-attempts "$TOK_A2" "{\"assignmentId\":$AID_A2}")"
expect_status "quan tri vien thu hoi → 403" 403 "$(call DELETE "/api/exams/$EID/assignments/classes/$G_A" "$ADMIN")"
expect_status "thu hoi lop A → 200" 200 "$(call DELETE "/api/exams/$EID/assignments/classes/$G_A" "$TEACHER")"
expect_json   "  go 1 em (em A chua bat dau)"  revokedCount 1
expect_json   "  giu 1 em (em A2 da bat dau)"  keptStartedCount 1
call GET "/api/exams/$EID/assignments" "$TEACHER" > /dev/null
expect_status "  em A khong con bai"           none "$(row_of_student status "$SID_A")"
expect_status "  em A2 van con bai"            true "$([[ "$(row_of_student status "$SID_A2")" != "none" ]] && echo true || echo false)"
expect_status "thu hoi lai lop A, chi con em da bat dau → 409" 409 "$(call DELETE "/api/exams/$EID/assignments/classes/$G_A" "$TEACHER")"

section "Doi gio thi bao cho dung nhung em bi doi"
# notify=false o moi lan giao, nen moi thong bao dem duoc o day deu sinh ra tu mot lan DOI GIO.
build_class "Bao gio F $STAMP" "${STAMP}f"; G_F=$BUILT_GID; SID_F=$BUILT_SID
build_class "Bao gio G $STAMP" "${STAMP}g"; G_G=$BUILT_GID; SID_G=$BUILT_SID
build_class "Bao gio H $STAMP" "${STAMP}h"; G_H=$BUILT_GID; SID_H=$BUILT_SID

# De chua toi gio mo (6 gio nua): lich cua de va cua lop F deu con xa, nen sua duoc; sau khi mo thi bi khoa.
new_exam "De bao gio $STAMP" 6 30
EID_N=$NEW_EID
F_OPEN=$(at_hours 4); F_CLOSE=$(at_hours 6)
NOTIFY_BODY="{\"studentIds\":[],\"groupIds\":[$G_G,$G_H],\"classes\":[$(class_entry "$G_F" "$F_OPEN" "$F_CLOSE")],\"notify\":false}"
expect_status "giao lop F (gio rieng), lop G va lop H (lich de) → 200" 200 \
  "$(call POST "/api/exams/$EID_N/assignments" "$TEACHER" "$NOTIFY_BODY")"
expect_json   "  assignedCount = 3" assignedCount 3

# Tai khoan vua tao mang MustChangePassword=true (Sec.5) — doi ngay sang mat khau rieng truoc khi
# dung token cho nghiep vu.
TOK_F=$(login_and_clear_must_change "q${STAMP}f" "123456" "Qf$STAMP")
TOK_G=$(login_and_clear_must_change "q${STAMP}g" "123456" "Qg$STAMP")
TOK_H=$(login_and_clear_must_change "q${STAMP}h" "123456" "Qh$STAMP")
expect_status "khong ai co thong bao nao (notify=false)" "0 0 0" \
  "$(notice_count "$TOK_F") $(notice_count "$TOK_G") $(notice_count "$TOK_H")"

call GET "/api/exams/$EID_N/assignments" "$TEACHER" > /dev/null
AID_G=$(row_of_student id "$SID_G")

N_OPEN=$(at_hours 8); N_CLOSE=$(at_hours 40)
expect_status "doi lich chung cua de → 200" 200 \
  "$(call PATCH "/api/exams/$EID_N/schedule" "$TEACHER" "{\"openAt\":\"$N_OPEN\",\"closeAt\":\"$N_CLOSE\"}")"
expect_status "  em lop G (theo lich de) duoc bao" 1 "$(notice_count "$TOK_G")"
expect_status "  em lop H (cung theo lich de) duoc bao" 1 "$(notice_count "$TOK_H")"
expect_status "  em lop F co gio rieng: khong bao" 0 "$(notice_count "$TOK_F")"
call GET /api/notifications "$TOK_G" > /dev/null
expect_status "  thong bao dan toi bai cua chinh em" "/hoc-sinh/bai-kiem-tra/$AID_G" "$(notice examUpdated link)"
expect_status "  tieu de noi ro chuyen gi" "Lịch làm bài đã thay đổi" "$(notice examUpdated title)"
expect_status "  con chua doc" false "$(notice examUpdated isRead)"
# Server chay theo UTC va khong biet truong o mui gio nao, nen cau chu khong duoc ghi gio cu the.
expect_status "  cau chu khong ghi gio cu the" yes \
  "$(notice examUpdated message | grep -qE '[0-9]{1,2}:[0-9]{2}' && echo no || echo yes)"

F_OPEN2=$(at_hours 7); F_CLOSE2=$(at_hours 9)
expect_status "gui lai lop F voi khung khac → 200" 200 \
  "$(call POST "/api/exams/$EID_N/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[$(class_entry "$G_F" "$F_OPEN2" "$F_CLOSE2")],\"notify\":false}")"
expect_status "  em lop F duoc bao"           1 "$(notice_count "$TOK_F")"
expect_status "  em lop G khong nhan them gi" 1 "$(notice_count "$TOK_G")"
expect_status "gui lai lop F voi dung khung do → 200" 200 \
  "$(call POST "/api/exams/$EID_N/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[$(class_entry "$G_F" "$F_OPEN2" "$F_CLOSE2")],\"notify\":false}")"
expect_status "  khong co thong bao thu hai cho mot lan doi" 1 "$(notice_count "$TOK_F")"

expect_status "doi thoi luong lam bai → 200" 200 \
  "$(call PUT "/api/exams/$EID_N" "$TEACHER" "$(exam_body "De bao gio $STAMP" "$N_OPEN" "$N_CLOSE" 60)")"
expect_status "  em lop F: doi thoi luong thi ai cung lam theo con so moi" 2 "$(notice_count "$TOK_F")"
expect_status "  em lop G" 2 "$(notice_count "$TOK_G")"
expect_status "  em lop H" 2 "$(notice_count "$TOK_H")"
call GET /api/notifications "$TOK_F" > /dev/null
expect_status "  cau chu noi so phut moi" yes "$(notice examUpdated message | grep -q "60 phút" && echo yes || echo no)"

# Cho de mo ngay bay gio (con xa nen van doi duoc), em lop G (theo lich de) duoc bao them mot lan.
expect_status "doi lich chung sang dang mo → 200" 200 \
  "$(call PATCH "/api/exams/$EID_N/schedule" "$TEACHER" "{\"openAt\":\"$(at_hours -0.4)\",\"closeAt\":\"$(at_hours 41)\"}")"
expect_status "  em lop G duoc bao them" 3 "$(notice_count "$TOK_G")"

call GET /api/my/exams "$TOK_G" > /dev/null
AID_G_MY=$(my_field assignmentId "$EID_N")
expect_status "em lop G vao lam bai → 200" 200 "$(call POST /api/exam-attempts "$TOK_G" "{\"assignmentId\":$AID_G_MY}")"
# De da mo va da giao nen khong doi lich chung duoc nua: cung vi vay khong the co chuyen em dang lam bai
# bi doi gio giua chung, va em da bat dau khong bao gio nhan thong bao doi gio.
expect_status "doi lich chung khi de da mo → 409" 409 \
  "$(call PATCH "/api/exams/$EID_N/schedule" "$TEACHER" "{\"openAt\":\"$(at_hours -0.3)\",\"closeAt\":\"$(at_hours 42)\"}")"
expect_status "  em da bat dau khong bi bao: han nop da chot luc vao thi" 3 "$(notice_count "$TOK_G")"

section "Dap an chi mo khi moi khung gio deu da dong"
# per-class-schedule.sh chung minh dap an cho toi khi lop dong muon nhat het gio. Cho nay canh chieu
# nguoc lai (H10, "chi tien, khong lui"): moi lop da dong va dap an da mo, roi mot lop duoc gia han —
# dap an KHONG dong lai, vi no da roi khoi server; va khong them duoc em moi vao de dung chung mot ban de.
build_class "Dap an K $STAMP" "${STAMP}k"; G_K=$BUILT_GID; SID_K=$BUILT_SID
build_class "Dap an L $STAMP" "${STAMP}l"; G_L=$BUILT_GID; SID_L=$BUILT_SID
new_exam "De mo dap an $STAMP" -24 168 1; EID_DA=$NEW_EID
K_CLOSE=$(at_hours 0.0125)
expect_status "lop K: dong sau 45 giay → 200" 200 \
  "$(call POST "/api/exams/$EID_DA/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[$(class_entry "$G_K" "$(at_hours -1)" "$K_CLOSE")],\"notify\":false}")"
expect_status "lop L: khung da qua → 200" 200 \
  "$(call POST "/api/exams/$EID_DA/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[$(class_entry "$G_L" "$(at_hours -3)" "$(at_hours -2)")],\"notify\":false}")"

TOK_K=$(login_and_clear_must_change "q${STAMP}k" "123456" "Qk$STAMP")
call GET /api/my/exams "$TOK_K" > /dev/null
AID_K=$(my_field assignmentId "$EID_DA")
expect_status "em lop K vao lam → 200" 200 "$(call POST /api/exam-attempts "$TOK_K" "{\"assignmentId\":$AID_K}")"
ATT_K=$(json_field attemptId)
expect_status "em lop K nop bai → 200" 200 "$(call POST "/api/exam-attempts/$ATT_K/submit" "$TOK_K" '{"answers":[]}')"
expect_status "em xem ket qua → 200" 200 "$(call GET "/api/results/$ATT_K" "$TOK_K")"
expect_json   "  co diem ngay, chua co dap an" canSeeAnswers false
expect_status "  hen mo dap an dung luc lop K dong" true "$(field_near answersVisibleAt "$K_CLOSE")"

printf '  doi lop K dong...\n'
wait_past "$K_CLOSE"
expect_status "moi lop da dong → 200" 200 "$(call GET "/api/results/$ATT_K" "$TOK_K")"
expect_json   "  dap an da mo" canSeeAnswers true

L_CLOSE=$(at_hours 26)
expect_status "gia han lop L sang ngay mai → 200" 200 \
  "$(call POST "/api/exams/$EID_DA/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[$(class_entry "$G_L" "$(at_hours 24)" "$L_CLOSE")],\"notify\":false}")"
expect_json   "  mot em cua lop L doi gio" scheduleUpdatedCount 1
expect_status "em lop K xem lai → 200" 200 "$(call GET "/api/results/$ATT_K" "$TOK_K")"
expect_json   "  dap an van mo: da roi khoi server thi khong giau lai" canSeeAnswers true
expect_status "  khong con hen gio mo dap an" absent "$(field_near answersVisibleAt "$L_CLOSE")"

expect_status "keo lop L ve khung da qua → 200" 200 \
  "$(call POST "/api/exams/$EID_DA/assignments" "$TEACHER" \
    "{\"studentIds\":[],\"classes\":[$(class_entry "$G_L" "$(at_hours -3)" "$(at_hours -2)")],\"notify\":false}")"
expect_json   "  mot em cua lop L doi gio" scheduleUpdatedCount 1
expect_status "em lop K xem lai → 200" 200 "$(call GET "/api/results/$ATT_K" "$TOK_K")"
expect_json   "  dap an van mo" canSeeAnswers true

add_student "$G_L" "${STAMP}m"; SID_M=$BUILT_SID
expect_status "em moi vao lop L, gui lai lop → 409" 409 \
  "$(call POST "/api/exams/$EID_DA/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$G_L],\"notify\":false}")"
expect_status "  cau noi ro de dung chung mot ban de" yes "$(body | grep -q 'dùng chung một bản đề' && echo yes || echo no)"

summary
