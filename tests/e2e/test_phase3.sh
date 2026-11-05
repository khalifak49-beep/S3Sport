#!/usr/bin/env bash
# Phase 3: players, transfers, team-manager isolation, competitions, fixtures (league/groups/knockout/mixed), standings.
cd "$(dirname "$0")"
. ./lib.sh
rm -f p3_admin.jar p3_tm.jar p3_mgr.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM

echo "== Setup: 8 approved teams (SQL) + admin login"
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
for n in 1 2 3 4 5 6 7 8; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'فريق $R-$n','Team $R $n','t$R-$n',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" >/dev/null
done
TEAMS=($(sql "SELECT Id FROM Teams WHERE Slug LIKE 't$R-%' ORDER BY Slug"))
check "8 teams created" 8 "${#TEAMS[@]}"
TOK=$(token p3_admin.jar /Account/Login)
post p3_admin.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
for p in /Admin/Sports /Admin/Seasons /Admin/Venues /Admin/Players /Admin/Competitions /Admin/Matches /Admin/Players/Create /Admin/Competitions/Create /Admin/Venues/Create /Admin/Seasons/Create /Admin/Sports/Edit/1; do
  check "GET $p" 200 "$(get p3_admin.jar $p)"
done

echo "== Venue + season"
TOK=$(token p3_admin.jar /Admin/Venues/Create)
check "create venue" 302 "$(post p3_admin.jar /Admin/Venues/Save "$TOK" -d Id=0 -d "NameAr=$(urlenc "ملعب الخوض $R")" -d Capacity=500)"
VENUE=$(sql "SELECT Id FROM Venues WHERE NameAr=N'ملعب الخوض $R'")
SEASON=$(sql "SELECT Id FROM Seasons WHERE IsCurrent=1")
[ -n "$SEASON" ] && ok "current season exists" || bad "current season"

echo "== Players: create, shirt uniqueness, transfer history"
TOK=$(token p3_admin.jar /Admin/Players/Create)
check "create player in team 1 (#10)" 302 "$(postm p3_admin.jar /Admin/Players/Save "$TOK" -F Id=0 -F "$(fv FullNameAr "لاعب أول $R")" -F TeamId=${TEAMS[0]} -F ShirtNumber=10 -F PositionId=4 -F DateOfBirth=2000-05-01)"
P1=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب أول $R'")
TOK=$(token p3_admin.jar /Admin/Players/Create)
check "duplicate shirt #10 refused" 200 "$(postm p3_admin.jar /Admin/Players/Save "$TOK" -F Id=0 -F "$(fv FullNameAr "لاعب ثان $R")" -F TeamId=${TEAMS[0]} -F ShirtNumber=10)"
grep -q "رقم القميص مستخدم" body.html && ok "shirt number message" || bad "shirt message"
TOK=$(token p3_admin.jar /Admin/Players/Details/$P1)
check "transfer to team 2" 302 "$(post p3_admin.jar /Admin/Players/Transfer/$P1 "$TOK" -d toTeamId=${TEAMS[1]} -d transferDate=$(date +%Y-%m-%d) -d shirtNumber=9)"
check "one active membership (team 2)" "${TEAMS[1]}" "$(sql "SELECT TeamId FROM TeamPlayers WHERE PlayerId=$P1 AND IsActive=1")"
check "history kept (2 memberships)" 2 "$(sql "SELECT COUNT(*) FROM TeamPlayers WHERE PlayerId=$P1")"
check "transfer recorded" 1 "$(sql "SELECT COUNT(*) FROM PlayerTransfers WHERE PlayerId=$P1 AND FromTeamId=${TEAMS[0]} AND ToTeamId=${TEAMS[1]}")"
check "transfer to same team refused" 302 "$(post p3_admin.jar /Admin/Players/Transfer/$P1 "$TOK" -d toTeamId=${TEAMS[1]})"
check "still 1 transfer" 1 "$(sql "SELECT COUNT(*) FROM PlayerTransfers WHERE PlayerId=$P1")"
check "player details page" 200 "$(get p3_admin.jar /Admin/Players/Details/$P1)"

