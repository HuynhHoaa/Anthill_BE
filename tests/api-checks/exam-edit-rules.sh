#!/usr/bin/env bash
# Sua de da giao (§4.6, luat sua de).
#
# Chay:  bash tests/api-checks/exam-edit-rules.sh   (API phai dang chay o $HOST; can psql — xem duoi)
#
# Moc phan chia la LUOT LAM BAI DAU TIEN, khong phai luc giao bai hay luc xuat ban:
#   1. Chua em nao bat dau: sua duoc moi truong, va ban de da sinh cho cac luot giao duoc sinh lai
#      theo cau hinh moi — chung minh bang chinh bai hoc sinh nhan duoc khi vao thi.
#   2. Ngan hang khong du cau cho cau hinh moi thi tu choi va khong doi gi.
#   3. Da co em bat dau: so cau, ty le, chu de, diem, thoi luong, hai tuy chon tron bi khoa (409); ten,
#      mo ta, lich va co hien diem van luu duoc, va KHONG sinh lai ban de.
#   4. Diem da cham giu nguyen sau moi lan sua duoc phep.
#   5. Giao, thu hoi het, doi cau hinh, giao lai: ban de chung cu khong duoc dung lai.
#   6. Nhan ban: ban nhap, cung cau hinh, khong luot giao; giao vien khac nhan 404; giao vien chua gan
#      mon nhan 403 nhu khi tao de.
#
# Giao vien thu hai va thu ba: he thong chi seed mot giao vien va khong co endpoint tao giao vien, nen
# script chep dong cua giao vien demo thang trong database bang psql — mot dong nghiep cung mon (phep
# thu kho nhat cho ranh gioi "de cua minh") va mot giao vien chua duoc gan mon. Thong tin ket noi lay
# tu PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD neu co, khong thi tu user-secrets cua Anthill.Api.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
GROUP_IDS=(); EXAM_IDS=(); STUDENT_IDS=(); MEMBERS=(); QUESTION_IDS=()
FOREIGN_ID=""

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

