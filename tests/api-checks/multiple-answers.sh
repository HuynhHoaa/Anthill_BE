#!/usr/bin/env bash
# Cau hoi cho chon NHIEU DAP AN DUNG: soan, lam bai, cham diem, xem lai.
#
# Script nay khoa lai nhung dieu ma unit test khong cham toi duoc vi can HTTP + database that:
#   - soan cau: cau mot dap an van phai dung mot o dung, cau nhieu dap an cho tu mot o dung tro len;
#   - ban de cua hoc sinh biet cau nao cho chon nhieu o (allowMultiple) nhung khong lo o nao dung;
#   - luu nhap nhan tap o da chon (answerIds), van nhan kieu cu (answerId), tu choi gui ca hai, tu choi
#     nhieu o cho cau mot dap an, tu choi o khong thuoc cau;
#   - cham diem "dung va du moi duoc diem": thieu mot o dung hay thua mot o sai deu khong co diem;
#   - xem lai: selectedAnswerIds luon co, correctAnswerIds + isCorrect chi co khi duoc phep xem;
#   - lua chon nhieu o nap lai dung sau khi tai lai phien (savedAnswers la mang).
#
# Chay:  bash tests/api-checks/multiple-answers.sh   (API phai dang chay o $HOST, da migrate)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
TOPIC="NhieuDapAn$STAMP"
QUESTIONS=(); EXAMS=(); STUDENTS=(); GROUP_IDS=()
GROUP_ID=""

cleanup() {
  section "Don du lieu"
  local exams=0 kept=0 questions=0 groups=0 locked=0
  for id in "${EXAMS[@]}"; do
    if [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]]; then exams=$((exams + 1)); else kept=$((kept + 1)); fi
  done
  for id in "${QUESTIONS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1))
  done
  for id in "${STUDENTS[@]}"; do
    [[ -n "$GROUP_ID" ]] && call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$ADMIN" "$REMOVE_BODY" > /dev/null
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" "$LOCK_BODY")" == "200" ]] && locked=$((locked + 1))
  done
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1))
  done
  printf '  da xoa %d/%d de · %d/%d cau hoi · %d/%d lop · khoa %d/%d hoc sinh\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "$locked" "${#STUDENTS[@]}"
  [[ $kept -gt 0 ]] && printf '  %d de o lai (da co em lam bai nen khong xoa duoc, va cau hoi cua no cung o lai) — dung thiet ke\n' "$kept"
}
trap 'rm -f "${SEQ_FILE:-}"; cleanup' EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }

# Doc mot bieu thuc python tren response hien tai. $1 la bieu thuc theo bien `d` (JSON da parse).
py() { python -c "import json,sys; d=json.load(open(sys.argv[2], encoding='utf-8')); print($1)" "" "$BODY_FILE"; }

# $1 noi dung · $2 allowMultiple (true/false) · $3 chuoi cac vi tri dung, vi du "1,3" (tren bon phuong an)
question_body() {
  local multi="$2" correct=",$3," answers="" slot
  for slot in 1 2 3 4; do
    [[ -n "$answers" ]] && answers="$answers,"
    if [[ "$correct" == *",$slot,"* ]]; then
      answers="$answers{\"content\":\"PA$slot\",\"isCorrect\":true}"
    else
      answers="$answers{\"content\":\"PA$slot\",\"isCorrect\":false}"
    fi
  done
  printf '{"content":"%s","topic":"%s","difficulty":"easy","defaultPoint":1,"allowMultiple":%s,"answers":[%s]}' \
    "$1" "$TOPIC" "$multi" "$answers"
}

# ── Soan cau hoi ──────────────────────────────────────────────────────────────────────────────────
section "Soan cau: mot dap an va nhieu dap an"
expect_status "cau mot dap an, hai o dung → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "$(question_body "Don hai dung $STAMP" false "1,2")")"
expect_status "  goi y bat nhieu dap an dung" yes "$(body_has 'nhiều đáp án đúng')"
expect_status "cau mot dap an, khong o nao dung → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "$(question_body "Don khong dung $STAMP" false "")")"
expect_status "  cau chu cua cau mot dap an" yes "$(body_has 'Vui lòng chọn một đáp án đúng')"

expect_status "cau nhieu dap an, khong o nao dung → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "$(question_body "Nhieu khong dung $STAMP" true "")")"
expect_status "  cau chu rieng cua cau nhieu dap an" yes "$(body_has 'ít nhất một đáp án đúng')"

