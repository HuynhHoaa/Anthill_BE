#!/usr/bin/env bash
# Nhap hoc sinh, giao vien va lop tu file Excel — chi quan tri vien.
#
# Chay:  bash tests/api-checks/excel-import.sh   (API phai dang chay o $HOST, database thu; can python co openpyxl)
#
# Moi ky co vai nghin hoc sinh, khong ai nhap tay. Bo kiem nay khoa lai nhung dieu ma unit test khong cham toi duoc vi can
# HTTP + database that + file .xlsx that:
#   - chi quan tri vien tai file mau va nhap; giao vien, hoc sinh bi tu choi, khach bi chan;
#   - file mau tai ve roi dien thang vao (dung luong nguoi dung lam that) van doc duoc, tieu de co dau * van nhan ra;
#   - file xau bi tu choi bang cau tieng Viet, khong phai loi 500: khong phai Excel, thieu cot bat buoc, chi co tieu de,
#     bom zip, qua dung luong, qua so dong;
#   - nhap thu (dryRun) dem dung va KHONG ghi gi; nhap that ghi dung; dong hong khong lam hong ca me, duoc liet ke kem so
#     dong va ly do; danh sach loi bi cat o 500 nhung so dong hong van dem du;
#   - hoc sinh: ma la khoa nhan dien, ma / ten dang nhap / email dung chung mot khong gian ten voi moi tai khoan khac;
#     hoc sinh da co chi duoc xep THEM lop (khong bao gio ghi de ho so), khac ten thi bao loi; em dang o lop khac van duoc xep
#     them (mot em hoc nhieu lop cung luc), em da lam bai cung vay; lop da khoa thi dong do bao loi; mat khau chung cho ca dot va
#     ep doi;
#     gui lai nguyen file thi cac dong da nhap duoc nhan ra (khong bao trung, khong doi hoi mat khau);
#   - giao vien: email la ten dang nhap, mon chon theo ten (khong dau) hoac ma, mon chung cho dong de trong;
#   - lop: moi lop co dung mot giao vien ghi tren dong; giao vien bi khoa bi tu choi; lop da co giao vien khac thi bao loi
#     (doi giao vien phai ghi ly do, lam o trang lop); lop moi la ban nhap va co dong lich su "lap lop".

source "$(dirname "$0")/lib.sh"

ADMIN=$(login "huy.pq@anthill.edu.vn" "123456")
TEACHER=$(login "an.nv@anthill.edu.vn" "123456")
STUDENT=$(login "lan.tt" "123456")

STAMP=$(date +%H%M%S)
EXAM_IDS=()
INITIAL_PASSWORD="Import$STAMP"

# Duong dan dang C:/... de ca bash, curl lan python (ban Windows) cung hieu.
WORK=$(cygpath -m "$(mktemp -d -t anthill-xlsx.XXXXXX)")
TOOL=$(cygpath -m "$(cd "$(dirname "$0")" && pwd)/xlsx_tool.py")
XLSX_TYPE='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

fail_fast() { echo "  Dung du lieu nen that bai: $1" >&2; exit 1; }

# Dung file .xlsx: xl make|fill|info|rows|bomb|junk ... (xem xlsx_tool.py)
xl() { python -X utf8 -W ignore "$TOOL" "$@"; }

# Gui file len mot endpoint nhap: upload PATH TOKEN FILE [truong=gia tri ...]. FILE rong thi khong gui file.
upload() {
  local path="$1" token="$2" file="$3"; shift 3
  local args=(-s -o "$BODY_FILE" -w '%{http_code}' -X POST "$HOST$path")
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$file" ]] && args+=(-F "file=@$file;type=$XLSX_TYPE")
  local field
  for field in "$@"; do args+=(-F "$field"); done
  curl "${args[@]}"
}

J() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$(to_native_path "$BODY_FILE")"; }
body_has() { if body | grep -q -- "$1"; then echo yes; else echo no; fi; }

# Dong ROW trong "issues" co truong FIELD (column hoac message) chua NEEDLE khong.
issue_has() {
  python -X utf8 - "$(to_native_path "$BODY_FILE")" "$1" "$2" "$3" <<'PY'
import json, sys
path, row, field, needle = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
d = json.load(open(path, encoding='utf-8'))
hit = [i for i in d['issues'] if i['row'] == row]
print('yes' if hit and needle in (hit[0].get(field) or '') else 'no ' + json.dumps(hit, ensure_ascii=True)[:200])
PY
}
failed_rows() { J "','.join(str(i['row']) for i in d['issues'])"; }

# Doc mot truong cua hoc sinh theo ma: student_of MA BIEU_THUC (bien i la ban ghi hoc sinh).
student_of() {
  call GET "/api/students?search=$1&pageSize=100" "$ADMIN" > /dev/null
  J "next((lambda i: $2)(i) for i in d['items'] if i['code'] == '$1')"
}
student_id() { student_of "$1" "i['id']"; }
# Id cac lop cua em (bien i trong student_of), tang dan, noi bang dau phay; "none" khi em chua co lop nao.
GROUPS_EXPR="','.join(str(x) for x in sorted(g['id'] for g in i['groups'])) or 'none'"
student_total() { call GET "/api/students?search=IM$STAMP&pageSize=100" "$ADMIN" > /dev/null; json_field total; }

new_student() { # $1 hau to ma, $2 ten, $3 id lop (tuy chon)
  local group="" c
  [[ -n "${3:-}" ]] && group=",\"groupId\":$3"
  c=$(call POST /api/students "$ADMIN" \
    "{\"fullName\":\"$2\",\"email\":\"im${STAMP}$1@kiemthu.vn\",\"username\":\"IM${STAMP}$1\",\"code\":\"IM${STAMP}$1\",\"password\":\"123456\"$group}")
  [[ "$c" != "200" ]] && fail_fast "tao hoc sinh $1: HTTP $c — $(body)"
  json_field id
}

