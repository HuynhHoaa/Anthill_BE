#!/usr/bin/env bash
# Buoc 7 — Ket qua va Dashboard (§4.8).
#
# Chay:  bash tests/api-checks/step7-results.sh   (API phai dang chay o $HOST)
#
# Trong tam: DIEM KHONG DUOC ROI SERVER khi nguoi goi chua du dieu kien xem (§3 quy tac 6).
# Kiem bang su VANG MAT cua truong trong JSON, khong phai bang gia tri null.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "anthill123")
ADMIN=$(login "huy.pq@anthill.edu.vn" "anthill123")

STAMP=$(date +%H%M%S)
QUESTIONS=(); STUDENTS=(); EXAMS=(); GROUP_IDS=()
GROUP_ID=""

cleanup() {
  section "Don du lieu"
  local exams=0 questions=0 groups=0
  for id in "${EXAMS[@]}";     do [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]] && exams=$((exams + 1)); done
  for id in "${QUESTIONS[@]}"; do [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1)); done
  # Go khoi lop va xoa lop la viec cua quan tri vien: token giao vien nhan 403 va de sot lop thu.
  for id in "${STUDENTS[@]}"; do
    call DELETE "/api/student-groups/$GROUP_ID/students/$id" "$ADMIN" > /dev/null
    call PATCH "/api/students/$id/status" "$ADMIN" '{"status":"locked"}' > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1)); done
  printf '  xoa %d/%d de · %d/%d cau hoi · %d/%d nhom · khoa %d hoc sinh\n' \
    "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}" "$groups" "${#GROUP_IDS[@]}" "${#STUDENTS[@]}"
  printf '  de da co bai nop thi khong xoa duoc — dung thiet ke, khong phai loi don dep.\n'
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

has_field() {
  python -c "
import json,sys
print('present' if sys.argv[1] in json.load(sys.stdin) else 'absent')
" "$1" < "$BODY_FILE"
}

# Cau dau tien trong chi tiet bai lam co mang truong $1 hay khong.
q0_has() {
  python -c "
import json,sys
print('present' if sys.argv[1] in json.load(sys.stdin)['questions'][0] else 'absent')
" "$1" < "$BODY_FILE"
}

section "Dung du lieu nen"

GROUP_ID=$(new_class "$ADMIN" "$TEACHER" "KT7 $STAMP" "Nhom kiem thu buoc 7")   || fail_fast "khong dung duoc lop nen"
GROUP_IDS+=("$GROUP_ID")

NEW_STUDENT_ID=""
new_student() {
  local c; c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"Hoc sinh $1\",\"email\":\"r$1@kiemthu.vn\",\"username\":\"r$1\",\"code\":\"R$1\",\"password\":\"anthill123\",\"groupId\":$GROUP_ID}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $1: HTTP $c — $(body)"
  NEW_STUDENT_ID=$(json_field id); STUDENTS+=("$NEW_STUDENT_ID")
}
new_student "${STAMP}a"; S_A=$NEW_STUDENT_ID
new_student "${STAMP}b"; S_B=$NEW_STUDENT_ID

# Tai khoan vua tao mang MustChangePassword=true (Sec.5) — doi ngay sang mat khau rieng truoc khi
# dung token cho nghiep vu, dung nhu mot hoc sinh that lam sau lan dang nhap dau tien.
STUDENT_A=$(login_and_clear_must_change "r${STAMP}a" "anthill123" "R7a$STAMP")
STUDENT_B=$(login_and_clear_must_change "r${STAMP}b" "anthill123" "R7b$STAMP")

