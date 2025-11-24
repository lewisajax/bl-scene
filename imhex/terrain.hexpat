#pragma description Mount & Blade II: Bannerlord's Scene Terrain
#pragma magic [5A 47 52 36] @ 0x00
#pragma endian little

import std.core;
import std.mem;
import std.io;
import std.array;
import hex.dec;

// The included png pattern wasn't working for me when I was trying to import it
// https://gist.github.com/LeoAdamek/4fea72236bf82e1f57c42db5d04b2cfa
namespace PNG {
    enum ChunkType : u32 {
        // Critical Chunks (uppercase first char)
        ImageHeader         = 0x49484452, //"IHDR",
        Palette             = 0x504c5445, //"PLTE",
        ImageData           = 0x49444154, //"IDAT",
        ImageEnd            = 0x49454E44, //"IEND",

        // Ancillery chunks (lowercase first char)
        Background          = 0x624B4744, //"bKGD",
        // ChromaticityCoords  = "cHRM",
        // Signature           = "dSIG",
        // Exif                = "eXIf",
        Gamma               = 0x67414d41,//"gAMA",
        // ColourHist          = "hIST",
        PixelDims           = 0x70485973, //"pHYs"
        Text                = 0x74455874, //"tEXt"
        ColorProfile        = 0x69434350, // iCCP
        Timestamp           = 0x74494D45, // tIME
        SRGB                = 0x73524742, // sRGB
    };
    
    enum ColorType : u8 {
        Grayscale   = 0,
        RGB         = 2,
        Indexed     = 3,
        GrayAlpha   = 4,
        RGBA        = 6
    };
    
    enum PixelUnit : u8 {
        Unknown,
        Metres
    };
    
    namespace ChunkData {
        struct ImageHeader {
            u32 imageWidth          [[name("Image Width")]];
            u32 imageHeight         [[name("Image Height")]];
            u8 bitsPerPixel         [[name("Bits per Channel")]];
            ColorType colorType     [[name("Colour Type")]];
            u8 compression          [[name("Compression Type")]];
            u8 filter               [[name("Filter Type")]];
            bool interlace          [[name("Interlaced Image?")]];
        };

        struct ImageData {
            u8 compression;
            u8 fCheck;
            u8 dataBlock[6];
            u32 alder32;
        };

        struct PixelPhysicalDimensions {
            u32 x           [[name("Width")]];
            u32 y           [[name("Height")]];
            PixelUnit unit  [[name("Unit")]];
        };

        struct PaletteEntry {
            u8 red          [[name("Red"),  color("ff0000ff")]];
            u8 green        [[name("Green"),color("00ff0000")]];
            u8 blue         [[name("Blue"), color("0000ff00")]];
        };

        struct Color16 {
            u16 red;
            u16 green;
            u16 blue;
        };

        struct Timestamp {
            u16 year;
            u8 month;
            u8 day;
            u8 hour;
            u8 minute;
            u8 second;
        };

        enum SRGBIntent : u8 {
            Perceptual,
            RelativeColorimetric,
            Saturation,
            AbsoluteColorimetric
        };
    }
    
    
    struct Header {
        u8 binaryFlag   [[comment("Byte with MSB high to avoid 7-bit interpretation"),name("Binary Flag")]];
        char magic[3]   [[comment("PNG Magic"), name("File Magic")]];
        char crlf[2]    [[comment("CRLF to detect DOS line endings"), name("CRLF")]];
        char eof        [[comment("EOF Marker to flag file as binary"), name("EOF")]];
        char lf         [[comment("LF to detect UNIX line endings"), name("LF")]];
    };
    
    struct Chunk {
        u32 length              [[comment("Chunk Length"), color("48006C00")]];
        be ChunkType chunkType  [[comment("Chunk type/name"), color("00566C00")]];
    
        if (chunkType == ChunkType::ImageHeader) {
            ChunkData::ImageHeader data;
        } else if (chunkType == ChunkType::Palette) {
            ChunkData::PaletteEntry palette[length / 3];
        } else if (chunkType == ChunkType::ImageData) {
            u8 compression;
            u8 fcheck;
            u8 data[length - 6];
            u32 checksum;
        } else if (chunkType == ChunkType::PixelDims) {
            ChunkData::PixelPhysicalDimensions data;
        } else if (chunkType == ChunkType::Background) {
            if (length == 1) {
                u8 colorIndex;
            } else if (length == 2) {
                u16 grayness;
            } else if (length == 6) {
                ChunkData::Color16 background;
            }
        } else if (chunkType == ChunkType::Text) {
            char data[length];
        } else if (chunkType == ChunkType::Timestamp) {
            ChunkData::Timestamp timestamp;
        } else if (chunkType == ChunkType::SRGB) {
            ChunkData::SRGBIntent intent;
        } else if (chunkType == ChunkType::Gamma) {
            u32 gamma;
        } else {
            u8 data[length];
        }
    
