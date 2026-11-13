#!/usr/bin/env bash
# Team-manager forms submitted the way a browser does: every hidden field (Id, TeamId, JoinedOn, RowVersion) is
# taken from the rendered page, not typed by the test. Guards against the route {id} (the team) leaking into a
# form's hidden Id (the player/match), which made "add player" fail with "غير مخول" and edits target other records.
cd "$(dirname "$0")"
. ./lib.sh
rm -f tf_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
hidden() { formval "$(cat $T/body.html)" "$1"; }   # value of a hidden input on the page just fetched

echo "== Setup: a team whose id equals an existing player id of ANOTHER team (the original failure)"
for n in A B; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'نموذج $n $R','Form $n $R','tf-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TA=$(sql "SELECT Id FROM Teams WHERE Slug='tf-A-$R'"); TB=$(sql "SELECT Id FROM Teams WHERE Slug='tf-B-$R'")
# Make sure a player with id = TA exists and belongs to team B.
sql "SET IDENTITY_INSERT Players ON; IF NOT EXISTS (SELECT 1 FROM Players WHERE Id=$TA) INSERT INTO Players(Id,SportId,FullNameAr,IsDeleted,CreatedAt) VALUES($TA,1,N'لاعب الفريق الآخر',0,SYSUTCDATETIME()); SET IDENTITY_INSERT Players OFF;
     IF NOT EXISTS (SELECT 1 FROM TeamPlayers WHERE PlayerId=$TA AND IsActive=1) INSERT INTO TeamPlayers(TeamId,PlayerId,ShirtNumber,JoinedOn,IsActive,IsCaptain,CreatedAt) VALUES($TB,$TA,99,'2026-01-01',1,0,SYSUTCDATETIME());" > /dev/null
TOK=$(token tf_m.jar "/Account/Register?type=user"); post tf_m.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مسؤول نموذج")" -d Email=tf-$R@test.om -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
TOK=$(token tf_a.jar /Account/Login); post tf_a.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
TOK=$(token tf_a.jar /Admin/Teams/Edit/$TA); post tf_a.jar /Admin/Teams/AddMember/$TA "$TOK" -d email=tf-$R@test.om -d role=Manager > /dev/null
check "manager of team A" 200 "$(get tf_m.jar /MyTeams/Squad/$TA)"

echo "== Add player (form as rendered)"
check "add-player form" 200 "$(get tf_m.jar /MyTeams/AddPlayer/$TA)"
check "hidden Id is 0 for a new player (not the team id)" 0 "$(hidden Id)"
check "hidden TeamId is the team" "$TA" "$(hidden TeamId)"
JOINED=$(hidden JoinedOn)
check "joined date in ISO format" 1 "$(echo "$JOINED" | grep -c -E '^[0-9]{4}-[0-9]{2}-[0-9]{2}$')"
TOK=$(token tf_m.jar /MyTeams/AddPlayer/$TA)
check "save new player" 302 "$(postm tf_m.jar /MyTeams/SavePlayer "$TOK" -F "Id=$(hidden Id)" -F "TeamId=$TA" -F "JoinedOn=$JOINED" -F "RowVersion=" -F "$(fv FullNameAr "لاعب جديد $R")" -F ShirtNumber=10)"
check "…back on the squad, not access denied" "/MyTeams/Squad/$TA" "$(cat $T/location)"
NEWP=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب جديد $R'")
check "player created in team A with today's join date" 1 "$(sql "SELECT COUNT(*) FROM TeamPlayers WHERE PlayerId=$NEWP AND TeamId=$TA AND IsActive=1 AND JoinedOn='$JOINED'")"
check "other team's player untouched" 1 "$(sql "SELECT COUNT(*) FROM Players WHERE Id=$TA AND FullNameAr NOT LIKE N'%$R%' AND IsDeleted=0")"

echo "== Edit player (form as rendered)"
check "edit form" 200 "$(get tf_m.jar "/MyTeams/EditPlayer/$TA?playerId=$NEWP")"
check "hidden Id is the player" "$NEWP" "$(hidden Id)"
TOK=$(token tf_m.jar "/MyTeams/EditPlayer/$TA?playerId=$NEWP")
check "save edit" 302 "$(postm tf_m.jar /MyTeams/SavePlayer "$TOK" -F "Id=$(hidden Id)" -F "TeamId=$(hidden TeamId)" -F "RowVersion=$(hidden RowVersion)" -F "$(fv FullNameAr "لاعب معدّل $R")" -F ShirtNumber=11)"
check "the right player was edited" 1 "$(sql "SELECT COUNT(*) FROM Players WHERE Id=$NEWP AND FullNameAr=N'لاعب معدّل $R'")"

