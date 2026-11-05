#!/usr/bin/env bash
# End-to-end tests for the regular-user experience: teams directory, team page, follow + notifications, favorites.
cd "$(dirname "$0")"
. ./lib.sh
rm -f f_admin.jar f_user.jar f_guest.jar

for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done

echo "== Setup: admin creates two teams (one later suspended)"
SEEB=$(sql "SELECT Id FROM Wilayats WHERE NameEn='As Seeb'")
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
TOK=$(token f_admin.jar /Account/Login)
post f_admin.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
T1NAME="نادي المتابعة $RANDOM"; T2NAME="نادي موقوف $RANDOM"
TOK=$(token f_admin.jar /Admin/Teams/Create)
postm f_admin.jar /Admin/Teams/Save "$TOK" -F Id=0 -F "$(fv NameAr "$T1NAME")" -F "NameEn=Follow Club $RANDOM" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F Status=Approved -F "$(fv Description "فريق للتجربة")" > /dev/null
T1=$(sql "SELECT Id FROM Teams WHERE NameAr=N'$T1NAME'"); S1=$(sql "SELECT Slug FROM Teams WHERE Id=$T1")
TOK=$(token f_admin.jar /Admin/Teams/Create)
postm f_admin.jar /Admin/Teams/Save "$TOK" -F Id=0 -F "$(fv NameAr "$T2NAME")" -F "NameEn=Suspended Club $RANDOM" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F Status=Suspended > /dev/null
T2=$(sql "SELECT Id FROM Teams WHERE NameAr=N'$T2NAME'"); S2=$(sql "SELECT Slug FROM Teams WHERE Id=$T2")
[ -n "$T1" ] && [ -n "$T2" ] && ok "two teams created ($S1, $S2)" || bad "team setup"

echo "== Guest browsing"
check "teams directory" 200 "$(get f_guest.jar /Teams)"
grep -q "$T1NAME" body.html && ok "approved team listed" || bad "approved team listed"
grep -q "$T2NAME" body.html && bad "suspended team hidden" || ok "suspended team hidden"
check "team page" 200 "$(get f_guest.jar /teams/$S1)"
grep -q "فريق للتجربة" body.html && ok "team page shows description" || bad "team description"
grep -q 'title="سجّل الدخول للمتابعة"' body.html && ok "guest gets login-to-follow button" || bad "guest follow button"
check "suspended team page 404" 404 "$(get f_guest.jar /teams/$S2)"
check "unknown team 404" 404 "$(get f_guest.jar /teams/no-such-team)"
check "search by area name" 200 "$(get f_guest.jar "/Teams?q=$(urlenc الخوض)")"
grep -q "$T1NAME" body.html && ok "search finds team by area" || bad "search by area"
check "guest follow → login" 302 "$(curl -s -o /dev/null -w '%{http_code}' -b f_guest.jar -X POST $BASE/Follows/Follow -d targetType=Team -d targetId=$T1)"
check "favorites requires login" 302 "$(get f_guest.jar /Favorites)"

echo "== User in a different region (Dhofar) follows a Muscat team"
EMAIL="fan$RANDOM@test.om"
TOK=$(token f_user.jar /Account/Register)
post f_user.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مشجع")" -d Email=$EMAIL -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
TOK=$(token f_user.jar /Region)
post f_user.jar /Region "$TOK" -d GovernorateId=2 -d MakeDefault=true > /dev/null
get f_user.jar /Teams > /dev/null
grep -q "$T1NAME" body.html && bad "region scope excludes Muscat team" || ok "region scope (Dhofar) excludes Muscat team"
get f_user.jar "/Teams?all=true" > /dev/null
grep -q "$T1NAME" body.html && ok "all-regions scope includes it" || bad "all-regions scope"
UID_=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$EMAIL'")
TOK=$(token f_user.jar /teams/$S1)
check "CSRF: follow without token" 400 "$(curl -s -o /dev/null -w '%{http_code}' -b f_user.jar -X POST $BASE/Follows/Follow -d targetType=Team -d targetId=$T1)"
check "follow (form post)" 302 "$(post f_user.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$T1 -d returnUrl=/teams/$S1)"
check "redirected back to team page" "/teams/$S1" "$(cat location)"
check "follow stored, notifications on" 1 "$(sql "SELECT COUNT(*) FROM UserFollows WHERE UserId='$UID_' AND TeamId=$T1 AND NotificationsEnabled=1")"
post f_user.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$T1 > /dev/null
check "follow twice → still one row" 1 "$(sql "SELECT COUNT(*) FROM UserFollows WHERE UserId='$UID_' AND TeamId=$T1")"

