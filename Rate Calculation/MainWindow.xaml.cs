using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;

namespace Rate_Calculation
{
    public class BetUndoStep
    {
        public string BetKey { get; set; }
        public double Amount { get; set; }
    }

    public class HistoryItem
    {
        public string RoundTitle { get; set; }
        public string BetDetails { get; set; }
        public string ProfitDeltaString { get; set; }
        public string BalanceString { get; set; }
        public Brush ProfitBrush { get; set; }
    }

    public partial class MainWindow : Window
    {
        private static readonly Dictionary<string, double> PayoutRatios = new Dictionary<string, double>();
        private static readonly Dictionary<string, string> BetNames = new Dictionary<string, string>();

        static MainWindow()
        {
            PayoutRatios.Add("4do", 14.0);
            PayoutRatios.Add("4trang", 14.0);
            PayoutRatios.Add("3d1t", 2.8);
            PayoutRatios.Add("3t1d", 2.8);
            PayoutRatios.Add("2d2t", 1.6);
            PayoutRatios.Add("tai", 0.96);
            PayoutRatios.Add("xiu", 0.96);
            PayoutRatios.Add("le", 0.96);
            PayoutRatios.Add("chan", 0.96);
            PayoutRatios.Add("4combo", 6.8);

            BetNames.Add("4do", "4 Đỏ");
            BetNames.Add("4trang", "4 Trắng");
            BetNames.Add("3d1t", "3 Đỏ 1 Trắng");
            BetNames.Add("3t1d", "3 Trắng 1 Đỏ");
            BetNames.Add("2d2t", "2 Đỏ 2 Trắng");
            BetNames.Add("tai", "Tài");
            BetNames.Add("xiu", "Xỉu");
            BetNames.Add("le", "Lẻ");
            BetNames.Add("chan", "Chẵn");
            BetNames.Add("4combo", "Combo 4Đ/4T");
        }

        private double currentBalance = 1000.0;
        private double initialBalance = 1000.0;
        private int roundCounter = 0;
        private double currentChip = 10.0;

        private System.Windows.Threading.DispatcherTimer playTimer;
        private DateTime startTime;
        private bool isTimerRunning = false;

        private readonly string sessionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "session_data.txt");

        private readonly Dictionary<string, double> betAmounts = new Dictionary<string, double>();
        private readonly Dictionary<string, string> cellOutcomes = new Dictionary<string, string>();
        private readonly Stack<BetUndoStep> undoStack = new Stack<BetUndoStep>();

        public ObservableCollection<HistoryItem> History { get; set; }
        public ObservableCollection<Brush> Streaks { get; set; }