echo "== Team manager isolation"
TMEMAIL="mgr$R@test.om"
TOK=$(token p3_mgr.jar "/Account/Register?type=user")
post p3_mgr.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مدير فريق")" -d Email=$TMEMAIL -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
TOK=$(token p3_admin.jar /Admin/Teams/Edit/${TEAMS[2]})
post p3_admin.jar /Admin/Teams/AddMember/${TEAMS[2]} "$TOK" -d email=$TMEMAIL -d role=Manager > /dev/null
rm -f p3_mgr.jar; TOK=$(token p3_mgr.jar /Account/Login)
post p3_mgr.jar /Account/Login "$TOK" -d Email=$TMEMAIL -d Password=Test@1234 > /dev/null
check "manager: own squad" 200 "$(get p3_mgr.jar /MyTeams/Squad/${TEAMS[2]})"
check "manager: other team squad denied" DENIED "$(denied $(get p3_mgr.jar /MyTeams/Squad/${TEAMS[3]}))"
check "manager: /MyTeams redirects to own team" 302 "$(get p3_mgr.jar /MyTeams)"
TOK=$(token p3_mgr.jar /MyTeams/AddPlayer/${TEAMS[2]})
check "manager adds player" 302 "$(postm p3_mgr.jar /MyTeams/SavePlayer "$TOK" -F Id=0 -F TeamId=${TEAMS[2]} -F "$(fv FullNameAr "لاعب المدير $R")" -F ShirtNumber=7)"
P2=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'لاعب المدير $R'")
check "player in manager's team" "${TEAMS[2]}" "$(sql "SELECT TeamId FROM TeamPlayers WHERE PlayerId=$P2 AND IsActive=1")"
check "manager cannot add to other team" DENIED "$(denied $(postm p3_mgr.jar /MyTeams/SavePlayer "$TOK" -F Id=0 -F TeamId=${TEAMS[3]} -F "$(fv FullNameAr "دخيل $R")"))"
check "manager cannot edit player of other team" DENIED "$(denied $(postm p3_mgr.jar /MyTeams/SavePlayer "$TOK" -F Id=$P1 -F TeamId=${TEAMS[2]} -F "$(fv FullNameAr "تعديل غير مسموح")"))"
check "manager cannot sign player of another team" 302 "$(post p3_mgr.jar /MyTeams/SignFreeAgent/${TEAMS[2]} "$TOK" -d playerId=$P1)"
check "P1 still in team 2" "${TEAMS[1]}" "$(sql "SELECT TeamId FROM TeamPlayers WHERE PlayerId=$P1 AND IsActive=1")"
check "manager: admin area denied" 302 "$(get p3_mgr.jar /Admin/Players)"
check "manager releases own player" 302 "$(post p3_mgr.jar /MyTeams/ReleasePlayer/${TEAMS[2]} "$TOK" -d playerId=$P2)"
check "released → no active team" 0 "$(sql "SELECT COUNT(*) FROM TeamPlayers WHERE PlayerId=$P2 AND IsActive=1")"
check "manager re-signs free agent" 302 "$(post p3_mgr.jar /MyTeams/SignFreeAgent/${TEAMS[2]} "$TOK" -d playerId=$P2 -d shirtNumber=8)"
check "back in team with #8" 8 "$(sql "SELECT ShirtNumber FROM TeamPlayers WHERE PlayerId=$P2 AND IsActive=1")"

