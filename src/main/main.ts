import { app, BrowserWindow, dialog, ipcMain, shell, Notification } from 'electron';
import path from 'node:path';
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import os from 'node:os';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import Store from 'electron-store';
import { z } from 'zod';
import { defaultSettings } from '../shared/defaults';
import type { AppSettings, ConvertOptions, ConvertProgress, ConvertRequest } from '../shared/types';
import { convertOne } from '../processing/engine';

const store = new Store<{ settings: AppSettings }>({ name: 'settings', defaults: { settings: defaultSettings } });
let mainWindow: BrowserWindow | null = null;
let progressWindow: BrowserWindow | null = null;
const cancelled = new Set<string>();
const execFileAsync = promisify(execFile);
const quickMenuExtensions = ['.jpg','.jpeg','.jpe','.jfif','.png','.webp','.avif','.tif','.tiff','.gif','.svg','.svgz'];
const optionsSchema = z.object({
  format: z.enum(['jpeg','png','webp','avif','tiff','gif']), quality: z.number().int().min(1).max(100),
  resizeMode: z.enum(['original','fit','cover','exact']), width: z.number().int().positive().max(100000).optional(), height: z.number().int().positive().max(100000).optional(),
  withoutEnlargement: z.boolean(), background: z.string().regex(/^#[0-9a-f]{6}$/i), preserveMetadata: z.boolean(), outputDir: z.string().max(32767).optional(),
  suffix: z.string().max(64), targetKB: z.number().positive().max(1024*1024).optional(), minQuality: z.number().int().min(1).max(100), reduceForTarget: z.boolean()
}).strict();
const settingsSchema = z.object({ theme: z.enum(['light','dark','system']), zoom: z.number().int().min(75).max(150), defaultFormat: z.enum(['jpeg','png','webp','avif','tiff','gif']), defaultQuality: z.number().int().min(1).max(100), defaultOutputDir: z.string().max(32767), autoOpenOutput: z.boolean(), showNotification: z.boolean(), quick: optionsSchema, shellEnabled: z.boolean() }).strict();
const validInput = (file: string) => typeof file === 'string' && path.isAbsolute(file) && /\.(jpe?g|jpe|jfif|png|webp|avif|tif|tiff|gif|svg|svgz)$/i.test(file);
function getSettings():AppSettings{const saved=store.get('settings');return {...defaultSettings,...saved,quick:{...defaultSettings.quick,...saved.quick}};}
function quickMenuKey(extension:string){return `HKCU\\Software\\Classes\\SystemFileAssociations\\${extension}\\shell\\MatinConverter`;}
async function registryEnabled(){try{await execFileAsync('reg.exe',['query',quickMenuKey('.jpg')],{windowsHide:true});return true;}catch{return false;}}
function menuAvailability(){return process.platform==='win32'&&app.isPackaged;}
function menuMessage(){const build=Number(os.release().split('.')[2]||0);return build>=22000?'منوی کلاسیک فعال می‌شود؛ در ویندوز ۱۱ آن را از «نمایش گزینه‌های بیشتر» باز کن.':'گزینه در منوی راست‌کلیک کلاسیک ویندوز ۱۰ نمایش داده می‌شود.';}
async function setQuickMenu(enabled:boolean){
  if(!menuAvailability())throw new Error('این اتصال پس از نصب نسخه‌ی ویندوزی برنامه قابل تنظیم است.');
  const executable=app.getPath('exe');
  if(enabled){
    const created:string[]=[];
    try{for(const extension of quickMenuExtensions){const key=quickMenuKey(extension);await execFileAsync('reg.exe',['add',key,'/ve','/t','REG_SZ','/d','Convert with Matin Converter','/f'],{windowsHide:true});await execFileAsync('reg.exe',['add',key,'/v','MultiSelectModel','/t','REG_SZ','/d','Player','/f'],{windowsHide:true});await execFileAsync('reg.exe',['add',`${key}\\command`,'/ve','/t','REG_SZ','/d',`"${executable}" --quick %*`,'/f'],{windowsHide:true});created.push(key);}}
    catch(error){for(const key of [...created,...quickMenuExtensions.map(quickMenuKey)])await execFileAsync('reg.exe',['delete',key,'/f'],{windowsHide:true}).catch(()=>{});throw error;}
  }else{for(const extension of quickMenuExtensions)await execFileAsync('reg.exe',['delete',quickMenuKey(extension),'/f'],{windowsHide:true}).catch(()=>{});}
}
async function mapLimit<T,R>(items:T[],limit:number,work:(item:T)=>Promise<R>):Promise<R[]>{const result: R[] = new Array(items.length);let next=0;await Promise.all(Array.from({length:Math.min(limit,items.length)},async()=>{while(true){const index=next++;if(index>=items.length)return;result[index]=await work(items[index]);}}));return result;}
function createProgress() {
  if (progressWindow && !progressWindow.isDestroyed()) return progressWindow;
  progressWindow = new BrowserWindow({ width: 380, height: 205, minWidth: 360, minHeight: 190, maxWidth: 440, maxHeight: 220, title: 'در حال تبدیل', parent: mainWindow ?? undefined, modal: !!mainWindow, show: false, resizable: false, autoHideMenuBar: true, backgroundColor: '#f7f8fb', webPreferences: { preload: path.join(__dirname, '../preload/index.js'), contextIsolation: true, nodeIntegration: false, sandbox: true } });
  progressWindow.once('ready-to-show', () => progressWindow?.show());
  if (app.isPackaged) void progressWindow.loadFile(path.join(__dirname, '../../renderer/index.html'), { hash: '/progress' }); else void progressWindow.loadURL('http://127.0.0.1:5173/#/progress');
  progressWindow.on('closed', () => { progressWindow = null; });
  return progressWindow;
}
function sendProgress(data: ConvertProgress) { for (const win of [mainWindow, progressWindow]) if (win && !win.isDestroyed()) win.webContents.send('convert:progress', data); }
async function runJob(files: string[], options: ConvertOptions) {
  const jobId = crypto.randomUUID(); cancelled.delete(jobId); const accepted = files.filter(validInput);
  const progress = createProgress(); void progress;
  sendProgress({ jobId, current: 0, total: accepted.length, file: '', status: 'started' });
  const results = [];
  for (let i = 0; i < accepted.length; i++) {
    if (cancelled.has(jobId)) { sendProgress({ jobId, current: i, total: accepted.length, file: accepted[i], status: 'cancelled' }); break; }
    const result = await convertOne(accepted[i], options); results.push(result);
    sendProgress({ jobId, current: i + 1, total: accepted.length, file: accepted[i], status: result.error ? 'error' : 'file-done', result });
  }
  if (!cancelled.has(jobId)) {
    sendProgress({ jobId, current: accepted.length, total: accepted.length, file: '', status: 'done' });
    const success = results.filter(r => r.output).length;
    const settings = getSettings();
    if (settings.showNotification && Notification.isSupported()) new Notification({ title: 'Matin Converter', body: `${success} از ${accepted.length} تصویر تبدیل شد${results.some(r => r.error) ? '؛ برخی فایل‌ها خطا داشتند' : ''}` }).show();
    if (settings.autoOpenOutput) {
      const folder = options.outputDir || results.find(r => r.output)?.output && path.dirname(results.find(r => r.output)!.output!);
      if (folder) await shell.openPath(folder);
    }
  }
  return { jobId, results };
}
function createMain() {
  mainWindow = new BrowserWindow({ width: 1120, height: 760, minWidth: 900, minHeight: 650, show: false, autoHideMenuBar: true, backgroundColor: '#f4f6fa', title: 'Matin Converter', webPreferences: { preload: path.join(__dirname, '../preload/index.js'), contextIsolation: true, nodeIntegration: false, sandbox: true } });
  mainWindow.webContents.setZoomFactor(getSettings().zoom / 100);
  mainWindow.once('ready-to-show', () => mainWindow?.show());
  mainWindow.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  mainWindow.webContents.on('will-navigate', (event, url) => { const allowed = app.isPackaged ? url.startsWith('file://') : url.startsWith('http://127.0.0.1:5173'); if (!allowed) event.preventDefault(); });
  if (app.isPackaged) void mainWindow.loadFile(path.join(__dirname, '../../renderer/index.html')); else void mainWindow.loadURL('http://127.0.0.1:5173');
  mainWindow.on('closed', () => { mainWindow = null; });
}
const lock = app.requestSingleInstanceLock();
if (!lock) app.quit(); else {
  app.on('second-instance', (_event, argv) => { const files = argv.filter(validInput); if (files.length) void runJob(files, getSettings().quick); else if (mainWindow) { if (mainWindow.isMinimized()) mainWindow.restore(); mainWindow.focus(); } });
  app.whenReady().then(() => {
    ipcMain.handle('settings:get', () => getSettings());
    ipcMain.handle('settings:set', (_e, settings: AppSettings) => { const checked = settingsSchema.parse(settings); store.set('settings', checked); if(mainWindow&&!mainWindow.isDestroyed())mainWindow.webContents.setZoomFactor(checked.zoom/100); return true; });
    ipcMain.handle('files:choose', async () => (await dialog.showOpenDialog({ properties: ['openFile', 'multiSelections'], filters: [{ name: 'فرمت‌های تصویر پشتیبانی‌شده', extensions: ['jpg','jpeg','jpe','jfif','png','webp','avif','tif','tiff','gif','svg','svgz'] }, { name: 'همه فایل‌ها', extensions: ['*'] }] })).filePaths);
    ipcMain.handle('folder:choose', async () => (await dialog.showOpenDialog({ properties: ['openDirectory', 'createDirectory'] })).filePaths[0] || '');
    ipcMain.handle('files:inspect', async (_e, files: string[]) => mapLimit([...new Set(files.filter(file => typeof file === 'string' && path.isAbsolute(file)))].slice(0,200),4,async file => { if(!validInput(file)) return { path: file, name: path.basename(file), error: 'این نوع فایل برای تبدیل تصویر پشتیبانی نمی‌شود.' }; try { const sharp = (await import('sharp')).default; const [stat, meta] = await Promise.all([fs.stat(file), sharp(file, { limitInputPixels: 268402689 }).metadata()]); const thumb = await sharp(file, { limitInputPixels: 268402689 }).rotate().resize(72,72,{fit:'inside',withoutEnlargement:true}).jpeg({quality:65}).toBuffer(); return { path: file, name: path.basename(file), size: stat.size, width: meta.width, height: meta.height, format: meta.format, thumbnail: `data:image/jpeg;base64,${thumb.toString('base64')}` }; } catch { return { path: file, name: path.basename(file), error: 'فایل تصویر معتبر یا قابل خواندن نیست' }; } }));
    ipcMain.handle('convert:start', async (_e, req: ConvertRequest) => { const checked = z.object({ files: z.array(z.string().max(32767)).max(1000), options: optionsSchema }).strict().parse(req); return runJob(checked.files, checked.options); });
    ipcMain.on('convert:cancel', (_e, jobId: string) => cancelled.add(jobId));
    ipcMain.on('progress:close', () => { if (progressWindow && !progressWindow.isDestroyed()) progressWindow.close(); });
    ipcMain.handle('output:open', (_e, folder: string) => shell.openPath(folder));
    ipcMain.handle('integration:status', async () => ({ available: menuAvailability(), enabled: menuAvailability() && await registryEnabled(), message: menuAvailability() ? menuMessage() : 'فعال‌سازی منوی کلاسیک پس از نصب نسخه‌ی ویندوزی در دسترس است.' }));
    ipcMain.handle('integration:set', async (_e, enabled:boolean) => { try{await setQuickMenu(Boolean(enabled));store.set('settings',{...getSettings(),shellEnabled:Boolean(enabled)});return {ok:true,enabled:Boolean(enabled),available:true,message:menuMessage()};}catch(error){return {ok:false,enabled:await registryEnabled(),available:menuAvailability(),message:error instanceof Error?error.message:'ثبت منوی راست‌کلیک انجام نشد.'};} });
    const initialFiles = process.argv.filter(validInput);
    if (initialFiles.length) void runJob(initialFiles, getSettings().quick); else createMain();
  });
  app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
}
