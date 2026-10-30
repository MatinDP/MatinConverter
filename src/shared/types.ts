export type OutputFormat = 'jpeg' | 'png' | 'webp' | 'avif' | 'tiff' | 'gif';
export type ResizeMode = 'original' | 'fit' | 'cover' | 'exact';
export interface ConvertOptions {
  format: OutputFormat; quality: number; resizeMode: ResizeMode; width?: number; height?: number;
  withoutEnlargement: boolean; background: string; preserveMetadata: boolean; outputDir?: string;
  suffix: string; targetKB?: number; minQuality: number; reduceForTarget: boolean;
}
export interface AppSettings {
  theme: 'light' | 'dark' | 'system'; zoom: number; defaultFormat: OutputFormat; defaultQuality: number;
  defaultOutputDir: string; autoOpenOutput: boolean; showNotification: boolean;
  quick: ConvertOptions; shellEnabled: boolean;
}
export interface ConvertResult { input: string; output?: string; size?: number; width?: number; height?: number; error?: string; }
export interface ConvertProgress { jobId: string; current: number; total: number; file: string; status: 'started'|'file-done'|'done'|'error'|'cancelled'; result?: ConvertResult; }
export interface ConvertRequest { files: string[]; options: ConvertOptions; }
export interface UpdateStatus { state: 'checking'|'downloading'|'installing'|'current'|'error'|'unavailable'; message: string; percent?: number; }
