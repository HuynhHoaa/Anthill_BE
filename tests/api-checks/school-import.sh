#!/usr/bin/env bash
# Nhap tong hop tu MOT file Excel: mot trang, moi dong mot hoc sinh kem lop va giao vien chu nhiem. Mot lan tai len tao giao vien,
# lop (kem chu nhiem) va hoc sinh, xep hoc sinh vao lop; sau do moi thu duoc quan ly o trang rieng cua no.
#
# Chay:  bash tests/api-checks/school-import.sh   (API phai dang chay o $HOST, database thu; can python co openpyxl)
#
# Quy tac duoc canh gac o day:
#   - chi quan tri vien tai file mau va nhap; file mau mot trang, du cot, co danh sach mon va trang huong dan;
#   - thieu cot bat buoc → ca file bi tu choi; thieu mat khau khi co tai khoan moi → 400 va KHONG ghi gi (rollback ca ba buoc);
#   - nhap thu chay het duong ma roi rollback: dem dung, khong ghi gi, thay duoc lop va giao vien moi tao cung luc;
#   - giao vien chu nhiem chi can ghi mot lan cho moi lop (cac dong sau de trong hoac lap lai deu duoc); hai giao vien khac nhau
#     o cung mot lop, hoac cung email ma khac ten / so dien thoai → dong den sau bi tu choi;
#   - loi cua giao vien hay lop keo theo hoc sinh cua lop do chua duoc xep: loi goc bao mot lan, cac em con lai bao "xem dong N";
#   - dong hong khong lam hong ca me; gui lai nguyen file thi dong da nhap duoc nhan ra, khong bao trung;
#   - lop moi la ban nhap, tru khi chon cong bo ngay; hoc sinh co san chi duoc xep THEM lop.

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

STAMP=$(date +%H%M%S)
INITIAL_PASSWORD="School$STAMP"

# Duong dan dang C:/... de ca bash, curl lan python (ban Windows) cung hieu.
WORK=$(cygpath -m "$(mktemp -d -t anthill-school.XXXXXX)")
TOOL=$(cygpath -m "$(cd "$(dirname "$0")" && pwd)/xlsx_tool.py")
XLSX_TYPE='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }
xl() { python -X utf8 -W ignore "$TOOL" "$@"; }
J() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$(to_native_path "$BODY_FILE")"; }
K() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$WORK/$2"; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }

upload() { # PATH TOKEN FILE [truong=gia tri ...]
  local path="$1" token="$2" file="$3"; shift 3
  local args=(-s -o "$BODY_FILE" -w '%{http_code}' -X POST "$HOST$path")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$file" ]] && args+=(-F "file=@$file;type=$XLSX_TYPE")
  local field
  for field in "$@"; do args+=(-F "$field"); done
  curl "${args[@]}"
}
school() { upload /api/imports/school "$ADMIN" "$@"; }

issue_has() { # DONG TRUONG CHUOI
  python -X utf8 - "$(to_native_path "$BODY_FILE")" "$1" "$2" "$3" <<'PY'
import json, sys
path, row, field, needle = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
d = json.load(open(path, encoding='utf-8'))
hit = [i for i in d['issues'] if i['row'] == row]
print('yes' if hit and needle in (hit[0].get(field) or '') else 'no ' + json.dumps(hit, ensure_ascii=True)[:200])
PY
}
failed_rows() { J "','.join(str(i['row']) for i in d['issues'])"; }