cleanup() {
  section "Don du lieu"
  local line sid gid
  for id in "${EXAM_IDS[@]}"; do call DELETE "/api/exams/$id" "$TEACHER" > /dev/null; done

  # Lop khoa la lop chi doc nen mo khoa truoc (409 neu lop dang mo — khong sao), roi moi go duoc thanh vien.
  call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null
  while IFS= read -r gid; do
    [[ -n "$gid" ]] && call POST "/api/student-groups/$gid/unlock" "$ADMIN" > /dev/null
  done < <(J "'\n'.join(str(g['id']) for g in d)")

  # Hoc sinh: go khoi moi lop roi khoa. Tim theo tien to ma nen don duoc ca khi script chet giua chung.
  call GET "/api/students?search=IM$STAMP&pageSize=100" "$ADMIN" > /dev/null
  while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    sid=${line%% *}
    for gid in ${line#* }; do
      [[ "$gid" != "0" ]] && call DELETE "/api/student-groups/$gid/students/$sid" "$ADMIN" "$REMOVE_BODY" > /dev/null
    done
    call PATCH "/api/students/$sid/status" "$ADMIN" "$LOCK_BODY" > /dev/null
  done < <(J "'\n'.join(str(i['id']) + ' ' + (' '.join(str(g['id']) for g in i['groups']) or '0') for i in d['items'])")

  # Lop: moi lop tao trong bo kiem co STAMP trong ten (ke ca lop do chinh viec nhap tao ra).
  call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null
  local removed=0 total=0
  while IFS= read -r gid; do
    [[ -z "$gid" ]] && continue
    total=$((total + 1))
    [[ "$(call DELETE "/api/student-groups/$gid" "$ADMIN")" == "200" ]] && removed=$((removed + 1))
  done < <(J "'\n'.join(str(g['id']) for g in d)")

  # Giao vien: khong xoa duoc (so huu cau hoi, de) nen khoa lai.
  call GET "/api/teachers?search=$STAMP&pageSize=100" "$ADMIN" > /dev/null
  local locked=0
  while IFS= read -r sid; do
    [[ -z "$sid" ]] && continue
    retire_teacher "$ADMIN" "$sid"; locked=$((locked + 1))
  done < <(J "'\n'.join(str(t['id']) for t in d['items'])")

  rm -rf "$WORK"
  printf '  xoa %d/%d lop, khoa %d giao vien kiem thu\n' "$removed" "$total" "$locked"
}
trap cleanup EXIT

# ── Du lieu nen ──────────────────────────────────────────────────────────────────────────────────
section "Dung du lieu: hai lop, ba hoc sinh lam tay"
GA=$(new_class "$ADMIN" "$TEACHER" "Lop A IM $STAMP") || fail_fast "tao lop A"
GB=$(new_class "$ADMIN" "$TEACHER" "Lop B IM $STAMP") || fail_fast "tao lop B"
H1=$(new_student H1 "Hoc sinh tay mot")
H2=$(new_student H2 "Hoc sinh tay hai" "$GB")
H3=$(new_student H3 "Hoc sinh tay ba")
expect_status "3 hoc sinh lam tay" 3 "$(student_total)"

# ── Phan quyen va file mau ───────────────────────────────────────────────────────────────────────
section "Chi quan tri vien tai file mau va nhap"
for kind in students teachers groups; do
  expect_status "$kind: khach tai mau → 401" 401 "$(call GET "/api/imports/$kind/template")"
  expect_status "$kind: hoc sinh tai mau → 403" 403 "$(call GET "/api/imports/$kind/template" "$STUDENT")"
  expect_status "$kind: giao vien tai mau → 403" 403 "$(call GET "/api/imports/$kind/template" "$TEACHER")"
  expect_status "$kind: giao vien nhap → 403" 403 "$(upload "/api/imports/$kind" "$TEACHER" "" dryRun=true)"
  expect_status "$kind: hoc sinh nhap → 403" 403 "$(upload "/api/imports/$kind" "$STUDENT" "" dryRun=true)"
  expect_status "$kind: khach nhap → 401" 401 "$(upload "/api/imports/$kind" "" "" dryRun=true)"
done

section "File mau hoc sinh"
code=$(curl -s -D "$WORK/headers.txt" -o "$WORK/tpl-students.xlsx" -w '%{http_code}' -H "Authorization: Bearer $ADMIN" "$HOST/api/imports/students/template")
expect_status "admin tai mau → 200" 200 "$code"
expect_status "  dung kieu noi dung xlsx" yes "$(grep -qi 'spreadsheetml.sheet' "$WORK/headers.txt" && echo yes || echo no)"
expect_status "  la goi zip (PK)" PK "$(head -c 2 "$WORK/tpl-students.xlsx")"
xl info "$WORK/tpl-students.xlsx" > "$WORK/tpl-students.json"
K() { python -X utf8 -c "import json,sys; d=json.load(open(sys.argv[1],encoding='utf-8')); print($1)" "$WORK/$2"; }
expect_status "  trang du lieu + trang danh sach + trang huong dan" "Học sinh,Danh sách,Hướng dẫn" "$(K "','.join(d['sheets'])" tpl-students.json)"
expect_status "  sau cot co dau * o hai cot bat buoc" "Mã học sinh*,Họ và tên*,Lớp,Email,Số điện thoại,Tên đăng nhập" "$(K "','.join(d['header'])" tpl-students.json)"
expect_status "  trang danh sach liet ke lop dang co" yes "$(K "'yes' if 'Lop A IM $STAMP' in d['lists'].get('Lớp', []) and 'Lop B IM $STAMP' in d['lists'].get('Lớp', []) else 'no'" tpl-students.json)"
expect_status "  trang huong dan co noi dung" yes "$(K "'yes' if len(d['guideLines']) > 5 else 'no'" tpl-students.json)"

section "File mau giao vien va lop"
expect_status "mau giao vien → 200" 200 "$(curl -s -o "$WORK/tpl-teachers.xlsx" -w '%{http_code}' -H "Authorization: Bearer $ADMIN" "$HOST/api/imports/teachers/template")"
xl info "$WORK/tpl-teachers.xlsx" > "$WORK/tpl-teachers.json"
expect_status "  cot cua mau giao vien (so dien thoai bat buoc)" "Họ và tên*,Email*,Số điện thoại*,Môn giảng dạy" "$(K "','.join(d['header'])" tpl-teachers.json)"
expect_status "  co danh sach mon" yes "$(K "'yes' if d['lists'].get('Môn giảng dạy') else 'no'" tpl-teachers.json)"
expect_status "mau lop → 200" 200 "$(curl -s -o "$WORK/tpl-groups.xlsx" -w '%{http_code}' -H "Authorization: Bearer $ADMIN" "$HOST/api/imports/groups/template")"
xl info "$WORK/tpl-groups.xlsx" > "$WORK/tpl-groups.json"
expect_status "  cot cua mau lop" "Tên lớp*,Giáo viên phụ trách*,Mô tả" "$(K "','.join(d['header'])" tpl-groups.json)"
expect_status "  danh sach giao vien la email giao vien dang hoat dong" yes "$(K "'yes' if 'an.nv@anthill.edu.vn' in d['lists'].get('Giáo viên phụ trách', []) else 'no'" tpl-groups.json)"

# ── File xau ─────────────────────────────────────────────────────────────────────────────────────
section "File xau bi tu choi bang cau tieng Viet"
expect_status "khong gui file → 400" 400 "$(upload /api/imports/students "$ADMIN" "" dryRun=true)"
expect_status "  noi ro chua chon file" yes "$(body_has 'Chưa chọn file')"

echo "day khong phai Excel" > "$WORK/fake.xlsx"
expect_status "file van ban doi duoi .xlsx → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/fake.xlsx" dryRun=true)"
expect_status "  noi ro khong phai Excel" yes "$(body_has 'không phải Excel')"

