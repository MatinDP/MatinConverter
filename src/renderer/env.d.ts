import type { AppSettings, ConvertProgress, ConvertRequest, UpdateStatus } from '../shared/types';
declare global {
  interface Window {
    matin: {
      getSettings(): Promise<AppSettings>; setSettings(s: AppSettings): Promise<boolean>;
      chooseFiles(): Promise<string[]>; chooseFolder(): Promise<string>; inspectFiles(f: string[]): Promise<any[]>;
      convert(r: ConvertRequest): Promise<any>; cancel(id: string): void; closeProgress(): void;
      openFolder(f: string): Promise<void>; integrationStatus(): Promise<any>; setIntegration(e: boolean): Promise<any>;
      checkAndInstallUpdate():Promise<UpdateStatus>; onUpdateStatus(cb:(status:UpdateStatus)=>void):()=>void;
      onProgress(cb: (p: ConvertProgress) => void): () => void;
    };
  }
}
export {};
