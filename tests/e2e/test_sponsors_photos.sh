#!/usr/bin/env bash
# Sponsors (admin: placement by country / governorate / wilayat / competition, dates, pause; public strip, view and
# click counts) and match photos (only a manager of one of the two teams uploads; coaches, fans and other teams cannot;
# the uploading team and admins delete; per-team limit; file checks).
cd "$(dirname "$0")"
. ./lib.sh
rm -f sp_*.jar
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
PHONE_UA='Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36'
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
WIL=$(sql "SELECT WilayatId FROM Areas WHERE Id=$KHOUDH"); GOV=$(sql "SELECT GovernorateId FROM Wilayats WHERE Id=$WIL")
OTHERGOV=$(sql "SELECT TOP 1 Id FROM Governorates WHERE Id<>$GOV ORDER BY Id")
YESTERDAY=$(date -d "-1 days" +%Y-%m-%d); TOMORROW=$(date -d "+1 days" +%Y-%m-%d)
printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=' | base64 -d > sp_img.png
printf 'not an image at all' > sp_fake.png

echo "== Setup: teams A, B, C; managers a, b, c; a coach of A; a fan; a friendly A v B"
for n in A B C; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'رعاة $n $R','Sp $n $R','sp-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TA=$(sql "SELECT Id FROM Teams WHERE Slug='sp-A-$R'"); TB=$(sql "SELECT Id FROM Teams WHERE Slug='sp-B-$R'"); TC=$(sql "SELECT Id FROM Teams WHERE Slug='sp-C-$R'")
for n in a b c coach fan; do
  TOK=$(token sp_$n.jar "/Account/Register?type=user"); post sp_$n.jar /Account/Register "$TOK" -d "FullName=$(urlenc "مستخدم $n")" -d Email=sp-$n-$R@test.om -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null
done
TOK=$(token sp_adm.jar /Account/Login); post sp_adm.jar /Account/Login "$TOK" -d Email=admin@s3sport.om --data-urlencode "Password=$ADMIN_PW" > /dev/null
for p in "a $TA Manager" "b $TB Manager" "c $TC Manager" "coach $TA Coach"; do set -- $p
  TOK=$(token sp_adm.jar /Admin/Teams/Edit/$2); post sp_adm.jar /Admin/Teams/AddMember/$2 "$TOK" -d email=sp-$1-$R@test.om -d role=$3 > /dev/null
done
for n in a b c coach fan; do rm -f sp_$n.jar; TOK=$(token sp_$n.jar /Account/Login); post sp_$n.jar /Account/Login "$TOK" -d Email=sp-$n-$R@test.om -d Password=Test@1234 > /dev/null; done
TOK=$(token sp_a.jar /MyTeams/CreateMatch/$TA)
post sp_a.jar /MyTeams/SaveMatch "$TOK" -d Id=0 -d TeamId=$TA -d OpponentTeamId=$TB -d IsHome=true -d KickoffLocal=$(date -d "+2 days" +%Y-%m-%d)T17:00 > /dev/null
M=$(sql "SELECT MAX(Id) FROM Matches WHERE OrganizerTeamId=$TA")
check "friendly created" 1 "$(sql "SELECT COUNT(*) FROM Matches WHERE Id=$M AND HomeTeamId=$TA AND AwayTeamId=$TB")"

echo "== Match photos: who may upload"
upload() { # upload JAR TEAM FILE [caption] -> status (body in $T/body.html)
  local tok=$(token $1 /matches/$M)
  [ -z "$tok" ] && tok=$(token sp_fan.jar /matches/$M)
  printf "%s" "${4:-}" > sp_caption.txt
  postm $1 /matches/$M/photos "$tok" -F teamId=$2 -F "photo=@$3;type=image/png" -F "caption=<sp_caption.txt"
}
check "guest cannot upload" 302 "$(curl -s -o /dev/null -w '%{http_code}' -X POST $BASE/matches/$M/photos)"
check "fan refused" 403 "$(upload sp_fan.jar $TA sp_img.png)"
check "coach of A refused (managers only)" 403 "$(upload sp_coach.jar $TA sp_img.png)"
check "manager of another team refused" 403 "$(upload sp_c.jar $TC sp_img.png)"
check "manager A cannot post as team B" 403 "$(upload sp_a.jar $TB sp_img.png)"
check "nothing stored" 0 "$(sql "SELECT COUNT(*) FROM MatchPhotos WHERE MatchId=$M")"
check "a non-image is refused" 400 "$(upload sp_a.jar $TA sp_fake.png)"
check "…with a reason" 1 "$(grep -c 'لا يطابق صورة' $T/body.html)"
check "manager A uploads" 200 "$(upload sp_a.jar $TA sp_img.png)"
P1=$(sql "SELECT MAX(Id) FROM MatchPhotos WHERE MatchId=$M AND TeamId=$TA")
F1=$(sql "SELECT Path FROM MatchPhotos WHERE Id=$P1")
check "…stored under the match folder" 1 "$(case "$F1" in /uploads/matches/$M/*.png) echo 1;; *) echo 0;; esac)"
check "…file served" 200 "$(curl -s -o /dev/null -w '%{http_code}' "$BASE$F1")"
check "manager B uploads for B, with a caption" 200 "$(upload sp_b.jar $TB sp_img.png "لحظة الهدف")"
P2=$(sql "SELECT MAX(Id) FROM MatchPhotos WHERE MatchId=$M AND TeamId=$TB")