cleanup() {
  section "Don du lieu"
  local line sid gid
  # Lop khoa la lop chi doc: mo khoa truoc roi moi go duoc thanh vien.
  call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null
  while IFS= read -r gid; do
    [[ -n "$gid" ]] && call POST "/api/student-groups/$gid/unlock" "$ADMIN" > /dev/null
  done < <(J "'\n'.join(str(g['id']) for g in d)")

  call GET "/api/students?search=SC$STAMP&pageSize=100" "$ADMIN" > /dev/null
  while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    sid=${line%% *}
    for gid in ${line#* }; do
      [[ "$gid" != "0" ]] && call DELETE "/api/student-groups/$gid/students/$sid" "$ADMIN" "$REMOVE_BODY" > /dev/null
    done
    call PATCH "/api/students/$sid/status" "$ADMIN" "$LOCK_BODY" > /dev/null
  done < <(J "'\n'.join(str(i['id']) + ' ' + (' '.join(str(g['id']) for g in i['groups']) or '0') for i in d['items'])")

  call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null
  local removed=0 total=0
  while IFS= read -r gid; do
    [[ -z "$gid" ]] && continue
    total=$((total + 1))
    [[ "$(call DELETE "/api/student-groups/$gid" "$ADMIN")" == "200" ]] && removed=$((removed + 1))
  done < <(J "'\n'.join(str(g['id']) for g in d)")

  call GET /api/terms "$ADMIN" > /dev/null
  local terms_removed=0
  while IFS= read -r sid; do
    [[ -z "$sid" ]] && continue
    [[ "$(call DELETE "/api/terms/$sid" "$ADMIN")" == "200" ]] && terms_removed=$((terms_removed + 1))
  done < <(J "'\n'.join(str(t['id']) for t in d if '$STAMP' in t['name'])")

  call GET "/api/teachers?search=$STAMP&pageSize=100" "$ADMIN" > /dev/null
  local locked=0
  while IFS= read -r sid; do
    [[ -z "$sid" ]] && continue
    retire_teacher "$ADMIN" "$sid"; locked=$((locked + 1))
  done < <(J "'\n'.join(str(t['id']) for t in d['items'])")

  rm -rf "$WORK"
  printf '  xoa %d/%d lop, %d ky hoc, khoa %d giao vien kiem thu\n' "$removed" "$total" "$terms_removed" "$locked"
}
trap cleanup EXIT

teacher_total() { call GET "/api/teachers?search=$STAMP&pageSize=100" "$ADMIN" > /dev/null; json_field total; }
student_total() { call GET "/api/students?search=SC$STAMP&pageSize=100" "$ADMIN" > /dev/null; json_field total; }
group_names() { call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null; J "','.join(sorted(g['name'] for g in d))"; }
# Cac lop (id tang dan) cua mot hoc sinh theo ma, dang "ten|ten".
student_groups() {
  call GET "/api/students?search=$1&pageSize=100" "$ADMIN" > /dev/null
  J "'|'.join(sorted(g['name'] for g in next(i for i in d['items'] if i['code'] == '$1')['groups']))"
}

call GET "/api/teachers/$DEMO_TEACHER_ID" "$ADMIN" > /dev/null
SUBJECT_ID=$(json_field subjectId)
DEMO_NAME=$(json_field fullName)
call GET /api/questions/subjects "$ADMIN" > /dev/null
SUBJ_NAME=$(J "next(s['name'] for s in d if s['id'] == $SUBJECT_ID)")

# ── Phan quyen va file mau ───────────────────────────────────────────────────────────────────────
section "Chi quan tri vien tai file mau va nhap"
expect_status "khach tai mau → 401" 401 "$(call GET /api/imports/school/template)"
expect_status "hoc sinh tai mau → 403" 403 "$(call GET /api/imports/school/template "$STUDENT")"
expect_status "giao vien tai mau → 403" 403 "$(call GET /api/imports/school/template "$TEACHER")"
expect_status "giao vien nhap → 403" 403 "$(upload /api/imports/school "$TEACHER" "" dryRun=true)"
expect_status "hoc sinh nhap → 403" 403 "$(upload /api/imports/school "$STUDENT" "" dryRun=true)"
expect_status "khach nhap → 401" 401 "$(upload /api/imports/school "" "" dryRun=true)"
expect_status "admin khong gui file → 400" 400 "$(school "" dryRun=true)"

section "File mau: mot trang, du cot, co danh sach mon"
code=$(curl -s -D "$WORK/headers.txt" -o "$WORK/tpl.xlsx" -w '%{http_code}' -H "Authorization: Bearer $ADMIN" "$HOST/api/imports/school/template")
expect_status "admin tai mau → 200" 200 "$code"
expect_status "  dung kieu noi dung xlsx" yes "$(grep -qi 'spreadsheetml.sheet' "$WORK/headers.txt" && echo yes || echo no)"
xl info "$WORK/tpl.xlsx" > "$WORK/tpl.json"
expect_status "  trang nhap lieu + trang danh sach (mon) + trang huong dan" "Học sinh,Danh sách,Hướng dẫn" "$(K "','.join(d['sheets'])" tpl.json)"
expect_status "  cot cua mau (sau * o cac cot bat buoc)" \
  "Mã học sinh*,Họ và tên*,Lớp*,Giáo viên chủ nhiệm*,Email giáo viên*,Số điện thoại giáo viên*,Môn giảng dạy,Email học sinh,Số điện thoại học sinh,Tên đăng nhập,Kỳ học" \
  "$(K "','.join(d['header'])" tpl.json)"
expect_status "  co danh sach mon" yes "$(K "'yes' if d['lists'].get('Môn giảng dạy') else 'no'" tpl.json)"
expect_status "  trang huong dan co noi dung" yes "$(K "'yes' if len(d['guideLines']) > 6 else 'no'" tpl.json)"

section "File hong bi tu choi bang cau tieng Viet"
xl make "$WORK/missing-col.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Email giao vien*' <<ROWS
SC${STAMP}00|Hoc sinh|Lop X|gv@kiemthu.vn
ROWS
expect_status "thieu cot bat buoc (giao vien chu nhiem, so dien thoai) → 400" 400 "$(school "$WORK/missing-col.xlsx" dryRun=true)"
expect_status "  noi ro cot thieu" yes "$(body_has 'Không tìm thấy cột bắt buộc')"
xl make "$WORK/header-only.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*' < /dev/null
expect_status "chi co tieu de → 400" 400 "$(school "$WORK/header-only.xlsx" dryRun=true)"
printf 'khong phai excel' > "$WORK/fake.xlsx"
expect_status "file khong phai Excel → 400" 400 "$(school "$WORK/fake.xlsx" dryRun=true)"

# ── Du lieu nen: mot giao vien da khoa, mot hoc sinh co san ──────────────────────────────────────
section "Dung du lieu nen: giao vien da khoa, hoc sinh co san trong lop khac"
TL=$(new_teacher "$ADMIN" "${STAMP}k") || fail_fast "tao giao vien bi khoa"
TL_ID=${TL%%|*}; TL_EMAIL=${TL#*|}
retire_teacher "$ADMIN" "$TL_ID"
GOLD=$(new_class "$ADMIN" "$TEACHER" "Lop Cu SC $STAMP") || fail_fast "tao lop cu"
code=$(call POST /api/students "$ADMIN" "{\"fullName\":\"Hoc sinh Co San\",\"email\":\"sc${STAMP}x@kiemthu.vn\",\"username\":\"SC${STAMP}XX\",\"code\":\"SC${STAMP}XX\",\"password\":\"123456\",\"groupId\":$GOLD}")
[[ "$code" != "200" ]] && fail_fast "tao hoc sinh co san: HTTP $code — $(body)"

# ── File chinh: ba lop hop le, mot lop thieu chu nhiem, mot lop chu nhiem hong, nhieu loi dong ────
xl make "$WORK/school-1.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*|Mon giang day|Email hoc sinh|So dien thoai hoc sinh|Ten dang nhap' <<ROWS
SC${STAMP}01|Hoc sinh Mot|Lop SC A ${STAMP}|GV Chu Nhiem A|gvsca${STAMP}@kiemthu.vn|#912345001|${SUBJ_NAME}|||
SC${STAMP}02|Hoc sinh Hai|Lop SC A ${STAMP}||||||#912345602|
SC${STAMP}03|Hoc sinh Ba|lop sc a ${STAMP}|gv chu nhiem a|GVSCA${STAMP}@kiemthu.vn|0912345001||||
SC${STAMP}04|Hoc sinh Bon|Lop SC B ${STAMP}|GV Chu Nhiem B|gvscb${STAMP}@kiemthu.vn|#912345002||||
SC${STAMP}05|Hoc sinh Nam|Lop SC B ${STAMP}||||||||
SC${STAMP}06|Hoc sinh Sau|Lop SC C ${STAMP}||||||||
SC${STAMP}07|Hoc sinh Bay|Lop SC C ${STAMP}||||||||
SC${STAMP}08|Hoc sinh Tam|Lop SC D ${STAMP}|GV Chu Nhiem D|gvscd${STAMP}@kiemthu.vn|abc|${SUBJ_NAME}|||
SC${STAMP}09|Hoc sinh Chin|Lop SC D ${STAMP}||||||||
SC${STAMP}10|Hoc sinh Muoi|Lop SC A ${STAMP}|GV Khac|gvsckhac${STAMP}@kiemthu.vn|#912345003|${SUBJ_NAME}|||
SC${STAMP}11||Lop SC B ${STAMP}||||||||
SC${STAMP}01|Trung Ma Dong 2|Lop SC B ${STAMP}||||||||
SC${STAMP}12|Hoc sinh Khong Lop|||||||||
SC${STAMP}13|Hoc sinh Email Sai|Lop SC B ${STAMP}|||||email-sai||
SC${STAMP}XX|Hoc sinh Co San|Lop SC A ${STAMP}||||||||
ROWS

# ── Nhap thu ─────────────────────────────────────────────────────────────────────────────────────
section "Nhap thu: dem dung, khong ghi gi, thay duoc lop va giao vien moi tao cung luc"
expect_status "nhap thu → 200" 200 "$(school "$WORK/school-1.xlsx" dryRun=true "subjectId=$SUBJECT_ID")"
expect_json   "  dryRun = true" dryRun true
expect_json   "  15 dong du lieu" totalRows 15
expect_json   "  2 giao vien chu nhiem se tao (A va B)" teachersCreated 2
expect_json   "  2 lop se tao (A va B)" groupsCreated 2
expect_json   "  5 hoc sinh moi (dong 2, 3, 4, 5, 6)" studentsCreated 5
expect_json   "  1 em co san se duoc xep THEM vao lop A (dong 16)" studentsPlaced 1
expect_json   "  9 dong hong" failed 9
expect_status "  cac dong hong la 7,8,9,10,11,12,13,14,15" "7,8,9,10,11,12,13,14,15" "$(failed_rows)"
expect_status "  dong 7: lop C khong co giao vien chu nhiem nao (loi goc)" yes "$(issue_has 7 column 'Email giáo viên')"
expect_status "  dong 8: hoc sinh lop C chua duoc xep, tro ve dong goc 7" yes "$(issue_has 8 message 'xem dòng 7')"
expect_status "  dong 9: so dien thoai giao vien sai (cot goi dung ten)" yes "$(issue_has 9 column 'Số điện thoại giáo viên')"
expect_status "  dong 10: hoc sinh lop D chua duoc xep, tro ve dong goc 9" yes "$(issue_has 10 message 'xem dòng 9')"
expect_status "  dong 11: lop A da co giao vien khac (dong 2)" yes "$(issue_has 11 message 'dòng 2')"
expect_status "  dong 12: thieu ho ten hoc sinh" yes "$(issue_has 12 column 'Họ và tên')"
expect_status "  dong 13: trung ma voi dong 2" yes "$(issue_has 13 message 'dòng 2')"
expect_status "  dong 14: khong ghi lop" yes "$(issue_has 14 column 'Lớp')"
expect_status "  dong 15: email hoc sinh sai (cot goi dung ten)" yes "$(issue_has 15 column 'Email học sinh')"
expect_status "nhap thu KHONG tao giao vien nao (chi con giao vien bi khoa cua nen)" 1 "$(teacher_total)"
expect_status "  khong tao hoc sinh nao" 1 "$(student_total)"
expect_status "  khong tao lop nao (chi co lop cu cua nen)" "Lop Cu SC $STAMP" "$(group_names)"
expect_status "  hoc sinh co san van chi o lop cu" "Lop Cu SC $STAMP" "$(student_groups "SC${STAMP}XX")"
expect_status "  khong tra mat khau hay tai khoan trong ban xem truoc" 0 "$(school "$WORK/school-1.xlsx" dryRun=true "subjectId=$SUBJECT_ID" > /dev/null; J "len(d['accounts'])")"

# ── Danh sach xem truoc de quan tri vien soat ────────────────────────────────────────────────────
section "Danh sach xem truoc: tung lop kem giao vien, mon, hoc sinh (chi gom nhung gi nhap duoc)"
school "$WORK/school-1.xlsx" dryRun=true "subjectId=$SUBJECT_ID" > /dev/null
expect_status "chi hai lop nhap duoc (lop C, D bi chan khong hien)" "Lop SC A $STAMP,Lop SC B $STAMP" "$(J "','.join(g['name'] for g in d['preview'])")"
expect_status "  lop A: chu nhiem, email, mon" "GV Chu Nhiem A|gvsca${STAMP}@kiemthu.vn|$SUBJ_NAME" \
  "$(J "'|'.join((d['preview'][0]['teacherName'], d['preview'][0]['teacherEmail'], d['preview'][0]['subject']))")"
expect_status "  lop A va giao vien deu MOI (chua co trong he thong)" "False False" "$(J "str(d['preview'][0]['exists']) + ' ' + str(d['preview'][0]['teacherExists'])")"
expect_status "  lop A: bon hoc sinh theo dong 2, 3, 4 va 16" "2,3,4,16" "$(J "','.join(str(s['row']) for s in d['preview'][0]['students'])")"
expect_status "  chi em co san (dong 16) duoc danh dau 'da co'" "SC${STAMP}XX" "$(J "','.join(s['code'] for s in d['preview'][0]['students'] if s['exists'])")"
expect_status "  moi em ghi ma va ho ten" yes "$(J "'yes' if all(s['code'] and s['fullName'] for g in d['preview'] for s in g['students']) else 'no'")"
expect_status "  lop B: hai hoc sinh dong 5 va 6" "5,6" "$(J "','.join(str(s['row']) for s in d['preview'][1]['students'])")"
expect_status "  khong co hoc sinh cua dong hong (7 den 15) trong danh sach" 0 "$(J "sum(1 for g in d['preview'] for s in g['students'] if s['row'] in (7,8,9,10,11,12,13,14,15))")"

# ── Nhap that ────────────────────────────────────────────────────────────────────────────────────
section "Nhap that: mat khau chung, mot transaction"
expect_status "thieu mat khau → 400" 400 "$(school "$WORK/school-1.xlsx" dryRun=false "subjectId=$SUBJECT_ID")"
expect_status "  noi ro can mat khau" yes "$(body_has 'mật khẩu ban đầu')"
expect_status "mat khau qua ngan → 400" 400 "$(school "$WORK/school-1.xlsx" dryRun=false "subjectId=$SUBJECT_ID" password=123)"
expect_status "mon chung khong ton tai → 400" 400 "$(school "$WORK/school-1.xlsx" dryRun=false subjectId=999999 "password=$INITIAL_PASSWORD")"
expect_status "CAC LAN BI TU CHOI KHONG GHI GI (rollback ca ba buoc)" "1 1 Lop Cu SC $STAMP" "$(echo "$(teacher_total) $(student_total) $(group_names)")"

expect_status "nhap that → 200" 200 "$(school "$WORK/school-1.xlsx" dryRun=false "subjectId=$SUBJECT_ID" "password=$INITIAL_PASSWORD")"
expect_json   "  dryRun = false" dryRun false
expect_json   "  tao 2 giao vien" teachersCreated 2
expect_json   "  tao 2 lop" groupsCreated 2
expect_json   "  tao 5 hoc sinh" studentsCreated 5
expect_json   "  xep them 1 em co san" studentsPlaced 1
expect_json   "  9 dong hong" failed 9
expect_json   "  khong cong bo lop nao" groupsPublished 0
expect_status "  nhap that khong tra danh sach xem truoc (ket qua la cac con so)" 0 "$(J "len(d['preview'])")"
expect_status "  phieu tai khoan: 2 giao vien + 5 hoc sinh" 7 "$(J "len(d['accounts'])")"
expect_status "  KHONG tra mat khau ve" no "$(body_has "$INITIAL_PASSWORD")"

section "Mot file, ba noi quan ly: giao vien, lop, hoc sinh"
call GET "/api/teachers?search=$STAMP&pageSize=100" "$ADMIN" > /dev/null
expect_status "trang Giao vien: co hai chu nhiem moi (A va B), khong co giao vien loi" 2 "$(J "sum(1 for t in d['items'] if t['email'].startswith('gvsca') or t['email'].startswith('gvscb'))")"
expect_status "  khong co giao vien lop C/D/khac" 0 "$(J "sum(1 for t in d['items'] if t['email'].startswith('gvscd') or t['email'].startswith('gvsckhac'))")"
expect_status "  so dien thoai kieu so mat so 0 duoc tra lai" yes "$(J "'yes' if any(t.get('phone') == '0912345001' for t in d['items']) else 'no'")"
expect_status "  giao vien dang hoat dong, mon dung" yes "$(J "'yes' if all(t['status'] == 'active' and t['subjectId'] == $SUBJECT_ID for t in d['items'] if t['email'].startswith('gvsc')) else 'no'")"
expect_status "trang Nhom lop: co lop A va B, KHONG co lop C va D" "Lop Cu SC $STAMP,Lop SC A $STAMP,Lop SC B $STAMP" "$(group_names)"
call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null
GA=$(J "next(g['id'] for g in d if g['name'] == 'Lop SC A $STAMP')")
GB=$(J "next(g['id'] for g in d if g['name'] == 'Lop SC B $STAMP')")
expect_status "  lop moi la ban nhap (giao vien chua thay)" yes "$(J "'yes' if all(not g['isPublished'] for g in d if g['name'] in ('Lop SC A $STAMP', 'Lop SC B $STAMP')) else 'no'")"
expect_status "  moi lop co dung mot giao vien chu nhiem la nguoi trong file" yes \
  "$(J "'yes' if next(g for g in d if g['name'] == 'Lop SC A $STAMP')['teachers'][0]['email'] == 'gvsca${STAMP}@kiemthu.vn' and next(g for g in d if g['name'] == 'Lop SC B $STAMP')['teachers'][0]['email'] == 'gvscb${STAMP}@kiemthu.vn' else 'no'")"
expect_status "  si so lop A = 4 (3 em moi + 1 em co san)" 4 "$(call GET "/api/student-groups/$GA/students" "$ADMIN" > /dev/null; J "len(d)")"
expect_status "  si so lop B = 2" 2 "$(call GET "/api/student-groups/$GB/students" "$ADMIN" > /dev/null; J "len(d)")"
call GET "/api/student-groups/$GA/history" "$ADMIN" > /dev/null
expect_json   "  lich su lop co dong lap lop" 0.kind created
expect_status "trang Hoc sinh: 5 em moi + em co san = 6 hoc sinh" 6 "$(student_total)"
expect_status "  em 01 o lop A" "Lop SC A $STAMP" "$(student_groups "SC${STAMP}01")"
expect_status "  em 03 (ten lop go khac hoa thuong) cung o lop A" "Lop SC A $STAMP" "$(student_groups "SC${STAMP}03")"
expect_status "  em 04 o lop B" "Lop SC B $STAMP" "$(student_groups "SC${STAMP}04")"
expect_status "  em co san hoc CA lop cu lan lop A (khong roi lop nao)" "Lop Cu SC $STAMP|Lop SC A $STAMP" "$(student_groups "SC${STAMP}XX")"
expect_status "  em bi loi (lop C, lop D, hoc sinh trung ma...) khong duoc tao" 0 \
  "$(call GET "/api/students?search=SC$STAMP&pageSize=100" "$ADMIN" > /dev/null; J "sum(1 for i in d['items'] if i['code'][-2:] in ('06','07','08','09','10','11','12','13'))")"

section "Giao vien va hoc sinh moi dang nhap bang mat khau chung"
expect_status "giao vien chu nhiem dang nhap bang email → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"gvsca${STAMP}@kiemthu.vn\",\"password\":\"$INITIAL_PASSWORD\"}")"
expect_json   "  bi ep doi mat khau" mustChangePassword true
expect_status "hoc sinh dang nhap bang ma → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"SC${STAMP}01\",\"password\":\"$INITIAL_PASSWORD\"}")"
expect_json   "  bi ep doi mat khau" mustChangePassword true
TT=$(login_and_clear_must_change "gvsca${STAMP}@kiemthu.vn" "$INITIAL_PASSWORD" "Gv${STAMP}New")
expect_status "giao vien chu nhiem doi mat khau xong: chua thay lop nao (lop con la ban nhap)" 0 "$(call GET /api/student-groups "$TT" > /dev/null; J "len(d)")"

