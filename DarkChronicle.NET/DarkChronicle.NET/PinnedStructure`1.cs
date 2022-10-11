namespace DarkChronicle;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
///     Allows for an efficient and reusable struct reader-writer using an internal buffer.
/// </summary>
/// <remarks>
///     The same internal buffer is used to read and write, so this class is not suitable for concurrent read write.
///     The caller is responsible to flush the underlying stream after a write operation.
///     Forgetting to call <see cref="PinnedStructure{T}.Dispose"/> can cause memory leaks as unmanaged memory is used when allocation the structure <typeparamref name="T"/>.
/// </remarks>
/// <typeparam name="T">The structure type.</typeparam>
[DebuggerTypeProxy(typeof(PinnedStructure<>.DebugView))]
public sealed class PinnedStructure<T> : IDisposable where T : struct
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly byte[] _buffer;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly GCHandle _handle;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _disposed;

    /// <summary>
    ///     Gets the size of the buffer.
    /// </summary>
    public int Size => _buffer.Length;

    /// <summary>
    ///     Initializes a new <see cref="PinnedStructure{T}"/> instance.
    /// </summary>
    public PinnedStructure()
    {
        _buffer = new byte[Marshal.SizeOf(typeof(T))];
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
    }

    /// <summary>
    ///     Reads the structure <typeparamref name="T"/> from the stream.
    /// </summary>
    /// <param name="stream">The stream from which to read.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="IOException">The stream does not contains enough data.</exception>
    /// <exception cref="ObjectDisposedException"></exception>
    public T Read(Stream stream)
        => Read(stream, out _);

    /// <summary>
    ///     Reads the structure <typeparamref name="T"/> from the stream.
    /// </summary>
    /// <param name="stream">The stream from which to read.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="IOException">The stream does not contains enough data.</exception>
    /// <exception cref="ObjectDisposedException"></exception>
    public T Read(Stream stream, out int bytesRead)
        => TryRead(stream, out var structure, out bytesRead) ? structure : throw new IOException("The stream does not contains enough data.");

    /// <summary>
    ///     Attemts to read the structure <typeparamref name="T"/> from the stream when there are enough bytes aviable.
    /// </summary>
    /// <param name="stream">The stream from which to read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="IOException">The stream does not contains enough data.</exception>
    /// <exception cref="ObjectDisposedException"></exception>
    public bool TryRead(Stream stream, out T structure)
        => TryRead(stream, out structure, out _);

    /// <summary>
    ///     Attemts to read the structure <typeparamref name="T"/> from the stream when there are enough bytes aviable.
    /// </summary>
    /// <param name="stream">The stream from which to read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="IOException">The stream does not contains enough data.</exception>
    /// <exception cref="ObjectDisposedException"></exception>
    public bool TryRead(Stream stream, out T structure, out int bytesRead)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        int i = stream.Read(_buffer, 0, _buffer.Length);
        bytesRead = i;

        if (i == _buffer.Length)
        {
            structure = Marshal.PtrToStructure<T>(_handle.AddrOfPinnedObject());
            return true;
        }

        structure = default;
        return false;
    }

    /// <summary>
    ///     Writes the structure <typeparamref name="T"/> to the stream.
    /// </summary>
    /// <param name="stream">The stream in which to write.</param>
    /// <param name="structure">The structure to write.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ObjectDisposedException"></exception>
    public void Write(Stream stream, T structure)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        Marshal.StructureToPtr(structure, _handle.AddrOfPinnedObject(), true);
        stream.Write(_buffer, 0, _buffer.Length);
    }

    /// <summary>
    ///     Free the pinned structure.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _handle.Free();
            _disposed = true;
        }
    }

    private sealed class DebugView
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly PinnedStructure<T> _instance;

        // public Type Type => typeof(T);

        public byte[] Buffer => _instance._buffer;
        public GCHandle Handle => _instance._handle;
        public int Size => _instance.Size;

        public DebugView(PinnedStructure<T> instance)
        {
            _instance = instance;
        }
    }
}

/// <summary>
///     Reusable binary string stream reader.
/// </summary>
/// <remarks>
///     The underlying stream expands whenever possible.
/// </remarks>
[DebuggerTypeProxy(typeof(DebugView))]
public sealed class BinaryStringReader : IDisposable
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly byte[] _buffer = new byte[1];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly MemoryStream _memoryStream;

    /// <summary>
    ///     Initializes a new <see cref="BinaryStringReader"/> instance.
    /// </summary>
    public BinaryStringReader() => _memoryStream = new(capacity: 42);

    /// <summary>
    ///     Initializes a new <see cref="BinaryStringReader"/> instance.
    /// </summary>
    public BinaryStringReader(int capacity) => _memoryStream = new(capacity);

    /// <summary>
    ///     Initializes a new <see cref="BinaryStringReader"/> instance.
    /// </summary>
    public BinaryStringReader(byte[] fixedBuffer) => _memoryStream = new(fixedBuffer, 0, fixedBuffer.Length, writable: true, publiclyVisible: true);

    /// <summary>
    ///     Initializes a new <see cref="BinaryStringReader"/> instance.
    /// </summary>
    public BinaryStringReader(byte[] fixedBuffer, int index, int count) => _memoryStream = new(fixedBuffer, index, count, writable: true, publiclyVisible: true);

    /// <summary>
    ///     Returns true if a byte has been read and the stream have advanced 1 byte.
    /// </summary>
    public bool Read(Stream stream, out bool nullTerminator)
    {
        if (stream.Read(_buffer, 0, 1) == 0)
        {
            nullTerminator = false;
            return false;
        }

        if (_buffer[0] == 0)
        {
            nullTerminator = true;
        }
        else
        {
            nullTerminator = false;
            _memoryStream.Write(_buffer, 0, 1);
        }

        return true;
    }

    /// <summary>
    ///     Reads a null terminated string from the stream.
    /// </summary>
    public string? ReadNullTerminatedString(Stream stream, Encoding encoding) => ReadNullTerminatedString(stream, encoding, out _);

    /// <summary>
    ///     Reads a null terminated string from the stream.
    /// </summary>
    public string? ReadNullTerminatedString(Stream stream, Encoding encoding, out int bytesRead)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (encoding is null) throw new ArgumentNullException(nameof(encoding));

        _memoryStream.Position = 0;

        int i = 0;
        while (true)
        {
            if (stream.Read(_buffer, 0, 1) == 0)
            {
                break;
            }

            i++;

            // null terminated
            if (_buffer[0] == 0)
            {
                break;
            }

            _memoryStream.Write(_buffer, 0, 1);
        }

        bytesRead = i;

        if (i == 0) return null;

        var len = (int)_memoryStream.Length;

        // only one byte (\0) has been read
        if (i == 1 && len == 0) return string.Empty;

        return encoding.GetString(_memoryStream.GetBuffer(), 0, len);
    }

    /// <summary>
    ///     Reads a null terminated byte array from the stream.
    /// </summary>
    public byte[] ReadNullTerminatedArray(Stream stream) => ReadNullTerminatedArray(stream, out _);

    /// <summary>
    ///     Reads a null terminated byte array from the stream.
    /// </summary>
    public byte[] ReadNullTerminatedArray(Stream stream, out int bytesRead)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        _memoryStream.Position = 0;

        int i = 0;
        while (true)
        {
            if (stream.Read(_buffer, 0, 1) == 0)
            {
                break;
            }

            i++;

            // null terminated
            if (_buffer[0] == 0)
            {
                break;
            }

            _memoryStream.Write(_buffer, 0, 1);
        }

        bytesRead = i;
        return _memoryStream.ToArray();
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="BinaryStringReader"/>.
    /// </summary>
    public void Dispose() => _memoryStream.Dispose();

    private sealed class DebugView
    {
        private readonly BinaryStringReader _instance;

        public byte[] Buffer => _instance._memoryStream.GetBuffer();

        public DebugView(BinaryStringReader instance)
        {
            _instance = instance;
        }
    }
}
