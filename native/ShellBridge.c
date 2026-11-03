#define COBJMACROS
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <strsafe.h>

/* Only selection transport lives in Explorer. The .NET worker owns all processing. */
static const GUID CLSID_Matin = {0x8b71e20d,0x9e6a,0x4936,{0x90,0x65,0x2d,0x7e,0x63,0x93,0x7b,0xf0}};
static const GUID IID_Command = {0xa08ce4d0,0xfa25,0x44ab,{0xb5,0x7c,0xc7,0xb1,0xc3,0x23,0xe0,0xb9}};
static HMODULE module;
static LONG objects, locks;
typedef struct { IExplorerCommand iface; LONG references; } Command;
typedef struct { IClassFactory iface; LONG references; } Factory;

static HRESULT copy_string(LPCWSTR text,LPWSTR *result) {
    if(!result)return E_POINTER; SIZE_T bytes=(wcslen(text)+1)*sizeof(WCHAR);
    *result=(LPWSTR)CoTaskMemAlloc(bytes); if(!*result)return E_OUTOFMEMORY; CopyMemory(*result,text,bytes);return S_OK;
}
static HRESULT executable(WCHAR *path,SIZE_T capacity) {
    DWORD length=GetModuleFileNameW(module,path,(DWORD)capacity);if(!length||length>=capacity)return E_FAIL;
    WCHAR *slash=wcsrchr(path,L'\\');if(!slash)return E_FAIL;slash[1]=0;return StringCchCatW(path,capacity,L"MatinConverter.exe");
}
static HRESULT STDMETHODCALLTYPE command_query(IExplorerCommand *self,REFIID iid,void **result) {
    if(!result)return E_POINTER;*result=NULL;
    if(IsEqualIID(iid,&IID_IUnknown)||IsEqualIID(iid,&IID_Command)){*result=self;IExplorerCommand_AddRef(self);return S_OK;}return E_NOINTERFACE;
}
static ULONG STDMETHODCALLTYPE command_add(IExplorerCommand *self){return InterlockedIncrement(&((Command*)self)->references);}
static ULONG STDMETHODCALLTYPE command_release(IExplorerCommand *self){LONG n=InterlockedDecrement(&((Command*)self)->references);if(!n){HeapFree(GetProcessHeap(),0,self);InterlockedDecrement(&objects);}return n;}
static HRESULT STDMETHODCALLTYPE command_title(IExplorerCommand *self,IShellItemArray *items,LPWSTR *value){(void)self;(void)items;return copy_string(L"Convert with Matin Converter",value);}
static HRESULT STDMETHODCALLTYPE command_icon(IExplorerCommand *self,IShellItemArray *items,LPWSTR *value){(void)self;(void)items;WCHAR path[32768];HRESULT hr=executable(path,32768);if(FAILED(hr))return hr;StringCchCatW(path,32768,L",0");return copy_string(path,value);}
static HRESULT STDMETHODCALLTYPE command_tip(IExplorerCommand *self,IShellItemArray *items,LPWSTR *value){(void)self;(void)items;return copy_string(L"Convert using your saved Matin Converter settings",value);}
static HRESULT STDMETHODCALLTYPE command_name(IExplorerCommand *self,GUID *name){(void)self;if(!name)return E_POINTER;*name=CLSID_Matin;return S_OK;}
static HRESULT STDMETHODCALLTYPE command_state(IExplorerCommand *self,IShellItemArray *items,BOOL slow,EXPCMDSTATE *state){(void)self;(void)slow;if(!state)return E_POINTER;DWORD count=0;if(items)IShellItemArray_GetCount(items,&count);*state=count>0&&count<=10000?ECS_ENABLED:ECS_DISABLED;return S_OK;}
static BOOL write_all(HANDLE file,const void *data,DWORD size){DWORD written=0;return WriteFile(file,data,size,&written,NULL)&&written==size;}
static HRESULT STDMETHODCALLTYPE command_invoke(IExplorerCommand *self,IShellItemArray *items,IBindCtx *context) {
    (void)self;(void)context;if(!items)return E_INVALIDARG;DWORD count=0;HRESULT hr=IShellItemArray_GetCount(items,&count);if(FAILED(hr))return hr;if(!count||count>10000)return E_INVALIDARG;
    PWSTR local=NULL;hr=SHGetKnownFolderPath(&FOLDERID_LocalAppData,0,NULL,&local);if(FAILED(hr))return hr;
    WCHAR folder[32768],manifest[32768],exe[32768],command[65536],guidText[40];GUID id;
    hr=StringCchPrintfW(folder,32768,L"%s\\MatinConverter",local);CoTaskMemFree(local);if(FAILED(hr))return hr;
    if(!CreateDirectoryW(folder,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS)return HRESULT_FROM_WIN32(GetLastError());
    if(FAILED(StringCchCatW(folder,32768,L"\\Jobs")))return E_FAIL;
    if(!CreateDirectoryW(folder,NULL)&&GetLastError()!=ERROR_ALREADY_EXISTS)return HRESULT_FROM_WIN32(GetLastError());
    CoCreateGuid(&id);StringFromGUID2(&id,guidText,40);hr=StringCchPrintfW(manifest,32768,L"%s\\%s.job",folder,guidText);if(FAILED(hr))return hr;
    HANDLE file=CreateFileW(manifest,GENERIC_WRITE,0,NULL,CREATE_NEW,FILE_ATTRIBUTE_NORMAL,NULL);if(file==INVALID_HANDLE_VALUE)return HRESULT_FROM_WIN32(GetLastError());
    DWORD magic=0x314A434D,total=8;BOOL ok=write_all(file,&magic,4)&&write_all(file,&count,4);
    for(DWORD i=0;i<count&&ok;i++) {
        IShellItem *item=NULL;PWSTR path=NULL;hr=IShellItemArray_GetItemAt(items,i,&item);
        if(SUCCEEDED(hr)){hr=IShellItem_GetDisplayName(item,SIGDN_FILESYSPATH,&path);IShellItem_Release(item);}
        if(FAILED(hr)||!path){ok=FALSE;break;}DWORD length=(DWORD)wcslen(path);total+=4+length*2;
        if(!length||length>32767||total>4000000)ok=FALSE;else ok=write_all(file,&length,4)&&write_all(file,path,length*2);CoTaskMemFree(path);
    }
    if(ok)ok=FlushFileBuffers(file);DWORD error=GetLastError();CloseHandle(file);
    if(!ok){DeleteFileW(manifest);return HRESULT_FROM_WIN32(error?error:ERROR_INVALID_DATA);}
    hr=executable(exe,32768);if(SUCCEEDED(hr))hr=StringCchPrintfW(command,65536,L"\"%s\" --job-file \"%s\"",exe,manifest);
    if(FAILED(hr)){DeleteFileW(manifest);return hr;}
    STARTUPINFOW startup={0};PROCESS_INFORMATION process={0};startup.cb=sizeof(startup);
    if(!CreateProcessW(exe,command,NULL,NULL,FALSE,CREATE_UNICODE_ENVIRONMENT|CREATE_NO_WINDOW,NULL,NULL,&startup,&process)){error=GetLastError();DeleteFileW(manifest);return HRESULT_FROM_WIN32(error);}
    CloseHandle(process.hThread);CloseHandle(process.hProcess);return S_OK;
}
static HRESULT STDMETHODCALLTYPE command_flags(IExplorerCommand *self,EXPCMDFLAGS *flags){(void)self;if(!flags)return E_POINTER;*flags=ECF_DEFAULT;return S_OK;}
static HRESULT STDMETHODCALLTYPE command_enum(IExplorerCommand *self,IEnumExplorerCommand **items){(void)self;if(items)*items=NULL;return E_NOTIMPL;}
static IExplorerCommandVtbl command_table={command_query,command_add,command_release,command_title,command_icon,command_tip,command_name,command_state,command_invoke,command_flags,command_enum};
static HRESULT STDMETHODCALLTYPE factory_query(IClassFactory *self,REFIID iid,void **result){if(!result)return E_POINTER;*result=NULL;if(IsEqualIID(iid,&IID_IUnknown)||IsEqualIID(iid,&IID_IClassFactory)){*result=self;IClassFactory_AddRef(self);return S_OK;}return E_NOINTERFACE;}
static ULONG STDMETHODCALLTYPE factory_add(IClassFactory *self){return InterlockedIncrement(&((Factory*)self)->references);}
static ULONG STDMETHODCALLTYPE factory_release(IClassFactory *self){LONG n=InterlockedDecrement(&((Factory*)self)->references);if(!n){HeapFree(GetProcessHeap(),0,self);InterlockedDecrement(&objects);}return n;}
static HRESULT STDMETHODCALLTYPE factory_create(IClassFactory *self,IUnknown *outer,REFIID iid,void **result){(void)self;if(outer)return CLASS_E_NOAGGREGATION;Command *object=HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(Command));if(!object)return E_OUTOFMEMORY;object->iface.lpVtbl=&command_table;object->references=1;InterlockedIncrement(&objects);HRESULT hr=command_query(&object->iface,iid,result);command_release(&object->iface);return hr;}
static HRESULT STDMETHODCALLTYPE factory_lock(IClassFactory *self,BOOL lock){(void)self;if(lock)InterlockedIncrement(&locks);else InterlockedDecrement(&locks);return S_OK;}
static IClassFactoryVtbl factory_table={factory_query,factory_add,factory_release,factory_create,factory_lock};
HRESULT WINAPI DllGetClassObject(REFCLSID clsid,REFIID iid,void **result){if(!IsEqualCLSID(clsid,&CLSID_Matin))return CLASS_E_CLASSNOTAVAILABLE;Factory *object=HeapAlloc(GetProcessHeap(),HEAP_ZERO_MEMORY,sizeof(Factory));if(!object)return E_OUTOFMEMORY;object->iface.lpVtbl=&factory_table;object->references=1;InterlockedIncrement(&objects);HRESULT hr=factory_query(&object->iface,iid,result);factory_release(&object->iface);return hr;}
HRESULT WINAPI DllCanUnloadNow(void){return InterlockedCompareExchange(&objects,0,0)==0&&InterlockedCompareExchange(&locks,0,0)==0?S_OK:S_FALSE;}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID reserved){(void)reserved;if(reason==DLL_PROCESS_ATTACH){module=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
