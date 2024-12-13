using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace IRAAS.ImageProcessing;

// TODO: should take in an app-settings object with limits
// -> max input size
// -> max output size
// - to prevent DDoS-style attacks with ridiculous requests
public class WebResponseStream : Stream
{
    private Stream _responseStream;
    private WebResponse _response;
    private readonly IAppSettings _appSettings;
    private MemoryStream _memStream;
    private bool _responseStreamExhausted;
    private long _responseStreamReadBytes;

    public WebResponseStream(
        WebResponse response,
        IAppSettings appSettings
    )
    {
        _response = response;
        _appSettings = appSettings;
        _responseStream = response.GetResponseStream();
        _memStream = new MemoryStream();
    }

    public WebResponseStream(
        Stream stream,
        IAppSettings appSettings
    )
    {
        _memStream = new MemoryStream();
        _responseStream = stream;
        _appSettings = appSettings;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _response?.Dispose();
        _response = null;
        _memStream?.Dispose();
        _memStream = null;
        _responseStream?.Dispose();
        _responseStream = null;
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ReadTo(_memStream.Position + count);
        return _memStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin)
        {
            ReadTo(offset);
        }
        else if (origin == SeekOrigin.End)
        {
            ReadToEnd();
        }
        else if (origin == SeekOrigin.Current)
        {
            ReadTo(_memStream.Position + offset);
        }

        return _memStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length ?? (_length = CalculateLength()).Value;
    private long? _length;

    private long CalculateLength()
    {
        ReadToEnd();
        return _responseStreamReadBytes;
    }

    private void ReadToEnd()
    {
        if (_responseStreamExhausted)
        {
            return;
        }

        var originalPosition = _memStream.Position;
        Seek(_responseStreamReadBytes, SeekOrigin.Begin);
        var read = 0;
        var buffer = Buffer;
        do
        {
            read = Read(buffer, 0, buffer.Length);
            _memStream.Write(buffer, 0, read);
        } while (read > 0);

        _memStream.Position = originalPosition;
        _responseStreamExhausted = true;
        Trim();
    }

    public void Trim()
    {
        _memStream.SetLength((int) Length);
        // _don't_ trim Capacity (on purpose) to avoid the
        // associated reallocation - even though the trim
        // ends up making the capacity smaller, dotnet will
        // allocate a brand new byte array and copy over the
        // existing data.
    }

    private void ReadTo(long position)
    {
        if (position < _responseStreamReadBytes)
        {
            return; // already read there (:
        }

        if (position > _appSettings.MaxInputImageSize)
        {
            throw new NotSupportedException(
                $"Images with sizes > {_appSettings.MaxInputImageSize} bytes are not supported"
            );
        }

        var originalPosition = _memStream.Position;
        _memStream.Seek(_responseStreamReadBytes, SeekOrigin.Begin);
        var toRead = position - _responseStreamReadBytes;
        var totalRead = 0;
        var thisRead = 0;
        var buffer = Buffer;
        do
        {
            var remaining = toRead - totalRead;
            var readNow = remaining > buffer.Length
                ? buffer.Length
                : remaining;
            thisRead = _responseStream.Read(buffer, 0, (int) readNow);
            _memStream.Write(buffer, 0, thisRead);
            totalRead += thisRead;
        } while (thisRead > 0);

        _responseStreamReadBytes += totalRead;
        _memStream.Position = originalPosition;
    }

    private byte[] Buffer => _buffer ?? (_buffer = new byte[32768]);
    private byte[] _buffer;

    public override long Position
    {
        get => _memStream.Position;
        set
        {
            if (_responseStreamReadBytes < value)
            {
                ReadTo((int) value);
            }

            _memStream.Position = value;
        }
    }
}