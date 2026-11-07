#!/usr/bin/env bash
# "Looking for a friendly" board: a team manager posts a request (day, home/away, number); everyone sees it, signed-in
# users see the number (call / WhatsApp); managers of other teams send offers; the requesting team accepts one, which
# creates the friendly on both teams' schedules and notifies everyone involved.
cd "$(dirname "$0")"
. ./lib.sh
rm -f fr_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
D1=$(date -d "+10 days" +%Y-%m-%d); D2=$(date -d "+12 days" +%Y-%m-%d); PAST=$(date -d "-1 days" +%Y-%m-%d)

echo "== Setup: teams A, B, C with managers; a fan"
for n in A B C; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'ودي $n $R','Fr $n $R','fr-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TA=$(sql "SELECT Id FROM Teams WHERE Slug='fr-A-$R'"); TB=$(sql "SELECT Id FROM Teams WHERE Slug='fr-B-$R'"); TC=$(sql "SELECT Id FROM Teams WHERE Slug='fr-C-$R'")
for n in a b c fan; do
  TOK=$(token fr_$n.jar "/Account/Register?type=user"); post fr_$n.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مستخدم $n")" -d Email=fr-$n-$R@test.om -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
done
TOK=$(token fr_adm.jar /Account/Login); post fr_adm.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
for p in "a $TA" "b $TB" "c $TC"; do set -- $p
  TOK=$(token fr_adm.jar /Admin/Teams/Edit/$2); post fr_adm.jar /Admin/Teams/AddMember/$2 "$TOK" -d email=fr-$1-$R@test.om -d role=Manager > /dev/null
  rm -f fr_$1.jar; TOK=$(token fr_$1.jar /Account/Login); post fr_$1.jar /Account/Login "$TOK" -d Email=fr-$1-$R@test.om -d Password=Test@1234 > /dev/null
done
UA=$(sql "SELECT Id FROM AspNetUsers WHERE Email='fr-a-$R@test.om'"); UB=$(sql "SELECT Id FROM AspNetUsers WHERE Email='fr-b-$R@test.om'"); UC=$(sql "SELECT Id FROM AspNetUsers WHERE Email='fr-c-$R@test.om'")

create() { # create JAR TEAM DATE VENUE [extra curl args] -> status
  local jar=$1 team=$2 day=$3 venue=$4; shift 4
  local tok=$(token $jar "/Friendlies/Create?teamId=$team")
  post $jar /Friendlies/Create "$tok" -d TeamId=$team -d PreferredDate=$day -d VenuePreference=$venue "$@"
}

echo "== Posting a request"
check "board is public" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/Friendlies)"
check "manager opens the form" 200 "$(get fr_a.jar "/Friendlies/Create?teamId=$TA")"
check "fan cannot post" DENIED "$(denied "$(create fr_fan.jar $TA $D1 Home -d ContactPhone=91234567)")"
check "bad number refused" 200 "$(create fr_a.jar $TA $D1 Home -d ContactPhone=123)"
check "…with a reason" 1 "$(grep -c 'اكتب رقماً صحيحاً' $T/body.html)"
check "past day refused" 200 "$(create fr_a.jar $TA $PAST Home -d ContactPhone=91234567)"
check "another team refused" 200 "$(create fr_a.jar $TB $D1 Home -d ContactPhone=91234567)"
check "nothing saved yet" 0 "$(sql "SELECT COUNT(*) FROM FriendlyRequests WHERE TeamId IN ($TA,$TB)")"
check "valid request" 302 "$(create fr_a.jar $TA $D1 Home -d "ContactPhone=$(urlenc "٩١٢٣ ٤٥٦٧")" -d "Notes=$(urlenc "ملعبنا مضاء")")"
Q=$(sql "SELECT Id FROM FriendlyRequests WHERE TeamId=$TA")
check "…opens its page" "/Friendlies/Details/$Q" "$(cat $T/location)"
check "Arabic digits normalised" 91234567 "$(sql "SELECT ContactPhone FROM FriendlyRequests WHERE Id=$Q")"

