import sharp from 'sharp';
import path from 'node:path';
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import type { ConvertOptions, ConvertResult } from '../shared/types';

const ext: Record<ConvertOptions['format'], string> = { jpeg: '.jpg', png: '.png', webp: '.webp', avif: '.avif', tiff: '.tiff', gif: '.gif' };
function outputName(input: string, options: ConvertOptions, outputDir?: string, index = 1) {
  const parsed = path.parse(input); const dir = outputDir || options.outputDir || parsed.dir;
  const suffix = (options.suffix?.trim() || '_converted').replace(/[<>:"/\\|?*\x00-\x1f]/g, '_').replace(/[. ]+$/g, '') || '_converted';
  const duplicate = index > 1 ? ` (${index})` : '';
  return path.join(dir, `${parsed.name}${suffix}${duplicate}${ext[options.format]}`);
}
async function encode(input: string, options: ConvertOptions, quality: number) {
  let image = sharp(input, { failOn: 'error', animated: false }).rotate();
  if (options.resizeMode !== 'original' && options.width && options.height) {
    image = image.resize(options.width, options.height, { fit: options.resizeMode === 'cover' ? 'cover' : options.resizeMode === 'exact' ? 'fill' : 'inside', withoutEnlargement: options.withoutEnlargement && options.resizeMode !== 'exact' });
  }
  if (options.format === 'jpeg') image = image.flatten({ background: options.background || '#ffffff' }).jpeg({ quality, mozjpeg: true });
  else if (options.format === 'png') image = image.png({ compressionLevel: 9, palette: false });
  else if (options.format === 'webp') image = image.webp({ quality, effort: 4 });
  else if (options.format === 'avif') image = image.avif({ quality, effort: 4 });
  else if (options.format === 'tiff') image = image.tiff({ quality, compression: 'jpeg' });
  else image = image.gif({ colors: Math.round(2 + (quality / 100) * 254), effort: 7 });
  if (options.preserveMetadata) image = image.withMetadata({ orientation: 1 });
  return image.toBuffer({ resolveWithObject: true });
}
export async function convertOne(input: string, options: ConvertOptions): Promise<ConvertResult> {
  const dir = options.outputDir || path.dirname(input);
  const temp = path.join(dir, `.${path.basename(input)}.${crypto.randomUUID()}.tmp`);
  try {
    await fs.mkdir(dir, { recursive: true });
    const sourceMeta = await sharp(input, { failOn: 'error', animated: false }).metadata();
    if ((sourceMeta.pages || 1) > 1) return { input, error: 'تصویر چندفریمی/متحرک است؛ برای جلوگیری از حذف ناخواسته‌ی فریم‌ها تبدیل نشد.' };
    let quality = Math.max(1, Math.min(100, options.quality));
    let encoded = await encode(input, options, quality);
    const target = options.targetKB ? options.targetKB * 1024 : 0;
    if (target && options.format !== 'png' && encoded.data.length > target) {
      let low = options.minQuality, high = quality, best: typeof encoded | undefined;
      for (let i = 0; i < 7 && low <= high; i++) {
        const mid = Math.floor((low + high) / 2); const candidate = await encode(input, options, mid);
        if (candidate.data.length <= target) { best = candidate; low = mid + 1; } else high = mid - 1;
      }
      if (best) encoded = best;
      else if (options.reduceForTarget && options.width && options.height) {
        let w = options.width, h = options.height;
        for (let i = 0; i < 3 && encoded.data.length > target; i++) { w = Math.round(w * .82); h = Math.round(h * .82); encoded = await encode(input, { ...options, width: w, height: h }, options.minQuality); }
      }
    }
    await fs.writeFile(temp, encoded.data, { flag: 'wx' });
    let output = '';
    for (let i=1; i<=1000; i++) {
      const candidate = outputName(input, options, dir, i);
      try { await fs.link(temp, candidate); output = candidate; break; }
      catch (e) { if ((e as NodeJS.ErrnoException).code !== 'EEXIST') throw e; }
    }
    if (!output) throw new Error('برای خروجی نام خالی پیدا نشد.');
    await fs.rm(temp, { force: true });
    return { input, output, size: encoded.data.length, width: encoded.info.width, height: encoded.info.height, ...(target && encoded.data.length > target ? { error: `به حجم هدف ${options.targetKB} کیلوبایت نرسید.` } : {}) };
  } catch (e) {
    await fs.rm(temp, { force: true }).catch(() => {});
    return { input, error: e instanceof Error ? e.message : 'خطای ناشناخته در تبدیل تصویر' };
  }
}
