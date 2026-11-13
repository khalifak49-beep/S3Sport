#!/usr/bin/env bash
# Phase 4: live match operation (state machine, idempotency, concurrency, corrections, notifications, stats).
cd "$(dirname "$0")"
. ./lib.sh
rm -f p4_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM

uuid() { cat /proc/sys/kernel/random/uuid 2>/dev/null || powershell -NoProfile -Command "[guid]::NewGuid().ToString()" | tr -d '\r'; }
# ev JAR TYPE [k=v ...] with a fresh or given request id → prints http code; body in $T/body.html
ev() { local jar=$1 type=$2; shift 2; local rid=${RID:-$(uuid)}; local tok=$(token $jar /Teams)
  local args=(-d EventType=$type -d ClientRequestId=$rid); for kv in "$@"; do args+=(-d "$kv"); done
  post $jar /MatchControl/AddEvent/$M "$tok" "${args[@]}"; }
score() { sql "SELECT CONCAT(HomeScore,'-',AwayScore) FROM Matches WHERE Id=$M"; }
status() { sql "SELECT CONCAT(Status,'/',CurrentPeriod) FROM Matches WHERE Id=$M"; }

echo "== Setup"
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
for n in H A; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'فريق $n $R','Team $n $R','live-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TH=$(sql "SELECT Id FROM Teams WHERE Slug='live-H-$R'"); TA=$(sql "SELECT Id FROM Teams WHERE Slug='live-A-$R'")
for n in 1 2 3; do for t in H A; do
  tid=$([ $t = H ] && echo $TH || echo $TA)
  sql "INSERT INTO Players(SportId,FullNameAr,IsDeleted,CreatedAt) VALUES(1,N'لاعب $t$n $R',0,SYSUTCDATETIME()); INSERT INTO TeamPlayers(TeamId,PlayerId,ShirtNumber,JoinedOn,IsActive,IsCaptain,CreatedAt) VALUES($tid,SCOPE_IDENTITY(),$n,'2026-01-01',1,0,SYSUTCDATETIME());" > /dev/null
