// Mock Web Push service for tests. Acts like FCM/Mozilla: receives the server's encrypted pushes and decrypts them
// with the device keys it generated (proving the payload, encryption and VAPID header are valid).
//   node push_mock.mjs <port> <outDir>
// Writes <outDir>/push_keys.json  { p256dh, auth }   (the "device" subscription keys, base64url)
// Appends one JSON line per received push to <outDir>/push_log.jsonl  { path, status, encoding, vapidKey, payload }
// Paths starting with /gone/ answer 410 (subscription expired); everything else 201.
import http from 'node:http';
import crypto from 'node:crypto';
import { writeFileSync, appendFileSync } from 'node:fs';
import { join } from 'node:path';

const [port, out] = [Number(process.argv[2] || 9555), process.argv[3] || '.'];
const b64u = b => Buffer.from(b).toString('base64url');
const fromB64u = s => Buffer.from(s.replace(/=+$/, ''), 'base64url');

const ecdh = crypto.createECDH('prime256v1');
const devicePublic = ecdh.generateKeys();
const auth = crypto.randomBytes(16);
writeFileSync(join(out, 'push_keys.json'), JSON.stringify({ p256dh: b64u(devicePublic), auth: b64u(auth) }));

const hkdf = (salt, ikm, info, len) => Buffer.from(crypto.hkdfSync('sha256', ikm, salt, info, len));
const gcm = (key, nonce, data) => {
    const d = crypto.createDecipheriv('aes-128-gcm', key, nonce);
    d.setAuthTag(data.subarray(data.length - 16));
    return Buffer.concat([d.update(data.subarray(0, data.length - 16)), d.final()]);
};

function decrypt(headers, body) {
    const encoding = headers['content-encoding'];
    if (encoding === 'aes128gcm') {
        const salt = body.subarray(0, 16), idlen = body[20], serverPublic = body.subarray(21, 21 + idlen);
        const secret = ecdh.computeSecret(serverPublic);
        const ikm = hkdf(auth, secret, Buffer.concat([Buffer.from('WebPush: info\0'), devicePublic, serverPublic]), 32);
        const plain = gcm(hkdf(salt, ikm, Buffer.from('Content-Encoding: aes128gcm\0'), 16),
            hkdf(salt, ikm, Buffer.from('Content-Encoding: nonce\0'), 12), body.subarray(21 + idlen));
        return plain.subarray(0, plain.lastIndexOf(2)).toString('utf8');
    }
    // aesgcm (draft-04): keys in the Crypto-Key / Encryption headers, 2-byte padding length prefix.
    const param = (h, name) => (h || '').split(/[;,]/).map(s => s.trim()).find(s => s.startsWith(name + '='))?.slice(name.length + 1);
    const serverPublic = fromB64u(param(headers['crypto-key'], 'dh'));
    const salt = fromB64u(param(headers['encryption'], 'salt'));
    const secret = ecdh.computeSecret(serverPublic);
    const ikm = hkdf(auth, secret, Buffer.from('Content-Encoding: auth\0'), 32);
    const len = k => Buffer.from([0, k.length]);
    const context = Buffer.concat([Buffer.from('P-256\0'), len(devicePublic), devicePublic, len(serverPublic), serverPublic]);
    const plain = gcm(hkdf(salt, ikm, Buffer.concat([Buffer.from('Content-Encoding: aesgcm\0'), context]), 16),
        hkdf(salt, ikm, Buffer.concat([Buffer.from('Content-Encoding: nonce\0'), context]), 12), body);
    return plain.subarray(2 + plain.readUInt16BE(0)).toString('utf8');
}

http.createServer((req, res) => {
    const chunks = [];
    req.on('data', c => chunks.push(c));
    req.on('end', () => {
        const gone = req.url.startsWith('/gone/');
        const entry = { path: req.url, status: gone ? 410 : 201, encoding: req.headers['content-encoding'] };
        const authz = req.headers['authorization'] || '';
        entry.vapidKey = /k=([A-Za-z0-9_-]+)/.exec(authz)?.[1] || /p256ecdsa=([A-Za-z0-9_-]+)/.exec(req.headers['crypto-key'] || '')?.[1] || null;
        entry.hasJwt = /(vapid t=|WebPush )eyJ/.test(authz);
        try { entry.payload = JSON.parse(decrypt(req.headers, Buffer.concat(chunks))); } catch (e) { entry.error = e.message; }
        appendFileSync(join(out, 'push_log.jsonl'), JSON.stringify(entry) + '\n');
        res.writeHead(entry.status).end();
    });
}).listen(port, '127.0.0.1', () => console.log(`push mock on ${port}`));
