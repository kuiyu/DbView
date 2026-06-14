

using System.Diagnostics;


//
// 摘要:
//     类似 Twitter Snowflake(41 + 10 + 12) 算法的 Id 生成器。 格式：{32 位时间戳, 0-10 位机器码, 0-12 位递增系列号}。
//     注意：程序启动前请确保系统时间正确。
public class IdGenerate
    {
        private readonly long _machineId = 0L;

        private readonly byte _machineIdBits = 0;

        private readonly byte _sequenceBits = 0;

        private readonly long _maxSequence = 0L;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private readonly object _lockObject = new object();

        private long _sequence = 0L;

        private long _lastTimestamp = 0L;

        private readonly long OffsetTicks = DateTime.UtcNow.Ticks - new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        //
        // 摘要:
        //     The constructor of IdGen.
        //
        // 参数:
        //   machineId:
        //     当前机器码
        //
        //   machineIdBits:
        //     机器码位数（0-10之间）
        //
        //   sequenceBits:
        //     序列号位数（0-20之间） 注意： 1. 并发量越大，此值也要越大，例如：10 可以 1 秒内生成 2^10=1024 个 ID。 2. 每台机器此参数务必相同。
        public IdGenerate(byte machineId = 0, byte machineIdBits = 0, byte sequenceBits = 10)
        {
            if (sequenceBits > 20)
            {
                throw new ArgumentOutOfRangeException("sequenceBits", "序列号不能超过 20 位。");
            }

            if (machineIdBits > 10)
            {
                throw new ArgumentOutOfRangeException("machineIdBits", "机器码不能超过 10 位。");
            }

            _machineIdBits = machineIdBits;
            _sequenceBits = sequenceBits;
            _maxSequence = GetMaxOfBits(_sequenceBits);
            if (machineId > 0)
            {
                long maxOfBits = GetMaxOfBits(machineId);
                if (machineId > maxOfBits)
                {
                    throw new ArgumentOutOfRangeException("machineId", $"机器码不能大于 {maxOfBits}。");
                }

                _machineId = machineId;
            }
        }

        private long GetTimestampNow()
        {
            return (OffsetTicks + _stopwatch.Elapsed.Ticks) / 10000000;
        }

        private long GetNextTimestamp()
        {
            long timestampNow = GetTimestampNow();
            if (timestampNow < _lastTimestamp)
            {
                throw new Exception("新的时间戳比旧的小，请检查系统时间。");
            }

            while (timestampNow == _lastTimestamp)
            {
                if (_sequence < _maxSequence)
                {
                    _sequence++;
                    return timestampNow;
                }

                Thread.Sleep(0);
                timestampNow = GetTimestampNow();
            }

            _sequence = 0L;
            return timestampNow;
        }

        //
        // 摘要:
        //     Generate a new sequence id.
        //
        // 返回结果:
        //     The generated id.
        public long NewId()
        {
            return NewSequenceId();
        }

        private long GetMaxOfBits(byte bits)
        {
            return (1L << (int)bits) - 1;
        }

        private int GetBitsLength(long number)
        {
            return (int)Math.Log(number, 2.0) + 1;
        }

        //
        // 摘要:
        //     生成新的ID
        //
        // 返回结果:
        //     ID
        private long NewSequenceId()
        {
            lock (_lockObject)
            {
                _lastTimestamp = GetNextTimestamp();
                int num = _machineIdBits + _sequenceBits;
                int sequenceBits = _sequenceBits;
                return (_lastTimestamp << num) | (_machineId << sequenceBits) | _sequence;
            }
        }
    }



