namespace DarkChronicle;

using System;
using System.IO;
using DiscUtils.Streams;

internal sealed class SafeStreamWrapper : Stream
{
    private readonly Stream _Stream;
    private readonly int _Offset;
    private readonly int _Lenght;
    private readonly bool _DisposeOnClose;

    /// <inheritdoc />
    public override bool CanRead => _Stream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _Stream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _Stream.CanWrite;

    /// <inheritdoc />
    public override long Length => _Lenght;

    /// <inheritdoc />
    public override long Position
    {
        get => _Stream.Position - _Offset;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            _Stream.Position = _Offset + value;
        }
    }

    public SafeStreamWrapper(SparseStream stream, int offset, int lenght, bool disposeOnClose)
    {
        _Stream = stream;
        _Offset = offset;
        _Lenght = lenght;
        _Stream.Position = offset;
        _DisposeOnClose = disposeOnClose;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        _Stream.Flush();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position > Length)
            return 0;

        if (Position + count > Length)
        {
            count = (int)Length - (int)Position;
        }

        return _Stream.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Position + count > Length)
        {
            count = (int)Length;
        }

        _Stream.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin: Position = offset; break;
            case SeekOrigin.Current: Position += offset; break;
            case SeekOrigin.End: Position = Position + Length + offset; break;
            default: throw new ArgumentOutOfRangeException(nameof(origin));
        }

        return Position;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_DisposeOnClose)
            {
                _Stream.Dispose();
            }
        }
    }
}