# ── psql ────────────────────────────────────────────────────────────────────────────────────────
PSQL=$(command -v psql 2>/dev/null || ls /c/Program\ Files/PostgreSQL/*/bin/psql.exe 2>/dev/null | tail -n 1)
CONN=""
if [[ -z "${PGPASSWORD:-}" ]]; then
  CONN=$(dotnet user-secrets list --project "$(dirname "$0")/../../src/Anthill.Api" 2>/dev/null \
    | grep -m1 'DefaultConnection' | sed 's/^[^=]*= //')
fi

HELPERS=$(mktemp -t anthill-helpers.XXXXXX.py)
SESSION_A=$(mktemp -t anthill-session-a.XXXXXX)
cat > "$HELPERS" <<'PYHELPER'
"""Vai phep tinh nho cho bo kiem. Tach ra file rieng de khoi long trich dan trong shell."""
import datetime
import json
import sys
import unicodedata

mode = sys.argv[1]

# Cau chu tieng Viet viet bang ma \u de khong phu thuoc bang ma cua terminal Windows.
PHRASES = {
    "so_cau": "số câu hỏi",
    "diem": "điểm mỗi câu",
    "thoi_luong": "thời gian làm bài",
    "chu_de": "chủ đề",
    "tron_cau": "trộn thứ tự câu hỏi",
    "tron_dap_an": "trộn thứ tự đáp án",
    "ty_le_de": "tỷ lệ câu Dễ",
    "nhan_ban": "nhân bản",
    "nhap": "bản nháp",
    "ban_sao": " (bản sao)",
    "chua_gan_mon": "chưa được gán môn",
}


def nfc(text):
    return unicodedata.normalize("NFC", text)


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def dig(data, path):
    for key in path.split("."):
        data = data[int(key)] if key.isdigit() else data[key]
    return data


if mode == "iso":
    moment = datetime.datetime.now(datetime.UTC) + datetime.timedelta(hours=float(sys.argv[2]))
    print(moment.isoformat().replace("+00:00", "Z"))

elif mode == "num":
    # So thap phan in gon: 20.00 va 20.0 deu thanh "20".
    try:
        print("%g" % float(dig(load(sys.argv[2]), sys.argv[3])))
    except Exception as exc:
        print("<khong doc duoc: %s>" % exc)

elif mode == "says":
    # Than loi co chua cau chu $3 khong.
    with open(sys.argv[2], encoding="utf-8") as handle:
        text = nfc(handle.read())
    print("yes" if nfc(PHRASES[sys.argv[3]]) in text else "no")

elif mode == "title_ends":
    print("yes" if nfc(load(sys.argv[2])["title"]).endswith(nfc(PHRASES[sys.argv[3]])) else "no")

elif mode == "count":
    print(len(load(sys.argv[2])))

elif mode == "all_correct":
    # Than request nop bai chon dap an "Dung" cho moi cau cua phien $2.
    session = load(sys.argv[2])
    answers = [
        {"questionId": q["id"], "answerId": next(a["id"] for a in q["answers"] if a["content"] == "Dung")}
        for q in session["questions"]
    ]
    print(json.dumps({"answers": answers}))

elif mode == "qids":
    # Tap id cau hoi cua mot phien, da sap — hai phien cung mot ban de thi hai chuoi nay bang nhau.
    print(",".join(str(q["id"]) for q in sorted(load(sys.argv[2])["questions"], key=lambda q: q["id"])))

elif mode == "ids":
    # Id cua moi dong trong mot danh sach, cach nhau bang dau cach.
    print(" ".join(str(row["id"]) for row in load(sys.argv[2])))

elif mode == "assignment":
    # assignmentId cua de $3 trong danh sach bai cua hoc sinh.
    rows = load(sys.argv[2])
    row = next((x for x in rows if x["examId"] == int(sys.argv[3])), None)
    print("" if row is None else row["assignmentId"])

elif mode == "conn":
    # Tach chuoi ket noi kieu Npgsql thanh host, port, db, user, password — moi thu mot dong.
    parts = dict(
        (key.strip().lower(), value.strip())
        for key, _, value in (item.partition("=") for item in sys.argv[2].split(";") if "=" in item)
    )
    for key in ("host", "port", "database", "username", "password"):
        print(parts.get(key, ""))
PYHELPER
HELPERS=$(to_native_path "$HELPERS")

at_hours()    { python "$HELPERS" iso "$1"; }
num_field()   { python "$HELPERS" num "$(to_native_path "$BODY_FILE")" "$1"; }
body_says()   { python "$HELPERS" says "$(to_native_path "$BODY_FILE")" "$1"; }
title_ends()  { python "$HELPERS" title_ends "$(to_native_path "$BODY_FILE")" "$1"; }
body_count()  { python "$HELPERS" count "$(to_native_path "$BODY_FILE")"; }
qids_of()     { python "$HELPERS" qids "$(to_native_path "$1")"; }
same()        { if [[ "$1" == "$2" ]]; then echo true; else echo false; fi; }

if [[ -n "$CONN" ]]; then
  mapfile -t PG < <(python "$HELPERS" conn "$CONN" | tr -d '\r')
  export PGHOST="${PG[0]}" PGPORT="${PG[1]:-5432}" PGDATABASE="${PG[2]}" PGUSER="${PG[3]}" PGPASSWORD="${PG[4]}"
fi
sql() { "$PSQL" -q -At -v ON_ERROR_STOP=1 -c "$1"; }

# Chep dong cua giao vien demo thanh mot giao vien moi, in id. $1 ho ten, $2 email (cung la ten dang
# nhap), $3 bieu thuc SQL cho cot SubjectId — '"SubjectId"' de giu mon, NULL de bo mon.
clone_teacher() {
  sql "INSERT INTO \"Users\" (\"FullName\",\"Email\",\"Username\",\"PasswordHash\",\"SecurityStamp\",\"Role\",\"Status\",\"SubjectId\",\"School\",\"AvatarColor\",\"MustChangePassword\",\"CreatedAt\")
    SELECT '$1', '$2', '$2', \"PasswordHash\", md5(random()::text), \"Role\", \"Status\", $3, \"School\", \"AvatarColor\", false, now()
    FROM \"Users\" WHERE \"Email\" = 'an.nv@anthill.edu.vn' RETURNING \"Id\";" | tr -d '\r'
}

NOSUBJECT_ID=""

cleanup() {
  section "Don du lieu"
  local member returned
  if [[ -n "$NOSUBJECT_ID" ]]; then
    # Tra moi de dang dung ten giao vien chua gan mon ve giao vien demo: de con tro toi dong cua ho thi
    # khoa ngoai giu dong do lai, va giao vien demo moi xoa duoc de. Ke ca ban sao lot ra neu mot lan
    # chay cu con cho nhan ban.
    returned=$(sql "UPDATE \"Exams\" SET \"CreatedByUserId\" = $TEACHER_ID WHERE \"CreatedByUserId\" = $NOSUBJECT_ID RETURNING \"Id\";" 2>/dev/null | tr -d '\r')
    for id in $returned; do [[ " ${EXAM_IDS[*]} " == *" $id "* ]] || EXAM_IDS+=("$id"); done
  fi
  for id in "${EXAM_IDS[@]}"; do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done
  for id in "${QUESTION_IDS[@]}"; do call DELETE "/api/questions/$id" "$TEACHER" > /dev/null; done
  # Go hoc sinh khoi lop truoc: lop con thanh vien thi khong xoa duoc (409).
  for member in "${MEMBERS[@]}"; do
    call DELETE "/api/student-groups/${member%/*}/students/${member#*/}" "$ADMIN" > /dev/null
  done
  for id in "${STUDENT_IDS[@]}"; do call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null; done
  for id in "${GROUP_IDS[@]}";   do call DELETE "/api/student-groups/$id" "$ADMIN" > /dev/null; done
  # Hai giao vien nay khong con de, cau hoi hay lop nao nen khong khoa ngoai RESTRICT nao giu dong cua
  # ho; thong bao (neu co) di theo nho CASCADE.
  for id in $FOREIGN_ID $NOSUBJECT_ID; do
    sql "DELETE FROM \"Users\" WHERE \"Id\" = $id;" \
      > /dev/null 2>&1 || printf '  khong xoa duoc giao vien kiem thu %s\n' "$id"
  done
  printf '  da don (de da co bai nop thi khong xoa duoc — dung thiet ke, khong phai loi don dep)\n'
}
trap 'rm -f "$HELPERS" "$SESSION_A"; cleanup' EXIT

