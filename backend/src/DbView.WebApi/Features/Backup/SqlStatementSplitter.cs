using System.Runtime.CompilerServices;
using System.Text;

namespace DbView.WebApi.Features.Backup
{
    /// <summary>
    /// 流式 SQL 语句拆分器：从 TextReader 逐字符读取，按顶层分号切分语句。
    ///
    /// 相比"整文件读入内存再按分号切"的旧实现，解决三个生产级问题：
    /// 1. 内存 —— 大备份文件不再一次性读入内存（读取 + 拆分成 List 会占用约 3 倍文件体积）；
    /// 2. 正确性 —— 正确跳过 -- 行注释、/* */ 块注释（支持 PostgreSQL 嵌套）、
    ///    PostgreSQL 的 $$...$$ 美元引用，以及字符串/标识符里的分号；
    /// 3. 可执行性 —— 注释内容不会作为独立语句提交给数据库（旧实现会把
    ///    "-- 表: xxx" 当成一条语句执行，多数驱动直接报错）。
    /// </summary>
    public sealed class SqlStatementSplitter
    {
        private enum LexState
        {
            Normal,
            SingleQuote,
            DoubleQuote,
            LineComment,
            BlockComment,
            DollarQuoted
        }

        private readonly TextReader _reader;
        private readonly Stack<int> _pushback = new();
        private readonly char[] _charBuffer = new char[1];
        private bool _eof;

        public SqlStatementSplitter(TextReader reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// 逐条产出可执行 SQL（已剔除注释，不含结尾分号）。
        /// </summary>
        public async IAsyncEnumerable<string> SplitAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var current = new StringBuilder();
            var state = LexState.Normal;
            string? dollarTag = null;
            var blockDepth = 0;

            while (true)
            {
                var read = await ReadAsync(cancellationToken);
                if (read < 0)
                {
                    break;
                }

                var ch = (char)read;

                switch (state)
                {
                    case LexState.Normal:
                        if (ch == '\'') { state = LexState.SingleQuote; current.Append(ch); }
                        else if (ch == '"') { state = LexState.DoubleQuote; current.Append(ch); }
                        else if (ch == '-')
                        {
                            var next = await ReadAsync(cancellationToken);
                            if (next == '-') { state = LexState.LineComment; }
                            else { current.Append(ch); Unread(next); }
                        }
                        else if (ch == '/')
                        {
                            var next = await ReadAsync(cancellationToken);
                            if (next == '*') { state = LexState.BlockComment; blockDepth = 1; }
                            else { current.Append(ch); Unread(next); }
                        }
                        else if (ch == '$')
                        {
                            var tag = await TryReadDollarTagAsync(cancellationToken);
                            if (tag != null)
                            {
                                // 保留美元引用的开标签，否则恢复出来的语句会丢掉 $$ 变成非法 SQL
                                current.Append(tag);
                                dollarTag = tag;
                                state = LexState.DollarQuoted;
                            }
                            else { current.Append(ch); }
                        }
                        else if (ch == ';')
                        {
                            // 块语句（SQLite / MySQL 触发器的 BEGIN ... END; 体内含分号）：
                            // 若 BEGIN 数量多于 END，说明还在块内，不能在此切分。
                            if (IsInsideBlockStatement(current))
                            {
                                current.Append(ch);
                            }
                            else
                            {
                                var stmt = current.ToString().Trim();
                                if (stmt.Length > 0) { yield return stmt; }
                                current.Clear();
                            }
                        }
                        else { current.Append(ch); }
                        break;

                    case LexState.SingleQuote:
                        current.Append(ch);
                        if (ch == '\'')
                        {
                            var next = await ReadAsync(cancellationToken);
                            if (next == '\'') { current.Append('\''); }
                            else { state = LexState.Normal; Unread(next); }
                        }
                        break;

                    case LexState.DoubleQuote:
                        current.Append(ch);
                        if (ch == '"')
                        {
                            var next = await ReadAsync(cancellationToken);
                            if (next == '"') { current.Append('"'); }
                            else { state = LexState.Normal; Unread(next); }
                        }
                        break;

                    case LexState.LineComment:
                        if (ch == '\n') { state = LexState.Normal; }
                        break;

                    case LexState.BlockComment:
                        if (ch == '*')
                        {
                            var next = await ReadAsync(cancellationToken);
                            if (next == '/') { blockDepth--; if (blockDepth == 0) { state = LexState.Normal; } }
                            else { Unread(next); }
                        }
                        else if (ch == '/')
                        {
                            var next = await ReadAsync(cancellationToken);
                            if (next == '*') { blockDepth++; }
                            else { Unread(next); }
                        }
                        break;

                    case LexState.DollarQuoted:
                        if (ch == '$')
                        {
                            var rest = dollarTag!.Substring(1);
                            var readChars = new List<int>();
                            var matched = true;
                            foreach (var expect in rest)
                            {
                                var n = await ReadAsync(cancellationToken);
                                if (n < 0) { matched = false; break; }
                                readChars.Add(n);
                                if (n != expect) { matched = false; break; }
                            }

                            if (matched)
                            {
                                // 保留美元引用的闭合标签
                                current.Append(dollarTag);
                                state = LexState.Normal;
                                dollarTag = null;
                            }
                            else { UnreadAll(readChars); current.Append(ch); }
                        }
                        else { current.Append(ch); }
                        break;
                }
            }

            var tail = current.ToString().Trim();
            if (tail.Length > 0) { yield return tail; }
        }

