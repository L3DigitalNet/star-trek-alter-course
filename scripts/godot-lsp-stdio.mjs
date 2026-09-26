#!/usr/bin/env node
/**
 * Bridge stdio LSP frames to a dedicated headless Godot editor's TCP service.
 * Adapt only .gd didOpen language IDs to gdscript; preserve other frames exactly.
 * Usage: node godot-lsp-stdio.mjs GODOT_BINARY ABSOLUTE_PROJECT_PATH
 * Requires Node's standard library and Linux /proc: listener ownership is checked
 * before forwarding client bytes, so a stolen ephemeral port cannot select an
 * unrelated editor. The supplied binary and project are trusted local inputs.
 * Editor settings are isolated; the inherited environment retains .NET discovery.
 */
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, readdir, readlink, rm } from 'node:fs/promises';
import net from 'node:net';
import { Transform } from 'node:stream';
import os from 'node:os';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const [binary, project, ...extra] = process.argv.slice(2);
if (!binary || !project || !path.isAbsolute(project) || extra.length) {
  console.error('Usage: godot-lsp-stdio.mjs GODOT_BINARY ABSOLUTE_PROJECT_PATH');
  process.exit(2);
}

let child;
let socket;
let settings;
let stopping = false;
let exited = false;
let exitCode = 0;
let stderrFailed = false;
// cclsp identifies unknown extensions as plaintext, but Godot ignores didOpen
// unless its language ID is gdscript. Keep this compatibility adapter at the
// transport boundary instead of modifying either installed upstream server.
const input = new Transform({
  transform(chunk, encoding, callback) {
    try {
      this.pending = Buffer.concat([this.pending ?? Buffer.alloc(0), chunk]);
      while (this.pending.length) {
        const end = this.pending.indexOf('\r\n\r\n');
        if (end < 0) {
          if (this.pending.length > 16_384) throw new Error('LSP header exceeds 16 KiB');
          break;
        }
        if (end > 16_384) throw new Error('LSP header exceeds 16 KiB');
        const header = this.pending.subarray(0, end).toString('ascii');
        const lengths = [...header.matchAll(/^Content-Length:[ \t]*(\d+)[ \t]*$/gim)];
        if (lengths.length !== 1) throw new Error('LSP frame requires one Content-Length');
        const length = Number(lengths[0][1]);
        if (!Number.isSafeInteger(length) || length > 16 * 1024 * 1024) {
          throw new Error('LSP frame exceeds 16 MiB');
        }
        const total = end + 4 + length;
        if (this.pending.length < total) break;
        const frame = this.pending.subarray(0, total);
        const message = JSON.parse(frame.subarray(end + 4).toString('utf8'));
        const document = message?.params?.textDocument;
        if (message?.method === 'textDocument/didOpen'
            && typeof document?.uri === 'string'
            && new URL(document.uri).pathname.endsWith('.gd')
            && document.languageId !== 'gdscript') {
          document.languageId = 'gdscript';
          const body = Buffer.from(JSON.stringify(message));
          const rewrittenHeader = header.replace(/^Content-Length:[^\r\n]*$/im, `Content-Length: ${body.length}`);
          this.push(Buffer.concat([Buffer.from(`${rewrittenHeader}\r\n\r\n`), body]));
        } else {
          this.push(frame);
        }
        this.pending = this.pending.subarray(total);
      }
      callback();
    } catch (error) { callback(error); }
  },
  flush(callback) {
    callback(this.pending?.length ? new Error('truncated LSP frame') : null);
  },
});
const startupDeadline = Date.now() + 30_000;

async function ownsListener(port) {
  const fds = await readdir(`/proc/${child.pid}/fd`);
  const links = await Promise.all(fds.map(async (fd) => {
    try { return await readlink(`/proc/${child.pid}/fd/${fd}`); }
    catch { return ''; }
  }));
  const owned = new Set(links.map((link) => /^socket:\[(\d+)\]$/.exec(link)?.[1]));
  const tables = await Promise.all(['tcp', 'tcp6'].map((name) => readFile(`/proc/net/${name}`, 'utf8')));
  return tables.flatMap((table) => table.split('\n').slice(1)).some((line) => {
    const fields = line.trim().split(/\s+/);
    return fields[1]?.endsWith(`:${port.toString(16).toUpperCase().padStart(4, '0')}`)
      && fields[3] === '0A' && owned.has(fields[9]);
  });
}

