// -*- coding: utf-8 -*-
/**
 * Unity MCP 브리지에 <b>직접</b> 붙는 명령줄 도구 (2026-08-15 신설).
 *
 * ★ 왜 이게 생겼나
 * ----------------
 * `.mcp.json` 은 mcp-unity 서버를 `"command": "node"` 로 띄운다. 그런데 이 PC 의 PATH 에는
 * node 가 없다(설치는 돼 있다 — `C:\Program Files\nodejs\node.exe`). 그래서 MCP 서버 프로세스가
 * 아예 안 뜨고, 대화 세션에 `update_component` 같은 도구가 하나도 붙지 않는다.
 * Unity 쪽 브리지(포트 8090)는 멀쩡히 살아 있는데도 그렇다.
 *
 * mcp-unity 서버가 하는 일은 결국 <b>WebSocket 으로 `{id, method, params}` 를 보내고
 * `{id, result|error}` 를 받는 것</b> 뿐이다(`Server~/src/unity/mcpUnity.ts`
 * `sendRequestInternal` · `Editor/UnityBridge/McpUnitySocketHandler.cs` `OnMessage`).
 * 이 파일은 그 한 줄짜리 규약을 그대로 구현한다 — <b>같은 브리지, 같은 도구, 같은 결과</b>다.
 * 씬 YAML 을 직접 건드리는 것이 아니라 Unity 의 에디터 API 를 그대로 태운다.
 *
 * ⚠ 이건 MCP <b>대체품이 아니라 우회로</b>다. `.mcp.json` 을 절대경로로 고쳐뒀으므로
 *   다음 세션부터는 네이티브 MCP 도구가 붙는다. 그때도 이 파일은 남겨둘 값어치가 있다 —
 *   파이썬 파이프라인(`Tools/*.py`)이 끝난 뒤 `refresh_assets`·`recompile_scripts` 를
 *   스스로 부를 수 있게 되기 때문이다.
 *
 * 쓰는 법
 * -------
 *   node Tools/mcp_unity_cli.js <method> '<json params>'
 *   node Tools/mcp_unity_cli.js --batch <파일.json>      // [{method, params}, ...] 배열
 *
 * 예)
 *   node Tools/mcp_unity_cli.js get_scenes_hierarchy '{}'
 *   node Tools/mcp_unity_cli.js get_gameobject '{"objectPath":"UI_Root/HUD_Log"}'
 *   node Tools/mcp_unity_cli.js update_component '{"objectPath":"UI_Root/HUD_Boss", ...}'
 *
 * ⚠ Unity 가 컴파일/임포트 중이면 응답이 늦다. 기본 타임아웃 120초.
 */

'use strict';

const path = require('path');
const fs = require('fs');

// ws 모듈은 mcp-unity 패키지가 이미 갖고 있다 — 따로 설치하지 않는다.
const PROJECT = path.dirname(__dirname);
const PKG_ROOT = path.join(PROJECT, 'Library', 'PackageCache');

function findWs() {
  const envPath = process.env.MCP_UNITY_WS_MODULE;
  if (envPath && fs.existsSync(envPath)) return require(envPath);

  const dirs = fs.existsSync(PKG_ROOT) ? fs.readdirSync(PKG_ROOT) : [];
  for (const d of dirs) {
    if (!d.startsWith('com.gamelovers.mcp-unity')) continue;
    const p = path.join(PKG_ROOT, d, 'Server~', 'node_modules', 'ws');
    if (fs.existsSync(p)) return require(p);
  }
  // 마지막 수단 — 전역에 설치돼 있을 수도 있다.
  return require('ws');
}

const WebSocket = findWs();

const PORT = readPort();
const URL = `ws://localhost:${PORT}/McpUnity`;
const TIMEOUT_MS = Number(process.env.MCP_UNITY_TIMEOUT_MS || 120000);

/** 포트는 ProjectSettings/McpUnitySettings.json 이 정본이다(기본 8090). */
function readPort() {
  try {
    const p = path.join(PROJECT, 'ProjectSettings', 'McpUnitySettings.json');
    const j = JSON.parse(fs.readFileSync(p, 'utf8'));
    if (j && j.Port) return j.Port;
  } catch (_) { /* 없으면 기본값 */ }
  return 8090;
}

/**
 * 요청 여러 개를 <b>한 연결에서 순서대로</b> 보낸다.
 * 연결을 매번 새로 여는 것보다 빠르고, Unity 쪽 로그도 덜 지저분해진다.
 */
function run(requests) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL, { headers: { 'X-Client-Name': 'mcp_unity_cli' } });
    const results = [];
    let index = 0;
    let timer = null;

    const fail = (err) => {
      if (timer) clearTimeout(timer);
      try { ws.close(); } catch (_) { /* 이미 닫혔으면 그만 */ }
      reject(err);
    };

    const sendNext = () => {
      if (index >= requests.length) {
        if (timer) clearTimeout(timer);
        try { ws.close(); } catch (_) { /* noop */ }
        resolve(results);
        return;
      }
      const req = requests[index];
      timer = setTimeout(
        () => fail(new Error(`시간 초과 (${TIMEOUT_MS}ms) — ${req.method}`)),
        TIMEOUT_MS);
      ws.send(JSON.stringify({ id: `cli-${index}`, method: req.method, params: req.params || {} }));
    };

    ws.on('open', sendNext);

    ws.on('message', (data) => {
      if (timer) clearTimeout(timer);
      let msg;
      try {
        msg = JSON.parse(data.toString());
      } catch (e) {
        return fail(new Error(`응답이 JSON 이 아닙니다: ${data.toString().slice(0, 200)}`));
      }
      // 하트비트(ping/pong)는 요청 응답이 아니다 — 세지 않는다.
      if (msg && msg.type === 'pong') { sendNext(); return; }

      results.push({ method: requests[index].method, response: msg });
      index += 1;
      sendNext();
    });

    ws.on('error', (err) =>
      fail(new Error(
        `Unity 브리지(${URL})에 붙지 못했습니다: ${err.message}\n` +
        `  · Unity 에디터가 켜져 있는지\n` +
        `  · Window > MCP Unity 의 서버가 Start 상태인지 확인하십시오.`)));

    ws.on('close', () => {
      if (index < requests.length) fail(new Error('요청을 다 보내기 전에 연결이 끊겼습니다.'));
    });
  });
}

async function main() {
  const argv = process.argv.slice(2);
  if (argv.length === 0) {
    console.error('사용법: node Tools/mcp_unity_cli.js <method> \'<json params>\'');
    console.error('        node Tools/mcp_unity_cli.js --batch <파일.json>');
    process.exit(2);
  }

  let requests;
  if (argv[0] === '--batch') {
    requests = JSON.parse(fs.readFileSync(argv[1], 'utf8'));
    if (!Array.isArray(requests)) throw new Error('--batch 파일은 배열이어야 합니다.');
  } else {
    requests = [{ method: argv[0], params: argv[1] ? JSON.parse(argv[1]) : {} }];
  }

  const results = await run(requests);

  // 실패가 하나라도 있으면 종료 코드를 1 로 — 스크립트에서 성공 여부를 볼 수 있게.
  let failed = 0;
  for (const r of results) {
    const err = r.response && r.response.error;
    const res = r.response && r.response.result;
    if (err || (res && res.success === false)) failed += 1;
    console.log(JSON.stringify({ method: r.method, ...(r.response || {}) }, null, 2));
  }
  process.exit(failed > 0 ? 1 : 0);
}

main().catch((err) => {
  console.error('[mcp_unity_cli] ' + err.message);
  process.exit(1);
});
