import { contextBridge, ipcRenderer } from 'electron';
import type { AppSettings, ConvertOptions, ConvertProgress, ConvertRequest, UpdateStatus } from '../shared/types';
const api = {
  getSettings: () => ipcRenderer.invoke('settings:get') as Promise<AppSettings>,
  setSettings: (settings: AppSettings) => ipcRenderer.invoke('settings:set', settings) as Promise<boolean>,
  chooseFiles: () => ipcRenderer.invoke('files:choose') as Promise<string[]>,
  chooseFolder: () => ipcRenderer.invoke('folder:choose') as Promise<string>,
  inspectFiles: (files: string[]) => ipcRenderer.invoke('files:inspect', files),
  convert: (req: ConvertRequest) => ipcRenderer.invoke('convert:start', req),
  cancel: (jobId: string) => ipcRenderer.send('convert:cancel', jobId),
  closeProgress: () => ipcRenderer.send('progress:close'),
  openFolder: (folder: string) => ipcRenderer.invoke('output:open', folder),
  integrationStatus: () => ipcRenderer.invoke('integration:status'),
  setIntegration: (enabled: boolean) => ipcRenderer.invoke('integration:set', enabled),
  checkAndInstallUpdate: () => ipcRenderer.invoke('updates:check-install') as Promise<UpdateStatus>,
  onUpdateStatus: (cb: (status:UpdateStatus)=>void) => { const listener=(_:Electron.IpcRendererEvent,status:UpdateStatus)=>cb(status);ipcRenderer.on('updates:status',listener);return()=>ipcRenderer.removeListener('updates:status',listener); },
  onProgress: (cb: (progress: ConvertProgress) => void) => { const listener = (_: Electron.IpcRendererEvent, data: ConvertProgress) => cb(data); ipcRenderer.on('convert:progress', listener); return () => ipcRenderer.removeListener('convert:progress', listener); }
};
contextBridge.exposeInMainWorld('matin', api);