# ── Than de ─────────────────────────────────────────────────────────────────────────────────────
# Cau hinh CHUAN cua de dang kiem (bien B_*). exam_json in than request theo cau hinh do, ghi de bang
# cac cap khoa=gia tri truyen vao — moi phep kiem chi noi ro dung mot dieu no doi.
TOPIC="SuaDe$STAMP"
B_TITLE="De sua $STAMP"; B_DESC="Mo ta ban dau"; B_TOPIC="$TOPIC"
B_TOTAL=4; B_EASY=50; B_MEDIUM=50; B_HARD=0; B_POINT=1; B_DURATION=30
B_SQ=false; B_SA=false; B_SHOW=true
B_OPEN=$(at_hours -1); B_CLOSE=$(at_hours 48)

exam_json() {
  local title="$B_TITLE" desc="$B_DESC" topic="$B_TOPIC" total="$B_TOTAL" easy="$B_EASY" medium="$B_MEDIUM"
  local hard="$B_HARD" point="$B_POINT" duration="$B_DURATION" sq="$B_SQ" sa="$B_SA" show="$B_SHOW"
  local open="$B_OPEN" close="$B_CLOSE" lifecycle="" pair key value
  for pair in "$@"; do
    key="${pair%%=*}"; value="${pair#*=}"
    case "$key" in
      title) title="$value" ;;       desc) desc="$value" ;;         topic) topic="$value" ;;
      total) total="$value" ;;       easy) easy="$value" ;;         medium) medium="$value" ;;
      hard) hard="$value" ;;         point) point="$value" ;;       duration) duration="$value" ;;
      sq) sq="$value" ;;             sa) sa="$value" ;;             show) show="$value" ;;
      open) open="$value" ;;         close) close="$value" ;;       lifecycle) lifecycle="$value" ;;
      *) echo "exam_json: khoa la '$key'" >&2 ;;
    esac
  done
  local life=""
  [[ -n "$lifecycle" ]] && life=",\"lifecycle\":\"$lifecycle\""
  printf '{"title":"%s","description":"%s","topic":"%s","durationMinutes":%s,"totalQuestions":%s,"easyPercentage":%s,"mediumPercentage":%s,"hardPercentage":%s,"pointPerQuestion":%s,"shuffleQuestions":%s,"shuffleAnswers":%s,"showResultAfterSubmit":%s,"openAt":"%s","closeAt":"%s"%s}' \
    "$title" "$desc" "$topic" "$duration" "$total" "$easy" "$medium" "$hard" "$point" "$sq" "$sa" "$show" "$open" "$close" "$life"
}

