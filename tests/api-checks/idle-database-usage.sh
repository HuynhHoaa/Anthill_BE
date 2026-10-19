#!/usr/bin/env bash
# Database co duoc ngu khi khong ai dung he thong khong?
#
# Database mien phi (Neon) chi ngu khi 5 phut lien khong co truy van nao, va goi mien phi tinh theo gio database thuc.
# Hai tien trinh quet (tu nop bai, nhac sap het gio) moi 30 giay se giu database thuc 24/7 neu chung khong chiu nghi.
# Bo nay chung minh bon dieu, bang cach theo doi lan cuoi bat ky ket noi nao cua API toi database lam viec gi do:
#   1. Co nguoi dung (yeu cau da dang nhap)  -> hai tien trinh quet dinh ky, khong can them yeu cau nao.
#   2. Vang nguoi (qua khoang vang)          -> database KHONG co hoat dong nao, ke ca khi co yeu cau an danh,
#                                               yeu cau token hong, duong dan la va /health/live.
#   3. Nguoi dung tro lai                    -> quet lai trong vai giay.
#   4. /health (khong phai /health/live)     -> cham database, de loi goi danh thuc cua frontend danh thuc duoc no.
#
# Chay:  API phai chay voi nhip quet ngan va khoang vang ngan, tren mot database ma script doc duoc bang psql:
#
#   BackgroundJobs__IdleAfterSeconds=15 AutoSubmit__IntervalSeconds=5 DeadlineReminder__IntervalSeconds=5 \
#     dotnet run --project src/Anthill.Api
#
#   PGDATABASE=<database ma API dang dung> PGUSER=... PGPASSWORD=... IDLE_AFTER_SECONDS=15 \
#     bash tests/api-checks/idle-database-usage.sh
#
# Mat chung gan 2 phut vi phai doi that qua khoang vang (hai lan). Doc bang pg_stat_activity noi qua database
# `postgres`, nen chinh phep do khong tao hoat dong tren database dang do. Co y KHONG dem bang pg_stat_database:
# so lieu do tre vai giay (backend ranh moi day thong ke len), nen doc som se thay "0" ke ca khi co truy van.

source "$(dirname "$0")/lib.sh"

: "${PGDATABASE:?Dat PGDATABASE = database ma API dang dung}"
IDLE_AFTER_SECONDS="${IDLE_AFTER_SECONDS:-15}"
PSQL="${PSQL:-psql}"

# Thoi diem (mili giay epoch) lan cuoi mot ket noi cua API toi database doi trang thai, tuc vua lam xong mot truy van.
# 0 khi API khong con ket noi nao (pool da don het). state_change chi tang khi co hoat dong that, khong tang khi ket noi
# bi dong, nen "khong tang" nghia la database khong bi dung toi.
last_activity_ms() {
  "$PSQL" -d postgres -Atc \
    "select coalesce(floor(extract(epoch from max(state_change)) * 1000), 0)::bigint
       from pg_stat_activity where datname = '$PGDATABASE'"
}

expect_true() {
  local label="$1" ok="$2" detail="$3"
  if [[ "$ok" == "1" ]]; then
    PASSED=$((PASSED + 1))
    printf '  \033[32mOK\033[0m   %-58s %s\n' "$label" "$detail"
  else
    FAILED=$((FAILED + 1))
    FAILURES+=("$label — $detail")
    printf '  \033[31mFAIL\033[0m %-58s %s\n' "$label" "$detail"
  fi
}

# Co doc duoc khong? Khong co psql hay sai mat khau thi dung ngay, thay vi bao "khong co hoat dong" roi cho qua.
PROBE=$(last_activity_ms) || { echo "Khong doc duoc pg_stat_activity — kiem tra PSQL, PGUSER, PGPASSWORD" >&2; exit 1; }
[[ "$PROBE" =~ ^[0-9]+$ ]] || { echo "Doc hoat dong database that bai (nhan '$PROBE')" >&2; exit 1; }

TEACHER=$(login "an.nv@anthill.edu.vn" "123456")

# ── 1. Co nguoi dung ────────────────────────────────────────────────────────────────

section "Co nguoi dung: hai tien trinh tu quet, khong can them yeu cau nao (nhip 5 giay)"

call GET /api/auth/me "$TEACHER" > /dev/null
sleep 3
BEFORE=$(last_activity_ms)
sleep 8        # tong chua toi $IDLE_AFTER_SECONDS giay ke tu yeu cau tren, nen van dang co nguoi dung
AFTER=$(last_activity_ms)
expect_true "8 giay sau, khong them yeu cau: database van co hoat dong" "$((AFTER > BEFORE ? 1 : 0))" \
  "+$((AFTER - BEFORE)) ms"

# ── 2. Vang nguoi ───────────────────────────────────────────────────────────────────

section "Vang nguoi: database khong co hoat dong nao"

WAIT=$((IDLE_AFTER_SECONDS + 10))
printf '  doi %ss de qua khoang vang (%ss) va luot quet dang do xong...\n' "$WAIT" "$IDLE_AFTER_SECONDS"
sleep "$WAIT"

BEFORE=$(last_activity_ms)
for _ in 1 2 3 4 5; do
  call GET /health/live "" > /dev/null
  call GET /wp-login.php "" > /dev/null                      # may do lo hong hay goi: 404, an danh
  call GET /api/auth/me "khong-phai-token" > /dev/null       # token hong: 401 ngay tai buoc xac thuc
  sleep 4
done
sleep 1
AFTER=$(last_activity_ms)
expect_true "20 giay vang nguoi: khong co hoat dong nao" "$((AFTER <= BEFORE ? 1 : 0))" \
  "chenh $((AFTER - BEFORE)) ms"

expect_status "GET /health/live → 200" 200 "$(call GET /health/live "")"
expect_json   "  status = ok" status ok

# ── 3. Nguoi dung tro lai ───────────────────────────────────────────────────────────

section "Nguoi dung tro lai: quet lai trong vai giay"

call GET /api/auth/me "$TEACHER" > /dev/null
sleep 3         # cho phan hoat dong cua chinh yeu cau tren xong
BEFORE=$(last_activity_ms)
sleep 8
AFTER=$(last_activity_ms)
expect_true "8 giay sau, khong them yeu cau: quet lai" "$((AFTER > BEFORE ? 1 : 0))" "+$((AFTER - BEFORE)) ms"

# ── 4. /health cham database ────────────────────────────────────────────────────────

section "/health (khac /health/live) cham database"

# Cho het khoang vang lan nua de khong lan voi cac luot quet o tren, va qua 5 giay bo nho dem cua /health.
sleep "$WAIT"
BEFORE=$(last_activity_ms)
expect_status "GET /health → 200" 200 "$(call GET /health "")"
expect_json   "  database = ok" database ok
sleep 1
AFTER=$(last_activity_ms)
expect_true "/health thuc su hoi database" "$((AFTER > BEFORE ? 1 : 0))" "+$((AFTER - BEFORE)) ms"

summary