xl junk "$WORK/random.xlsx" --mb 1
expect_status "byte ngau nhien doi duoi .xlsx → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/random.xlsx" dryRun=true)"

printf 'Mot nguoi|Lop A\n' | xl make "$WORK/no-code.xlsx" --header 'Ho va ten*|Lop'
expect_status "thieu cot bat buoc (Ma hoc sinh) → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/no-code.xlsx" dryRun=true)"
expect_status "  noi ro cot nao thieu" yes "$(body_has 'Không tìm thấy cột bắt buộc: Mã học sinh')"

printf '' | xl make "$WORK/header-only.xlsx" --header 'Ma hoc sinh*|Ho va ten*'
expect_status "chi co hang tieu de → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/header-only.xlsx" dryRun=true)"
expect_status "  noi ro khong co dong du lieu" yes "$(body_has 'không có dòng dữ liệu')"

printf 'ZZ%s1|Mot nguoi\n' "$STAMP" | xl make "$WORK/ok-small.xlsx" --header 'Ma hoc sinh*|Ho va ten*'
xl bomb "$WORK/ok-small.xlsx" "$WORK/bomb.xlsx" --mb 120
expect_status "bom zip (nen nho, phinh 120 MB) → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/bomb.xlsx" dryRun=true)"
expect_status "  noi ro qua lon sau khi giai nen" yes "$(body_has 'quá lớn sau khi giải nén')"

xl junk "$WORK/big-file.xlsx" --mb 6
expect_status "file 6 MB → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/big-file.xlsx" dryRun=true)"
expect_status "  noi ro tran 5 MB" yes "$(body_has 'tối đa 5 MB')"

xl rows "$WORK/rows-5001.xlsx" --header 'Ma hoc sinh*|Ho va ten*' --count 5001 --template "BK$STAMP{i}|Hoc sinh so {i}"
expect_status "5001 dong → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/rows-5001.xlsx" dryRun=true)"
expect_status "  noi ro tran 5000 dong" yes "$(body_has 'hơn 5000 dòng')"

section "File thuc te: dong tieu de phia tren, trang huong dan dat truoc, ten cot kieu khac"
printf 'ZZ%s1|Mot nguoi\nZZ%s2|\n' "$STAMP" "$STAMP" | xl make "$WORK/padded.xlsx" --header 'Ma hoc sinh*|Ho va ten*' --pad 3 --decoy
expect_status "tieu de nam o dong 4, trang du lieu o sau trang huong dan → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/padded.xlsx" dryRun=true)"
expect_json   "  doc duoc hai dong du lieu" totalRows 2
expect_json   "  dong dau la tao moi" created 1
expect_json   "  dong sau thieu ten" failed 1
expect_status "  loi mang SO DONG THAT trong Excel (6), khong phai chi so trong danh sach" 6 "$(J "d['issues'][0]['row']")"
printf 'ZZ%s2|Hai nguoi\n' "$STAMP" | xl make "$WORK/aliases.xlsx" --header 'MSHS|Họ tên'
expect_status "ten cot khac (MSHS, Ho ten) → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/aliases.xlsx" dryRun=true)"
expect_json   "  van nhan ra" created 1