# ── Gui lai nguyen file ──────────────────────────────────────────────────────────────────────────
section "Gui lai nguyen file: dong da nhap duoc nhan ra, khong bao trung"
expect_status "gui lai khong mat khau → 200" 200 "$(school "$WORK/school-1.xlsx" dryRun=false "subjectId=$SUBJECT_ID")"
expect_json   "  khong tao them giao vien" teachersCreated 0
expect_json   "  khong tao them lop" groupsCreated 0
expect_json   "  khong tao them hoc sinh" studentsCreated 0
expect_json   "  hai giao vien da co duoc nhan ra" teachersUnchanged 2
expect_json   "  hai lop da co duoc nhan ra" groupsUnchanged 2
expect_json   "  nam hoc sinh + em co san da o dung lop duoc nhan ra" studentsUnchanged 6
expect_json   "  9 dong hong van hong" failed 9
expect_status "  van 6 hoc sinh, 2 giao vien" "6 2" "$(echo "$(student_total) $(call GET "/api/teachers?search=$STAMP&pageSize=100" "$ADMIN" > /dev/null; J "sum(1 for t in d['items'] if t['email'].startswith('gvsca') or t['email'].startswith('gvscb'))")")"

# ── Giao vien co san, giao vien bi khoa, cong bo ngay ────────────────────────────────────────────
section "Giao vien co san lam chu nhiem, giao vien bi khoa khong nhan lop, cong bo ngay"
xl make "$WORK/school-2.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*' <<ROWS
SC${STAMP}21|Hoc sinh Hai Mot|Lop SC E ${STAMP}|${DEMO_NAME}|an.nv@anthill.edu.vn|0903112233
SC${STAMP}22|Hoc sinh Hai Hai|Lop SC E ${STAMP}||||
SC${STAMP}23|Hoc sinh Hai Ba|Lop SC F ${STAMP}|GV kiem thu ${STAMP}k|${TL_EMAIL}|0912345678
SC${STAMP}24|Hoc sinh Hai Bon|Lop SC F ${STAMP}||||
SC${STAMP}25|Hoc sinh Hai Nam|Lop SC G ${STAMP}|Ten Khac Hoan Toan|an.nv@anthill.edu.vn|0903112233
ROWS
expect_status "nhap thu → 200" 200 "$(school "$WORK/school-2.xlsx" dryRun=true "subjectId=$SUBJECT_ID" publishGroups=true)"
expect_json   "  hai giao vien co san (dung ten) duoc nhan ra, khong tao them" teachersUnchanged 2
expect_json   "  khong tao giao vien nao" teachersCreated 0
expect_json   "  chi lop E tao duoc" groupsCreated 1
expect_json   "  lop E se duoc cong bo ngay" groupsPublished 1
expect_json   "  hai hoc sinh lop E" studentsCreated 2
expect_json   "  3 dong hong" failed 3
expect_status "  lop F: giao vien bi khoa, loi goc o dong dau cua lop" yes "$(issue_has 4 message 'khoá')"
expect_status "  hoc sinh con lai cua lop F chua duoc xep, tro ve dong 4" yes "$(issue_has 5 message 'xem dòng 4')"
expect_status "  lop G: cung email giao vien co san nhung khac ten → loi dong 6" yes "$(issue_has 6 column 'Giáo viên chủ nhiệm')"
expect_status "nhap that + cong bo → 200" 200 "$(school "$WORK/school-2.xlsx" dryRun=false "subjectId=$SUBJECT_ID" publishGroups=true "password=$INITIAL_PASSWORD")"
expect_json   "  da cong bo 1 lop" groupsPublished 1
call GET "/api/student-groups?search=Lop%20SC%20E%20$STAMP" "$ADMIN" > /dev/null
expect_status "  lop E da cong bo" yes "$(J "'yes' if len(d) == 1 and d[0]['isPublished'] else 'no'")"
expect_status "  giao vien co san thay lop E ngay trong danh sach lop cua minh" yes \
  "$(call GET /api/student-groups "$TEACHER" > /dev/null; J "'yes' if any(g['name'] == 'Lop SC E $STAMP' for g in d) else 'no'")"