echo "== Friendly match (form as rendered)"
# Admin follows team B with notifications off: muting must keep "new match" away even though it goes to every user.
TOK=$(token tf_a.jar /Teams); post tf_a.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$TB > /dev/null
post tf_a.jar /Follows/ToggleNotifications "$TOK" -d targetType=Team -d targetId=$TB > /dev/null
ADMID=$(sql "SELECT Id FROM AspNetUsers WHERE Email='admin@s3sport.om'")
check "admin muted team B" 0 "$(sql "SELECT CAST(NotificationsEnabled AS int) FROM UserFollows WHERE UserId='$ADMID' AND TeamId=$TB")"
check "create-match form" 200 "$(get tf_m.jar /MyTeams/CreateMatch/$TA)"
check "hidden Id is 0 for a new match" 0 "$(hidden Id)"
TOK=$(token tf_m.jar /MyTeams/CreateMatch/$TA)
check "save new match" 302 "$(post tf_m.jar /MyTeams/SaveMatch "$TOK" -d "Id=$(hidden Id)" -d "TeamId=$(hidden TeamId)" -d OpponentTeamId=$TB -d IsHome=true -d KickoffLocal=2026-12-20T17:00)"
M=$(sql "SELECT Id FROM Matches WHERE OrganizerTeamId=$TA")
check "match created" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE OrganizerTeamId=$TA")"
check "new match notification reaches users who follow neither team" 1 "$(sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE n.MatchId=$M AND n.Type=12 AND u.Email='tf-$R@test.om'")"
EXP=$(sql "SELECT COUNT(*) - 1 FROM AspNetUsers WHERE IsActive=1")
check "…every active user once, minus the one who muted" "$EXP/$EXP" "$(sql "SELECT COUNT(DISTINCT UserId) FROM Notifications WHERE MatchId=$M AND Type=12")/$(sql "SELECT COUNT(*) FROM Notifications WHERE MatchId=$M AND Type=12")"
check "…except who muted one of the teams" 0 "$(sql "SELECT COUNT(*) FROM Notifications WHERE MatchId=$M AND Type=12 AND UserId='$ADMID'")"
check "edit-match form" 200 "$(get tf_m.jar "/MyTeams/EditMatch/$TA?matchId=$M")"
check "hidden Id is the match" "$M" "$(hidden Id)"
TOK=$(token tf_m.jar "/MyTeams/EditMatch/$TA?matchId=$M")
check "save edit" 302 "$(post tf_m.jar /MyTeams/SaveMatch "$TOK" -d "Id=$(hidden Id)" -d "TeamId=$(hidden TeamId)" -d OpponentTeamId=$TB -d IsHome=false -d KickoffLocal=2026-12-21T18:00 -d Status=Scheduled --data-urlencode "RowVersion=$(hidden RowVersion)")"
check "the right match was edited" "$TB/$TA" "$(sql "SELECT CONCAT(HomeTeamId,'/',AwayTeamId) FROM Matches WHERE Id=$M")"
check "match type is optional: saved as a friendly" NULL "$(sql "SELECT ISNULL(TournamentName,'NULL') FROM Matches WHERE Id=$M")"

