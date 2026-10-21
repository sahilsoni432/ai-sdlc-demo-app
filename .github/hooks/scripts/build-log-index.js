const childProcess = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");

const repositoryRoot = path.resolve(__dirname, "../../..");
const logsDirectory = path.join(repositoryRoot, "ai-logs");
const transcriptsDirectory = path.join(logsDirectory, "transcripts");
const sessionStateDirectory = path.join(os.homedir(), ".copilot", "session-state");
const briefSteps = new Map([
  ["4b5ad28", "Step 8: log all AI interactions (hooks)"],
  ["c027722", "Step 1: extend AGENTS.md"],
  ["cf2c22e", "Step 2: skills"],
  ["75f70fa", "Step 9: agents and sub-agents"],
  ["8592133", "Step 3: hooks for structured output"],
  ["7dcc7e7", "Step 4: spec-driven specification (OpenSpec)"],
  ["589b3e8", "Hand-in 6: mutation score before"],
  ["5c4359d", "Step 5 (stage 1): single-approver limits + unit tests"],
  ["68867d8", "Step 5 (stage 2): two-approver rule + unit tests"],
  ["0019b62", "Step 6: Playwright E2E test"],
  ["fe3360c", "Hand-in 6: mutation score after"],
  ["437a429", "Step 7: agent review + human verdicts"],
]);

function parseTimestamp(value) {
  if (typeof value === "number") {
    return new Date(value < 1e12 ? value * 1000 : value);
  }

  if (typeof value === "string") {
    return new Date(value);
  }

  return null;
}

function firstExportTimestamp(value, timestamps = []) {
  if (Array.isArray(value)) {
    for (const item of value) {
      firstExportTimestamp(item, timestamps);
    }
  } else if (value !== null && typeof value === "object") {
    for (const [key, child] of Object.entries(value)) {
      if (key === "timestamp") {
        const timestamp = parseTimestamp(child);
        if (timestamp && !Number.isNaN(timestamp.getTime())) {
          timestamps.push(timestamp);
        }
      }

      firstExportTimestamp(child, timestamps);
    }
  }

  return timestamps.sort((left, right) => left - right)[0] || null;
}

function readExportedSetupChat(filePath) {
  const data = JSON.parse(fs.readFileSync(filePath, "utf8"));
  const request = data.requests?.[0]?.message;
  const prompt =
    request?.text ||
    request?.parts?.find((part) => typeof part.text === "string")?.text ||
    "";
  const timestamp = firstExportTimestamp(data);

  if (!timestamp) {
    throw new Error(`No valid timestamp found in ${filePath}`);
  }

  return { timestamp, prompt };
}

function readMarkdownTranscript(filePath) {
  const contents = fs.readFileSync(filePath, "utf8");
  const headers = [
    ...contents.matchAll(
      /^### (\S+) — ([^\r\n]+)$/gm
    ),
  ];
  const firstTimestamp = headers
    .map((header) => parseTimestamp(header[1]))
    .find((timestamp) => timestamp && !Number.isNaN(timestamp.getTime()));
  const firstUserMessage = headers.find(
    (header) => header[2] === "user.message"
  );

  if (!firstTimestamp) {
    throw new Error(`No valid timestamp found in ${filePath}`);
  }

  let prompt = "";
  if (firstUserMessage) {
    const start = firstUserMessage.index + firstUserMessage[0].length;
    const nextHeader = headers.find((header) => header.index > start);
    prompt = contents.slice(start, nextHeader?.index ?? contents.length);
  }

  return { timestamp: firstTimestamp, prompt };
}

function singleLinePrompt(prompt) {
  const characters = Array.from(prompt.replace(/\s+/g, " ").trim());
  const escaped = characters.map((character) =>
    character === "|" ? "\\|" : character
  );
  const escapedLength = escaped.reduce(
    (length, character) => length + Array.from(character).length,
    0
  );

  if (escapedLength <= 120) {
    return escaped.join("");
  }

  let shortened = "";
  for (const character of escaped) {
    if (Array.from(shortened).length + Array.from(character).length > 117) {
      break;
    }
    shortened += character;
  }

  return `${shortened}...`;
}

