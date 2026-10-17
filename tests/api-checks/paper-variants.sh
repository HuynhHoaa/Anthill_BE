#!/usr/bin/env bash
# Chia mot "de lon" thanh nhieu de nho (paperCount > 1) cho moi lop.
#
# Cau hinh thu: de lon 24 cau (9 De / 9 Trung binh / 6 Kho), chia 4 de, moi de 8 cau (3 / 3 / 2), diem theo muc
# 1 / 1,5 / 1,25 → moi de dung 10 diem. Nhu cau 12 / 12 / 8 luot cau > 9 / 9 / 6 cau cua de lon, nen phai trung cau:
# dung 3 / 3 / 2 cau nam o hai de (chia phan tang cho phep trung, uu tien du 8 cau moi de va dung ty le).
#
# Luat can khoa lai:
#   - kiem request: so de 1..10, chi voi de theo danh sach, cau cung muc phai cung diem, de lon du cau tung muc;
#   - moi lop duoc chia rieng (ba lop ra ba cach chia khac nhau), moi de dung 8 cau dung 3 / 3 / 2, khong cau lap trong mot de;
#   - phat de ngau nhien nhung can bang: so em cua hai de chenh nhau toi da 1, ke ca khi giao them hoc sinh vao lop;
#   - hoc sinh khong thay ma de; giao vien thay ma de (A, B, C, D) o bang theo doi va o /papers;
#   - sua de khi chua ai lam: chia lai; khi da co em lam: doi so de / so cau moi de bi 409, doi ten van duoc.
#
# Chay:  bash tests/api-checks/paper-variants.sh   (API phai dang chay o $HOST)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
TOPIC="ChiaDe$STAMP"
QUESTIONS=(); EXAMS=(); STUDENTS=(); GROUP_IDS=()
EASY=(); MEDIUM=(); HARD=()
CLASS=()      # id cac lop
CLASS_SIZE=(9 8 7)

cleanup() {
  section "Don du lieu"
  local exams=0 kept=0 questions=0 groups=0 locked=0
  for id in "${EXAMS[@]}"; do
    if [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]]; then exams=$((exams + 1)); else kept=$((kept + 1)); fi
  done
  for id in "${QUESTIONS[@]}"; do
    if [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]]; then
      questions=$((questions + 1))
    else
      # Cau nam trong de da co bai lam nen khong xoa duoc: an di de khoi lan vao ngan hang that.
      call PATCH "/api/questions/$id/status" "$TEACHER" '{"status":"hidden"}' > /dev/null
    fi
  done
  for id in "${STUDENTS[@]}"; do
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}')" == "200" ]] && locked=$((locked + 1))
  done
  for id in "${GROUP_IDS[@]}"; do
    if [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]]; then
      groups=$((groups + 1))
    else
      # Lop con bai giao thi khong xoa duoc: go cong bo de giao vien khong thay lop kiem thu.
      call POST "/api/student-groups/$id/unpublish" "$ADMIN" > /dev/null
    fi
  done
  printf '  da xoa %d/%d de · %d/%d cau hoi · %d/%d lop · khoa %d/%d hoc sinh\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "$locked" "${#STUDENTS[@]}"
  [[ $kept -gt 0 ]] && printf '  %d de o lai (da co em lam bai nen khong xoa duoc) — dung thiet ke\n' "$kept"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
py() { python -c "import json,sys; d=json.load(open(sys.argv[2], encoding='utf-8')); print($1)" "" "$BODY_FILE"; }

# ── Dung du lieu ──────────────────────────────────────────────────────────────────────────────────
section "Dung 3 lop (9 / 8 / 7 hoc sinh) va de lon 24 cau (9 De / 9 Trung binh / 6 Kho)"