        public MainWindow()
        {
            try
            {
                History = new ObservableCollection<HistoryItem>();
                Streaks = new ObservableCollection<Brush>();
                InitializeComponent();
                HistoryList.ItemsSource = History;
                StreakList.ItemsSource = Streaks;

                // Configure Play Timer
                playTimer = new System.Windows.Threading.DispatcherTimer();
                playTimer.Interval = TimeSpan.FromSeconds(1);
                playTimer.Tick += PlayTimer_Tick;

                foreach (string key in PayoutRatios.Keys)
                {
                    betAmounts[key] = 0;
                    cellOutcomes[key] = "none";
                }

                // Load saved session
                LoadSession();

                // Register Ctrl+Z
                KeyBinding undoBind = new KeyBinding();
                undoBind.Key = Key.Z;
                undoBind.Modifiers = ModifierKeys.Control;
                undoBind.Command = new RelayCommand(UndoLastBet);
                this.InputBindings.Add(undoBind);

                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup crash details:\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSession()
        {
            try
            {
                if (File.Exists(sessionFilePath))
                {
                    string[] lines = File.ReadAllLines(sessionFilePath);
                    if (lines.Length >= 2)
                    {
                        double init, curr;
                        if (double.TryParse(lines[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out init))
                            initialBalance = init;
                        if (double.TryParse(lines[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out curr))
                            currentBalance = curr;
                    }
                    if (lines.Length >= 3)
                    {
                        bool isTwoCol;
                        if (bool.TryParse(lines[2].Trim(), out isTwoCol))
                        {
                            ChkTwoColumn.IsChecked = isTwoCol;
                            if (MainWrapPanel != null)
                                MainWrapPanel.MinWidth = isTwoCol ? 810 : 0;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveSession()
        {
            try
            {
                File.WriteAllLines(sessionFilePath, new string[] {
                    initialBalance.ToString(CultureInfo.InvariantCulture),
                    currentBalance.ToString(CultureInfo.InvariantCulture),
                    (ChkTwoColumn != null && ChkTwoColumn.IsChecked == true).ToString()
                });
            }
            catch { }
        }

        // Maps bet key -> its label TextBlock
        private TextBlock GetBetLabel(string key)
        {
            switch (key)
            {
                case "4trang": return BetLbl_4trang;
                case "3t1d":   return BetLbl_3t1d;
                case "2d2t":   return BetLbl_2d2t;
                case "chan":   return BetLbl_chan;
                case "xiu":    return BetLbl_xiu;
                case "le":     return BetLbl_le;
                case "tai":    return BetLbl_tai;
                case "4do":    return BetLbl_4do;
                case "3d1t":   return BetLbl_3d1t;
                case "4combo": return BetLbl_4combo;
                default:       return null;
            }
        }

        // Maps bet key -> its Border cell
        private System.Windows.Controls.Border GetBetCell(string key)
        {
            switch (key)
            {
                case "4trang": return Cell_4trang;
                case "3t1d":   return Cell_3t1d;
                case "2d2t":   return Cell_2d2t;
                case "chan":   return Cell_chan;
                case "xiu":    return Cell_xiu;
                case "le":     return Cell_le;
                case "tai":    return Cell_tai;
                case "4do":    return Cell_4do;
                case "3d1t":   return Cell_3d1t;
                case "4combo": return Cell_4combo;
                default:       return null;
            }
        }

        private void UpdateUI()
        {
            // Guard: don't run before InitializeComponent finishes
            if (!IsLoaded && TxtBalance == null) return;

            if (TxtBalance != null) TxtBalance.Text = currentBalance.ToString("#,##0.##");
            if (LblInitBalance != null) LblInitBalance.Text = initialBalance.ToString("#,##0.##");

            double profit = currentBalance - initialBalance;
            if (LblNetProfit != null)
            {
                LblNetProfit.Text = (profit >= 0 ? "+" : "") + profit.ToString("#,##0.##");
                LblNetProfit.Foreground = profit > 0
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                    : profit < 0
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(96, 205, 255));
            }

            // Update each cell's label and highlight
            foreach (string key in betAmounts.Keys)
            {
                double amt = betAmounts[key];
                TextBlock lbl = GetBetLabel(key);
                System.Windows.Controls.Border cell = GetBetCell(key);

                if (lbl != null)
                {
                    if (amt > 0)
                    {
                        lbl.Text = amt.ToString("#,##0.##");
                        lbl.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        lbl.Text = "";
                        lbl.Visibility = Visibility.Collapsed;
                    }
                }

                if (cell != null)
                {
                    cell.BorderBrush = amt > 0
                        ? new SolidColorBrush(Color.FromRgb(96, 205, 255))
                        : new SolidColorBrush(Color.FromRgb(68, 68, 68));
                    cell.Background = amt > 0
                        ? new SolidColorBrush(Color.FromArgb(40, 0, 120, 212))
                        : new SolidColorBrush(Color.FromRgb(37, 37, 37));
                }
            }
        }

        private void ShowAlert(string message, string type)
        {
            BorderAlert.Visibility = Visibility.Visible;
            TxtAlert.Text = message;

            if (type == "win")
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromArgb(40, 34, 197, 94));
                BorderAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                TxtAlert.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else if (type == "draw")
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromArgb(40, 234, 179, 8));
                BorderAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                TxtAlert.Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8));
            }
            else if (type == "lose")
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68));
                BorderAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TxtAlert.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromArgb(40, 96, 205, 255));
                BorderAlert.BorderBrush = new SolidColorBrush(Color.FromRgb(96, 205, 255));
                TxtAlert.Foreground = new SolidColorBrush(Color.FromRgb(96, 205, 255));
            }
        }

        // ===== Cell mouse events =====

        // Left click: add chip amount to this cell
        private void Cell_LeftClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Controls.Border border = sender as System.Windows.Controls.Border;
            if (border == null || border.Tag == null) return;
            string key = border.Tag.ToString();

            if (currentBalance < currentChip)
            {
                ShowAlert("⚠️ Số dư hiện tại không đủ để đặt cược!", "info");
                return;
            }

            currentBalance = Math.Round(currentBalance - currentChip, 2);
            betAmounts[key] = betAmounts[key] + currentChip;
            undoStack.Push(new BetUndoStep { BetKey = key, Amount = currentChip });
            PlaySound(1200, 30);
            UpdateUI();
        }

        // Right click: undo last step for this cell
        private void Cell_RightClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Controls.Border border = sender as System.Windows.Controls.Border;
            if (border == null || border.Tag == null) return;
            string key = border.Tag.ToString();

            // Find and remove the most recent undo step for this key
            List<BetUndoStep> all = new List<BetUndoStep>(undoStack);
            int removeIdx = -1;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].BetKey == key) { removeIdx = i; break; }
            }

            if (removeIdx >= 0)
            {
                double removedAmt = all[removeIdx].Amount;
                betAmounts[key] = Math.Max(0, betAmounts[key] - removedAmt);
                currentBalance = Math.Round(currentBalance + removedAmt, 2);
                all.RemoveAt(removeIdx);
                undoStack.Clear();
                for (int i = all.Count - 1; i >= 0; i--)
                    undoStack.Push(all[i]);
                UpdateUI();
            }
            e.Handled = true;
        }

        // Middle click: clear all bets for this cell
        private void Cell_MiddleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;

            System.Windows.Controls.Border border = sender as System.Windows.Controls.Border;
            if (border == null || border.Tag == null) return;
            string key = border.Tag.ToString();

            double amt = betAmounts[key];
            currentBalance = Math.Round(currentBalance + amt, 2);
            betAmounts[key] = 0;
            List<BetUndoStep> remaining = new List<BetUndoStep>();
            foreach (BetUndoStep step in undoStack)
            {
                if (step.BetKey != key) remaining.Add(step);
            }
            undoStack.Clear();
            for (int i = remaining.Count - 1; i >= 0; i--)
                undoStack.Push(remaining[i]);
            UpdateUI();
            e.Handled = true;
        }

        // Ctrl+Z: undo last bet step (any cell)
        private void UndoLastBet()
        {
            if (undoStack.Count == 0) return;
            BetUndoStep step = undoStack.Pop();
            betAmounts[step.BetKey] = Math.Max(0, betAmounts[step.BetKey] - step.Amount);
            currentBalance = Math.Round(currentBalance + step.Amount, 2);
            UpdateUI();
        }

        // ===== Global Win/Draw/Lose =====
        private void GlobalResolve_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || btn.Tag == null) return;
            string outcome = btn.Tag.ToString();

            bool hasBets = false;
            foreach (double v in betAmounts.Values)
                if (v > 0) { hasBets = true; break; }

            if (!hasBets)
            {
                ShowAlert("⚠️ Hãy đặt cược vào ít nhất một ô trước!", "info");
                return;
            }

            double totalDeal = 0;
            foreach (double v in betAmounts.Values) totalDeal += v;

            if (totalDeal > currentBalance)
            {
                ShowAlert("⚠️ Số dư hiện tại không đủ để thực hiện giao dịch!", "info");
                return;
            }

            double payout = 0;
            double totalDelta = 0;
            List<string> descList = new List<string>();

            foreach (string key in betAmounts.Keys)
            {
                double amount = betAmounts[key];
                if (amount <= 0) continue;

                double ratio = PayoutRatios[key];
                double cellPayout = 0;
                double cellNet = 0;
                if (outcome == "win")
                {
                    cellPayout = amount + Math.Round(amount * ratio, 2);
                    cellNet = Math.Round(amount * ratio, 2);
                }
                else if (outcome == "draw")
                {
                    cellPayout = Math.Round(amount * 0.96, 2);
                    cellNet = cellPayout - amount;
                }
                else if (outcome == "lose")
                {
                    cellPayout = 0;
                    cellNet = -amount;
                }

                payout += cellPayout;
                totalDelta += cellNet;
                descList.Add(string.Format("{0}: {1:#,##0.##} xu", BetNames[key], amount));
            }

            currentBalance = Math.Round(currentBalance + payout, 2);
            roundCounter++;

            HistoryItem item = new HistoryItem();
            item.RoundTitle = string.Format("Ván #{0} ({1})", roundCounter, outcome.ToUpper());
            item.BetDetails = string.Join(", ", descList);
            item.ProfitDeltaString = (totalDelta >= 0 ? "+" : "") + totalDelta.ToString("#,##0.##");
            item.BalanceString = string.Format("Dư: {0:#,##0.##}", currentBalance);
            item.ProfitBrush = totalDelta > 0 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) :
                               totalDelta < 0 ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) :
                               new SolidColorBrush(Color.FromRgb(234, 179, 8));
            History.Insert(0, item);

            // Add to Streak Roadmap
            SolidColorBrush streakBrush = totalDelta > 0 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) :
                                          totalDelta < 0 ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) :
                                          new SolidColorBrush(Color.FromRgb(234, 179, 8));
            Streaks.Add(streakBrush);
            if (Streaks.Count > 18) Streaks.RemoveAt(0);

            // Play resolution sound
            if (totalDelta > 0)
            {
                PlaySound(1000, 100);
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => PlaySound(1500, 150));
            }
            else if (totalDelta < 0)
            {
                PlaySound(400, 250);
            }
            else
            {
                PlaySound(800, 150);
            }

            string alertMsg = "";
            if (outcome == "win")
                alertMsg = string.Format("🏆 Thắng! Tổng lãi +{0:#,##0.##} xu", totalDelta);
            else if (outcome == "draw")
                alertMsg = string.Format("🤝 Hoà! Tổng lỗ phí -{0:#,##0.##} xu", Math.Abs(totalDelta));
            else if (outcome == "lose")
                alertMsg = string.Format("💀 Thua! Tổng tổn thất -{0:#,##0.##} xu", Math.Abs(totalDelta));

            ClearAllBets();
            ShowAlert(alertMsg, outcome);
            FlashAlertBackground(outcome);
            UpdateUI();
            SaveSession();
        }

        // Toggles outcome for a cell (W/D/L)
        private void MiniOutcome_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton btn = sender as ToggleButton;
            if (btn != null && btn.Tag != null)
            {
                string[] parts = btn.Tag.ToString().Split('|');
                if (parts.Length == 2)
                {
                    string betKey = parts[0];
                    string outcome = parts[1];

                    bool isChecked = btn.IsChecked == true;

                    if (isChecked)
                    {
                        cellOutcomes[betKey] = outcome;
                        UncheckOtherMiniButtons(betKey, outcome);

                        // Auto-add current chip if no bet exists on this cell
                        if (betAmounts[betKey] <= 0)
                        {
                            if (currentBalance < currentChip)
                            {
                                ShowAlert("⚠️ Số dư hiện tại không đủ để đặt cược!", "info");
                                btn.IsChecked = false;
                                cellOutcomes[betKey] = "none";
                                return;
                            }
                            currentBalance = Math.Round(currentBalance - currentChip, 2);
                            betAmounts[betKey] = currentChip;
                            undoStack.Push(new BetUndoStep { BetKey = betKey, Amount = currentChip });
                            PlaySound(1200, 30);
                            UpdateUI();
                        }
                    }
                    else
                    {
                        cellOutcomes[betKey] = "none";
                    }
                }
            }
            e.Handled = true;
        }

        private void UncheckOtherMiniButtons(string betKey, string activeOutcome)
        {
            string[] outcomes = { "win", "draw", "lose" };
            foreach (string o in outcomes)
            {
                if (o != activeOutcome)
                {
                    ToggleButton tb = GetMiniButton(betKey, o);
                    if (tb != null) tb.IsChecked = false;
                }
            }
        }

        private ToggleButton GetMiniButton(string betKey, string outcome)
        {
            string name = "Btn_" + betKey + "_" + (outcome == "win" ? "W" : outcome == "draw" ? "D" : "L");
            return this.FindName(name) as ToggleButton;
        }

        // Resolves the round combining all selected cell outcomes
        private void ApplyOutcomes_Click(object sender, RoutedEventArgs e)
        {
            bool hasSelections = false;
            foreach (string key in betAmounts.Keys)
            {
                if (betAmounts[key] > 0 && cellOutcomes[key] != "none")
                {
                    hasSelections = true;
                    break;
                }
            }

            if (!hasSelections)
            {
                ShowAlert("⚠️ Vui lòng chọn kết quả (W/D/L) cho các ô có tiền cược!", "info");
                return;
            }

            double totalDeal = 0;
            foreach (string key in betAmounts.Keys)
            {
                if (cellOutcomes[key] != "none")
                {
                    totalDeal += betAmounts[key];
                }
            }

            if (totalDeal > currentBalance)
            {
                ShowAlert("⚠️ Số dư hiện tại không đủ để thực hiện giao dịch!", "info");
                return;
            }

            double payout = 0;
            double totalDelta = 0;
            List<string> descList = new List<string>();

            foreach (string key in betAmounts.Keys)
            {
                double amount = betAmounts[key];
                string outcome = cellOutcomes[key];
                if (outcome == "none" || amount <= 0) continue;

                double ratio = PayoutRatios[key];
                double cellPayout = 0;
                double cellNet = 0;
                if (outcome == "win")
                {
                    cellPayout = amount + Math.Round(amount * ratio, 2);
                    cellNet = Math.Round(amount * ratio, 2);
                }
                else if (outcome == "draw")
                {
                    cellPayout = Math.Round(amount * 0.96, 2);
                    cellNet = cellPayout - amount;
                }
                else if (outcome == "lose")
                {
                    cellPayout = 0;
                    cellNet = -amount;
                }

                payout += cellPayout;
                totalDelta += cellNet;
                descList.Add(string.Format("{0}: {1:#,##0.##} xu ({2})", BetNames[key], amount, outcome.ToUpper()));
            }

            currentBalance = Math.Round(currentBalance + payout, 2);
            roundCounter++;

            HistoryItem item = new HistoryItem();
            item.RoundTitle = string.Format("Ván #{0} (Tổng hợp)", roundCounter);
            item.BetDetails = string.Join(", ", descList);
            item.ProfitDeltaString = (totalDelta >= 0 ? "+" : "") + totalDelta.ToString("#,##0.##");
            item.BalanceString = string.Format("Dư: {0:#,##0.##}", currentBalance);
            item.ProfitBrush = totalDelta > 0 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) :
                               totalDelta < 0 ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) :
                               new SolidColorBrush(Color.FromRgb(234, 179, 8));
            History.Insert(0, item);

            // Add to Streak Roadmap
            SolidColorBrush streakBrush = totalDelta > 0 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) :
                                          totalDelta < 0 ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) :
                                          new SolidColorBrush(Color.FromRgb(234, 179, 8));
            Streaks.Add(streakBrush);
            if (Streaks.Count > 18) Streaks.RemoveAt(0);

            // Play resolution sound
            if (totalDelta > 0)
            {
                PlaySound(1000, 100);
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => PlaySound(1500, 150));
            }
            else if (totalDelta < 0)
            {
                PlaySound(400, 250);
            }
            else
            {
                PlaySound(800, 150);
            }

            string alertMsg = string.Format("⚡ Đã áp dụng kết quả! Tổng lãi/lỗ ván này: {0}{1:#,##0.##} xu", (totalDelta >= 0 ? "+" : ""), totalDelta);
            string alertType = totalDelta > 0 ? "win" : totalDelta < 0 ? "lose" : "draw";

            ClearAllBets();
            ShowAlert(alertMsg, alertType);
            FlashAlertBackground(alertType);
            UpdateUI();
            SaveSession();
        }

        private void ClearAllBets()
        {
            List<string> keys = new List<string>(betAmounts.Keys);
            foreach (string key in keys)
            {
                betAmounts[key] = 0;
                cellOutcomes[key] = "none";
                ToggleButton w = GetMiniButton(key, "win");
                ToggleButton d = GetMiniButton(key, "draw");
                ToggleButton l = GetMiniButton(key, "lose");
                if (w != null) w.IsChecked = false;
                if (d != null) d.IsChecked = false;
                if (l != null) l.IsChecked = false;
            }
            undoStack.Clear();
        }

        // ===== Panel 1 handlers =====
        private void SetInitialBalance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double val;
                string rawText = InputInitBalance.Text.Trim().Replace(",", ".");
                if (double.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val > 0)
                {
                    initialBalance = val;
                    currentBalance = val;
                    History.Clear();
                    roundCounter = 0;
                    InputInitBalance.Text = "";
                    ClearAllBets();
                    
                    // Start Timer
                    startTime = DateTime.Now;
                    playTimer.Start();
                    isTimerRunning = true;
                    TxtPlayTime.Text = "⏱️ 00:00:00";

                    UpdateUI();
                    SaveSession();
                    if (BorderAlert != null) BorderAlert.Visibility = Visibility.Collapsed;
                    ShowAlert(string.Format("Đã reset số dư ban đầu về {0:#,##0.##} xu", val), "info");
                }
                else
                {
                    ShowAlert("Vui lòng nhập số dư ban đầu hợp lệ!", "info");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Crash details:\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlayTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan elapsed = DateTime.Now - startTime;
            TxtPlayTime.Text = string.Format("⏱️ {0:00}:{1:00}:{2:00}", elapsed.Hours, elapsed.Minutes, elapsed.Seconds);
        }

        private void EndSession_Click(object sender, RoutedEventArgs e)
        {
            if (!isTimerRunning)
            {
                ShowAlert("⚠️ Chưa khởi tạo phiên chơi để tính giờ!", "info");
                return;
            }

            playTimer.Stop();
            isTimerRunning = false;

            TimeSpan elapsed = DateTime.Now - startTime;
            double netProfit = Math.Round(currentBalance - initialBalance, 2);
            string profitSign = netProfit >= 0 ? "+" : "";

            // History Log Item
            HistoryItem chotDon = new HistoryItem();
            chotDon.RoundTitle = "🏁 --- CHỐT PHIÊN (End) ---";
            chotDon.BetDetails = string.Format("Thời gian: {0:00}p {1:00}s | Số ván: {2}", Math.Floor(elapsed.TotalMinutes), elapsed.Seconds, roundCounter);
            chotDon.ProfitDeltaString = profitSign + netProfit.ToString("#,##0.##");
            chotDon.BalanceString = string.Format("Dư: {0:#,##0.##}", currentBalance);
            chotDon.ProfitBrush = netProfit >= 0 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            History.Insert(0, chotDon);

            // Audio effects
            PlaySound(1500, 150);
            System.Threading.Tasks.Task.Delay(150).ContinueWith(_ => PlaySound(1800, 250));

            ShowAlert(string.Format("🏁 Chốt phiên! Lợi nhuận: {0}{1:#,##0.##} xu", profitSign, netProfit), netProfit >= 0 ? "win" : "lose");

            // Reset Initial Balance to Current Balance for new cycle
            initialBalance = currentBalance;
            UpdateUI();
            SaveSession();
        }

        private void ResetSession_Click(object sender, RoutedEventArgs e)
        {
            currentBalance = initialBalance;
            History.Clear();
            roundCounter = 0;
            BorderAlert.Visibility = Visibility.Collapsed;
            ClearAllBets();
            UpdateUI();
            SaveSession();
            ShowAlert("Đã thiết lập lại số dư về số dư ban đầu.", "info");
        }

        private void ResetLog_Click(object sender, RoutedEventArgs e)
        {
            History.Clear();
            roundCounter = 0;
            currentBalance = initialBalance;
            BorderAlert.Visibility = Visibility.Collapsed;
            ClearAllBets();
            UpdateUI();
            SaveSession();
            ShowAlert("Đã xóa toàn bộ lịch sử đấu.", "info");
        }

        // ===== Chip selector =====
        private void Chip_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb != null && rb.Tag != null)
            {
                double val;
                if (double.TryParse(rb.Tag.ToString(), out val))
                {
                    currentChip = val;
                    if (InputCustomBet != null) InputCustomBet.Text = "";
                }
            }
        }

        private void InputCustomBet_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (InputCustomBet.IsFocused)
            {
                double val;
                if (double.TryParse(InputCustomBet.Text, out val) && val > 0)
                    currentChip = val;
            }
        }

        private void InputCustomBet_GotFocus(object sender, RoutedEventArgs e) { }
        private void InputInitBalance_GotFocus(object sender, RoutedEventArgs e)
        {
            BorderAlert.Visibility = Visibility.Collapsed;
        }

        // ===== UX Helpers =====
        private void PlaySound(int freq, int dur)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try { System.Console.Beep(freq, dur); } catch { }
            });
        }

        private async void FlashAlertBackground(string type)
        {
            if (BorderAlert == null) return;
            Brush oldBg = BorderAlert.Background;
            Brush oldBorder = BorderAlert.BorderBrush;

            if (type == "win")
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                BorderAlert.BorderBrush = new SolidColorBrush(Colors.White);
            }
            else if (type == "lose")
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                BorderAlert.BorderBrush = new SolidColorBrush(Colors.White);
            }
            else
            {
                BorderAlert.Background = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                BorderAlert.BorderBrush = new SolidColorBrush(Colors.White);
            }

            await System.Threading.Tasks.Task.Delay(300);

            BorderAlert.Background = oldBg;
            BorderAlert.BorderBrush = oldBorder;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Do not trigger if typing in textboxes
            if (FocusManager.GetFocusedElement(this) is TextBox) return;

            if (e.Key == Key.Q) SimulateAddBet("chan");
            else if (e.Key == Key.W) SimulateAddBet("le");
            else if (e.Key == Key.A) SimulateAddBet("xiu");
            else if (e.Key == Key.S) SimulateAddBet("tai");
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                ApplyOutcomes_Click(null, null);
                e.Handled = true;
            }
        }

        private void SimulateAddBet(string key)
        {
            if (betAmounts.ContainsKey(key))
            {
                if (currentBalance < currentChip)
                {
                    ShowAlert("⚠️ Số dư hiện tại không đủ để đặt cược!", "info");
                    return;
                }
                currentBalance = Math.Round(currentBalance - currentChip, 2);
                betAmounts[key] += currentChip;
                undoStack.Push(new BetUndoStep { BetKey = key, Amount = currentChip });
                PlaySound(1200, 30);
                UpdateUI();
            }
        }

        private void ChkTwoColumn_Changed(object sender, RoutedEventArgs e)
        {
            if (MainWrapPanel == null || ChkTwoColumn == null) return;
            bool isTwoCol = ChkTwoColumn.IsChecked == true;
            MainWrapPanel.MinWidth = isTwoCol ? 810 : 0;
            SaveSession();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { _execute(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