echo "== Match type: friendly or tournament (optional; a tournament needs its name)"
TNAME="بطولة الولاية $R"
savematch() { # IsTournament [TournamentName]
  get tf_m.jar "/MyTeams/EditMatch/$TA?matchId=$M" > /dev/null
  local rv; rv=$(hidden RowVersion); local tok; tok=$(token tf_m.jar "/MyTeams/EditMatch/$TA?matchId=$M")
  post tf_m.jar /MyTeams/SaveMatch "$tok" -d "Id=$M" -d "TeamId=$TA" -d OpponentTeamId=$TB -d IsHome=true -d KickoffLocal=2026-12-21T18:00 \
       -d Status=Scheduled --data-urlencode "RowVersion=$rv" -d "IsTournament=$1" -d "TournamentName=$(urlenc "${2:-}")"
}
get tf_m.jar "/MyTeams/EditMatch/$TA?matchId=$M" > /dev/null
check "edit form: «ودية» selected by default" 1 "$(grep -c 'id="kind-friendly" value="false" checked' $T/body.html)"
check "tournament without a name is refused (form shown again)" 200 "$(savematch true)"
check "…with the reason" 1 "$(grep -c 'اكتب اسم البطولة' $T/body.html)"
check "…and nothing saved" NULL "$(sql "SELECT ISNULL(TournamentName,'NULL') FROM Matches WHERE Id=$M")"
check "tournament with its name saved" 302 "$(savematch true "  $TNAME  ")"
check "…name stored trimmed" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE Id=$M AND TournamentName=N'$TNAME'")"
get tf_m.jar "/MyTeams/EditMatch/$TA?matchId=$M" > /dev/null
check "edit form: «بطولة» selected and name filled" "1/1" "$(grep -c 'id="kind-tournament" value="true" checked' $T/body.html)/$(grep -c "value=\"$TNAME\"" $T/body.html)"
get tf_m.jar "/matches/$M" > /dev/null
check "public match page shows the tournament (not «مباراة ودية»)" "1/0" "$(grep -c "<span>$TNAME</span>" $T/body.html)/$(grep -c '<span>مباراة ودية</span>' $T/body.html)"
check "…and in the info tab" 1 "$(grep -c "بطولة: $TNAME" $T/body.html)"
get tf_m.jar "/MyTeams/Matches/$TA" > /dev/null
check "team's match list shows the tournament" 1 "$(grep -c "$TNAME" $T/body.html)"
get tf_m.jar "/matches?date=2026-12-21&all=true" > /dev/null
check "match centre groups it under the tournament" 1 "$(grep -c "$TNAME" $T/body.html)"
check "back to friendly drops the name" 302 "$(savematch false "$TNAME")"
check "…name cleared" NULL "$(sql "SELECT ISNULL(TournamentName,'NULL') FROM Matches WHERE Id=$M")"
get tf_m.jar "/matches/$M" > /dev/null
check "…page shows «مباراة ودية» again" 1 "$(grep -c '<span>مباراة ودية</span>' $T/body.html)"

echo "== Admin sets the type on a match already played (the team form no longer edits it)"
LNAME="دوري ملتقى البداية $R"
sql "UPDATE Matches SET Status=3, HomeScore=2, AwayScore=1 WHERE Id=$M" > /dev/null   # 3 = Finished
check "admin edit form has the match type" 200 "$(get tf_a.jar /Admin/Matches/Edit/$M)"
check "…«ودية» selected" 1 "$(grep -c 'id="kind-friendly" value="false" checked' $T/body.html)"
adminsave() { # IsTournament [TournamentName]
  get tf_a.jar /Admin/Matches/Edit/$M > /dev/null
  local rv; rv=$(hidden RowVersion); local tok; tok=$(token tf_a.jar /Admin/Matches/Edit/$M)
  post tf_a.jar /Admin/Matches/Save "$tok" -d Id=$M -d HomeTeamId=$TA -d AwayTeamId=$TB -d KickoffLocal=2026-12-21T18:00 \
       -d Status=Finished -d ResultHome=2 -d ResultAway=1 --data-urlencode "RowVersion=$rv" -d "IsTournament=$1" -d "TournamentName=$(urlenc "${2:-}")"
}
check "tournament without a name refused" 200 "$(adminsave true)"
check "…with the reason" 1 "$(grep -c 'اكتب اسم البطولة' $T/body.html)"
check "admin saves the league name" 302 "$(adminsave true "$LNAME")"
check "…stored, result kept" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE Id=$M AND TournamentName=N'$LNAME' AND HomeScore=2 AND AwayScore=1 AND Status=3")"
get tf_a.jar /Admin/Matches/Edit/$M > /dev/null
check "…form shows it on reopen" "1/1" "$(grep -c 'id="kind-tournament" value="true" checked' $T/body.html)/$(grep -c "value=\"$LNAME\"" $T/body.html)"
get tf_a.jar "/matches/$M" > /dev/null
check "public page shows the league" 1 "$(grep -c "<span>$LNAME</span>" $T/body.html)"
check "admin form for a platform-competition match has no type choice" 0 "$(get tf_a.jar /Admin/Matches/Edit/$(sql "SELECT TOP 1 Id FROM Matches WHERE CompetitionSeasonId IS NOT NULL AND IsDeleted=0") > /dev/null; grep -c 'data-match-kind' $T/body.html)"

echo
echo "RESULT: $PASS passed, $FAIL failed"
