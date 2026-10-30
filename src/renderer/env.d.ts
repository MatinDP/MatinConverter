import type { AppSettings, ConvertProgress, ConvertRequest } from '../shared/types';
declare global {
  interface Window {
    matin: {
      getSettings(): Promise<AppSettings>; setSettings(s: AppSettings): Promise<boolean>;
      chooseFiles(): Promise<string[]>; chooseFolder(): Promise<string>; inspectFiles(f: string[]): Promise<any[]>;
      convert(r: ConvertRequest): Promise<any>; cancel(id: string): void; closeProgress(): void;
      openFolder(f: string): Promise<void>; integrationStatus(): Promise<any>; setIntegration(e: boolean): Promise<any>;
      onProgress(cb: (p: ConvertProgress) => void): () => void;
    };
  }
}
export {};
