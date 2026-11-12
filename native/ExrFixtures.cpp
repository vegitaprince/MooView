#include <ImfOutputFile.h>
#include <ImfMultiPartOutputFile.h>
#include <ImfOutputPart.h>
#include <ImfTiledOutputFile.h>
#include <ImfTileDescription.h>
#include <ImfChannelList.h>
#include <ImfHeader.h>
#include <ImfPartType.h>
#include <ImfFrameBuffer.h>
#include <filesystem>
#include <vector>
#include <string>
#include <iostream>
int main(int argc,char** argv){try {if(argc!=2)return 1;std::filesystem::create_directories(argv[1]);const int w=32,h=24;std::vector<float> r(w*h,.25f),g(w*h,.5f),b(w*h,1.f),other(w*h,.1f);Imf::FrameBuffer fb;fb.insert("R",Imf::Slice::Make(Imf::FLOAT,r.data(),{0,0},w,h));fb.insert("G",Imf::Slice::Make(Imf::FLOAT,g.data(),{0,0},w,h));fb.insert("B",Imf::Slice::Make(Imf::FLOAT,b.data(),{0,0},w,h));
std::vector<IMATH_NAMESPACE::half> hr(w*h,.25f),hg(w*h,.5f),hb(w*h,1.f);Imf::FrameBuffer half;half.insert("R",Imf::Slice::Make(Imf::HALF,hr.data(),{0,0},w,h));half.insert("G",Imf::Slice::Make(Imf::HALF,hg.data(),{0,0},w,h));half.insert("B",Imf::Slice::Make(Imf::HALF,hb.data(),{0,0},w,h));
for(auto compression:{Imf::NO_COMPRESSION,Imf::RLE_COMPRESSION,Imf::ZIPS_COMPRESSION,Imf::ZIP_COMPRESSION,Imf::PIZ_COMPRESSION,Imf::PXR24_COMPRESSION,Imf::B44_COMPRESSION,Imf::B44A_COMPRESSION,Imf::DWAA_COMPRESSION,Imf::DWAB_COMPRESSION,Imf::HTJ2K256_COMPRESSION,Imf::HTJ2K32_COMPRESSION}){Imf::Header header(w,h);header.compression()=compression;for(auto c:{"R","G","B"})header.channels().insert(c,Imf::Channel(Imf::HALF));Imf::OutputFile out((std::filesystem::path(argv[1])/("compression-"+std::to_string(compression)+".exr")).string().c_str(),header);out.setFrameBuffer(half);out.writePixels(h);}
Imf::Header layers(w,h);layers.channels().insert("R",Imf::Channel(Imf::FLOAT));layers.channels().insert("G",Imf::Channel(Imf::FLOAT));layers.channels().insert("B",Imf::Channel(Imf::FLOAT));for(auto c:{"diffuse.R","diffuse.G","diffuse.B","Z"}){layers.channels().insert(c,Imf::Channel(Imf::FLOAT));fb.insert(c,Imf::Slice::Make(Imf::FLOAT,other.data(),{0,0},w,h));}{Imf::OutputFile out((std::filesystem::path(argv[1])/"layers.exr").string().c_str(),layers);out.setFrameBuffer(fb);out.writePixels(h);}
Imf::Header headers[2]={layers,layers};headers[0].setName("beauty");headers[1].setName("lighting");for(auto& head:headers)head.setType(Imf::SCANLINEIMAGE);{Imf::MultiPartOutputFile file((std::filesystem::path(argv[1])/"multipart.exr").string().c_str(),headers,2);for(int p=0;p<2;p++){Imf::OutputPart part(file,p);part.setFrameBuffer(fb);part.writePixels(h);}}
layers.setTileDescription(Imf::TileDescription(16,16));{Imf::TiledOutputFile out((std::filesystem::path(argv[1])/"tiled.exr").string().c_str(),layers);out.setFrameBuffer(fb);out.writeTiles(0,out.numXTiles()-1,0,out.numYTiles()-1);}
Imf::Header offset(w,h);offset.dataWindow()=IMATH_NAMESPACE::Box2i({-8,12},{w-9,h+11});for(auto c:{"R","G","B"})offset.channels().insert(c,Imf::Channel(Imf::FLOAT));Imf::FrameBuffer of;of.insert("R",Imf::Slice::Make(Imf::FLOAT,r.data(),offset.dataWindow()));of.insert("G",Imf::Slice::Make(Imf::FLOAT,g.data(),offset.dataWindow()));of.insert("B",Imf::Slice::Make(Imf::FLOAT,b.data(),offset.dataWindow()));{Imf::OutputFile out((std::filesystem::path(argv[1])/"offset.exr").string().c_str(),offset);out.setFrameBuffer(of);out.writePixels(h);}
return 0;}catch(const std::exception& e){std::cerr<<e.what()<<std::endl;return 1;}}