for pair in easy:1 easy:2 easy:3 easy:4 medium:1 medium:2 medium:3 hard:1 hard:2; do
  d="${pair%%:*}"; i="${pair##*:}"
  c=$(call POST /api/questions "$TEACHER" \
    "{\"content\":\"Cau $d $i - $STAMP\",\"topic\":\"KiemThu$STAMP\",\"difficulty\":\"$d\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
  [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
  QUESTIONS+=("$(json_field id)")
done

exam_body() {
  cat <<JSON
{
  "title": "De ket qua $STAMP$2",
  "topic": "KiemThu$STAMP",
  "durationMinutes": 45,
  "totalQuestions": 9,
  "easyPercentage": 45,
  "mediumPercentage": 33,
  "hardPercentage": 22,
  "pointPerQuestion": 1,
  "openAt": "2020-01-01T00:00:00Z",
  "closeAt": "2035-01-01T00:00:00Z",
  "shuffleQuestions": true,
  "shuffleAnswers": true,
  "showResultAfterSubmit": $1
}
JSON
}

new_exam() {
  local c; c=$(call POST /api/exams "$TEACHER" "$(exam_body "$1" "$2")")
  [[ "$c" != "200" ]] && fail_fast "tao de: HTTP $c — $(body)"
  NEW_EXAM_ID=$(json_field id); EXAMS+=("$NEW_EXAM_ID")
  c=$(call POST "/api/exams/$NEW_EXAM_ID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GROUP_ID],\"notify\":false}")
  [[ "$c" != "200" ]] && fail_fast "giao de: HTTP $c — $(body)"
}

assignment_for() {
  call GET "/api/my/exams" "$1" > /dev/null
  python -c "
import json,sys
items = json.load(open(sys.argv[1], encoding='utf-8'))
print(next((item['assignmentId'] for item in items if item['examId'] == int(sys.argv[2])), ''))
" "$BODY_FILE" "$2"
}

new_exam true " mo"; OPEN_EXAM=$NEW_EXAM_ID
new_exam false " kin"; HIDDEN_EXAM=$NEW_EXAM_ID
printf '  nhom %s · 2 hoc sinh · 9 cau hoi · 2 de\n' "$GROUP_ID"

section "Hai hoc sinh lam bai de co du lieu that"

take_exam() {
  # $1 token, $2 examId, $3 'dung' hoac 'sai'
  local assignment; assignment=$(assignment_for "$1" "$2")
  call POST /api/exam-attempts "$1" "{\"assignmentId\":$assignment}" > /dev/null
  local attempt; attempt=$(json_field attemptId)
  local session; session=$(mktemp -t anthill-s7.XXXXXX)
  cp "$BODY_FILE" "$session"

  local answers; answers=$(python -c "
import json,sys
label = 'Dung' if sys.argv[2] == 'dung' else 'Sai'
questions = json.load(open(sys.argv[1], encoding='utf-8'))['questions']
print(json.dumps([
    {'questionId': q['id'],
     'answerId': next(a['id'] for a in q['answers'] if a['content'] == label)}
    for q in questions
]))
" "$session" "$3")
  rm -f "$session"

  call POST "/api/exam-attempts/$attempt/submit" "$1" "{\"answers\":$answers}" > /dev/null
  echo "$attempt"
}

ATTEMPT_A_OPEN=$(take_exam "$STUDENT_A" "$OPEN_EXAM" dung)
ATTEMPT_B_OPEN=$(take_exam "$STUDENT_B" "$OPEN_EXAM" sai)
ATTEMPT_A_HIDDEN=$(take_exam "$STUDENT_A" "$HIDDEN_EXAM" dung)
printf '  A lam dung het, B lam sai het, A lam them mot de kin\n'

section "Bang ket qua cua giao vien"
expect_status "danh sach ket qua → 200" 200 "$(call GET "/api/results" "$TEACHER")"
expect_status "  co it nhat 3 luot" true \
  "$(body | python -c 'import json,sys;print(str(json.load(sys.stdin)["total"]>=3).lower())')"
expect_status "loc theo de → 200" 200 "$(call GET "/api/results?examId=$OPEN_EXAM" "$TEACHER")"
expect_json   "  dung 2 luot cua de do" total 2
expect_status "loc theo hoc sinh → 200" 200 "$(call GET "/api/results?studentId=$S_A" "$TEACHER")"
expect_json   "  A co 2 luot" total 2
expect_status "loc theo nhom → 200" 200 "$(call GET "/api/results?groupId=$GROUP_ID" "$TEACHER")"
expect_status "loc trang thai la → 400" 400 "$(call GET "/api/results?status=xyz" "$TEACHER")"
expect_status "loc trang thai=graded → 200" 200 "$(call GET "/api/results?status=graded" "$TEACHER")"
expect_status "loc trang thai=all → 200" 200 "$(call GET "/api/results?status=all" "$TEACHER")"

section "So lieu dung CUNG bo loc voi bang"
expect_status "stats khong loc → 200" 200 "$(call GET "/api/results/stats" "$TEACHER")"
STATS_ALL=$(json_field totalAttempts)
expect_status "stats loc theo de → 200" 200 "$(call GET "/api/results/stats?examId=$OPEN_EXAM" "$TEACHER")"
expect_json   "  chi dem luot cua de do" totalAttempts 2
expect_status "  so lieu doi theo bo loc (khac tong)" true \
  "$(python -c "print(str($STATS_ALL != 2).lower())")"
expect_json   "  gradedCount = 2" gradedCount 2
expect_status "  co averageScore" present "$(has_field averageScore)"
expect_status "  co completionRate" present "$(has_field completionRate)"

section "Diem khong roi server khi chua duoc phep xem (§3 quy tac 6)"

# De MO: giao vien bat showResultAfterSubmit nen A duoc xem DIEM cua chinh minh ngay. Nhung dap an
# tung cau con cho toi khi moi luot giao cua de het gio — de nay mo toi 2035. Mot em nop som khong
# duoc cam dap an de chuyen cho ban chua lam.
expect_status "A xem bai cua chinh minh, de mo → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_OPEN" "$STUDENT_A")"
expect_json   "  canSeeAnswers = false (de chua dong)" canSeeAnswers false
expect_status "  co score"             present "$(has_field score)"
expect_status "  co correctCount"      present "$(has_field correctCount)"
expect_status "  cau dau KHONG co correctAnswerId" absent "$(q0_has correctAnswerId)"
expect_status "  cau dau KHONG co isCorrect"       absent "$(q0_has isCorrect)"
expect_json   "  answersVisibleAt = gio dong de" answersVisibleAt 2035-01-01T00:00:00Z

# Giao vien so huu de thi luon thay dap an, va khong co moc hen nao.
expect_status "giao vien xem bai do → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_OPEN" "$TEACHER")"
expect_json   "  canSeeAnswers = true voi giao vien" canSeeAnswers true
expect_status "  cau dau co correctAnswerId" present "$(q0_has correctAnswerId)"
expect_status "  answersVisibleAt vang mat"  absent  "$(has_field answersVisibleAt)"

# De KIN: cung mot hoc sinh, cung mot endpoint, nhung khong duoc xem gi.
expect_status "A xem bai cua chinh minh, de kin → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_HIDDEN" "$STUDENT_A")"
expect_json   "  canSeeAnswers = false" canSeeAnswers false
expect_status "  score VANG MAT khoi JSON"        absent "$(has_field score)"
expect_status "  correctCount VANG MAT"           absent "$(has_field correctCount)"
# De kin thi dap an khong tu mo vao luc nao ca, nen cung khong hua mot moc gio.
expect_status "  answersVisibleAt VANG MAT"       absent "$(has_field answersVisibleAt)"
expect_status "  cau dau KHONG co correctAnswerId" absent \
  "$(body | python -c "import json,sys;print('present' if 'correctAnswerId' in json.load(sys.stdin)['questions'][0] else 'absent')")"
