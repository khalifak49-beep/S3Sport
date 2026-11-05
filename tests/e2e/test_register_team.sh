#!/usr/bin/env bash
# Sign-up choice: regular user vs team. Team sign-up creates the account + team application together (or nothing).
cd "$(dirname "$0")"
. ./lib.sh
rm -f rt_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
UPLOADS=../../S3Sport/wwwroot/uploads/teams
uploads() { ls "$UPLOADS" 2>/dev/null | wc -l; }
SEEB=$(sql "SELECT Id FROM Wilayats WHERE NameEn='As Seeb'"); KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
EMAIL=team-$R@test.om

echo "== Choice page"
check "sign-up opens the choice" 200 "$(get rt_guest.jar /Account/Register)"
check "offers a regular user account" 1 "$(grep -c 'Register?type=user' $T/body.html)"
check "offers a team account" 1 "$(grep -c '/Account/RegisterTeam' $T/body.html)"
check "regular form still reachable" 200 "$(get rt_guest.jar "/Account/Register?type=user")"
check "user form has the type switch" 1 "$(grep -c 'account-switch' $T/body.html)"
check "type=team redirects to the team form" 302 "$(get rt_guest.jar "/Account/Register?type=team")"
check "team form" 200 "$(get rt_guest.jar /Account/RegisterTeam)"
check "team form has account + team fields" 2 "$(grep -c -E 'name="Password"|name="NameAr"' $T/body.html)"

TOK=$(token rt_guest.jar /Account/RegisterTeam)
team() { postm rt_guest.jar /Account/RegisterTeam "$TOK" -F "$(fv FullName "مسؤول فريق تجريبي")" -F "Email=$1" -F "Password=$2" -F "ConfirmPassword=$2" \
  -F "$(fv NameAr "فريق التسجيل $R")" -F "NameEn=Signup Team $R" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$3 -F ContactPhone=99112233 "${@:4}"; }

echo "== Validation leaves nothing behind"
U0=$(uploads)
check "weak password + no logo refused" 200 "$(team $EMAIL abc $KHOUDH)"
grep -q "شعار الفريق مطلوب" $T/body.html && ok "logo required message" || bad "logo required message"
check "region mismatch refused" 200 "$(team $EMAIL Test@1234 99999 -F "Logo=@logo.png;type=image/png")"
check "no account created" 0 "$(sql "SELECT COUNT(*) FROM AspNetUsers WHERE Email='$EMAIL'")"
check "no application created" 0 "$(sql "SELECT COUNT(*) FROM TeamApplications WHERE NameEn='Signup Team $R'")"
check "no logo stored for failed attempts" "$U0" "$(uploads)"
check "existing email refused" 200 "$(team admin@s3sport.om Test@1234 $KHOUDH -F "Logo=@logo.png;type=image/png")"
grep -q "هذا البريد مسجل مسبقاً" $T/body.html && ok "existing email message" || bad "existing email message"

echo "== Team sign-up"
check "account + application created" 302 "$(team $EMAIL Test@1234 $KHOUDH -F "Logo=@logo.png;type=image/png" -F "$(fv Description "فريق للتجربة")")"
APP=$(sql "SELECT Id FROM TeamApplications WHERE NameEn='Signup Team $R'")
check "redirected to the application" "/TeamApplications/Details/$APP" "$(cat $T/location)"
check "application pending, owned by the new account" "0|$EMAIL" "$(sql "SELECT CONCAT(a.Status,'|',u.Email) FROM TeamApplications a JOIN AspNetUsers u ON u.Id=a.ApplicantUserId WHERE a.Id=$APP")"
check "account has the regular user role" "User" "$(sql "SELECT r.Name FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId JOIN AspNetUsers u ON u.Id=ur.UserId WHERE u.Email='$EMAIL'")"
check "team region is the account's default region" 1 "$(sql "SELECT COUNT(*) FROM UserAreas ua JOIN AspNetUsers u ON u.Id=ua.UserId WHERE u.Email='$EMAIL' AND ua.AreaId=$KHOUDH AND ua.IsDefault=1")"
check "logo stored" "$((U0 + 1))" "$(uploads)"
check "signed in: sees the application" 200 "$(get rt_guest.jar /TeamApplications/Details/$APP)"
check "signed in: team form now goes to the in-account application" 302 "$(get rt_guest.jar /Account/RegisterTeam)"
check "same team name again refused (pending in this wilayat)" 200 "$(postm rt_other.jar /Account/RegisterTeam "$(token rt_other.jar /Account/RegisterTeam)" -F "$(fv FullName "شخص آخر")" -F "Email=other-$R@test.om" -F Password=Test@1234 -F ConfirmPassword=Test@1234 -F "$(fv NameAr "فريق التسجيل $R")" -F "NameEn=Signup Team $R" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F ContactPhone=99112233 -F "Logo=@logo.png;type=image/png")"
check "…and no account for it" 0 "$(sql "SELECT COUNT(*) FROM AspNetUsers WHERE Email='other-$R@test.om'")"