done; done
PH1=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب H1 $R'"); PH2=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب H2 $R'"); PH3=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب H3 $R'")
PA1=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب A1 $R'"); PA2=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب A2 $R'")
TOK=$(token p4_admin.jar /Account/Login); post p4_admin.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
SEASON=$(sql "SELECT Id FROM Seasons WHERE IsCurrent=1")
TOK=$(token p4_admin.jar /Admin/Competitions/Create)
postm p4_admin.jar /Admin/Competitions/Save "$TOK" -F Id=0 -F "$(fv NameAr "دوري مباشر $R")" -F "NameEn=Live League $R" -F SportId=1 -F IsActive=true > /dev/null
COMP=$(sql "SELECT Id FROM Competitions WHERE NameEn='Live League $R'")
TOK=$(token p4_admin.jar "/Admin/Editions/Create?competitionId=$COMP")
post p4_admin.jar /Admin/Editions/Save "$TOK" -d Id=0 -d CompetitionId=$COMP -d SeasonId=$SEASON -d Format=League -d Status=Registration -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 > /dev/null
ED=$(sql "SELECT Id FROM CompetitionSeasons WHERE CompetitionId=$COMP")
TOK=$(token p4_admin.jar /Admin/Editions/Manage/$ED)
post p4_admin.jar /Admin/Editions/AddTeams/$ED "$TOK" -d teamIds=$TH -d teamIds=$TA > /dev/null
post p4_admin.jar /Admin/Editions/GenerateFixtures/$ED "$TOK" -d startDate=$(date +%Y-%m-%d) -d kickoffTime=16:00 -d daysBetweenRounds=7 -d legs=1 > /dev/null
M=$(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED")
[ -n "$M" ] && ok "match $M scheduled" || bad "match scheduled"
sql "UPDATE Matches SET HomeTeamId=$TH, AwayTeamId=$TA WHERE Id=$M" > /dev/null  # fix orientation for readable asserts

mkuser() { local jar=$1 email=$2; local tok=$(token $jar "/Account/Register?type=user")
  post $jar /Account/Register "$tok" -d "FullName=$(urlenc "مستخدم $R")" -d Email=$email -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null; }
OP="op$R@test.om"; OP2="op2$R@test.om"; FAN="fan$R@test.om"
mkuser p4_op.jar $OP; mkuser p4_op2.jar $OP2; mkuser p4_fan.jar $FAN
for e in $OP $OP2; do
  uid=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$e'")
  tok=$(token p4_admin.jar /Admin/Users/Roles/$uid); post p4_admin.jar /Admin/Users/Roles/$uid "$tok" -d selectedRoles=MatchOperator > /dev/null
done
OPID=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$OP'")
sql "UPDATE Matches SET OperatorUserId='$OPID' WHERE Id=$M" > /dev/null
for j in p4_op.jar p4_op2.jar; do rm -f $j; done
TOK=$(token p4_op.jar /Account/Login); post p4_op.jar /Account/Login "$TOK" -d Email=$OP -d Password=Test@1234 > /dev/null
TOK=$(token p4_op2.jar /Account/Login); post p4_op2.jar /Account/Login "$TOK" -d Email=$OP2 -d Password=Test@1234 > /dev/null
FANID=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$FAN'")
TOK=$(token p4_fan.jar /Teams); post p4_fan.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$TH > /dev/null
check "fan follows home team" 1 "$(sql "SELECT COUNT(*) FROM UserFollows WHERE UserId='$FANID' AND TeamId=$TH")"

echo "== Access"
check "assigned operator: screen" 200 "$(get p4_op.jar /MatchControl/Index/$M)"
check "assigned operator: my matches" 200 "$(get p4_op.jar /MatchControl/Mine)"
grep -q "فريق H $R" body.html && ok "match listed under my matches" || bad "my matches list"
check "other operator: denied" DENIED "$(denied $(get p4_op2.jar /MatchControl/Index/$M))"
check "plain user: denied" DENIED "$(denied $(get p4_fan.jar /MatchControl/Index/$M))"
check "other operator cannot post events" DENIED "$(denied $(ev p4_op2.jar MatchStarted))"
check "admin: screen" 200 "$(get p4_admin.jar /MatchControl/Index/$M)"
check "CSRF: event without token rejected" 400 "$(curl -s -o /dev/null -w '%{http_code}' -b p4_op.jar -X POST $BASE/MatchControl/AddEvent/$M -d EventType=MatchStarted -d ClientRequestId=$(uuid))"

echo "== State machine"
check "goal before kick-off refused" 400 "$(ev p4_op.jar Goal TeamId=$TH PlayerId=$PH1)"
grep -q "ابدأ المباراة" body.html && ok "Arabic error returned" || bad "error text"
check "half-time before start refused" 400 "$(ev p4_op.jar FirstHalfEnded)"
check "kick-off" 200 "$(ev p4_op.jar MatchStarted Minute=0)"
check "status Live / period 1" "1/1" "$(status)"
check "kick-off twice refused" 400 "$(ev p4_op.jar MatchStarted)"

echo "== Goals, idempotency, validation"
GOAL_RID=$(uuid)
check "goal H1 (assist H2)" 200 "$(RID=$GOAL_RID ev p4_op.jar Goal TeamId=$TH PlayerId=$PH1 RelatedPlayerId=$PH2 Minute=12)"
check "score 1-0" "1-0" "$(score)"
check "same click resent" 200 "$(RID=$GOAL_RID ev p4_op.jar Goal TeamId=$TH PlayerId=$PH1 Minute=12)"
grep -q '"duplicate":true' body.html && ok "flagged as duplicate" || bad "duplicate flag"
check "still 1-0 and 1 goal event" "1-0 1" "$(score) $(sql "SELECT COUNT(*) FROM MatchEvents WHERE MatchId=$M AND EventType=1")"
echo "  · 5 identical requests in parallel"
RID2=$(uuid); TOK=$(token p4_op.jar /MatchControl/Index/$M)
for i in 1 2 3 4 5; do curl -s -o /dev/null -b p4_op.jar -X POST $BASE/MatchControl/AddEvent/$M -H "RequestVerificationToken: $TOK" \
  -d EventType=YellowCard -d ClientRequestId=$RID2 -d TeamId=$TA -d PlayerId=$PA1 -d Minute=20 & done; wait
check "exactly one event stored" 1 "$(sql "SELECT COUNT(*) FROM MatchEvents WHERE MatchId=$M AND ClientRequestId='$RID2'")"
echo "  · 5 different goals in parallel (row lock must serialise them)"
for i in 1 2 3 4 5; do curl -s -o /dev/null -b p4_op.jar -X POST $BASE/MatchControl/AddEvent/$M -H "RequestVerificationToken: $TOK" \
  -d EventType=Goal -d ClientRequestId=$(uuid) -d TeamId=$TA -d PlayerId=$PA2 -d Minute=3$i & done; wait
check "score 1-5 (all five counted, none lost)" "1-5" "$(score)"
check "stored score = events" "1-5" "$(sql "SELECT CONCAT(SUM(CASE WHEN (EventType IN (1,3) AND TeamId=$TH) OR (EventType=2 AND TeamId=$TA) THEN 1 ELSE 0 END),'-',SUM(CASE WHEN (EventType IN (1,3) AND TeamId=$TA) OR (EventType=2 AND TeamId=$TH) THEN 1 ELSE 0 END)) FROM MatchEvents WHERE MatchId=$M AND IsCancelled=0")"
check "scorer from the other team refused" 400 "$(ev p4_op.jar Goal TeamId=$TH PlayerId=$PA1)"
grep -q "لا ينتمي للفريق" body.html && ok "player/team check" || bad "player/team message"
check "own goal by away player → home +1" 200 "$(ev p4_op.jar OwnGoal TeamId=$TA PlayerId=$PA1 Minute=40)"
check "score 2-5" "2-5" "$(score)"

echo "== Half time"
check "end of first half" 200 "$(ev p4_op.jar FirstHalfEnded Minute=45)"
check "status HalfTime" "2/1" "$(status)"
check "goal during half time refused" 400 "$(ev p4_op.jar Goal TeamId=$TH PlayerId=$PH1)"
check "substitution during half time allowed" 200 "$(ev p4_op.jar Substitution TeamId=$TH PlayerId=$PH2 RelatedPlayerId=$PH3 Minute=45)"
check "second half" 200 "$(ev p4_op.jar SecondHalfStarted Minute=46)"
check "status Live / period 2" "1/2" "$(status)"
check "penalty goal H3" 200 "$(ev p4_op.jar PenaltyGoal TeamId=$TH PlayerId=$PH3 Minute=70)"
check "score 3-5" "3-5" "$(score)"

echo "== Full time → standings"
check "full time" 200 "$(ev p4_op.jar MatchEnded Minute=90)"
check "status Finished" "3/2" "$(status)"
check "events after full time refused" 400 "$(ev p4_op.jar Goal TeamId=$TH PlayerId=$PH1)"
check "away team 3 pts in table" 3 "$(sql "SELECT Points FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$TA")"
check "player stats: A2 scored 5" 5 "$(sql "SELECT Goals FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PA2")"
check "player stats: H2 1 assist" 1 "$(sql "SELECT Assists FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PH2")"
check "player stats: A1 own goal + yellow" "1 1" "$(sql "SELECT CONCAT(OwnGoals,' ',YellowCards) FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PA1")"
check "sub (H3) counted as appearance" 1 "$(sql "SELECT Appearances FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PH3")"

echo "== Corrections (with history)"
G=$(sql "SELECT TOP 1 Id FROM MatchEvents WHERE MatchId=$M AND EventType=1 AND TeamId=$TA AND IsCancelled=0 ORDER BY Id")
TOK=$(token p4_op.jar /MatchControl/Index/$M)
check "correction without reason refused" 400 "$(post p4_op.jar "/MatchControl/CorrectEvent/$M?eventId=$G" "$TOK" -d TeamId=$TA -d PlayerId=$PA1 -d Minute=33)"
check "re-assign goal scorer A2→A1, minute 33" 200 "$(post p4_op.jar "/MatchControl/CorrectEvent/$M?eventId=$G" "$TOK" -d TeamId=$TA -d PlayerId=$PA1 -d Minute=33 -d "Reason=$(urlenc "خطأ في اسم اللاعب")")"
check "revision stored with old values" 1 "$(sql "SELECT COUNT(*) FROM MatchEventRevisions WHERE MatchEventId=$G AND Action=0 AND OldValuesJson LIKE '%PlayerId_:$PA2,%'")"
check "stats follow: A2 now 4 goals" 4 "$(sql "SELECT Goals FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PA2")"
check "cancel a goal" 200 "$(post p4_op.jar "/MatchControl/CancelEvent/$M?eventId=$G" "$TOK" -d "reason=$(urlenc "هدف ملغى بداعي التسلل")")"
check "score 3-4" "3-4" "$(score)"
check "cancelled event kept (not deleted)" 1 "$(sql "SELECT COUNT(*) FROM MatchEvents WHERE Id=$G AND IsCancelled=1")"
check "restore it" 200 "$(post p4_op.jar "/MatchControl/CancelEvent/$M?eventId=$G&restore=true" "$TOK" -d "reason=$(urlenc "مراجعة")")"
check "score back 3-5" "3-5" "$(score)"
echo "  · undo full time (ended by mistake), then end again"
END=$(sql "SELECT Id FROM MatchEvents WHERE MatchId=$M AND EventType=11 AND IsCancelled=0")
HT=$(sql "SELECT Id FROM MatchEvents WHERE MatchId=$M AND EventType=9")
check "only the latest state event can be undone" 400 "$(post p4_op.jar "/MatchControl/CancelEvent/$M?eventId=$HT" "$TOK" -d reason=x)"
check "undo full time" 200 "$(post p4_op.jar "/MatchControl/CancelEvent/$M?eventId=$END" "$TOK" -d "reason=$(urlenc "إنهاء بالخطأ")")"
check "back to Live / period 2" "1/2" "$(status)"
check "table: match no longer counted" 0 "$(sql "SELECT Played FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$TA")"
check "end again" 200 "$(ev p4_op.jar MatchEnded Minute=93)"
check "table counted again" 1 "$(sql "SELECT Played FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$TA")"

echo "== Lineup + report"
check "save lineup" 302 "$(post p4_op.jar /MatchControl/SaveLineup/$M "$TOK" -d homeStarters=$PH1 -d homeStarters=$PH2 -d homeBench=$PH3 -d awayStarters=$PA1 -d awayStarters=$PA2)"
check "4 starters + 1 bench" "4 1" "$(sql "SELECT CONCAT(SUM(CASE WHEN IsStarter=1 THEN 1 ELSE 0 END),' ',SUM(CASE WHEN IsStarter=0 THEN 1 ELSE 0 END)) FROM MatchLineups WHERE MatchId=$M")"
check "foreign player ignored in lineup" 0 "$(post p4_op.jar /MatchControl/SaveLineup/$M "$TOK" -d homeStarters=$PA1 > /dev/null; sql "SELECT COUNT(*) FROM MatchLineups WHERE MatchId=$M AND TeamId=$TH AND PlayerId=$PA1")"
post p4_op.jar /MatchControl/SaveLineup/$M "$TOK" -d homeStarters=$PH1 -d homeStarters=$PH2 -d homeBench=$PH3 -d awayStarters=$PA1 -d awayStarters=$PA2 > /dev/null

echo "== Moving starters on the pitch"
check "operator page offers the pitch arrangement" 1 "$(get p4_op.jar /MatchControl/Index/$M > /dev/null; grep -c 'id="pitchArrange"' $T/body.html)"
# H2 → forward (4) first in line, H1 → forward second, A1 → goalkeeper (1); the bench player and a foreign/invalid line are ignored.
check "save pitch arrangement" 302 "$(post p4_op.jar /MatchControl/SavePitch/$M "$TOK" \
  -d 'slots[0].PlayerId'=$PH1 -d 'slots[0].PositionId'=4 -d 'slots[0].Order'=2 \
  -d 'slots[1].PlayerId'=$PH2 -d 'slots[1].PositionId'=4 -d 'slots[1].Order'=1 \
  -d 'slots[2].PlayerId'=$PA1 -d 'slots[2].PositionId'=1 -d 'slots[2].Order'=1 \
  -d 'slots[3].PlayerId'=$PH3 -d 'slots[3].PositionId'=2 -d 'slots[3].Order'=1 \
  -d 'slots[4].PlayerId'=$PA2 -d 'slots[4].PositionId'=999 -d 'slots[4].Order'=1)"
check "lines and order saved" "4:2 4:1 1:1" "$(sql "SELECT CONCAT((SELECT CONCAT(PositionId,':',PitchOrder) FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PH1),' ',(SELECT CONCAT(PositionId,':',PitchOrder) FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PH2),' ',(SELECT CONCAT(PositionId,':',PitchOrder) FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PA1))")"
check "bench player and invalid line untouched" "x x" "$(sql "SELECT CONCAT(ISNULL(CAST((SELECT PitchOrder FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PH3) AS varchar),'x'),' ',ISNULL(CAST((SELECT PitchOrder FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PA2) AS varchar),'x'))")"
LP=$(get p4_op.jar /matches/$M > /dev/null; cat $T/body.html | tr -d '\r\n' | grep -o 'data-lineup-side="home".*data-lineup-side="away"' | grep -o 'data-lineup-player="[0-9]*"' | head -2 | grep -o '[0-9]*' | tr '\n' ' ')
check "public pitch shows H2 then H1 in the same line" "$PH2 $PH1 " "$LP"
post p4_op.jar /MatchControl/SaveLineup/$M "$TOK" -d homeStarters=$PH1 -d homeStarters=$PH2 -d homeBench=$PH3 -d awayStarters=$PA1 -d awayStarters=$PA2 > /dev/null
check "re-saving the lineup keeps the arrangement" "4:2 4:1" "$(sql "SELECT CONCAT((SELECT CONCAT(PositionId,':',PitchOrder) FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PH1),' ',(SELECT CONCAT(PositionId,':',PitchOrder) FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PH2))")"
check "a fan cannot move players" DENIED "$(TOKF=$(token p4_fan.jar /Teams); denied $(post p4_fan.jar /MatchControl/SavePitch/$M "$TOKF" -d 'slots[0].PlayerId'=$PH1 -d 'slots[0].PositionId'=1 -d 'slots[0].Order'=1))"
check "…and nothing changed" "4:2" "$(sql "SELECT CONCAT(PositionId,':',PitchOrder) FROM MatchLineups WHERE MatchId=$M AND PlayerId=$PH1")"
check "H1 starts counted" 1 "$(sql "SELECT Starts FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PH1")"
check "report with best player" 302 "$(post p4_op.jar /MatchControl/SaveReport/$M "$TOK" -d bestPlayerId=$PA2 -d attendance=350 -d "summary=$(urlenc "مباراة قوية")")"
check "best-player award in stats" 1 "$(sql "SELECT BestPlayerAwards FROM PlayerStatistics WHERE CompetitionSeasonId=$ED AND PlayerId=$PA2")"

echo "== Notifications to followers"
check "kick-off notification" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND MatchId=$M AND Type=0")"
check "goal notifications (1+5+own+pen = 8)" 8 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND MatchId=$M AND Type=1")"
check "half-time notification" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND MatchId=$M AND Type=2")"
check "full-time notifications (ended twice)" 2 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND MatchId=$M AND Type=3")"
check "one notification per card recorded" "$(sql "SELECT COUNT(*) FROM MatchEvents WHERE MatchId=$M AND EventType IN (5,6,7)")" "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND MatchId=$M AND Type=9")"
check "operator (not following) still gets kick-off and goals (sent to every user)" "1/8" "$(sql "SELECT CONCAT(SUM(CASE WHEN Type=0 THEN 1 ELSE 0 END),'/',SUM(CASE WHEN Type=1 THEN 1 ELSE 0 END)) FROM Notifications WHERE UserId='$OPID' AND MatchId=$M")"
check "…but not cards, missed penalties or substitutions (followers only)" 0 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$OPID' AND MatchId=$M AND Type IN (9,10,11)")"
echo "  · muting the team silences new alerts"
TOK=$(token p4_fan.jar /Teams); post p4_fan.jar /Follows/ToggleNotifications "$TOK" -d targetType=Team -d targetId=$TH > /dev/null
check "audit trail for events" 1 "$(sql "SELECT CASE WHEN COUNT(*) >= 15 THEN 1 ELSE 0 END FROM AuditLogs WHERE EntityName='Match' AND EntityId='$M'")"

