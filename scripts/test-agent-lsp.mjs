/**
 * Exercise the launcher with a disposable installation, without downloaded tools.
 * Requires Node 18+ on Linux: the fixture observes process-group cleanup as well
 * as byte transport. Fake cclsp preserves upstream's two-marker preload contract.
 */
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const fake = `
const fs = require('node:fs');
const {spawn} = require('node:child_process');
const config = JSON.parse(fs.readFileSync(process.env.CCLSP_CONFIG_PATH, 'utf8'));
fs.writeFileSync(process.env.RESULT, JSON.stringify({config, path:process.env.CCLSP_CONFIG_PATH, cwd:process.cwd(), dotnet:process.env.DOTNET_ROOT, searchPath:process.env.PATH}));
if (process.env.MODE === 'exit') process.exit(7);
const descendant = spawn(process.execPath, ['-e', "process.on('SIGTERM',()=>{});setInterval(()=>{},1000)"], {stdio:'ignore'});
fs.writeFileSync(process.env.PID_FILE, String(descendant.pid));
let ready = false;
process.stdin.on('data', chunk => {
  if (!ready) { fs.writeFileSync(process.env.EARLY, 'early'); process.exit(8); }
  process.stdout.write(chunk);
});
if (process.env.MODE !== 'hang') {
  setTimeout(() => process.stderr.write('Successfully preloaded LSP server for extensions: '+config.servers[0].extensions[0]+'\\n'), 80);
  setTimeout(() => process.stderr.write('Initialization timeout\\n'), 100);
  setTimeout(() => {ready=true;process.stderr.write('LSP server preload');}, 180);
  setTimeout(() => process.stderr.write('ing completed\\n'), 220);
}
setInterval(()=>{},1000);
`;