FIRST_STUDENT_USER=""
for index in 0 1 2; do
  gid=$(new_class "$ADMIN" "$TEACHER" "ChiaDe${index} $STAMP" "Lop kiem thu chia de") || fail_fast "khong dung duoc lop $index"
  GROUP_IDS+=("$gid"); CLASS+=("$gid")
  for n in $(seq 1 "${CLASS_SIZE[$index]}"); do
    c=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS CD${index}-${n} $STAMP\",\"email\":\"cd${index}x${n}$STAMP@kiemthu.vn\",\"username\":\"cd${index}x${n}$STAMP\",\"code\":\"CD${index}X${n}$STAMP\",\"password\":\"123456\",\"groupId\":$gid}")
    [[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
    STUDENTS+=("$(json_field id)")
    [[ $index -eq 0 && $n -eq 1 ]] && FIRST_STUDENT_USER="cd${index}x${n}$STAMP"
  done
done
printf '  lop: %s · hoc sinh: %d\n' "${CLASS[*]}" "${#STUDENTS[@]}"

make_questions() { # $1 muc, $2 so cau, $3 ten mang
  local level="$1" count="$2" target="$3" i id
  for i in $(seq 1 "$count"); do
    c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau $level $i - $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"$level\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
    [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
    id=$(json_field id)
    QUESTIONS+=("$id")
    eval "$target+=(\"$id\")"
  done
}
make_questions easy 9 EASY
make_questions medium 9 MEDIUM
make_questions hard 6 HARD

# Mang JSON cac cau: diem cung muc cung diem ($1 De, $2 Trung binh, $3 Kho)
list_json() {
  local pe="$1" pm="$2" ph="$3" out="" id
  for id in "${EASY[@]}";   do out+="{\"questionId\":$id,\"point\":$pe},"; done
  for id in "${MEDIUM[@]}"; do out+="{\"questionId\":$id,\"point\":$pm},"; done
  for id in "${HARD[@]}";   do out+="{\"questionId\":$id,\"point\":$ph},"; done
  printf '[%s]' "${out%,}"
}
LIST=$(list_json 1 1.5 1.25)

# $1 tieu de, $2 so de, $3 so cau moi de, $4/$5/$6 ty le, $7 mang cau, $8 lifecycle (tuy chon)
exam_body() {
  local life=""; [[ -n "${8:-}" ]] && life=",\"lifecycle\":\"$8\""
  printf '{"title":"%s","description":"","durationMinutes":45,"totalQuestions":%s,"easyPercentage":%s,"mediumPercentage":%s,"hardPercentage":%s,"shuffleQuestions":true,"shuffleAnswers":true,"showResultAfterSubmit":true,"openAt":"2020-01-01T00:00:00Z","closeAt":"2035-01-01T00:00:00Z","paperCount":%s,"questions":%s%s}' \
    "$1" "$3" "$4" "$5" "$6" "$2" "$7" "$life"
}
track() { EXAMS+=("$(json_field id)"); }

# ── Kiem request ─────────────────────────────────────────────────────────────────────────────────
section "Kiem request: so de, diem, de lon du cau"
expect_status "so de = 11 → 400" 400 "$(call POST /api/exams "$TEACHER" "$(exam_body "Sai $STAMP" 11 8 38 37 25 "$LIST")")"
expect_status "so de = 0 → 400" 400 "$(call POST /api/exams "$TEACHER" "$(exam_body "Sai $STAMP" 0 8 38 37 25 "$LIST")")"
expect_status "chia nhieu de ma khong co danh sach cau → 400" 400 \
  "$(call POST /api/exams "$TEACHER" "{\"title\":\"Ma tran $STAMP\",\"description\":\"\",\"topic\":\"$TOPIC\",\"durationMinutes\":45,\"totalQuestions\":8,\"easyPercentage\":38,\"mediumPercentage\":37,\"hardPercentage\":25,\"pointEasy\":1,\"pointMedium\":1.5,\"pointHard\":1.25,\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":true,\"openAt\":\"2020-01-01T00:00:00Z\",\"closeAt\":\"2035-01-01T00:00:00Z\",\"paperCount\":4}")"

# hai cau De khac diem
FIRST_EASY="${EASY[0]}"
BAD_LIST=$(python -c "
import json,sys
items=json.loads(sys.argv[1])
items[0]['point']=0.5
print(json.dumps(items))" "$LIST")
expect_status "cau cung muc khac diem → 400" 400 "$(call POST /api/exams "$TEACHER" "$(exam_body "Diem le $STAMP" 4 8 38 37 25 "$BAD_LIST")")"
expect_status "  noi 'cung diem'" yes "$(body | grep -q 'cùng điểm' && echo yes || echo no)"

expect_status "xuat ban khi 8 cau Kho khong du: moi de 20 cau → 400" 400 \
  "$(call POST /api/exams "$TEACHER" "$(exam_body "Thieu cau $STAMP" 4 30 38 37 25 "$LIST")")"
expect_status "  noi muc Kho/De thieu bao nhieu" yes "$(body | grep -q 'mỗi đề cần' && echo yes || echo no)"
expect_status "ty le khong cong du 100 → 400" 400 "$(call POST /api/exams "$TEACHER" "$(exam_body "Ty le $STAMP" 4 8 38 37 20 "$LIST")")"

c=$(call POST /api/exams "$TEACHER" "$(exam_body "Nhap thieu $STAMP" 4 30 38 37 25 "$LIST" draft)")
expect_status "luu NHAP dang do (thieu cau) van duoc → 200" 200 "$c"
DRAFT=$(json_field id); track
expect_status "xuat ban ban nhap thieu cau → 400" 400 "$(call POST "/api/exams/$DRAFT/publish" "$TEACHER")"

# ── Tao de hop le ────────────────────────────────────────────────────────────────────────────────
section "De 4 de x 8 cau (3 / 3 / 2), diem 1 / 1,5 / 1,25"
expect_status "tao de → 200" 200 "$(call POST /api/exams "$TEACHER" "$(exam_body "Chia 4 de $STAMP" 4 8 38 37 25 "$LIST")")"
EXAM=$(json_field id); track
expect_json "  paperCount = 4" paperCount 4
expect_json "  totalQuestions = 8 (cua MOT de)" totalQuestions 8
expect_json "  totalScore = 10 (cua MOT de)" totalScore 10.0
expect_json "  khong bat randomizePerStudent" randomizePerStudent false

expect_status "GET papers truoc khi giao → 200" 200 "$(call GET "/api/exams/$EXAM/papers" "$TEACHER")"
expect_json "  paperCount = 4" paperCount 4
expect_json "  chua co lop nao" classes "[]"
expect_json "  cau De trung chung: 3" levels.0.sharedQuestions 3
expect_json "  cau Trung binh trung chung: 3" levels.1.sharedQuestions 3
expect_json "  cau Kho trung chung: 2" levels.2.sharedQuestions 2
expect_json "  du cau moi muc" levels.0.sufficient true

# ── Giao cho ba lop ──────────────────────────────────────────────────────────────────────────────
section "Giao cho 3 lop (24 hoc sinh)"
CLASSES_JSON=$(printf '{"groupId":%s},{"groupId":%s},{"groupId":%s}' "${CLASS[0]}" "${CLASS[1]}" "${CLASS[2]}")
expect_status "giao → 200" 200 "$(call POST "/api/exams/$EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[],\"classes\":[$CLASSES_JSON],\"notify\":false}")"
expect_json "  giao cho 24 em" assignedCount 24

expect_status "GET papers sau khi giao → 200" 200 "$(call GET "/api/exams/$EXAM/papers" "$TEACHER")"
expect_status "  co 3 lop" 3 "$(py "len(d['classes'])")"
expect_status "  moi lop 4 de" True "$(py "all(len(c['papers']) == 4 for c in d['classes'])")"
expect_status "  moi de 8 cau, dung 3/3/2" True \
  "$(py "all((p['questionCount'], p['easy'], p['medium'], p['hard']) == (8, 3, 3, 2) for c in d['classes'] for p in c['papers'])")"
expect_status "  khong cau lap trong mot de" True \
  "$(py "all(len(set(p['questionIds'])) == len(p['questionIds']) for c in d['classes'] for p in c['papers'])")"
expect_status "  moi lop dung het 24 cau cua de lon" True \
  "$(py "all(len(set(i for p in c['papers'] for i in p['questionIds'])) == 24 for c in d['classes'])")"
expect_status "  moi cau dung 1 hoac 2 lan (khong cau nao bi bo, khong cau nao dung 3 lan)" True \
  "$(py "all(max(__import__('collections').Counter(i for p in c['papers'] for i in p['questionIds']).values()) == 2 for c in d['classes'])")"
expect_status "  so cau trung chung moi lop = 3 + 3 + 2" True \
  "$(py "all(sum(1 for n in __import__('collections').Counter(i for p in c['papers'] for i in p['questionIds']).values() if n >= 2) == 8 for c in d['classes'])")"
expect_status "  so em cua lop = si so" True \
  "$(py "sorted(c['students'] for c in d['classes']) == [7, 8, 9]")"
expect_status "  hai de cua mot lop chenh nhau toi da 1 em" True \
  "$(py "all(max(p['students'] for p in c['papers']) - min(p['students'] for p in c['papers']) <= 1 for c in d['classes'])")"
expect_status "  ba lop duoc chia KHAC NHAU (xao rieng tung lop)" True \
  "$(py "len({tuple(frozenset(p['questionIds']) for p in c['papers']) for c in d['classes']}) == 3")"
expect_status "  ma de A, B, C, D" True \
  "$(py "all([p['label'] for p in c['papers']] == ['A','B','C','D'] for c in d['classes'])")"

# ── Bang theo doi: giao vien thay ma de ──────────────────────────────────────────────────────────
section "Bang theo doi cua giao vien co ma de, can bang theo tung lop"
expect_status "GET assignments → 200" 200 "$(call GET "/api/exams/$EXAM/assignments" "$TEACHER")"
expect_status "  24 dong, dong nao cung co ma de A-D" True "$(py "len(d) == 24 and all(r.get('paperLabel') in ['A','B','C','D'] for r in d)")"
expect_status "  can bang trong tung lop" True \
  "$(py "all(max(n) - min(n) <= 1 for n in [[sum(1 for r in d if r['assignedViaGroupId'] == g and r['paperLabel'] == l) for l in 'ABCD'] for g in {r['assignedViaGroupId'] for r in d}])")"
expect_status "  khong theo thu tu danh sach (A,B,C,D,A,...)" True \
  "$(py "[r['paperLabel'] for r in d] != list(('ABCD' * 6)[:24])")"

# ── Hoc sinh ─────────────────────────────────────────────────────────────────────────────────────
section "Hoc sinh lam bai tren de nho cua minh; khong thay ma de"
STUDENT=$(login_and_clear_must_change "$FIRST_STUDENT_USER" "123456" "Cd$STAMP")
expect_status "danh sach bai cua hoc sinh → 200" 200 "$(call GET /api/my/exams "$STUDENT")"
expect_status "  khong co paperLabel" True "$(py "not any('paperLabel' in i for i in d)")"
ASSIGNMENT=$(py "next(i['assignmentId'] for i in d if i['examId'] == $EXAM)")
expect_status "bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$STUDENT" "{\"assignmentId\":$ASSIGNMENT}")"
ATTEMPT=$(json_field attemptId)
expect_status "  de co 8 cau" 8 "$(py "len(d['questions'])")"
expect_status "  khong lo ma de" True "$(py "'paperLabel' not in d and not any('paperLabel' in q for q in d['questions'])")"
expect_status "nop bai → 200" 200 "$(call POST "/api/exam-attempts/$ATTEMPT/submit" "$STUDENT" '{"clientSeq":1,"answers":[]}')"
expect_status "giao vien xem bai → 200" 200 "$(call GET "/api/results/$ATTEMPT" "$TEACHER")"
expect_status "  diem toi da = 10 (cua mot de)" 10 "$(py "int(d['maxScore'])")"
expect_status "  8 cau" 8 "$(py "len(d['questions'])")"
expect_status "  giao vien thay ma de cua em" True "$(py "d.get('paperLabel') in list('ABCD')")"
expect_status "hoc sinh xem bai cua minh → 200" 200 "$(call GET "/api/results/$ATTEMPT" "$STUDENT")"
expect_status "  hoc sinh khong thay ma de" True "$(py "'paperLabel' not in d")"

section "Phan quyen"
expect_status "hoc sinh doc cach chia de → 403" 403 "$(call GET "/api/exams/$EXAM/papers" "$STUDENT")"
expect_status "quan tri vien doc duoc → 200" 200 "$(call GET "/api/exams/$EXAM/papers" "$ADMIN")"
expect_status "de khong ton tai → 404" 404 "$(call GET "/api/exams/99999999/papers" "$TEACHER")"

# ── Sua de khi da co em lam ──────────────────────────────────────────────────────────────────────
section "Sua de khi da co em lam: doi so de / so cau bi khoa, doi ten van duoc"
expect_status "doi so de 4 → 2 → 409" 409 "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(exam_body "Chia 4 de $STAMP" 2 8 38 37 25 "$LIST")")"
expect_status "  noi 'số đề'" yes "$(body | grep -q 'số đề' && echo yes || echo no)"
expect_status "doi so cau moi de 8 → 6 → 409" 409 "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(exam_body "Chia 4 de $STAMP" 4 6 38 37 25 "$LIST")")"
expect_status "doi ten → 200" 200 "$(call PUT "/api/exams/$EXAM" "$TEACHER" "$(exam_body "Chia 4 de (doi ten) $STAMP" 4 8 38 37 25 "$LIST")")"
expect_json "  paperCount van 4" paperCount 4

