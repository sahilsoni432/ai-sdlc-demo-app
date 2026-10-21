const os = require("node:os");
const path = require("node:path");
const { writeTranscript } = require("./transcript");

const sessionIds = process.argv.slice(2);
const workspaceRoot = path.resolve(__dirname, "..", "..", "..");

if (sessionIds.length === 0) {
  console.error("Usage: node backfill-transcripts.js <session-id> [session-id ...]");
  process.exit(1);
}

let failed = false;
for (const sessionId of sessionIds) {
  const transcriptPath = path.join(
    os.homedir(),
    ".copilot",
    "session-state",
    sessionId,
    "events.jsonl"
  );

  try {
    const outputPath = writeTranscript(transcriptPath, sessionId, workspaceRoot);
    console.log(`Wrote ${path.basename(outputPath)}`);
  } catch (error) {
    failed = true;
    console.error(`Could not backfill ${sessionId}: ${error.message}`);
  }
}

if (failed) {
  process.exitCode = 1;
}
