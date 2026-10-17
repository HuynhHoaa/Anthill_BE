#!/usr/bin/env bash
# Tong diem cua de: diem gan theo muc do kho (De / Trung binh / Kho) chu khong phai mot con so cho ca de,
# va 10 la tong diem CHUAN (truoc day la 100, doi sang 10 ngay 15/10/2026) chu khong phai dieu kien bat buoc.
#
# Server nhan moi tong hop le (giao vien chon 56 cau chang han, kho chia dung 10) va bao lai tong that o
# `totalScore`; viec nhac "chua du 10, co chot khong" nam o giao dien. Script nay khoa lai hai dieu:
#   - diem tung muc van duoc kiem (khong am, khong vuot tran cung 100 cua mot cau, toi da 2 chu so thap phan,
#     muc co cau thi > 0). Tran 100 cua mot cau CO Y khac tong chuan: de cu cham tren 100 van sua duoc;
#   - tong khac 10 KHONG bi chan o bat ky duong luu nao (tao, sua, nhan ban, xuat ban), va totalScore dung.
#
# Chay:  bash tests/api-checks/exam-total-score.sh   (API phai dang chay o $HOST)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
TOPIC="TongDiem$STAMP"
QUESTIONS=(); EXAMS=()

cleanup() {
  section "Don du lieu"
  local exams=0 questions=0
  for id in "${EXAMS[@]}"; do
    [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]] && exams=$((exams + 1))
  done
  for id in "${QUESTIONS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && questions=$((questions + 1))
  done
  printf '  da xoa %d/%d de · %d/%d cau hoi\n' "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

# So trong response, in dang %g: server tra 10 khi tao va 10.0 khi doc lai (cot numeric(6,2)), nen so khop
# theo gia tri chu khong theo chuoi.
json_num() {
  python -c "
import json,sys
value = json.load(open(sys.argv[2], encoding='utf-8'))
for key in sys.argv[1].split('.'):
    value = value[key]
print('%g' % float(value))
" "$1" "$BODY_FILE"
}

# Ghi nho de vua tao (response hien tai) de don dep; khong the lam trong create_exam vi no chay trong $( ).
track() { local id; id=$(json_field id); [[ -n "$id" ]] && EXAMS+=("$id"); }

# in 'yes' neu response hien tai co chuoi $1
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }

# ── Ngan hang cau hoi: 3 De, 2 Trung binh, 2 Kho trong mot chu de rieng ─────────────────────────────
section "Dung du lieu nen"
for pair in easy:3 medium:2 hard:2; do
  for i in $(seq 1 "${pair##*:}"); do
    c=$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau ${pair%%:*} $i - $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"${pair%%:*}\",\"defaultPoint\":1,\"answers\":[{\"content\":\"Dung\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")
    [[ "$c" != "200" ]] && fail_fast "tao cau hoi: HTTP $c — $(body)"
    QUESTIONS+=("$(json_field id)")
  done
done
printf '  %d cau hoi chu de %s\n' "${#QUESTIONS[@]}" "$TOPIC"

# $1 tieu de · $2 so cau · $3/$4/$5 ty le De/TB/Kho · $6/$7/$8 diem De/TB/Kho · $9 lifecycle (tuy chon)
exam_body() {
  local life=""
  [[ -n "${9:-}" ]] && life=",\"lifecycle\":\"$9\""
  printf '{"title":"%s","description":"","topic":"%s","durationMinutes":30,"totalQuestions":%s,"easyPercentage":%s,"mediumPercentage":%s,"hardPercentage":%s,"pointEasy":%s,"pointMedium":%s,"pointHard":%s,"shuffleQuestions":false,"shuffleAnswers":false,"showResultAfterSubmit":true,"openAt":"2020-01-01T00:00:00Z","closeAt":"2035-01-01T00:00:00Z"%s}' \
    "$1" "$TOPIC" "$2" "$3" "$4" "$5" "$6" "$7" "$8" "$life"
}