# ── Giao them hoc sinh vao lop dang co nguoi ─────────────────────────────────────────────────────
section "Giao them 1 hoc sinh vao lop 1: nhan mot de it nguoi nhat"
c=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS them $STAMP\",\"email\":\"them$STAMP@kiemthu.vn\",\"username\":\"them$STAMP\",\"code\":\"THEM$STAMP\",\"password\":\"123456\",\"groupId\":${CLASS[0]}}")
[[ "$c" != "200" ]] && fail_fast "tao hoc sinh them: HTTP $c — $(body)"
STUDENTS+=("$(json_field id)")
expect_status "giao lai lop 1 → 200" 200 "$(call POST "/api/exams/$EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[],\"classes\":[{\"groupId\":${CLASS[0]}}],\"notify\":false}")"
expect_json "  chi 1 em moi" assignedCount 1
expect_status "GET papers → 200" 200 "$(call GET "/api/exams/$EXAM/papers" "$TEACHER")"
expect_status "  lop 1 co 10 em, hai de chenh nhau toi da 1" True \
  "$(py "next((c['students'], max(p['students'] for p in c['papers']) - min(p['students'] for p in c['papers'])) for c in d['classes'] if c['groupId'] == ${CLASS[0]}) in [(10, 0), (10, 1)]")"
