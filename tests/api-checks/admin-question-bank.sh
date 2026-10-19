#!/usr/bin/env bash
# Ngan hang cau hoi cho QUAN TRI VIEN: doc moi mon, an/hien, xoa, tao moi (kem chon mon).
#
# Khoa lai nhung dieu ma unit test khong cham toi duoc vi can HTTP + database that:
#   - GET /api/questions/subjects: chi quan tri vien; giao vien, hoc sinh va khach bi tu choi;
#   - cau hoi tra ve ten mon (subjectId, subject) o ca danh sach lan chi tiet;
#   - quan tri vien doc ca cau RIENG TU cua giao vien, ke ca khi thu hep ve mot mon (truoc day thu hep ve mot
#     mon lam mat cac cau rieng tu, trong khi khong thu hep thi hien du; ba endpoint danh sach/thong ke/chu de);
#   - quan tri vien tao cau phai chon mon (thieu hoac mon khong co that → 400), cau mac dinh la cau chung;
#   - sua cau khong doi mon va khong doi tac gia; an/hien duoc; xoa duoc cau chua dung, cau dang nam trong mot de → 409;
#   - giao vien KHONG sua/an/xoa duoc cau cua quan tri vien (403), va khong thay cau rieng tu cua nguoi khac.
#
# Cac phep co "mon thu hai" va "giao vien thu hai" can ghi thang vao database bang psql. psql CHI chay khi nguoi chay tu
# dat PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD (script KHONG tu doc user-secrets, de khong the ghi nham vao database that)
# va PGDATABASE khac `anthill`. Thieu thi cac phep do bao hong chu khong im lang bo qua.
#
# Chay:  bash tests/api-checks/admin-question-bank.sh   (API phai dang chay o $HOST, da migrate)

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

STAMP=$(date +%H%M%S)
TOPIC="AdminNH$STAMP"
QUESTIONS=(); EXAMS=()
SUBJECT_B=""; FOREIGN_ID=""

