#!/usr/bin/env bash
# End-to-end tests for phase 2 (accounts, regions, team applications, admin).
cd "$(dirname "$0")"
. ./lib.sh
rm -f guest.jar user.jar admin.jar other.jar

for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done

echo "== Public pages"
check "home" 200 "$(get guest.jar /)"
check "region page" 200 "$(get guest.jar /Region)"
check "login page" 200 "$(get guest.jar /Account/Login)"
check "register page" 200 "$(get guest.jar "/Account/Register?type=user")"
check "404 page" 404 "$(get guest.jar /does-not-exist)"
grep -q "الصفحة غير موجودة" body.html && ok "404 is Arabic" || bad "404 is Arabic"

echo "== Region lookups"
W=$(curl -s "$BASE/Region/Wilayats?governorateId=1")
echo "$W" | grep -q '"name":"السيب"' && ok "wilayats JSON has raw Arabic (As Seeb)" || bad "wilayats JSON: $W"
SEEB=$(sql "SELECT Id FROM Wilayats WHERE NameEn='As Seeb'")
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
MABAILA=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Mabaila'")
NIZWA_AREA=$(sql "SELECT Id FROM Areas WHERE NameEn='Firq'")
S=$(curl -s "$BASE/Region/Search?q=$(urlenc الخوض)")
echo "$S" | grep -q "\"areaId\":$KHOUDH" && ok "search finds الخوض" || bad "search: $S"

echo "== Guest selects region (cookie)"
TOK=$(token guest.jar /Region)
check "guest region post" 302 "$(post guest.jar /Region "$TOK" -d GovernorateId=1 -d WilayatId=$SEEB -d AreaId=$KHOUDH -d MakeDefault=true)"
grep -q "s3s_region" guest.jar && ok "region cookie set" || bad "region cookie set"
get guest.jar / > /dev/null
grep -q "الخوض، السيب، مسقط" body.html && ok "home shows region name" || bad "home shows region name"
TOK=$(token guest.jar /Region)
check "tampered parents ignored (area wins)" 302 "$(post guest.jar /Region "$TOK" -d GovernorateId=5 -d WilayatId=$SEEB -d AreaId=$KHOUDH)"
grep -q "1-$SEEB-$KHOUDH-0" guest.jar && ok "region normalised from deepest id" || bad "normalised cookie: $(grep s3s_region guest.jar)"

echo "== CSRF"
check "post without token rejected" 400 "$(curl -s -o /dev/null -w '%{http_code}' -b guest.jar -X POST $BASE/Region -d GovernorateId=1)"

echo "== Register"
EMAIL="player$RANDOM@test.om"
TOK=$(token user.jar "/Account/Register?type=user")
check "register weak password" 200 "$(post user.jar /Account/Register "$TOK" -d "FullName=$(urlenc تجربة)" -d Email=$EMAIL -d Password=abc -d ConfirmPassword=abc)"
grep -q "8 أحرف" body.html && ok "Arabic validation message" || bad "Arabic validation message"
TOK=$(token user.jar "/Account/Register?type=user")
check "register ok" 302 "$(post user.jar /Account/Register "$TOK" -d "FullName=$(urlenc "سالم المعمري")" -d Email=$EMAIL -d Password=Test@1234 -d ConfirmPassword=Test@1234)"
grep -q "/Region" location && ok "new user without region → region page" || bad "redirect: $(cat location)"
check "user has User role" 1 "$(sql "SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId JOIN AspNetUsers u ON u.Id=ur.UserId WHERE u.Email='$EMAIL' AND r.Name='User'")"
TOK=$(token user.jar /Region)
post user.jar /Region "$TOK" -d GovernorateId=1 -d WilayatId=$SEEB -d AreaId=$KHOUDH -d MakeDefault=true > /dev/null
TOK=$(token user.jar /Region)
post user.jar /Region "$TOK" -d GovernorateId=5 -d MakeDefault=true > /dev/null
check "user saved 2 regions" 2 "$(sql "SELECT COUNT(*) FROM UserAreas ua JOIN AspNetUsers u ON u.Id=ua.UserId WHERE u.Email='$EMAIL'")"
check "exactly one default" 1 "$(sql "SELECT COUNT(*) FROM UserAreas ua JOIN AspNetUsers u ON u.Id=ua.UserId WHERE u.Email='$EMAIL' AND IsDefault=1")"
check "default is newest (Ad Dakhiliyah)" 5 "$(sql "SELECT GovernorateId FROM UserAreas ua JOIN AspNetUsers u ON u.Id=ua.UserId WHERE u.Email='$EMAIL' AND IsDefault=1")"

