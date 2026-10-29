using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VinXiangQi
{
    // 强制变招 + 候选招列表
    // - 列表只保留当前局面的候选招：首选 + 每次变招的结果；局面一变就清空
    // - 点「强制变招」：禁掉当前这步，在剩下的合法着法里重新算（重新计时）
    // - 默认选中正在计算的那一行；选中前面某一行后，立即出招或到时间出招都按选中的走
    public partial class Mainform
    {
        class Candidate
        {
            public string Move = "";      // 引擎坐标，如 h2e2
            public string Ponder = "";    // 预测对方应着（pv 第二步）
            public string Pv = "";
            public string Score = "";
            public string Depth = "";
            public string Source = "引擎";
            public bool Finished = false;
            public bool Banned = false;
        }

        readonly object CandidateLock = new object();
        List<Candidate> Candidates = new List<Candidate>();
        // 候选招所属局面（发给引擎的 FEN）和对应的棋盘
        string CandidateFen = "";
        string[,] CandidateBoard = null;
        List<string> CandidateBanned = new List<string>();
        // -1 表示跟随正在计算的（最后一行）
        int SelectedCandidate = -1;
        bool SuppressCandidateSelectEvent = false;

        // 新一轮分析开始（新局面），清空列表
        void CandidatesNewPosition(string fen)
        {
            lock (CandidateLock)
            {
                CandidateFen = fen;
                CandidateBoard = (string[,])EngineAnalyzingBoard.Clone();
                Candidates.Clear();
                CandidateBanned.Clear();
                SelectedCandidate = -1;
            }
            RefreshCandidateView();
        }

        // 后台思考命中：引擎接着算的就是这个局面，换成新列表并加一行「计算中」
        void CandidatesPonderHit(string fen)
        {
            CandidatesNewPosition(fen);
            lock (CandidateLock)
            {
                Candidates.Add(new Candidate());
            }
            RefreshCandidateView();
        }

        // 开局库 / 云库直接给出的着法
        void CandidateAddBookMove(string fen, string move, string score, string source)
        {
            lock (CandidateLock)
            {
                if (fen != CandidateFen) return;
                Candidates.Add(new Candidate { Move = move, Score = score, Source = source, Finished = true });
                SelectedCandidate = -1;
            }
            RefreshCandidateView();
        }

        // 在当前局面上启动引擎计算，并在列表末尾加一行「计算中」
        void StartEngineSearchForCandidate(string fen)
        {
            List<string> searchMoves = null;
            List<string> banMoves = null;
            lock (CandidateLock)
            {
                if (fen == CandidateFen)
                {
                    Candidates.Add(new Candidate());
                    SelectedCandidate = -1;
                    if (CandidateBanned.Count > 0)
                    {
                        banMoves = new List<string>(CandidateBanned);
                        searchMoves = XiangQiRules.LegalMoves(fen).Where(m => !CandidateBanned.Contains(m)).ToList();
                    }
                }
            }
            RefreshCandidateView();
            Engine.StartAnalyze(fen, Settings.EngineStepTime, Settings.EngineDepth, searchMoves, banMoves);
        }

        // 引擎当前输出属于哪一次计算：队列头就是正在算的局面；在 SkipList 里的是被叫停、结果要丢弃的那次
        bool EngineOutputIsCurrentCandidate()
        {
            try
            {
                if (Engine == null || Engine.AnalyzeQueue.Count == 0) return false;
                string head = Engine.AnalyzeQueue.Peek();
                return head == CandidateFen && !Engine.SkipList.Contains(head);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 引擎 info 输出（带 pv）时调用：刷新「计算中」那一行；选中了别的行时，箭头改为显示选中的着法
        void CandidateOnInfo(Dictionary<string, string> infos)
        {
            if (BackgroundAnalyzing || !infos.ContainsKey("pv")) return;
            bool changed = false;
            lock (CandidateLock)
            {
                if (Candidates.Count > 0 && EngineOutputIsCurrentCandidate())
                {
                    var cur = Candidates[Candidates.Count - 1];
                    if (!cur.Finished)
                    {
                        string[] moves = infos["pv"].Split(' ');
                        cur.Move = moves[0];
                        cur.Ponder = moves.Length > 1 ? moves[1] : "";
                        cur.Pv = infos["pv"];
                        if (infos.ContainsKey("score")) cur.Score = infos["score"];
                        if (infos.ContainsKey("depth")) cur.Depth = infos["depth"];
                        changed = true;
                    }
                }
                ApplySelectedArrow();
            }
            if (changed) RefreshCandidateView();
        }

        // 选中的不是最后一行时，把棋盘箭头换成选中的着法（调用方持有 CandidateLock）
        void ApplySelectedArrow()
        {
            if (SelectedCandidate < 0 || SelectedCandidate >= Candidates.Count - 1) return;
            var sel = Candidates[SelectedCandidate];
            if (sel.Move.Length != 4) return;
            bool redSide = RedSide;
            BestMove = new ChessMove(Utils.Move2Point(sel.Move.Substring(0, 2), redSide), Utils.Move2Point(sel.Move.Substring(2, 2), redSide));
            if (sel.Ponder.Length == 4)
            {
                PonderMove = new ChessMove(Utils.Move2Point(sel.Ponder.Substring(0, 2), redSide), Utils.Move2Point(sel.Ponder.Substring(2, 2), redSide));
            }
            else
            {
                PonderMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
            }
        }

        // 引擎给出 bestmove 时调用：标记计算完成；选中了前面的行就换成选中的着法
        string CandidateOnBestMove(string sourceFen, string bestMove, ref string ponderMove)
        {
            string result = bestMove;
            lock (CandidateLock)
            {
                if (sourceFen != CandidateFen || Candidates.Count == 0) return bestMove;
                var cur = Candidates[Candidates.Count - 1];
                if (!cur.Finished)
                {
                    cur.Finished = true;
                    string bm = bestMove.Trim('\0', ' ');
                    if (bm.Length == 4 && bm != cur.Move)
                    {
                        cur.Move = bm;
                        cur.Ponder = ponderMove.Trim('\0', ' ');
                        cur.Pv = bm + (cur.Ponder != "" ? " " + cur.Ponder : "");
                    }
                }
                if (SelectedCandidate >= 0 && SelectedCandidate < Candidates.Count - 1)
                {
                    var sel = Candidates[SelectedCandidate];
                    if (sel.Move.Length == 4)
                    {
                        result = sel.Move;
                        ponderMove = sel.Ponder;
                        LogHelper.Log("候选招: 按选中的第 " + (SelectedCandidate + 1) + " 行出招 " + sel.Move + "（引擎最终给的是 " + bestMove + "）");
                    }
                }
            }
            RefreshCandidateView();
            return result;
        }

        private void button_force_change_Click(object sender, EventArgs e)
        {
            if (Engine == null || !Engine.Initialized)
            {
                DisplayStatus("强制变招：引擎还没加载");
                return;
            }
            if (BackgroundAnalyzing)
            {
                DisplayStatus("强制变招：后台思考中，等对方走棋后再变招");
                return;
            }
            string fen;
            bool searching;
            lock (CandidateLock)
            {
                fen = CandidateFen;
                if (fen == "" || Candidates.Count == 0)
                {
                    DisplayStatus("强制变招：当前没有在分析的局面");
                    return;
                }
                searching = EngineOutputIsCurrentCandidate();
                if (!searching && Settings.AutoGo)
                {
                    DisplayStatus("强制变招：这一步已经走出去了，没法再变");
                    return;
                }
                var cur = Candidates[Candidates.Count - 1];
                if (cur.Move.Length != 4)
                {
                    DisplayStatus("强制变招：引擎还没给出着法，稍等一下再点");
                    return;
                }
                List<string> legal;
                try
                {
                    legal = XiangQiRules.LegalMoves(fen);
                }
                catch (Exception ex)
                {
                    LogHelper.Log("强制变招：生成合法着法失败 " + fen + " " + ex.Message);
                    DisplayStatus("强制变招：局面解析失败");
                    return;
                }
                var remaining = legal.Where(m => m != cur.Move && !CandidateBanned.Contains(m)).ToList();
                if (remaining.Count == 0)
                {
                    DisplayStatus("强制变招：除了已禁的着法，没有其他合法着法了");
                    return;
                }
                cur.Banned = true;
                cur.Finished = true;
                if (!CandidateBanned.Contains(cur.Move)) CandidateBanned.Add(cur.Move);
                LogHelper.Log("强制变招: 禁 " + string.Join(" ", CandidateBanned) + "，剩 " + remaining.Count + " 步");
            }
            if (searching)
            {
                // 叫停当前计算，并丢弃它的 bestmove（不能用 StopAnalyzeAndSkip，它是异步的，会和新计算抢顺序）
                Engine.SkipList.Add(fen);
                Engine.StopAnalyze();
            }
            else
            {
                // 引擎已经空闲：新计算的 bestmove 会让计数减一，这里补上
                EngineAnalyzeCount++;
            }
            DisplayStatus("强制变招：重新计算");
            StartEngineSearchForCandidate(fen);
        }

        // 最近一次确认的棋盘变化是不是我方走的（是的话现在轮到对方）
        bool LastMoveWasMine = false;

        // 「立即出招」在引擎空闲时：中局一键连线默认等对方先走，轮到我方时会一直干等，这里直接按轮到我方开算
        void AnalyzeCurrentBoardAsMyTurn()
        {
            // 自动走棋时 ExpectedMove 非空 = 已经替我方点了这步、还在等画面确认
            if (LastMoveWasMine || (Settings.AutoGo && ExpectedMove != ""))
            {
                DisplayStatus("立即出招：我方刚走完，现在轮到对方");
                return;
            }
            string[,] board = LastBoard;
            if (board == null || !Utils.CheckBoardValid(board, RedSide))
            {
                DisplayStatus("立即出招：还没识别到完整的棋盘");
                return;
            }
            board = (string[,])board.Clone();
            string fen = Utils.BoardToFen(board, RedSide ? "w" : "b", RedSide ? "w" : "b");
            try
            {
                if (XiangQiRules.LegalMoves(fen).Count == 0)
                {
                    DisplayStatus("立即出招：我方已经没有合法着法");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log("立即出招：局面解析失败 " + fen + " " + ex.Message);
                DisplayStatus("立即出招：局面解析失败");
                return;
            }
            LogHelper.Log("立即出招：引擎空闲，按轮到我方计算 " + fen);
            TurnToOpponent = false;
            EngineAnalyzingBoard = board;
            EngineAnalyzeCount++;
            DisplayStatus("立即出招：按轮到我方开始计算");
            StartAnalyze(fen);
        }

        private void listView_candidates_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SuppressCandidateSelectEvent) return;
            lock (CandidateLock)
            {
                if (listView_candidates.SelectedIndices.Count == 0)
                {
                    SelectedCandidate = -1;
                }
                else
                {
                    int idx = listView_candidates.SelectedIndices[0];
                    SelectedCandidate = idx >= Candidates.Count - 1 ? -1 : idx;
                }
                if (SelectedCandidate >= 0)
                {
                    ApplySelectedArrow();
                }
                else if (Candidates.Count > 0)
                {
                    var cur = Candidates[Candidates.Count - 1];
                    if (cur.Move.Length == 4)
                    {
                        BestMove = new ChessMove(Utils.Move2Point(cur.Move.Substring(0, 2), RedSide), Utils.Move2Point(cur.Move.Substring(2, 2), RedSide));
                        PonderMove = cur.Ponder.Length == 4
                            ? new ChessMove(Utils.Move2Point(cur.Ponder.Substring(0, 2), RedSide), Utils.Move2Point(cur.Ponder.Substring(2, 2), RedSide))
                            : new ChessMove(new Point(-1, -1), new Point(-1, -1));
                    }
                }
            }
            RefreshCandidateView();
            RenderDisplayBoard();
        }

        string CandidateMoveText(string[,] board, string pv)
        {
            if (board == null || string.IsNullOrEmpty(pv)) return pv ?? "";
            try
            {
                return Utils.FenToChina(board, pv.Split(' '), RedSide);
            }
            catch (Exception)
            {
                return pv;
            }
        }

        void RefreshCandidateView()
        {
            if (listView_candidates == null || !listView_candidates.IsHandleCreated) return;
            List<string[]> rows = new List<string[]>();
            lock (CandidateLock)
            {
                for (int i = 0; i < Candidates.Count; i++)
                {
                    var c = Candidates[i];
                    bool isLast = i == Candidates.Count - 1;
                    string state;
                    if (c.Banned) state = "已禁";
                    else if (!c.Finished) state = "计算中";
                    else if (c.Source != "引擎") state = c.Source;
                    else state = "已完成";
                    string[] pv = (c.Pv ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string first = pv.Length > 0 ? CandidateMoveText(CandidateBoard, pv[0]) : CandidateMoveText(CandidateBoard, c.Move);
                    string line = pv.Length > 1 ? CandidateMoveText(CandidateBoard, string.Join(" ", pv.Take(8))) : "";
                    rows.Add(new[] { (i + 1).ToString(), first, c.Score, c.Depth, state, line });
                }
            }
            listView_candidates.BeginInvoke(new Action(() =>
            {
                // 选中行在真正刷新时再读：点选另一行时会先后触发「取消选中」「选中」两次事件，提前读会把用户的选择覆盖掉
                int selected;
                lock (CandidateLock)
                {
                    selected = SelectedCandidate >= 0 ? SelectedCandidate : rows.Count - 1;
                }
                SuppressCandidateSelectEvent = true;
                try
                {
                    listView_candidates.BeginUpdate();
                    while (listView_candidates.Items.Count > rows.Count)
                    {
                        listView_candidates.Items.RemoveAt(listView_candidates.Items.Count - 1);
                    }
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (i < listView_candidates.Items.Count)
                        {
                            var item = listView_candidates.Items[i];
                            for (int j = 0; j < rows[i].Length; j++)
                            {
                                if (item.SubItems[j].Text != rows[i][j]) item.SubItems[j].Text = rows[i][j];
                            }
                        }
                        else
                        {
                            listView_candidates.Items.Add(new ListViewItem(rows[i]));
                        }
                    }
                    for (int i = 0; i < listView_candidates.Items.Count; i++)
                    {
                        bool sel = i == selected;
                        if (listView_candidates.Items[i].Selected != sel) listView_candidates.Items[i].Selected = sel;
                        listView_candidates.Items[i].ForeColor = rows[i][4] == "已禁" ? Color.Gray : Color.Black;
                    }
                    if (selected >= 0 && selected < listView_candidates.Items.Count)
                    {
                        listView_candidates.EnsureVisible(selected);
                    }
                    listView_candidates.EndUpdate();
                }
                finally
                {
                    SuppressCandidateSelectEvent = false;
                }
            }));
        }
    }
}
