using System;
using System.Collections.Generic;
using System.IO;

namespace IRAAS.Security;

public class LimitedMemoryStream : Stream
{
    private readonly long _maxSize;
    private readonly MemoryStream _actual;

    public LimitedMemoryStream(long maxSize)
    {
        _maxSize = maxSize;
        _actual = new MemoryStream();
    }

    public override void Flush()
    {
        _actual.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _actual.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var final = PositionCalculators[origin](this, offset);
        if (final > _maxSize)
        {
            LimitExceeded();
        }

        return _actual.Seek(offset, origin);
    }

    private static readonly Dictionary<SeekOrigin, Func<LimitedMemoryStream, long, long>>
        PositionCalculators = new Dictionary<SeekOrigin, Func<LimitedMemoryStream, long, long>>()
        {
            [SeekOrigin.Begin] = (s, count) => count,
            [SeekOrigin.Current] = (s, count) => s.Position + count,
            [SeekOrigin.End] = (s, count) => s.Length - count
        };

    public override void SetLength(long value)
    {
        _actual.SetLength(value);
    }

    private void LimitExceeded()
    {
        throw new NotSupportedException(
            $"Buffer may not exceed {_maxSize} bytes"
        );
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Position + count > _maxSize)
        {
            LimitExceeded();
        }

        _actual.Write(buffer, offset, count);
    }

    public override bool CanRead => _actual.CanRead;
    public override bool CanSeek => _actual.CanSeek;
    public override bool CanWrite => _actual.CanWrite;
    public override long Length => _actual.Length;

    public override long Position
    {
        get => _actual.Position;
        set => _actual.Position = value;
    }

    public byte[] ToArray()
    {
        return _actual.ToArray();
    }
}