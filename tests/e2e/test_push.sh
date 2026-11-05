#!/usr/bin/env bash
# Web Push: subscription endpoints (auth, CSRF, endpoint allow-list, ownership) and real delivery through a mock
# push service that decrypts every message (push_mock.mjs). Needs Node.
cd "$(dirname "$0")"
. ./lib.sh
rm -f pu_*.jar push_log.jsonl push_keys.json
for i in $(seq 1 60); do curl -s -o /dev/null $BASE/ && break; sleep 1; done
R=$RANDOM
MOCK=9555
uuid() { cat /proc/sys/kernel/random/uuid 2>/dev/null || powershell -NoProfile -Command "[guid]::NewGuid().ToString()" | tr -d '\r'; }
register() { local tok=$(token $1 /Account/Register)
  post $1 /Account/Register "$tok" -d "FullName=$(urlenc "$2")" -d Email=$3 -d Password=Test@1234 -d ConfirmPassword=Test@1234 > /dev/null; }
login() { local tok=$(token $1 /Account/Login); post $1 /Account/Login "$tok" -d Email=$2 --data-urlencode "Password=$3" > /dev/null; }
ev() { local tok=$(token pu_admin.jar /Teams); post pu_admin.jar /MatchControl/AddEvent/$M "$tok" -d EventType=$1 -d ClientRequestId=$(uuid) "${@:2}"; }

node push_mock.mjs $MOCK . > /dev/null 2>&1 &
MOCKPID=$!
trap 'kill $MOCKPID 2>/dev/null' EXIT
for i in $(seq 1 20); do [ -f push_keys.json ] && break; sleep 0.3; done

echo "== Setup"
KHOUDH=$(sql "SELECT Id FROM Areas WHERE NameEn='Al Khoudh'")
for n in H A; do
  sql "INSERT INTO Teams(SportId,NameAr,NameEn,Slug,Status,IsDeleted,CreatedAt) VALUES(1,N'دفع $n $R','Push $n $R','push-$n-$R',1,0,SYSUTCDATETIME()); INSERT INTO TeamAreas(TeamId,AreaId,IsPrimary) VALUES(SCOPE_IDENTITY(),$KHOUDH,1);" > /dev/null
done
TH=$(sql "SELECT Id FROM Teams WHERE Slug='push-H-$R'"); TA=$(sql "SELECT Id FROM Teams WHERE Slug='push-A-$R'")
sql "INSERT INTO Matches(SportId,HomeTeamId,AwayTeamId,KickoffAt,Status,HomeScore,AwayScore,CurrentPeriod,IsDeleted,CreatedAt) VALUES(1,$TH,$TA,SYSUTCDATETIME(),0,0,0,0,0,SYSUTCDATETIME())" > /dev/null
M=$(sql "SELECT Id FROM Matches WHERE HomeTeamId=$TH")
FAN=pfan-$R@test.om; OTHER=pother-$R@test.om
register pu_fan.jar "مشجع" $FAN; register pu_other.jar "آخر" $OTHER
login pu_admin.jar admin@s3sport.om "$ADMIN_PW"
TOK=$(token pu_fan.jar /Teams); post pu_fan.jar /Follows/Follow "$TOK" -d targetType=Team -d targetId=$TH > /dev/null
FANID=$(sql "SELECT Id FROM AspNetUsers WHERE Email='$FAN'")

