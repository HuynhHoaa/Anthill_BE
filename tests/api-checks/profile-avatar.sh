#!/usr/bin/env bash
# Anh dai dien: giao vien va quan tri vien tu dat / go anh cua minh, hoc sinh thi khong.
#
# PUT /api/auth/me/avatar { avatarUrl }
#   - anh tai len truoc o POST /api/uploads/images, roi gui duong dan nhan ve;
#   - chi nhan duong dan tuong doi do chinh server cap (/uploads/...): URL tuyet doi, data URI, ".." bi 400;
#   - gui rong / null la go anh;
#   - hoc sinh bi 403 (ho so hoc sinh do nha truong quan ly, va hoc sinh cung khong upload duoc anh);
#   - ket qua co o GET /api/auth/me va o phan hoi dang nhap.
#
# Chay:  bash tests/api-checks/profile-avatar.sh   (API phai dang chay o $HOST)

source "$(dirname "$0")/lib.sh"

WORKDIR=$(mktemp -d)
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

clear_avatars() {
  call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":null}' > /dev/null
  call PUT /api/auth/me/avatar "$ADMIN" '{"avatarUrl":null}' > /dev/null
  rm -rf "$WORKDIR"
}
trap clear_avatars EXIT

# API bo qua truong co gia tri null, nen "khong co anh" nghia la thieu han khoa avatarUrl chu khong phai avatarUrl: null.
expect_no_avatar() {
  local label="$1"
  if body | grep -q '"avatarUrl"'; then
    FAILED=$((FAILED + 1)); FAILURES+=("$label — van con avatarUrl: $(body | head -c 160)")
    printf '  \033[31mFAIL\033[0m %-58s van con avatarUrl\n' "$label"
  else
    PASSED=$((PASSED + 1)); printf '  \033[32mOK\033[0m   %-58s %s\n' "$label" "khong co avatarUrl"
  fi
}

upload_as() {
  local token="$1" file="$2" name="$3"
  curl -s -o "$BODY_FILE" -w '%{http_code}' -X POST "$HOST/api/uploads/images" \
    -H "Authorization: Bearer $token" \
    -F "file=@$(to_native_path "$file");filename=$name;type=image/png"
}

PNG="$WORKDIR/avatar.png"
printf '\x89PNG\r\n\x1a\n' > "$PNG"
head -c 1024 /dev/urandom >> "$PNG"

section "Truoc khi dat: chua co anh"
expect_status "giao vien GET /api/auth/me → 200" 200 "$(call GET /api/auth/me "$TEACHER")"
expect_no_avatar "chua co avatarUrl"

section "Giao vien dat anh"
expect_status "tai anh len → 200" 200 "$(upload_as "$TEACHER" "$PNG" "avatar.png")"
URL=$(json_field url)
printf '  duong dan nhan ve: %s\n' "$URL"
expect_status "dat avatar bang duong dan do server cap → 200" 200 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" "{\"avatarUrl\":\"$URL\"}")"
expect_json "phan hoi co avatarUrl" avatarUrl "$URL"
expect_status "GET /api/auth/me → 200" 200 "$(call GET /api/auth/me "$TEACHER")"
expect_json "/me tra dung avatarUrl" avatarUrl "$URL"
expect_json "ho ten khong bi dung vao" fullName "$(json_field fullName)"

CODE=$(curl -s -o /dev/null -w '%{http_code}' "$HOST$URL")
expect_status "file anh tai ve duoc (khong can token)" 200 "$CODE"

c=$(call POST /api/auth/login "" '{"username":"an.nv@anthill.edu.vn","password":"123456"}')
expect_status "dang nhap lai → 200" 200 "$c"
expect_json "phan hoi dang nhap co avatarUrl" user.avatarUrl "$URL"

section "Duong dan khong hop le bi tu choi"
expect_status "URL tuyet doi toi may chu khac → 400" 400 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":"https://nguoi-la.example/a.png"}')"
expect_status "URL cung giao thuc // → 400" 400 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":"//nguoi-la.example/uploads/a.png"}')"
expect_status "data URI → 400" 400 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":"data:image/png;base64,AAAA"}')"
expect_status "duong dan co .. → 400" 400 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":"/uploads/../secrets/a.png"}')"
expect_status "duong dan ngoai /uploads → 400" 400 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":"/avatars/a.png"}')"
LONG=$(python -c "print('/uploads/' + 'a'*520)")
expect_status "duong dan qua 512 ky tu → 400" 400 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" "{\"avatarUrl\":\"$LONG\"}")"
expect_status "thieu truong avatarUrl → 400" 400 "$(call PUT /api/auth/me/avatar "$TEACHER" '{}')"

call GET /api/auth/me "$TEACHER" > /dev/null
expect_json "anh cu van con sau cac lan bi tu choi" avatarUrl "$URL"

section "Phan quyen"
expect_status "khong token → 401" 401 "$(call PUT /api/auth/me/avatar "" '{"avatarUrl":null}')"
expect_status "hoc sinh dat anh → 403" 403 \
  "$(call PUT /api/auth/me/avatar "$STUDENT" "{\"avatarUrl\":\"$URL\"}")"
expect_status "hoc sinh go anh cung → 403" 403 \
  "$(call PUT /api/auth/me/avatar "$STUDENT" '{"avatarUrl":null}')"
expect_status "hoc sinh tai anh len → 403" 403 "$(upload_as "$STUDENT" "$PNG" "avatar.png")"

section "Go anh"
expect_status "avatarUrl = null → 200" 200 "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":null}')"
expect_no_avatar "da go anh"
expect_status "dat lai" 200 "$(call PUT /api/auth/me/avatar "$TEACHER" "{\"avatarUrl\":\"$URL\"}")"
expect_status "avatarUrl = chuoi rong → 200" 200 "$(call PUT /api/auth/me/avatar "$TEACHER" '{"avatarUrl":""}')"
expect_no_avatar "chuoi rong cung la go anh"
expect_status "dat lai voi khoang trang hai dau → 200" 200 \
  "$(call PUT /api/auth/me/avatar "$TEACHER" "{\"avatarUrl\":\"  $URL  \"}")"
expect_json "khoang trang duoc cat" avatarUrl "$URL"

section "Quan tri vien cung dat va go duoc, va anh cua hai nguoi khong lan nhau"
expect_status "tai anh len (admin) → 200" 200 "$(upload_as "$ADMIN" "$PNG" "admin.png")"
ADMIN_URL=$(json_field url)
expect_status "admin dat avatar → 200" 200 "$(call PUT /api/auth/me/avatar "$ADMIN" "{\"avatarUrl\":\"$ADMIN_URL\"}")"
expect_json "avatar cua admin" avatarUrl "$ADMIN_URL"
call GET /api/auth/me "$TEACHER" > /dev/null
expect_json "avatar giao vien khong bi doi" avatarUrl "$URL"
expect_status "admin go avatar → 200" 200 "$(call PUT /api/auth/me/avatar "$ADMIN" '{"avatarUrl":null}')"
expect_no_avatar "admin da go"

summary
