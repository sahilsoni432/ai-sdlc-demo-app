const fs = require("node:fs");
const path = require("node:path");
const { writeTranscript } = require("./transcript");

const maximumFieldLength = 2000;
const sensitiveFieldName = /token|password|secret|key/i;
const requestedEventName = process.argv[2];
let input = "";

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

  if (typeof value === "string" && value.length > maximumFieldLength) {
    return `${value.slice(0, maximumFieldLength)}…[truncated]`;
  }

  return value;
}

function appendRecord(record) {
  const workspaceRoot = path.resolve(__dirname, "..", "..", "..");
  const logDirectory = path.join(workspaceRoot, "ai-logs");
  const logFile = path.join(logDirectory, "ai-interactions.jsonl");

  fs.mkdirSync(logDirectory, { recursive: true });
  fs.appendFileSync(logFile, `${JSON.stringify(record)}\n`, "utf8");
}

function writeLog() {
  try {
    let parsedInput;

    try {
      parsedInput = JSON.parse(input);
    } catch {
      appendRecord({
        timestamp: new Date().toISOString(),
        event: sanitize(requestedEventName || "unknown"),
        parseError: true,
        rawInput: input.slice(0, 500),
      });
      process.exit(0);
      return;
    }

    const payload = sanitize(parsedInput);
    const inputFields =
      parsedInput !== null && typeof parsedInput === "object"
        ? parsedInput
        : {};
    const sessionId =
      inputFields.session_id ??
      inputFields.sessionId ??
      inputFields.sessionID;
    const eventName =
      requestedEventName ||
      inputFields.hook_event_name ||
      inputFields.hookEventName ||
      inputFields.event_name ||
      inputFields.eventName ||
      inputFields.event ||
      "unknown";
    const record = {
      timestamp: new Date().toISOString(),
      event: sanitize(eventName),
      ...(sessionId === undefined ? {} : { sessionId: sanitize(sessionId) }),
      payload,
    };

    appendRecord(record);
    if (
      eventName === "Stop" &&
      typeof inputFields.transcript_path === "string" &&
      fs.existsSync(inputFields.transcript_path)
    ) {
      writeTranscript(
        inputFields.transcript_path,
        sessionId,
        path.resolve(__dirname, "..", "..", "..")
      );
    }
  } catch {
    process.exit(0);
  }

  process.exit(0);
}

function exitSuccessfully() {
  process.exit(0);
}

process.on("uncaughtException", exitSuccessfully);
process.on("unhandledRejection", exitSuccessfully);

try {
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (chunk) => {
    input += chunk;
  });
  process.stdin.on("end", writeLog);
  process.stdin.on("error", exitSuccessfully);
} catch {
  exitSuccessfully();
}