echo "== Approval makes the account a team manager"
TOK=$(token rt_admin.jar /Account/Login); post rt_admin.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
TOK=$(token rt_admin.jar /Admin/TeamApplications/Details/$APP)
RV=$(curl -s -b rt_admin.jar $BASE/Admin/TeamApplications/Details/$APP | grep -o 'name="rowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "admin approves" 302 "$(post rt_admin.jar /Admin/TeamApplications/Approve/$APP "$TOK" --data-urlencode "rowVersion=$RV")"
check "account is now a team manager" 1 "$(sql "SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId JOIN AspNetUsers u ON u.Id=ur.UserId WHERE u.Email='$EMAIL' AND r.Name='TeamManager'")"
TEAM=$(sql "SELECT CreatedTeamId FROM TeamApplications WHERE Id=$APP")
check "approval notification opens the team management" "/MyTeams/Squad/$TEAM" "$(sql "SELECT TOP 1 n.Url FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$EMAIL' ORDER BY n.Id DESC")"
# The applicant's existing session (signed in at sign-up, before approval) — no re-login.
check "same session gets team permissions immediately" 200 "$(get rt_guest.jar /MyTeams/Squad/$TEAM)"
check "menu now shows team management" 1 "$(curl -s -b rt_guest.jar -c rt_guest.jar $BASE/ | grep -c 'إدارة فريقي')"
cp rt_guest.jar rt_mgr.jar

echo "== Full control of own team only"
OTHER=$(sql "SELECT TOP 1 Id FROM Teams WHERE Status=1 AND Id<>$TEAM ORDER BY Id")
if [ -z "$OTHER" ]; then
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'فريق آخر $R','Other $R','other-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
  OTHER=$(sql "SELECT Id FROM Teams WHERE Slug='other-$R'")
fi
for p in /MyTeams/Squad/$OTHER /MyTeams/Info/$OTHER /MyTeams/Matches/$OTHER /MyTeams/AddPlayer/$OTHER; do
  check "other team denied: $p" DENIED "$(denied $(get rt_mgr.jar $p))"
done
get rt_mgr.jar /MyTeams/Info/$TEAM > /dev/null; RV=$(formval "$(cat $T/body.html)" RowVersion)
TOK=$(token rt_mgr.jar /MyTeams/Info/$TEAM)
check "rename own team" 302 "$(postm rt_mgr.jar /MyTeams/Info "$TOK" -F TeamId=$TEAM -F "$(fv NameAr "فريق جديد الاسم $R")" -F "NameEn=Renamed $R" -F "RowVersion=$RV")"
check "name changed, page URL kept" 1 "$(sql "SELECT COUNT(*) FROM Teams WHERE Id=$TEAM AND NameAr=N'فريق جديد الاسم $R' AND NameEn='Renamed $R' AND Slug LIKE 'signup-team-$R%'")"
sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'اسم محجوز $R','Taken $R','taken-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
get rt_mgr.jar /MyTeams/Info/$TEAM > /dev/null; RV=$(formval "$(cat $T/body.html)" RowVersion)
check "name of another team in the same area refused" 200 "$(postm rt_mgr.jar /MyTeams/Info "$TOK" -F TeamId=$TEAM -F "$(fv NameAr "اسم محجوز $R")" -F "NameEn=Taken $R" -F "RowVersion=$RV")"
check "renaming another team refused" DENIED "$(denied $(postm rt_mgr.jar /MyTeams/Info "$TOK" -F TeamId=$OTHER -F "$(fv NameAr "اختراق")" -F "NameEn=Hack" -F "RowVersion=$RV"))"
check "other team untouched" 0 "$(sql "SELECT COUNT(*) FROM Teams WHERE Id=$OTHER AND NameEn='Hack'")"

TOK=$(token rt_mgr.jar /MyTeams/AddPlayer/$TEAM)
for n in 1 2; do postm rt_mgr.jar /MyTeams/SavePlayer "$TOK" -F Id=0 -F TeamId=$TEAM -F "$(fv FullNameAr "لاعب حذف $n $R")" -F ShirtNumber=$((20+n)) -F JoinedOn=2026-11-01 > /dev/null; done
P1=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب حذف 1 $R'"); P2=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب حذف 2 $R'")
check "delete button offered for a player without matches" 1 "$(curl -s -b rt_mgr.jar $BASE/MyTeams/Squad/$TEAM | grep -c "DeletePlayer/$TEAM?playerId=$P1\"")"
TOK=$(token rt_mgr.jar /MyTeams/Squad/$TEAM)
check "delete player added by mistake" 302 "$(post rt_mgr.jar "/MyTeams/DeletePlayer/$TEAM?playerId=$P1" "$TOK")"
check "player hidden (soft delete) and off the squad" "1|0" "$(sql "SELECT CONCAT(IsDeleted,'|',(SELECT COUNT(*) FROM TeamPlayers WHERE PlayerId=$P1)) FROM Players WHERE Id=$P1")"
sql "INSERT INTO TeamPlayers(TeamId,PlayerId,ShirtNumber,JoinedOn,LeftOn,IsActive,IsCaptain,CreatedAt) VALUES($OTHER,$P2,5,'2025-01-01','2025-12-31',0,0,SYSUTCDATETIME())" > /dev/null
check "player with history elsewhere refused" 302 "$(post rt_mgr.jar "/MyTeams/DeletePlayer/$TEAM?playerId=$P2" "$TOK")"
check "…and kept" 0 "$(sql "SELECT IsDeleted FROM Players WHERE Id=$P2")"
OP=$(sql "SELECT TOP 1 PlayerId FROM TeamPlayers WHERE TeamId=$OTHER AND IsActive=1")
if [ -n "$OP" ]; then check "deleting another team's player refused" DENIED "$(denied $(post rt_mgr.jar "/MyTeams/DeletePlayer/$OTHER?playerId=$OP" "$TOK"))"; fi

echo
echo "RESULT: $PASS passed, $FAIL failed"