echo "== Match photos: the page"
curl -s $BASE/matches/$M > sp_page.html
check "guest sees the photos tab with the count" 1 "$(grep -c 'الصور (2)' sp_page.html)"
check "guest sees both thumbnails" 2 "$(grep -c 'class="photo-thumb"' sp_page.html)"
check "guest: no upload form" 0 "$(grep -c 'data-photo-upload' sp_page.html)"
check "guest: no delete rights" 0 "$(grep -c 'data-can-delete="1"' sp_page.html)"
get sp_coach.jar /matches/$M > /dev/null
check "coach: no upload form" 0 "$(grep -c 'data-photo-upload' $T/body.html)"
get sp_a.jar /matches/$M > /dev/null
check "manager A: upload form for team A" 1 "$(grep -c "name=\"teamId\" value=\"$TA\"" $T/body.html)"
check "manager A: may delete only A's photo" 1 "$(grep -c 'data-can-delete="1"' $T/body.html)"
check "caption shown" 1 "$(grep -c 'data-caption="لحظة الهدف"' $T/body.html)"

echo "== Match photos: deleting and the limit"
del() { local tok=$(token $1 /matches/$M); post $1 /matches/photos/$2/delete "$tok"; }
check "manager B cannot delete A's photo" 403 "$(del sp_b.jar $P1)"
check "manager A deletes A's photo" 200 "$(del sp_a.jar $P1)"
check "…row gone" 0 "$(sql "SELECT COUNT(*) FROM MatchPhotos WHERE Id=$P1")"
check "…file gone" 404 "$(curl -s -o /dev/null -w '%{http_code}' "$BASE$F1")"
check "admin deletes B's photo (moderation)" 200 "$(del sp_adm.jar $P2)"
check "…audited" 1 "$(sql "SELECT COUNT(*) FROM AuditLogs WHERE EntityName='MatchPhoto' AND EntityId='$P2'")"
sql "INSERT INTO MatchPhotos(MatchId,TeamId,Path,CreatedAt) SELECT TOP 40 $M,$TA,'/uploads/x.png',SYSUTCDATETIME() FROM sys.objects" > /dev/null
check "41st photo of a team refused" 400 "$(upload sp_a.jar $TA sp_img.png)"
check "…with the limit message" 1 "$(grep -c 'الأقصى' $T/body.html)"
check "the other team still uploads" 200 "$(upload sp_b.jar $TB sp_img.png)"
sql "DELETE FROM MatchPhotos WHERE MatchId=$M AND Path='/uploads/x.png'" > /dev/null

echo "== Sponsors: admin only"
check "team manager cannot open sponsors" DENIED "$(denied "$(get sp_a.jar /Admin/Sponsors)")"
check "admin opens sponsors" 200 "$(get sp_adm.jar /Admin/Sponsors)"
save() { # save NAME SCOPE [curl -F args] -> status
  local name=$1 scope=$2; shift 2
  local tok=$(token sp_adm.jar /Admin/Sponsors/Create)
  printf '%s' "$name" > sp_name.txt
  postm sp_adm.jar /Admin/Sponsors/Save "$tok" -F Id=0 -F "NameAr=<sp_name.txt" -F Scope=$scope -F IsActive=true -F "Logo=@sp_img.png;type=image/png" "$@"
}
sid() { sql "SELECT Id FROM Sponsors WHERE NameAr=N'$1'"; }
check "logo required" 200 "$(tok=$(token sp_adm.jar /Admin/Sponsors/Create); postm sp_adm.jar /Admin/Sponsors/Save "$tok" -F Id=0 -F NameAr=NoLogo$R -F Scope=0)"
check "…not saved" 0 "$(sql "SELECT COUNT(*) FROM Sponsors WHERE NameAr='NoLogo$R'")"
check "non-http link refused" 200 "$(save "BadLink$R" 0 -F WebsiteUrl=ftp://x.om)"
check "wilayat without a wilayat refused" 200 "$(save "NoWil$R" 2)"
check "neither saved" 0 "$(sql "SELECT COUNT(*) FROM Sponsors WHERE NameAr IN ('BadLink$R','NoWil$R')")"
check "wilayat sponsor saved" 302 "$(save "WilSp$R" 2 -F GovernorateId=$GOV -F WilayatId=$WIL -F WebsiteUrl=instagram.com/wilsp$R -F SortOrder=1)"
check "…link gets https://" "https://instagram.com/wilsp$R" "$(sql "SELECT WebsiteUrl FROM Sponsors WHERE NameAr='WilSp$R'")"
check "country sponsor with WhatsApp" 302 "$(save "AllSp$R" 0 -F Phone=91112222 -F SortOrder=900)"
check "other governorate sponsor" 302 "$(save "OtherGov$R" 1 -F GovernorateId=$OTHERGOV)"
check "expired sponsor" 302 "$(save "Expired$R" 0 -F EndsOn=$YESTERDAY)"
check "future sponsor" 302 "$(save "Future$R" 0 -F StartsOn=$TOMORROW)"
check "paused sponsor" 302 "$(save "Paused$R" 0)"
TOK=$(token sp_adm.jar /Admin/Sponsors); post sp_adm.jar /Admin/Sponsors/Toggle/$(sid "Paused$R") "$TOK" > /dev/null
check "…paused" 0 "$(sql "SELECT CAST(IsActive AS int) FROM Sponsors WHERE NameAr='Paused$R'")"
WS=$(sid "WilSp$R"); AS=$(sid "AllSp$R")