expect_status "cau mot dap an, mot o dung → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "$(question_body "Don $STAMP" false "2")")"
Q_SINGLE=$(json_field id); QUESTIONS+=("$Q_SINGLE")
expect_json "  allowMultiple = false" allowMultiple false

expect_status "cau nhieu dap an, hai o dung → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "$(question_body "Nhieu A $STAMP" true "1,3")")"
Q_MULTI=$(json_field id); QUESTIONS+=("$Q_MULTI")
expect_json "  allowMultiple = true" allowMultiple true
expect_status "  hai o duoc danh dau dung" 2 "$(py "sum(1 for a in d['answers'] if a['isCorrect'])")"

expect_status "cau nhieu dap an chi co mot o dung → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "$(question_body "Nhieu B $STAMP" true "4")")"
Q_MULTI_ONE=$(json_field id); QUESTIONS+=("$Q_MULTI_ONE")
expect_json "  allowMultiple = true" allowMultiple true

expect_status "doc lai cau nhieu dap an → 200" 200 "$(call GET "/api/questions/$Q_MULTI" "$TEACHER")"
expect_json "  allowMultiple van la true" allowMultiple true

expect_status "sua cau nhieu dap an ve mot dap an khi con hai o dung → 400" 400 \
  "$(call PUT "/api/questions/$Q_MULTI" "$TEACHER" "$(question_body "Nhieu A $STAMP" false "1,3")")"
expect_status "sua cau don thanh nhieu dap an → 200" 200 \
  "$(call PUT "/api/questions/$Q_SINGLE" "$TEACHER" "$(question_body "Don $STAMP" true "2,3")")"
expect_json "  allowMultiple = true" allowMultiple true
expect_status "tra cau do ve mot dap an → 200" 200 \
  "$(call PUT "/api/questions/$Q_SINGLE" "$TEACHER" "$(question_body "Don $STAMP" false "2")")"
expect_json "  allowMultiple = false" allowMultiple false

# ── Lop, hoc sinh, de ─────────────────────────────────────────────────────────────────────────────
section "Dung de gom 1 cau don + 2 cau nhieu dap an va giao cho mot lop"
GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "NhieuDA $STAMP" "Lop kiem thu nhieu dap an") || fail_fast "khong dung duoc lop"
GROUP_IDS+=("$GROUP_ID")
c=$(call POST /api/students "$ADMIN" "{\"fullName\":\"Hoc sinh NDA $STAMP\",\"email\":\"nda$STAMP@kiemthu.vn\",\"username\":\"nda$STAMP\",\"code\":\"NDA$STAMP\",\"password\":\"123456\",\"groupId\":$GROUP_ID}")
[[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
STUDENTS+=("$(json_field id)")
STUDENT=$(login_and_clear_must_change "nda$STAMP" "123456" "Nda$STAMP")

LIST="[{\"questionId\":$Q_SINGLE,\"point\":20},{\"questionId\":$Q_MULTI,\"point\":50},{\"questionId\":$Q_MULTI_ONE,\"point\":30}]"
expect_status "tao de xuat ban → 200" 200 \
  "$(call POST /api/exams "$TEACHER" "{\"title\":\"De nhieu dap an $STAMP\",\"description\":\"\",\"durationMinutes\":30,\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":true,\"openAt\":\"2020-01-01T00:00:00Z\",\"closeAt\":\"2035-01-01T00:00:00Z\",\"questions\":$LIST}")"
EXAM=$(json_field id); EXAMS+=("$EXAM")
expect_json "  tong diem 100" totalScore 100
expect_status "giao cho lop → 200" 200 \
  "$(call POST "/api/exams/$EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":false}")"
expect_json "  giao cho 1 em" assignedCount 1

call GET /api/my/exams "$STUDENT" > /dev/null
ASSIGNMENT=$(python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((i['assignmentId'] for i in items if i['examId'] == int(sys.argv[2])), ''))" "$BODY_FILE" "$EXAM")

# ── Ban de cua hoc sinh ───────────────────────────────────────────────────────────────────────────
section "Ban de: biet cau nao cho chon nhieu o, khong lo o nao dung"
expect_status "bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$STUDENT" "{\"assignmentId\":$ASSIGNMENT}")"
ATTEMPT=$(json_field attemptId)
expect_status "  ba cau, allowMultiple theo dung thu tu" "false,true,true" "$(py "','.join(str(q['allowMultiple']).lower() for q in d['questions'])")"
expect_status "  khong phuong an nao mang isCorrect" no \
  "$(py "'yes' if any('isCorrect' in a for q in d['questions'] for a in q['answers']) else 'no'")"