# ── Hoc sinh: nhap thu ───────────────────────────────────────────────────────────────────────────
section "Hoc sinh: nhap thu (dryRun) dem dung va khong ghi gi"
xl make "$WORK/students-1.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop|Email|So dien thoai|Ten dang nhap' <<ROWS
IM${STAMP}01|Nguyen Van An|lop a im ${STAMP}|an${STAMP}@kiemthu.vn|#912345678|
IM${STAMP}02|Tran Thi Binh||||
IM${STAMP}03||Lop A IM ${STAMP}|||
IM${STAMP}01|Nguyen Van An Trung|Lop A IM ${STAMP}|||
IM${STAMP}04|Le Van Cuong|Lop khong ton tai|||
IM${STAMP}05|Pham Thi Dung||khong-phai-email||
IM${STAMP}06|Hoang Van Em|||abc|
IM${STAMP}07|Vu Thi Phuong|Lop B IM ${STAMP}|||
IM${STAMP}H1|Hoc sinh tay mot|Lop A IM ${STAMP}|||
IM${STAMP}H2|Hoc sinh tay hai|Lop A IM ${STAMP}|||
IM${STAMP}H3|Ten sai hoan toan|Lop A IM ${STAMP}|||
huy.pq@anthill.edu.vn|Gia mao|Lop A IM ${STAMP}|||
IM${STAMP}08|Do Van Giang||||lan.tt
IM${STAMP}09|~Nguy\u1ec5n V\u0103n H\u00f9ng|Lop A IM ${STAMP}|||
IM${STAMP}10|=1+1||||
ROWS

expect_status "nhap thu khong can mat khau → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/students-1.xlsx" dryRun=true "groupId=$GB")"
expect_json   "  dryRun = true" dryRun true
expect_json   "  15 dong du lieu" totalRows 15
expect_json   "  4 tai khoan se tao (dong 2, 3, 9, 15)" created 4
expect_json   "  2 em co san se duoc xep them lop (dong 10 va 11)" placed 2
expect_json   "  chua ai o nguyen vi tri" unchanged 0
expect_json   "  9 dong hong" failed 9
expect_json   "  danh sach loi khong bi cat" issuesTruncated false
expect_status "  cac dong hong la 4,5,6,7,8,12,13,14,16" "4,5,6,7,8,12,13,14,16" "$(failed_rows)"
expect_status "  dong 4: thieu ho ten" yes "$(issue_has 4 column 'Họ và tên')"
expect_status "  dong 5: trung ma voi dong 2 trong file" yes "$(issue_has 5 message 'dòng 2')"
expect_status "  dong 6: lop khong ton tai" yes "$(issue_has 6 column 'Lớp')"
expect_status "  dong 7: email sai dinh dang" yes "$(issue_has 7 column 'Email')"
expect_status "  dong 8: so dien thoai sai" yes "$(issue_has 8 column 'Số điện thoại')"
expect_status "  dong 12: cung ma khac ho ten (nghi go nham ma)" yes "$(issue_has 12 message 'đã thuộc học sinh')"
expect_status "  dong 13: ma trung email cua tai khoan khac" yes "$(issue_has 13 message 'tài khoản khác')"
expect_status "  dong 14: ten dang nhap da co nguoi dung" yes "$(issue_has 14 column 'Tên đăng nhập')"
expect_status "  dong 16: o cong thuc khong duoc tinh (ho ten rong)" yes "$(issue_has 16 column 'Họ và tên')"
expect_status "nhap thu KHONG tao hoc sinh nao" 3 "$(student_total)"
expect_status "  em lam tay chua co lop van chua co lop" none "$(student_of "IM${STAMP}H1" "$GROUPS_EXPR")"
expect_status "  em lam tay dang o lop B van chi o lop B" "$GB" "$(student_of "IM${STAMP}H2" "$GROUPS_EXPR")"

# ── Hoc sinh: nhap that ──────────────────────────────────────────────────────────────────────────
section "Hoc sinh: nhap that can mat khau khi co tai khoan moi"
expect_status "thieu mat khau → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/students-1.xlsx" dryRun=false "groupId=$GB")"
expect_status "  noi ro can mat khau ban dau" yes "$(body_has 'mật khẩu ban đầu')"
expect_status "mat khau qua ngan → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/students-1.xlsx" dryRun=false "groupId=$GB" password=123)"
expect_status "lop mac dinh khong ton tai → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/students-1.xlsx" dryRun=false groupId=999999 "password=$INITIAL_PASSWORD")"
expect_status "van chua tao gi sau cac lan bi tu choi" 3 "$(student_total)"

section "Hoc sinh: nhap that"
expect_status "nhap that → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/students-1.xlsx" dryRun=false "groupId=$GB" "password=$INITIAL_PASSWORD")"
expect_json   "  dryRun = false" dryRun false
expect_json   "  tao 4 tai khoan" created 4
expect_json   "  xep them 2 em co san" placed 2
expect_json   "  9 dong hong" failed 9
expect_status "  tra ve 4 tai khoan de in phieu" 4 "$(J "len(d['accounts'])")"
expect_status "  moi tai khoan co ten dang nhap, ma va lop" yes "$(J "'yes' if all(a['username'] and a['code'] and a['group'] for a in d['accounts']) else 'no'")"
expect_status "  KHONG tra mat khau ve" no "$(body_has "$INITIAL_PASSWORD")"
expect_status "co 7 hoc sinh (3 lam tay + 4 moi)" 7 "$(student_total)"
call GET "/api/students?search=IM$STAMP&pageSize=100" "$ADMIN" > /dev/null
expect_status "dong hong khong tao tai khoan (khong co IM03..06, IM08, IM10)" 0 \
  "$(J "sum(1 for i in d['items'] if i['code'][-2:] in ('03', '04', '05', '06', '08', '10'))")"