expect_status "  cach chia cua lop khong doi (van 4 de 8 cau)" True \
  "$(py "all(p['questionCount'] == 8 for c in d['classes'] for p in c['papers'])")"

# ── Sua de khi chua ai lam: chia lai ─────────────────────────────────────────────────────────────
section "De thu hai: sua khi chua ai lam thi chia lai va phat lai"
expect_status "tao de 4 de → 200" 200 "$(call POST /api/exams "$TEACHER" "$(exam_body "Chia lai $STAMP" 4 8 38 37 25 "$LIST")")"
EXAM2=$(json_field id); track
expect_status "giao cho lop 2 → 200" 200 "$(call POST "/api/exams/$EXAM2/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[],\"classes\":[{\"groupId\":${CLASS[1]}}],\"notify\":false}")"
expect_status "doi sang 3 de, moi de 6 cau (2/2/2) → 200" 200 "$(call PUT "/api/exams/$EXAM2" "$TEACHER" "$(exam_body "Chia lai $STAMP" 3 6 34 33 33 "$LIST")")"
expect_json "  paperCount = 3" paperCount 3
expect_json "  totalQuestions = 6" totalQuestions 6
expect_status "GET papers → 200" 200 "$(call GET "/api/exams/$EXAM2/papers" "$TEACHER")"
expect_status "  lop 2 co 3 de, moi de 6 cau (2/2/2)" True \
  "$(py "len(d['classes']) == 1 and len(d['classes'][0]['papers']) == 3 and all((p['questionCount'], p['easy'], p['medium'], p['hard']) == (6, 2, 2, 2) for p in d['classes'][0]['papers'])")"
