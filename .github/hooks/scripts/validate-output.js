const fs = require("node:fs");
const path = require("node:path");

const templatesPath = path.join(__dirname, "..", "templates", "output-formats.json");
let input = "";

function exitSuccessfully() {
  process.exitCode = 0;
}

function normalizeLabel(value) {
  return value
    .replace(/\[([^\]]+)\]\([^)]+\)/g, "$1")
    .replace(/\|/g, " ")
    .replace(/^\s{0,3}#{1,6}\s*/gm, "")
    .replace(/^\s*(?:[-+*]\s+|\d+[.)]\s+)/gm, "")
    .replace(/[*_~`]/g, "")
    .replace(/\s+/g, " ")
    .trim()
    .replace(/:\s*$/, "")
    .toLowerCase();
}

function containsRequiredLabel(message, requiredLabel) {
  const normalizedRequiredLabel = normalizeLabel(requiredLabel);
  if (!normalizedRequiredLabel) {
    return true;
  }

  return message.split(/\r?\n/).some((line) => {
    const normalizedLine = normalizeLabel(line);
    return (
      normalizedLine === normalizedRequiredLabel ||
      normalizedLine.startsWith(`${normalizedRequiredLabel}:`)
    );
  });
}

process.on("uncaughtException", () => process.exit(0));
process.on("unhandledRejection", () => process.exit(0));

try {
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (chunk) => {
    input += chunk;
  });
  process.stdin.on("error", exitSuccessfully);
  process.stdin.on("end", () => {
    try {
      const hookInput = JSON.parse(input);
      const agentName =
        hookInput.agent_type ??
        hookInput.agentName ??
        hookInput.agent_name ??
        hookInput.agentType;
      const assistantMessage =
        hookInput.last_assistant_message ?? hookInput.lastAssistantMessage;

      if (
        (hookInput.stop_hook_active ?? hookInput.stopHookActive) === true ||
        typeof agentName !== "string" ||
        typeof assistantMessage !== "string"
      ) {
        exitSuccessfully();
        return;
      }

      const templates = JSON.parse(fs.readFileSync(templatesPath, "utf8"));
      const required = templates[agentName]?.required;

      if (!Array.isArray(required)) {
        exitSuccessfully();
        return;
      }

      const missing = required.filter(
        (heading) =>
          typeof heading !== "string" ||
          !containsRequiredLabel(assistantMessage, heading)
      );
      if (missing.length > 0) {
        const reason = `Missing required sections: ${missing.join(", ")}. Rewrite your final answer using the required format.`;
        process.stdout.write(JSON.stringify({ decision: "block", reason }));
      }
    } catch {
      exitSuccessfully();
      return;
    }

    exitSuccessfully();
  });
} catch {
  exitSuccessfully();
}
