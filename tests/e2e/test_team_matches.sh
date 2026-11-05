#!/usr/bin/env bash
# Team managers: friendlies (platform or guest opponent), live recording by the organiser, follower notifications, isolation.
cd "$(dirname "$0")"
. ./lib.sh
rm -f tm_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM

uuid() { cat /proc/sys/kernel/random/uuid 2>/dev/null || powershell -NoProfile -Command "[guid]::NewGuid().ToString()" | tr -d '\r'; }
ev() { local jar=$1 type=$2; shift 2; local tok=$(token $jar /Teams)
  local args=(-d EventType=$type -d ClientRequestId=$(uuid)); for kv in "$@"; do args+=(-d "$kv"); done
  post $jar /MatchControl/AddEvent/$M "$tok" "${args[@]}"; }
register() { local tok=$(token $1 /Account/Register)
  post $1 /Account/Register "$tok" -d "FullName=$(urlenc "$2")" -d Email=$3 -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null; }
login() { local tok=$(token $1 /Account/Login); post $1 /Account/Login "$tok" -d Email=$2 --data-urlencode "Password=$3" > /dev/null; }
notes() { sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$FAN' AND n.MatchId=$M AND n.Type=$1"; }

echo "== Setup: two approved teams, two managers, one fan"
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
for n in A B; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'فريق $n $R','Team $n $R','tm-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TA=$(sql "SELECT Id FROM Teams WHERE Slug='tm-A-$R'"); TB=$(sql "SELECT Id FROM Teams WHERE Slug='tm-B-$R'")
MA=mgr-a-$R@test.om; MB=mgr-b-$R@test.om; FAN=fan-$R@test.om
register tm_a.jar "مسؤول أ" $MA; register tm_b.jar "مسؤول ب" $MB; register tm_fan.jar "مشجع" $FAN
login tm_admin.jar admin@s3sport.om "$ADMIN_PW"
TOK=$(token tm_admin.jar /Admin/Teams/Edit/$TA)
post tm_admin.jar /Admin/Teams/AddMember/$TA "$TOK" -d email=$MA -d role=Manager > /dev/null
post tm_admin.jar /Admin/Teams/AddMember/$TB "$TOK" -d email=$MB -d role=Manager > /dev/null
# Re-login so the new TeamManager role is in the cookie.
rm -f tm_a.jar tm_b.jar; login tm_a.jar $MA Test@1234; login tm_b.jar $MB Test@1234
TOK=$(token tm_fan.jar /Teams); post tm_fan.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$TA > /dev/null
check "fan follows team A" 1 "$(sql "SELECT COUNT(*) FROM UserFollows f JOIN AspNetUsers u ON u.Id=f.UserId WHERE u.Email='$FAN' AND f.TeamId=$TA")"

echo "== Squad: manager registers players"
for n in 1 2 3; do
  TOK=$(token tm_a.jar /MyTeams/AddPlayer/$TA)
  postm tm_a.jar /MyTeams/SavePlayer "$TOK" -F Id=0 -F TeamId=$TA -F "$(fv FullNameAr "لاعب $n $R")" -F ShirtNumber=$n -F JoinedOn=2026-11-01 > /dev/null
done
check "3 players in squad" 3 "$(sql "SELECT COUNT(*) FROM TeamPlayers WHERE TeamId=$TA AND IsActive=1")"
P1=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب 1 $R'"); P2=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب 2 $R'"); P3=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب 3 $R'")

echo "== Pages"
check "matches page" 200 "$(get tm_a.jar /MyTeams/Matches/$TA)"
check "create page" 200 "$(get tm_a.jar /MyTeams/CreateMatch/$TA)"
check "menu link to live screen list" 200 "$(get tm_a.jar /MatchControl/Mine)"
check "other team's matches page" DENIED "$(denied $(get tm_a.jar /MyTeams/Matches/$TB))"
check "fan cannot open create page" DENIED "$(denied $(get tm_fan.jar /MyTeams/CreateMatch/$TA))"

echo "== Validation"
TOK=$(token tm_a.jar /MyTeams/CreateMatch/$TA)
check "no opponent refused" 200 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d IsHome=true -d KickoffLocal=2026-11-10T17:00)"
check "both opponent kinds refused" 200 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d OpponentTeamId=$TB -d "OpponentGuestName=$(urlenc ضيف)" -d IsHome=true -d KickoffLocal=2026-11-10T17:00)"
check "self as opponent refused" 200 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d OpponentTeamId=$TA -d IsHome=true -d KickoffLocal=2026-11-10T17:00)"
check "creating for another team refused" DENIED "$(denied $(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TB -d OpponentTeamId=$TA -d IsHome=true -d KickoffLocal=2026-11-10T17:00))"
check "no match created" 0 "$(sql "SELECT COUNT(*) FROM Matches WHERE OrganizerTeamId IN ($TA,$TB)")"

