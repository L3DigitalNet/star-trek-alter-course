/**
 * Exercise the stdio bridge with a local TCP editor double, without Godot imports.
 * Requires Linux /proc, Node's test runner and an executable temporary directory:
 * the fixture must be spawned exactly as the production Godot executable is.
 */
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, writeFile, chmod, rm, access } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import test from 'node:test';

const bridge = fileURLToPath(new URL('./godot-lsp-stdio.mjs', import.meta.url));
function frame(message) {
  const body = Buffer.from(JSON.stringify(message));
  return Buffer.concat([Buffer.from(`Content-Length: ${body.length}\r\n\r\n`), body]);
}
const fixture = `#!/usr/bin/env node
const fs = require('node:fs');
const net = require('node:net');
const { spawn } = require('node:child_process');
const args = process.argv.slice(2);
const project = args[args.indexOf('--path') + 1];
const port = Number(args[args.indexOf('--lsp-port') + 1]);
const mode = fs.readFileSync(project + '/mode', 'utf8');
const stubborn = mode === 'stubborn' || mode === 'noisy-stubborn';
const helper = stubborn ? spawn(process.execPath, ['-e', "process.on('SIGTERM',()=>{});setInterval(()=>{},1000)"], {stdio:'ignore'}) : null;
fs.writeFileSync(project + '/editor.json', JSON.stringify({pid:process.pid,helper:helper?.pid,config:process.env.XDG_CONFIG_HOME}));
console.log('EDITOR CONSOLE MUST NOT REACH STDOUT');
console.error('EDITOR STDERR');
if (mode === 'early') process.exit(7);
if (stubborn) process.on('SIGTERM', () => {});
if (mode === 'noisy-stubborn') setInterval(() => console.log('editor still running'), 50);
if (mode === 'waiting') setInterval(() => {}, 1000);
else net.createServer(socket => {
  if (mode === 'disconnect') socket.destroy();
  else socket.pipe(socket);
}).listen(port, mode === 'ipv6' ? '::' : '127.0.0.1');
`;

