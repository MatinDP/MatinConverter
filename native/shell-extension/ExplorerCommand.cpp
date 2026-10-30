#include <windows.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <string>
#include <vector>
#include <algorithm>
#include <cwctype>
#include <iterator>
#include <new>

namespace {
constexpr CLSID CLSID_MatinConvert = {0x7F036A93,0x6017,0x4F13,{0xA8,0x5B,0xA2,0xCA,0x2D,0x53,0x8D,0x41}};
HINSTANCE g_module{};
long g_objects{};
const wchar_t* kExtensions[] = {L".jpg",L".jpeg",L".jpe",L".jfif",L".png",L".webp",L".avif",L".tif",L".tiff",L".gif",L".svg",L".svgz"};

bool Supported(IShellItem* item) {
  if (!item) return false;
  SFGAOF attrs = 0;
  if (FAILED(item->GetAttributes(SFGAO_FOLDER, &attrs)) || (attrs & SFGAO_FOLDER)) return false;
  PWSTR raw = nullptr;
  if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &raw)) || !raw) return false;
  std::wstring path(raw); CoTaskMemFree(raw);
  const auto dot = path.find_last_of(L'.');
  if (dot == std::wstring::npos) return false;
  std::wstring ext = path.substr(dot);
  std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t c){ return static_cast<wchar_t>(std::towlower(c)); });
  return std::any_of(std::begin(kExtensions), std::end(kExtensions), [&](const wchar_t* e){ return ext == e; });
}

std::wstring Quote(const std::wstring& value) {
  std::wstring out = L"\""; size_t slashes = 0;
  for (wchar_t c : value) {
    if (c == L'\\') { ++slashes; continue; }
    if (c == L'\"') { out.append(slashes * 2 + 1, L'\\'); out += c; slashes = 0; continue; }
    out.append(slashes, L'\\'); slashes = 0; out += c;
  }
  out.append(slashes * 2, L'\\'); out += L'\"'; return out;
}

HRESULT SelectedPaths(IShellItemArray* items, std::vector<std::wstring>& paths) {
  if (!items) return E_INVALIDARG;
  DWORD count = 0; HRESULT hr = items->GetCount(&count); if (FAILED(hr)) return hr;
  for (DWORD i=0; i<count; ++i) {
    IShellItem* item = nullptr; if (FAILED(items->GetItemAt(i, &item))) continue;
    if (Supported(item)) { PWSTR raw = nullptr; if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &raw)) && raw) { paths.emplace_back(raw); CoTaskMemFree(raw); } }
    item->Release();
  }
  return paths.empty() ? HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED) : S_OK;
}

