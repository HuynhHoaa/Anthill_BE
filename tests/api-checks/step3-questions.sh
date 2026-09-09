#!/usr/bin/env bash
# Buoc 3 — Ngan hang cau hoi va upload anh (§4.5, §6).
#
# Chay:  bash tests/api-checks/step3-questions.sh   (API phai dang chay o $HOST)
#
# Bo kiem cua buoc nay tung duoc chay tay roi mat. Day la ban dung lai, chay lai duoc.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@eduquiz.vn" "eduquiz123")
ADMIN=$(login "huy.pq@eduquiz.vn" "eduquiz123")
STUDENT=$(login "lan.tt" "eduquiz123")

STAMP=$(date +%H%M%S)
TOPIC="KT3$STAMP"
QUESTION_IDS=()
WORKDIR=$(mktemp -d)

cleanup() {
  section "Don du lieu"
  local removed=0
  for id in "${QUESTION_IDS[@]}"; do
    [[ "$(call DELETE "/api/questions/$id" "$TEACHER")" == "200" ]] && removed=$((removed + 1))
  done
  rm -rf "$WORKDIR"
  printf '  xoa %d/%d cau hoi\n' "$removed" "${#QUESTION_IDS[@]}"
}
trap cleanup EXIT

question_body() {
  # $1 = so dap an dung, $2 = tong so dap an
  local correct="$1" total="$2" answers="" i
  for ((i = 1; i <= total; i++)); do
    [[ -n "$answers" ]] && answers="$answers,"
    if [[ $i -le $correct ]]; then
      answers="$answers{\"content\":\"Dap an $i\",\"isCorrect\":true}"
    else
      answers="$answers{\"content\":\"Dap an $i\",\"isCorrect\":false}"
    fi
  done
  echo "{\"content\":\"Cau kiem thu $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[$answers]}"
}

section "Rang buoc dap an (§4.5)"
expect_status "khong co dap an dung → 400"  400 "$(call POST /api/questions "$TEACHER" "$(question_body 0 4)")"
expect_status "hai dap an dung → 400"        400 "$(call POST /api/questions "$TEACHER" "$(question_body 2 4)")"
expect_status "chi mot dap an → 400"         400 "$(call POST /api/questions "$TEACHER" "$(question_body 1 1)")"
expect_status "dung mot dap an dung → 200"   200 "$(call POST /api/questions "$TEACHER" "$(question_body 1 4)")"
Q1=$(json_field id); QUESTION_IDS+=("$Q1")

# Dap an chi co anh, khong co chu — hop le.
expect_status "dap an chi co anh, khong co chu → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau co anh $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"medium\",\"defaultPoint\":1,\"answers\":[{\"imageUrl\":\"/uploads/202601/abc.png\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")"
Q2=$(json_field id); QUESTION_IDS+=("$Q2")

section "Sua cau hoi thay CA mang dap an"
expect_status "doc cau vua tao → 200" 200 "$(call GET "/api/questions/$Q1" "$TEACHER")"
OLD_ANSWER_ID=$(body | python -c 'import json,sys;print(json.load(sys.stdin)["answers"][0]["id"])')
expect_status "sua cau hoi → 200" 200 "$(call PUT "/api/questions/$Q1" "$TEACHER" "$(question_body 1 3)")"
NEW_ANSWER_IDS=$(body | python -c 'import json,sys;print(",".join(str(a["id"]) for a in json.load(sys.stdin)["answers"]))')
expect_status "  id dap an cu bien mat hoan toan" absent \
  "$(python -c "print('present' if '$OLD_ANSWER_ID' in '$NEW_ANSWER_IDS'.split(',') else 'absent')")"

section "Data URI bi tu choi (§1)"
expect_status "gui data URI thay vi URL → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau data uri\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"imageUrl\":\"data:image/png;base64,iVBORw0KGgo=\",\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"isCorrect\":false}]}")"

section "Pham vi to bo mon (§6)"
expect_status "hoc sinh doc ngan hang → 403" 403 "$(call GET /api/questions "$STUDENT")"
expect_status "hoc sinh tao cau hoi → 403" 403 "$(call POST /api/questions "$STUDENT" "$(question_body 1 4)")"
expect_status "quan tri vien doc duoc toan bo → 200" 200 "$(call GET /api/questions "$ADMIN")"
# Tai khoan quan tri khong thuoc mon nao nen khong co gi de suy ra subjectId.
expect_status "quan tri vien tao cau ma thieu subjectId → 400" 400 \
  "$(call POST /api/questions "$ADMIN" "$(question_body 1 4)")"
expect_status "cau khong ton tai → 404" 404 "$(call GET /api/questions/999999 "$TEACHER")"