expect_status "  tong so em = 8" 8 "$(py "d['classes'][0]['students']")"
expect_status "GET assignments → 200" 200 "$(call GET "/api/exams/$EXAM2/assignments" "$TEACHER")"
expect_status "  ma de chi con A, B, C" True "$(py "all(r.get('paperLabel') in ['A','B','C'] for r in d) and len(d) == 8")"

section "Nhan ban giu so de"
expect_status "nhan ban → 200" 200 "$(call POST "/api/exams/$EXAM2/duplicate" "$TEACHER")"
track
expect_json "  ban sao cung 3 de" paperCount 3
expect_json "  la ban nhap" lifecycle draft

# ── Diem cong de moi de nho dung 10 ──────────────────────────────────────────────────────────────
section "Diem cong: 3 / 3 / 3 cau khong chia ra dung 10 → cong 0,01 cho 1 cau Kho moi de"
# 9 cau moi de (33 / 33 / 34 → 3 / 3 / 3), diem 0,9 / 1,1 / 1,33 → 9,99; cong 1 don vi 0,01 → 10,00.
LIST_BONUS=$(list_json 0.9 1.1 1.33)
bonus_body() { # $1 tieu de, $2 so de, $3 don vi cong
  printf '{"title":"%s","description":"","durationMinutes":45,"totalQuestions":9,"easyPercentage":33,"mediumPercentage":33,"hardPercentage":34,"shuffleQuestions":true,"shuffleAnswers":true,"showResultAfterSubmit":true,"openAt":"2020-01-01T00:00:00Z","closeAt":"2035-01-01T00:00:00Z","paperCount":%s,"paperBonusUnits":%s,"questions":%s}' \
    "$1" "$2" "$3" "$LIST_BONUS"
}
expect_status "diem cong voi de mot de → 400" 400 "$(call POST /api/exams "$TEACHER" "$(bonus_body "Cong sai $STAMP" 1 1)")"
expect_status "diem cong nhieu hon so cau cua mot de → 400" 400 "$(call POST /api/exams "$TEACHER" "$(bonus_body "Cong qua $STAMP" 2 10)")"
expect_status "diem cong am → 400" 400 "$(call POST /api/exams "$TEACHER" "$(bonus_body "Cong am $STAMP" 2 -1)")"

