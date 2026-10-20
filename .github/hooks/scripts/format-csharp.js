const { spawnSync } = require("node:child_process");
const path = require("node:path");

function isWriteTool(toolName) {
  if (typeof toolName !== "string") {
    return false;
  }

  const normalizedToolName = toolName.replace(/([a-z])([A-Z])/g, "$1_$2");
  return /(^|[_-])(edit|create|write|apply_patch)([_-]|$)/i.test(normalizedToolName);
}

function findCsharpPath(value, key = "") {
  if (typeof value === "string") {
    return /^(file|path|uri)/i.test(key) && /\.cs$/i.test(value.trim())
      ? value.trim()
      : undefined;
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      const filePath = findCsharpPath(item, key);
      if (filePath) {
        return filePath;
      }
    }
    return undefined;
  }

  if (value && typeof value === "object") {
    for (const [childKey, childValue] of Object.entries(value)) {
      const filePath = findCsharpPath(childValue, childKey);
      if (filePath) {
        return filePath;
      }
    }
  }
}

let input = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => {
  input += chunk;
});
process.stdin.on("error", () => {
  process.exit(0);
});
process.stdin.on("end", () => {
  try {
    const hookInput = JSON.parse(input);
    const filePath = findCsharpPath(hookInput);
    const toolName = hookInput.tool_name ?? hookInput.toolName;

    if (filePath && isWriteTool(toolName)) {
      const repositoryRoot = path.resolve(__dirname, "..", "..", "..");
      const relativePath = path
        .relative(repositoryRoot, path.resolve(filePath))
        .replace(/\\/g, "/");

      spawnSync(
        "dotnet",
        ["format", "whitespace", "backend/InvoiceApp.sln", "--include", relativePath],
        { timeout: 50000, stdio: "ignore" },
      );
    }
  } catch {
    // Formatting hooks must never interrupt the agent.
  }

  process.exitCode = 0;
});
