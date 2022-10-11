namespace DarkChronicle;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using DiscUtils.Iso9660;
using DiscUtils.Streams;

public enum DarkChronicleLang
{
    JP = 0,
    EN = 1,
    FR = 2,
    DE = 3,
    IT = 4,
    ES = 5,
    UN = 6,
}

[DebuggerDisplay("{FileName} ({FileLenght} bytes)")]
public record class DataInfo
{
    public const int ChunkSize = 2048;

    public string FileName { get; }
    public int FileNameOffset { get; }
    public int FileOffset { get; }
    public int FileLenght { get; }
    public int ChunkOffset { get; }
    public int ChunkLenght { get; }

    [JsonConstructor]
    public DataInfo(string fileName, int fileNameOffset, int fileOffset, int fileLenght, int chunkOffset, int chunkLenght)
    {
        if (fileName is null) throw new ArgumentNullException(nameof(fileName));

        FileName = fileName;
        FileNameOffset = fileNameOffset;
        FileOffset = fileOffset;
        FileLenght = fileLenght;
        ChunkOffset = chunkOffset;
        ChunkLenght = chunkLenght;
    }

    /// <inheritdoc />
    public override string ToString() => FileName;
}

public class DarkChronicleReader : IDisposable
{
    public static DarkChronicleReader OpenISOFile(string fileName)
    {
        if (fileName is null) throw new ArgumentNullException(nameof(fileName));

        var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        var reader = new CDReader(fs, joliet: true);

        if (!reader.FileExists("DATA.DAT"))
        {
            fs.Dispose();
            reader.Dispose();

            throw new InvalidOperationException($"The ISO does not contains the file 'DATA.DAT'.");
        }

        if (!reader.FileExists("DATA.HD3"))
        {
            fs.Dispose();
            reader.Dispose();

            throw new InvalidOperationException($"The ISO does not contains the file 'DATA.HD3'.");
        }

        Dictionary<string, DataInfo> nameToData = new();
        List<DataInfo> list = new();
        using (var stream = reader.OpenFile("data.hd3", FileMode.Open, FileAccess.Read))
        {
            byte[] buffer = new byte[Marshal.SizeOf(typeof(DataHD3))];

            while (true)
            {
                var i = stream.Read(buffer, 0, buffer.Length);

                if (i < buffer.Length)
                    break;

                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                DataHD3 dataHD;

                try
                {
                    dataHD = Marshal.PtrToStructure<DataHD3>(handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }

                // some file are broked
                // if (dataHD.FileLenght == 0)
                // {
                //     continue;
                // }

                var dataFileName = ReadString(stream, dataHD.FileNameOffset);

                if (dataFileName.Length == 0)
                    break;

                var data = new DataInfo(
                    dataFileName,
                    dataHD.FileNameOffset,
                    dataHD.ChunkOffset * DataInfo.ChunkSize,
                    dataHD.FileLenght,
                    dataHD.ChunkOffset,
                    dataHD.ChunkLenght
                );

                // TODO: the last file for some reason is exact duplicate
                if (nameToData.TryGetValue(data.FileName, out _))
                {
                    break;
                }

                nameToData[data.FileName] = data;
            }
        }

        Debug.Assert(nameToData.Count == 8751, "Dark Chronicle should contain 8751 files.");

        return new DarkChronicleReader(fs, reader, nameToData.ToImmutableDictionary());

        static string ReadString(SparseStream stream, int offset)
        {
            StringBuilder stringBuilder = new();

            var oldPosition = stream.Position;
            stream.Position = offset;

            int b;
            while ((b = stream.ReadByte()) > 0)
                stringBuilder.Append((char)b);

            stream.Position = oldPosition;

            return stringBuilder.ToString();
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly FileStream stream;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly CDReader reader;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly ImmutableDictionary<string, DataInfo> nameToData;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool disposedValue;

    public ImmutableDictionary<string, DataInfo> Files => nameToData;

    private DarkChronicleReader(FileStream stream, CDReader reader, ImmutableDictionary<string, DataInfo> nameToData)
    {
        this.stream = stream;
        this.reader = reader;
        this.nameToData = nameToData;
    }

    [DebuggerDisplay("{Id}: {Name,nq}")]
    public class PIC
    {
        public int Id { get; }
        public string Name { get; }
        public int Id2 { get; }

        public PIC(int id, string name, int id2)
        {
            Id = id;
            Name = name;
            Id2 = id2;
        }

        public override string ToString() => $"PIC_NAME {Id},{Name},{Id2};";
    }

    public List<PIC> GetPIC(DarkChronicleLang lang)
    {
        var e = CodePagesEncodingProvider.Instance.GetEncoding(932) ?? throw new NotSupportedException("Encoding 932 'shift_jis' is not supported.");

        using var s = OpenFile($@"menu\{(int)lang}\neta2.lst");
        using var r = new StreamReader(s, e);

        var csv = new CsvReader(r);

        List<PIC> c = new();

        _ = csv.Read(out _);  // PIC_INFO 294; 294 is the PIC count

        while (csv.Read(out var line))
        {
            var id = line.GetId("PIC_NAME");
            var name = line.GetString(1);
            var id2 = line.GetInt(2);

            c.Add(new PIC(id, name, id2));
        }

        return c;
    }

    [DebuggerDisplay("{Id}: {Name,nq}")]
    public class SCOOP
    {
        public int Id { get; }
        public string Name { get; }

        public SCOOP(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => $"STR {Id},{Name};";
    }

    public List<SCOOP> LoadSCOOP(DarkChronicleLang lang)
    {
        var e = CodePagesEncodingProvider.Instance.GetEncoding(932) ?? throw new NotSupportedException("Encoding 932 'shift_jis' is not supported.");

        using var s = OpenFile($@"menu\{(int)lang}\scoop.cfg");
        using var r = new StreamReader(s, e);

        var csv = new CsvReader(r);

        List<SCOOP> c = new();

        while (csv.Read(out var line))
        {
            var id = line.GetId("STR");
            var name = line.GetString(1);

            c.Add(new SCOOP(id, name));
        }

        return c;
    }

    [DebuggerDisplay("{Id}: {Name,nq}")]
    public class ITEM
    {
        public int Id { get; }
        public string Name { get; }

        public ITEM(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => $"MES_SYS {Id},{Name};";
    }

    public List<ITEM> LoadITEM(DarkChronicleLang lang)
    {
        var e = CodePagesEncodingProvider.Instance.GetEncoding(932) ?? throw new NotSupportedException("Encoding 932 'shift_jis' is not supported.");

        using var s = OpenFile($@"menu\cfg7\comdatmes{(int)lang}.cfg");
        using var r = new StreamReader(s, e);

        var csv = new CsvReader(r);

        List<ITEM> c = new();

        while (csv.Read(out var line))
        {
            var id = line.GetId("MES_SYS");
            var name = line.GetString(1);

            c.Add(new ITEM(id, name));
        }

        return c;
    }





    public Stream OpenFile(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        if (!nameToData.TryGetValue(path, out var data))
        {
            throw new FileNotFoundException(null, path);
        }

        var stream = reader.OpenFile("DATA.DAT", FileMode.Open, FileAccess.Read);

        return new SafeStreamWrapper(stream, data.FileOffset, data.FileLenght, disposeOnClose: true);
    }

    public Stream OpenFile(DataInfo dataInfo)
    {
        if (dataInfo is null) throw new ArgumentNullException(nameof(dataInfo));

        return OpenFile(dataInfo.FileName);
    }

    private string ReadFileContent(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        // the default encoding for most text stuff is 932
        // CodePagesEncodingProvider.Instance.GetEncoding(932); // shift_jis

        using var fs = OpenFile(path);
        using var r = new StreamReader(fs);

        return r.ReadToEnd();
    }

    public void ExtractTo(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException();

        path = Path.GetFullPath(path);

        using (var stream = reader.OpenFile("DATA.DAT", FileMode.Open, FileAccess.Read))
        {
            foreach (KeyValuePair<string, DataInfo> data in nameToData)
            {
                var fullName = Path.Join(path, data.Key);
                var dirName = Path.GetDirectoryName(fullName);

                Debug.Assert(dirName is not null);

                _ = Directory.CreateDirectory(dirName);

                using var s = new SafeStreamWrapper(stream, data.Value.FileOffset, data.Value.FileLenght, disposeOnClose: false);
                using var fs = new FileStream(fullName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 8);

                s.CopyTo(fs);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                stream.Dispose();
                reader.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataHD3
    {
        public int FileNameOffset;
        public int FileLenght;
        public int ChunkOffset;
        public int ChunkLenght;
    }
}

public class CsvReader
{
    private readonly StreamReader streamReader;

    public CsvReader(StreamReader streamReader)
    {
        this.streamReader = streamReader ?? throw new ArgumentNullException(nameof(streamReader));
    }

    // TODO: ignore line that start with //
    public bool Read([NotNullWhen(true)] out CSVRecord? record)
    {
        string? line;

        while (true)
        {
            line = streamReader.ReadLine();

            if (line is null)
            {
                record = null;
                return false;
            }

            // skip empty line
            if (!string.IsNullOrWhiteSpace(line)) break;
        }

        // this is an example from the game
        // "MAP_NAME \"s40\",\"Venetium\",\"bs/battleship01\", 3 , 3 ,-1, 4 ;// 9 ";

        int lineLength = line.Length;

        Debug.Assert(lineLength > 0);

        List<string> dataList = new();

        int separator = 0;

        bool openQuote = false;
        for (int i = 0; i < lineLength; i++)
        {
            char c = line[i];

            if (c is '"')
            {
                openQuote = !openQuote;
                continue;
            }

            // accept any value inside a quote
            if (openQuote)
            {
                continue;
            }

            // ; is the end of the csv
            if (c is ';')
            {
                string value = line[separator..i];

                value = value.Trim();
                if (value.Length > 0 && value[0] == '"')
                {
                    // this is a quoted string
                    value = value.Trim('"');
                }

                dataList.Add(value);
                break;
            }

            if (c is ',')
            {
                string value = line[separator..i];

                value = value.Trim();
                if (value.Length > 0 && value[0] == '"')
                {
                    // this is a quoted string
                    value = value.Trim('"');
                }

                dataList.Add(value);
                separator = i + 1;
            }
        }

        // ; delimit the end not the newline
        // dataList.Add(line[separator..lineLength]);

        record = new CSVRecord(dataList);

        return true;
    }
}

public sealed class CSVRecord
{
    private readonly List<string> parts;

    public int Count => parts.Count;

    internal CSVRecord(IEnumerable<string> parts)
    {
        this.parts = parts.ToList();
    }

    public int GetId(string? str)
    {
        var part = parts[0];

        str = str?.Trim();
        if (str is null)
        {
            return int.Parse(part);
        }

        str += ' ';

        var indexOfId = part.IndexOf(str);

        return indexOfId == -1 ? 0 : int.Parse(part[(indexOfId + str.Length)..]);
    }

    public int GetInt(int ordinal) => int.Parse(parts[ordinal]);

    public string GetString(int ordinal) => StringUtils.Unescape(parts[ordinal]);

    public string GetRawValue(int ordinal) => parts[ordinal];
}








/// <summary>
///     Values for the archive format type.
/// </summary>
public enum DataHdVersion
{
    /// <summary>
    ///     The version 2 data.hd2, used by Dark Cloud.
    /// </summary>
    Version2,

    /// <summary>
    ///     The version 3 data.hd3, used on Dark Chronicle.
    /// </summary>
    Version3,

    /// <summary>
    ///     The version 4 data.hd4, used on Dark Chronicle.
    /// </summary>
    Version4,
}

/// <summary>
///     Specifies values for interacting with data.hd archive entries.
/// </summary>
public enum DataHdArchiveMode
{
    /// <summary>
    ///     Only reading archive entries is permitted.
    /// </summary>
    Read = 0,

    /// <summary>
    ///     Only creating new archive entries is permitted.
    /// </summary>
    Create = 1,

    /// <summary>
    ///     Both read and write operations are permitted for archive entries.
    /// </summary>
    Update = 2
}

public class DataHdArchiveEntry
{
    public const int ChunkSize = 2048;

    /// <summary>
    ///     Gets the filename of the entry.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the position of the entry.
    /// </summary>
    public int Position => ChunkOffset * ChunkSize;

    /// <summary>
    ///     Gets the length in bytes of the entry.
    /// </summary>
    public int Lenght { get; }

    /// <summary>
    ///     Gets how many chunks the entry uses.
    /// </summary>
    public int ChunkOffset { get; }

    /// <summary>
    ///     Gets how many chunks are used by the entry.
    /// </summary>
    public int ChunkCount { get; }

    /// <summary>
    ///     Gets the position of the name of the entry, -1 indicates that the entry have not been written.
    /// </summary>
    public int NamePosition { get; }

    DataHdArchive? _archive;
    internal DataHdArchiveEntry(DataHdArchive archive) // , ZipCentralDirectoryFileHeader cd
    {
        _archive = archive;
    }

    private void ThrowIfInvalidArchive()
    {
        if (_archive is null) throw new InvalidOperationException("Cannot modify deleted entry.");

        _archive.ThrowIfDisposed();
    }
}

public class DataHdArchive : IDisposable
{
    public static DataHdArchive Open(string fileName)
        => Open(fileName, DataHdArchiveMode.Read);

    public static DataHdArchive Open(string fileName, DataHdArchiveMode mode)
        => Open(fileName, mode, DataHdVersion.Version2, autoDetectVersion: true);

    public static DataHdArchive Open(string fileName, DataHdArchiveMode mode, DataHdVersion version)
        => Open(fileName, mode, version, autoDetectVersion: true);

    private static DataHdArchive Open(string fileName, DataHdArchiveMode mode, DataHdVersion version, bool autoDetectVersion)
    {
        if (fileName is null) throw new ArgumentNullException(nameof(fileName));

        FileMode fileMode;
        FileAccess access;
        FileShare fileShare;

        switch (mode)
        {
            case DataHdArchiveMode.Read:
                fileMode = FileMode.Open;
                access = FileAccess.Read;
                fileShare = FileShare.Read;
                break;

            case DataHdArchiveMode.Create:
                fileMode = FileMode.CreateNew;
                access = FileAccess.Write;
                fileShare = FileShare.None;
                break;

            case DataHdArchiveMode.Update:
                fileMode = FileMode.OpenOrCreate;
                access = FileAccess.ReadWrite;
                fileShare = FileShare.None;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (autoDetectVersion)
        {
            var ex = Path.GetExtension(fileName);

            if (ex is ".hd2") version = DataHdVersion.Version2;
            else if (ex is ".hd3") version = DataHdVersion.Version3;
            else if (ex is ".hd4") version = DataHdVersion.Version4;
            else throw new InvalidOperationException("Unable to determine the archive version.");
        }

        FileStream fs = new(fileName, fileMode, access, fileShare, bufferSize: 0x1000, useAsync: false);

        try
        {
            return new DataHdArchive(fs, version, mode, leaveOpen: false);
        }
        catch
        {
            fs.Dispose();

            throw;
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Stream _stream;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly bool _leaveOpen;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _isDisposed;

    /// <summary>
    ///     The <see cref="DataHdVersion" /> of the archive.
    /// </summary>
    public DataHdVersion Version { get; }

    /// <summary>
    ///     The <see cref="DataHdArchiveMode" /> of the archive.
    /// </summary>
    public DataHdArchiveMode Mode { get; }

    public DataHdArchive(Stream stream, DataHdVersion version, DataHdArchiveMode mode = DataHdArchiveMode.Read, bool leaveOpen = false)
    {
        if (version is not (DataHdVersion.Version2 or DataHdVersion.Version3 or DataHdVersion.Version4))
            throw new ArgumentOutOfRangeException(nameof(version));

        switch (mode)
        {
            case DataHdArchiveMode.Create:
                if (!stream.CanWrite) throw new ArgumentException("Cannot use create mode on a non-writable stream");
                break;

            case DataHdArchiveMode.Read:
                if (!stream.CanRead) throw new ArgumentException("Cannot use read mode on a non-readable stream.");
                if (!stream.CanSeek) throw new ArgumentException("Cannot use read mode on a non-seekable stream.");
                break;

            case DataHdArchiveMode.Update:
                if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek)
                    throw new ArgumentException("Update mode requires a stream with read, write, and seek capabilities.");
                break;

            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }

        _stream = stream;
        Version = version;
        Mode = mode;
        _leaveOpen = leaveOpen;
    }

    ///// <summary>
    ///// The collection of entries that are currently in the ZipArchive. This may not accurately represent the actual entries that are present in the underlying file or stream.
    ///// </summary>
    ///// <exception cref="NotSupportedException">The ZipArchive does not support reading.</exception>
    ///// <exception cref="ObjectDisposedException">The ZipArchive has already been closed.</exception>
    ///// <exception cref="InvalidDataException">The Zip archive is corrupt and the entries cannot be retrieved.</exception>
    //public ReadOnlyCollection<ZipArchiveEntry> Entries
    //{
    //    get
    //    {
    //        if (_mode == DataHdArchiveMode.Create)
    //            throw new NotSupportedException("Cannot access entries in Create mode.");

    //        ThrowIfDisposed();

    //        EnsureCentralDirectoryRead();
    //        return _entriesCollection;
    //    }
    //}

    [StructLayout(LayoutKind.Sequential)]
    private struct DataHD3
    {
        public int FileNameOffset;
        public int FileLenght;
        public int ChunkOffset;
        public int ChunkLenght;
    }
    bool _readEntries;
    public void EnsureCentralDirectoryRead()
    {
        if (!_readEntries)
        {
            // ReadCentralDirectory();

            if (Version == DataHdVersion.Version3)
            {
                List<DataHD3> list = new();

                int i = 0;
                byte[] buffer = new byte[Marshal.SizeOf(typeof(DataHD3))];
                while (TryReadBlock(_stream, buffer, out DataHD3 data))
                {
                    i += buffer.Length;

                    // this is the last file
                    if (data.ChunkLenght == 0 && data.ChunkOffset == 0 && data.FileLenght == 0)
                    {
                        break;
                    }
                    else
                    {
                        list.Add(data);
                    }
                }

                foreach (var data in list)
                {
                    var fileOffset = data.FileNameOffset;
                    var pos = _stream.Position;

                    ReadString(_stream, fileOffset);
                }
            }

            _readEntries = true;
        }

        static bool TryReadBlock<T>(Stream stream, byte[] buffer, out T value) where T : struct
        {
            var i = stream.Read(buffer, 0, buffer.Length);

            if (i < buffer.Length)
            {
                value = default;
                return false;
            }

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            T dataHD;

            try
            {
                dataHD = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            value = dataHD;
            return true;
        }

        static string ReadString(Stream stream, int offset)
        {
            StringBuilder stringBuilder = new();

            var oldPosition = stream.Position;
            stream.Position = offset;

            bool nonAscii = false;

            int b;
            int i = 0;
            while ((b = stream.ReadByte()) > 0)
            {
                i++;
                if (b > 127)
                {
                    nonAscii = true;
                }

                stringBuilder.Append((char)b);
            }

            if (nonAscii)
            {
                stream.Position = offset;
                byte[] buffer = new byte[i];
                _ = stream.Read(buffer, 0, i);

                var e = CodePagesEncodingProvider.Instance.GetEncoding(932) ?? throw new NotSupportedException("Encoding 932 'shift_jis' is not supported.");
                var ff = e.GetString(buffer);

            }

            stream.Position = oldPosition;

            return stringBuilder.ToString();
        }
    }

    internal void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(GetType().FullName);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            if (!_leaveOpen)
            {
                _stream.Dispose();
                _isDisposed = true;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}







public class DataHdReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool disposedValue;

    /// <summary>
    ///     The <see cref="DataHdVersion" /> of the reader.
    /// </summary>
    public DataHdVersion Version { get; }

    public DataHdReader(Stream stream, DataHdVersion version) : this(stream, version, leaveOpen: false) { }

    public DataHdReader(Stream stream, DataHdVersion version, bool leaveOpen)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        if (version is not (DataHdVersion.Version2 or DataHdVersion.Version3 or DataHdVersion.Version4))
            throw new ArgumentOutOfRangeException(nameof(version));

        if (!stream.CanRead) throw new ArgumentException("Cannot use read mode on a non-readable stream.");
        // if (!stream.CanSeek) throw new ArgumentException("Cannot use read mode on a non-seekable stream.");

        _stream = stream;
        _leaveOpen = leaveOpen;
        Version = version;
    }

    public void ReadFileHeaders()
    {
        var e = CodePagesEncodingProvider.Instance.GetEncoding(932) ?? throw new NotSupportedException("Encoding 932 'shift_jis' is not supported.");

        ReadFileHeaders(e);
    }

    public void ReadFileHeaders(Encoding encoding)
    {
        if (encoding is null) throw new ArgumentNullException(nameof(encoding));

        // 8752 items are expected most of the time
        List<DataHD> list = new(capacity: 8752);

        int position = 0;

        if (Version == DataHdVersion.Version2)
        {
            byte[] buffer = new byte[Marshal.SizeOf(typeof(DataHD2))];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                while (true)
                {
                    var i = _stream.Read(buffer, 0, buffer.Length);

                    // EOF
                    if (i == 0) break;

                    position += i;

                    if (i != buffer.Length)
                    {
                        throw new IOException("Unexpected end of file");
                    }

                    var data = Marshal.PtrToStructure<DataHD2>(handle.AddrOfPinnedObject());

                    // the last file
                    if (data.ChunkLenght == 0 && data.ChunkOffset == 0 && data.FileLength == 0)
                    {
                        break;
                    }

                    list.Add((DataHD)data);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        else if (Version == DataHdVersion.Version3)
        {
            byte[] buffer = new byte[Marshal.SizeOf(typeof(DataHD3))];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                while (true)
                {
                    var i = _stream.Read(buffer, 0, buffer.Length);

                    // EOF
                    if (i == 0) break;

                    position += i;

                    if (i != buffer.Length)
                    {
                        throw new IOException("Unexpected end of file");
                    }

                    var data = Marshal.PtrToStructure<DataHD3>(handle.AddrOfPinnedObject());

                    // the last file
                    if (data.ChunkLenght == 0 && data.ChunkOffset == 0 && data.FileLength == 0)
                    {
                        break;
                    }

                    list.Add((DataHD)data);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        else if (Version == DataHdVersion.Version4)
        {
            byte[] buffer = new byte[Marshal.SizeOf(typeof(DataHD4))];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                while (true)
                {
                    var i = _stream.Read(buffer, 0, buffer.Length);

                    // EOF
                    if (i == 0) break;

                    position += i;

                    if (i != buffer.Length)
                    {
                        throw new IOException("Unexpected end of file");
                    }

                    var data = Marshal.PtrToStructure<DataHD4>(handle.AddrOfPinnedObject());

                    // the last file
                    if (data.ChunkOffset == 0 && data.FileLength == 0)
                    {
                        break;
                    }

                    list.Add((DataHD)data);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        else throw new InvalidOperationException("Unsupported version.");

        foreach (var data in list)
        {
            var offset = data.FileNameOffset;

            if (offset <= 0 || position > offset)
            {
                // we have just read the entries in sequence, the offset is point backwards
                throw new InvalidOperationException($"The file name offset {offset} is invalid.");
            }
            else if (position < offset) // this does not happen normally
            {
                // throw new InvalidOperationException($"The archive header is not aligned.");

                if (_stream.CanSeek)
                {
                    position = (int)_stream.Seek(offset, SeekOrigin.Begin);
                }
                else
                {
                    // consume the steam until the offset is reach
                    for (int i = 0; i < offset - position; i++)
                    {
                        if (_stream.ReadByte() == -1)
                        {
                            throw new IOException("Unexpected end of file");
                        }
                    }

                    position = offset;
                }
            }

            // 42 is usually big enough to avoid reallocation
            MemoryStream memoryStream = new(capacity: 42);

            byte[] buffer = new byte[1];

            while (true)
            {
                if (_stream.Read(buffer, 0, 1) == 0)
                {
                    throw new IOException("Unexpected end of file");
                }

                position++;

                // null terminated
                if (buffer[0] == 0)
                {
                    break;
                }

                memoryStream.Write(buffer, 0, 1);
            }

            var len = (int)memoryStream.Length;
            data.FileName = encoding.GetString(memoryStream.GetBuffer(), 0, len);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !disposedValue)
        {
            if (!_leaveOpen)
            {
                _stream.Dispose();
                disposedValue = true;
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [DebuggerDisplay("{FileName,nq}, Length: {FileLength}")]
    private class DataHD // ZipCentralDirectoryFileHeader
    {
        public const int ChunkSize = 2048;

        public string? FileName;
        public int FileNameOffset;

        public int FileOffset;
        public int FileLength;

        public int ChunkOffset;
        public int ChunkLenght;

        public static explicit operator DataHD(DataHD2 dataHD)
        {
            return new DataHD()
            {
                FileNameOffset = dataHD.FileNameOffset,
                FileOffset = dataHD.FileOffset,
                FileLength = dataHD.FileLength,
                ChunkOffset = dataHD.ChunkOffset,
                ChunkLenght = dataHD.ChunkLenght,
            };
        }

        public static explicit operator DataHD(DataHD3 dataHD)
        {
            return new DataHD()
            {
                FileNameOffset = dataHD.FileNameOffset,
                FileOffset = dataHD.ChunkOffset * 2048,
                FileLength = dataHD.FileLength,
                ChunkOffset = dataHD.ChunkOffset,
                ChunkLenght = dataHD.ChunkLenght,
            };
        }

        public static explicit operator DataHD(DataHD4 dataHD)
        {
            return new DataHD()
            {
                FileNameOffset = dataHD.FileNameOffset,
                FileOffset = dataHD.ChunkOffset * 2048,
                FileLength = dataHD.FileLength,
                ChunkOffset = dataHD.ChunkOffset,
                ChunkLenght = (int)Math.Ceiling((double)dataHD.FileLength / 2048D),
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataHD2
    {
        public int FileNameOffset;
        public int Unused0;
        public int Unused1;
        public int Unused2;

        public int FileOffset;
        public int FileLength;
        public int ChunkOffset;
        public int ChunkLenght;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataHD3
    {
        public int FileNameOffset;
        public int FileLength;
        public int ChunkOffset;
        public int ChunkLenght;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataHD4
    {
        public int FileNameOffset;
        public int FileLength;
        public int ChunkOffset;
    }
}