expect_status "tao de co diem cong → 200" 200 "$(call POST /api/exams "$TEACHER" "$(bonus_body "Cong $STAMP" 2 1)")"
EXAM_BONUS=$(json_field id); track
expect_json "  paperBonusUnits = 1" paperBonusUnits 1
expect_json "  totalScore cua mot de nho = 10 (da tinh diem cong)" totalScore 10.0
expect_status "giao cho lop 3 → 200" 200 "$(call POST "/api/exams/$EXAM_BONUS/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[],\"classes\":[{\"groupId\":${CLASS[2]}}],\"notify\":false}")"

BONUS_STUDENT=$(login_and_clear_must_change "cd2x1$STAMP" "123456" "Cd$STAMP")
expect_status "hoc sinh xem bai cua minh → 200" 200 "$(call GET /api/my/exams "$BONUS_STUDENT")"
BONUS_ASSIGNMENT=$(py "next(i['assignmentId'] for i in d if i['examId'] == $EXAM_BONUS)")
expect_status "bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$BONUS_STUDENT" "{\"assignmentId\":$BONUS_ASSIGNMENT}")"
BONUS_ATTEMPT=$(json_field attemptId)
expect_status "  de co 9 cau" 9 "$(py "len(d['questions'])")"
expect_status "  diem toi da cua de = 10" True "$(py "round(d['maxScore'] * 100) == 1000")"
expect_status "  tong diem cac cau = 10,00 (khong thieu mot xu)" True "$(py "sum(round(q['point'] * 100) for q in d['questions']) == 1000")"
expect_status "  dung 1 cau Kho duoc cong 0,01 (1,34), cac cau con lai dung diem muc" True \
  "$(py "sorted(round(q['point'] * 100) for q in d['questions']) == [90, 90, 90, 110, 110, 110, 133, 133, 134]")"
expect_status "nop bai → 200" 200 "$(call POST "/api/exam-attempts/$BONUS_ATTEMPT/submit" "$BONUS_STUDENT" '{"clientSeq":1,"answers":[]}')"
expect_status "giao vien xem bai → 200" 200 "$(call GET "/api/results/$BONUS_ATTEMPT" "$TEACHER")"
expect_status "  diem toi da trong ket qua = 10" True "$(py "round(d['maxScore'] * 100) == 1000")"

expect_status "doi diem cong khi da co em lam → 409" 409 "$(call PUT "/api/exams/$EXAM_BONUS" "$TEACHER" "$(bonus_body "Cong $STAMP" 2 0)")"
expect_status "  noi 'điểm cộng thêm'" yes "$(body | grep -q 'điểm cộng thêm' && echo yes || echo no)"
expect_status "doi ten, giu nguyen diem cong → 200" 200 "$(call PUT "/api/exams/$EXAM_BONUS" "$TEACHER" "$(bonus_body "Cong (doi ten) $STAMP" 2 1)")"
expect_json "  paperBonusUnits van 1" paperBonusUnits 1

# Sua khi chua ai lam: doi diem cong thi sinh lai ban de
expect_status "de moi, chua giao: tao → 200" 200 "$(call POST /api/exams "$TEACHER" "$(bonus_body "Cong 2 $STAMP" 2 1)")"
EXAM_BONUS2=$(json_field id); track
expect_status "giao cho lop 2 → 200" 200 "$(call POST "/api/exams/$EXAM_BONUS2/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[],\"classes\":[{\"groupId\":${CLASS[1]}}],\"notify\":false}")"
expect_status "doi diem cong 1 → 0 khi chua ai lam → 200" 200 "$(call PUT "/api/exams/$EXAM_BONUS2" "$TEACHER" "$(bonus_body "Cong 2 $STAMP" 2 0)")"
expect_json "  paperBonusUnits = 0" paperBonusUnits 0
expect_json "  totalScore = 9.99 (khong con diem cong)" totalScore 9.99
expect_status "nhan ban de giu diem cong → 200" 200 "$(call POST "/api/exams/$EXAM_BONUS/duplicate" "$TEACHER")"
track
expect_json "  ban sao cung paperBonusUnits = 1" paperBonusUnits 1

summary
