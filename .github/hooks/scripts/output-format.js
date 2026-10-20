const fs = require("node:fs");
const path = require("node:path");

const templatesPath = path.join(__dirname, "..", "templates", "output-formats.json");

let input = "";

function exitSuccessfully() {
  process.exit(0);
}

process.on("uncaughtException", exitSuccessfully);
process.on("unhandledRejection", exitSuccessfully);

process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => {
  input += chunk;
});
process.stdin.on("error", exitSuccessfully);
process.stdin.on("end", () => {
  try {
    const hookInput = JSON.parse(input);
    const templates = JSON.parse(fs.readFileSync(templatesPath, "utf8"));
    const agentName =
      hookInput.agent_type ??
      hookInput.agentName ??
      hookInput.agent_name ??
      hookInput.agentType;
    const template = templates[agentName]?.format ?? templates[agentName];

    if (typeof template !== "string") {
      process.exitCode = 0;
      return;
    }

    const additionalContext = `you MUST answer using exactly this structure:\n\n${template}`;
    process.stdout.write(JSON.stringify({
      additionalContext,
      hookSpecificOutput: {
        hookEventName: "SubagentStart",
        additionalContext,
      },
    }));
  } catch {
    exitSuccessfully();
  }
});
