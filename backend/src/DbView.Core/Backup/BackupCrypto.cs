using System.Security.Cryptography;
using System.Text;

namespace DbView.Core.Backup
{
    /// <summary>
    /// 备份文件加密（AES-256-GCM）。
    ///
    /// 设计要点：
    /// - 密钥不落在代码里、不落在备份目录里，由环境变量 DBVIEW_BACKUP_KEY 提供（Base64 的 32 字节密钥）；
    ///   未配置时不加密（备份仍可用，但元数据会标注"未加密"）。
    /// - 文件格式：[魔数 "DBVBK1"][分块长度 4B][nonce 12B][GCM tag 16B][密文]...，
    ///   每个分块使用独立的随机 nonce（GCM 下绝不可复用），分块长度作为附加认证数据，
    ///   防止分块被截断或调换顺序；任何一处密文被篡改都会在解密时直接抛错。
    /// - 密文位于压缩之后：先 gzip 压缩、再加密落盘，避免明文长度信息泄漏。
    /// </summary>
    public static class BackupCrypto
    {
        /// <summary>存放密钥的环境变量名</summary>
        public const string KeyEnvironmentVariable = "DBVIEW_BACKUP_KEY";

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DBVBK1");
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        /// <summary>单个加密分块的最大明文长度</summary>
        private const int ChunkSize = 1024 * 1024;

        /// <summary>
        /// 读取配置中的密钥；未配置或格式不正确时返回 null（表示不加密）。
        /// </summary>
        public static byte[]? TryGetKey()
        {
            var raw = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                var key = Convert.FromBase64String(raw.Trim());
                return key.Length == KeySize ? key : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>是否已启用加密</summary>
        public static bool IsEnabled => TryGetKey() != null;

        /// <summary>创建加密写入流（自动写入魔数头）</summary>
        public static Stream CreateEncryptStream(Stream output, byte[] key)
        {
            output.Write(Magic, 0, Magic.Length);
            return new GcmChunkStream(output, key, encrypt: true);
        }

        /// <summary>创建解密读取流；魔数不匹配时抛 InvalidDataException</summary>
        public static Stream CreateDecryptStream(Stream input, byte[] key)
        {
            var magic = new byte[Magic.Length];
            ReadExactly(input, magic);
            if (!magic.SequenceEqual(Magic))
            {
                throw new InvalidDataException("备份文件加密头不匹配：密钥不正确或文件已损坏");
            }

            return new GcmChunkStream(input, key, encrypt: false);
        }

        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException("备份文件被截断（加密头不完整）");
                }
                offset += read;
            }
        }
        /// <summary>
        /// 分块 AES-256-GCM 流。每块独立使用随机 nonce，块长度写入帧头并作为附加认证数据。
        /// </summary>
        private sealed class GcmChunkStream : Stream
        {
            private readonly Stream _inner;
            private readonly bool _encrypt;
            private readonly AesGcm _aes;
            private readonly MemoryStream _buffer = new();

            /// <summary>已解密但尚未完全消费的数据（Read 必须遵守 count 限制，故需跨次缓存）</summary>
            private byte[] _pending = Array.Empty<byte>();
            private int _pendingOffset;

            public GcmChunkStream(Stream inner, byte[] key, bool encrypt)
            {
                _inner = inner;
                _encrypt = encrypt;
                _aes = new AesGcm(key, TagSize);
            }

            public override bool CanRead => !_encrypt;
            public override bool CanSeek => false;
            public override bool CanWrite => _encrypt;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (!_encrypt)
                {
                    throw new NotSupportedException("解密流不支持写入");
                }

                _buffer.Write(buffer);
                if (_buffer.Length >= ChunkSize)
                {
                    FlushChunk();
                }
            }

            private void FlushChunk()
            {
                if (_buffer.Length == 0)
                {
                    return;
                }

                var plain = _buffer.ToArray();
                _buffer.SetLength(0);

                var nonce = RandomNumberGenerator.GetBytes(NonceSize);
                var cipher = new byte[plain.Length];
                var tag = new byte[TagSize];
                var aad = BitConverter.GetBytes(plain.Length);

                _aes.Encrypt(nonce, plain, cipher, tag, aad);

                _inner.Write(aad, 0, aad.Length);
                _inner.Write(nonce, 0, nonce.Length);
                _inner.Write(tag, 0, tag.Length);
                _inner.Write(cipher, 0, cipher.Length);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_encrypt)
                {
                    throw new NotSupportedException("加密流不支持读取");
                }

                // 先把上次剩下的数据消费掉（Read 的契约：最多返回 count 字节）
                if (_pendingOffset < _pending.Length)
                {
                    var avail = _pending.Length - _pendingOffset;
                    var n = Math.Min(count, avail);
                    Array.Copy(_pending, _pendingOffset, buffer, offset, n);
                    _pendingOffset += n;
                    return n;
                }

                var aad = new byte[sizeof(int)];
                if (!TryReadExactly(_inner, aad))
                {
                    return 0;
                }

                var length = BitConverter.ToInt32(aad, 0);
                if (length < 0 || length > ChunkSize)
                {
                    throw new InvalidDataException("备份文件分块长度非法，文件可能已损坏");
                }

                var nonce = new byte[NonceSize];
                var tag = new byte[TagSize];
                var cipher = new byte[length];
                if (!TryReadExactly(_inner, nonce) || !TryReadExactly(_inner, tag) || !TryReadExactly(_inner, cipher))
                {
                    throw new EndOfStreamException("备份文件被截断");
                }

                var plain = new byte[length];
                // AesGcm.Decrypt 的参数顺序是 (nonce, ciphertext, tag, plaintext, associatedData)
                _aes.Decrypt(nonce, cipher, tag, plain, aad);

                var take = Math.Min(count, plain.Length);
                Array.Copy(plain, 0, buffer, offset, take);
                if (take < plain.Length)
                {
                    // 剩余部分留给下一次 Read
                    _pending = plain;
                    _pendingOffset = take;
                }
                return take;
            }

            private static bool TryReadExactly(Stream stream, byte[] buffer)
            {
                var offset = 0;
                while (offset < buffer.Length)
                {
                    var read = stream.Read(buffer, offset, buffer.Length - offset);
                    if (read <= 0)
                    {
                        return false;
                    }
                    offset += read;
                }
                return true;
            }

            public override void Flush()
            {
                if (_encrypt)
                {
                    FlushChunk();
                }
                _inner.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                Flush();
                return Task.CompletedTask;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_encrypt)
                    {
                        FlushChunk();
                    }
                    _aes.Dispose();
                    _buffer.Dispose();

                    // 必须关闭底层流（文件句柄），否则写入完成后文件仍被占用，
                    // 后续的原子改名 / 读取会因"文件被另一进程使用"而失败。
                    _inner.Dispose();
                }
                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
