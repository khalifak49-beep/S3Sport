#!/usr/bin/env bash
# Phase 5: public pages, follows for players/competitions/matches, news + notifications, PWA assets.
cd "$(dirname "$0")"
. ./lib.sh
rm -f p5_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
uuid() { powershell -NoProfile -Command "[guid]::NewGuid().ToString()" | tr -d '\r'; }

echo "== Setup: league with 2 teams, players, one finished match, one scheduled"
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
for n in H A S; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'نادي $n $R','Club $n $R','p5-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TH=$(sql "SELECT Id FROM Teams WHERE Slug='p5-H-$R'"); TA=$(sql "SELECT Id FROM Teams WHERE Slug='p5-A-$R'"); TS=$(sql "SELECT Id FROM Teams WHERE Slug='p5-S-$R'")
sql "INSERT INTO Players(SportId,FullNameAr,IsDeleted,CreatedAt) VALUES(1,N'هداف $R',0,SYSUTCDATETIME()); INSERT INTO TeamPlayers(TeamId,PlayerId,ShirtNumber,JoinedOn,IsActive,IsCaptain,CreatedAt) VALUES($TH,SCOPE_IDENTITY(),9,'2026-01-01',1,0,SYSUTCDATETIME());" > /dev/null
PL=$(sql "SELECT Id FROM Players WHERE FullNameAr=N'هداف $R'")
TOK=$(token p5_admin.jar /Account/Login); post p5_admin.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
SEASON=$(sql "SELECT Id FROM Seasons WHERE IsCurrent=1")
TOK=$(token p5_admin.jar /Admin/Competitions/Create)
postm p5_admin.jar /Admin/Competitions/Save "$TOK" -F Id=0 -F "$(fv NameAr "دوري العرض $R")" -F "NameEn=Showcase League $R" -F SportId=1 -F GovernorateId=1 -F IsActive=true > /dev/null
COMP=$(sql "SELECT Id FROM Competitions WHERE NameEn='Showcase League $R'"); CSLUG=$(sql "SELECT Slug FROM Competitions WHERE Id=$COMP")
TOK=$(token p5_admin.jar "/Admin/Editions/Create?competitionId=$COMP")
post p5_admin.jar /Admin/Editions/Save "$TOK" -d Id=0 -d CompetitionId=$COMP -d SeasonId=$SEASON -d Format=League -d Status=Registration -d PointsForWin=3 -d PointsForDraw=1 -d PointsForLoss=0 > /dev/null
ED=$(sql "SELECT Id FROM CompetitionSeasons WHERE CompetitionId=$COMP")
TOK=$(token p5_admin.jar /Admin/Editions/Manage/$ED)
post p5_admin.jar /Admin/Editions/AddTeams/$ED "$TOK" -d teamIds=$TH -d teamIds=$TA -d teamIds=$TS > /dev/null
TODAY=$(powershell -NoProfile -Command "(Get-Date).ToUniversalTime().AddHours(4).ToString('yyyy-MM-dd')" | tr -d '\r')
post p5_admin.jar /Admin/Editions/GenerateFixtures/$ED "$TOK" -d startDate=$TODAY -d kickoffTime=23:30 -d daysBetweenRounds=1 -d legs=1 > /dev/null
check "3 fixtures" 3 "$(sql "SELECT COUNT(*) FROM Matches WHERE CompetitionSeasonId=$ED")"
M1=$(sql "SELECT TOP 1 Id FROM Matches WHERE CompetitionSeasonId=$ED AND RoundNumber=1")
sql "UPDATE Matches SET HomeTeamId=$TH, AwayTeamId=$TA WHERE Id=$M1" > /dev/null
for t in MatchStarted; do RID=$(uuid); TOK=$(token p5_admin.jar /Teams); post p5_admin.jar /MatchControl/AddEvent/$M1 "$TOK" -d EventType=$t -d ClientRequestId=$RID > /dev/null; done
RID=$(uuid); post p5_admin.jar /MatchControl/AddEvent/$M1 "$TOK" -d EventType=Goal -d ClientRequestId=$RID -d TeamId=$TH -d PlayerId=$PL -d Minute=10 > /dev/null
check "match live 1-0" "1 1-0" "$(sql "SELECT CONCAT(Status,' ',HomeScore,'-',AwayScore) FROM Matches WHERE Id=$M1")"

echo "== Public pages"
for p in "/" "/Matches" "/Matches?live=true" "/Matches?all=true" "/matches/$M1" "/Competitions" "/Competitions?all=true" "/competitions/$CSLUG" "/Standings" "/Standings?all=true" "/Players?all=true" "/players/$PL" "/News" "/teams/p5-H-$R"; do
  check "GET $p" 200 "$(get p5_guest.jar "$p")"