# ── Khong co giao vien nao nhap duoc, giao vien co san khong can ghi mon ─────────────────────────
section "Moi giao vien deu hong van tra danh sach loi (khong phai 400), giao vien co san khong can ghi mon"
xl make "$WORK/school-3.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*' <<ROWS
SC${STAMP}31|Hoc sinh Ba Mot|Lop SC H ${STAMP}|GV Hong|gvsch${STAMP}@kiemthu.vn|abc
SC${STAMP}32|Hoc sinh Ba Hai|Lop SC H ${STAMP}||||
ROWS
expect_status "chi co mot giao vien va giao vien hong → 200 (khong phai 400 'file trong')" 200 "$(school "$WORK/school-3.xlsx" dryRun=true "subjectId=$SUBJECT_ID")"
expect_json   "  khong tao gi" teachersCreated 0
expect_json   "  hai dong deu hong" failed 2
expect_status "  loi goc: so dien thoai giao vien (dong 2)" yes "$(issue_has 2 column 'Số điện thoại giáo viên')"
expect_status "  hoc sinh con lai chua duoc xep, tro ve dong 2" yes "$(issue_has 3 message 'xem dòng 2')"

xl make "$WORK/school-4.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*' <<ROWS
SC${STAMP}41|Hoc sinh Bon Mot|Lop SC I ${STAMP}|${DEMO_NAME}|an.nv@anthill.edu.vn|0903112233
ROWS
expect_status "giao vien co san, khong ghi mon, khong chon mon chung → 200" 200 "$(school "$WORK/school-4.xlsx" dryRun=true)"
expect_json   "  giao vien co san duoc nhan ra" teachersUnchanged 1
expect_json   "  lop va hoc sinh moi tao duoc" groupsCreated 1
expect_json   "  khong dong nao hong" failed 0
xl make "$WORK/school-5.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*' <<ROWS
SC${STAMP}51|Hoc sinh Nam Mot|Lop SC K ${STAMP}|GV Moi Khong Mon|gvsck${STAMP}@kiemthu.vn|0912345051
ROWS
expect_status "giao vien MOI khong ghi mon va khong chon mon chung → 200, dong bi tu choi" 200 "$(school "$WORK/school-5.xlsx" dryRun=true)"
expect_status "  loi o cot Mon giang day" yes "$(issue_has 2 column 'Môn giảng dạy')"