HRESULT Launch(const std::vector<std::wstring>& paths) {
  wchar_t module[MAX_PATH]{}; DWORD len = GetModuleFileNameW(g_module, module, MAX_PATH);
  if (!len || len >= MAX_PATH) return HRESULT_FROM_WIN32(GetLastError());
  std::wstring exe(module); const auto slash = exe.find_last_of(L"\\/");
  if (slash == std::wstring::npos) return E_FAIL;
  exe.resize(slash + 1); exe += L"Matin Converter.exe";
  std::wstring cmd = Quote(exe);
  for (const auto& file : paths) { cmd += L" "; cmd += Quote(file); }
  std::vector<wchar_t> mutableCmd(cmd.begin(), cmd.end()); mutableCmd.push_back(L'\0');
  STARTUPINFOW si{}; si.cb = sizeof(si); PROCESS_INFORMATION pi{};
  if (!CreateProcessW(exe.c_str(), mutableCmd.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) return HRESULT_FROM_WIN32(GetLastError());
  CloseHandle(pi.hThread); CloseHandle(pi.hProcess); return S_OK;
}

class Command final : public IExplorerCommand {
  volatile long refs_{1};
public:
  Command(){ InterlockedIncrement(&g_objects); }
  ~Command(){ InterlockedDecrement(&g_objects); }
  IFACEMETHODIMP QueryInterface(REFIID iid, void** out) override { if(!out)return E_POINTER; *out=nullptr; if(IsEqualIID(iid,IID_IUnknown)||IsEqualIID(iid,IID_IExplorerCommand)){*out=static_cast<IExplorerCommand*>(this);AddRef();return S_OK;} return E_NOINTERFACE; }
  IFACEMETHODIMP_(ULONG) AddRef() override{return InterlockedIncrement(&refs_);}
  IFACEMETHODIMP_(ULONG) Release() override{ULONG n=InterlockedDecrement(&refs_);if(!n)delete this;return n;}
  IFACEMETHODIMP GetTitle(IShellItemArray*,PWSTR* title) override{return SHStrDupW(L"Convert with Matin Converter",title);}
  IFACEMETHODIMP GetIcon(IShellItemArray*,PWSTR* icon) override { if(!icon)return E_POINTER; wchar_t path[MAX_PATH]{};GetModuleFileNameW(g_module,path,MAX_PATH);std::wstring value=path;value+=L",-101";return SHStrDupW(value.c_str(),icon); }
  IFACEMETHODIMP GetToolTip(IShellItemArray*,PWSTR* tip) override{return SHStrDupW(L"Convert selected images with Matin Converter",tip);}
  IFACEMETHODIMP GetCanonicalName(GUID* name) override{if(!name)return E_POINTER;*name=CLSID_MatinConvert;return S_OK;}
  IFACEMETHODIMP GetState(IShellItemArray* items,BOOL,EXPCMDSTATE* state) override { if(!state)return E_POINTER;*state=ECS_HIDDEN;if(!items)return S_OK;DWORD n=0;if(FAILED(items->GetCount(&n)))return S_OK;for(DWORD i=0;i<n;++i){IShellItem* item=nullptr;if(SUCCEEDED(items->GetItemAt(i,&item))){bool ok=Supported(item);item->Release();if(ok){*state=ECS_ENABLED;break;}}}return S_OK; }
  IFACEMETHODIMP Invoke(IShellItemArray* items,IBindCtx*) override{std::vector<std::wstring> paths;HRESULT hr=SelectedPaths(items,paths);return FAILED(hr)?hr:Launch(paths);}
  IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override{if(!flags)return E_POINTER;*flags=ECF_DEFAULT;return S_OK;}
  IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override{if(!commands)return E_POINTER;*commands=nullptr;return E_NOTIMPL;}
};

class Factory final : public IClassFactory {
  volatile long refs_{1};
public:
  Factory(){InterlockedIncrement(&g_objects);} ~Factory(){InterlockedDecrement(&g_objects);}
  IFACEMETHODIMP QueryInterface(REFIID iid,void** out) override{if(!out)return E_POINTER;*out=nullptr;if(IsEqualIID(iid,IID_IUnknown)||IsEqualIID(iid,IID_IClassFactory)){*out=static_cast<IClassFactory*>(this);AddRef();return S_OK;}return E_NOINTERFACE;}
  IFACEMETHODIMP_(ULONG) AddRef() override{return InterlockedIncrement(&refs_);}
  IFACEMETHODIMP_(ULONG) Release() override{ULONG n=InterlockedDecrement(&refs_);if(!n)delete this;return n;}
  IFACEMETHODIMP CreateInstance(IUnknown* outer,REFIID iid,void** out) override{if(outer)return CLASS_E_NOAGGREGATION;auto* command=new(std::nothrow) Command();if(!command)return E_OUTOFMEMORY;HRESULT hr=command->QueryInterface(iid,out);command->Release();return hr;}
  IFACEMETHODIMP LockServer(BOOL) override{return S_OK;}
};
}

BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){g_module=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
extern "C" __declspec(dllexport) HRESULT __stdcall DllGetClassObject(REFCLSID clsid,REFIID iid,void** out){if(!IsEqualCLSID(clsid,CLSID_MatinConvert))return CLASS_E_CLASSNOTAVAILABLE;auto* factory=new(std::nothrow) Factory();if(!factory)return E_OUTOFMEMORY;HRESULT hr=factory->QueryInterface(iid,out);factory->Release();return hr;}
extern "C" __declspec(dllexport) HRESULT __stdcall DllCanUnloadNow(){return g_objects==0?S_OK:S_FALSE;}