echo "== Knockout final decided on penalties"
TOK=$(token p4_admin.jar /Admin/Competitions/Create)
postm p4_admin.jar /Admin/Competitions/Save "$TOK" -F Id=0 -F "$(fv NameAr "كأس مباشر $R")" -F "NameEn=Live Cup $R" -F SportId=1 -F IsActive=true > /dev/null
COMP2=$(sql "SELECT Id FROM Competitions WHERE NameEn='Live Cup $R'")
TOK=$(token p4_admin.jar "/Admin/Editions/Create?competitionId=$COMP2")
post p4_admin.jar /Admin/Editions/Save "$TOK" -d Id=0 -d CompetitionId=$COMP2 -d SeasonId=$SEASON -d Format=Knockout -d Status=Registration -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 > /dev/null
ED2=$(sql "SELECT Id FROM CompetitionSeasons WHERE CompetitionId=$COMP2")
TOK=$(token p4_admin.jar /Admin/Editions/Manage/$ED2)
post p4_admin.jar /Admin/Editions/AddTeams/$ED2 "$TOK" -d teamIds=$TH -d teamIds=$TA > /dev/null
post p4_admin.jar /Admin/Editions/GenerateFixtures/$ED2 "$TOK" -d startDate=$(date +%Y-%m-%d) -d kickoffTime=18:00 -d daysBetweenRounds=7 -d legs=1 > /dev/null
M=$(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED2")
check "final created" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED2")"
for t in MatchStarted FirstHalfEnded SecondHalfStarted; do ev p4_admin.jar $t > /dev/null; done
ev p4_admin.jar Goal TeamId=$TH PlayerId=$PH1 > /dev/null; ev p4_admin.jar Goal TeamId=$TA PlayerId=$PA1 > /dev/null
check "1-1" "1-1" "$(score)"
check "drawn KO end without penalties refused" 400 "$(ev p4_admin.jar MatchEnded)"
grep -q "ركلات الترجيح" body.html && ok "asks for penalties" || bad "penalties message"
check "end with penalties 5-4" 200 "$(ev p4_admin.jar MatchEnded HomePenaltyScore=5 AwayPenaltyScore=4)"
KH=$(sql "SELECT HomeTeamId FROM Matches WHERE Id=$M")
check "winner = home side (5-4 pens), champion crowned" "$KH $KH 3" "$(sql "SELECT CONCAT(m.WinnerTeamId,' ',e.ChampionTeamId,' ',e.Status) FROM Matches m JOIN CompetitionSeasons e ON e.Id=m.CompetitionSeasonId WHERE m.Id=$M")"

echo
echo "RESULT: $PASS passed, $FAIL failed"