# ── Truong gop o cot Lop va cot giao vien cho ca nhom dong (file that hay lam the) ───────────────
section "O gop (merge): lop va giao vien gop cho ca nhom dong van doc dung"
python -X utf8 -W ignore - "$WORK/school-merged.xlsx" "$STAMP" "$SUBJ_NAME" <<'PY'
import sys
import openpyxl
path, stamp, subject = sys.argv[1], sys.argv[2], sys.argv[3]
wb = openpyxl.Workbook()
ws = wb.active
ws.title = 'Học sinh'
ws.append(['Mã học sinh*', 'Họ và tên*', 'Lớp*', 'Giáo viên chủ nhiệm*', 'Email giáo viên*', 'Số điện thoại giáo viên*', 'Môn giảng dạy'])
rows = [
    ('SC%s61' % stamp, 'Hoc sinh Gop Mot'), ('SC%s62' % stamp, 'Hoc sinh Gop Hai'), ('SC%s63' % stamp, 'Hoc sinh Gop Ba'),
    ('SC%s64' % stamp, 'Hoc sinh Gop Bon'), ('SC%s65' % stamp, 'Hoc sinh Gop Nam'),
]
for code, name in rows:
    ws.append([code, name])
# Lop M1: dong 2-4, lop M2: dong 5-6. Chi o tren cung ben trai cua vung gop co gia tri.
for first, last, cls, teacher, email, phone in (
    (2, 4, 'Lop SC M1 %s' % stamp, 'GV Gop Mot', 'gvscm1%s@kiemthu.vn' % stamp, '0912345061'),
    (5, 6, 'Lop SC M2 %s' % stamp, 'GV Gop Hai', 'gvscm2%s@kiemthu.vn' % stamp, 912345062),
):
    for column, value in zip('CDEFG', (cls, teacher, email, phone, subject)):
        ws['%s%d' % (column, first)] = value
        ws.merge_cells('%s%d:%s%d' % (column, first, column, last))