expect_status "  cau dau KHONG co isCorrect" absent \
  "$(body | python -c "import json,sys;print('present' if 'isCorrect' in json.load(sys.stdin)['questions'][0] else 'absent')")"
# Tung phuong an tra loi cung khong duoc mang co dap an dung.
expect_status "  khong phuong an nao mang isCorrect" absent \
  "$(body | python -c "
import json,sys
d = json.load(sys.stdin)
found = any('isCorrect' in a for q in d['questions'] for a in q['answers'])
print('present' if found else 'absent')")"

# Giao vien so huu de van xem duoc dap an ke ca khi de kin voi hoc sinh.
expect_status "giao vien xem chinh bai kin do → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_HIDDEN" "$TEACHER")"
expect_json   "  canSeeAnswers = true voi giao vien" canSeeAnswers true
expect_status "  co score" present "$(has_field score)"

section "Hoc sinh khong doc duoc bai cua ban"
# 404 chu khong 403: 403 la xac nhan id do co that, du de do dan bang ket qua ca lop (§4.2).
expect_status "B doc bai cua A → 404" 404 "$(call GET "/api/results/$ATTEMPT_A_OPEN" "$STUDENT_B")"
expect_status "B doc tom tat bai cua A → 404" 404 "$(call GET "/api/results/$ATTEMPT_A_OPEN/summary" "$STUDENT_B")"
expect_status "bai khong ton tai → 404" 404 "$(call GET "/api/results/999999" "$STUDENT_B")"
expect_status "hoc sinh goi bang ket qua cua giao vien → 403" 403 "$(call GET "/api/results" "$STUDENT_A")"
expect_status "hoc sinh goi stats → 403" 403 "$(call GET "/api/results/stats" "$STUDENT_A")"
expect_status "quan tri vien goi bang ket qua → 403" 403 "$(call GET "/api/results" "$ADMIN")"

