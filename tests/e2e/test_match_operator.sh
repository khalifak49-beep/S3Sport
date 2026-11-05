#!/usr/bin/env bash
# The admin hands a match to a team's own manager (the account that registered the team) as its event operator.
# Team managers appear in the "مسؤول تسجيل الأحداث" list, may be assigned only to matches of their own team,
# and the assigned manager can then run the live screen for that match and no other.
cd "$(dirname "$0")"
. ./lib.sh
rm -f mo_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")

echo "== Setup: three approved teams, managers for A and C"
for n in A B C; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'مشغل $n $R','Op $n $R','mo-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TA=$(sql "SELECT Id FROM Teams WHERE Slug='mo-A-$R'"); TB=$(sql "SELECT Id FROM Teams WHERE Slug='mo-B-$R'"); TC=$(sql "SELECT Id FROM Teams WHERE Slug='mo-C-$R'")
for n in a c; do
  TOK=$(token mo_$n.jar "/Account/Register?type=user"); post mo_$n.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مسؤول $n")" -d Email=mo-$n-$R@test.om -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
done
UA=$(sql "SELECT Id FROM AspNetUsers WHERE Email='mo-a-$R@test.om'"); UC=$(sql "SELECT Id FROM AspNetUsers WHERE Email='mo-c-$R@test.om'")
TOK=$(token mo_adm.jar /Account/Login); post mo_adm.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
TOK=$(token mo_adm.jar /Admin/Teams/Edit/$TA); post mo_adm.jar /Admin/Teams/AddMember/$TA "$TOK" -d email=mo-a-$R@test.om -d role=Manager > /dev/null
TOK=$(token mo_adm.jar /Admin/Teams/Edit/$TC); post mo_adm.jar /Admin/Teams/AddMember/$TC "$TOK" -d email=mo-c-$R@test.om -d role=Manager > /dev/null

echo "== The operator list offers team managers"
check "new-match form" 200 "$(get mo_adm.jar /Admin/Matches/Create)"
check "manager of A listed" 1 "$(grep -c "value=\"$UA\"" $T/body.html)"
check "manager of C listed" 1 "$(grep -c "value=\"$UC\"" $T/body.html)"

save() { # save OPERATOR -> status; creates a friendly A vs B
  local tok=$(token mo_adm.jar /Admin/Matches/Create)
  post mo_adm.jar /Admin/Matches/Save "$tok" -d Id=0 -d HomeTeamId=$TA -d AwayTeamId=$TB -d KickoffLocal=2026-12-24T17:00 -d Status=Scheduled -d "OperatorUserId=$1"
}
echo "== Assigning"
check "manager of C refused for A vs B" 200 "$(save $UC)"
check "…with a reason" 1 "$(grep -c 'مسؤول الفريق يسجّل مباريات فريقه فقط' $T/body.html)"
check "no match saved" 0 "$(sql "SELECT COUNT(*) FROM Matches WHERE HomeTeamId=$TA AND AwayTeamId=$TB")"
check "manager of A accepted for A vs B" 302 "$(save $UA)"
M=$(sql "SELECT Id FROM Matches WHERE HomeTeamId=$TA AND AwayTeamId=$TB")
check "operator stored" "$UA" "$(sql "SELECT OperatorUserId FROM Matches WHERE Id=$M")"

echo "== The assigned manager runs it live"
TOK=$(token mo_a.jar /Account/Login); post mo_a.jar /Account/Login "$TOK" -d Email=mo-a-$R@test.om -d Password=Test@1234 > /dev/null
check "manager A opens live screen" 200 "$(get mo_a.jar /MatchControl/Index/$M)"
TOK=$(token mo_c.jar /Account/Login); post mo_c.jar /Account/Login "$TOK" -d Email=mo-c-$R@test.om -d Password=Test@1234 > /dev/null
check "manager C cannot" DENIED "$(denied "$(get mo_c.jar /MatchControl/Index/$M)")"

echo
echo "RESULT: $PASS passed, $FAIL failed"