expect_status "  savedAnswers rong luc moi bat dau" 0 "$(py "len(d['savedAnswers'])")"

# id cua paper question va phuong an: tim theo noi dung
paper_q() { py "next(q['id'] for q in d['questions'] if q['content'] == '$1')"; }
paper_a() { py "next(a['id'] for q in d['questions'] if q['content'] == '$1' for a in q['answers'] if a['content'] == '$2')"; }
PQ_SINGLE=$(paper_q "Don $STAMP");     PQ_MULTI=$(paper_q "Nhieu A $STAMP");  PQ_ONE=$(paper_q "Nhieu B $STAMP")
S_PA2=$(paper_a "Don $STAMP" PA2);     S_PA1=$(paper_a "Don $STAMP" PA1)
M_PA1=$(paper_a "Nhieu A $STAMP" PA1); M_PA2=$(paper_a "Nhieu A $STAMP" PA2); M_PA3=$(paper_a "Nhieu A $STAMP" PA3)
O_PA4=$(paper_a "Nhieu B $STAMP" PA4); O_PA2=$(paper_a "Nhieu B $STAMP" PA2)

put() { call PUT "/api/exam-attempts/$ATTEMPT/answers" "$STUDENT" "$1"; }

# Bo dem goi tin phai song qua $( ): moi lan goi save chay trong mot subshell, nen bien thuong se bi mat va
# moi goi sau deu mang cung mot seq roi bi server bo qua nhu goi cu. Dung mot file de giu so.
SEQ_FILE=$(mktemp)
echo 0 > "$SEQ_FILE"
next_seq() {
  local n
  n=$(cat "$SEQ_FILE"); n=$((n + 1)); echo "$n" > "$SEQ_FILE"; echo "$n"
}
save() {  # $1 = mang JSON cac lua chon
  put "{\"clientSeq\":$(next_seq),\"answers\":$1}"
}