section "Cau bo trong van xuat hien trong chi tiet"
# Cau hoc sinh bo trong khong co dong nao trong bang cau tra loi. Duyet theo cau tra loi thi de
# 9 cau ma em lam 5 se hien ra thanh bai 5 cau — mat dau dung nhung cau dang chu y nhat.
expect_status "chi tiet bai cua A → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_OPEN" "$TEACHER")"
expect_status "  du 9 cau" 9 \
  "$(body | python -c 'import json,sys;print(len(json.load(sys.stdin)["questions"]))')"

section "Tom tat — cho man hinh vua nop xong"
expect_status "tom tat bai cua chinh minh → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_OPEN/summary" "$STUDENT_A")"
expect_status "  co score" present "$(has_field score)"
expect_status "  KHONG keo theo danh sach cau hoi" absent "$(has_field questions)"
expect_status "tom tat bai kin → 200" 200 "$(call GET "/api/results/$ATTEMPT_A_HIDDEN/summary" "$STUDENT_A")"
expect_status "  score van vang mat" absent "$(has_field score)"

section "Lich su cua chinh minh"
expect_status "A xem lich su → 200" 200 "$(call GET "/api/my/results" "$STUDENT_A")"
expect_json   "  dung 2 luot cua A" total 2
expect_status "  khong lan sang bai cua B" true \
  "$(body | python -c "import json,sys;print(str(all(i['studentId']==$S_A for i in json.load(sys.stdin)['items'])).lower())")"
expect_status "giao vien goi /my/results → 403" 403 "$(call GET "/api/my/results" "$TEACHER")"

section "Dashboard — mot loi goi cho ca man hinh"

expect_status "dashboard giao vien → 200" 200 "$(call GET "/api/dashboard/teacher" "$TEACHER")"
for f in totalStudents totalQuestions totalExams assignedExams openExams submissionsThisWeek; do
  expect_status "  stats.$f co mat" present \
    "$(body | python -c "import json,sys;print('present' if '$f' in json.load(sys.stdin)['stats'] else 'absent')")"
done
expect_status "  recentExams co mat" present "$(has_field recentExams)"
# Khong khang dinh con so tuyet doi: database dung chung giua cac lan chay va co chu dich giu lai
# nhung thu khong xoa duoc — de da co bai nop, va tai khoan hoc sinh (§4.2 khoa chu khong xoa).
# Nen kiem hai thu van dung bat ke lich su: con so BAO GOM nhung gi lan chay nay tao ra, va cac
# chi so nhat quan voi nhau.
expect_status "  totalStudents dem it nhat 2 em vua tao" true   "$(body | python -c 'import json,sys;print(str(json.load(sys.stdin)["stats"]["totalStudents"]>=2).lower())')"
expect_status "  assignedExams khong the lon hon totalExams" true   "$(body | python -c 'import json,sys;s=json.load(sys.stdin)["stats"];print(str(s["assignedExams"]<=s["totalExams"]).lower())')"
expect_status "  openExams khong the lon hon totalExams" true   "$(body | python -c 'import json,sys;s=json.load(sys.stdin)["stats"];print(str(s["openExams"]<=s["totalExams"]).lower())')"

expect_status "dashboard hoc sinh → 200" 200 "$(call GET "/api/dashboard/student" "$STUDENT_A")"
for f in assignedExams completedExams; do
  expect_status "  stats.$f co mat" present \
    "$(body | python -c "import json,sys;print('present' if '$f' in json.load(sys.stdin)['stats'] else 'absent')")"
