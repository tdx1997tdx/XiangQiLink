using System;
using System.Collections.Generic;

namespace VinXiangQi
{
    // 按 FEN 生成合法着法（用于强制变招时给 UCI 引擎发 searchmoves）
    // 坐标和引擎一致：列 a~i 从左到右，行 0~9 从红方底线到黑方底线，着法形如 h2e2
    public static class XiangQiRules
    {
        static readonly int[,] OrthDirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        static readonly int[,] HorseDirs = { { 1, 2 }, { -1, 2 }, { 1, -2 }, { -1, -2 }, { 2, 1 }, { -2, 1 }, { 2, -1 }, { -2, -1 } };
        static readonly int[,] DiagDirs = { { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } };

        // fen 只需要棋盘部分和走子方（"w"/"b"），后面的字段可有可无
        public static List<string> LegalMoves(string fen)
        {
            char[,] board;
            bool redToMove;
            Parse(fen, out board, out redToMove);
            var result = new List<string>();
            foreach (var m in PseudoMoves(board, redToMove))
            {
                char captured = board[m[2], m[3]];
                board[m[2], m[3]] = board[m[0], m[1]];
                board[m[0], m[1]] = '\0';
                if (!InCheck(board, redToMove))
                {
                    result.Add(ToMove(m));
                }
                board[m[0], m[1]] = board[m[2], m[3]];
                board[m[2], m[3]] = captured;
            }
            return result;
        }