# ── Luu nhap ──────────────────────────────────────────────────────────────────────────────────────
section "Luu nhap: tap o da chon"
expect_status "cau nhieu dap an chon hai o → 200" 200 "$(save "[{\"questionId\":$PQ_MULTI,\"answerIds\":[$M_PA1,$M_PA3]}]")"
expect_status "tai lai phien → 200" 200 "$(call GET "/api/exam-attempts/$ATTEMPT" "$STUDENT")"
expect_status "  savedAnswers la mang hai o" "$(python -c "print(sorted([$M_PA1,$M_PA3]))")" \
  "$(py "sorted(d['savedAnswers']['$PQ_MULTI'])")"

expect_status "bot mot o (con mot o) → 200" 200 "$(save "[{\"questionId\":$PQ_MULTI,\"answerIds\":[$M_PA1]}]")"
expect_status "  tap chi con mot o" "[$M_PA1]" "$(call GET "/api/exam-attempts/$ATTEMPT" "$STUDENT" > /dev/null; py "d['savedAnswers']['$PQ_MULTI']")"
expect_status "bo chon het (mang rong) → 200" 200 "$(save "[{\"questionId\":$PQ_MULTI,\"answerIds\":[]}]")"
expect_status "  cau bien mat khoi savedAnswers" absent \
  "$(call GET "/api/exam-attempts/$ATTEMPT" "$STUDENT" > /dev/null; py "'present' if '$PQ_MULTI' in d['savedAnswers'] else 'absent'")"

expect_status "kieu cu answerId cho cau mot dap an → 200" 200 "$(save "[{\"questionId\":$PQ_SINGLE,\"answerId\":$S_PA1}]")"
expect_status "  luu thanh mang mot phan tu" "[$S_PA1]" "$(call GET "/api/exam-attempts/$ATTEMPT" "$STUDENT" > /dev/null; py "d['savedAnswers']['$PQ_SINGLE']")"
expect_status "answerIds mot phan tu cho cau mot dap an → 200" 200 "$(save "[{\"questionId\":$PQ_SINGLE,\"answerIds\":[$S_PA2]}]")"
expect_status "  doi sang o khac" "[$S_PA2]" "$(call GET "/api/exam-attempts/$ATTEMPT" "$STUDENT" > /dev/null; py "d['savedAnswers']['$PQ_SINGLE']")"

section "Luu nhap: nhung goi bi tu choi"
expect_status "cau mot dap an gui hai o → 422" 422 "$(save "[{\"questionId\":$PQ_SINGLE,\"answerIds\":[$S_PA1,$S_PA2]}]")"
expect_status "  noi cau nay chi duoc chon mot" yes "$(body_has 'chỉ được chọn một đáp án')"
expect_status "o khong thuoc cau → 422" 422 "$(save "[{\"questionId\":$PQ_MULTI,\"answerIds\":[$M_PA1,$O_PA4]}]")"
expect_status "gui ca answerId lan answerIds → 400" 400 "$(save "[{\"questionId\":$PQ_MULTI,\"answerId\":$M_PA1,\"answerIds\":[$M_PA2]}]")"
expect_status "answerIds co ma khong duong → 400" 400 "$(save "[{\"questionId\":$PQ_MULTI,\"answerIds\":[0]}]")"
expect_status "  cau bi tu choi khong ghi gi: cau mot dap an van la O thu hai" "[$S_PA2]" \
  "$(call GET "/api/exam-attempts/$ATTEMPT" "$STUDENT" > /dev/null; py "d['savedAnswers']['$PQ_SINGLE']")"

# ── Nop bai va cham ───────────────────────────────────────────────────────────────────────────────
section "Nop bai: dung va du moi duoc diem"
# cau don: dung (PA2) · cau nhieu A: chon PA1+PA3 = dung va du · cau nhieu B: chon PA4+PA2 = thua mot o sai
expect_status "nop bai → 200" 200 \
  "$(call POST "/api/exam-attempts/$ATTEMPT/submit" "$STUDENT" "{\"clientSeq\":$(next_seq),\"answers\":[{\"questionId\":$PQ_SINGLE,\"answerIds\":[$S_PA2]},{\"questionId\":$PQ_MULTI,\"answerIds\":[$M_PA3,$M_PA1]},{\"questionId\":$PQ_ONE,\"answerIds\":[$O_PA4,$O_PA2]}]}")"
expect_status "  dung 2 trong 3 cau" 2 "$(py "d['correctCount']")"
expect_status "  diem = 20 + 50 (cau thua o sai khong duoc diem)" 70 "$(py "int(d['score'])")"
expect_status "  ca ba cau deu tinh la da lam" 3 "$(py "d['answeredCount']")"
expect_status "nop lai cung luot → 200, ra dung ket qua cu" 200 \
  "$(call POST "/api/exam-attempts/$ATTEMPT/submit" "$STUDENT" "{\"clientSeq\":$(next_seq),\"answers\":[]}")"
expect_status "  van 70 diem" 70 "$(py "int(d['score'])")"

# ── Xem lai ───────────────────────────────────────────────────────────────────────────────────────
section "Xem lai: giao vien thay het, hoc sinh chua den gio mo dap an thi khong thay dap an dung"
expect_status "giao vien xem bai → 200" 200 "$(call GET "/api/results/$ATTEMPT" "$TEACHER")"
expect_status "  allowMultiple tung cau" "false,true,true" "$(py "','.join(str(q['allowMultiple']).lower() for q in d['questions'])")"
expect_status "  cau nhieu A: em chon 2 o" 2 "$(py "len(next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_MULTI)['selectedAnswerIds'])")"
expect_status "  cau nhieu A: 2 o dung" 2 "$(py "len(next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_MULTI)['correctAnswerIds'])")"
expect_status "  cau nhieu A: isCorrect = true" True "$(py "next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_MULTI)['isCorrect']")"
expect_status "  cau nhieu B (thua o sai): isCorrect = false" False "$(py "next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_ONE)['isCorrect']")"
expect_status "  cau nhieu B: dap an dung chi co mot o" "[$O_PA4]" "$(py "next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_ONE)['correctAnswerIds']")"
expect_status "  cau don: isCorrect = true" True "$(py "next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_SINGLE)['isCorrect']")"

expect_status "hoc sinh xem bai cua minh → 200" 200 "$(call GET "/api/results/$ATTEMPT" "$STUDENT")"
expect_status "  van thay lua chon cua minh" 2 "$(py "len(next(q for q in d['questions'] if q['paperQuestionId'] == $PQ_MULTI)['selectedAnswerIds'])")"
expect_status "  chua mo dap an: khong co correctAnswerIds" absent \
  "$(py "'present' if any('correctAnswerIds' in q for q in d['questions']) else 'absent'")"
expect_status "  chua mo dap an: khong co isCorrect" absent \
  "$(py "'present' if any('isCorrect' in q for q in d['questions']) else 'absent'")"
expect_status "  van biet cau nao cho chon nhieu o" "false,true,true" "$(py "','.join(str(q['allowMultiple']).lower() for q in d['questions'])")"

summary