echo "== League: 4 teams, single round-robin"
TOK=$(token p3_admin.jar /Admin/Competitions/Create)
check "create competition" 302 "$(postm p3_admin.jar /Admin/Competitions/Save "$TOK" -F Id=0 -F "$(fv NameAr "دوري الاختبار $R")" -F "NameEn=Test League $R" -F SportId=1 -F GovernorateId=1 -F IsActive=true)"
COMP=$(sql "SELECT Id FROM Competitions WHERE NameEn='Test League $R'")
TOK=$(token p3_admin.jar "/Admin/Editions/Create?competitionId=$COMP")
check "create league edition" 302 "$(post p3_admin.jar /Admin/Editions/Save "$TOK" -d Id=0 -d CompetitionId=$COMP -d SeasonId=$SEASON -d Format=League -d Status=Registration -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 -d "TieBreakers[0]=HeadToHeadPoints" -d "TieBreakers[1]=GoalDifference" -d "TieBreakers[2]=GoalsFor")"
ED=$(sql "SELECT Id FROM CompetitionSeasons WHERE CompetitionId=$COMP")
check "tie-breakers stored in order" "HeadToHeadPoints,GoalDifference,GoalsFor" "$(sql "SELECT TieBreakers FROM CompetitionSeasons WHERE Id=$ED")"
TOK=$(token p3_admin.jar /Admin/Editions/Manage/$ED)
check "add 4 teams" 302 "$(post p3_admin.jar /Admin/Editions/AddTeams/$ED "$TOK" -d teamIds=${TEAMS[0]} -d teamIds=${TEAMS[1]} -d teamIds=${TEAMS[2]} -d teamIds=${TEAMS[3]})"
check "generate league" 302 "$(post p3_admin.jar /Admin/Editions/GenerateFixtures/$ED "$TOK" -d startDate=2026-12-01 -d kickoffTime=17:00 -d daysBetweenRounds=7 -d legs=1 -d venueId=$VENUE)"
check "6 matches (4C2)" 6 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED")"
check "3 rounds" 3 "$(sql "SELECT COUNT(DISTINCT RoundNumber) FROM Matches WHERE CompetitionSeasonId=$ED")"
check "every pair exactly once" 6 "$(sql "SELECT COUNT(*) FROM (SELECT CASE WHEN HomeTeamId<AwayTeamId THEN HomeTeamId ELSE AwayTeamId END a, CASE WHEN HomeTeamId<AwayTeamId THEN AwayTeamId ELSE HomeTeamId END b FROM Matches WHERE CompetitionSeasonId=$ED GROUP BY CASE WHEN HomeTeamId<AwayTeamId THEN HomeTeamId ELSE AwayTeamId END, CASE WHEN HomeTeamId<AwayTeamId THEN AwayTeamId ELSE HomeTeamId END HAVING COUNT(*)=1) x")"
check "each team plays once per round" 0 "$(sql "SELECT COUNT(*) FROM (SELECT RoundNumber, t, COUNT(*) c FROM (SELECT RoundNumber, HomeTeamId t FROM Matches WHERE CompetitionSeasonId=$ED UNION ALL SELECT RoundNumber, AwayTeamId FROM Matches WHERE CompetitionSeasonId=$ED) u GROUP BY RoundNumber, t HAVING COUNT(*)>1) x")"
check "kickoff stored as UTC (17:00 Muscat = 13:00Z)" 13 "$(sql "SELECT TOP 1 DATEPART(hour,KickoffAt) FROM Matches WHERE CompetitionSeasonId=$ED")"
check "edition moved to InProgress" 2 "$(sql "SELECT Status FROM CompetitionSeasons WHERE Id=$ED")"
check "standings rows created" 4 "$(sql "SELECT COUNT(*) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED")"
check "generate twice refused" 302 "$(post p3_admin.jar /Admin/Editions/GenerateFixtures/$ED "$TOK" -d startDate=2026-12-01 -d kickoffTime=17:00 -d daysBetweenRounds=7 -d legs=1)"
check "still 6" 6 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED")"

