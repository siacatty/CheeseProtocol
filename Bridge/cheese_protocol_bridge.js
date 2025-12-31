import fs from "fs";
import path from "path";
import os from "os";
import express from "express";

const app = express();
app.use(express.json());

// Default output: OS user-data folder (no hardcoding)
function defaultOutPath() {
  const base =
    process.env.APPDATA ||
    (process.platform === "darwin"
      ? path.join(os.homedir(), "Library", "Application Support")
      : path.join(os.homedir(), ".config"));

  const dir = path.join(base, "CheeseProtocol");
  fs.mkdirSync(dir, { recursive: true });
  return path.join(dir, "donations.ndjson");
}

// Override: CHEESE_OUT (absolute or relative)
const OUT_PATH = process.env.CHEESE_OUT
  ? path.resolve(process.env.CHEESE_OUT)
  : defaultOutPath();

function append(evt) {
  fs.appendFileSync(OUT_PATH, JSON.stringify(evt) + "\n", "utf8");
}

app.post("/test-donation", (req, res) => {
  const { donor, amount, message } = req.body || {};
  const evt = {
    donor: donor || "TestDonor",
    amount: Number(amount) || 1000,
    message: message || "test"
  };
  append(evt);
  res.json({ ok: true, out: OUT_PATH, evt });
});

const port = 17523;
app.listen(port, () => {
  console.log("Cheese Protocol bridge listening on", port);
  console.log("Writing to", OUT_PATH);
  console.log("Tip: set CHEESE_OUT to match the mod's Default path shown in Mod Settings.");
});
