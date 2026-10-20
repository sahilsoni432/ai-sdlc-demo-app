const fs = require("node:fs");
const path = require("node:path");

const maximumEntryLength = 2000;
const sensitiveFieldName = /token|password|secret|key/i;
const transcriptEventTypes = new Set([
  "user.message",
  "assistant.message",
  "tool.execution_start",
  "tool.execution_complete",
  "permission.requested",
  "permission.completed",
]);

function sanitize(value) {
  if (Array.isArray(value)) {
    return value.map(sanitize);
  }

  if (value !== null && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([fieldName, fieldValue]) => [
        fieldName,
        sensitiveFieldName.test(fieldName)
          ? "[REDACTED]"
          : sanitize(fieldValue),
      ])
    );
  }

  if (typeof value === "string" && value.length > maximumEntryLength) {
    return `${value.slice(0, maximumEntryLength)}…[truncated]`;
  }

  return value;
}

function truncateEntry(entry) {
  if (entry.length <= maximumEntryLength) {
    return entry;
  }

  return `${entry.slice(0, maximumEntryLength)}…[truncated]`;
}

function formatEvent(event, toolNames) {
  const data = sanitize(event.data || {});
  const lines = [`### ${event.timestamp || "Unknown timestamp"} — ${event.type}`];

  if (event.type === "user.message" || event.type === "assistant.message") {
    lines.push("", data.content || "");
  } else if (
    event.type === "tool.execution_start" ||
    event.type === "tool.execution_complete"
  ) {
    const toolCallId = data.toolCallId;
    const toolName =
      data.toolName || (toolCallId && toolNames.get(toolCallId)) || "Unknown tool";
    lines.push("", `Tool: ${toolName}`);

    if (event.type === "tool.execution_start" && data.arguments !== undefined) {
      lines.push(`Arguments: \`${JSON.stringify(data.arguments)}\``);
    } else if (event.type === "tool.execution_complete") {
      lines.push(`Success: ${data.success === true ? "yes" : "no"}`);
    }
  } else if (event.type === "permission.requested") {
    const permission = data.permissionRequest || data.promptRequest || {};
    lines.push(
      "",
      `Permission requested: ${permission.kind || "unspecified"}`,
      `Intention: ${permission.intention || "unspecified"}`
    );
  } else if (event.type === "permission.completed") {
    lines.push(
      "",
      `Permission decision: ${data.result?.kind || "unspecified"}`,
      `Decision source: ${data.decisionSource || "unspecified"}`
    );
  }

  return truncateEntry(lines.join("\n"));
}

function getTranscriptDate(events) {
  const firstTimestamp = events.find(
    (event) =>
      typeof event.timestamp === "string" &&
      !Number.isNaN(Date.parse(event.timestamp))
  )?.timestamp;

  return firstTimestamp
    ? new Date(firstTimestamp).toISOString().slice(0, 10)
    : new Date().toISOString().slice(0, 10);
}

function writeTranscript(transcriptPath, sessionId, workspaceRoot) {
  const contents = fs.readFileSync(transcriptPath, "utf8");
  const events = [];

  for (const [index, line] of contents.split(/\r?\n/).entries()) {
    if (!line.trim()) {
      continue;
    }

    let event;
    try {
      event = JSON.parse(line);
    } catch (error) {
      throw new Error(`Invalid JSONL at line ${index + 1} in ${transcriptPath}`, {
        cause: error,
      });
    }

    if (transcriptEventTypes.has(event.type)) {
      events.push(event);
    }
  }

  const resolvedSessionId =
    sessionId ||
    events.find((event) => typeof event.data?.sessionId === "string")?.data.sessionId;
  if (
    typeof resolvedSessionId !== "string" ||
    !/^[A-Za-z0-9._-]+$/.test(resolvedSessionId)
  ) {
    throw new Error("A valid session ID is required to write a transcript");
  }

  const toolNames = new Map();
  for (const event of events) {
    if (
      event.type === "tool.execution_start" &&
      typeof event.data?.toolCallId === "string" &&
      typeof event.data?.toolName === "string"
    ) {
      toolNames.set(event.data.toolCallId, sanitize(event.data.toolName));
    }
  }

  const transcript = events
    .map((event) => formatEvent(event, toolNames))
    .join("\n\n");
  const transcriptDirectory = path.join(workspaceRoot, "ai-logs", "transcripts");
  const outputPath = path.join(
    transcriptDirectory,
    `${getTranscriptDate(events)}-${resolvedSessionId}.md`
  );

  fs.mkdirSync(transcriptDirectory, { recursive: true });
  fs.writeFileSync(outputPath, `${transcript}\n`, "utf8");
  return outputPath;
}

module.exports = { writeTranscript };