echo "== Friendly vs platform team"
check "create friendly" 302 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d OpponentTeamId=$TB -d IsHome=true -d KickoffLocal=2026-11-10T17:00 -d "RefereeName=$(urlenc "حكم")")"
M=$(sql "SELECT Id FROM Matches WHERE OrganizerTeamId=$TA")
check "stored as friendly organised by A, A at home" "$TA/$TB/0" "$(sql "SELECT CONCAT(HomeTeamId,'/',AwayTeamId,'/',ISNULL(CompetitionSeasonId,0)) FROM Matches WHERE Id=$M")"
check "fan notified of new match" 1 "$(sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$FAN' AND n.Type=12")"
check "public match page" 200 "$(get tm_fan.jar /matches/$M)"
check "listed in public matches" 1 "$(curl -s "$BASE/Matches?date=2026-11-10" | grep -c "/matches/$M\"")"
check "opponent manager cannot edit" 404 "$(get tm_b.jar "/MyTeams/EditMatch/$TB?matchId=$M")"
check "opponent manager cannot operate" DENIED "$(denied $(get tm_b.jar /MatchControl/Index/$M))"
check "edit page" 200 "$(get tm_a.jar "/MyTeams/EditMatch/$TA?matchId=$M")"
RV=$(formval "$(cat $T/body.html)" RowVersion)
TOK=$(token tm_a.jar "/MyTeams/EditMatch/$TA?matchId=$M")
check "reschedule" 302 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=$M -d TeamId=$TA -d OpponentTeamId=$TB -d IsHome=false -d KickoffLocal=2026-11-11T18:00 -d Status=Scheduled --data-urlencode "RowVersion=$RV")"
check "home/away swapped" "$TB/$TA" "$(sql "SELECT CONCAT(HomeTeamId,'/',AwayTeamId) FROM Matches WHERE Id=$M")"
check "stale edit refused" 302 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=$M -d TeamId=$TA -d OpponentTeamId=$TB -d IsHome=true -d KickoffLocal=2026-11-12T18:00 -d Status=Scheduled --data-urlencode "RowVersion=$RV")"
check "still swapped after stale edit" "$TB/$TA" "$(sql "SELECT CONCAT(HomeTeamId,'/',AwayTeamId) FROM Matches WHERE Id=$M")"

echo "== Live recording by the organiser"
check "operator screen" 200 "$(get tm_a.jar /MatchControl/Index/$M)"
TOK=$(token tm_a.jar /MatchControl/Index/$M)
check "save lineup" 302 "$(post tm_a.jar /MatchControl/SaveLineup/$M "$TOK" -d awayStarters=$P1 -d awayStarters=$P2 -d awayBench=$P3)"
check "goal before start refused" 400 "$(ev tm_a.jar Goal TeamId=$TA PlayerId=$P1)"
check "kick-off" 200 "$(ev tm_a.jar MatchStarted)"
check "fan: match started" 1 "$(notes 0)"
check "goal" 200 "$(ev tm_a.jar Goal TeamId=$TA PlayerId=$P1 RelatedPlayerId=$P2 Minute=12)"
check "fan: goal" 1 "$(notes 1)"
check "goal message names scorer and minute" 1 "$(sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$FAN' AND n.MatchId=$M AND n.Type=1 AND n.Message LIKE N'%لاعب 1 $R%' AND n.Message LIKE '%(12'')%'")"
check "yellow card" 200 "$(ev tm_a.jar YellowCard TeamId=$TB Minute=20)"
check "fan: card" 1 "$(notes 9)"
check "missed penalty" 200 "$(ev tm_a.jar MissedPenalty TeamId=$TB Minute=30)"
check "fan: penalty" 1 "$(notes 10)"
check "half-time" 200 "$(ev tm_a.jar FirstHalfEnded)"
check "fan: half-time" 1 "$(notes 2)"
check "substitution at half-time" 200 "$(ev tm_a.jar Substitution TeamId=$TA PlayerId=$P2 RelatedPlayerId=$P3 Minute=45)"
check "fan: substitution" 1 "$(notes 11)"
check "second half" 200 "$(ev tm_a.jar SecondHalfStarted)"
check "fan: second half" 1 "$(notes 8)"
check "penalty goal for guest side without a player" 200 "$(ev tm_a.jar PenaltyGoal TeamId=$TB Minute=60)"
check "red card" 200 "$(ev tm_a.jar RedCard TeamId=$TA PlayerId=$P1 Minute=80)"
check "score 1-1 (home B, away A)" "1-1" "$(sql "SELECT CONCAT(HomeScore,'-',AwayScore) FROM Matches WHERE Id=$M")"
check "edit refused once started" 302 "$(post tm_a.jar /MyTeams/SaveMatch "$(token tm_a.jar /MyTeams/Matches/$TA)" -d Id=$M -d TeamId=$TA -d OpponentTeamId=$TB -d IsHome=true -d KickoffLocal=2026-11-12T18:00 -d Status=Scheduled)"
check "delete refused once started" 302 "$(post tm_a.jar "/MyTeams/DeleteMatch/$TA?matchId=$M" "$(token tm_a.jar /MyTeams/Matches/$TA)")"
check "full time" 200 "$(ev tm_a.jar MatchEnded)"
check "finished" 3 "$(sql "SELECT Status FROM Matches WHERE Id=$M")"
check "fan: full time" 1 "$(notes 3)"
check "event timeline public" 1 "$(curl -s "$BASE/matches/$M" | grep -c 'id="matchState"')"