put_exam() { local exam="$1"; shift; call PUT "/api/exams/$exam" "$TEACHER" "$(exam_json "$@")"; }

new_exam() {
  local code
  code=$(call POST /api/exams "$TEACHER" "$(exam_json "$@")")
  [[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
  NEW_EID=$(json_field id); EXAM_IDS+=("$NEW_EID")
}

# Giao de $1 cho lop cua bo kiem. $2 (tuy chon) la phan JSON them vao than request.
assign_class() {
  call POST "/api/exams/$1/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GID],\"notify\":false${2:-}}"
}

# Hoc sinh $1 (token) vao lam de $2. Phien nam o $BODY_FILE; in ma HTTP.
start_exam() {
  local token="$1" exam="$2" aid
  call GET /api/my/exams "$token" > /dev/null
  aid=$(python "$HELPERS" assignment "$(to_native_path "$BODY_FILE")" "$exam")
  [[ -z "$aid" ]] && { echo "000"; return; }
  call POST /api/exam-attempts "$token" "{\"assignmentId\":$aid}"
}

# ── Du lieu nen ─────────────────────────────────────────────────────────────────────────────────
section "Dung du lieu nen"
TEACHER_ID=$(call GET /api/auth/me "$TEACHER" > /dev/null; json_field id)

code=$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop sua de $STAMP\"}")
[[ "$code" != "200" ]] && fail_fast "tao lop: HTTP $code — $(body)"
GID=$(json_field id); GROUP_IDS+=("$GID")
for suffix in a b; do
  code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS sua de $suffix\",\"email\":\"sd$STAMP$suffix@kiemthu.vn\",\"username\":\"sd$STAMP$suffix\",\"code\":\"SD$STAMP$suffix\",\"password\":\"123456\",\"groupId\":$GID}")
  [[ "$code" != "200" ]] && fail_fast "tao hoc sinh $suffix: HTTP $code — $(body)"
  SID=$(json_field id); STUDENT_IDS+=("$SID"); MEMBERS+=("$GID/$SID")
done
call POST "/api/student-groups/$GID/teachers" "$ADMIN" "{\"teacherIds\":[$TEACHER_ID]}" > /dev/null
call POST "/api/student-groups/$GID/publish" "$ADMIN" > /dev/null
# Tai khoan vua tao mang MustChangePassword=true (Sec.5) — doi ngay sang mat khau rieng truoc khi
# dung token cho nghiep vu.
TOK_A=$(login_and_clear_must_change "sd${STAMP}a" "123456" "SdA$STAMP")
TOK_B=$(login_and_clear_must_change "sd${STAMP}b" "123456" "SdB$STAMP")

# 6 De, 4 Trung binh, 2 Kho trong mot chu de rieng: du cho 4 cau 50/50/0 va 10 cau 60/30/10, khong du
# cho 12 cau 60/30/10 (can 7 cau De).
for pair in easy:6 medium:4 hard:2; do
  for i in $(seq 1 "${pair##*:}"); do
    code=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau ${pair%%:*} $i - $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"${pair%%:*}\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
    [[ "$code" != "200" ]] && fail_fast "tao cau hoi: HTTP $code — $(body)"
    QUESTION_IDS+=("$(json_field id)")
  done
done
printf '  lop %s · 2 hoc sinh · %d cau hoi chu de %s\n' "$GID" "${#QUESTION_IDS[@]}" "$TOPIC"

# ── 1. Chua ai lam ──────────────────────────────────────────────────────────────────────────────
section "De moi: chua ai lam thi chua khoa"
new_exam; E1=$NEW_EID
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  startedCount = 0"       startedCount 0
expect_json   "  blueprintFrozen = false" blueprintFrozen false
expect_status "giao cho lop (de chung) → 200" 200 "$(assign_class "$E1")"
expect_json   "  assignedCount = 2" assignedCount 2