function escapeTableText(value) {
  return value.replace(/\|/g, "\\|").replace(/\r?\n/g, " ");
}

function getTranscripts() {
  return fs
    .readdirSync(transcriptsDirectory, { withFileTypes: true })
    .filter(
      (entry) =>
        entry.isFile() && (entry.name.endsWith(".md") || entry.name === "00-setup-chat.json")
    )
    .map((entry) => {
      const filePath = path.join(transcriptsDirectory, entry.name);
      const isSetupChat = entry.name === "00-setup-chat.json";
      const transcript = isSetupChat
        ? readExportedSetupChat(filePath)
        : readMarkdownTranscript(filePath);
      const sessionId = isSetupChat
        ? null
        : path.basename(entry.name, ".md").replace(/^\d{4}-\d{2}-\d{2}-/, "");
      const type = isSetupChat
        ? "exported setup chat"
        : fs.existsSync(path.join(sessionStateDirectory, sessionId)) &&
            fs.statSync(path.join(sessionStateDirectory, sessionId)).isDirectory()
          ? "chat session"
          : "subagent run";

      return {
        name: entry.name,
        timestamp: transcript.timestamp,
        prompt: transcript.prompt,
        type,
      };
    })
    .sort(
      (left, right) =>
        left.timestamp - right.timestamp || left.name.localeCompare(right.name)
    );
}

function getCommits() {
  const output = childProcess.execFileSync(
    "git",
    ["log", "--reverse", "--format=%h %s", "main..HEAD"],
    { cwd: repositoryRoot, encoding: "utf8" }
  );

  return output
    .trim()
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => {
      const separator = line.indexOf(" ");
      return {
        hash: line.slice(0, separator),
        subject: line.slice(separator + 1),
      };
    });
}

function buildReadme() {
  const commits = getCommits();
  const transcripts = getTranscripts();
  const lines = [
    "# AI interaction logs",
    "",
    "## How logging works",
    "",
    "Hooks in `.github/hooks/ai-interaction-log.json` append every prompt, tool call, subagent start/stop, and stop event to `ai-interactions.jsonl`. On Stop, a Markdown transcript per session is written to `transcripts/`, containing user messages, assistant replies, tool calls, and permission decisions; sensitive field names are redacted and long values are truncated.",
    "",
    "## Phases",
    "",
    "| Brief step | Commit |",
    "| --- | --- |",
    ...commits.map((commit) => {
      const briefStep = briefSteps.get(commit.hash) || "—";
      return `| ${briefStep} | \`${commit.hash} ${escapeTableText(commit.subject)}\` |`;
    }),
    "",
    "## Transcripts",
    "",
    "| # | Date (UTC) | File | Type | First prompt |",
    "| ---: | --- | --- | --- | --- |",
    ...transcripts.map(
      (transcript, index) =>
        `| ${index + 1} | ${transcript.timestamp.toISOString().slice(0, 10)} | [${transcript.name}](transcripts/${encodeURIComponent(transcript.name)}) | ${transcript.type} | ${singleLinePrompt(transcript.prompt)} |`
    ),
    "",
    "## Notes",
    "",
    "- `00-setup-chat.json` was exported manually because the setup chat ran before the hooks existed.",
    "- One small edit by Claude Code (removing duplicated text in `AGENTS.md` lines 19–20, made at the user's request) is not captured by these Copilot logs.",
    "- Regenerate with `node .github/hooks/scripts/build-log-index.js`; the transcript of the chat that makes a commit is written when that chat ends, so it appears in the next regeneration.",
    "",
  ];

  fs.writeFileSync(path.join(logsDirectory, "README.md"), lines.join("\n"), "utf8");
  return transcripts.length;
}

const transcriptCount = buildReadme();
console.log(`Wrote ai-logs/README.md with ${transcriptCount} transcript rows.`);