        /// <summary>
        /// 判断当前累积的语句是否仍处于 BEGIN ... END 块内部。
        ///
        /// SQLite / MySQL 的触发器体形如：
        ///   CREATE TRIGGER t AFTER INSERT ON x BEGIN UPDATE ...; END;
        /// 块内的分号不是语句结束符，若按分号切分会得到半截触发器定义（恢复时语法错误）。
        /// 这里按"独立单词 BEGIN 多于 END"来判断是否仍在块内。
        /// </summary>
        private static bool IsInsideBlockStatement(StringBuilder current)
        {
            // 绝大多数语句（INSERT 等）不含 BEGIN，先做一次快速判断避免无谓扫描
            if (current.Length == 0 || current.ToString().IndexOf("BEGIN", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var beginCount = 0;
            var endCount = 0;
            var text = current.ToString();

            var i = 0;
            while (i < text.Length)
            {
                if (!IsWordChar(text[i]))
                {
                    i++;
                    continue;
                }

                var start = i;
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }

                var word = text.Substring(start, i - start);
                if (string.Equals(word, "BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    beginCount++;
                }
                else if (string.Equals(word, "END", StringComparison.OrdinalIgnoreCase))
                {
                    endCount++;
                }
            }

            return beginCount > endCount;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private async Task<int> ReadAsync(CancellationToken cancellationToken)
        {
            if (_pushback.Count > 0) { return _pushback.Pop(); }
            if (_eof) { return -1; }
            cancellationToken.ThrowIfCancellationRequested();

            // TextReader 只提供 ReadAsync(Memory<char>, CancellationToken) 重载
            var read = await _reader.ReadAsync(_charBuffer.AsMemory(), cancellationToken);
            if (read == 0) { _eof = true; return -1; }
            return _charBuffer[0];
        }

        private void Unread(int c)
        {
            if (c >= 0) { _pushback.Push(c); }
        }

        private void UnreadAll(List<int> chars)
        {
            for (var i = chars.Count - 1; i >= 0; i--)
            {
                Unread(chars[i]);
            }
        }

        /// <summary>
        /// 尝试读取美元引用标签（$$ 或 $tag$）。当前已消费首个 '$'。
        /// 不是合法标签时返回 null，并把已读字符全部推回。
        /// </summary>
        private async Task<string?> TryReadDollarTagAsync(CancellationToken cancellationToken)
        {
            var readChars = new List<int>();
            var c = await ReadAsync(cancellationToken);
            if (c < 0) { return null; }
            readChars.Add(c);

            if (c == '$')
            {
                return "$$";
            }

            var first = (char)c;
            if (first == '_' || char.IsLetter(first))
            {
                while (true)
                {
                    var n = await ReadAsync(cancellationToken);
                    if (n < 0) { break; }
                    readChars.Add(n);
                    var nc = (char)n;
                    if (nc == '$')
                    {
                        var tagBody = new string(readChars.Take(readChars.Count - 1).Select(x => (char)x).ToArray());
                        return "$" + tagBody + "$";
                    }
                    if (nc == '_' || char.IsLetterOrDigit(nc)) { continue; }
                    break;
                }
            }

            UnreadAll(readChars);
            return null;
        }
    }
}