wb.save(path)
PY
expect_status "nhap thu file gop o → 200" 200 "$(school "$WORK/school-merged.xlsx" dryRun=true)"
expect_json   "  5 dong, khong dong nao bi bao 'chua co lop'" failed 0
expect_json   "  2 giao vien, 2 lop, 5 hoc sinh" teachersCreated 2
expect_json   "  hai lop" groupsCreated 2
expect_json   "  nam hoc sinh" studentsCreated 5

# ── Mon ghi trong file ma he thong chua co: nhap thu liet ke, KHONG tao that (khong de lai mon rac) ──
section "Mon moi trong file: xem truoc liet ke, rollback khong de lai gi, mon cua dong hong khong duoc tao"
subject_count() { call GET /api/questions/subjects "$ADMIN" > /dev/null; J "len(d)"; }
SUBJECTS_BEFORE=$(subject_count)
xl make "$WORK/school-subjects.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*|Mon giang day' <<ROWS
SC${STAMP}71|Hoc sinh Mon Mot|Lop SC M1 ${STAMP}|GV Mon A|gvscm1${STAMP}@kiemthu.vn|0912345071|Mon Moi A ${STAMP}
SC${STAMP}72|Hoc sinh Mon Hai|Lop SC M2 ${STAMP}|GV Mon B|gvscm2${STAMP}@kiemthu.vn|0912345072|MON MOI B ${STAMP}
SC${STAMP}73|Hoc sinh Mon Ba|Lop SC M3 ${STAMP}|GV Mon Rac|gvscm3${STAMP}@kiemthu.vn|abc|Mon Rac ${STAMP}
SC${STAMP}74|Hoc sinh Mon Bon|Lop SC M4 ${STAMP}|GV Mon C|gvscm4${STAMP}@kiemthu.vn|0912345074|mon moi a ${STAMP}
SC${STAMP}75|Hoc sinh Mon Nam|Lop SC M5 ${STAMP}|${DEMO_NAME}|an.nv@anthill.edu.vn|0903112233|Mon Khong Tao ${STAMP}
ROWS
expect_status "nhap thu KHONG chon mon chung → 200" 200 "$(school "$WORK/school-subjects.xlsx" dryRun=true)"
expect_status "  liet ke hai mon moi (A dung chung hai giao vien, khong phan biet hoa thuong)" "Mon Moi A $STAMP,MON MOI B $STAMP" "$(J "','.join(d['subjectsCreated'])")"
expect_json   "  ba giao vien moi tao duoc (A, B, C)" teachersCreated 3
expect_json   "  giao vien co san nhan ra, mon ghi tren dong bi bo qua" teachersUnchanged 1
expect_json   "  dong cua giao vien hong (so dien thoai) bi tu choi" failed 1
expect_status "  mon cua giao vien hong va mon ghi cho giao vien co san KHONG nam trong danh sach tao" no "$(body_has 'Mon Rac\|Mon Khong Tao' )"
expect_status "nhap thu la rollback: khong them mon nao vao he thong" "$SUBJECTS_BEFORE" "$(subject_count)"