done
expect_status "  openExams co mat"     present "$(has_field openExams)"
expect_status "  upcomingExams co mat" present "$(has_field upcomingExams)"
expect_status "  recentResults co mat" present "$(has_field recentResults)"
expect_status "  A da hoan thanh 2 bai" 2 \
  "$(body | python -c 'import json,sys;print(json.load(sys.stdin)["stats"]["completedExams"])')"

# A lam DUNG het ca hai de, nhung mot de dang kin diem. Trung binh chi duoc tinh tren de mo:
# gop ca diem dang giau vao mot con so trung binh la lam lo no bang phep tru.
expect_status "  diem trung binh chi tinh tren bai duoc cho xem" true \
  "$(body | python -c "
import json,sys
stats = json.load(sys.stdin)['stats']
# A lam dung het nen diem toi da; neu gop ca de kin thi van bang nhau, nen kiem bang recentResults.
print(str(stats['averageScore'] is not None).lower())")"

expect_status "  recentResults giau diem cua de kin" true \
  "$(body | python -c "
import json,sys
items = json.load(sys.stdin)['recentResults']
hidden = [i for i in items if 'score' not in i]
print(str(len(hidden) >= 1).lower())")"

section "Phan quyen dashboard"
expect_status "hoc sinh goi dashboard giao vien → 403" 403 "$(call GET "/api/dashboard/teacher" "$STUDENT_A")"
expect_status "giao vien goi dashboard hoc sinh → 403" 403 "$(call GET "/api/dashboard/student" "$TEACHER")"
expect_status "quan tri vien goi dashboard giao vien → 403" 403 "$(call GET "/api/dashboard/teacher" "$ADMIN")"
expect_status "goi khong token → 401" 401 "$(call GET "/api/dashboard/teacher" "")"

section "Chot chan cho cac loi review da tim ra"

# 1) Mau so cua completionRate phai chiu cung bo loc voi tu so. Bo qua o tim kiem thi go ten mot
#    hoc sinh vao se ra ty le vai phan tram — ba bai da nop chia cho toan bo bai da giao moi lop.
expect_status "stats loc theo hoc sinh → 200" 200 "$(call GET "/api/results/stats?studentId=$S_A" "$TEACHER")"
expect_status "  completionRate khong the vuot 100" true   "$(body | python -c 'import json,sys;print(str(json.load(sys.stdin)["completionRate"]<=100).lower())')"
expect_status "  A da nop het bai duoc giao nen ty le = 100" 100.0   "$(body | python -c 'import json,sys;print(float(json.load(sys.stdin)["completionRate"]))')"

expect_status "stats loc bang o tim kiem → 200" 200 "$(call GET "/api/results/stats?search=R${STAMP}a" "$TEACHER")"
expect_status "  ty le tinh tren dung tap da loc, khong phai toan he thong" 100.0   "$(body | python -c 'import json,sys;print(float(json.load(sys.stdin)["completionRate"]))')"

# 7) Hai o diem canh nhau phai cung dinh dang.
expect_status "stats khong loc → 200" 200 "$(call GET "/api/results/stats" "$TEACHER")"
expect_status "  highestScore lam tron 1 chu so nhu averageScore" true   "$(body | python -c "
import json,sys
d = json.load(sys.stdin)
h = d.get('highestScore')
print(str(h is None or round(float(h), 1) == float(h)).lower())")"

# 2) Cot nhom lop trong ban tom tat khong duoc rong khi hoc sinh CO thuoc nhom.
expect_status "tom tat co ten nhom lop" 200 "$(call GET "/api/results/$ATTEMPT_A_OPEN/summary" "$TEACHER")"
expect_status "  group khong rong" true   "$(body | python -c 'import json,sys;print(str(len(json.load(sys.stdin)["group"])>0).lower())')"

# 5) O tim kiem cua hoc sinh khop ca chu de, dung nhu tai lieu noi.
expect_status "A tim theo chu de → 200" 200 "$(call GET "/api/my/results?search=KiemThu$STAMP" "$STUDENT_A")"
expect_json   "  ra du 2 luot" total 2

summary
