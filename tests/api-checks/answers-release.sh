#!/usr/bin/env bash
# "Mo dap an": giao vien mo diem va dap an cho hoc sinh, nhung CHI KHI DA HET GIO.
#
# Luat can khoa lai (POST /api/exams/{id}/release-answers):
#   - de chua giao cho lop nao → 409;
#   - con lop dang lam bai (hay chua toi gio lam bai) → 409, du da co em nop xong;
#   - chi chu de moi mo duoc (hoc sinh, quan tri vien bi chan);
#   - het gio cua lop dong bai muon nhat → 200, bat showResultAfterSubmit, hoc sinh thay diem va dap an dung;
#   - mo lan nua → 200, khong doi gi (idempotent).
#
# De tat "Hien ket qua sau khi nop" luc dau: hoc sinh nop xong khong thay diem lan dap an. Co mot quang cho
# ~35 giay that, vi de co khung gio chi khoang 30 giay (phai chua tron thoi luong 1 phut).
#
# Chay:  bash tests/api-checks/answers-release.sh   (API phai dang chay o $HOST)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
TOPIC="MoDapAn$STAMP"
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
    [[ -n "$GROUP_ID" ]] && call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$ADMIN" > /dev/null
    [[ "$(call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}')" == "200" ]] && locked=$((locked + 1))
  done
  for id in "${GROUP_IDS[@]}"; do
    [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1))
  done
  printf '  da xoa %d/%d de · %d/%d cau hoi · %d/%d lop · khoa %d/%d hoc sinh\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "$locked" "${#STUDENTS[@]}"
  [[ $kept -gt 0 ]] && printf '  %d de o lai (da co em lam bai nen khong xoa duoc) — dung thiet ke\n' "$kept"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }
py() { python -c "import json,sys; d=json.load(open(sys.argv[2], encoding='utf-8')); print($1)" "" "$BODY_FILE"; }

# ── Dung du lieu ──────────────────────────────────────────────────────────────────────────────────
section "Dung lop, hoc sinh, mot cau hoi va mot de co khung gio ngan, tat 'hien ket qua'"
GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "MoDA $STAMP" "Lop kiem thu mo dap an") || fail_fast "khong dung duoc lop"
GROUP_IDS+=("$GROUP_ID")
c=$(call POST /api/students "$ADMIN" "{\"fullName\":\"Hoc sinh MDA $STAMP\",\"email\":\"mda$STAMP@kiemthu.vn\",\"username\":\"mda$STAMP\",\"code\":\"MDA$STAMP\",\"password\":\"123456\",\"groupId\":$GROUP_ID}")
[[ "$c" != "200" ]] && fail_fast "tao hoc sinh: HTTP $c — $(body)"
STUDENTS+=("$(json_field id)")
STUDENT=$(login_and_clear_must_change "mda$STAMP" "123456" "Mda$STAMP")

