# Helpers for HTTP end-to-end tests against the running app.
export MSYS_NO_PATHCONV=1  # stop Git Bash rewriting "=/path" arguments into Windows paths
BASE=${BASE:-http://localhost:5107}
DB=${DB:-S3SportDb}
# Password of admin@s3sport.om on the instance under test (SeedAdmin:Password); never committed.
: "${ADMIN_PW:?set ADMIN_PW to the admin password of the test instance (see tests/e2e/README.md)}"
T=$(dirname "$0")
PASS=0; FAIL=0

ok()   { PASS=$((PASS+1)); echo "  PASS  $1"; }
bad()  { FAIL=$((FAIL+1)); echo "  FAIL  $1"; }
check(){ if [ "$2" = "$3" ]; then ok "$1 ($3)"; else bad "$1 (expected $2, got $3)"; fi; }

# Location header of the last request (CR stripped) -> $T/location
saveloc() { grep -i '^location:' "$T/headers" | sed 's/^[Ll]ocation: //' | tr -d '\r' > "$T/location"; }
# get JAR PATH -> prints status code, body saved to $T/body.html
get()  { curl -s -b "$1" -c "$1" -o "$T/body.html" -D "$T/headers" -w "%{http_code}" "$BASE$2"; saveloc; }
# denied CODE -> "DENIED" when the request was refused (403, or 302 to the access-denied page)
denied() { if [ "$1" = 403 ] || { [ "$1" = 302 ] && grep -qi AccessDenied "$T/location"; }; then echo DENIED; else echo "$1"; fi; }
# token JAR PATH -> antiforgery token from the page
token(){ curl -s -b "$1" -c "$1" "$BASE$2" | grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//'; }
# post JAR PATH TOKEN [curl args...] -> status code; Location saved to $T/location
post() {
  local jar=$1 path=$2 tok=$3; shift 3
  curl -s -b "$jar" -c "$jar" -o "$T/body.html" -D "$T/headers" -w "%{http_code}" -X POST "$BASE$path" \
       -H "RequestVerificationToken: $tok" --data-urlencode "__RequestVerificationToken=$tok" "$@"
  saveloc
}
postm() { # multipart
  local jar=$1 path=$2 tok=$3; shift 3
  curl -s -b "$jar" -c "$jar" -o "$T/body.html" -D "$T/headers" -w "%{http_code}" -X POST "$BASE$path" \
       -F "__RequestVerificationToken=$tok" "$@"
  saveloc
}
sql() { sqlcmd -S . -E -C -I -d "$DB" -W -h -1 -Q "SET NOCOUNT ON; $1" | tr -d '\r' | sed '/^$/d'; }
# Arabic-safe argument helpers (curl.exe on Windows mangles non-ANSI argv).
urlenc() { printf '%s' "$1" | od -An -tx1 | tr -d ' \n' | sed 's/../%&/g'; }
fv() { printf '%s' "$2" > "v_$1.txt"; printf '%s=<v_%s.txt' "$1" "$1"; }
# formval HTML NAME -> value attribute of the input NAME, HTML entities for + / = decoded
formval() { printf '%s' "$1" | grep -o "name=\"$2\" value=\"[^\"]*\"" | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g'; }