section "Da giao, chua ai lam: sua moi truong, ban de sinh lai"
# Mot lan PUT doi het: so cau, ba ty le, diem, thoi luong, hai tuy chon tron, co hien diem, ten, mo ta,
# lich. Ban de 4 cau da sinh luc giao phai bi thay bang ban 10 cau.
B_TITLE="De sua da doi $STAMP"; B_DESC="Mo ta da doi"
B_TOTAL=10; B_EASY=60; B_MEDIUM=30; B_HARD=10; B_POINT=2; B_DURATION=40
B_SQ=true; B_SA=true; B_SHOW=false; B_CLOSE=$(at_hours 72)
expect_status "sua het moi truong → 200" 200 "$(put_exam "$E1")"
expect_json   "  totalQuestions = 10"    totalQuestions 10
expect_status "  pointPerQuestion = 2"   2 "$(num_field pointPerQuestion)"
expect_json   "  durationMinutes = 40"   durationMinutes 40
expect_json   "  shuffleQuestions = true" shuffleQuestions true
expect_json   "  shuffleAnswers = true"  shuffleAnswers true
expect_json   "  showResultAfterSubmit = false" showResultAfterSubmit false
expect_json   "  title da doi"           title "$B_TITLE"
expect_json   "  van chua khoa"          blueprintFrozen false

section "Ngan hang khong du cau cho cau hinh moi: tu choi, khong doi gi"
code=$(put_exam "$E1" total=12)
case "$code" in 400|409) TU_CHOI=true ;; *) TU_CHOI=false ;; esac
expect_status "12 cau 60/30/10 (can 7 cau De, co 6) → bi tu choi" true "$TU_CHOI"
expect_status "doc lai de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  van 10 cau" totalQuestions 10

section "Khung gio rieng ngan hon thoi luong moi: tu choi (de chua ai lam)"
new_exam title="De khung gio $STAMP" total=4 easy=50 medium=50 hard=0 point=1 duration=30; E2=$NEW_EID
expect_status "giao kem khung gio rieng 60 phut → 200" 200 \
  "$(assign_class "$E2" ",\"openAt\":\"$(at_hours 1)\",\"closeAt\":\"$(at_hours 2)\"")"
expect_status "tang thoi luong len 90 phut → 400" 400 \
  "$(put_exam "$E2" title="De khung gio $STAMP" total=4 easy=50 medium=50 hard=0 point=1 duration=90)"
expect_status "thoi luong 45 phut → 200" 200 \
  "$(put_exam "$E2" title="De khung gio $STAMP" total=4 easy=50 medium=50 hard=0 point=1 duration=45)"

# ── 2. Hoc sinh vao thi: bang chung ban de da duoc sinh lai ─────────────────────────────────────
section "Em A vao thi: bai nhan duoc khop cau hinh MOI"
expect_status "em A bat dau → 200" 200 "$(start_exam "$TOK_A" "$E1")"
expect_json   "  10 cau (khong phai 4 cua ban de cu)" totalQuestions 10
expect_status "  thang diem 20 = 10 cau x 2 diem" 20 "$(num_field maxScore)"
expect_json   "  thoi luong 40 phut" durationMinutes 40
cp "$BODY_FILE" "$SESSION_A"
ATT_A=$(json_field attemptId)
QIDS_A=$(qids_of "$SESSION_A")
expect_status "em A nop bai, dung het → 200" 200 \
  "$(call POST "/api/exam-attempts/$ATT_A/submit" "$TOK_A" "$(python "$HELPERS" all_correct "$(to_native_path "$SESSION_A")")")"
expect_status "giao vien xem bai em A → 200" 200 "$(call GET "/api/results/$ATT_A" "$TEACHER")"
expect_status "  diem 20" 20 "$(num_field score)"

# ── 3. Da co em lam: khoa cau hinh ──────────────────────────────────────────────────────────────
section "Da co em lam: cau hinh de bi khoa"
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  startedCount = 1"        startedCount 1
expect_json   "  blueprintFrozen = true"  blueprintFrozen true

