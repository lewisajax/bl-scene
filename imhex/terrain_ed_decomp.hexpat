#pragma description Mount & Blade II: Bannerlord's Scene Terrain Edit Data Decompiled
#pragma endian little

import std.io;
import hex.dec;

// This is just the terrain_ed pattern without the decomp step

struct Vec4f {
    float x, y, z, w;
} [[static]];

struct Material {
    u32 layerIndex; // Starts at 1
    float weights[parent.numVerts];
} [[static]];

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
    
    float weights[numVerts]; // Or maybe better term would be a splatmap
    
    // Idk where 32 is coming from. 
    // It is affected by surrounding higher/lower res nodes but it still takes up 32 * 4 bytes of space
    // It's usually the full 32 res values, unless the bordering nodes are a higher/lower res
    // Then it's only like 4 or 8 values and the rest 0s
    float unknown[32]; 
    
    float heights[numVerts];
    
    u32 numMaterials;
    Material materialWeights[numMaterials];
};

u32 xAxis @ 0x00;
u32 yAxis @ 0x04; 
u32 numNodes = xAxis * yAxis;
Node nodes[numNodes] @ 0x08;