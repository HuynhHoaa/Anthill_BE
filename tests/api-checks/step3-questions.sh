#!/usr/bin/env bash
# Buoc 3 — Ngan hang cau hoi va upload anh (§4.5, §6).
#
# Chay:  bash tests/api-checks/step3-questions.sh   (API phai dang chay o $HOST)
#
# Bo kiem cua buoc nay tung duoc chay tay roi mat. Day la ban dung lai, chay lai duoc.

source "$(dirname "$0")/lib.sh"

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

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

section "Ten cau hoi tach khoi noi dung"
# Ten la nhan ngan de nhan ra cau trong ngan hang; khong bat buoc, la mot dong, toi da 200 ky tu.
title_body() {
  # $1 = ten (dang JSON, da escape), $2 = noi dung
  echo "{\"title\":\"$1\",\"content\":\"$2\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"isCorrect\":false}]}"
}
title_of() { body | python -c 'import json,sys;print(json.load(sys.stdin)["title"])'; }

expect_status "tao cau co ten (nhieu dau cach, xuong dong) → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "$(title_body "  Tieu de   rieng\n  $STAMP " "Noi dung khong chua ten $STAMP")")"
QT=$(json_field id); QUESTION_IDS+=("$QT")
expect_status "  ten duoc gop thanh mot dong" "Tieu de rieng $STAMP" "$(title_of)"

expect_status "doc lai: ten giu nguyen → 200" 200 "$(call GET "/api/questions/$QT" "$TEACHER")"
expect_status "  ten van la mot dong" "Tieu de rieng $STAMP" "$(title_of)"

expect_status "tim theo tu chi co trong ten → 200" 200 \
  "$(call GET "/api/questions?search=Tieu%20de%20rieng%20$STAMP&pageSize=10" "$TEACHER")"
expect_status "  ra dung mot cau" 1 "$(body | python -c 'import json,sys;print(json.load(sys.stdin)["total"])')"

expect_status "tao cau khong gui ten → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "$(question_body 1 3)")"
QN=$(json_field id); QUESTION_IDS+=("$QN")
expect_status "  ten la chuoi rong (khong null)" "" "$(title_of)"

expect_status "ten 200 ky tu → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "$(title_body "$(printf 'a%.0s' $(seq 1 200))" "Cau ten dung tran $STAMP")")"
QL=$(json_field id); QUESTION_IDS+=("$QL")
expect_status "ten 201 ky tu → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "$(title_body "$(printf 'a%.0s' $(seq 1 201))" "Cau ten vuot tran $STAMP")")"

# PUT thay toan bo: bo trong ten la xoa ten da dat.
expect_status "sua cau, bo trong ten → 200" 200 \
  "$(call PUT "/api/questions/$QT" "$TEACHER" "$(question_body 1 3)")"
expect_status "  ten da bi xoa" "" "$(title_of)"

section "Data URI bi tu choi (§1)"
expect_status "gui data URI thay vi URL → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau data uri\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"imageUrl\":\"data:image/png;base64,iVBORw0KGgo=\",\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"isCorrect\":false}]}")"

section "URL anh tuyet doi bi tu choi (bao mat)"
# ImageUrl chi duoc nhan khi la duong dan tuong doi do chinh /api/uploads/images cap. Khong chan thi
# mot loi goi API truc tiep (ngoai giao dien) co the gan mot URL tuyet doi toi may chu khac — trinh
# duyet cua moi hoc sinh mo de se tai URL do luc lam bai, lo IP va thoi diem lam bai cho ben thu ba.
expect_status "imageUrl tuyet doi (https) o cau hoi → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau url ngoai $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"imageUrl\":\"https://third-party.example/pixel.png\",\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"isCorrect\":false}]}")"
expect_status "imageUrl noi giao thuc (//host) o cau hoi → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau url ngoai $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"imageUrl\":\"//third-party.example/pixel.png\",\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"isCorrect\":false}]}")"
expect_status "imageUrl tuyet doi o dap an → 400" 400 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau url ngoai dap an $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"answers\":[{\"imageUrl\":\"https://third-party.example/pixel.png\",\"isCorrect\":true},{\"content\":\"Sai\",\"isCorrect\":false}]}")"
expect_status "imageUrl tuong doi dung /uploads → 200" 200 \
  "$(call POST /api/questions "$TEACHER" "{\"content\":\"Cau url dung $STAMP\",\"topic\":\"$TOPIC\",\"difficulty\":\"easy\",\"defaultPoint\":1,\"imageUrl\":\"/uploads/202601/hop-le.png\",\"answers\":[{\"content\":\"A\",\"isCorrect\":true},{\"content\":\"B\",\"isCorrect\":false}]}")"
Q3=$(json_field id); QUESTION_IDS+=("$Q3")

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

# Hang rao 1: dung luong. Tran la 4 MB, chan o HAI cho: dau vao HTTP (truoc khi ASP.NET doc form,
# UploadSizeLimitAttribute) va service (theo do dai file). Hai cho phai cung tra 400 kem cung mot
# cau — truoc day file tren ~28 MB nhan ve 413 rong cua Kestrel, frontend khong co gi de hien.
png_of_size() {
  # $1 = duong dan, $2 = tong so byte (gom 8 byte chu ky PNG)
  { printf '\x89PNG\r\n\x1a\n'; head -c $(($2 - 8)) /dev/zero; } > "$1"
}
# Doc thang byte UTF-8: stdin cua python tren Windows mac dinh la cp1252, vap ngay chu tieng Viet.
says_too_large() {
  python -c "import json,sys;m=json.loads(sys.stdin.buffer.read().decode('utf-8')).get('message','');print('co' if '4 MB' in m else 'khong: '+m.encode('ascii','replace').decode())" < "$BODY_FILE"
}
upload_chunked() {
  curl -s -o "$BODY_FILE" -w '%{http_code}' -X POST "$HOST/api/uploads/images" \
    -H "Authorization: Bearer $TEACHER" -H "Transfer-Encoding: chunked" \
    -F "file=@$(to_native_path "$1");filename=$2;type=image/png"
}

# Vua qua tran 1 byte: than request van nam trong phan du cho vo multipart, nen toi duoc service.
png_of_size "$WORKDIR/tran-cong-1.png" $((4 * 1024 * 1024 + 1))
expect_status "file 4 MB + 1 byte → 400 (service chan)" 400 "$(upload "$WORKDIR/tran-cong-1.png" "tran.png")"
expect_status "  thong diep neu tran 4 MB" co "$(says_too_large)"

png_of_size "$WORKDIR/qua-lon.png" $((5 * 1024 * 1024))
expect_status "file 5 MB → 400 ngay o dau vao" 400 "$(upload "$WORKDIR/qua-lon.png" "qua-lon.png")"
expect_status "  cung mot thong diep voi service" co "$(says_too_large)"

# Khong khai Content-Length: dau vao khong biet truoc, tran than request cua Kestrel cat giua chung.
expect_status "file 5 MB gui chunked → 400" 400 "$(upload_chunked "$WORKDIR/qua-lon.png" "qua-lon.png")"
expect_status "  van la JSON kem thong diep" co "$(says_too_large)"

# Vuot ca tran mac dinh cua Kestrel (~28 MB): truoc day la 413 khong co body.
png_of_size "$WORKDIR/rat-lon.png" $((40 * 1024 * 1024))
expect_status "file 40 MB → 400, khong phai 413 rong" 400 "$(upload "$WORKDIR/rat-lon.png" "rat-lon.png")"
expect_status "  van la JSON kem thong diep" co "$(says_too_large)"

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