echo "== Sponsors: where they show"
curl -s -A "$PHONE_UA" $BASE/matches/$M > sp_page.html
check "match page: the home team's wilayat sponsor" 1 "$(grep -c "data-sponsor=\"$WS\"" sp_page.html)"
check "match page: the country sponsor" 1 "$(grep -c "data-sponsor=\"$AS\"" sp_page.html)"
check "…wilayat first" 1 "$(grep -o 'data-sponsor="[0-9]*"' sp_page.html | head -1 | grep -c "\"$WS\"")"
for n in OtherGov Expired Future Paused; do
  check "match page: no $n sponsor" 0 "$(grep -c "data-sponsor=\"$(sid "$n$R")\"" sp_page.html)"
done
check "a view was counted" 1 "$(sql "SELECT Views FROM Sponsors WHERE Id=$WS")"
curl -s $BASE/matches/$M > /dev/null
check "a bot/curl view is not counted" 1 "$(sql "SELECT Views FROM Sponsors WHERE Id=$WS")"
check "home page in that wilayat: wilayat sponsor" 1 "$(curl -s -A "$PHONE_UA" -b "s3s_region=$GOV-$WIL-0-0" $BASE/ | grep -c "data-sponsor=\"$WS\"")"
curl -s -A "$PHONE_UA" -b "s3s_region=$OTHERGOV-0-0-0" $BASE/ > sp_page.html
check "home page elsewhere: no wilayat sponsor" 0 "$(grep -c "data-sponsor=\"$WS\"" sp_page.html)"
check "…but that governorate's sponsor" 1 "$(grep -c "data-sponsor=\"$(sid "OtherGov$R")\"" sp_page.html)"

echo "== Sponsors: clicks"
check "click opens the sponsor's link" "https://instagram.com/wilsp$R" "$(curl -s -A "$PHONE_UA" -o /dev/null -w '%{redirect_url}' $BASE/sponsors/$WS/go)"
check "…counted" 1 "$(sql "SELECT Clicks FROM Sponsors WHERE Id=$WS")"
check "no link: opens WhatsApp +968" 1 "$(curl -s -A "$PHONE_UA" -o /dev/null -w '%{redirect_url}' $BASE/sponsors/$AS/go | grep -c '^https://wa.me/96891112222')"
check "paused sponsor link goes home" "$BASE/" "$(curl -s -o /dev/null -w '%{redirect_url}' $BASE/sponsors/$(sid "Paused$R")/go)"
get sp_adm.jar /Admin/Sponsors > /dev/null
check "admin list shows the sponsor" 1 "$(grep -q "WilSp$R" $T/body.html && echo 1)"

echo "== Cleanup"
for n in WilSp AllSp OtherGov Expired Future Paused; do TOK=$(token sp_adm.jar /Admin/Sponsors); post sp_adm.jar /Admin/Sponsors/Delete/$(sid "$n$R") "$TOK" > /dev/null; done
check "sponsors deleted" 0 "$(sql "SELECT COUNT(*) FROM Sponsors WHERE NameAr LIKE '%$R'")"
rm -f sp_img.png sp_fake.png sp_name.txt sp_page.html sp_caption.txt

echo; echo "PASS=$PASS FAIL=$FAIL"
[ $FAIL -eq 0 ]
