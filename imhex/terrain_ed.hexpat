#pragma description Mount & Blade II: Bannerlord's Scene Terrain Edit Data
#pragma magic [4F 4B 30 31] @ 0x00
#pragma endian little

import std.io;
import hex.dec;

struct Vec4f {
    float x, y, z, w;
};

struct Header {
    u32 magicNum;
    u64 compressedSize;
    u32 version;
    padding[4]; // Reserve?
    u64 actualSize;
    padding[8]; // Compressed size again. There are 2 or more read events that take place, so that's probably why it's repeated
    
    padding[4];
    padding[4]; // Could be an offset/count
    padding[4]; // Maybe a 4 byte checksum??
    padding[4]; // Could be the max lz4 sequence size??
};

struct Material {
    u32 layerIndex; // Starts at 1
    float weights[parent.numVerts];
};

struct Node {
    u32 xIndex;
    u32 yIndex;
    u32 layerMasks[4]; // Seasons
    
    u32 normResX; // For some reason they add +1 to the resolution
    u32 normResY;
    u32 numNormals = normResX * normResY; // Normal maps are usually set to the same size as the heightmap.
    Vec4f normals[numNormals];
    
    u32 vertResX;
    u32 vertResY;
    u32 numVerts = vertResX * vertResY;
    
    float weights[numVerts]; // Splatmap
    
    // Idk where 32 is coming from. 
    // It is affected by surrounding higher/lower res nodes but it still takes up 32 * 4 bytes of space
    // It's usually the full 32 res values, unless the bordering nodes are a higher/lower res
    // Then it's only like 4 or 8 values and the rest 0s
    float unknown[32]; 
    
    float heights[numVerts];
    
    u32 numMaterials;
    Material materialWeights[numMaterials];
};

Header header@0;
u8 rawData[header.compressedSize - 40]@0x34; // Subtract 40 bytes for the header
std::mem::Section decompSection = std::mem::create_section("Decomp Data");
hex::dec::lz4_decompress(rawData, decompSection, false);

u32 xAxis @ 0x00 in decompSection;
u32 yAxis @ 0x04 in decompSection; 
u32 numNodes = xAxis * yAxis;
Node nodes[numNodes] @ 0x08 in decompSection;