section "Hoc sinh: du lieu duoc ghi dung"
expect_status "IM01 o lop A (khop ten lop khong phan biet hoa thuong)" "$GA" "$(student_of "IM${STAMP}01" "$GROUPS_EXPR")"
expect_status "  so dien thoai kieu so mat so 0 duoc tra lai" "0912345678" "$(student_of "IM${STAMP}01" "i['phone']")"
expect_status "  email nhap duoc giu nguyen" "an${STAMP}@kiemthu.vn" "$(student_of "IM${STAMP}01" "i['email']")"
expect_status "  ten dang nhap mac dinh la ma" "IM${STAMP}01" "$(student_of "IM${STAMP}01" "i['username']")"
expect_status "IM02 khong ghi lop → vao lop mac dinh (lop B)" "$GB" "$(student_of "IM${STAMP}02" "$GROUPS_EXPR")"
expect_status "  khong co email → email tam, ro rang khong phai hop thu that" yes "$(student_of "IM${STAMP}02" "'yes' if i['email'].endswith('@student.anthill.local') else 'no'")"
expect_status "IM07 o lop B" "$GB" "$(student_of "IM${STAMP}07" "$GROUPS_EXPR")"
expect_status "IM09: ten dang to hop (NFD) duoc luu dang NFC" yes "$(student_of "IM${STAMP}09" "'yes' if i['fullName'] == 'Nguy\u1ec5n V\u0103n H\u00f9ng' else 'no'")"
expect_status "em lam tay H1 duoc xep vao lop A" "$GA" "$(student_of "IM${STAMP}H1" "$GROUPS_EXPR")"
expect_status "  ho so cua H1 khong bi ghi de (ten van la ten cu)" "Hoc sinh tay mot" "$(student_of "IM${STAMP}H1" "i['fullName']")"
expect_status "H2 dang o lop B duoc xep THEM vao lop A (dong 11), van o lop B" "$GA,$GB" "$(student_of "IM${STAMP}H2" "$GROUPS_EXPR")"
expect_status "H3 van chua co lop (dong 12 bi tu choi)" none "$(student_of "IM${STAMP}H3" "$GROUPS_EXPR")"
expect_status "  H3 giu ten cu, khong bi ghi de" "Hoc sinh tay ba" "$(student_of "IM${STAMP}H3" "i['fullName']")"
call GET "/api/student-groups/$GA/students" "$ADMIN" > /dev/null
expect_status "lop A co 4 em (IM01, IM09, H1, H2)" 4 "$(python -c "import json,sys;print(len(json.load(sys.stdin)))" < "$BODY_FILE")"
call GET "/api/student-groups/$GB/students" "$ADMIN" > /dev/null
expect_status "lop B co 3 em (H2, IM02, IM07)" 3 "$(python -c "import json,sys;print(len(json.load(sys.stdin)))" < "$BODY_FILE")"

section "Hoc sinh: mat khau chung, ep doi, lich su"
expect_status "dang nhap bang ma + mat khau chung → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"IM${STAMP}01\",\"password\":\"$INITIAL_PASSWORD\"}")"
expect_json   "  bi ep doi mat khau" mustChangePassword true
expect_status "dang nhap bang email + mat khau chung → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"an${STAMP}@kiemthu.vn\",\"password\":\"$INITIAL_PASSWORD\"}")"
expect_status "em khong co email that dang nhap bang ma → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"IM${STAMP}02\",\"password\":\"$INITIAL_PASSWORD\"}")"
expect_status "sai mat khau → 401" 401 "$(call POST /api/auth/login "" "{\"username\":\"IM${STAMP}01\",\"password\":\"sai-mat-khau\"}")"
SID1=$(student_id "IM${STAMP}01")
expect_status "lich su cua em moi: co dong xep vao lop" 200 "$(call GET "/api/students/$SID1/history" "$ADMIN")"
expect_json   "  loai = xep vao lop" 0.kind addedToGroup
expect_status "  ghi ten lop dich" yes "$(J "'yes' if 'Lop A IM $STAMP' in (d[0].get('toGroup') or '') else 'no'")"
expect_status "  ghi nguoi thuc hien" yes "$(J "'yes' if d[0].get('actorName') else 'no'")"
expect_status "lich su cua em co san duoc xep lop" 200 "$(call GET "/api/students/$H1/history" "$ADMIN")"
expect_json   "  loai = xep vao lop" 0.kind addedToGroup

# ── Gui lai nguyen file ──────────────────────────────────────────────────────────────────────────
section "Gui lai nguyen file: dong da nhap duoc nhan ra, khong can mat khau"
expect_status "gui lai khong mat khau → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/students-1.xlsx" dryRun=false "groupId=$GB")"
expect_json   "  khong tao them" created 0
expect_json   "  khong xep them" placed 0
expect_json   "  6 dong da nhap duoc nhan ra (dong 2, 3, 9, 10, 11, 15)" unchanged 6
expect_json   "  9 dong hong van hong" failed 9
expect_status "  van 7 hoc sinh" 7 "$(student_total)"

# ── Em da lam bai va lop da khoa ────────────────────────────────────────────────────────────────
section "Em da lam bai duoc xep vao lop nao cung duoc"
code=$(call POST /api/exams "$TEACHER" "$(cat <<JSON
{
  "title": "De nhap Excel $STAMP", "topic": "", "durationMinutes": 45, "totalQuestions": 9,
  "easyPercentage": 45, "mediumPercentage": 33, "hardPercentage": 22, "pointEasy": 10, "pointMedium": 10, "pointHard": 15,
  "shuffleQuestions": true, "shuffleAnswers": true, "showResultAfterSubmit": true,
  "openAt": "2020-01-01T00:00:00Z", "closeAt": "2035-01-01T00:00:00Z"
}
JSON
)")
[[ "$code" != "200" ]] && fail_fast "tao de: HTTP $code — $(body)"
EID=$(json_field id); EXAM_IDS+=("$EID")
code=$(call POST "/api/exams/$EID/assignments" "$TEACHER" "{\"studentIds\":[],\"groupIds\":[$GB],\"notify\":false}")
[[ "$code" != "200" ]] && fail_fast "giao bai: HTTP $code — $(body)"
TOK2=$(login_and_clear_must_change "IM${STAMP}02" "$INITIAL_PASSWORD" "Pw${STAMP}x")
call GET /api/my/exams "$TOK2" > /dev/null
AID2=$(J "next(x['assignmentId'] for x in d if x['examId'] == $EID)")
expect_status "IM02 bat dau lam bai → 200" 200 "$(call POST /api/exam-attempts "$TOK2" "{\"assignmentId\":$AID2}")"
SID2=$(student_id "IM${STAMP}02")
expect_status "go IM02 khoi lop B kem ly do → 200" 200 "$(call DELETE "/api/student-groups/$GB/students/$SID2" "$ADMIN" '{"reason":"Em nghi hoc tam thoi de chua benh"}')"