echo "== No-reload (fetch) mode"
CODE=$(curl -s -b f_user.jar -c f_user.jar -o body.html -D headers -w "%{http_code}" -X POST $BASE/Follows/ToggleNotifications \
  -H "X-Requested-With: fetch" -H "RequestVerificationToken: $TOK" --data-urlencode "__RequestVerificationToken=$TOK" -d targetType=Team -d targetId=$T1)
check "toggle via fetch" 200 "$CODE"
grep -q "data-follow-box" body.html && ok "returns the follow partial" || bad "partial returned"
grep -q "<html" body.html && bad "partial without layout" || ok "partial without layout"
grep -qi "x-s3-message:" headers && ok "toast message header present" || bad "message header"
check "notifications now off" 0 "$(sql "SELECT CAST(NotificationsEnabled AS int) FROM UserFollows WHERE UserId='$UID_' AND TeamId=$T1")"
grep -q "fa-bell-slash" body.html && ok "partial shows muted bell" || bad "muted bell icon"
post f_user.jar /Follows/ToggleNotifications "$TOK" -d targetType=Team -d targetId=$T1 > /dev/null
check "notifications back on" 1 "$(sql "SELECT CAST(NotificationsEnabled AS int) FROM UserFollows WHERE UserId='$UID_' AND TeamId=$T1")"

echo "== Pages reflect the follow"
get f_user.jar /teams/$S1 > /dev/null
grep -q "متابَع" body.html && ok "team page shows following state" || bad "following state"
grep -q '<strong>1</strong> متابع' body.html && ok "followers count = 1" || bad "followers count"
check "favorites page" 200 "$(get f_user.jar /Favorites)"
grep -q "$T1NAME" body.html && ok "team in favorites" || bad "team in favorites"
get f_guest.jar / > /dev/null
grep -q "الفرق الأكثر متابعة" body.html && grep -q "$T1NAME" body.html && ok "home shows most-followed team" || bad "most followed on home"

echo "== Invalid targets"
check "follow suspended team → 404" 404 "$(post f_user.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$T2)"
check "follow missing player → 404" 404 "$(post f_user.jar /Follows/Follow "$TOK" -d targetType=Player -d targetId=99999999)"
check "toggle without follow → 404" 404 "$(post f_user.jar /Follows/ToggleNotifications "$TOK" -d targetType=Team -d targetId=$T2)"

echo "== Unfollow"
check "unfollow" 302 "$(post f_user.jar /Follows/Unfollow "$TOK" -d targetType=Team -d targetId=$T1)"
check "follow removed" 0 "$(sql "SELECT COUNT(*) FROM UserFollows WHERE UserId='$UID_' AND TeamId=$T1")"
get f_user.jar /Favorites > /dev/null
grep -q "لا تتابع أي فريق بعد" body.html && ok "favorites empty state" || bad "favorites empty state"
# leave one follow in place for screenshots
post f_user.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$T1 > /dev/null
echo "== Home for a user whose region (Dhofar) differs from the followed team (Muscat)"
get f_user.jar / > /dev/null
grep -q "فرقي المفضلة" body.html && grep -q "$T1NAME" body.html && ok "home shows followed team from another region" || bad "followed teams on home"
grep -q "الفرق الأكثر متابعة في عُمان" body.html && ok "most-followed falls back to national" || bad "national fallback"
echo "$EMAIL" > fan_email.txt

echo
echo "RESULT: $PASS passed, $FAIL failed"