# ── Ky hoc ghi trong file: tao cung lop, dung chung giua cac lop, xung dot trong mot lop bi tu choi ──
section "Ky hoc trong file: xem truoc liet ke, mot lop mot ky, ky moi chi tao khi nhap that"
term_count() { call GET /api/terms "$ADMIN" > /dev/null; J "len(d)"; }
# Mot ky CO SAN trong he thong: lop trong file dung ky nay phai noi vao ky cu, khong tao them (tung loi PK trung khi gan sai cach).
TERM_EXISTING="Hoc ky SC ${STAMP} co san"
expect_status "tao san mot ky co san → 200" 200 "$(call POST /api/terms "$ADMIN" "{\"name\":\"$TERM_EXISTING\"}")"
TERM_EXISTING_ID=$(json_field id)
TERMS_BEFORE=$(term_count)
TERM_ONE="Hoc ky SC ${STAMP} mot"
TERM_TWO="Hoc ky SC ${STAMP} hai"
xl make "$WORK/school-terms.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop*|Giao vien chu nhiem*|Email giao vien*|So dien thoai giao vien*|Ky hoc' <<ROWS
SC${STAMP}81|Hoc sinh Ky Mot|Lop SC K1 ${STAMP}|GV Ky A|gvsck1${STAMP}@kiemthu.vn|0912345081|${TERM_ONE}
SC${STAMP}82|Hoc sinh Ky Hai|Lop SC K1 ${STAMP}|||
SC${STAMP}83|Hoc sinh Ky Ba|Lop SC K2 ${STAMP}|GV Ky B|gvsck2${STAMP}@kiemthu.vn|0912345082|hoc KY sc ${STAMP} MOT
SC${STAMP}84|Hoc sinh Ky Bon|Lop SC K3 ${STAMP}|GV Ky C|gvsck3${STAMP}@kiemthu.vn|0912345083|
SC${STAMP}85|Hoc sinh Ky Nam|Lop SC K4 ${STAMP}|GV Ky D|gvsck4${STAMP}@kiemthu.vn|0912345084|${TERM_TWO}
SC${STAMP}86|Hoc sinh Ky Sau|Lop SC K4 ${STAMP}||||Ky Khac Xung Dot ${STAMP}
SC${STAMP}87|Hoc sinh Ky Bay|Lop SC K5 ${STAMP}|GV Ky E|gvsck5${STAMP}@kiemthu.vn|0912345085|${TERM_EXISTING}
ROWS
expect_status "nhap thu → 200" 200 "$(school "$WORK/school-terms.xlsx" dryRun=true "subjectId=$SUBJECT_ID")"
expect_status "  liet ke hai ky moi (ky viet hoa thuong khac nhau cua hai lop la cung mot ky)" "$TERM_ONE,$TERM_TWO" "$(J "','.join(d['termsCreated'])")"
expect_json   "  nam lop" groupsCreated 5
expect_status "  dong xung dot ky trong cung lop bi tu choi (dong 7)" yes "$(issue_has 7 column 'Kỳ học')"
expect_json   "  chi mot dong hong" failed 1
expect_status "  xem truoc ghi ky cho tung lop (theo cach viet cua file, khong phan biet hoa thuong)" "${TERM_ONE,,}|${TERM_ONE,,}||${TERM_TWO,,}|${TERM_EXISTING,,}"   "$(J "'|'.join((g.get('termName') or '').lower() for g in sorted(d['preview'], key=lambda g: g['name']))")"
expect_status "nhap thu la rollback: khong them ky nao" "$TERMS_BEFORE" "$(term_count)"

