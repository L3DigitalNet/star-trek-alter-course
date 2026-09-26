#!/usr/bin/env node
/**
 * Run the installed upstream cclsp bridge for one project language over MCP stdio.
 * Requires Node 18+ and Linux process groups so shutdown also reaches LSP descendants.
 * The installer owns .tools/agent-lsp/runtime.json and the upstream bundle; startup
 * never downloads tools. Godot's TCP transport is owned by godot-lsp-stdio.mjs.
 */
import { spawn } from 'node:child_process';
import { accessSync, constants, createReadStream, closeSync, mkdtempSync, openSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const language = process.argv[2];
let temporary;
let child;
let spool;
let stopping = false;
let timer;
let ready = false;
const installHint = 'Run scripts/install-agent-lsp.sh from the repository.';

function signalGroup(signal) {
  if (!child?.pid) return;
  try { process.kill(-child.pid, signal); } catch (error) {
    if (error.code !== 'ESRCH') process.stderr.write(`agent-lsp: ${error.message}\n`);
  }
}

async function stop(code, message) {
  if (stopping) return;
  stopping = true;
  clearTimeout(timer);
  if (message) process.stderr.write(`agent-lsp: ${message}\n`);
  process.stdin.pause();
  process.stdin.unpipe();
  child?.stdout.unpipe();
  signalGroup('SIGTERM');
  // cclsp can exit before its descendants. Give the Godot
  // transport time to clean up, then kill remaining members regardless of bridge exit.
  if (child?.pid) await new Promise((done) => setTimeout(done, 3200));
  signalGroup('SIGKILL');
  if (spool !== undefined) { closeSync(spool); spool = undefined; }
  if (temporary) rmSync(temporary, { recursive: true, force: true });
  process.exit(code);
}

try {
  if (process.platform !== 'linux' || Number(process.versions.node.split('.')[0]) < 18) {
    throw new Error('Requires Linux and Node 18 or newer.');
  }
  if (process.argv.length !== 3 || !['csharp', 'godot'].includes(language)) {
    throw new Error('Usage: node scripts/agent-lsp.mjs csharp|godot');
  }
  const tools = join(root, '.tools/agent-lsp');
  let runtime;
  try {
    runtime = JSON.parse(readFileSync(join(tools, 'runtime.json'), 'utf8'));
    if (!isAbsolute(runtime.dotnetRoot) || !isAbsolute(runtime.godotBinary)) throw new Error('Invalid runtime paths');
    accessSync(join(runtime.dotnetRoot, 'dotnet'), constants.X_OK);
    accessSync(join(tools, 'cclsp/package/dist/index.js'), constants.R_OK);
    accessSync(language === 'csharp' ? join(tools, 'roslyn/roslyn-language-server') : runtime.godotBinary, constants.X_OK);
    if (language === 'godot') accessSync(join(root, 'scripts/godot-lsp-stdio.mjs'), constants.R_OK);
  } catch (error) {
    throw new Error(`Missing or invalid agent LSP installation (${error.message}). ${installHint}`);
  }
  const extension = language === 'csharp' ? 'cs' : 'gd';
  const godotProject = join(root, 'src/AlterCourse.Godot');
  const command = language === 'csharp'
    ? [join(tools, 'roslyn/roslyn-language-server'), '--stdio', '--autoLoadProjects', '--logLevel', 'Warning']
    : [process.execPath, join(root, 'scripts/godot-lsp-stdio.mjs'), runtime.godotBinary, godotProject];
  temporary = mkdtempSync(join(tmpdir(), 'agent-lsp-'));
  const config = join(temporary, 'config.json');
  writeFileSync(config, JSON.stringify({ servers: [{ extensions: [extension], command, rootDir: language === 'csharp' ? root : godotProject }] }), { mode: 0o600 });
  const spoolPath = join(temporary, 'stdin');
  spool = openSync(spoolPath, 'w', 0o600);
  // Upstream 0.7.0 duplicates servers when tools arrive during async preloading.
  // Spool input to disk until both success markers arrive; draining input also
  // lets EOF cancel startup without waiting for the readiness deadline.
  const bufferInput = (chunk) => {
    try { writeFileSync(spool, chunk); } catch (error) { void stop(1, error.message); }
  };
  process.stdin.on('data', bufferInput);
  process.stdin.on('end', () => void stop(0));
  process.stdin.on('error', (error) => void stop(1, `stdin failed: ${error.message}`));
  process.stdout.on('error', (error) => void stop(1, `stdout failed: ${error.message}`));
  process.stderr.on('error', () => void stop(1));
  process.on('SIGINT', () => void stop(130));
  process.on('SIGTERM', () => void stop(143));
  child = spawn(process.execPath, [join(tools, 'cclsp/package/dist/index.js')], {
    cwd: root, detached: true, stdio: ['pipe', 'pipe', 'pipe'],
    env: { ...process.env, DOTNET_ROOT: runtime.dotnetRoot, PATH: `${runtime.dotnetRoot}:${process.env.PATH ?? ''}`, CCLSP_CONFIG_PATH: config },
  });
  child.on('error', (error) => void stop(1, error.message));
  child.on('exit', (code, signal) => void stop(code === 0 ? 0 : 1, `cclsp exited (${signal ?? code}).`));
  child.stdin.on('error', (error) => void stop(1, `bridge input failed: ${error.message}`));
  child.stdout.on('error', (error) => void stop(1, error.message));
  child.stdout.pipe(process.stdout, { end: false });
  let lineBuffer = '';
  let preloaded = false;
  let completed = false;
  child.stderr.on('data', (chunk) => {
    process.stderr.write(chunk);
    if (ready || stopping) return;
    lineBuffer += chunk.toString('utf8');
    let newline;
    while ((newline = lineBuffer.indexOf('\n')) >= 0) {
      const line = lineBuffer.slice(0, newline).replace(/\r$/, '');
      lineBuffer = lineBuffer.slice(newline + 1);
      if (line === 'LSP server preloading completed') completed = true;
      if (line === `Successfully preloaded LSP server for extensions: ${extension}`) preloaded = true;
    }
    if (!completed || !preloaded) return;
    ready = true;
    clearTimeout(timer);
    process.stdin.pause();
    process.stdin.removeListener('data', bufferInput);
    closeSync(spool);
    spool = undefined;
    const buffered = createReadStream(spoolPath);
    buffered.on('error', (error) => void stop(1, error.message));
    buffered.pipe(child.stdin, { end: false });
    buffered.on('end', () => {
      if (!stopping) process.stdin.pipe(child.stdin);
    });
  });
  child.stderr.on('error', (error) => void stop(1, error.message));
  timer = setTimeout(() => void stop(1, 'LSP readiness timed out after 60 seconds.'), 60_000);
} catch (error) {
  void stop(1, error.message);
}