echo "== Endpoints"
check "key requires login" 302 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/Push/Key)"
KEY=$(curl -s -b pu_fan.jar $BASE/Push/Key | sed 's/.*"publicKey":"\([^"]*\)".*/\1/')
check "public key is a P-256 point (87 chars base64url)" 87 "${#KEY}"
check "same key across calls" "$KEY" "$(curl -s -b pu_other.jar $BASE/Push/Key | sed 's/.*"publicKey":"\([^"]*\)".*/\1/')"
TOK=$(token pu_fan.jar /Notifications)
FCM="https://fcm.googleapis.com/fcm/send/test-$R"
check "subscribe without CSRF token" 400 "$(curl -s -o /dev/null -w '%{http_code}' -b pu_fan.jar -X POST $BASE/Push/Subscribe -d endpoint=$FCM -d p256dh=x -d auth=y)"
check "guest cannot subscribe" 302 "$(curl -s -o /dev/null -w '%{http_code}' -X POST $BASE/Push/Subscribe -d endpoint=$FCM -d p256dh=x -d auth=y)"
for bad in "http://fcm.googleapis.com/x" "https://127.0.0.1/x" "https://evil.example.com/x" "https://fcm.googleapis.com.evil.com/x" "https://fcm.googleapis.com:8443/x" "not-a-url"; do
  check "endpoint refused: $bad" 400 "$(post pu_fan.jar /Push/Subscribe "$TOK" --data-urlencode "endpoint=$bad" -d p256dh=x -d auth=y)"
done
check "missing keys refused" 400 "$(post pu_fan.jar /Push/Subscribe "$TOK" --data-urlencode "endpoint=$FCM" -d p256dh= -d auth=y)"
check "subscribe (FCM)" 200 "$(post pu_fan.jar /Push/Subscribe "$TOK" --data-urlencode "endpoint=$FCM" -d p256dh=k1 -d auth=a1)"
check "stored for fan" "$FANID" "$(sql "SELECT UserId FROM PushSubscriptions WHERE Endpoint='$FCM'")"
check "resubscribe updates, no duplicate" 200 "$(post pu_fan.jar /Push/Subscribe "$TOK" --data-urlencode "endpoint=$FCM" -d p256dh=k2 -d auth=a2)"
check "one row, new keys" "1/k2" "$(sql "SELECT CONCAT(COUNT(*),'/',MAX(P256dh)) FROM PushSubscriptions WHERE Endpoint='$FCM'")"
OTOK=$(token pu_other.jar /Notifications)
check "other user cannot remove fan's device" 200 "$(post pu_other.jar /Push/Unsubscribe "$OTOK" --data-urlencode "endpoint=$FCM")"
check "still there" 1 "$(sql "SELECT COUNT(*) FROM PushSubscriptions WHERE Endpoint='$FCM'")"
check "same device signed in as another user moves to them" 200 "$(post pu_other.jar /Push/Subscribe "$OTOK" --data-urlencode "endpoint=$FCM" -d p256dh=k3 -d auth=a3)"
check "owner is now the other user" 1 "$(sql "SELECT COUNT(*) FROM PushSubscriptions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE s.Endpoint='$FCM' AND u.Email='$OTHER'")"
check "unsubscribe" 200 "$(post pu_other.jar /Push/Unsubscribe "$OTOK" --data-urlencode "endpoint=$FCM")"
check "removed" 0 "$(sql "SELECT COUNT(*) FROM PushSubscriptions WHERE Endpoint='$FCM'")"
check "notifications page has device toggle" 1 "$(curl -s -b pu_fan.jar $BASE/Notifications | grep -c 'data-push-toggle')"
check "push.js served" 200 "$(curl -s -o /dev/null -w '%{http_code}' $BASE/js/push.js)"

echo "== Delivery through the mock push service"
P256=$(sed 's/.*"p256dh":"\([^"]*\)".*/\1/' push_keys.json); AUTH=$(sed 's/.*"auth":"\([^"]*\)".*/\1/' push_keys.json)
# Inserted directly: the controller (correctly) only accepts real push-service hosts.
sql "INSERT INTO PushSubscriptions(UserId,Endpoint,P256dh,Auth,CreatedAt) VALUES('$FANID','http://127.0.0.1:$MOCK/ok/$R','$P256','$AUTH',SYSUTCDATETIME()),('$FANID','http://127.0.0.1:$MOCK/gone/$R','$P256','$AUTH',SYSUTCDATETIME())" > /dev/null
check "kick-off" 200 "$(ev MatchStarted)"
check "goal" 200 "$(ev Goal -d TeamId=$TH -d Minute=7)"
for i in $(seq 1 30); do [ "$(grep -c "/ok/$R" push_log.jsonl 2>/dev/null)" -ge 2 ] && break; sleep 0.5; done
check "2 pushes reached the device" 2 "$(grep -c "/ok/$R" push_log.jsonl)"
check "payload decrypted: kick-off title" 1 "$(grep "/ok/$R" push_log.jsonl | grep -c 'بدأت المباراة')"
check "payload decrypted: goal title + url" 1 "$(grep "/ok/$R" push_log.jsonl | grep '⚽ هدف' | grep -c "\"url\":\"/matches/$M\"")"
check "no decryption errors" 0 "$(grep -c '"error"' push_log.jsonl)"
check "signed with the server's VAPID key" 0 "$(grep "/ok/$R" push_log.jsonl | grep -vc "\"vapidKey\":\"$KEY\"")"
check "VAPID JWT present" 0 "$(grep "/ok/$R" push_log.jsonl | grep -c '"hasJwt":false')"
check "expired (410) subscription deleted" 0 "$(sql "SELECT COUNT(*) FROM PushSubscriptions WHERE Endpoint LIKE '%/gone/$R'")"
check "working subscription kept" 1 "$(sql "SELECT COUNT(*) FROM PushSubscriptions WHERE Endpoint LIKE '%/ok/$R'")"
check "outbox drained" 0 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND PushPending=1")"
N=$(grep -c "/ok/$R" push_log.jsonl)
TOK=$(token pu_fan.jar /Teams); post pu_fan.jar /Follows/ToggleNotifications "$TOK" -d targetType=Team -d targetId=$TH > /dev/null
check "muted team" 200 "$(ev YellowCard -d TeamId=$TA -d Minute=9)"
sleep 4
check "muted: nothing pushed" "$N" "$(grep -c "/ok/$R" push_log.jsonl)"
sql "INSERT INTO Notifications(UserId,Type,Title,IsRead,PushPending,CreatedAt) VALUES('$FANID',7,N'قديم',0,1,DATEADD(MINUTE,-30,SYSUTCDATETIME()))" > /dev/null
sleep 4
check "stale notification (30 min) not pushed" 0 "$(grep -c 'قديم' push_log.jsonl)"
check "stale notification cleared from outbox" 0 "$(sql "SELECT COUNT(*) FROM Notifications WHERE UserId='$FANID' AND PushPending=1")"

echo
echo "RESULT: $PASS passed, $FAIL failed"
