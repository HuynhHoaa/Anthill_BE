#!/usr/bin/env bash
# "Soan tu dong" theo chu de (chuong) va theo lich su giao bai cua lop.
#
# Luat can khoa lai:
#   - rut theo chu de: moi cau tra ve dung chu de da chon; rut tiep chuong khac vao de (loai cau da co) khong lap cau;
#   - excludeAssignedToGroupIds: bo nhung cau lop da nhan o de truoc (de giao ca lop chung mot ban de, va de chia nhieu de
#     thi chi tinh cau thuc su roi vao de cua lop), skippedAlreadyAssigned bao dung so cau bi bo;
#   - thieu cau sau khi loc thi khong loi: tra nhung cau co va bao `missing`;
#   - lop khong ton tai / khong thuoc quyen giao vien → 400 (khong am tham bo qua).
#
# Chay:  bash tests/api-checks/compose-history.sh   (API phai dang chay o $HOST)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
CH1="Chuong1-$STAMP"
CH2="Chuong2-$STAMP"
QUESTIONS=(); EXAMS=(); STUDENTS=(); GROUP_IDS=()
EASY1=(); MEDIUM1=(); EASY2=()

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
      call PATCH "/api/questions/$id/status" "$TEACHER" '{"status":"hidden"}' > /dev/null
    fi
  done
  for id in "${STUDENTS[@]}"; do
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" "$LOCK_BODY")" == "200" ]] && locked=$((locked + 1))
  done
  for id in "${GROUP_IDS[@]}"; do
    if [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]]; then
      groups=$((groups + 1))
    else
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