        u32 crc32 [[comment("Data CRC-32")]];
    };
}

struct Header {
    u32 magicNum;
    u32 fourCC; // Always RTRN/0x5254524E
    u32 version;
};

struct ChunkMetadata {
    char type[4];
    u32 unk; // Always 1
    u32 offset;
    u32 pngSize; // Need to + 32 for header
    u32 decompSize; // Maybe
};

struct SegmentMetadata {
    u64 count; // count?? always 3
    u64 decompSize; // decomp size
    u64 pngSize; // png size / header end pos
    u64 reserve; // always 0. reserve??
    
    // These 8 bytes are the same set of bytes that are in the related terrain_ed.bin, so it's probably an id
    // It could also be that the first 4 bytes are a 4 byte checksum and the last 4 is the max size of an lz4 chunk??
    u64 unk; // ??
};

struct Metadata {
    ChunkMetadata midx;
    ChunkMetadata hght;
    ChunkMetadata nrml;
    ChunkMetadata wght;
    if (header.version == 2) {
        ChunkMetadata phym;
    }
};

struct Segment<auto ChunkMetadata> {
    SegmentMetadata head;
    u8 rawData[head.pngSize - 40] [[export]]; // 40 bytes being the size of the segment metadata
    std::mem::Section decompSection = std::mem::create_section(ChunkMetadata.type);
    hex::dec::lz4_decompress(rawData, decompSection, false);
};

struct SegmentPNG {
    u32 pngSize;
    // Gotta be careful when using namespaces, since these variables get pushed to the global scope for a short window
    PNG::Header pngHeader;    
    be PNG::Chunk pngChunks[4];
};

struct WeightMap {
    u32 hasPaint;
    if (hasPaint == 1) {
        SegmentPNG png;
    }
};

struct PhysicsMaterial {
    u32 index;
    u32 size;
    float values[size];
};

struct MIDX {
    Segment<metadata.midx> segment[[inline]];
    SegmentPNG png[numNodes]@ 0x00 in segment.decompSection;
};

struct HGHT {
    // Each of the chunks, except for MIDX, has the offsets for each node's png above the compressed png data 
    u32 nodeOffsets[numNodes];
    Segment<metadata.hght> segment[[inline]];
    SegmentPNG png[numNodes]@ 0x00 in segment.decompSection;
};

struct NRML {
    u32 nodeOffsets[numNodes];
    Segment<metadata.nrml> segment[[inline]];
    SegmentPNG png[numNodes]@ 0x00 in segment.decompSection;
};

struct WGHT {
    u32 nodeOffsets[numNodes];
    Segment<metadata.wght> segment[[inline]];
    // It goes through each of the 15 paint layers. 
    // If a paint layer is used on the node, then it will have a 1 before the png bytes, else it will be 0 and no png
    std::Array<std::Array<WeightMap, 16>, numNodes> weightMaps@ 0x00 in segment.decompSection;
};

struct PHYM {
    u32 nodeOffsets[numNodes];
    Segment<metadata.phym> segment[[inline]];
    std::Array<std::Array<PhysicsMaterial, 4>, numNodes> physMaterials@ 0x00 in segment.decompSection;
};

// Global conditionals don't show up in the pattern data
struct Segments {
    MIDX midx;
    HGHT hght@metadata.hght.offset;
    NRML nrml@metadata.nrml.offset;
    WGHT wght@metadata.wght.offset;
    if (header.version >= 2) {
        PHYM phym@metadata.phym.offset;
    }
};

Header header@0;
Metadata metadata@0xC;

// We only need to do this once. Then it's the same amount of bytes for HGHT, NRML and WGHT.
// Unless I'm missing something obvious in terrain.bin, they probably get the amount of nodes from the .xscene
s32 numNodes = s32((metadata.nrml.offset - metadata.hght.pngSize - (metadata.midx.offset + metadata.midx.pngSize)) / 4) [[export]];

Segments segments@metadata.midx.offset;