echo "== Friendly vs guest team"
TOK=$(token tm_a.jar /MyTeams/CreateMatch/$TA)
check "create vs guest" 302 "$(post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d "OpponentGuestName=$(urlenc "شباب القرية $R")" -d IsHome=true -d KickoffLocal=2026-11-20T17:00)"
G=$(sql "SELECT Id FROM Teams WHERE NameAr=N'شباب القرية $R'")
check "guest team created hidden" 4 "$(sql "SELECT Status FROM Teams WHERE Id=$G")"
M2=$(sql "SELECT Id FROM Matches WHERE OrganizerTeamId=$TA AND AwayTeamId=$G")
check "guest match public page" 200 "$(get tm_fan.jar /matches/$M2)"
check "no link to guest team page" 0 "$(grep -c "/teams/guest-" $T/body.html)"
check "guest not in teams list" 0 "$(curl -s "$BASE/Teams?all=true" | grep -c "شباب القرية $R")"
check "guest not followable" 404 "$(curl -s -o /dev/null -w '%{http_code}' -b tm_fan.jar -X POST $BASE/Follows/Follow -H "RequestVerificationToken: $(token tm_fan.jar /Teams)" -d targetType=Team -d targetId=$G)"
TOK=$(token tm_a.jar /MyTeams/CreateMatch/$TA)
post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d "OpponentGuestName=$(urlenc "شباب القرية $R")" -d IsHome=false -d KickoffLocal=2026-11-27T17:00 > /dev/null
check "same guest name reused" 1 "$(sql "SELECT COUNT(*) FROM Teams WHERE NameAr=N'شباب القرية $R'")"
check "guest offered in opponent list" 1 "$(curl -s -b tm_a.jar "$BASE/MyTeams/CreateMatch/$TA" | grep -c "value=\"$G\"")"
get tm_a.jar "/MyTeams/EditMatch/$TA?matchId=$M2" > /dev/null; RV=$(formval "$(cat $T/body.html)" RowVersion)
check "manager sets referee" 302 "$(post tm_a.jar /MyTeams/SaveMatch "$(token tm_a.jar /MyTeams/Matches/$TA)" -d Id=$M2 -d TeamId=$TA -d OpponentTeamId=$G -d IsHome=true -d KickoffLocal=2026-11-20T17:00 -d Status=Scheduled -d "RefereeName=$(urlenc "حكم")" --data-urlencode "RowVersion=$RV")"
check "referee stored" 1 "$(sql "SELECT COUNT(*) FROM MatchOfficials WHERE MatchId=$M2")"
check "admin can edit guest friendly" 200 "$(get tm_admin.jar /Admin/Matches/Edit/$M2)"
RV=$(formval "$(cat $T/body.html)" RowVersion)
check "admin saves with referee cleared" 302 "$(post tm_admin.jar /Admin/Matches/Save "$(token tm_admin.jar /Admin/Matches/Edit/$M2)" -d Id=$M2 -d HomeTeamId=$TA -d AwayTeamId=$G -d KickoffLocal=2026-11-21T17:00 -d Status=Scheduled --data-urlencode "RowVersion=$RV")"
check "referee removed" 0 "$(sql "SELECT COUNT(*) FROM MatchOfficials WHERE MatchId=$M2")"
check "organiser kept" $TA "$(sql "SELECT OrganizerTeamId FROM Matches WHERE Id=$M2")"
TOK=$(token tm_a.jar /MyTeams/Matches/$TA)
check "delete unstarted friendly" 302 "$(post tm_a.jar "/MyTeams/DeleteMatch/$TA?matchId=$M2" "$TOK")"
check "soft-deleted" 1 "$(sql "SELECT IsDeleted FROM Matches WHERE Id=$M2")"
check "other manager cannot delete" 404 "$(post tm_b.jar "/MyTeams/DeleteMatch/$TB?matchId=$M" "$(token tm_b.jar /MyTeams/Matches/$TB)")"