echo "== Seeing it"
check "listed on the board" 1 "$(curl -s "$BASE/Friendlies?all=true" | grep -c "/Friendlies/Details/$Q\"")"
check "guest: no number" 0 "$(curl -s "$BASE/Friendlies/Details/$Q" | grep -c 91234567)"
check "guest: asked to sign in" 1 "$(curl -s "$BASE/Friendlies/Details/$Q" | grep -c 'سجّل دخولك لرؤية الرقم')"
rm -f fr_fan.jar; TOK=$(token fr_fan.jar /Account/Login); post fr_fan.jar /Account/Login "$TOK" -d Email=fr-fan-$R@test.om -d Password=Test@1234 > /dev/null
get fr_fan.jar /Friendlies/Details/$Q > /dev/null
check "signed in: call link" 1 "$(grep -c 'href="tel:91234567"' $T/body.html)"
check "signed in: WhatsApp with +968" 1 "$(grep -c 'https://wa.me/96891234567' $T/body.html)"
check "fan: no offer form" 0 "$(grep -c 'name="teamId"' $T/body.html)"

echo "== Offers"
offer() { # offer JAR TEAM [args] -> status
  local jar=$1 team=$2; shift 2
  local tok=$(token $jar /Friendlies/Details/$Q)
  post $jar /Friendlies/Offer/$Q "$tok" -d teamId=$team "$@"
}
get fr_b.jar /Friendlies/Details/$Q > /dev/null
check "manager B sees the offer form for team B" 1 "$(grep -c "<option value=\"$TB\"" $T/body.html)"
check "B offers" 302 "$(offer fr_b.jar $TB -d contactPhone=99887766 -d "message=$(urlenc "جاهزين الساعة 5")")"
check "…stored pending" 1 "$(sql "SELECT COUNT(*) FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND TeamId=$TB AND Status=0")"
check "…manager A notified" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$UA' AND Type=13 AND Url='/Friendlies/Details/$Q'")"
offer fr_b.jar $TB -d contactPhone=99887766 > /dev/null
check "duplicate offer refused" 1 "$(sql "SELECT COUNT(*) FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND TeamId=$TB")"
offer fr_b.jar $TC -d contactPhone=99887766 > /dev/null
check "B cannot offer for team C" 0 "$(sql "SELECT COUNT(*) FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND TeamId=$TC")"
offer fr_a.jar $TA -d contactPhone=99887766 > /dev/null
check "A cannot offer on its own request" 0 "$(sql "SELECT COUNT(*) FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND TeamId=$TA")"
check "C offers" 302 "$(offer fr_c.jar $TC -d contactPhone=95554444)"
OB=$(sql "SELECT Id FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND TeamId=$TB"); OC=$(sql "SELECT Id FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND TeamId=$TC")
get fr_a.jar /Friendlies/Details/$Q > /dev/null
check "A sees both offers" 2 "$(grep -c 'data-offer=' $T/body.html)"
check "…with B's WhatsApp" 1 "$(grep -c 'https://wa.me/96899887766' $T/body.html)"
get fr_c.jar /Friendlies/Details/$Q > /dev/null
check "C does not see other offers" 0 "$(grep -c 'data-offer=' $T/body.html)"
TOK=$(token fr_b.jar /Friendlies/Details/$Q)
check "B cannot accept its own offer" DENIED "$(denied "$(post fr_b.jar "/Friendlies/Accept?offerId=$OB" "$TOK")")"