make_questions() { # $1 muc, $2 so cau, $3 chu de, $4 ten mang
  local level="$1" count="$2" topic="$3" target="$4" i id c
  for i in $(seq 1 "$count"); do
    c=$(call POST /api/questions "$TEACHER" "{\"title\":\"Cau $level $i $STAMP\",\"content\":\"Cau $level $i - $STAMP\",\"topic\":\"$topic\",\"difficulty\":\"$level\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
    [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
    id=$(json_field id)
    QUESTIONS+=("$id")
    eval "$target+=(\"$id\")"
  done
}

compose() { # $1 JSON
  call POST /api/exams/compose-questions "$TEACHER" "$1"
}

# ── Dung du lieu ──────────────────────────────────────────────────────────────────────────────────
section "Dung 2 lop (2 hoc sinh moi lop), chuong 1 (12 De + 8 Trung binh), chuong 2 (10 De)"
make_questions easy 12 "$CH1" EASY1
make_questions medium 8 "$CH1" MEDIUM1
make_questions easy 10 "$CH2" EASY2

CLASS=()
for index in 0 1; do
  gid=$(new_class "$ADMIN" "$TEACHER" "LichSu${index} $STAMP" "Lop kiem thu loc cau") || fail_fast "khong dung duoc lop $index"
  GROUP_IDS+=("$gid"); CLASS+=("$gid")
  for n in 1 2; do
    c=$(call POST /api/students "$ADMIN" "{\"fullName\":\"HS LS${index}-${n} $STAMP\",\"email\":\"ls${index}x${n}$STAMP@kiemthu.vn\",\"username\":\"ls${index}x${n}$STAMP\",\"code\":\"LS${index}X${n}$STAMP\",\"password\":\"123456\",\"groupId\":$gid}")
    [[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
    STUDENTS+=("$(json_field id)")
  done
done
printf '  lop: %s\n' "${CLASS[*]}"

json_list() { # $1 diem, mang id...
  local point="$1" out="" id; shift
  for id in "$@"; do out+="{\"questionId\":$id,\"point\":$point},"; done
  printf '%s' "$out"
}
assign_to() { # $1 de, $2 lop
  call POST "/api/exams/$1/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[],\"classes\":[{\"groupId\":$2}],\"notify\":false}"
}

# De A: 5 cau De cua chuong 1, giao ca lop 1 (ca lop chung mot ban de).
USED_A=("${EASY1[@]:0:5}")
LIST_A="[$(json_list 2 "${USED_A[@]}" | sed 's/,$//')]"
c=$(call POST /api/exams "$TEACHER" "{\"title\":\"LichSu A $STAMP\",\"description\":\"\",\"durationMinutes\":45,\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":true,\"openAt\":\"2020-01-01T00:00:00Z\",\"closeAt\":\"2035-01-01T00:00:00Z\",\"questions\":$LIST_A}")
[[ "$c" != "200" ]] && fail_fast "tao de A: HTTP $c — $(body)"
EXAM_A=$(json_field id); EXAMS+=("$EXAM_A")
[[ "$(assign_to "$EXAM_A" "${CLASS[0]}")" != "200" ]] && fail_fast "giao de A: $(body)"

# De B: 8 cau Trung binh cua chuong 1 chia 2 de x 3 cau, giao lop 2 (hai em nhan hai de khac nhau → 6 cau da phat, 2 cau chua phat).
LIST_B="[$(json_list 1.5 "${MEDIUM1[@]}" | sed 's/,$//')]"
c=$(call POST /api/exams "$TEACHER" "{\"title\":\"LichSu B $STAMP\",\"description\":\"\",\"durationMinutes\":45,\"totalQuestions\":3,\"easyPercentage\":0,\"mediumPercentage\":100,\"hardPercentage\":0,\"paperCount\":2,\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":true,\"openAt\":\"2020-01-01T00:00:00Z\",\"closeAt\":\"2035-01-01T00:00:00Z\",\"questions\":$LIST_B}")
[[ "$c" != "200" ]] && fail_fast "tao de B: HTTP $c — $(body)"
EXAM_B=$(json_field id); EXAMS+=("$EXAM_B")
[[ "$(assign_to "$EXAM_B" "${CLASS[1]}")" != "200" ]] && fail_fast "giao de B: $(body)"
DELIVERED_B=$(call GET "/api/exams/$EXAM_B/papers" "$TEACHER" >/dev/null; py "len({qid for c in d['classes'] for p in c['papers'] for qid in p['questionIds']})")
printf '  de A (ca lop chung ban de): 5 cau · de B (2 de x 3 cau): %s cau da phat\n' "$DELIVERED_B"

# ── Soan theo chu de ─────────────────────────────────────────────────────────────────────────────
section "Soan theo chu de (chuong)"
expect_status "chuong 1: 4 cau De → 200" 200 "$(compose "{\"easy\":4,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\"}")"
expect_status "  du 4 cau, thieu 0" True "$(py "len(d['questions']) == 4 and d['missing']['easy'] == 0")"
expect_status "  moi cau deu thuoc chuong 1" True "$(py "all(q['topic'] == '$CH1' for q in d['questions'])")"
PICKED1=$(py "' '.join(str(q['id']) for q in d['questions'])")

expect_status "rut tiep chuong 2 (loai cau da co): 4 cau De → 200" 200 \
  "$(compose "{\"easy\":4,\"medium\":0,\"hard\":0,\"topic\":\"$CH2\",\"excludeQuestionIds\":[${PICKED1// /,}]}")"
expect_status "  moi cau deu thuoc chuong 2" True "$(py "all(q['topic'] == '$CH2' for q in d['questions'])")"
expect_status "  khong trung cau chuong 1" True "$(py "not (set(q['id'] for q in d['questions']) & set(map(int, '$PICKED1'.split())))")"

expect_status "chuong khong co cau nao → 200, thieu het" 200 "$(compose "{\"easy\":3,\"medium\":0,\"hard\":0,\"topic\":\"Khong-co-$STAMP\"}")"
expect_status "  khong co cau, thieu 3" True "$(py "len(d['questions']) == 0 and d['missing']['easy'] == 3")"

# ── Loc theo lich su giao bai cua lop ────────────────────────────────────────────────────────────
section "Bo cau lop da nhan (excludeAssignedToGroupIds)"
USED_JSON=$(printf '%s,' "${USED_A[@]}"); USED_JSON="[${USED_JSON%,}]"

expect_status "khong chon lop: rut het 12 cau De cua chuong 1 → 200" 200 "$(compose "{\"easy\":12,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\"}")"
expect_status "  du 12 cau (ke ca 5 cau lop 1 da nhan), khong bo cau nao" True "$(py "len(d['questions']) == 12 and d.get('skippedAlreadyAssigned', 0) == 0")"

expect_status "bo cau lop 1 da nhan, can 7 cau De → 200" 200 "$(compose "{\"easy\":7,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[${CLASS[0]}]}")"
expect_status "  du 7 cau" True "$(py "len(d['questions']) == 7 and d['missing']['easy'] == 0")"
expect_status "  khong co cau nao trong 5 cau lop 1 da nhan" True "$(py "not (set(q['id'] for q in d['questions']) & set($USED_JSON))")"
expect_status "  skippedAlreadyAssigned = 5" True "$(py "d['skippedAlreadyAssigned'] == 5")"

expect_status "can 9 cau De sau khi bo → 200, thieu 2" 200 "$(compose "{\"easy\":9,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[${CLASS[0]}]}")"
expect_status "  tra 7 cau, thieu 2 (khong bao loi)" True "$(py "len(d['questions']) == 7 and d['missing']['easy'] == 2")"

expect_status "lop 2 chua nhan cau De nao → 200" 200 "$(compose "{\"easy\":12,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[${CLASS[1]}]}")"
expect_status "  du 12 cau, khong bo cau nao" True "$(py "len(d['questions']) == 12 and d['skippedAlreadyAssigned'] == 0")"

expect_status "de chia 2 de: bo cau lop 2 da nhan (chi cau roi vao de cua lop) → 200" 200 \
  "$(compose "{\"easy\":0,\"medium\":8,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[${CLASS[1]}]}")"
expect_status "  chi bo cau da phat: tra $((8 - DELIVERED_B)) cau, thieu $DELIVERED_B" True "$(py "len(d['questions']) == 8 - $DELIVERED_B and d['missing']['medium'] == $DELIVERED_B")"
expect_status "  skippedAlreadyAssigned = $DELIVERED_B" True "$(py "d['skippedAlreadyAssigned'] == $DELIVERED_B")"

expect_status "bo ca hai lop → 200" 200 "$(compose "{\"easy\":12,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[${CLASS[0]},${CLASS[1]}]}")"
expect_status "  van chi bo 5 cau De (cau De chua phat cho lop 2)" True "$(py "len(d['questions']) == 7 and d['skippedAlreadyAssigned'] == 5")"

section "Phan quyen va kiem request"
expect_status "lop khong ton tai → 400" 400 "$(compose "{\"easy\":3,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[99999999]}")"
expect_status "tron lop hop le va lop la → 400 (khong am tham bo lop la)" 400 "$(compose "{\"easy\":3,\"medium\":0,\"hard\":0,\"topic\":\"$CH1\",\"excludeAssignedToGroupIds\":[${CLASS[0]},99999999]}")"
LONG=$(python -c "print(','.join(str(i) for i in range(1, 52)))")
expect_status "qua 50 lop → 400" 400 "$(compose "{\"easy\":3,\"medium\":0,\"hard\":0,\"excludeAssignedToGroupIds\":[$LONG]}")"
expect_status "quan tri vien khong soan tu dong → 403" 403 "$(call POST /api/exams/compose-questions "$ADMIN" "{\"easy\":3,\"medium\":0,\"hard\":0}")"

# ── Hop chon cau tu ngan hang: bo cau lop da nhan ───────────────────────────────────────────────
section "Hop chon cau (GET /api/questions) bo cau lop da nhan"
expect_status "chuong 1 khong loc → 200" 200 "$(call GET "/api/questions?topic=$CH1&pageSize=100" "$TEACHER")"
expect_status "  co 20 cau (12 De + 8 Trung binh)" True "$(py "d['total'] == 20")"
expect_status "bo cau lop 1 da nhan → 200" 200 "$(call GET "/api/questions?topic=$CH1&pageSize=100&excludeAssignedToGroupIds=${CLASS[0]}" "$TEACHER")"
expect_status "  con 15 cau, khong co cau nao cua de A" True "$(py "d['total'] == 15 and not (set(q['id'] for q in d['items']) & set($USED_JSON))")"
expect_status "bo cau lop 2 da nhan (de chia 2 de) → 200" 200 "$(call GET "/api/questions?topic=$CH1&pageSize=100&excludeAssignedToGroupIds=${CLASS[1]}" "$TEACHER")"
expect_status "  chi bo cau da phat: con $((20 - DELIVERED_B)) cau" True "$(py "d['total'] == 20 - $DELIVERED_B")"
expect_status "bo ca hai lop → 200" 200 "$(call GET "/api/questions?topic=$CH1&pageSize=100&excludeAssignedToGroupIds=${CLASS[0]},${CLASS[1]}" "$TEACHER")"
expect_status "  con $((20 - 5 - DELIVERED_B)) cau" True "$(py "d['total'] == 20 - 5 - $DELIVERED_B")"
expect_status "ket hop voi loc muc do → 200" 200 "$(call GET "/api/questions?topic=$CH1&difficulty=easy&pageSize=100&excludeAssignedToGroupIds=${CLASS[0]}" "$TEACHER")"
expect_status "  chi cau De chua phat cho lop 1: 7 cau" True "$(py "d['total'] == 7")"
expect_status "lop la → 400" 400 "$(call GET "/api/questions?excludeAssignedToGroupIds=99999999" "$TEACHER")"
expect_status "tron lop hop le va lop la → 400 (khong am tham bo lop la)" 400 "$(call GET "/api/questions?excludeAssignedToGroupIds=${CLASS[0]},99999999" "$TEACHER")"
expect_status "chuoi khong phai so → 400" 400 "$(call GET "/api/questions?excludeAssignedToGroupIds=abc" "$TEACHER")"
expect_status "qua 50 lop → 400" 400 "$(call GET "/api/questions?excludeAssignedToGroupIds=$LONG" "$TEACHER")"
expect_status "quan tri vien khong loc theo lop → 400" 400 "$(call GET "/api/questions?excludeAssignedToGroupIds=${CLASS[0]}" "$ADMIN")"
expect_status "bo trong tham so → khong loc, 200" 200 "$(call GET "/api/questions?topic=$CH1&pageSize=100&excludeAssignedToGroupIds=" "$TEACHER")"
expect_status "  van du 20 cau" True "$(py "d['total'] == 20")"

summary
