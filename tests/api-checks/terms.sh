#!/usr/bin/env bash
# Ky hoc: danh muc do quan tri vien quan ly (/api/terms) va viec gan ky cho lop (termId tren /api/student-groups).
#
# Chay:  bash tests/api-checks/terms.sh   (API phai dang chay o $HOST, database thu)
#
# Quy tac duoc canh gac o day:
#   - chi quan tri vien quan ly ky hoc (khach 401, giao vien va hoc sinh 403);
#   - ten ky bat buoc, khong trung (409), ngay ket thuc khong truoc ngay bat dau;
#   - toi da MOT ky hien tai: bat ky nay thi ky hien tai cu tu tat;
#   - lop co the khong thuoc ky nao (termId null); termId khong ton tai → 400; ky con lop thi khong xoa duoc (409);
#   - giao vien thay ky cua lop minh (termName) nhung khong dung duoc danh muc ky.
#
# Bo kiem DOI ky hien tai cua database thu, nen luc don dep no dat lai ky hien tai ban dau.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

STAMP=$(date +%H%M%S)
TERM_IDS=(); GROUP_IDS=(); STUDENTS=()
J() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$(to_native_path "$BODY_FILE")"; }

# Ky hien tai truoc khi chay (neu co) de dat lai luc don dep.
call GET /api/terms "$ADMIN" > /dev/null
ORIGINAL_CURRENT=$(J "json.dumps(next((t for t in d if t['isCurrent']), None), ensure_ascii=False)")

cleanup() {
  section "Don du lieu"
  local groups=0 terms=0
  for sid in "${STUDENTS[@]}"; do
    for gid in "${GROUP_IDS[@]}"; do call DELETE "/api/student-groups/$gid/students/$sid" "$ADMIN" "$REMOVE_BODY" > /dev/null; done
    call PATCH "/api/students/$sid/status" "$ADMIN" "$LOCK_BODY" > /dev/null
  done
  for id in "${GROUP_IDS[@]}"; do [[ "$(call DELETE "/api/student-groups/$id" "$ADMIN")" == "200" ]] && groups=$((groups + 1)); done
  for id in "${TERM_IDS[@]}"; do [[ "$(call DELETE "/api/terms/$id" "$ADMIN")" == "200" ]] && terms=$((terms + 1)); done
  # Dat lai ky hien tai ban dau. Di bang python: curl tren Windows lam hong chu co dau trong -d (ten ky la tieng Viet).
  if [[ "$ORIGINAL_CURRENT" != "null" ]]; then
    python -X utf8 - "$HOST" "$ADMIN" "$ORIGINAL_CURRENT" <<'PY'
import json, sys, urllib.request
host, token, original = sys.argv[1], sys.argv[2], json.loads(sys.argv[3])
body = json.dumps({'name': original['name'], 'startsOn': original.get('startsOn'), 'endsOn': original.get('endsOn'), 'isCurrent': True}, ensure_ascii=False).encode('utf-8')
request = urllib.request.Request(f"{host}/api/terms/{original['id']}", data=body, method='PUT')
request.add_header('Content-Type', 'application/json; charset=utf-8')
request.add_header('Authorization', f'Bearer {token}')
urllib.request.urlopen(request, timeout=30).read()
PY
  fi
  printf '  xoa %d/%d lop · %d/%d ky · khoa %d hoc sinh · dat lai ky hien tai ban dau\n' \
    "$groups" "${#GROUP_IDS[@]}" "$terms" "${#TERM_IDS[@]}" "${#STUDENTS[@]}"
}
trap cleanup EXIT

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

TERM_A="Ky kiem thu A $STAMP"
TERM_B="Ky kiem thu B $STAMP"

section "Phan quyen: chi quan tri vien quan ly ky hoc"
expect_status "khach: danh sach ky → 401" 401 "$(call GET /api/terms)"
expect_status "giao vien: danh sach ky → 403" 403 "$(call GET /api/terms "$TEACHER")"
expect_status "giao vien: tao ky → 403" 403 "$(call POST /api/terms "$TEACHER" "{\"name\":\"$TERM_A\"}")"
expect_status "giao vien: sua ky → 403" 403 "$(call PUT /api/terms/1 "$TEACHER" "{\"name\":\"$TERM_A\"}")"
expect_status "giao vien: xoa ky → 403" 403 "$(call DELETE /api/terms/1 "$TEACHER")"
expect_status "quan tri vien: danh sach ky → 200" 200 "$(call GET /api/terms "$ADMIN")"

section "Tao ky: kiem tra du lieu va trung ten"
expect_status "thieu ten → 400" 400 "$(call POST /api/terms "$ADMIN" '{"name":""}')"
expect_status "chi co khoang trang → 400" 400 "$(call POST /api/terms "$ADMIN" '{"name":"    "}')"
expect_status "ten dai hon 100 ky tu → 400" 400 "$(call POST /api/terms "$ADMIN" "{\"name\":\"$(printf 'a%.0s' $(seq 1 101))\"}")"
expect_status "ngay ket thuc truoc ngay bat dau → 400" 400 \
  "$(call POST /api/terms "$ADMIN" "{\"name\":\"$TERM_A\",\"startsOn\":\"2027-02-01\",\"endsOn\":\"2027-01-01\"}")"