printf 'IM%s02|Tran Thi Binh|Lop A IM %s\n' "$STAMP" "$STAMP" | xl make "$WORK/rule-a.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop'
expect_status "em da lam bai chua co lop → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/rule-a.xlsx" dryRun=false)"
expect_json   "  xep duoc vao lop A (khong phai lop cu)" placed 1
expect_json   "  khong dong hong" failed 0
expect_status "  em o lop A" "$GA" "$(student_of "IM${STAMP}02" "$GROUPS_EXPR")"

printf 'IM%s02|Tran Thi Binh|Lop B IM %s\n' "$STAMP" "$STAMP" | xl make "$WORK/rule-b.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop'
expect_status "xep them vao lop cu (lop B) → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/rule-b.xlsx" dryRun=false)"
expect_json   "  xep duoc 1 em" placed 1
expect_json   "  khong dong hong" failed 0
expect_status "  em hoc ca lop A va lop B" "$GA,$GB" "$(student_of "IM${STAMP}02" "$GROUPS_EXPR")"

section "Lop da khoa: dong ghi lop do bi tu choi, lop khac van nhan"
expect_status "khoa lop A → 200" 200 "$(call POST "/api/student-groups/$GA/lock" "$ADMIN" '{"reason":"Lop A tam khoa de kiem thu nhap Excel"}')"
printf 'IM%s11|Hoc sinh moi vao lop khoa|Lop A IM %s\nIM%sH3|Hoc sinh tay ba|Lop A IM %s\nIM%s02|Tran Thi Binh|Lop B IM %s\n' \
  "$STAMP" "$STAMP" "$STAMP" "$STAMP" "$STAMP" "$STAMP" | xl make "$WORK/locked.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop'
expect_status "nhap file co dong ghi lop khoa → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/locked.xlsx" dryRun=false)"
expect_json   "  khong tao tai khoan nao" created 0
expect_json   "  khong xep them ai" placed 0
expect_json   "  1 dong da dung cho (IM02 da hoc lop B)" unchanged 1
expect_json   "  2 dong hong" failed 2
expect_status "  cac dong hong la 2 va 3" "2,3" "$(failed_rows)"
expect_status "  dong 2: em moi vao lop khoa bi tu choi" yes "$(issue_has 2 message 'đã khoá')"
expect_status "  dong 3: em co san vao lop khoa cung bi tu choi" yes "$(issue_has 3 message 'đã khoá')"
expect_status "  em moi khong duoc tao" 7 "$(student_total)"
expect_status "chon lop mac dinh la lop khoa → 400" 400 "$(upload /api/imports/students "$ADMIN" "$WORK/rule-b.xlsx" dryRun=true "groupId=$GA")"
expect_status "  noi ro lop da khoa" yes "$(body_has 'đã khoá')"
expect_status "mo khoa lop A → 200" 200 "$(call POST "/api/student-groups/$GA/unlock" "$ADMIN")"
printf 'IM%sH3|Hoc sinh tay ba|Lop A IM %s\n' "$STAMP" "$STAMP" | xl make "$WORK/unlocked.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop'
expect_status "sau khi mo khoa, nhap lai dong cua H3 → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/unlocked.xlsx" dryRun=false)"
expect_json   "  xep duoc vao lop A" placed 1
expect_status "  H3 o lop A" "$GA" "$(student_of "IM${STAMP}H3" "$GROUPS_EXPR")"

# ── Danh sach loi bi cat ─────────────────────────────────────────────────────────────────────────
section "Danh sach loi bi cat o 500 nhung so dong hong dem du"
xl rows "$WORK/all-bad.xlsx" --header 'Ma hoc sinh*|Ho va ten*|Lop' --count 600 --template "ZZ$STAMP{i}||"
expect_status "600 dong deu hong → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/all-bad.xlsx" dryRun=true)"
expect_json   "  dem du 600 dong hong" failed 600
expect_status "  chi tra ve 500 loi" 500 "$(J "len(d['issues'])")"
expect_json   "  danh dau bi cat" issuesTruncated true
expect_status "  loi sap theo so dong" 2 "$(J "d['issues'][0]['row']")"

section "Co 5000 dong van doc duoc, khong cham"
xl rows "$WORK/rows-5000.xlsx" --header 'Ma hoc sinh*|Ho va ten*' --count 5000 --template "BK$STAMP{i}|Hoc sinh so {i}"
start=$(date +%s)
expect_status "5000 dong, nhap thu → 200" 200 "$(upload /api/imports/students "$ADMIN" "$WORK/rows-5000.xlsx" dryRun=true)"
elapsed=$(( $(date +%s) - start ))
expect_json   "  5000 tai khoan se tao" created 5000
expect_status "  chay duoi 30 giay (that: ${elapsed}s)" yes "$([[ $elapsed -lt 30 ]] && echo yes || echo no)"
expect_status "  nhap thu khong ghi gi" 7 "$(student_total)"

# ── Giao vien ────────────────────────────────────────────────────────────────────────────────────
section "Giao vien: nhap thu"
call GET "/api/teachers/$DEMO_TEACHER_ID" "$ADMIN" > /dev/null
SUBJECT_A=$(json_field subjectId)
DEMO_NAME=$(J "d['fullName']")
call GET /api/questions/subjects "$ADMIN" > /dev/null
SUBJ_NAME=$(J "next(s['name'] for s in d if s['id'] == $SUBJECT_A)")
SUBJ_CODE=$(J "next(s['code'] for s in d if s['id'] == $SUBJECT_A)")
SUBJ_FOLDED=$(J "''.join(c for c in __import__('unicodedata').normalize('NFD', next(s['name'] for s in d if s['id'] == $SUBJECT_A)) if __import__('unicodedata').category(c) != 'Mn').lower().replace('đ', 'd')")