echo "== Live vs guest team: typed player names"
TOK=$(token tm_a.jar /MyTeams/CreateMatch/$TA)
post tm_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d OpponentTeamId=$G -d IsHome=true -d KickoffLocal=2026-11-28T17:00 > /dev/null
M=$(sql "SELECT MAX(Id) FROM Matches WHERE OrganizerTeamId=$TA AND AwayTeamId=$G AND IsDeleted=0")
check "operator screen marks guest side" 1 "$(curl -s -b tm_a.jar $BASE/MatchControl/Index/$M | grep -c 'data-away-guest="1"')"
check "name inputs present" 2 "$(curl -s -b tm_a.jar $BASE/MatchControl/Index/$M | grep -o 'name="PlayerName"' | wc -l)"
ev tm_a.jar MatchStarted > /dev/null
check "guest goal with typed scorer" 200 "$(ev tm_a.jar Goal TeamId=$G "PlayerName=$(urlenc "سعيد الضيف")" Minute=10)"
check "scorer name stored" 1 "$(sql "SELECT COUNT(*) FROM MatchEvents WHERE MatchId=$M AND PlayerNameText=N'سعيد الضيف' AND PlayerId IS NULL")"
check "goal notification names guest scorer" 1 "$(sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$FAN' AND n.MatchId=$M AND n.Type=1 AND n.Message LIKE N'%سعيد الضيف%'")"
check "guest substitution needs both names" 400 "$(ev tm_a.jar Substitution TeamId=$G "PlayerName=$(urlenc "خارج")" Minute=30)"
check "guest substitution same name refused" 400 "$(ev tm_a.jar Substitution TeamId=$G "PlayerName=$(urlenc "نفس")" "RelatedPlayerName=$(urlenc "نفس")" Minute=30)"
check "guest substitution with names" 200 "$(ev tm_a.jar Substitution TeamId=$G "PlayerName=$(urlenc "خالد")" "RelatedPlayerName=$(urlenc "ماجد")" Minute=31)"
check "fan: substitution names" 1 "$(sql "SELECT COUNT(*) FROM Notifications n JOIN AspNetUsers u ON u.Id=n.UserId WHERE u.Email='$FAN' AND n.MatchId=$M AND n.Type=11 AND n.Message LIKE N'%ماجد%' AND n.Message LIKE N'%خالد%'")"
check "live state shows typed names" 1 "$(curl -s -b tm_a.jar $BASE/MatchControl/State/$M | grep -o '"relatedPlayerName":"ماجد"' | wc -l)"
check "own team: typed name ignored, player id used" 200 "$(ev tm_a.jar Goal TeamId=$TA PlayerId=$P1 "PlayerName=$(urlenc "مزيف")" Minute=40)"
check "no typed name on platform team event" 0 "$(sql "SELECT COUNT(*) FROM MatchEvents WHERE MatchId=$M AND TeamId=$TA AND PlayerNameText IS NOT NULL")"
check "own team substitution still needs registered players" 400 "$(ev tm_a.jar Substitution TeamId=$TA "PlayerName=$(urlenc "أ")" "RelatedPlayerName=$(urlenc "ب")" Minute=41)"
EID=$(sql "SELECT Id FROM MatchEvents WHERE MatchId=$M AND PlayerNameText=N'سعيد الضيف'")
TOK=$(token tm_a.jar /MatchControl/Index/$M)
check "correct guest scorer name" 200 "$(post tm_a.jar "/MatchControl/CorrectEvent/$M?eventId=$EID" "$TOK" -d TeamId=$G -d "PlayerName=$(urlenc "سعيد العامري")" -d Minute=11 -d "Reason=$(urlenc "تصحيح الاسم")")"
check "name corrected, history kept" "1/1" "$(sql "SELECT CONCAT(SUM(CASE WHEN e.PlayerNameText=N'سعيد العامري' THEN 1 ELSE 0 END),'/',(SELECT COUNT(*) FROM MatchEventRevisions WHERE MatchEventId=$EID)) FROM MatchEvents e WHERE e.Id=$EID")"
check "score 1-1" "1-1" "$(sql "SELECT CONCAT(HomeScore,'-',AwayScore) FROM Matches WHERE Id=$M")"

echo
echo "RESULT: $PASS passed, $FAIL failed"