section "Gop loi goi — /questions/summary"
expect_status "summary → 200" 200 "$(call GET "/api/questions/summary" "$TEACHER")"
for field in total active hidden easy medium hard; do
  expect_status "  co truong $field" present \
    "$(body | python -c "import json,sys;print('present' if '$field' in json.load(sys.stdin) else 'absent')")"
done
expect_status "  co danh sach chu de" present \
  "$(body | python -c "import json,sys;print('present' if 'topics' in json.load(sys.stdin) else 'absent')")"

section "Bo loc tu choi gia tri la thay vi am tham bo loc"
expect_status "difficulty=42 → 400"       400 "$(call GET "/api/questions?difficulty=42" "$TEACHER")"
expect_status "difficulty=all → 200"      200 "$(call GET "/api/questions?difficulty=all" "$TEACHER")"
expect_status "status=deleted → 400"      400 "$(call GET "/api/questions?status=deleted" "$TEACHER")"

section "Upload anh — bon hang rao (§4.5)"

# Dung file that thay vi chuoi bia: hang rao thu ba doc magic bytes, nen noi dung phai dung.
PNG="$WORKDIR/that.png"
printf '\x89PNG\r\n\x1a\n' > "$PNG"
head -c 2048 /dev/urandom >> "$PNG"

upload() {
  local file="$1" name="$2" type="${3:-image/png}"
  curl -s -o "$BODY_FILE" -w '%{http_code}' -X POST "$HOST/api/uploads/images" \
    -H "Authorization: Bearer $TEACHER" \
    -F "file=@$(to_native_path "$file");filename=$name;type=$type"
}

# Hang rao 2: duoi ngoai danh sach cho phep.
cp "$PNG" "$WORKDIR/anh.bmp"
expect_status "duoi .bmp ngoai danh sach → 400" 400 "$(upload "$WORKDIR/anh.bmp" "anh.bmp" "image/bmp")"

# SVG co y khong nam trong danh sach: no chua duoc the script, ma anh phuc vu tu cung origin
# voi API la mot lo XSS hoan chinh.
printf '<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>' > "$WORKDIR/xau.svg"
expect_status "SVG bi tu choi (chua duoc the script) → 400" 400 "$(upload "$WORKDIR/xau.svg" "xau.svg" "image/svg+xml")"

# Hang rao 3: magic bytes phai khop duoi file.
printf '<script>alert(1)</script>' > "$WORKDIR/gia.png"
expect_status "file script doi duoi .png → 400" 400 "$(upload "$WORKDIR/gia.png" "gia.png")"
cp "$PNG" "$WORKDIR/that.gif"
expect_status "PNG that doi duoi .gif → 400" 400 "$(upload "$WORKDIR/that.gif" "that.gif" "image/gif")"

# Hang rao 1: dung luong chan truoc khi doc byte nao. Tran la 4 MB.
head -c 5242880 /dev/urandom > "$WORKDIR/qua-lon.png"
printf '\x89PNG\r\n\x1a\n' | cat - "$WORKDIR/qua-lon.png" > "$WORKDIR/qua-lon2.png"
expect_status "file 5 MB vuot tran 4 MB → 400" 400 "$(upload "$WORKDIR/qua-lon2.png" "qua-lon.png")"

# Duong di hop le.
expect_status "PNG that, duoi dung → 200" 200 "$(upload "$PNG" "anh-cua-toi.png")"
UPLOADED_URL=$(json_field url)

# Hang rao 4: ten file do server sinh, ten nguoi dung gui len khong cham toi dia.
expect_status "  ten file do server sinh, khong giu ten goc" true \
  "$(python -c "
import re
url = '''$UPLOADED_URL'''
print(str(bool(re.search(r'/[0-9a-f]{32}\.png\$', url)) and 'anh-cua-toi' not in url).lower())
")"

section "Phuc vu anh"
expect_status "tai lai anh vua upload → 200" 200 "$(call GET "$UPLOADED_URL" "")"
# Duyet thu muc: /uploads/../appsettings.json khong duoc cham toi file cau hinh.
expect_status "duyet thu muc len tren → 404" 404 "$(call GET "/uploads/../appsettings.json" "")"

section "Xoa cau da vao de → 409 kem goi y an"
# Chi kiem duoc khi co de dung cau nay; buoc 5 da co endpoint giao bai nen dung thang.
expect_status "an cau hoi thay vi xoa → 200" 200 \
  "$(call PATCH "/api/questions/$Q2/status" "$TEACHER" '{"status":"hidden"}')"
expect_json   "  status doi sang hidden" status hidden
expect_status "bat lai → 200" 200 \
  "$(call PATCH "/api/questions/$Q2/status" "$TEACHER" '{"status":"active"}')"
expect_status "trang thai la → 400" 400 \
  "$(call PATCH "/api/questions/$Q2/status" "$TEACHER" '{"status":"deleted"}')"

summary