T2=$(new_teacher "$ADMIN" "${STAMP}t") || fail_fast "tao giao vien phu"
T2_ID=${T2%%|*}; T2_EMAIL=${T2#*|}
T3=$(new_teacher "$ADMIN" "${STAMP}l") || fail_fast "tao giao vien bi khoa"
T3_ID=${T3%%|*}; T3_EMAIL=${T3#*|}
retire_teacher "$ADMIN" "$T3_ID"

xl make "$WORK/teachers-1.xlsx" --header 'Ho va ten*|Email*|So dien thoai*|Mon giang day' <<ROWS
Giao vien Nhap Mot|gvim1${STAMP}@kiemthu.vn|#912345670|${SUBJ_FOLDED}
Giao vien Nhap Hai|gvim2${STAMP}@kiemthu.vn|#912345671|${SUBJ_CODE}
Giao vien Nhap Ba|gvim3${STAMP}@kiemthu.vn|#912345672|
Giao vien Nhap Bon|khong-phai-email|#912345673|${SUBJ_NAME}
Giao vien Nhap Nam|gvim5${STAMP}@kiemthu.vn|#912345674|Mon khong co
Giao vien trung|gvim1${STAMP}@kiemthu.vn|#912345675|${SUBJ_NAME}
${DEMO_NAME}|an.nv@anthill.edu.vn|#912345676|${SUBJ_NAME}
Ten Khac Hoan Toan|${T2_EMAIL}|#912345677|${SUBJ_NAME}
Mot hoc sinh|im${STAMP}H1@kiemthu.vn|#912345678|${SUBJ_NAME}
GV Dien thoai|gvim6${STAMP}@kiemthu.vn|abc|${SUBJ_NAME}
Giao vien Khong Mon|gvim7${STAMP}@kiemthu.vn|#912345679|
Giao vien Khong SDT|gvim8${STAMP}@kiemthu.vn||${SUBJ_NAME}
ROWS

expect_status "nhap thu (co mon chung) → 200" 200 "$(upload /api/imports/teachers "$ADMIN" "$WORK/teachers-1.xlsx" dryRun=true "subjectId=$SUBJECT_A")"
expect_json   "  12 dong du lieu" totalRows 12
expect_json   "  4 giao vien se tao (mon theo ten khong dau, theo ma, theo mon chung x2)" created 4
expect_json   "  1 giao vien da co dung ten (dong 8) duoc nhan ra" unchanged 1
expect_json   "  7 dong hong" failed 7
expect_status "  cac dong hong la 5,6,7,9,10,11,13" "5,6,7,9,10,11,13" "$(failed_rows)"
expect_status "  dong 5: email sai dinh dang" yes "$(issue_has 5 column 'Email')"
expect_status "  dong 6: mon khong co" yes "$(issue_has 6 column 'Môn giảng dạy')"
expect_status "  dong 7: trung email voi dong 2" yes "$(issue_has 7 message 'dòng 2')"
expect_status "  dong 9: email thuoc giao vien khac ten" yes "$(issue_has 9 message 'đã thuộc giáo viên')"
expect_status "  dong 10: email cua hoc sinh" yes "$(issue_has 10 message 'đã có người dùng')"
expect_status "  dong 11: so dien thoai sai" yes "$(issue_has 11 column 'Số điện thoại')"
expect_status "  dong 13: thieu so dien thoai (bat buoc)" yes "$(issue_has 13 message 'Chưa có số điện thoại')"

expect_status "nhap thu KHONG co mon chung → 200" 200 "$(upload /api/imports/teachers "$ADMIN" "$WORK/teachers-1.xlsx" dryRun=true)"
expect_json   "  chi con 2 giao vien tao duoc (dong co mon rieng)" created 2
expect_json   "  9 dong hong (them hai dong khong co mon)" failed 9
expect_status "  dong 4: chua co mon" yes "$(issue_has 4 column 'Môn giảng dạy')"
expect_status "  dong 12: chua co mon" yes "$(issue_has 12 message 'Chưa có môn')"

section "Giao vien: nhap that"
imported_teachers() { call GET "/api/teachers?search=$STAMP&pageSize=100" "$ADMIN" > /dev/null; J "sum(1 for t in d['items'] if t['email'].startswith('gvim'))"; }
expect_status "cac lan nhap thu chua tao giao vien nao" 0 "$(imported_teachers)"
expect_status "thieu mat khau → 400" 400 "$(upload /api/imports/teachers "$ADMIN" "$WORK/teachers-1.xlsx" dryRun=false "subjectId=$SUBJECT_A")"
expect_status "mon chung khong ton tai → 400" 400 "$(upload /api/imports/teachers "$ADMIN" "$WORK/teachers-1.xlsx" dryRun=false subjectId=999999 "password=$INITIAL_PASSWORD")"
expect_status "nhap that → 200" 200 "$(upload /api/imports/teachers "$ADMIN" "$WORK/teachers-1.xlsx" dryRun=false "subjectId=$SUBJECT_A" "password=$INITIAL_PASSWORD")"
expect_json   "  tao 4 giao vien" created 4
expect_json   "  7 dong hong" failed 7
expect_status "  phieu tai khoan: ten dang nhap la email, cot lop ghi ten mon" yes "$(J "'yes' if all('@' in a['username'] and a['group'] for a in d['accounts']) else 'no'")"
expect_status "co 4 giao vien moi" 4 "$(imported_teachers)"
expect_status "  moi giao vien dung mon" yes "$(J "'yes' if all(t['subjectId'] == $SUBJECT_A for t in d['items'] if t['email'].startswith('gvim')) else 'no'")"
expect_status "  giao vien dang hoat dong" yes "$(J "'yes' if all(t['status'] == 'active' for t in d['items'] if t['email'].startswith('gvim')) else 'no'")"
expect_status "  so dien thoai kieu so mat so 0 duoc tra lai" yes "$(J "'yes' if any(t.get('phone') == '0912345670' for t in d['items']) else 'no'")"
expect_status "giao vien dang nhap bang email + mat khau chung → 200" 200 "$(call POST /api/auth/login "" "{\"username\":\"gvim1${STAMP}@kiemthu.vn\",\"password\":\"$INITIAL_PASSWORD\"}")"
expect_json   "  bi ep doi mat khau" mustChangePassword true
expect_status "gui lai nguyen file khong mat khau → 200" 200 "$(upload /api/imports/teachers "$ADMIN" "$WORK/teachers-1.xlsx" dryRun=false "subjectId=$SUBJECT_A")"
expect_json   "  khong tao them" created 0
expect_json   "  5 dong da nhap duoc nhan ra (dong 2, 3, 4, 8, 12)" unchanged 5
expect_json   "  7 dong hong van hong" failed 7

# ── Lop ──────────────────────────────────────────────────────────────────────────────────────────
section "Lop: nhap thu"
LONG_DESC=$(python -c "print('a' * 513)")
xl make "$WORK/groups-1.xlsx" --header 'Ten lop*|Giao vien phu trach*|Mo ta' <<ROWS
Lop Nhap Mot ${STAMP}|${T2_EMAIL}|Mo ta lop mot
Lop Nhap Hai ${STAMP}|${T2_EMAIL^^}|
Lop Nhap Ba ${STAMP}||
Lop Nhap Bon ${STAMP}|khong.ton.tai@kiemthu.vn|
Lop Nhap Nam ${STAMP}|${T3_EMAIL}|
lop nhap mot ${STAMP}|${T2_EMAIL}|
X|${T2_EMAIL}|
Lop A IM ${STAMP}|an.nv@anthill.edu.vn|
Lop B IM ${STAMP}|${T2_EMAIL}|
Lop Nhap Sau ${STAMP}|${T2_EMAIL}|${LONG_DESC}
ROWS

expect_status "nhap thu → 200" 200 "$(upload /api/imports/groups "$ADMIN" "$WORK/groups-1.xlsx" dryRun=true)"
expect_json   "  10 dong" totalRows 10
expect_json   "  2 lop se tao" created 2
expect_json   "  1 lop da co dung giao vien (dong 9) duoc nhan ra" unchanged 1
expect_json   "  7 dong hong" failed 7
expect_status "  cac dong hong la 4,5,6,7,8,10,11" "4,5,6,7,8,10,11" "$(failed_rows)"
expect_status "  dong 4: thieu giao vien (moi lop phai co mot)" yes "$(issue_has 4 column 'Giáo viên phụ trách')"
expect_status "  dong 5: khong co giao vien dung email do" yes "$(issue_has 5 message 'Không có giáo viên nào')"
expect_status "  dong 6: giao vien bi khoa" yes "$(issue_has 6 message 'bị khoá')"
expect_status "  dong 7: trung ten voi dong 2 (khong phan biet hoa thuong)" yes "$(issue_has 7 message 'dòng 2')"
expect_status "  dong 8: ten lop qua ngan" yes "$(issue_has 8 column 'Tên lớp')"
expect_status "  dong 10: lop da co giao vien khac, doi giao vien phai ghi ly do" yes "$(issue_has 10 message 'ghi lý do')"
expect_status "  dong 11: mo ta qua dai" yes "$(issue_has 11 column 'Mô tả')"
new_groups() { call GET "/api/student-groups?search=$STAMP" "$ADMIN" > /dev/null; }
new_groups
expect_status "nhap thu chua tao lop nao" 0 "$(J "len([g for g in d if g['name'].startswith('Lop Nhap')])")"

section "Lop: nhap that"
expect_status "nhap that → 200" 200 "$(upload /api/imports/groups "$ADMIN" "$WORK/groups-1.xlsx" dryRun=false)"
expect_json   "  tao 2 lop" created 2
expect_json   "  7 dong hong" failed 7
new_groups
expect_status "co 2 lop moi" 2 "$(J "len([g for g in d if g['name'].startswith('Lop Nhap')])")"
expect_status "  deu la ban nhap (chua cong bo)" yes "$(J "'yes' if all(not g['isPublished'] for g in d if g['name'].startswith('Lop Nhap')) else 'no'")"
expect_status "  moi lop co dung mot giao vien, la giao vien ghi tren dong" yes "$(J "'yes' if all(len(g['teachers']) == 1 and g['teachers'][0]['id'] == $T2_ID for g in d if g['name'].startswith('Lop Nhap')) else 'no'")"
expect_status "  mo ta duoc luu" yes "$(J "'yes' if any(g['description'] == 'Mo ta lop mot' for g in d) else 'no'")"
G_NEW=$(J "next(g['id'] for g in d if g['name'].startswith('Lop Nhap Mot'))")
expect_status "lich su cua lop moi: co dong lap lop" 200 "$(call GET "/api/student-groups/$G_NEW/history" "$ADMIN")"
expect_json   "  loai = lap lop" 0.kind created
expect_status "  ghi ten giao vien phu trach" yes "$(J "'yes' if d[0].get('toTeacher') else 'no'")"
expect_status "  ghi nguoi thuc hien" yes "$(J "'yes' if d[0].get('actorName') else 'no'")"
expect_status "gui lai nguyen file → 200" 200 "$(upload /api/imports/groups "$ADMIN" "$WORK/groups-1.xlsx" dryRun=false)"
expect_json   "  khong tao them" created 0
expect_json   "  3 dong da nhap duoc nhan ra" unchanged 3
expect_json   "  7 dong hong van hong" failed 7

summary