# ── psql: chi khi nguoi chay chu dong tro vao database thu ───────────────────────────────────────
PSQL=$(command -v psql 2>/dev/null || ls /c/Program\ Files/PostgreSQL/*/bin/psql.exe 2>/dev/null | tail -n 1)
HAS_PSQL=no
if [[ -n "$PSQL" && -n "${PGPASSWORD:-}" && -n "${PGDATABASE:-}" ]]; then
  if [[ "$PGDATABASE" == "anthill" ]]; then
    echo "  Tu choi chay: PGDATABASE=anthill la database that. Tro vao database thu (vd anthill_score100)." >&2
    exit 1
  fi
  HAS_PSQL=yes
  printf '  (psql -> %s@%s)\n' "$PGDATABASE" "${PGHOST:-<mac dinh>}"
fi
sql() { "$PSQL" -q -At -v ON_ERROR_STOP=1 -c "$1" | tr -d '\r'; }

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }
# Doc mot bieu thuc python tren response hien tai. $1 la bieu thuc theo bien `d` (JSON da parse).
py() { python -c "import json,sys; d=json.load(open(sys.argv[2], encoding='utf-8')); print($1)" "" "$BODY_FILE"; }
# 'yes' neu danh sach (phan trang) hien tai co cau hoi mang id $1.
has_id() { py "'yes' if any(i['id']==$1 for i in d['items']) else 'no'"; }

# $1 noi dung · $2 chu de · $3 phan JSON them (vd ,"visibility":"private")
qjson() {
  printf '{"content":"%s","topic":"%s","difficulty":"easy","defaultPoint":1%s,"answers":[{"content":"Dung","isCorrect":true},{"content":"Sai","isCorrect":false}]}' \
    "$1" "$2" "${3:-}"
}

cleanup() {
  section "Don du lieu"
  local exams=0 questions=0
  for id in "${EXAMS[@]}"; do
    [[ "$(call DELETE "/api/exams/$id" "$TEACHER")" == "200" ]] && exams=$((exams + 1))
  done
  for id in "${QUESTIONS[@]}"; do
    # 404 cung tinh la da don: cau da xoa ngay trong bo kiem khong con de xoa lan nua.
    case "$(call DELETE "/api/questions/$id" "$ADMIN")" in 200|404) questions=$((questions + 1)) ;; esac
  done
  # Mon thu hai va giao vien thu hai khong con cau hoi, de hay lop nao nen khong khoa ngoai nao giu dong cua chung.
  [[ -n "$SUBJECT_B" ]] && { sql "DELETE FROM \"Subjects\" WHERE \"Id\" = $SUBJECT_B;" > /dev/null 2>&1 || printf '  khong xoa duoc mon kiem thu %s\n' "$SUBJECT_B"; }
  [[ -n "$FOREIGN_ID" ]] && { sql "DELETE FROM \"Users\" WHERE \"Id\" = $FOREIGN_ID;" > /dev/null 2>&1 || printf '  khong xoa duoc giao vien kiem thu %s\n' "$FOREIGN_ID"; }
  printf '  da xoa %d/%d de · %d/%d cau hoi\n' "$exams" "${#EXAMS[@]}" "$questions" "${#QUESTIONS[@]}"
}
trap cleanup EXIT

# ── Du lieu nen ──────────────────────────────────────────────────────────────────────────────────
section "Du lieu nen: giao vien tao mot cau chung va mot cau rieng tu"
c=$(call POST /api/questions "$TEACHER" "$(qjson "Cau chung $STAMP" "$TOPIC")"); [[ "$c" != "200" ]] && fail_fast "tao cau chung: HTTP $c — $(body)"
Q_PUB=$(json_field id); SUBJECT_A=$(json_field subjectId); QUESTIONS+=("$Q_PUB")
c=$(call POST /api/questions "$TEACHER" "$(qjson "Cau rieng $STAMP" "$TOPIC" ',"visibility":"private"')"); [[ "$c" != "200" ]] && fail_fast "tao cau rieng: HTTP $c — $(body)"
Q_PRIV=$(json_field id); QUESTIONS+=("$Q_PRIV")
call GET /api/auth/me "$TEACHER" > /dev/null; TEACHER_ID=$(json_field id)
call GET /api/auth/me "$ADMIN" > /dev/null; ADMIN_ID=$(json_field id)
printf '  cau chung #%s, cau rieng #%s, mon #%s\n' "$Q_PUB" "$Q_PRIV" "$SUBJECT_A"

# ── Danh sach mon ────────────────────────────────────────────────────────────────────────────────
section "GET /api/questions/subjects"
expect_status "quan tri vien → 200" 200 "$(call GET /api/questions/subjects "$ADMIN")"
expect_status "  co mon cua giao vien" yes "$(py "'yes' if any(s['id']==$SUBJECT_A for s in d) else 'no'")"
expect_status "  moi mon co id, ten va ma" yes "$(py "'yes' if all(s['id'] and s['name'] and s['code'] for s in d) else 'no'")"
expect_status "giao vien → 403" 403 "$(call GET /api/questions/subjects "$TEACHER")"
expect_status "hoc sinh → 403" 403 "$(call GET /api/questions/subjects "$STUDENT")"
expect_status "khach → 401" 401 "$(call GET /api/questions/subjects)"

# ── Cau hoi mang ten mon ─────────────────────────────────────────────────────────────────────────
section "Cau hoi tra ve ten mon"
expect_status "chi tiet → 200" 200 "$(call GET "/api/questions/$Q_PUB" "$TEACHER")"
expect_json "  subjectId" subjectId "$SUBJECT_A"
expect_status "  co ten mon" yes "$(py "'yes' if d['subject'] else 'no'")"
SUBJECT_A_NAME=$(json_field subject)
expect_status "danh sach cua giao vien → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50" "$TEACHER")"
expect_status "  moi cau trong danh sach co ten mon" yes "$(py "'yes' if all(i['subject'] == '$SUBJECT_A_NAME' for i in d['items']) else 'no'")"

# ── Quan tri vien doc ca cau rieng tu ────────────────────────────────────────────────────────────
section "Quan tri vien doc ca cau rieng tu cua giao vien — co hay khong thu hep theo mon"
expect_status "khong thu hep → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50" "$ADMIN")"
expect_json "  thay ca hai cau" total 2
expect_status "  co cau rieng tu" yes "$(has_id "$Q_PRIV")"
expect_status "thu hep ve mon cua giao vien → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50&subjectId=$SUBJECT_A" "$ADMIN")"
expect_json "  van thay ca hai cau" total 2
expect_status "  van co cau rieng tu" yes "$(has_id "$Q_PRIV")"
expect_status "thu hep ve mon khong co that → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&subjectId=999999" "$ADMIN")"
expect_json "  khong co cau nao" total 0
expect_status "chu de khi thu hep theo mon → 200" 200 "$(call GET "/api/questions/topics?subjectId=$SUBJECT_A" "$ADMIN")"
expect_status "  co chu de cua cau rieng tu" yes "$(py "'yes' if '$TOPIC' in d else 'no'")"
expect_status "thong ke khi thu hep theo mon → 200" 200 "$(call GET "/api/questions/summary?subjectId=$SUBJECT_A" "$ADMIN")"
expect_status "  co chu de cua cau rieng tu" yes "$(py "'yes' if '$TOPIC' in d['topics'] else 'no'")"
expect_status "  tong >= 2" yes "$(py "'yes' if d['total'] >= 2 else 'no'")"

# ── Giao vien khac cung mon ──────────────────────────────────────────────────────────────────────
section "Giao vien khac cung mon: thay cau chung, KHONG thay cau rieng tu"
if [[ "$HAS_PSQL" != yes ]]; then
  expect_status "dung giao vien thu hai (can psql va PG* tro vao database thu)" ok "thieu"
else
  FOREIGN_EMAIL="gvnh$STAMP@kiemthu.vn"
  FOREIGN_ID=$(sql "INSERT INTO \"Users\" (\"FullName\",\"Email\",\"Username\",\"PasswordHash\",\"SecurityStamp\",\"Role\",\"Status\",\"SubjectId\",\"AvatarColor\",\"MustChangePassword\",\"CreatedAt\")
    SELECT 'GV kiem thu $STAMP', '$FOREIGN_EMAIL', '$FOREIGN_EMAIL', \"PasswordHash\", md5(random()::text), \"Role\", \"Status\", \"SubjectId\", \"AvatarColor\", false, now()
    FROM \"Users\" WHERE \"Email\" = 'an.nv@anthill.edu.vn' RETURNING \"Id\";")
  if [[ -z "$FOREIGN_ID" ]]; then
    expect_status "dung giao vien thu hai qua psql" ok "hong"
  else
    FOREIGN=$(login "$FOREIGN_EMAIL" "123456")
    expect_status "giao vien khac liet ke → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50" "$FOREIGN")"
    expect_json "  chi thay cau chung" total 1
    expect_status "  khong co cau rieng tu" no "$(has_id "$Q_PRIV")"
    expect_status "doc thang cau rieng tu → 404" 404 "$(call GET "/api/questions/$Q_PRIV" "$FOREIGN")"
    expect_status "doc cau chung → 200" 200 "$(call GET "/api/questions/$Q_PUB" "$FOREIGN")"
    expect_json "  khong sua duoc (canEdit = false)" canEdit false
    expect_status "sua cau chung cua dong nghiep → 403" 403 "$(call PUT "/api/questions/$Q_PUB" "$FOREIGN" "$(qjson "Sua trom $STAMP" "$TOPIC")")"
  fi
fi

# ── Quan tri vien tao cau hoi ────────────────────────────────────────────────────────────────────
section "Quan tri vien tao cau hoi"
expect_status "thieu subjectId → 400" 400 "$(call POST /api/questions "$ADMIN" "$(qjson "Cau admin $STAMP" "$TOPIC")")"
expect_status "  noi phai chon mon" yes "$(body_has 'subjectId')"
expect_status "mon khong ton tai → 400" 400 "$(call POST /api/questions "$ADMIN" "$(qjson "Cau admin $STAMP" "$TOPIC" ',"subjectId":999999')")"
expect_status "  noi mon khong ton tai" yes "$(body_has 'không tồn tại')"
expect_status "chon mon hop le → 200" 200 "$(call POST /api/questions "$ADMIN" "$(qjson "Cau admin $STAMP" "$TOPIC" ",\"subjectId\":$SUBJECT_A")")"
Q_ADMIN=$(json_field id); QUESTIONS+=("$Q_ADMIN")
expect_json "  dung mon da chon" subjectId "$SUBJECT_A"
expect_json "  tac gia la quan tri vien" createdByUserId "$ADMIN_ID"
expect_json "  mac dinh la cau chung" visibility shared
expect_json "  quan tri vien sua duoc" canEdit true

section "Giao vien voi cau cua quan tri vien: thay duoc, khong sua duoc"
expect_status "doc cau chung cua quan tri vien → 200" 200 "$(call GET "/api/questions/$Q_ADMIN" "$TEACHER")"
expect_json "  canEdit = false" canEdit false
expect_status "sua → 403" 403 "$(call PUT "/api/questions/$Q_ADMIN" "$TEACHER" "$(qjson "Sua $STAMP" "$TOPIC")")"
expect_status "an → 403" 403 "$(call PATCH "/api/questions/$Q_ADMIN/status" "$TEACHER" '{"status":"hidden"}')"
expect_status "xoa → 403" 403 "$(call DELETE "/api/questions/$Q_ADMIN" "$TEACHER")"
expect_status "cau chung xuat hien trong danh sach cua giao vien" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50" "$TEACHER")"
expect_status "  co cau cua quan tri vien" yes "$(has_id "$Q_ADMIN")"

expect_status "quan tri vien tao cau rieng tu → 200" 200 \
  "$(call POST /api/questions "$ADMIN" "$(qjson "Cau admin rieng $STAMP" "$TOPIC" ",\"subjectId\":$SUBJECT_A,\"visibility\":\"private\"")")"
Q_ADMIN_PRIV=$(json_field id); QUESTIONS+=("$Q_ADMIN_PRIV")
expect_status "giao vien doc cau rieng tu cua quan tri vien → 404" 404 "$(call GET "/api/questions/$Q_ADMIN_PRIV" "$TEACHER")"
expect_status "quan tri vien van thay no → 200" 200 "$(call GET "/api/questions/$Q_ADMIN_PRIV" "$ADMIN")"

# ── Sua, an, hien cau cua giao vien ──────────────────────────────────────────────────────────────
section "Quan tri vien sua, an, hien cau cua giao vien"
expect_status "sua noi dung → 200" 200 "$(call PUT "/api/questions/$Q_PUB" "$ADMIN" "$(qjson "Cau chung da sua $STAMP" "$TOPIC")")"
expect_json "  noi dung moi" content "Cau chung da sua $STAMP"
expect_json "  van la cau cua giao vien (khong cuop tac gia)" createdByUserId "$TEACHER_ID"
expect_json "  van thuoc mon cu" subjectId "$SUBJECT_A"
expect_status "an → 200" 200 "$(call PATCH "/api/questions/$Q_PUB/status" "$ADMIN" '{"status":"hidden"}')"
expect_json "  trang thai = hidden" status hidden
expect_status "quan tri vien loc cau dang an → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&status=hidden&pageSize=50" "$ADMIN")"
expect_status "  co cau vua an" yes "$(has_id "$Q_PUB")"
expect_status "hien lai → 200" 200 "$(call PATCH "/api/questions/$Q_PUB/status" "$ADMIN" '{"status":"active"}')"
expect_json "  trang thai = active" status active
expect_status "giao vien van sua duoc cau cua minh → 200" 200 "$(call PUT "/api/questions/$Q_PUB" "$TEACHER" "$(qjson "Cau chung $STAMP" "$TOPIC")")"

# ── Xoa ──────────────────────────────────────────────────────────────────────────────────────────
section "Quan tri vien xoa cau hoi"
c=$(call POST /api/questions "$TEACHER" "$(qjson "Cau se xoa $STAMP" "$TOPIC")"); [[ "$c" != "200" ]] && fail_fast "tao cau se xoa: HTTP $c — $(body)"
Q_DEL=$(json_field id); QUESTIONS+=("$Q_DEL")
expect_status "xoa cau chua dung o dau → 200" 200 "$(call DELETE "/api/questions/$Q_DEL" "$ADMIN")"
expect_status "  doc lai → 404" 404 "$(call GET "/api/questions/$Q_DEL" "$ADMIN")"
expect_status "xoa cau khong ton tai → 404" 404 "$(call DELETE "/api/questions/$Q_DEL" "$ADMIN")"

exam_body() {
  printf '{"title":"De dung cau %s","description":"","durationMinutes":30,"shuffleQuestions":false,"shuffleAnswers":false,"showResultAfterSubmit":true,"openAt":"2020-01-01T00:00:00Z","closeAt":"2035-01-01T00:00:00Z","lifecycle":"draft","questions":[{"questionId":%s,"point":100}]}' \
    "$STAMP" "$1"
}
c=$(call POST /api/exams "$TEACHER" "$(exam_body "$Q_PUB")"); [[ "$c" != "200" ]] && fail_fast "tao de dung cau: HTTP $c — $(body)"
EXAMS+=("$(json_field id)")
expect_status "xoa cau dang nam trong mot de → 409" 409 "$(call DELETE "/api/questions/$Q_PUB" "$ADMIN")"
expect_status "  loi goi y an cau hoi thay vi xoa" yes "$(body_has 'Hãy ẩn câu hỏi')"
expect_status "  cau van con nguyen" 200 "$(call GET "/api/questions/$Q_PUB" "$ADMIN")"
expect_status "van an duoc cau dang nam trong de → 200" 200 "$(call PATCH "/api/questions/$Q_PUB/status" "$ADMIN" '{"status":"hidden"}')"
call PATCH "/api/questions/$Q_PUB/status" "$ADMIN" '{"status":"active"}' > /dev/null

# ── Mon thu hai ──────────────────────────────────────────────────────────────────────────────────
section "Hai mon: loc va ranh gioi"
if [[ "$HAS_PSQL" != yes ]]; then
  expect_status "dung mon thu hai (can psql va PG* tro vao database thu)" ok "thieu"
else
  SUBJECT_B=$(sql "INSERT INTO \"Subjects\" (\"Name\",\"Code\",\"CreatedAt\") VALUES ('Mon kiem thu $STAMP','KT$STAMP', now()) RETURNING \"Id\";")
  if [[ -z "$SUBJECT_B" ]]; then
    expect_status "dung mon thu hai qua psql" ok "hong"
  else
    expect_status "danh sach mon co mon moi → 200" 200 "$(call GET /api/questions/subjects "$ADMIN")"
    expect_status "  co mon thu hai" yes "$(py "'yes' if any(s['id']==$SUBJECT_B for s in d) else 'no'")"
    expect_status "tao cau o mon thu hai → 200" 200 "$(call POST /api/questions "$ADMIN" "$(qjson "Cau mon hai $STAMP" "$TOPIC" ",\"subjectId\":$SUBJECT_B")")"
    Q_B=$(json_field id); QUESTIONS+=("$Q_B")
    expect_json "  mang ten mon thu hai" subject "Mon kiem thu $STAMP"
    expect_status "khong thu hep: thay ca hai mon → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50" "$ADMIN")"
    expect_status "  co cau mon hai" yes "$(has_id "$Q_B")"
    expect_status "thu hep ve mon hai → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50&subjectId=$SUBJECT_B" "$ADMIN")"
    expect_json "  chi mot cau" total 1
    expect_status "  dung cau mon hai" yes "$(has_id "$Q_B")"
    expect_status "thu hep ve mon cua giao vien → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50&subjectId=$SUBJECT_A" "$ADMIN")"
    expect_status "  khong co cau mon hai" no "$(has_id "$Q_B")"
    expect_status "giao vien doc cau mon hai → 404" 404 "$(call GET "/api/questions/$Q_B" "$TEACHER")"
    expect_status "giao vien liet ke: khong co cau mon hai → 200" 200 "$(call GET "/api/questions?topic=$TOPIC&pageSize=50" "$TEACHER")"
    expect_status "  khong thay" no "$(has_id "$Q_B")"
    expect_status "sua cau mon A, gui subjectId mon B → 200" 200 "$(call PUT "/api/questions/$Q_PUB" "$ADMIN" "$(qjson "Cau chung $STAMP" "$TOPIC" ",\"subjectId\":$SUBJECT_B")")"
    expect_json "  cau van o mon A (cau hoi khong chuyen mon giua chung)" subjectId "$SUBJECT_A"
  fi
fi

summary