expect_status "doi so cau → 409" 409 "$(put_exam "$E1" total=8)"
expect_status "  loi ke ten 'so cau hoi'" yes "$(body_says so_cau)"
expect_status "  loi chi loi ra: nhan ban de" yes "$(body_says nhan_ban)"
expect_status "doi diem moi cau → 409" 409 "$(put_exam "$E1" point=3)"
expect_status "  loi ke ten 'diem moi cau'" yes "$(body_says diem)"
expect_status "doi thoi luong → 409" 409 "$(put_exam "$E1" duration=50)"
expect_status "  loi ke ten 'thoi gian lam bai'" yes "$(body_says thoi_luong)"
expect_status "doi ty le 50/40/10 → 409" 409 "$(put_exam "$E1" easy=50 medium=40)"
expect_status "  loi ke ten 'ty le cau De'" yes "$(body_says ty_le_de)"
expect_status "doi chu de sang tong hop → 409" 409 "$(put_exam "$E1" topic=)"
expect_status "  loi ke ten 'chu de'" yes "$(body_says chu_de)"
expect_status "tat tron cau hoi → 409" 409 "$(put_exam "$E1" sq=false)"
expect_status "  loi ke ten 'tron thu tu cau hoi'" yes "$(body_says tron_cau)"
expect_status "tat tron dap an → 409" 409 "$(put_exam "$E1" sa=false)"
expect_status "  loi ke ten 'tron thu tu dap an'" yes "$(body_says tron_dap_an)"
expect_status "rut ve ban nhap → 409" 409 "$(put_exam "$E1" lifecycle=draft)"
expect_status "  loi noi ro 'ban nhap'" yes "$(body_says nhap)"
expect_status "doc lai de → 200" 200 "$(call GET "/api/exams/$E1" "$TEACHER")"
expect_json   "  van 10 cau sau bay lan bi tu choi" totalQuestions 10
expect_json   "  van khong phai nhap" lifecycle open

section "Da co em lam: ten, mo ta, lich, co hien diem van luu duoc"
B_TITLE="De sua lan ba $STAMP"; B_DESC="Mo ta lan ba"; B_SHOW=true
B_OPEN=$(at_hours -2); B_CLOSE=$(at_hours 96)
expect_status "sua ten, mo ta, lich, bat hien diem → 200" 200 "$(put_exam "$E1")"
expect_json   "  title moi"                     title "$B_TITLE"
expect_json   "  description moi"               description "$B_DESC"
expect_json   "  showResultAfterSubmit = true"  showResultAfterSubmit true
expect_json   "  cau hinh giu nguyen: 10 cau"   totalQuestions 10
expect_status "doi lich qua PATCH → 200" 200 \
  "$(call PATCH "/api/exams/$E1/schedule" "$TEACHER" "{\"openAt\":\"$B_OPEN\",\"closeAt\":\"$(at_hours 120)\"}")"
# Dong nghiep an bot mot cau De: ngan hang con 5 cau De, cau hinh da khoa can 6. Cau hinh khong doi thi
# khong duoc kiem lai ngan hang — neu khong, giao vien het ca sua ten.
expect_status "an mot cau De → 200" 200 \
  "$(call PATCH "/api/questions/${QUESTION_IDS[0]}/status" "$TEACHER" '{"status":"hidden"}')"
expect_status "ngan hang thieu nhung chi sua ten → 200" 200 "$(put_exam "$E1" title="De sua lan bon $STAMP")"
B_TITLE="De sua lan bon $STAMP"
call PATCH "/api/questions/${QUESTION_IDS[0]}/status" "$TEACHER" '{"status":"active"}' > /dev/null

section "Diem da cham giu nguyen sau cac lan sua duoc phep"
expect_status "giao vien xem lai bai em A → 200" 200 "$(call GET "/api/results/$ATT_A" "$TEACHER")"
expect_status "  van 20 diem"       20 "$(num_field score)"
expect_status "  thang diem van 20" 20 "$(num_field maxScore)"
expect_status "em A xem ket qua (vua bat hien diem) → 200" 200 "$(call GET "/api/results/$ATT_A" "$TOK_A")"
expect_status "  em A thay 20 diem" 20 "$(num_field score)"

section "Sua duoc phep KHONG sinh lai ban de: em B lam dung bo cau em A da lam"
expect_status "em B bat dau → 200" 200 "$(start_exam "$TOK_B" "$E1")"
expect_json   "  10 cau" totalQuestions 10
expect_status "  cung bo cau voi em A (de chung)" true "$(same "$(qids_of "$BODY_FILE")" "$QIDS_A")"

