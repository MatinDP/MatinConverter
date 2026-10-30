import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import sharp from 'sharp';

const app = path.resolve('release/win-unpacked/Matin Converter.exe');
const dir = await fs.mkdtemp(path.join(os.tmpdir(), 'matin-smoke-'));
const input = path.join(dir, 'ورودی.png');
const output = path.join(dir, 'ورودی_converted.jpg');
await sharp({ create: { width: 80, height: 48, channels: 3, background: '#d33' } }).png().toFile(input);
const child = spawn(app, [input], { detached: true, stdio: 'ignore' });
try {
  let exists = false;
  for (let i = 0; i < 40; i++) {
    try { await fs.access(output); exists = true; break; } catch { await delay(500); }
  }
  if (!exists) throw new Error('Packaged Quick Convert did not create its expected output.');
  const metadata = await sharp(output).metadata();
  if (metadata.format !== 'jpeg' || metadata.width !== 80 || metadata.height !== 48) throw new Error(`Unexpected output metadata: ${JSON.stringify(metadata)}`);
  console.log(JSON.stringify({ launched: true, input: path.basename(input), output: path.basename(output), format: metadata.format, width: metadata.width, height: metadata.height }));
} finally {
  child.kill();
  await fs.rm(dir, { recursive: true, force: true }).catch(() => {});
}