done
get p5_guest.jar "/Matches?live=true&all=true" > /dev/null
grep -q "data-live-match=\"$M1\"" body.html && ok "live tab lists the live match" || bad "live tab"
get p5_guest.jar "/matches/$M1" > /dev/null
grep -q "\"playerId\":$PL" body.html && ok "match page embeds the scorer in its state" || bad "match state scorer"
check "state JSON" 200 "$(get p5_guest.jar /matches/$M1/state)"
grep -q '"homeScore":1' body.html && ok "state JSON has score" || bad "state score"
get p5_guest.jar "/competitions/$CSLUG" > /dev/null
grep -q "هداف $R" body.html && bad "stats must count finished matches only" || ok "live match not yet in competition stats (finished only)"
grep -q "standings-table" body.html && ok "competition table rendered" || bad "competition table"
get p5_guest.jar "/" > /dev/null
grep -q "مباشر الآن" body.html && ok "home shows live section" || bad "home live section"
get p5_guest.jar "/teams/p5-H-$R" > /dev/null
grep -q "المباريات القادمة" body.html && grep -q "دوري العرض $R" body.html && ok "team page: matches + competitions" || bad "team page sections"
check "unknown match → 404" 404 "$(get p5_guest.jar /matches/999999)"
check "unknown competition → 404" 404 "$(get p5_guest.jar /competitions/no-such-$R)"
check "unknown player → 404" 404 "$(get p5_guest.jar /players/999999)"

echo "== Suspended team hides its matches"
M_S=$(sql "SELECT TOP 1 Id FROM Matches WHERE CompetitionSeasonId=$ED AND (HomeTeamId=$TS OR AwayTeamId=$TS)")
sql "UPDATE Teams SET Status=3 WHERE Id=$TS" > /dev/null
check "suspended team's match → 404" 404 "$(get p5_guest.jar /matches/$M_S)"
sql "UPDATE Teams SET Status=1 WHERE Id=$TS" > /dev/null

echo "== Follow player / competition / match"
EMAIL="p5fan$R@test.om"
TOK=$(token p5_fan.jar /Account/Register)
post p5_fan.jar /Account/Register "$TOK" -d "FullName=$(urlenc مشجع)" -d Email=$EMAIL -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
FAN=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$EMAIL'")
TOK=$(token p5_fan.jar /Teams)
check "follow player" 302 "$(post p5_fan.jar /Follows/Follow "$TOK" -d targetType=Player -d targetId=$PL)"
check "follow competition" 302 "$(post p5_fan.jar /Follows/Follow "$TOK" -d targetType=Competition -d targetId=$COMP)"
check "follow match" 302 "$(post p5_fan.jar /Follows/Follow "$TOK" -d targetType=Match -d targetId=$M1)"
check "3 follows stored" 3 "$(sql "SELECT COUNT(*) FROM UserFollows WHERE UserId='$FAN'")"
get p5_fan.jar /Favorites > /dev/null
grep -q "هداف $R" body.html && grep -q "دوري العرض $R" body.html && grep -q "data-live-match=\"$M1\"" body.html && ok "favorites lists player, competition, match" || bad "favorites sections"
echo "  · match follow → goal notification; muting the match silences it"
RID=$(uuid); TOK=$(token p5_admin.jar /Teams); post p5_admin.jar /MatchControl/AddEvent/$M1 "$TOK" -d EventType=Goal -d ClientRequestId=$RID -d TeamId=$TH -d PlayerId=$PL -d Minute=20 > /dev/null
check "goal notified to match follower" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FAN' AND MatchId=$M1 AND Type=1")"
TOK=$(token p5_fan.jar /Teams); post p5_fan.jar /Follows/ToggleNotifications "$TOK" -d targetType=Match -d targetId=$M1 > /dev/null
RID=$(uuid); TOK=$(token p5_admin.jar /Teams); post p5_admin.jar /MatchControl/AddEvent/$M1 "$TOK" -d EventType=Goal -d ClientRequestId=$RID -d TeamId=$TH -d PlayerId=$PL -d Minute=25 > /dev/null
check "muted match → no new notification" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FAN' AND MatchId=$M1 AND Type=1")"
check "follow non-existent player → 404" 404 "$(TOK=$(token p5_fan.jar /Teams); post p5_fan.jar /Follows/Follow "$TOK" -d targetType=Player -d targetId=999999)"