# ── 4. Moi em mot de rieng ──────────────────────────────────────────────────────────────────────
section "Moi em mot de rieng: sinh lai ca hai ban"
new_exam title="De rieng $STAMP" total=4 easy=50 medium=50 hard=0 point=1; E3=$NEW_EID
expect_status "giao, randomizePerStudent = true → 200" 200 "$(assign_class "$E3" ',"randomizePerStudent":true')"
expect_status "sua sang 10 cau → 200" 200 "$(put_exam "$E3" title="De rieng $STAMP")"
expect_status "em A bat dau → 200" 200 "$(start_exam "$TOK_A" "$E3")"
expect_json   "  10 cau" totalQuestions 10
QIDS_RIENG_A=$(qids_of "$BODY_FILE")
expect_status "em B bat dau → 200" 200 "$(start_exam "$TOK_B" "$E3")"
expect_json   "  10 cau" totalQuestions 10
expect_status "  hai em hai ban de khac nhau" false "$(same "$(qids_of "$BODY_FILE")" "$QIDS_RIENG_A")"

# ── 5. Thu hoi het roi sua ──────────────────────────────────────────────────────────────────────
section "Thu hoi het, doi cau hinh, giao lai: ban de theo cau hinh MOI"
# Ban de chung khong gan voi luot giao nao, nen thu hoi het cac em khong xoa no. Truoc day lan sua luc
# khong con luot giao nao cung khong dong toi no, va lan giao sau phat lai dung bo 4 cau cu.
new_exam title="De thu hoi $STAMP" total=4 easy=50 medium=50 hard=0 point=1; E4=$NEW_EID
expect_status "giao cho lop (de chung) → 200" 200 "$(assign_class "$E4")"
expect_json   "  assignedCount = 2" assignedCount 2
expect_status "danh sach giao bai → 200" 200 "$(call GET "/api/exams/$E4/assignments" "$TEACHER")"
REVOKED=0
for aid in $(python "$HELPERS" ids "$(to_native_path "$BODY_FILE")" | tr -d '\r'); do
  [[ "$(call DELETE "/api/exams/$E4/assignments/$aid" "$TEACHER")" == "200" ]] && REVOKED=$((REVOKED + 1))
done
expect_status "thu hoi ca hai em" 2 "$REVOKED"
expect_status "doc de → 200" 200 "$(call GET "/api/exams/$E4" "$TEACHER")"
expect_json   "  assignedCount = 0" assignedCount 0
expect_status "doi sang 6 cau luc khong con luot giao → 200" 200 \
  "$(put_exam "$E4" title="De thu hoi $STAMP" total=6 easy=50 medium=50 hard=0 point=1)"
expect_status "giao lai cho lop → 200" 200 "$(assign_class "$E4")"
expect_json   "  assignedCount = 2" assignedCount 2
expect_status "em A bat dau → 200" 200 "$(start_exam "$TOK_A" "$E4")"
expect_json   "  6 cau (khong phai 4 cua ban de truoc khi thu hoi)" totalQuestions 6
expect_status "  thang diem 6 = 6 cau x 1 diem" 6 "$(num_field maxScore)"

# ── 6. Nhan ban ─────────────────────────────────────────────────────────────────────────────────
section "Nhan ban de da khoa"
expect_status "nhan ban → 200" 200 "$(call POST "/api/exams/$E1/duplicate" "$TEACHER")"
COPY=$(json_field id); [[ -n "$COPY" ]] && EXAM_IDS+=("$COPY")
expect_status "  la de moi" true "$( [[ -n "$COPY" && "$COPY" != "$E1" ]] && echo true || echo false )"
expect_json   "  lifecycle = draft"      lifecycle draft
expect_status "  ten them hau to (ban sao)" yes "$(title_ends ban_sao)"
expect_json   "  assignedCount = 0"      assignedCount 0
expect_json   "  startedCount = 0"       startedCount 0
expect_json   "  blueprintFrozen = false" blueprintFrozen false
expect_json   "  cung so cau"            totalQuestions 10
expect_json   "  cung ty le De"          easyPercentage 60
expect_status "  cung diem moi cau"      2 "$(num_field pointPerQuestion)"
expect_json   "  cung thoi luong"        durationMinutes 40
expect_json   "  cung chu de"            topic "$TOPIC"
expect_json   "  cung tuy chon tron cau" shuffleQuestions true
expect_status "danh sach giao bai cua ban sao → 200" 200 "$(call GET "/api/exams/$COPY/assignments" "$TEACHER")"
expect_status "  khong co luot giao nao" 0 "$(body_count)"
expect_status "ban sao sua duoc cau hinh → 200" 200 \
  "$(put_exam "$COPY" title="Ban sao da sua $STAMP" total=6 lifecycle=draft)"