function killGroup(signal) {
  if (!child?.pid) return;
  try { process.kill(-child.pid, signal); }
  catch (error) { if (error.code !== 'ESRCH' && !stderrFailed) console.error(error.message); }
}

async function stop(code, message) {
  if (stopping) return;
  stopping = true;
  exitCode = code;
  if (message && !stderrFailed) console.error(`godot-lsp-stdio: ${message}`);
  process.stdin.unpipe(input);
  input.destroy();
  process.stdin.pause();
  socket?.destroy();
  // The detached group includes editor-spawned helpers. Kill the group even when
  // its leader already exited, otherwise an editor crash can leave helpers alive.
  killGroup('SIGTERM');
  if (child?.pid) {
    await delay(1_000);
    killGroup('SIGKILL');
    if (!exited) await Promise.race([
      new Promise((resolve) => child.once('exit', resolve)), delay(1_000),
    ]);
  }
  if (settings) await rm(settings, { recursive: true, force: true });
  process.exit(exitCode);
}

process.on('SIGINT', () => void stop(130));
process.on('SIGTERM', () => void stop(143));
input.once('finish', () => void stop(0));
input.on('error', (error) => void stop(1, `LSP input: ${error.message}`));
// Buffer with stream backpressure during startup while still observing an empty
// stdin EOF; leaving stdin paused would keep a disconnected client alive.
process.stdin.pipe(input);
process.stdin.on('error', (error) => void stop(1, `stdin: ${error.message}`));
process.stdout.on('error', (error) => void stop(1, `stdout: ${error.message}`));
// Console forwarding makes stderr a lifecycle dependency too. Reporting its
// EPIPE back to the same stream can recurse; drain editor output silently while
// the ordinary bounded process-group cleanup completes.
process.stderr.on('error', () => {
  stderrFailed = true;
  exitCode = 1;
  child?.stdout.unpipe(process.stderr);
  child?.stderr.unpipe(process.stderr);
  child?.stdout.resume();
  child?.stderr.resume();
  void stop(1);
});

try {
  settings = await mkdtemp(path.join(os.tmpdir(), 'godot-lsp-'));
  const reservation = net.createServer();
  await new Promise((resolve, reject) => {
    reservation.once('error', reject);
    reservation.listen(0, '127.0.0.1', resolve);
  });
  const port = reservation.address().port;
  await new Promise((resolve) => reservation.close(resolve));
  if (stopping) throw new Error('startup cancelled');
  child = spawn(binary, ['--headless', '--editor', '--path', project, '--lsp-port', String(port)], {
    detached: true,
    stdio: ['ignore', 'pipe', 'pipe'],
    env: { ...process.env, XDG_CONFIG_HOME: settings },
  });
  child.stdout.pipe(process.stderr, { end: false });
  child.stderr.pipe(process.stderr, { end: false });
  child.once('error', (error) => void stop(1, `editor: ${error.message}`));
  child.once('exit', (code, signal) => {
    exited = true;
    if (!stopping) void stop(1, `editor exited (${signal ?? code})`);
  });

  // Reserving port zero only chooses a candidate: releasing it introduces a bind
  // race. /proc ownership, rather than a successful connection or console text,
  // proves that this editor actually bound it before any client data is sent.
  while (!stopping && Date.now() < startupDeadline) {
    if (await ownsListener(port)) {
      const candidate = net.createConnection({ host: '127.0.0.1', port });
      let accepted = false;
      candidate.on('end', () => {
        if (accepted && !stopping) void stop(1, 'LSP socket closed');
      });
      candidate.on('close', () => {
        if (accepted && !stopping) void stop(1, 'LSP socket closed');
      });
      candidate.on('error', (error) => {
        if (accepted) void stop(1, `socket: ${error.message}`);
      });
      const connected = await new Promise((resolve) => {
        candidate.once('connect', () => { accepted = true; resolve(true); });
        candidate.once('error', () => resolve(false));
        candidate.setTimeout(500, () => { candidate.destroy(); resolve(false); });
      });
      candidate.setTimeout(0);
      if (connected && !stopping && await ownsListener(port)) {
        socket = candidate;
        input.pipe(socket);
        socket.pipe(process.stdout, { end: false });
        break;
      }
      candidate.destroy();
    }
    await delay(100);
  }
  if (!stopping && !socket) await stop(1, 'editor LSP startup exceeded 30 seconds');
} catch (error) {
  if (!stopping) await stop(1, error.message);
}