echo "== Accepting"
TOK=$(token fr_a.jar /Friendlies/Details/$Q)
check "A accepts B" 302 "$(post fr_a.jar "/Friendlies/Accept?offerId=$OB" "$TOK")"
check "…back on A's matches" "/MyTeams/Matches/$TA" "$(cat $T/location)"
M=$(sql "SELECT MatchId FROM FriendlyRequests WHERE Id=$Q")
check "request matched" 1 "$(sql "SELECT Status FROM FriendlyRequests WHERE Id=$Q")"
check "friendly created: A home, B away, A organises" "$TA/$TB/$TA/0" "$(sql "SELECT CONCAT(HomeTeamId,'/',AwayTeamId,'/',OrganizerTeamId,'/',Status) FROM Matches WHERE Id=$M")"
check "kick-off 17:00 Muscat by default" "$D1 13:00" "$(sql "SELECT FORMAT(KickoffAt,'yyyy-MM-dd HH:mm') FROM Matches WHERE Id=$M")"
check "offers: B accepted, C declined" "1/2" "$(sql "SELECT CONCAT((SELECT Status FROM FriendlyOffers WHERE Id=$OB),'/',(SELECT Status FROM FriendlyOffers WHERE Id=$OC))")"
check "B told it was accepted" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$UB' AND Type=14 AND Url='/matches/$M'")"
check "C told another team was chosen" 1 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$UC' AND Type=14")"
check "match on A's schedule" 1 "$(curl -s -b fr_a.jar $BASE/MyTeams/Matches/$TA | grep -c "/MatchControl/Index/$M\"")"
check "match public" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/matches/$M)"
TOK=$(token fr_a.jar /Friendlies/Details/$Q)
post fr_a.jar "/Friendlies/Accept?offerId=$OC" "$TOK" > /dev/null
check "second accept does nothing" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE (HomeTeamId=$TA OR AwayTeamId=$TA) AND IsDeleted=0")"
check "no longer on the board" 0 "$(curl -s "$BASE/Friendlies?all=true" | grep -c "/Friendlies/Details/$Q\"")"
check "late offer refused" 0 "$(offer fr_c.jar $TC -d contactPhone=95554444 > /dev/null; sql "SELECT COUNT(*) FROM FriendlyOffers WHERE FriendlyRequestId=$Q AND Status=0")"

echo "== Away request with a time"
check "A asks to play away at 19:30" 302 "$(create fr_a.jar $TA $D2 Away -d PreferredTime=19:30 -d ContactPhone=91234567)"
Q2=$(sql "SELECT MAX(Id) FROM FriendlyRequests WHERE TeamId=$TA")
Q=$Q2; offer fr_c.jar $TC -d contactPhone=95554444 > /dev/null
O2=$(sql "SELECT Id FROM FriendlyOffers WHERE FriendlyRequestId=$Q2")
TOK=$(token fr_a.jar /Friendlies/Details/$Q2); post fr_a.jar "/Friendlies/Accept?offerId=$O2" "$TOK" > /dev/null
M2=$(sql "SELECT MatchId FROM FriendlyRequests WHERE Id=$Q2")
check "C home, A away, 19:30 Muscat" "$TC/$TA/$D2 15:30" "$(sql "SELECT CONCAT(HomeTeamId,'/',AwayTeamId,'/',FORMAT(KickoffAt,'yyyy-MM-dd HH:mm')) FROM Matches WHERE Id=$M2")"

echo "== Limits and closing"
for i in 1 2 3; do create fr_b.jar $TB $D1 Either -d ContactPhone=99887766 > /dev/null; done
check "B has 3 open" 3 "$(sql "SELECT COUNT(*) FROM FriendlyRequests WHERE TeamId=$TB AND Status=0")"
check "4th refused" 200 "$(create fr_b.jar $TB $D1 Either -d ContactPhone=99887766)"
check "…with a reason" 1 "$(grep -c 'طلبات مفتوحة' $T/body.html)"
QB=$(sql "SELECT MIN(Id) FROM FriendlyRequests WHERE TeamId=$TB")
TOK=$(token fr_a.jar /Friendlies/Details/$QB)
check "A cannot close B's request" DENIED "$(denied "$(post fr_a.jar /Friendlies/Close/$QB "$TOK")")"
TOK=$(token fr_b.jar /Friendlies/Details/$QB)
check "B closes it" 302 "$(post fr_b.jar /Friendlies/Close/$QB "$TOK")"
check "…closed" 2 "$(sql "SELECT Status FROM FriendlyRequests WHERE Id=$QB")"
check "my requests page" 200 "$(get fr_b.jar /Friendlies/Mine)"

echo
echo "RESULT: $PASS passed, $FAIL failed"