expect_json   "  6 cau" totalQuestions 6

section "Nhan ban: ranh gioi quyen"
expect_status "de khong ton tai → 404" 404 "$(call POST /api/exams/999999/duplicate "$TEACHER")"
expect_status "quan tri vien → 403" 403 "$(call POST "/api/exams/$E1/duplicate" "$ADMIN")"
expect_status "hoc sinh → 403" 403 "$(call POST "/api/exams/$E1/duplicate" "$TOK_A")"

if [[ -z "$PSQL" || -z "${PGPASSWORD:-}" ]]; then
  # Khong bo qua trong im lang: mot phep kiem vang mat trong bao cao la mot phep kiem khong ai biet
  # la chua chay.
  expect_status "dung giao vien thu hai (can psql va thong tin ket noi)" ok "thieu"
else
  FOREIGN_EMAIL="gv$STAMP@kiemthu.vn"
  FOREIGN_ID=$(clone_teacher "GV kiem thu $STAMP" "$FOREIGN_EMAIL" '"SubjectId"')
  if [[ -z "$FOREIGN_ID" ]]; then
    expect_status "dung giao vien thu hai qua psql" ok "hong"
  else
    FOREIGN=$(login "$FOREIGN_EMAIL" "123456")
    expect_status "giao vien khac nhan ban de → 404" 404 "$(call POST "/api/exams/$E1/duplicate" "$FOREIGN")"
    expect_status "giao vien khac sua de → 404"      404 "$(call PUT "/api/exams/$E1" "$FOREIGN" "$(exam_json)")"
    expect_status "giao vien khac doc de → 404"      404 "$(call GET "/api/exams/$E1" "$FOREIGN")"
  fi

  section "Nhan ban: giao vien chua duoc gan mon bi chan nhu khi tao de"
  # Giao vien chua gan mon khong tao duoc de nao, nen de "cua ho" phai dung bang cach chuyen chu mot de
  # nhap cua giao vien demo sang ho. Truoc day nhan ban bo qua cong nay: ho nhan duoc mot ban nhap moi
  # ma khong bao gio xuat ban duoc.
  NOSUBJECT_EMAIL="gvkm$STAMP@kiemthu.vn"
  NOSUBJECT_ID=$(clone_teacher "GV chua gan mon $STAMP" "$NOSUBJECT_EMAIL" NULL)
  if [[ -z "$NOSUBJECT_ID" ]]; then
    expect_status "dung giao vien chua gan mon qua psql" ok "hong"
  else
    new_exam title="De cua GV chua gan mon $STAMP" lifecycle=draft; E5=$NEW_EID
    sql "UPDATE \"Exams\" SET \"CreatedByUserId\" = $NOSUBJECT_ID WHERE \"Id\" = $E5;" > /dev/null
    NOSUBJECT=$(login "$NOSUBJECT_EMAIL" "123456")
    expect_status "doc de cua chinh minh → 200 (chi doc thi khong can mon)" 200 "$(call GET "/api/exams/$E5" "$NOSUBJECT")"
    expect_status "nhan ban de cua chinh minh → 403" 403 "$(call POST "/api/exams/$E5/duplicate" "$NOSUBJECT")"
    expect_status "  loi noi ro 'chua duoc gan mon'" yes "$(body_says chua_gan_mon)"
    expect_status "danh sach de cua ho → 200" 200 "$(call GET /api/exams "$NOSUBJECT")"
    expect_json   "  van chi mot de, khong co ban sao" total 1
    sql "UPDATE \"Exams\" SET \"CreatedByUserId\" = $TEACHER_ID WHERE \"Id\" = $E5;" > /dev/null
  fi
fi

summary