create_exam() { call POST /api/exams "$TEACHER" "$(exam_body "$@")"; }

# ── Dung 10 diem ──────────────────────────────────────────────────────────────────────────────────────
section "Mot muc do: 2 cau De, moi cau 5 diem"
expect_status "de 2 cau De x 5 = 10 → 200" 200 "$(create_exam "De 10 diem $STAMP" 2 100 0 0 5 0 0)"
EXAM_OK=$(json_field id); track
expect_json   "  totalScore = 10" totalScore 10
expect_json   "  pointEasy = 5"   pointEasy 5
expect_json   "  pointMedium = 0 (muc khong co cau)" pointMedium 0

section "Ba muc do: 5 cau 40/40/20 = 2 De + 2 TB + 1 Kho"
expect_status "2 x 1 + 2 x 2 + 1 x 4 = 10 → 200" 200 "$(create_exam "De ba muc $STAMP" 5 40 40 20 1 2 4)"
EXAM_3=$(json_field id); track
expect_json   "  totalScore = 10" totalScore 10
expect_json   "  pointEasy = 1"   pointEasy 1
expect_json   "  pointMedium = 2" pointMedium 2
expect_json   "  pointHard = 4"   pointHard 4

# ── Khong du 10: van luu duoc, server bao dung tong that ─────────────────────────────────────────────
section "Chua dung 10: van luu duoc, totalScore la tong that"
expect_status "de 9,8 diem → 200" 200 "$(create_exam "De thieu $STAMP" 2 100 0 0 4.9 0 0)"
EXAM_98=$(json_field id); track
expect_status "  totalScore = 9.8" 9.8 "$(json_num totalScore)"
expect_json   "  de co hieu luc (khong phai nhap)" lifecycle open
expect_status "de 10,2 diem → 200" 200 "$(create_exam "De thua $STAMP" 2 100 0 0 5.1 0 0)"
track
expect_status "  totalScore = 10.2" 10.2 "$(json_num totalScore)"
expect_status "3 cau De x 3,33 = 9,99 → 200" 200 "$(create_exam "De chia deu $STAMP" 3 100 0 0 3.33 0 0)"
track
expect_status "  totalScore = 9.99 (khong bi lam tron len 10)" 9.99 "$(json_num totalScore)"
expect_status "5 cau 40/40/20 diem 0,3 / 0,4 / 0,5 = 1,9 → 200" 200 "$(create_exam "De it diem $STAMP" 5 40 40 20 0.3 0.4 0.5)"
track
expect_status "  totalScore = 1.9 (2x0,3 + 2x0,4 + 1x0,5)" 1.9 "$(json_num totalScore)"

section "Sua mot de da co hieu luc ve tong khac 10 cung khong bi chan"
expect_status "sua 9,8 → 10 → 200" 200 \
  "$(call PUT "/api/exams/$EXAM_98" "$TEACHER" "$(exam_body "De thieu $STAMP" 2 100 0 0 5 0 0)")"
expect_json   "  totalScore = 10" totalScore 10
expect_status "doi cau hinh, tong ve 7,5 → 200" 200 \
  "$(call PUT "/api/exams/$EXAM_98" "$TEACHER" "$(exam_body "De thieu $STAMP" 3 100 0 0 2.5 0 0)")"
expect_status "  totalScore = 7.5" 7.5 "$(json_num totalScore)"
expect_status "chi doi ten, giu nguyen cau hinh → 200" 200 \
  "$(call PUT "/api/exams/$EXAM_98" "$TEACHER" "$(exam_body "De thieu da doi ten $STAMP" 3 100 0 0 2.5 0 0)")"
expect_status "  totalScore van 7.5" 7.5 "$(json_num totalScore)"

