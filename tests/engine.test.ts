import { beforeAll, describe, expect, it } from 'vitest';
import sharp from 'sharp';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { convertOne } from '../src/processing/engine';
import type { ConvertOptions } from '../src/shared/types';

let root = '';
const base: ConvertOptions = { format: 'webp', quality: 78, resizeMode: 'fit', width: 64, height: 64, withoutEnlargement: true, background: '#ffffff', preserveMetadata: false, suffix: '_converted', minQuality: 35, reduceForTarget: false };
async function readMetadata(file: string) { const image = sharp(file); try { return await image.metadata(); } finally { image.destroy(); } }
beforeAll(async () => { root = await fs.mkdtemp(path.join(os.tmpdir(), 'matin-converter-')); });

describe('image conversion engine', () => {
  it('converts PNG content to a real JPEG and limits dimensions', async () => {
    const input = path.join(root, 'ورودی.png');
    await sharp({ create: { width: 120, height: 80, channels: 4, background: { r: 240, g: 30, b: 80, alpha: .5 } } }).png().toFile(input);
    const result = await convertOne(input, { ...base, format: 'jpeg' });
    expect(result.output).toMatch(/ورودی_converted\.jpg$/);
    const metadata = await readMetadata(result.output!);
    expect(metadata.format).toBe('jpeg');
    expect(metadata.channels).toBe(3);
    expect(metadata.width).toBeLessThanOrEqual(64);
    expect(metadata.height).toBeLessThanOrEqual(64);
  });
  it('reports invalid image input without creating an output', async () => {
    const input = path.join(root, 'broken.webp'); await fs.writeFile(input, 'invalid image bytes');
    const result = await convertOne(input, base);
    expect(result.error).toBeTruthy();
    await expect(fs.stat(path.join(root, 'broken_converted.webp'))).rejects.toThrow();
  });
  it('keeps existing converted output and chooses a distinct name', async () => {
    const input = path.join(root, 'duplicate.png');
    await sharp({ create: { width: 12, height: 12, channels: 3, background: '#2c75d4' } }).png().toFile(input);
    const first = await convertOne(input, base); const second = await convertOne(input, base);
    expect(first.output).toBe(path.join(root, 'duplicate_converted.webp'));
    expect(second.output).toBe(path.join(root, 'duplicate_converted (2).webp'));
    expect((await readMetadata(second.output!)).format).toBe('webp');
  });
  it('writes valid AVIF data when AVIF is selected', async () => {
    const input = path.join(root, 'avif-source.png');
    await sharp({ create: { width: 24, height: 18, channels: 3, background: '#14a975' } }).png().toFile(input);
    const result = await convertOne(input, { ...base, format: 'avif' });
    expect(result.output).toMatch(/\.avif$/);
    expect((await readMetadata(result.output!)).format).toBe('heif');
  });
  it('accepts a .jfif source as JPEG input', async () => {
    const input = path.join(root, 'photo.jfif');
    await sharp({ create: { width: 22, height: 16, channels: 3, background: '#2288aa' } }).jpeg().toFile(input);
    const result = await convertOne(input, { ...base, format: 'png' });
    expect((await readMetadata(result.output!)).format).toBe('png');
  });
  it.each(['tiff','gif'] as const)('encodes %s output when selected', async format => {
    const input = path.join(root, `source-${format}.png`);
    await sharp({ create: { width: 20, height: 15, channels: 3, background: '#34a65c' } }).png().toFile(input);
    const result = await convertOne(input, { ...base, format });
    expect(result.output).toMatch(new RegExp(`\\.${format}$`));
    const metadata = await readMetadata(result.output!);
    expect(metadata.format).toBe(format === 'tiff' ? 'tiff' : 'gif');
  });
});
