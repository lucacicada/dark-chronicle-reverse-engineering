# Dark Chronicle Reverse Engineering

Dark Chronicle / Dark Cloud reverse engineering stuff

> Most of the work has been done using an original Dark Chronicle PAL PS2 CD

## Data HD file specification

The game uses an archive format to store its main files, the format is very similar among the two games Dark Cloud and Dark Chronicle (Dark Cloud 2)

The archive is split into chunks of 2048 bytes each, a file start from a chunk and the next file is offset to align itself to the next chunk location.

<p align="center">
  <img src=".assets/chunk.png" height="100" alt="Chunk Diagram" />
</p>

The `data.hd` file contains the name, offset and length of each file in the archive.

### data.hd2

From Dark Cloud:

```c
struct DATAHD2 {
    // the position of the null terminated string on the current file
    int fileNameOffset;
    int unused0; // padding for future uses
    int unused1; // padding for future uses
    int unused2; // padding for future uses
    // the file offset from the beginning of the archive
    int fileOffset;
    // the file size, in bytes
    int fileLength;
    // the chunk index at which the file starts, chunkOffset * 2048 gives you fileOffset
    int chunkOffset;
    // the number of chunks the file span across
    int chunkCount;
};
```

The file name is a null `\0` terminated string.

All files that contains invalid `ASCII` characters are empty, it is likely caused by the developers not deleting some temporary files properly before packing the game.

The game has been created using Windows, Level 5 is a japanese company, therefore the default CodePage used on japanese windows computer is `Shift_JIS`, this is also the encoding for most text-based game files.

Code page 932 `Shift_JIS` is the appropriated encoding when dealing with text based game files.

<p align="center">
  <img src=".assets/datahd.png" height="300" alt="Data HD" />
</p>

### data.hd3

From Dark Chronicle:

In Dark Chronicle, Level 5 have settled down to the archive format, therefore redundant fields has been removed.

```c
struct DATAHD3 {
    // the position of the null terminated string on the current file
    int fileNameOffset;
    // the file size, in bytes
    int fileLength;
    // the chunk index at which the file starts, chunkOffset * 2048 gives you fileOffset
    int chunkOffset;
    // the number of chunks the file span across
    int chunkCount;
};
```

The `fileOffset` has been removed, `chunkOffset * 2048` gives you the file offset.

The unused fields have also been removed.

#### data.hd4

```c
struct DATAHD4 {
    // the position of the null terminated string on the current file
    int fileNameOffset;
    // the file size, in bytes
    int fileLength;
    // the chunk index at which the file starts, chunkOffset * 2048 gives you fileOffset
    int chunkOffset;
};
```

Dark Chronicle contains a `data.hd4` which removes the `chunkCount`, you can calculate the number of chunks by dividing the `fileLength` by `2048`.

This archive format appear to be unused, it saves (4 * 8752) + 4 bytes.

There are 8752 files in the archive (one is duplicated), + 4 for what appear to be padding.

`data.hd3` file size is `354912 bytes`
`data.hd4` file size is `319900 bytes`

`35012` bytes are saved in the process.

#### Duplicate Files

The file `wep_t\ws050_item.chr` is the last file of the `data.hd3` archive and appear to be duplicate for unknown reasons. The two duplicated files are binary identical, it is likely that the software used by Level 5 to pack the files may have contained a bug that write the last file twice.

It is unknown if the game actually load the second or the first file.

As a side note, incase the game would actually load the second file it would be possible to write mods that do not rewrite the file entirely, but merely append some content at the end thus it will not be too hard to create a fast mod repacker.

## *Never Ending Adventure*

The `scans` folder contains hi resolution scan of the italian game cover, for preservation and reconstruction purposes.

The original font cover:

![Original Front Cover](scans/Front%20Cover.png)

The original CD:

![Original CD](scans/CD.png)

## Cheat Engine Table

The cheat engine table is mostly in italian as it's very old, I'v recently just updated it for PCSX2 1.7 `pcsx2-avx2.exe`.

It have some very useful features, for example:

- Debug Menu, see: [The Cutting Room Floor: Debug Menu](https://tcrf.net/Dark_Cloud_2/Debug_Menu)
- Character Position
- Aquarium, you can make the fish to always eat!
- Camera, take a picture of something useless, you can swap it for a photo idea at any moment!
- Invention List, add any invention you want, in any order!
- Weapons, id and flags have the proper menu!

![Cheat Table](.assets/cheat-table.png)

## ISO Explorer

[DarkChronicle.NET](DarkChronicle.NET/README.md)

The program allows you to see all the files in the ISO of the game, there are some shortcut and a filter so you can very quickly found anything!