        static void Parse(string fen, out char[,] board, out bool redToMove)
        {
            board = new char[9, 10];
            string[] parts = fen.Trim().Split(' ');
            string[] rows = parts[0].Split('/');
            if (rows.Length != 10) throw new ArgumentException("FEN 行数不是 10: " + fen);
            for (int i = 0; i < 10; i++)
            {
                int rank = 9 - i;
                int file = 0;
                foreach (char c in rows[i])
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0';
                    }
                    else
                    {
                        if (file > 8) throw new ArgumentException("FEN 列数超出: " + fen);
                        board[file, rank] = c;
                        file++;
                    }
                }
            }
            redToMove = parts.Length < 2 || parts[1] != "b";
        }

        static string ToMove(int[] m)
        {
            return "" + (char)('a' + m[0]) + m[1] + (char)('a' + m[2]) + m[3];
        }

        static bool IsRed(char p) { return p >= 'A' && p <= 'Z'; }
        static bool OnBoard(int x, int y) { return x >= 0 && x < 9 && y >= 0 && y < 10; }
        static bool InPalace(int x, int y, bool red) { return x >= 3 && x <= 5 && (red ? y >= 0 && y <= 2 : y >= 7 && y <= 9); }
        static bool OwnSide(int y, bool red) { return red ? y <= 4 : y >= 5; }

        // 目标格可以走（空格或对方棋子）
        static bool CanLand(char[,] b, int x, int y, bool red)
        {
            char t = b[x, y];
            return t == '\0' || IsRed(t) != red;
        }

        static IEnumerable<int[]> PseudoMoves(char[,] b, bool red)
        {
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    char p = b[x, y];
                    if (p == '\0' || IsRed(p) != red) continue;
                    switch (char.ToLower(p))
                    {
                        case 'r':
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = x + OrthDirs[d, 0], ny = y + OrthDirs[d, 1];
                                while (OnBoard(nx, ny))
                                {
                                    if (b[nx, ny] == '\0')
                                    {
                                        yield return new[] { x, y, nx, ny };
                                    }
                                    else
                                    {
                                        if (IsRed(b[nx, ny]) != red) yield return new[] { x, y, nx, ny };
                                        break;
                                    }
                                    nx += OrthDirs[d, 0]; ny += OrthDirs[d, 1];
                                }
                            }
                            break;
                        case 'c':
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = x + OrthDirs[d, 0], ny = y + OrthDirs[d, 1];
                                bool screened = false;
                                while (OnBoard(nx, ny))
                                {
                                    if (!screened)
                                    {
                                        if (b[nx, ny] == '\0') yield return new[] { x, y, nx, ny };
                                        else screened = true;
                                    }
                                    else if (b[nx, ny] != '\0')
                                    {
                                        if (IsRed(b[nx, ny]) != red) yield return new[] { x, y, nx, ny };
                                        break;
                                    }
                                    nx += OrthDirs[d, 0]; ny += OrthDirs[d, 1];
                                }
                            }
                            break;
                        case 'n':
                            for (int d = 0; d < 8; d++)
                            {
                                int dx = HorseDirs[d, 0], dy = HorseDirs[d, 1];
                                int nx = x + dx, ny = y + dy;
                                if (!OnBoard(nx, ny)) continue;
                                int lx = x + (Math.Abs(dx) == 2 ? dx / 2 : 0);
                                int ly = y + (Math.Abs(dy) == 2 ? dy / 2 : 0);
                                if (b[lx, ly] != '\0') continue; // 蹩马腿
                                if (CanLand(b, nx, ny, red)) yield return new[] { x, y, nx, ny };
                            }
                            break;
                        case 'b':
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = x + 2 * DiagDirs[d, 0], ny = y + 2 * DiagDirs[d, 1];
                                if (!OnBoard(nx, ny) || !OwnSide(ny, red)) continue; // 象不过河
                                if (b[x + DiagDirs[d, 0], y + DiagDirs[d, 1]] != '\0') continue; // 塞象眼
                                if (CanLand(b, nx, ny, red)) yield return new[] { x, y, nx, ny };
                            }
                            break;
                        case 'a':
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = x + DiagDirs[d, 0], ny = y + DiagDirs[d, 1];
                                if (!InPalace(nx, ny, red)) continue;
                                if (CanLand(b, nx, ny, red)) yield return new[] { x, y, nx, ny };
                            }
                            break;
                        case 'k':
                            for (int d = 0; d < 4; d++)
                            {
                                int nx = x + OrthDirs[d, 0], ny = y + OrthDirs[d, 1];
                                if (!InPalace(nx, ny, red)) continue;
                                if (CanLand(b, nx, ny, red)) yield return new[] { x, y, nx, ny };
                            }
                            break;
                        case 'p':
                            {
                                int fwd = red ? 1 : -1;
                                if (OnBoard(x, y + fwd) && CanLand(b, x, y + fwd, red)) yield return new[] { x, y, x, y + fwd };
                                if (!OwnSide(y, red)) // 过河兵可以横走
                                {
                                    if (OnBoard(x - 1, y) && CanLand(b, x - 1, y, red)) yield return new[] { x, y, x - 1, y };
                                    if (OnBoard(x + 1, y) && CanLand(b, x + 1, y, red)) yield return new[] { x, y, x + 1, y };
                                }
                            }
                            break;
                    }
                }
            }
        }

        // red 一方的将（帅）是否被将军，包括将帅照面
        static bool InCheck(char[,] b, bool red)
        {
            int kx = -1, ky = -1, ox = -1, oy = -1;
            char myKing = red ? 'K' : 'k', oppKing = red ? 'k' : 'K';
            for (int x = 3; x <= 5; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (b[x, y] == myKing) { kx = x; ky = y; }
                    else if (b[x, y] == oppKing) { ox = x; oy = y; }
                }
            }
            if (kx == -1) return true;
            if (ox == kx)
            {
                bool blocked = false;
                for (int y = Math.Min(ky, oy) + 1; y < Math.Max(ky, oy); y++)
                {
                    if (b[kx, y] != '\0') { blocked = true; break; }
                }
                if (!blocked) return true;
            }
            foreach (var m in PseudoMoves(b, !red))
            {
                if (m[2] == kx && m[3] == ky) return true;
            }
            return false;
        }
    }
}