c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau mo dap an $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
[[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
QUESTION=$(json_field id); QUESTIONS+=("$QUESTION")

# Khung gio: mo tu 5 phut truoc, dong sau ~30 giay (van chua tron thoi luong 1 phut vi mo tu truoc).
OPEN_AT=$(date -u -d '-5 minutes' +%Y-%m-%dT%H:%M:%SZ)
CLOSE_EPOCH=$(( $(date -u +%s) + 30 ))
CLOSE_AT=$(date -u -d "@$CLOSE_EPOCH" +%Y-%m-%dT%H:%M:%SZ)

c=$(call POST /api/exams "$TEACHER" "{\"title\":\"De mo dap an $STAMP\",\"description\":\"\",\"durationMinutes\":1,\"shuffleQuestions\":false,\"shuffleAnswers\":false,\"showResultAfterSubmit\":false,\"openAt\":\"$OPEN_AT\",\"closeAt\":\"$CLOSE_AT\",\"questions\":[{\"questionId\":$QUESTION,\"point\":100}]}")
[[ "$c" != "200" ]] && fail_fast "tao de: HTTP $c — $(body)"
EXAM=$(json_field id); EXAMS+=("$EXAM")
expect_json "  de tat hien ket qua" showResultAfterSubmit false

# ── Chua giao cho lop nao ─────────────────────────────────────────────────────────────────────────
section "De chua giao cho lop nao: khong co gi de mo"
expect_status "mo dap an → 409" 409 "$(call POST "/api/exams/$EXAM/release-answers" "$TEACHER")"
expect_status "  noi chua giao cho lop nao" yes "$(body_has 'chưa giao cho lớp nào')"

# ── Con dang trong gio lam bai ────────────────────────────────────────────────────────────────────
section "Con trong gio lam bai: nut bi khoa, ke ca khi da co em nop xong"
expect_status "giao cho lop → 200" 200 \
  "$(call POST "/api/exams/$EXAM/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":false}")"
expect_status "mo dap an khi lop con dang trong gio → 409" 409 "$(call POST "/api/exams/$EXAM/release-answers" "$TEACHER")"
expect_status "  noi chua het gio" yes "$(body_has 'Chưa hết giờ')"

call GET /api/my/exams "$STUDENT" > /dev/null
ASSIGNMENT=$(python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((i['assignmentId'] for i in items if i['examId'] == int(sys.argv[2])), ''))" "$BODY_FILE" "$EXAM")

expect_status "hoc sinh bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$STUDENT" "{\"assignmentId\":$ASSIGNMENT}")"
ATTEMPT=$(json_field attemptId)
PQ=$(py "d['questions'][0]['id']")
PA=$(py "d['questions'][0]['answers'][0]['id']")
expect_status "hoc sinh nop bai → 200" 200 \
  "$(call POST "/api/exam-attempts/$ATTEMPT/submit" "$STUDENT" "{\"clientSeq\":1,\"answers\":[{\"questionId\":$PQ,\"answerIds\":[$PA]}]}")"

expect_status "da nop xong nhung van con trong gio: mo dap an → 409" 409 \
  "$(call POST "/api/exams/$EXAM/release-answers" "$TEACHER")"

section "De tat 'hien ket qua': hoc sinh nop xong khong thay diem lan dap an"
expect_status "hoc sinh xem bai cua minh → 200" 200 "$(call GET "/api/results/$ATTEMPT" "$STUDENT")"
expect_status "  khong co diem" absent "$(py "'present' if 'score' in d else 'absent'")"
expect_status "  khong xem duoc dap an" False "$(py "d['canSeeAnswers']")"
expect_status "  khong co correctAnswerIds" absent \
  "$(py "'present' if any('correctAnswerIds' in q for q in d['questions']) else 'absent'")"

section "Chi chu de moi mo duoc"
expect_status "hoc sinh goi mo dap an → 403" 403 "$(call POST "/api/exams/$EXAM/release-answers" "$STUDENT")"
expect_status "quan tri vien goi mo dap an → 403" 403 "$(call POST "/api/exams/$EXAM/release-answers" "$ADMIN")"
expect_status "de khong ton tai → 404" 404 "$(call POST "/api/exams/99999999/release-answers" "$TEACHER")"

# ── Het gio ───────────────────────────────────────────────────────────────────────────────────────
section "Het gio: mo duoc, hoc sinh thay diem va dap an dung"
NOW_EPOCH=$(date -u +%s)
WAIT=$(( CLOSE_EPOCH - NOW_EPOCH + 3 ))
if (( WAIT > 0 )); then
  echo "  cho ${WAIT}s toi het gio cua lop..."
  sleep "$WAIT"
fi

expect_status "mo dap an sau khi het gio → 200" 200 "$(call POST "/api/exams/$EXAM/release-answers" "$TEACHER")"
expect_json "  showResultAfterSubmit = true" showResultAfterSubmit true

expect_status "hoc sinh xem lai bai → 200" 200 "$(call GET "/api/results/$ATTEMPT" "$STUDENT")"
expect_status "  thay diem" 100 "$(py "int(d['score'])")"
expect_status "  xem duoc dap an" True "$(py "d['canSeeAnswers']")"
expect_status "  co correctAnswerIds" present \
  "$(py "'present' if all('correctAnswerIds' in q for q in d['questions']) else 'absent'")"
expect_status "  cau tra loi dung duoc danh dau" True "$(py "d['questions'][0]['isCorrect']")"

expect_status "mo lan nua → 200, khong doi gi" 200 "$(call POST "/api/exams/$EXAM/release-answers" "$TEACHER")"
expect_json "  van mo" showResultAfterSubmit true

summary