echo "== Access control"
check "user → admin dashboard denied" 302 "$(get user.jar /Admin/Dashboard)"
curl -s -b user.jar -o /dev/null -w "%{redirect_url}" $BASE/Admin/Dashboard | grep -q AccessDenied && ok "redirected to AccessDenied" || bad "AccessDenied redirect"
check "guest → applications requires login" 302 "$(get guest.jar /TeamApplications/Create)"

echo "== Team application"
printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\rIDATx\x9cc\xf8\xcf\xc0\xf0\x1f\x00\x05\x00\x01\xff\x89\x99=\x1d\x00\x00\x00\x00IEND\xaeB`\x82' > logo.png
echo "<script>alert(1)</script>" > fake.png
TEAM="نادي الخوض $RANDOM"
TOK=$(token user.jar /TeamApplications/Create)
check "fake image rejected" 200 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "$TEAM")" -F "NameEn=Al Khoudh Club" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F ContactPhone=99123456 -F "Logo=@fake.png;type=image/png")"
grep -q "لا يطابق صورة صالحة" body.html && ok "magic-byte check message" || bad "magic-byte check"
TOK=$(token user.jar /TeamApplications/Create)
check "mismatched region rejected" 200 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "$TEAM")" -F "NameEn=Al Khoudh Club" -F GovernorateId=5 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F ContactPhone=99123456 -F "Logo=@logo.png;type=image/png")"
grep -q "لا تتبع الولاية" body.html && ok "region chain validated" || bad "region chain validated"
TOK=$(token user.jar /TeamApplications/Create)
check "application submitted" 302 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "$TEAM")" -F "NameEn=Al Khoudh Club" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F AdditionalAreaIds=$MABAILA -F FoundedYear=1998 -F ContactPhone=99123456 -F "$(fv Description "فريق أهلي")" -F "Logo=@logo.png;type=image/png")"
APP=$(sql "SELECT TOP 1 Id FROM TeamApplications ORDER BY Id DESC")
check "status pending" 0 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP")"
check "additional area stored" 1 "$(sql "SELECT COUNT(*) FROM TeamApplicationAreas WHERE TeamApplicationId=$APP AND AreaId=$MABAILA")"
LOGO=$(sql "SELECT LogoPath FROM TeamApplications WHERE Id=$APP")
check "logo served" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE$LOGO)"
curl -s -D - -o /dev/null $BASE$LOGO | grep -qi "x-content-type-options: nosniff" && ok "uploads send nosniff" || bad "nosniff header"
check "own application visible" 200 "$(get user.jar /TeamApplications/Details/$APP)"

echo "== Other user cannot see it"
EMAIL2="other$RANDOM@test.om"
TOK=$(token other.jar "/Account/Register?type=user")
post other.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مستخدم آخر")" -d Email=$EMAIL2 -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
check "other user → 404" 404 "$(get other.jar /TeamApplications/Details/$APP)"

echo "== Admin login + review"
TOK=$(token admin.jar /Account/Login)
check "admin login" 302 "$(post admin.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW")"
for p in /Admin/Dashboard /Admin/TeamApplications /Admin/TeamApplications/Details/$APP /Admin/Teams /Admin/Teams/Create /Admin/Regions "/Admin/Regions?level=Wilayat&parentId=1" "/Admin/Regions?level=Area&parentId=$SEEB" /Admin/Users /Admin/Roles /Admin/AuditLogs "/Admin/TeamApplications?status="; do
  check "GET $p" 200 "$(get admin.jar "$p")"
done
TOK=$(token admin.jar /Admin/TeamApplications/Details/$APP)
RV=$(curl -s -b admin.jar $BASE/Admin/TeamApplications/Details/$APP | grep -o 'name="rowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "reject without reason refused" 302 "$(post admin.jar /Admin/TeamApplications/Reject/$APP "$TOK" --data-urlencode "rowVersion=$RV" -d notes=)"
check "still pending" 0 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP")"
check "stale rowVersion approve refused" 302 "$(post admin.jar /Admin/TeamApplications/Approve/$APP "$TOK" --data-urlencode "rowVersion=AAAAAAAAAAA=")"
check "still pending after stale approve" 0 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP")"
check "approve" 302 "$(post admin.jar /Admin/TeamApplications/Approve/$APP "$TOK" --data-urlencode "rowVersion=$RV" -d "notes=$(urlenc "مرحباً بكم")")"
TEAMID=$(sql "SELECT CreatedTeamId FROM TeamApplications WHERE Id=$APP")
check "application approved" 1 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP")"
check "team created & approved" 1 "$(sql "SELECT Status FROM Teams WHERE Id=$TEAMID")"
check "team slug" "al-khoudh-club" "$(sql "SELECT Slug FROM Teams WHERE Id=$TEAMID" | sed 's/-[0-9]*$//')"
check "team areas (primary+extra)" 2 "$(sql "SELECT COUNT(*) FROM TeamAreas WHERE TeamId=$TEAMID")"
check "primary area" $KHOUDH "$(sql "SELECT AreaId FROM TeamAreas WHERE TeamId=$TEAMID AND IsPrimary=1")"
check "applicant is manager" 1 "$(sql "SELECT COUNT(*) FROM TeamMembers m JOIN AspNetUsers u ON u.Id=m.UserId WHERE m.TeamId=$TEAMID AND u.Email='$EMAIL' AND m.Role=0")"
check "applicant got TeamManager role" 1 "$(sql "SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId JOIN AspNetUsers u ON u.Id=ur.UserId WHERE u.Email='$EMAIL' AND r.Name='TeamManager'")"
check "applicant notified" 1 "$(sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$EMAIL'")"
check "audit logged" 1 "$(sql "SELECT COUNT(*) FROM AuditLogs WHERE EntityName='TeamApplication' AND EntityId='$APP' AND Action=N'اعتماد طلب فريق'")"
check "approve twice refused" 302 "$(post admin.jar /Admin/TeamApplications/Approve/$APP "$TOK" --data-urlencode "rowVersion=$RV")"
check "still one team" 1 "$(sql "SELECT COUNT(*) FROM TeamApplications WHERE Id=$APP AND CreatedTeamId=$TEAMID")"

echo "== Team visible in region"
get guest.jar / > /dev/null
grep -q "$TEAM" body.html && ok "team on guest home (Al Khoudh)" || bad "team on home"
get user.jar /Notifications > /dev/null
grep -q "تمت الموافقة" body.html && ok "user sees notification" || bad "user notification page"

echo "== Duplicate name in same area blocked"
TOK=$(token user.jar /TeamApplications/Create)
check "duplicate rejected" 200 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "$TEAM")" -F "NameEn=Dup" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$KHOUDH -F ContactPhone=99123456 -F "Logo=@logo.png;type=image/png")"
grep -q "يوجد فريق مسجل بنفس الاسم" body.html && ok "duplicate message" || bad "duplicate message"

echo "== Reject flow + resubmit"
TOK=$(token user.jar /TeamApplications/Create)
postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "فريق فرق $RANDOM")" -F "NameEn=Firq FC" -F GovernorateId=5 -F WilayatId=$(sql "SELECT Id FROM Wilayats WHERE NameEn='Nizwa'") -F AreaId=$NIZWA_AREA -F ContactPhone=99123456 -F "Logo=@logo.png;type=image/png" > /dev/null
APP2=$(sql "SELECT TOP 1 Id FROM TeamApplications ORDER BY Id DESC")
TOK=$(token admin.jar /Admin/TeamApplications/Details/$APP2)
RV2=$(curl -s -b admin.jar $BASE/Admin/TeamApplications/Details/$APP2 | grep -o 'name="rowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "reject" 302 "$(post admin.jar /Admin/TeamApplications/Reject/$APP2 "$TOK" --data-urlencode "rowVersion=$RV2" -d "notes=$(urlenc "الشعار غير واضح")")"
check "rejected" 2 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP2")"
TOK=$(token user.jar /TeamApplications/Edit/$APP2)
RVU=$(curl -s -b user.jar $BASE/TeamApplications/Edit/$APP2 | grep -o 'name="RowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "resubmit" 302 "$(postm user.jar /TeamApplications/Edit/$APP2 "$TOK" -F "RowVersion=$RVU" -F "$(fv NameAr "فريق فرق المحدث $RANDOM")" -F "NameEn=Firq FC" -F GovernorateId=5 -F WilayatId=$(sql "SELECT Id FROM Wilayats WHERE NameEn='Nizwa'") -F AreaId=$NIZWA_AREA -F ContactPhone=99123456)"
check "back to pending" 0 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP2")"

echo "== Admin team edit: change primary area"
TOK=$(token admin.jar /Admin/Teams/Edit/$TEAMID)
RVT=$(curl -s -b admin.jar $BASE/Admin/Teams/Edit/$TEAMID | grep -o 'name="RowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "save with new primary" 302 "$(postm admin.jar /Admin/Teams/Save "$TOK" -F Id=$TEAMID -F "RowVersion=$RVT" -F "$(fv NameAr "$TEAM")" -F "NameEn=Al Khoudh Club" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$MABAILA -F AdditionalAreaIds=$KHOUDH -F Status=Approved -F PrimaryColor=#1cc683)"
check "primary moved" $MABAILA "$(sql "SELECT AreaId FROM TeamAreas WHERE TeamId=$TEAMID AND IsPrimary=1")"
check "still 2 areas" 2 "$(sql "SELECT COUNT(*) FROM TeamAreas WHERE TeamId=$TEAMID")"
TOK=$(token admin.jar /Admin/Teams/Edit/$TEAMID)
RVT=$(curl -s -b admin.jar $BASE/Admin/Teams/Edit/$TEAMID | grep -o 'name="RowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "save unchanged primary" 302 "$(postm admin.jar /Admin/Teams/Save "$TOK" -F Id=$TEAMID -F ShortName=KH -F "RowVersion=$RVT" -F "$(fv NameAr "$TEAM")" -F "NameEn=Al Khoudh Club" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$MABAILA -F AdditionalAreaIds=$KHOUDH -F Status=Approved -F PrimaryColor=#1cc683)"
check "primary kept" $MABAILA "$(sql "SELECT AreaId FROM TeamAreas WHERE TeamId=$TEAMID AND IsPrimary=1")"
check "stale rowversion save refused" 302 "$(postm admin.jar /Admin/Teams/Save "$TOK" -F Id=$TEAMID -F "RowVersion=$RVT" -F "$(fv NameAr "تعديل متزامن")" -F "NameEn=Al Khoudh Club" -F GovernorateId=1 -F WilayatId=$SEEB -F AreaId=$MABAILA -F Status=Approved)"
check "name not overwritten by stale save" 0 "$(sql "SELECT COUNT(*) FROM Teams WHERE Id=$TEAMID AND NameAr=N'تعديل متزامن'")"

echo "== Suspend / reactivate"
TOK=$(token admin.jar /Admin/Teams)
check "suspend" 302 "$(post admin.jar /Admin/Teams/ChangeStatus/$TEAMID "$TOK" -d status=Suspended)"
get guest.jar / > /dev/null
grep -q "$TEAM" body.html && bad "suspended team hidden from home" || ok "suspended team hidden from home"
check "reactivate" 302 "$(post admin.jar /Admin/Teams/ChangeStatus/$TEAMID "$TOK" -d status=Approved)"

echo "== Regions admin"
TOK=$(token admin.jar "/Admin/Regions/Create?level=Area&parentId=$SEEB")
check "create area" 302 "$(post admin.jar /Admin/Regions/Save "$TOK" -d Level=Area -d Id=0 -d ParentId=$SEEB -d "NameAr=$(urlenc "منطقة اختبار $RANDOM")" -d NameEn=TestArea -d IsActive=true)"
NEWAREA=$(sql "SELECT TOP 1 Id FROM Areas ORDER BY Id DESC")
check "duplicate area name refused" 200 "$(post admin.jar /Admin/Regions/Save "$TOK" -d Level=Area -d Id=0 -d ParentId=$SEEB -d "NameAr=$(urlenc الخوض)" -d NameEn=X -d IsActive=true)"
check "delete unused area" 302 "$(post admin.jar /Admin/Regions/Delete "$TOK" -d level=Area -d id=$NEWAREA)"
check "area gone" 0 "$(sql "SELECT COUNT(*) FROM Areas WHERE Id=$NEWAREA")"
check "delete used area blocked" 302 "$(post admin.jar /Admin/Regions/Delete "$TOK" -d level=Area -d id=$KHOUDH)"
check "used area still there" 1 "$(sql "SELECT COUNT(*) FROM Areas WHERE Id=$KHOUDH")"

echo "== Users admin"
UID2=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$EMAIL2'")
TOK=$(token admin.jar "/Admin/Users/Roles/$UID2")
check "grant MatchOperator" 302 "$(post admin.jar "/Admin/Users/Roles/$UID2" "$TOK" -d selectedRoles=MatchOperator)"
check "role granted" 1 "$(sql "SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId='$UID2' AND r.Name='MatchOperator'")"
check "deactivate" 302 "$(post admin.jar /Admin/Users/ToggleActive/$UID2 "$TOK")"
TOK=$(token other.jar /Account/Login 2>/dev/null)
rm -f other2.jar; TOK=$(token other2.jar /Account/Login)
post other2.jar /Account/Login "$TOK" -d Email=$EMAIL2 -d Password=Test@1234 > /dev/null
grep -q "هذا الحساب موقوف" body.html && ok "inactive user cannot log in" || bad "inactive login"
ADMINID=$(sql "SELECT Id FROM AspNetUsers WHERE Email='admin@s3sport.om'")
TOK=$(token admin.jar /Admin/Users)
post admin.jar /Admin/Users/ToggleActive/$ADMINID "$TOK" > /dev/null
check "admin cannot deactivate self" 1 "$(sql "SELECT CAST(IsActive AS int) FROM AspNetUsers WHERE Id='$ADMINID'")"

echo "== Proposed (unregistered) area"
IBRI=$(sql "SELECT TOP 1 w.Id FROM Wilayats w WHERE NOT EXISTS (SELECT 1 FROM Areas a WHERE a.WilayatId=w.Id) ORDER BY w.Id")  # any wilayat without areas
DHAHIRAH=$(sql "SELECT GovernorateId FROM Wilayats WHERE Id=$IBRI")
check "Ibri has no areas yet" 0 "$(sql "SELECT COUNT(*) FROM Areas WHERE WilayatId=$IBRI")"
TOK=$(token user.jar /TeamApplications/Create)
check "neither area nor name → refused" 200 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "فريق عبري $RANDOM")" -F "NameEn=Ibri FC" -F GovernorateId=$DHAHIRAH -F WilayatId=$IBRI -F ContactPhone=99123456 -F "Logo=@logo.png;type=image/png")"
grep -q "أو كتابة اسمها" body.html && ok "asks to pick or type an area" || bad "missing-area message"
PROPOSED="حي الاختبار $RANDOM"
IBRI_TEAM="فريق عبري $RANDOM"
TOK=$(token user.jar /TeamApplications/Create)
check "submit with proposed area" 302 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "$IBRI_TEAM")" -F "NameEn=Ibri FC" -F GovernorateId=$DHAHIRAH -F WilayatId=$IBRI -F "$(fv ProposedAreaName "$PROPOSED")" -F ContactPhone=99123456 -F "Logo=@logo.png;type=image/png")"
APP3=$(sql "SELECT TOP 1 Id FROM TeamApplications ORDER BY Id DESC")
check "stored without AreaId" 1 "$(sql "SELECT COUNT(*) FROM TeamApplications WHERE Id=$APP3 AND AreaId IS NULL AND ProposedAreaName=N'$PROPOSED'")"
check "no area created before approval" 0 "$(sql "SELECT COUNT(*) FROM Areas WHERE WilayatId=$IBRI")"
get admin.jar /Admin/TeamApplications/Details/$APP3 > /dev/null
grep -q "منطقة جديدة مقترحة" body.html && ok "admin sees proposed-area badge" || bad "proposed badge"
grep -q 'name="areaNameAr"' body.html && ok "admin approve form has area fields" || bad "area fields"
TOK=$(token admin.jar /Admin/TeamApplications/Details/$APP3)
RV3=$(curl -s -b admin.jar $BASE/Admin/TeamApplications/Details/$APP3 | grep -o 'name="rowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
check "approve with new area" 302 "$(post admin.jar /Admin/TeamApplications/Approve/$APP3 "$TOK" --data-urlencode "rowVersion=$RV3" -d "areaNameAr=$(urlenc "$PROPOSED")" -d "areaNameEn=Test Quarter")"
NEWAREA3=$(sql "SELECT Id FROM Areas WHERE WilayatId=$IBRI AND NameAr=N'$PROPOSED'")
[ -n "$NEWAREA3" ] && ok "area created under Ibri" || bad "area created under Ibri"
check "area English name" "Test Quarter" "$(sql "SELECT NameEn FROM Areas WHERE Id=$NEWAREA3")"
check "application linked to new area" "$NEWAREA3" "$(sql "SELECT AreaId FROM TeamApplications WHERE Id=$APP3")"
TEAM3=$(sql "SELECT CreatedTeamId FROM TeamApplications WHERE Id=$APP3")
check "team primary area = new area" "$NEWAREA3" "$(sql "SELECT AreaId FROM TeamAreas WHERE TeamId=$TEAM3 AND IsPrimary=1")"
check "area audit logged" 1 "$(sql "SELECT COUNT(*) FROM AuditLogs WHERE EntityName='Area' AND EntityId='$NEWAREA3'")"
echo "  · typed name that already exists is matched, not duplicated"
TOK=$(token user.jar /TeamApplications/Create)
check "submit proposing an existing name" 302 "$(postm user.jar /TeamApplications/Create "$TOK" -F "$(fv NameAr "فريق مكرر $RANDOM")" -F "NameEn=Dup Area FC" -F GovernorateId=$DHAHIRAH -F WilayatId=$IBRI -F "$(fv ProposedAreaName "$PROPOSED")" -F ContactPhone=99123456 -F "Logo=@logo.png;type=image/png")"
APP4=$(sql "SELECT TOP 1 Id FROM TeamApplications ORDER BY Id DESC")
check "existing area auto-selected" "$NEWAREA3" "$(sql "SELECT AreaId FROM TeamApplications WHERE Id=$APP4")"
check "proposed name cleared" 1 "$(sql "SELECT COUNT(*) FROM TeamApplications WHERE Id=$APP4 AND ProposedAreaName IS NULL")"
check "still exactly one such area" 1 "$(sql "SELECT COUNT(*) FROM Areas WHERE WilayatId=$IBRI AND NameAr=N'$PROPOSED'")"
echo "  · admin team form still requires a real area"
TOK=$(token admin.jar /Admin/Teams/Create)
check "admin create without area refused" 200 "$(postm admin.jar /Admin/Teams/Save "$TOK" -F Id=0 -F "$(fv NameAr "فريق إداري $RANDOM")" -F "NameEn=Admin FC" -F GovernorateId=$DHAHIRAH -F WilayatId=$IBRI -F "$(fv ProposedAreaName "لا يجب قبوله")" -F Status=Approved)"
grep -q "يرجى اختيار المنطقة." body.html && ok "admin form asks for area" || bad "admin form area message"

echo "== Malformed input"
TOK=$(token admin.jar /Admin/TeamApplications/Details/$APP2)
check "malformed rowVersion → handled, not 500" 302 "$(post admin.jar /Admin/TeamApplications/Approve/$APP2 "$TOK" -d "rowVersion=not-base64!!")"
check "still pending after malformed approve" 0 "$(sql "SELECT Status FROM TeamApplications WHERE Id=$APP2")"

echo "== Soft delete"
TOK=$(token admin.jar /Admin/Teams)
check "delete team" 302 "$(post admin.jar /Admin/Teams/Delete/$TEAMID "$TOK")"
check "row kept, flagged deleted" 1 "$(sql "SELECT CAST(IsDeleted AS int) FROM Teams WHERE Id=$TEAMID")"
get admin.jar /Admin/Teams > /dev/null
grep -q "$TEAM" body.html && bad "deleted team hidden in admin list" || ok "deleted team hidden in admin list"

echo
echo "RESULT: $PASS passed, $FAIL failed"