# ── Ban nhap va xuat ban ──────────────────────────────────────────────────────────────────────────────
section "Ban nhap chua du 10 luu duoc, xuat ban bang PUT hay POST /publish deu khong bi chan"
expect_status "nhap 9,8 diem → 200" 200 "$(create_exam "Nhap thieu $STAMP" 2 100 0 0 4.9 0 0 draft)"
DRAFT=$(json_field id); track
expect_json   "  lifecycle = draft" lifecycle draft
expect_status "  totalScore = 9.8" 9.8 "$(json_num totalScore)"
expect_status "xuat ban bang PUT khi con 9,8 → 200" 200 \
  "$(call PUT "/api/exams/$DRAFT" "$TEACHER" "$(exam_body "Nhap thieu $STAMP" 2 100 0 0 4.9 0 0)")"
expect_json   "  het la nhap" lifecycle open
expect_status "  totalScore van 9.8" 9.8 "$(json_num totalScore)"

expect_status "nhap 7,5 diem → 200" 200 "$(create_exam "Nhap xuat ban $STAMP" 3 100 0 0 2.5 0 0 draft)"
DRAFT_PUBLISH=$(json_field id); track
expect_status "POST /publish khi con 7,5 → 200" 200 "$(call POST "/api/exams/$DRAFT_PUBLISH/publish" "$TEACHER")"
expect_json   "  het la nhap" lifecycle open
expect_status "  totalScore van 7.5" 7.5 "$(json_num totalScore)"

# ── Hinh dang tung diem ───────────────────────────────────────────────────────────────────────────────
section "Tung diem: muc co cau thi > 0, khong am, khong vuot tran cung 100 cua mot cau, toi da 2 chu so thap phan"
expect_status "muc De co cau ma diem 0 → 400" 400 "$(create_exam "Diem 0 $STAMP" 2 100 0 0 0 0 0)"
expect_status "  noi diem muc De phai lon hon 0" yes "$(body_has 'phải lớn hơn 0')"
expect_status "diem muc khong co cau am → 400" 400 "$(create_exam "Diem am $STAMP" 2 100 0 0 50 -1 0)"
expect_status "diem mot cau vuot 100 → 400" 400 "$(create_exam "Diem 101 $STAMP" 1 100 0 0 101 0 0)"
expect_status "diem co 3 chu so thap phan → 400" 400 "$(create_exam "Diem le $STAMP" 3 100 0 0 3.333 0 0)"
expect_status "  noi toi da 2 chu so" yes "$(body_has 'tối đa 2 chữ số thập phân')"
expect_status "diem 0 o muc khong co cau van hop le → 200" 200 "$(create_exam "Muc rong $STAMP" 2 100 0 0 4 0 0)"
track

# ── Nhan ban ──────────────────────────────────────────────────────────────────────────────────────────
section "Nhan ban giu nguyen ba diem, ke ca khi tong khac 10"
expect_status "nhan ban de ba muc → 200" 200 "$(call POST "/api/exams/$EXAM_3/duplicate" "$TEACHER")"
track
expect_status "  pointEasy = 1"   1  "$(json_num pointEasy)"
expect_status "  pointMedium = 2" 2  "$(json_num pointMedium)"
expect_status "  pointHard = 4"   4  "$(json_num pointHard)"
expect_status "  totalScore = 10" 10 "$(json_num totalScore)"
expect_status "nhan ban de 7,5 diem → 200" 200 "$(call POST "/api/exams/$DRAFT_PUBLISH/duplicate" "$TEACHER")"
track
expect_status "  totalScore = 7.5" 7.5 "$(json_num totalScore)"

# ── Hoc sinh chi nhan tong diem ───────────────────────────────────────────────────────────────────────
section "Doc lai de: ba diem va tong diem nam trong response cua giao vien"
expect_status "doc de 7,5 diem → 200" 200 "$(call GET "/api/exams/$DRAFT_PUBLISH" "$TEACHER")"
expect_status "  pointEasy = 2.5" 2.5 "$(json_num pointEasy)"
expect_status "  totalScore = 7.5" 7.5 "$(json_num totalScore)"

summary