expect_status "tao ky A (co ngay) → 200" 200 \
  "$(call POST /api/terms "$ADMIN" "{\"name\":\"$TERM_A\",\"startsOn\":\"2027-01-18\",\"endsOn\":\"2027-06-01\"}")"
TERM_A_ID=$(json_field id); TERM_IDS+=("$TERM_A_ID")
expect_json "  ten giu nguyen" name "$TERM_A"
expect_json "  moi tao chua la ky hien tai" isCurrent false
expect_json "  chua co lop nao" groupCount 0
expect_json "  ngay bat dau" startsOn "2027-01-18"
expect_status "trung ten → 409" 409 "$(call POST /api/terms "$ADMIN" "{\"name\":\"$TERM_A\"}")"
expect_status "trung ten khac hoa thuong va khoang trang thua → 409" 409 \
  "$(call POST /api/terms "$ADMIN" "{\"name\":\"  ky KIEM thu  a   $STAMP \"}")"
expect_status "tao ky B khong ghi ngay → 200" 200 "$(call POST /api/terms "$ADMIN" "{\"name\":\"$TERM_B\"}")"
TERM_B_ID=$(json_field id); TERM_IDS+=("$TERM_B_ID")
expect_status "  khong ngay van hop le (startsOn vang)" True "$(J "d.get('startsOn') is None")"

section "Ky hien tai: toi da mot"
expect_status "dat ky A lam hien tai → 200" 200 "$(call PUT "/api/terms/$TERM_A_ID" "$ADMIN" "{\"name\":\"$TERM_A\",\"startsOn\":\"2027-01-18\",\"endsOn\":\"2027-06-01\",\"isCurrent\":true}")"
expect_json "  ky A la ky hien tai" isCurrent true
call GET /api/terms "$ADMIN" > /dev/null
expect_status "  trong danh sach chi co dung mot ky hien tai" 1 "$(J "sum(1 for t in d if t['isCurrent'])")"
expect_status "  ky hien tai dung dau danh sach" True "$(J "d[0]['isCurrent'] and d[0]['id'] == $TERM_A_ID")"
expect_status "dat ky B lam hien tai → 200" 200 "$(call PUT "/api/terms/$TERM_B_ID" "$ADMIN" "{\"name\":\"$TERM_B\",\"isCurrent\":true}")"
call GET /api/terms "$ADMIN" > /dev/null
expect_status "  van chi mot ky hien tai" 1 "$(J "sum(1 for t in d if t['isCurrent'])")"
expect_status "  ky A da tat" False "$(J "next(t for t in d if t['id'] == $TERM_A_ID)['isCurrent']")"
expect_status "tao ky C hien tai ngay luc tao → 200" 200 "$(call POST /api/terms "$ADMIN" "{\"name\":\"Ky kiem thu C $STAMP\",\"isCurrent\":true}")"
TERM_C_ID=$(json_field id); TERM_IDS+=("$TERM_C_ID")
call GET /api/terms "$ADMIN" > /dev/null
expect_status "  van chi mot ky hien tai (ky C)" True "$(J "sum(1 for t in d if t['isCurrent']) == 1 and next(t for t in d if t['id'] == $TERM_C_ID)['isCurrent']")"
expect_status "tat ky hien tai bang cach sua isCurrent=false → 200" 200 "$(call PUT "/api/terms/$TERM_C_ID" "$ADMIN" "{\"name\":\"Ky kiem thu C $STAMP\",\"isCurrent\":false}")"
call GET /api/terms "$ADMIN" > /dev/null
expect_status "  duoc phep khong co ky hien tai nao" 0 "$(J "sum(1 for t in d if t['isCurrent'])")"

section "Sua ky: doi ten, trung ten, khong tim thay"
expect_status "doi ten ky A → 200" 200 "$(call PUT "/api/terms/$TERM_A_ID" "$ADMIN" "{\"name\":\"$TERM_A (sua)\"}")"
expect_json "  ten moi" name "$TERM_A (sua)"
expect_status "giu nguyen ten cua chinh no khong bi coi la trung → 200" 200 "$(call PUT "/api/terms/$TERM_A_ID" "$ADMIN" "{\"name\":\"$TERM_A (sua)\"}")"
expect_status "doi ten ky A thanh ten ky B → 409" 409 "$(call PUT "/api/terms/$TERM_A_ID" "$ADMIN" "{\"name\":\"$TERM_B\"}")"
expect_status "sua ky khong ton tai → 404" 404 "$(call PUT /api/terms/999999 "$ADMIN" "{\"name\":\"Khong co\"}")"
expect_status "xoa ky khong ton tai → 404" 404 "$(call DELETE /api/terms/999999 "$ADMIN")"
expect_status "doi ten ve nhu cu → 200" 200 "$(call PUT "/api/terms/$TERM_A_ID" "$ADMIN" "{\"name\":\"$TERM_A\"}")"

