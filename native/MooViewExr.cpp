// MooView EXR bridge. OpenEXR owns parsing/decompression; no exceptions cross the ABI.
#include <ImfMultiPartInputFile.h>
#include <ImfInputPart.h>
#include <ImfTiledInputPart.h>
#include <ImfChannelList.h>
#include <ImfHeader.h>
#include <ImfPartType.h>
#include <ImfIO.h>
#include <ImfFrameBuffer.h>
#include <fstream>
#include <filesystem>
#include <memory>
#include <sstream>
#include <vector>
#include <cstring>
#include <algorithm>
class Stream final : public Imf::IStream {
    std::ifstream file;
    uint64_t position=0, length=0;
public:
    explicit Stream(const wchar_t* path):IStream("MooView unicode input"),file(std::filesystem::path(path),std::ios::binary){if(!file)throw std::runtime_error("Cannot open EXR file.");length=std::filesystem::file_size(std::filesystem::path(path));}
    bool read(char* data,int n) override {if(n<0)throw std::runtime_error("Invalid EXR read.");file.read(data,n);position+=file.gcount();if(file.gcount()!=n)throw std::runtime_error("Truncated EXR file.");return position<length;}
    uint64_t tellg() override {return position;}
    int64_t size() override {return static_cast<int64_t>(length);}
    void clear() override {file.clear();}
    void seekg(uint64_t p) override {if(p>length)throw std::runtime_error("Invalid EXR seek.");file.clear();file.seekg(p);if(!file)throw std::runtime_error("Invalid EXR seek.");position=p;}
};
static void error(char* dest,int cap,const char* msg){if(cap>0){size_t n=std::min(static_cast<size_t>(cap-1),strlen(msg));memcpy(dest,msg,n);dest[n]=0;}}
static std::string quote(const std::string& s){std::ostringstream o;o<<'"';for(unsigned char c:s){if(c=='"'||c=='\\')o<<'\\'<<c;else if(c<32){const char* h="0123456789abcdef";o<<"\\u00"<<h[c>>4]<<h[c&15];}else o<<c;}o<<'"';return o.str();}
static void dimensions(const Imf::Header& h,int& w,int& v){auto d=h.dataWindow();int64_t x=int64_t(d.max.x)-d.min.x+1,y=int64_t(d.max.y)-d.min.y+1;if(x<1||y<1||x>131072||y>131072||x*y>134217728)throw std::runtime_error("EXR data window exceeds the preview memory limit.");w=int(x);v=int(y);}
#define API extern "C" __declspec(dllexport)
API void mv_free(void* p){free(p);}
API int mv_info(const wchar_t* path,char** result,char* err,int cap){*result=nullptr;try{Stream stream(path);Imf::MultiPartInputFile f(stream,2);if(f.parts()>1024)throw std::runtime_error("Too many EXR parts.");std::ostringstream j;j<<"[";for(int p=0;p<f.parts();p++){auto& h=f.header(p);int w,v;dimensions(h,w,v);if(p)j<<",";j<<"{\"Index\":"<<p<<",\"Name\":"<<quote(h.hasName()?h.name():"Image")<<",\"Type\":"<<quote(h.type())<<",\"Width\":"<<w<<",\"Height\":"<<v<<",\"X\":"<<h.dataWindow().min.x<<",\"Y\":"<<h.dataWindow().min.y<<",\"Channels\":[";int n=0;for(auto c=h.channels().begin();c!=h.channels().end();++c){if(++n>4096)throw std::runtime_error("Too many EXR channels.");if(n>1)j<<",";j<<"{\"Name\":"<<quote(c.name())<<",\"Type\":"<<int(c.channel().type)<<",\"XSampling\":"<<c.channel().xSampling<<",\"YSampling\":"<<c.channel().ySampling<<"}";}j<<"]}";}j<<"]";auto text=j.str();*result=static_cast<char*>(malloc(text.size()+1));if(!*result)throw std::bad_alloc();memcpy(*result,text.c_str(),text.size()+1);return 1;}catch(const std::exception& e){error(err,cap,e.what());return 0;}catch(...){error(err,cap,"EXR read failed.");return 0;}}
API int mv_read(const wchar_t* path,int part,const char** channels,int count,float** result,int* width,int* height,char* err,int cap){*result=nullptr;try{if(count<1||count>4)throw std::runtime_error("Select one to four EXR channels.");Stream stream(path);Imf::MultiPartInputFile f(stream,2);if(part<0||part>=f.parts())throw std::runtime_error("Invalid EXR part.");auto& h=f.header(part);if(Imf::isDeepData(h.type()))throw std::runtime_error("Deep EXR samples cannot be flattened in this viewer. Select a flat part.");int w,v;dimensions(h,w,v);size_t pixels=size_t(w)*v;if(pixels*count*sizeof(float)>1073741824)throw std::runtime_error("Selected EXR channels exceed the 1 GiB decode limit.");auto buffer=std::unique_ptr<float,decltype(&free)>(static_cast<float*>(calloc(pixels*count,sizeof(float))),&free);if(!buffer)throw std::bad_alloc();Imf::FrameBuffer frame;
    for(int i=0;i<count;i++){auto c=h.channels().findChannel(channels[i]);if(!c)throw std::runtime_error("EXR channel was not found.");if(c->xSampling<1||c->ySampling<1||c->xSampling>w||c->ySampling>v)throw std::runtime_error("Unsupported EXR channel sampling.");frame.insert(channels[i],Imf::Slice::Make(Imf::FLOAT,buffer.get()+pixels*i,h.dataWindow(),sizeof(float)*c->xSampling,sizeof(float)*w*c->ySampling,c->xSampling,c->ySampling));}
    if(Imf::isTiled(h.type())){Imf::TiledInputPart input(f,part);input.setFrameBuffer(frame);input.readTiles(0,input.numXTiles(0)-1,0,input.numYTiles(0)-1,0,0);}else{Imf::InputPart input(f,part);input.setFrameBuffer(frame);input.readPixels(h.dataWindow().min.y,h.dataWindow().max.y);}
    for(int i=0;i<count;i++){auto c=h.channels().findChannel(channels[i]);if(c->xSampling==1&&c->ySampling==1)continue;float* plane=buffer.get()+pixels*i;for(int y=v-1;y>=0;y--)for(int x=w-1;x>=0;x--)plane[size_t(y)*w+x]=plane[size_t(y/c->ySampling*c->ySampling)*w+x/c->xSampling*c->xSampling];}
    *width=w;*height=v;*result=buffer.release();return 1;
}catch(const std::exception& e){error(err,cap,e.what());return 0;}catch(...){error(err,cap,"EXR decode failed.");return 0;}}