expect_status "nhap that → 200" 200 "$(school "$WORK/school-terms.xlsx" dryRun=false "subjectId=$SUBJECT_ID" "password=$INITIAL_PASSWORD")"
expect_status "  tao hai ky moi" "$TERM_ONE,$TERM_TWO" "$(J "','.join(d['termsCreated'])")"
call GET /api/terms "$ADMIN" > /dev/null
expect_status "  danh sach ky co them hai ky" "$((TERMS_BEFORE + 2))" "$(J "len(d)")"
expect_status "  ky tao tu file khong phai ky hien tai" False "$(J "any(t['isCurrent'] for t in d if '$STAMP' in t['name'])")"
expect_status "  ky mot co hai lop (hai lop dung chung ky)" 2 "$(J "next(t for t in d if t['name'] == '$TERM_ONE')['groupCount']")"
expect_status "  ky hai co mot lop" 1 "$(J "next(t for t in d if t['name'] == '$TERM_TWO')['groupCount']")"
expect_status "  ky co san duoc dung lai (mot lop, khong sinh ky trung)" "1 1" "$(J "str(next(t for t in d if t['id'] == $TERM_EXISTING_ID)['groupCount']) + ' ' + str(sum(1 for t in d if t['name'] == '$TERM_EXISTING'))")"
call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null
expect_status "  lop gan dung ky (lop khong ghi ky thi de trong)" "$TERM_ONE|$TERM_ONE||$TERM_TWO|$TERM_EXISTING" \
  "$(J "'|'.join((g.get('termName') or '') for g in sorted((g for g in d if g['name'].startswith('Lop SC K')), key=lambda g: g['name']))")"
expect_status "gui lai nguyen file: khong tao them ky" 200 "$(school "$WORK/school-terms.xlsx" dryRun=false "subjectId=$SUBJECT_ID" "password=$INITIAL_PASSWORD")"
expect_status "  danh sach ky khong dai ra" "$((TERMS_BEFORE + 2))" "$(term_count)"

summary