section "Gan ky cho lop: tao, sua, bo, ky khong ton tai"
call GET /api/auth/me "$TEACHER" > /dev/null; TEACHER_ID=$(json_field id)
expect_status "tao lop kem termId khong ton tai → 400" 400 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop ky sai $STAMP\",\"teacherId\":$TEACHER_ID,\"termId\":999999}")"
expect_status "tao lop kem ky A → 200" 200 \
  "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop ky kiem thu $STAMP\",\"teacherId\":$TEACHER_ID,\"termId\":$TERM_A_ID}")"
GID=$(json_field id); GROUP_IDS+=("$GID")
expect_json "  lop ghi ky" termId "$TERM_A_ID"
expect_json "  lop ghi ten ky" termName "$TERM_A"
expect_status "tao lop khong ghi ky → 200 (ky la tuy chon)" 200 "$(call POST /api/student-groups "$ADMIN" "{\"name\":\"Lop khong ky $STAMP\",\"teacherId\":$TEACHER_ID}")"
GID_NONE=$(json_field id); GROUP_IDS+=("$GID_NONE")
expect_status "  lop khong ky: termId vang" True "$(J "d.get('termId') is None and d.get('termName') is None")"

expect_status "sua lop sang ky khong ton tai → 400" 400 \
  "$(call PUT "/api/student-groups/$GID" "$ADMIN" "{\"name\":\"Lop ky kiem thu $STAMP\",\"termId\":999999}")"
call GET "/api/student-groups/$GID" "$ADMIN" > /dev/null
expect_json "  ky cua lop khong doi sau khi bi tu choi" termId "$TERM_A_ID"
expect_status "chuyen lop sang ky B → 200" 200 "$(call PUT "/api/student-groups/$GID" "$ADMIN" "{\"name\":\"Lop ky kiem thu $STAMP\",\"termId\":$TERM_B_ID}")"
expect_json "  lop da sang ky B" termId "$TERM_B_ID"
expect_status "gan ky cho lop khong ky → 200" 200 "$(call PUT "/api/student-groups/$GID_NONE" "$ADMIN" "{\"name\":\"Lop khong ky $STAMP\",\"termId\":$TERM_A_ID}")"

section "Giao vien thay ky cua lop minh"
[[ "$(call POST "/api/student-groups/$GID/publish" "$ADMIN")" != "200" ]] && fail_fast "cong bo lop"
expect_status "giao vien: danh sach lop → 200" 200 "$(call GET /api/student-groups "$TEACHER")"
expect_status "  lop cua giao vien ghi ten ky" "$TERM_B" \
  "$(J "next(g for g in (d['items'] if isinstance(d, dict) else d) if g['id'] == $GID)['termName']")"

section "Xoa ky: ky con lop thi khong xoa duoc"
expect_status "xoa ky A khi con lop → 409" 409 "$(call DELETE "/api/terms/$TERM_A_ID" "$ADMIN")"
call GET /api/terms "$ADMIN" > /dev/null
expect_status "  danh sach dem dung so lop cua ky A (mot lop)" 1 "$(J "next(t for t in d if t['id'] == $TERM_A_ID)['groupCount']")"
expect_status "  danh sach dem dung so lop cua ky B (mot lop)" 1 "$(J "next(t for t in d if t['id'] == $TERM_B_ID)['groupCount']")"
expect_status "bo phan ky cho lop (termId null) → 200" 200 "$(call PUT "/api/student-groups/$GID_NONE" "$ADMIN" "{\"name\":\"Lop khong ky $STAMP\",\"termId\":null}")"
expect_status "  lop da khong con ky" True "$(J "d.get('termId') is None and d.get('termName') is None")"
expect_status "xoa ky A khi het lop → 200" 200 "$(call DELETE "/api/terms/$TERM_A_ID" "$ADMIN")"
TERM_IDS=("$TERM_B_ID" "$TERM_C_ID")
expect_status "  ky A khong con trong danh sach" False "$(call GET /api/terms "$ADMIN" > /dev/null; J "any(t['id'] == $TERM_A_ID for t in d)")"

section "Hoc sinh khong dung duoc danh muc ky cua quan tri vien"
S=$(call POST /api/students "$ADMIN" "{\"fullName\":\"Hoc sinh ky $STAMP\",\"email\":\"ky$STAMP@kiemthu.vn\",\"username\":\"ky$STAMP\",\"code\":\"KY$STAMP\",\"password\":\"123456\",\"groupId\":$GID}")
[[ "$S" != "200" ]] && fail_fast "tao hoc sinh: HTTP $S — $(body)"
STUDENTS+=("$(json_field id)")
TOKS=$(login_and_clear_must_change "ky$STAMP" "123456" "Ky${STAMP}x")
expect_status "hoc sinh: danh sach ky cua quan tri vien → 403" 403 "$(call GET /api/terms "$TOKS")"
expect_status "hoc sinh: tao ky → 403" 403 "$(call POST /api/terms "$TOKS" "{\"name\":\"Ky hoc sinh $STAMP\"}")"

summary