echo "== News: permissions, YouTube validation, publish → followers notified"
CMAIL="cm$R@test.om"
TOK=$(token p5_cm.jar /Account/Register)
post p5_cm.jar /Account/Register "$TOK" -d "FullName=$(urlenc محرر)" -d Email=$CMAIL -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
check "plain user: admin news denied" DENIED "$(denied $(get p5_cm.jar /Admin/News))"
CMID=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$CMAIL'")
TOK=$(token p5_admin.jar /Admin/Users/Roles/$CMID); post p5_admin.jar /Admin/Users/Roles/$CMID "$TOK" -d selectedRoles=ContentManager > /dev/null
rm -f p5_cm.jar; TOK=$(token p5_cm.jar /Account/Login); post p5_cm.jar /Account/Login "$TOK" -d Email=$CMAIL -d Password=Test@1234 > /dev/null
check "content manager: news admin" 200 "$(get p5_cm.jar /Admin/News)"
check "content manager: other admin pages denied" DENIED "$(denied $(get p5_cm.jar /Admin/Teams))"
TOK=$(token p5_fan.jar /Teams); post p5_fan.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$TA > /dev/null
TOK=$(token p5_cm.jar /Admin/News/Create)
check "non-YouTube video refused" 200 "$(postm p5_cm.jar /Admin/News/Save "$TOK" -F Id=0 -F "$(fv TitleAr "خبر تجريبي $R")" -F "$(fv Body "نص")" -F Category=TeamNews -F VideoUrl=https://evil.example/x.mp4 -F TeamId=$TA -F IsPublished=true)"
grep -q "يُقبل رابط يوتيوب فقط" body.html && ok "video validation message" || bad "video validation"
check "publish news linked to team A" 302 "$(postm p5_cm.jar /Admin/News/Save "$TOK" -F Id=0 -F "$(fv TitleAr "خبر تجريبي $R")" -F "$(fv Summary "ملخص الخبر")" -F "$(fv Body "السطر الأول
السطر الثاني")" -F Category=TeamNews -F VideoUrl=https://youtu.be/dQw4w9WgXcQ -F TeamId=$TA -F IsPublished=true)"
NEWS=$(sql "SELECT Id FROM News WHERE TitleAr=N'خبر تجريبي $R'"); NSLUG=$(sql "SELECT Slug FROM News WHERE Id=$NEWS")
check "team follower notified of news" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FAN' AND NewsId=$NEWS AND Type=5")"
check "news page" 200 "$(get p5_guest.jar /news/$NSLUG)"
grep -q "youtube-nocookie.com/embed/dQw4w9WgXcQ" body.html && ok "video embedded via nocookie domain" || bad "video embed"
check "view counted" 1 "$(sql "SELECT ViewCount FROM News WHERE Id=$NEWS")"
RV=$(formval "$(curl -s -b p5_cm.jar $BASE/Admin/News/Edit/$NEWS)" RowVersion)
TOK=$(token p5_cm.jar /Admin/News/Edit/$NEWS)
postm p5_cm.jar /Admin/News/Save "$TOK" -F Id=$NEWS -F "RowVersion=$RV" -F "$(fv TitleAr "خبر تجريبي معدل $R")" -F "$(fv Body "نص")" -F Category=TeamNews -F TeamId=$TA -F IsPublished=true > /dev/null
check "re-saving published news does not notify again" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FAN' AND NewsId=$NEWS")"
check "draft not public" 404 "$(sql "UPDATE News SET IsPublished=0 WHERE Id=$NEWS" > /dev/null; get p5_guest.jar /news/$NSLUG)"
sql "UPDATE News SET IsPublished=1 WHERE Id=$NEWS" > /dev/null
get p5_guest.jar /News > /dev/null
grep -q "خبر تجريبي معدل $R" body.html && ok "news listed" || bad "news list"

echo "== Full time → competition statistics"
for t in FirstHalfEnded SecondHalfStarted MatchEnded; do RID=$(uuid); TOK=$(token p5_admin.jar /Teams); post p5_admin.jar /MatchControl/AddEvent/$M1 "$TOK" -d EventType=$t -d ClientRequestId=$RID > /dev/null; done
check "match finished 3-0" "3 3-0" "$(sql "SELECT CONCAT(Status,' ',HomeScore,'-',AwayScore) FROM Matches WHERE Id=$M1")"
get p5_guest.jar "/competitions/$CSLUG" > /dev/null
grep -q "هداف $R" body.html && ok "top scorer listed after full time" || bad "top scorer listed"
check "scorer has 3 goals" 3 "$(sql "SELECT Goals FROM PlayerStatistics WHERE PlayerId=$PL AND CompetitionSeasonId=$ED")"

echo "== PWA assets"
curl -s -D h.txt -o /dev/null $BASE/manifest.webmanifest; grep -qi "content-type: application/manifest+json" h.txt && ok "manifest content-type" || bad "manifest type: $(grep -i content-type h.txt)"
check "service worker" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/sw.js)"
check "offline page" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/offline.html)"
check "icon 512" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/icons/icon-512.png)"
get p5_guest.jar / > /dev/null
grep -q 'rel="manifest"' body.html && ok "layout links manifest" || bad "manifest link"

echo
echo "RESULT: $PASS passed, $FAIL failed"
echo "M1=$M1 ED=$ED TH=$TH PL=$PL" > p5_ids.txt