async function start(t, mode = 'echo', executable) {
  const dir = await mkdtemp(path.join(os.tmpdir(), 'godot-bridge-test-'));
  await writeFile(path.join(dir, 'editor'), fixture);
  await chmod(path.join(dir, 'editor'), 0o700);
  await writeFile(path.join(dir, 'mode'), mode);
  const child = spawn(process.execPath, [bridge, executable ?? path.join(dir, 'editor'), dir], {
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  const stdout = [];
  let stderr = '';
  child.stdout.on('data', (chunk) => stdout.push(chunk));
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const finished = new Promise((resolve) => child.once('exit', (code, signal) => resolve({ code, signal })));
  t.after(async () => {
    if (child.exitCode === null && child.signalCode === null) child.kill('SIGKILL');
    // A deliberately failing lifecycle regression must not leave its detached
    // fixture behind after the assertions have recorded the production defect.
    const owned = await readFile(path.join(dir, 'editor.json'), 'utf8').then(JSON.parse).catch(() => null);
    if (owned) {
      try { process.kill(-owned.pid, 'SIGKILL'); }
      catch (error) { if (error.code !== 'ESRCH') throw error; }
      await rm(owned.config, { recursive: true, force: true });
    }
    await rm(dir, { recursive: true, force: true });
  });
  return { dir, child, finished, output: () => Buffer.concat(stdout), errors: () => stderr };
}

async function until(predicate) {
  const deadline = Date.now() + 5_000;
  while (Date.now() < deadline) {
    if (await predicate()) return;
    await delay(20);
  }
  assert.fail('condition did not become true within 5 seconds');
}

async function editor(run) {
  await until(async () => {
    try { await access(path.join(run.dir, 'editor.json')); return true; }
    catch { return false; }
  });
  return JSON.parse(await readFile(path.join(run.dir, 'editor.json'), 'utf8'));
}

async function closed(run, expected, owned) {
  const result = await Promise.race([
    run.finished, delay(5_000).then(() => { throw new Error('bridge did not exit'); }),
  ]);
  assert.equal(result.code, expected, run.errors());
  if (owned) {
    assert.throws(() => process.kill(owned.pid, 0), { code: 'ESRCH' });
    if (owned.helper) {
      // A killed orphan can briefly remain a zombie awaiting the system reaper.
      // It must never remain an executing helper after the bridge has exited.
      const stat = await readFile(`/proc/${owned.helper}/stat`, 'utf8').catch(() => '');
      assert.ok(stat === '' || stat.split(') ')[1].startsWith('Z '), stat);
    }
    await assert.rejects(access(owned.config), { code: 'ENOENT' });
  }
}

test('forwards byte-exact traffic and keeps console output on stderr; EOF cleans editor', async (t) => {
  const run = await start(t);
  const owned = await editor(run);
  const bytes = frame({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { text: 'λ🌌' } });
  run.child.stdin.write(bytes.subarray(0, 7));
  run.child.stdin.write(bytes.subarray(7));
  await until(() => run.output().length === bytes.length);
  assert.deepEqual(run.output(), bytes);
  assert.match(run.errors(), /EDITOR CONSOLE/);
  assert.match(run.errors(), /EDITOR STDERR/);
  run.child.stdin.end();
  await closed(run, 0, owned);
});

test('disconnected stderr cleans editor and helpers without recursive diagnostics', async (t) => {
  const run = await start(t, 'noisy-stubborn');
  const owned = await editor(run);
  const bytes = frame({ method: 'initialized', params: {} });
  run.child.stdin.write(bytes);
  await until(() => run.output().equals(bytes));
  run.child.stderr.destroy();
  await closed(run, 1, owned);
});

for (const [signal, code] of [['SIGINT', 130], ['SIGTERM', 143]]) {
  test(`${signal} cleans a running editor, including kill fallback`, async (t) => {
    const run = await start(t, 'stubborn');
    const owned = await editor(run);
    const bytes = frame({ method: 'initialized', params: {} });
    run.child.stdin.write(bytes);
    await until(() => run.output().equals(bytes));
    run.child.kill(signal);
    await closed(run, code, owned);
  });
}

test('signal during startup cleans an editor that never listens', async (t) => {
  const run = await start(t, 'waiting');
  const owned = await editor(run);
  run.child.kill('SIGTERM');
  await closed(run, 143, owned);
});

test('editor failure terminates bridge and removes isolated settings', async (t) => {
  const run = await start(t, 'early');
  const owned = await editor(run);
  await closed(run, 1, owned);
  assert.match(run.errors(), /editor exited \(7\)/);
  assert.equal(run.output().length, 0);
});

test('missing editor executable fails cleanly', async (t) => {
  const run = await start(t, 'echo', '/nonexistent/godot-bridge-fixture');
  await closed(run, 1);
  assert.match(run.errors(), /ENOENT/);
  assert.equal(run.output().length, 0);
});

test('unexpected LSP socket close terminates editor', async (t) => {
  const run = await start(t, 'disconnect');
  const owned = await editor(run);
  await closed(run, 1, owned);
  assert.match(run.errors(), /LSP socket closed/);
});

test('stdin EOF during startup cleans an editor that never listens', async (t) => {
  const run = await start(t, 'waiting');
  const owned = await editor(run);
  run.child.stdin.end();
  await closed(run, 0, owned);
});

test('recognizes an editor with a dual-stack IPv6 listener', async (t) => {
  const run = await start(t, 'ipv6');
  const owned = await editor(run);
  const bytes = frame({ method: 'initialized', params: {} });
  run.child.stdin.write(bytes);
  await until(() => run.output().equals(bytes));
  run.child.stdin.end();
  await closed(run, 0, owned);
});

test('adapts split UTF8 .gd didOpen and preserves coalesced other messages exactly', async (t) => {
  const run = await start(t);
  const owned = await editor(run);
  const opening = { jsonrpc: '2.0', method: 'textDocument/didOpen', params: {
    textDocument: { uri: 'file:///project/space.gd', languageId: 'plaintext', version: 1, text: '# λ🌌' },
  } };
  const original = frame(opening);
  const other = frame({ method: 'textDocument/didOpen', params: {
    textDocument: { uri: 'file:///project/space.cs', languageId: 'csharp', text: 'λ' },
  } });
  const untouchedBody = Buffer.from('{ "method": "initialized", "params": {"a": "λ"} }');
  const untouched = Buffer.concat([
    Buffer.from(`Content-Length: ${untouchedBody.length}\r\nX-Fixture: keep\r\n\r\n`), untouchedBody,
  ]);
  // The split lands inside a multibyte codepoint, so character counts cannot
  // accidentally stand in for the protocol's UTF8 byte lengths.
  const split = original.indexOf(Buffer.from('🌌')) + 2;
  run.child.stdin.write(original.subarray(0, split));
  run.child.stdin.write(Buffer.concat([original.subarray(split), other, untouched]));
  opening.params.textDocument.languageId = 'gdscript';
  const expected = Buffer.concat([frame(opening), other, untouched]);
  await until(() => run.output().length === expected.length);
  assert.deepEqual(run.output(), expected);
  run.child.stdin.end();
  await closed(run, 0, owned);
});

for (const [label, bytes] of [
  ['missing length', Buffer.from('Wrong: 3\r\n\r\n{}')],
  ['oversized frame', Buffer.from('Content-Length: 16777217\r\n\r\n')],
  ['invalid JSON', Buffer.from('Content-Length: 3\r\n\r\nxxx')],
  ['truncated frame', Buffer.from('Content-Length: 10\r\n\r\n{}')],
]) {
  test(`rejects ${label} and cleans editor`, async (t) => {
    const run = await start(t);
    const owned = await editor(run);
    run.child.stdin.end(bytes);
    await closed(run, 1, owned);
    assert.match(run.errors(), /LSP input:/);
  });
}