function fixture(mode = 'ready', language = 'csharp') {
  const root = mkdtempSync(join(tmpdir(), 'agent-lsp-test-'));
  for (const path of ['scripts', '.tools/agent-lsp/roslyn', '.tools/agent-lsp/cclsp/package/dist', 'sdk', 'src/AlterCourse.Godot']) {
    mkdirSync(join(root, path), { recursive: true });
  }
  copyFileSync(join(here, 'agent-lsp.mjs'), join(root, 'scripts/agent-lsp.mjs'));
  writeFileSync(join(root, 'scripts/godot-lsp-stdio.mjs'), '');
  for (const path of ['sdk/dotnet', 'godot', '.tools/agent-lsp/roslyn/roslyn-language-server']) {
    writeFileSync(join(root, path), '', { mode: 0o700 });
  }
  writeFileSync(join(root, '.tools/agent-lsp/runtime.json'), JSON.stringify({ dotnetRoot: join(root, 'sdk'), godotBinary: join(root, 'godot') }));
  writeFileSync(join(root, '.tools/agent-lsp/cclsp/package/dist/index.js'), fake);
  const child = spawn(process.execPath, [join(root, 'scripts/agent-lsp.mjs'), language], {
    cwd: tmpdir(), env: { ...process.env, MODE: mode, RESULT: join(root, 'result'), PID_FILE: join(root, 'pid'), EARLY: join(root, 'early') },
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  let stdout = Buffer.alloc(0);
  let stderr = '';
  child.stdout.on('data', (chunk) => { stdout = Buffer.concat([stdout, chunk]); });
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const exited = new Promise((done) => child.on('close', (code, signal) => done({ code, signal })));
  return { root, child, exited, output: () => stdout, error: () => stderr };
}

async function until(predicate) {
  const deadline = Date.now() + 5000;
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error('Fixture condition timed out');
    await new Promise((done) => setTimeout(done, 20));
  }
}

function assertCleanup(run) {
  if (existsSync(join(run.root, 'result'))) {
    const { path } = JSON.parse(readFileSync(join(run.root, 'result')));
    assert.equal(existsSync(dirname(path)), false, 'private session directory removed');
  }
  if (existsSync(join(run.root, 'pid'))) {
    const pid = Number(readFileSync(join(run.root, 'pid')));
    // A killed process can briefly remain a zombie until init reaps it; neither
    // state can retain ports or run language-server work after launcher cleanup.
    if (existsSync(`/proc/${pid}/stat`)) {
      assert.match(readFileSync(`/proc/${pid}/stat`, 'utf8'), /^\d+ \(.+\) Z /);
    }
  }
}

for (const language of ['csharp', 'godot']) {
  test(`${language}: waits for both markers, preserves bytes and project paths`, { timeout: 10_000 }, async () => {
    const run = fixture('ready', language);
    try {
      const payload = Buffer.from([0, 255, 10, 123, 125]);
      run.child.stdin.write(payload);
      await until(() => run.output().length === payload.length);
      assert.deepEqual(run.output(), payload);
      assert.equal(existsSync(join(run.root, 'early')), false);
      assert.match(run.error(), /Initialization timeout/);
      const observed = JSON.parse(readFileSync(join(run.root, 'result')));
      assert.equal(observed.cwd, run.root);
      assert.equal(observed.dotnet, join(run.root, 'sdk'));
      assert.ok(observed.searchPath.startsWith(`${observed.dotnet}:`));
      assert.deepEqual(observed.config, { servers: [{
        extensions: [language === 'csharp' ? 'cs' : 'gd'],
        rootDir: language === 'csharp' ? run.root : join(run.root, 'src/AlterCourse.Godot'),
        command: language === 'csharp'
          ? [join(run.root, '.tools/agent-lsp/roslyn/roslyn-language-server'), '--stdio', '--autoLoadProjects', '--logLevel', 'Warning']
          : [process.execPath, join(run.root, 'scripts/godot-lsp-stdio.mjs'), join(run.root, 'godot'), join(run.root, 'src/AlterCourse.Godot')],
      }] });
      run.child.stdin.end();
      assert.equal((await run.exited).code, 0);
      assertCleanup(run);
    } finally { run.child.kill('SIGKILL'); rmSync(run.root, { recursive: true, force: true }); }
  });
}

for (const cause of ['EOF', 'SIGINT', 'SIGTERM', 'exit']) {
  test(`cleanup on ${cause} while preload is pending`, { timeout: 10_000 }, async () => {
    const run = fixture(cause === 'exit' ? 'exit' : 'hang');
    try {
      await until(() => existsSync(join(run.root, cause === 'exit' ? 'result' : 'pid')));
      if (cause === 'EOF') run.child.stdin.end('pending request');
      else if (cause !== 'exit') run.child.kill(cause);
      assert.equal((await run.exited).code, { EOF: 0, SIGINT: 130, SIGTERM: 143, exit: 1 }[cause]);
      assertCleanup(run);
    } finally { run.child.kill('SIGKILL'); rmSync(run.root, { recursive: true, force: true }); }
  });
}

test('missing installation fails with installer remedy', { timeout: 5000 }, async () => {
  const root = mkdtempSync(join(tmpdir(), 'agent-lsp-missing-'));
  try {
    mkdirSync(join(root, 'scripts'));
    copyFileSync(join(here, 'agent-lsp.mjs'), join(root, 'scripts/agent-lsp.mjs'));
    const child = spawn(process.execPath, [join(root, 'scripts/agent-lsp.mjs'), 'csharp']);
    let stderr = '';
    child.stderr.on('data', (chunk) => { stderr += chunk; });
    const code = await new Promise((done) => child.on('close', done));
    assert.equal(code, 1);
    assert.match(stderr, /Run scripts\/install-agent-lsp.sh/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('output pipe failure cleans up the bridge and descendants', { timeout: 10_000 }, async () => {
  const run = fixture();
  try {
    run.child.stdin.write('first');
    await until(() => run.output().toString() === 'first');
    run.child.stdout.destroy();
    run.child.stdin.write('second');
    assert.equal((await run.exited).code, 1);
    assertCleanup(run);
  } finally { run.child.kill('SIGKILL'); rmSync(run.root, { recursive: true, force: true }); }
});
