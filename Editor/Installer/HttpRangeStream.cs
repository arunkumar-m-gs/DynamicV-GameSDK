using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DynamicV.GameSDK.Installer
{
    // Read-only seekable view of a remote file via HTTP Range requests, so ZipArchive can read
    // the zip's index and pull out individual entries without downloading the whole archive.
    // Blocking I/O: only use from a background thread.
    internal sealed class HttpRangeStream : Stream
    {
        private const int BlockSize = 4 * 1024 * 1024;

        private readonly HttpClient _http;
        private readonly Uri _url;
        private readonly long _length;
        private long _position;
        private byte[] _block = Array.Empty<byte>();
        private long _blockStart = -1;

        public HttpRangeStream(HttpClient http, Uri url, long length)
        {
            _http = http;
            _url = url;
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (count > 0 && _position < _length)
            {
                if (_blockStart < 0 || _position < _blockStart || _position >= _blockStart + _block.Length)
                    Fetch(_position);

                var inBlock = (int)(_position - _blockStart);
                var n = Math.Min(count, _block.Length - inBlock);
                Buffer.BlockCopy(_block, inBlock, buffer, offset, n);
                _position += n;
                offset += n;
                count -= n;
                total += n;
            }
            return total;
        }

        private void Fetch(long start)
        {
            var end = Math.Min(start + BlockSize, _length) - 1;
            using (var req = new HttpRequestMessage(HttpMethod.Get, _url))
            {
                req.Headers.Range = new RangeHeaderValue(start, end);
                using (var resp = _http.SendAsync(req).GetAwaiter().GetResult())
                {
                    resp.EnsureSuccessStatusCode();
                    _block = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    _blockStart = start;
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: _position = offset; break;
                case SeekOrigin.Current: _position += offset; break;
                default: _position = _length + offset; break;
            }
            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