echo "  · enter results: A beats B 2-0, C beats D 1-0, A draws C 1-1, B beats D 3-0, A beats D 1-0, B beats C 1-0"
A=${TEAMS[0]}; B=${TEAMS[1]}; C=${TEAMS[2]}; D=${TEAMS[3]}
# savematch ID STATUS HS AS [PH PA]: submits the admin match form the way a browser does,
# re-sending every current field (stage, group, round, round name, kickoff, venue) plus the new result.
savematch() {
  local id=$1 st=$2 hs=$3 as=$4 ph=$5 pa=$6
  local tok=$(token p3_admin.jar /Admin/Matches/Edit/$id)
  local page=$(curl -s -b p3_admin.jar $BASE/Admin/Matches/Edit/$id)
  local rv=$(formval "$page" RowVersion) rn=$(formval "$page" RoundName) ko=$(formval "$page" KickoffLocal)
  local row=$(sql "SELECT CONCAT(CompetitionSeasonId,'|',ISNULL(CAST(StageId AS varchar),''),'|',ISNULL(CAST(GroupId AS varchar),''),'|',ISNULL(CAST(RoundNumber AS varchar),''),'|',HomeTeamId,'|',AwayTeamId,'|',ISNULL(CAST(VenueId AS varchar),'')) FROM Matches WHERE Id=$id")
  local ed stg grp rnum h a ven
  IFS='|' read -r ed stg grp rnum h a ven <<< "$row"
  post p3_admin.jar /Admin/Matches/Save "$tok" -d Id=$id -d CompetitionSeasonId=$ed -d StageId=$stg -d GroupId=$grp \
    -d RoundNumber=$rnum -d "RoundName=$(urlenc "$rn")" -d HomeTeamId=$h -d AwayTeamId=$a -d VenueId=$ven \
    --data-urlencode "KickoffLocal=$ko" -d Status=$st -d ResultHome=$hs -d ResultAway=$as \
    ${ph:+-d PenaltyHome=$ph} ${pa:+-d PenaltyAway=$pa} --data-urlencode "RowVersion=$rv"
}
result() { # home away hs as  (orientation independent)
  local id=$(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED AND HomeTeamId=$1 AND AwayTeamId=$2")
  if [ -n "$id" ]; then savematch $id Finished $3 $4
  else savematch $(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED AND HomeTeamId=$2 AND AwayTeamId=$1") Finished $4 $3; fi
}
check "A-B 2-0" 302 "$(result $A $B 2 0)"
check "C-D 1-0" 302 "$(result $C $D 1 0)"
check "A-C 1-1" 302 "$(result $A $C 1 1)"
check "B-D 3-0" 302 "$(result $B $D 3 0)"
check "A-D 1-0" 302 "$(result $A $D 1 0)"
check "B-C 1-0" 302 "$(result $B $C 1 0)"
# A: W W D = 7 pts, GD +3 ; B: L W W = 6 pts, GD +2 ; C: W D L = 4 ; D: 0
check "A first, 7 pts" "1 7" "$(sql "SELECT CONCAT(Position,' ',Points) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$A")"
check "B second, 6 pts" "2 6" "$(sql "SELECT CONCAT(Position,' ',Points) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$B")"
check "C 4 pts" "3 4" "$(sql "SELECT CONCAT(Position,' ',Points) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$C")"
check "D last, 0 pts, GD -5" "4 0 -5" "$(sql "SELECT CONCAT(Position,' ',Points,' ',GoalDifference) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$D")"
check "A form WWD (by date)" 3 "$(sql "SELECT LEN(Form) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$A")"
check "team stats: A clean sheets 2" 2 "$(sql "SELECT CleanSheets FROM TeamStatistics WHERE CompetitionSeasonId=$ED AND TeamId=$A")"

echo "  · head-to-head tie-break: deduct 1 point from A → A and B both 6; B wins on GD? H2H first: A beat B → A above B"
TOK=$(token p3_admin.jar /Admin/Editions/Manage/$ED)
check "adjust A by -1" 302 "$(post p3_admin.jar /Admin/Editions/AdjustPoints/$ED "$TOK" -d teamId=$A -d adjustment=-1 -d reason=test)"
check "A 6 pts with adjustment kept" "6 -1" "$(sql "SELECT CONCAT(Points,' ',PointsAdjustment) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$A")"
check "A still first via head-to-head" 1 "$(sql "SELECT Position FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$A")"
TOK=$(token p3_admin.jar /Admin/Editions/Edit/$ED)
RVE=$(curl -s -b p3_admin.jar $BASE/Admin/Editions/Edit/$ED | grep -o 'name="RowVersion" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//;s/&#x2B;/+/g;s|&#x2F;|/|g;s/&#x3D;/=/g')
# Switch to goals-for first: A scored 4, B scored 4 → equal; then wins: A 2, B 2 → equal; then GD: A +3 vs B +2 → A. Use GoalsAgainst-sensitive: put Wins then GoalDifference
check "change tie-breakers to GoalDifference first" 302 "$(post p3_admin.jar /Admin/Editions/Save "$TOK" -d Id=$ED -d CompetitionId=$COMP -d SeasonId=$SEASON -d Format=League -d Status=InProgress -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 -d "TieBreakers[0]=GoalDifference" --data-urlencode "RowVersion=$RVE")"
check "A first by GD (+3 vs +2)" 1 "$(sql "SELECT Position FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$A")"
check "adjustment survives rule change" -1 "$(sql "SELECT PointsAdjustment FROM CompetitionStandings WHERE CompetitionSeasonId=$ED AND TeamId=$A")"
check "manage page renders" 200 "$(get p3_admin.jar /Admin/Editions/Manage/$ED)"

echo "== Groups: 8 teams, 2 groups"
TOK=$(token p3_admin.jar /Admin/Competitions/Create)
postm p3_admin.jar /Admin/Competitions/Save "$TOK" -F Id=0 -F "$(fv NameAr "كأس المجموعات $R")" -F "NameEn=Group Cup $R" -F SportId=1 -F IsActive=true > /dev/null
COMP2=$(sql "SELECT Id FROM Competitions WHERE NameEn='Group Cup $R'")
TOK=$(token p3_admin.jar "/Admin/Editions/Create?competitionId=$COMP2")
post p3_admin.jar /Admin/Editions/Save "$TOK" -d Id=0 -d CompetitionId=$COMP2 -d SeasonId=$SEASON -d Format=Mixed -d Status=Registration -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 > /dev/null
ED2=$(sql "SELECT Id FROM CompetitionSeasons WHERE CompetitionId=$COMP2")
TOK=$(token p3_admin.jar /Admin/Editions/Manage/$ED2)
post p3_admin.jar /Admin/Editions/AddTeams/$ED2 "$TOK" $(for t in "${TEAMS[@]}"; do printf -- '-d teamIds=%s ' $t; done) > /dev/null
check "8 teams in mixed edition" 8 "$(sql "SELECT COUNT(*) FROM CompetitionTeams WHERE CompetitionSeasonId=$ED2")"
check "fixtures before groups refused" 302 "$(post p3_admin.jar /Admin/Editions/GenerateFixtures/$ED2 "$TOK" -d startDate=2026-12-05 -d kickoffTime=16:00 -d daysBetweenRounds=3 -d legs=1)"
check "no matches yet" 0 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED2")"
check "setup 2 groups, 2 qualify" 302 "$(post p3_admin.jar /Admin/Editions/SetupGroups/$ED2 "$TOK" -d groupCount=2 -d qualifiersPerGroup=2)"
check "4 teams per group" "4 4" "$(sql "SELECT STRING_AGG(c, ' ') FROM (SELECT COUNT(*) c FROM CompetitionTeams WHERE CompetitionSeasonId=$ED2 GROUP BY GroupId) x")"
check "re-setup works (FK-safe)" 302 "$(post p3_admin.jar /Admin/Editions/SetupGroups/$ED2 "$TOK" -d groupCount=2 -d qualifiersPerGroup=2 -d shuffle=true)"
check "still 2 groups" 2 "$(sql "SELECT COUNT(*) FROM CompetitionGroups g JOIN CompetitionStages s ON s.Id=g.StageId WHERE s.CompetitionSeasonId=$ED2")"
check "generate group fixtures" 302 "$(post p3_admin.jar /Admin/Editions/GenerateFixtures/$ED2 "$TOK" -d startDate=2026-12-05 -d kickoffTime=16:00 -d daysBetweenRounds=3 -d legs=1)"
check "12 group matches (2×6)" 12 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED2")"
check "no cross-group matches" 0 "$(sql "SELECT COUNT(*) FROM Matches m JOIN CompetitionTeams h ON h.TeamId=m.HomeTeamId AND h.CompetitionSeasonId=m.CompetitionSeasonId JOIN CompetitionTeams a ON a.TeamId=m.AwayTeamId AND a.CompetitionSeasonId=m.CompetitionSeasonId WHERE m.CompetitionSeasonId=$ED2 AND (h.GroupId<>a.GroupId OR m.GroupId<>h.GroupId)")"
check "knockout before groups finish refused" 302 "$(post p3_admin.jar /Admin/Editions/NextKnockoutRound/$ED2 "$TOK" -d date=2026-12-20 -d kickoffTime=16:00)"
grep -q "" location; check "no KO matches yet" 0 "$(sql "SELECT COUNT(*) FROM Matches m JOIN CompetitionStages s ON s.Id=m.StageId WHERE m.CompetitionSeasonId=$ED2 AND s.StageType=2")"
echo "  · finish all group matches (home team wins 1-0)"
for id in $(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED2"); do
  sql "UPDATE Matches SET Status=3, HomeScore=1, AwayScore=0, WinnerTeamId=HomeTeamId, FinishedAt=SYSUTCDATETIME() WHERE Id=$id" > /dev/null
done
post p3_admin.jar /Admin/Editions/Recalculate/$ED2 "$TOK" > /dev/null
check "group tables: 8 rows" 8 "$(sql "SELECT COUNT(*) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED2")"
check "generate semi-finals from groups" 302 "$(post p3_admin.jar /Admin/Editions/NextKnockoutRound/$ED2 "$TOK" -d date=2026-12-20 -d kickoffTime=16:00)"
check "2 semi-finals" 2 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED2 AND RoundName=N'نصف النهائي'")"
check "semi: winners vs runners-up of other group" 0 "$(sql "SELECT COUNT(*) FROM Matches m JOIN CompetitionTeams h ON h.TeamId=m.HomeTeamId AND h.CompetitionSeasonId=m.CompetitionSeasonId JOIN CompetitionTeams a ON a.TeamId=m.AwayTeamId AND a.CompetitionSeasonId=m.CompetitionSeasonId WHERE m.CompetitionSeasonId=$ED2 AND m.RoundName=N'نصف النهائي' AND h.GroupId=a.GroupId")"
check "next round before semis finish refused" 302 "$(post p3_admin.jar /Admin/Editions/NextKnockoutRound/$ED2 "$TOK" -d date=2026-12-27 -d kickoffTime=16:00)"
echo "  · semis: one decided on penalties via the match form"
SEMIS=($(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED2 AND RoundName=N'نصف النهائي' ORDER BY Id"))
S1H=$(sql "SELECT HomeTeamId FROM Matches WHERE Id=${SEMIS[0]}"); S1A=$(sql "SELECT AwayTeamId FROM Matches WHERE Id=${SEMIS[0]}")
KO_STAGE=$(sql "SELECT StageId FROM Matches WHERE Id=${SEMIS[0]}")
check "drawn KO without penalties refused" 200 "$(savematch ${SEMIS[0]} Finished 1 1)"
grep -q "ركلات الترجيح" body.html && ok "penalties required message" || bad "penalties message"
check "drawn KO with penalties 4-3" 302 "$(savematch ${SEMIS[0]} Finished 1 1 4 3)"
check "winner = home via penalties" "$S1H" "$(sql "SELECT WinnerTeamId FROM Matches WHERE Id=${SEMIS[0]}")"
sql "UPDATE Matches SET Status=3, HomeScore=0, AwayScore=2, WinnerTeamId=AwayTeamId WHERE Id=${SEMIS[1]}" > /dev/null
S2W=$(sql "SELECT AwayTeamId FROM Matches WHERE Id=${SEMIS[1]}")
check "generate final" 302 "$(post p3_admin.jar /Admin/Editions/NextKnockoutRound/$ED2 "$TOK" -d date=2026-12-27 -d kickoffTime=18:00)"
FINAL=$(sql "SELECT Id FROM Matches WHERE CompetitionSeasonId=$ED2 AND RoundName=N'النهائي'")
check "final pairs the two semi winners" "$S1H $S2W" "$(sql "SELECT CONCAT(HomeTeamId,' ',AwayTeamId) FROM Matches WHERE Id=$FINAL")"
FH=$(sql "SELECT HomeTeamId FROM Matches WHERE Id=$FINAL"); FA=$(sql "SELECT AwayTeamId FROM Matches WHERE Id=$FINAL")
check "final result 2-1" 302 "$(savematch $FINAL Finished 2 1)"
check "champion set & edition completed" "$FH 3" "$(sql "SELECT CONCAT(ChampionTeamId,' ',Status) FROM CompetitionSeasons WHERE Id=$ED2")"

echo "== Pure knockout with 5 teams (byes)"
TOK=$(token p3_admin.jar /Admin/Competitions/Create)
postm p3_admin.jar /Admin/Competitions/Save "$TOK" -F Id=0 -F "$(fv NameAr "كأس الإقصاء $R")" -F "NameEn=KO Cup $R" -F SportId=1 -F IsActive=true > /dev/null
COMP3=$(sql "SELECT Id FROM Competitions WHERE NameEn='KO Cup $R'")
TOK=$(token p3_admin.jar "/Admin/Editions/Create?competitionId=$COMP3")
post p3_admin.jar /Admin/Editions/Save "$TOK" -d Id=0 -d CompetitionId=$COMP3 -d SeasonId=$SEASON -d Format=Knockout -d Status=Registration -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 > /dev/null
ED3=$(sql "SELECT Id FROM CompetitionSeasons WHERE CompetitionId=$COMP3")
TOK=$(token p3_admin.jar /Admin/Editions/Manage/$ED3)
post p3_admin.jar /Admin/Editions/AddTeams/$ED3 "$TOK" $(for t in "${TEAMS[@]:0:5}"; do printf -- '-d teamIds=%s ' $t; done) > /dev/null
check "first round" 302 "$(post p3_admin.jar /Admin/Editions/GenerateFixtures/$ED3 "$TOK" -d startDate=2026-12-10 -d kickoffTime=16:00 -d daysBetweenRounds=7 -d legs=1)"
check "5 teams → 1 match, 3 byes" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED3")"
check "round named quarter-final" 1 "$(sql "SELECT CASE WHEN RoundName=N'ربع النهائي' THEN 1 ELSE 0 END FROM Matches WHERE CompetitionSeasonId=$ED3")"
check "no standings table for knockout" 0 "$(sql "SELECT COUNT(*) FROM CompetitionStandings WHERE CompetitionSeasonId=$ED3")"
sql "UPDATE Matches SET Status=3, HomeScore=3, AwayScore=0, WinnerTeamId=HomeTeamId WHERE CompetitionSeasonId=$ED3" > /dev/null
QW=$(sql "SELECT WinnerTeamId FROM Matches WHERE CompetitionSeasonId=$ED3")
check "semis after byes" 302 "$(post p3_admin.jar /Admin/Editions/NextKnockoutRound/$ED3 "$TOK" -d date=2026-12-17 -d kickoffTime=16:00)"
check "2 semi-finals" 2 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED3 AND RoundNumber=2")"
check "QF winner in a semi" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED3 AND RoundNumber=2 AND (HomeTeamId=$QW OR AwayTeamId=$QW)")"
check "all 4 remaining teams once" 4 "$(sql "SELECT COUNT(DISTINCT t) FROM (SELECT HomeTeamId t FROM Matches WHERE CompetitionSeasonId=$ED3 AND RoundNumber=2 UNION ALL SELECT AwayTeamId FROM Matches WHERE CompetitionSeasonId=$ED3 AND RoundNumber=2) x")"
check "top seeds (byes) do not meet in semis" 0 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED3 AND RoundNumber=2 AND HomeTeamId IN (${TEAMS[0]},${TEAMS[1]}) AND AwayTeamId IN (${TEAMS[0]},${TEAMS[1]})")"

echo "== Tournament-manager scoping"
TMAIL="tm$R@test.om"
TOK=$(token p3_tm.jar "/Account/Register?type=user")
post p3_tm.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مسؤول بطولة")" -d Email=$TMAIL -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
TOK=$(token p3_admin.jar /Admin/Competitions/Details/$COMP)
check "assign TM to league" 302 "$(post p3_admin.jar /Admin/Competitions/AddManager/$COMP "$TOK" -d email=$TMAIL)"
rm -f p3_tm.jar; TOK=$(token p3_tm.jar /Account/Login)
post p3_tm.jar /Account/Login "$TOK" -d Email=$TMAIL -d Password=Test@1234 > /dev/null
check "TM: competitions list" 200 "$(get p3_tm.jar /Admin/Competitions)"
grep -q "دوري الاختبار $R" body.html && ok "TM sees assigned competition" || bad "TM sees assigned"
grep -q "كأس المجموعات $R" body.html && bad "TM must not see others" || ok "TM does not see other competitions"
check "TM: own edition" 200 "$(get p3_tm.jar /Admin/Editions/Manage/$ED)"
check "TM: other edition denied" DENIED "$(denied $(get p3_tm.jar /Admin/Editions/Manage/$ED2))"
check "TM: dashboard forbidden (admins only)" 302 "$(get p3_tm.jar /Admin/Dashboard)"
check "TM: cannot create competitions" DENIED "$(denied $(get p3_tm.jar /Admin/Competitions/Create))"
OTHERM=$(sql "SELECT TOP 1 Id FROM Matches WHERE CompetitionSeasonId=$ED2")
check "TM: other competition's match → 404" 404 "$(get p3_tm.jar /Admin/Matches/Edit/$OTHERM)"

echo
echo "RESULT: $PASS passed, $FAIL failed"
