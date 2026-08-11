using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LumaPlayer
{
    internal sealed class PlayerForm : Form
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private static readonly string[] VideoExtensions = new string[]
        {
            ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv",
            ".flv", ".ts", ".mts", ".m2ts", ".mpg", ".mpeg", ".vob",
            ".ogv", ".3gp"
        };

        private static readonly string[] AudioExtensions = new string[]
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus",
            ".wma", ".aiff", ".aif", ".alac", ".ape", ".ac3", ".dts"
        };

        private readonly string initialFile;
        private readonly List<string> folderFiles = new List<string>();
        private readonly Timer uiTimer = new Timer();
        private readonly ToolTip toolTip = new ToolTip();

        private Panel infoBar;
        private Panel videoPanel;
        private Panel controlsPanel;
        private Label emptyLabel;
        private Label titleLabel;
        private Label statusLabel;
        private Label timeLabel;
        private Label volumeLabel;
        private TimelineBar timeline;
        private Button openButton;
        private Button previousButton;
        private Button frameBackButton;
        private Button playButton;
        private Button frameForwardButton;
        private Button nextButton;
        private Button captureButton;
        private Button recordButton;
        private SpeedSlider speedSlider;

        private IntPtr mpv;
        private string currentFile;
        private int currentIndex = -1;
        private bool recording;
        private bool endHandled;
        private bool fullscreen;
        private DateTime transientStatusUntil;
        private FormBorderStyle savedBorderStyle;
        private Rectangle savedBounds;
        private FormWindowState savedWindowState;
        private double currentVolume = 70.0;
        private int queueGeneration;

        private IntPtr mouseHook;
        private IntPtr keyboardHook;
        private NativeMethods.LowLevelMouseProc mouseHookDelegate;
        private NativeMethods.LowLevelKeyboardProc keyboardHookDelegate;

        private DateTime pendingClickTime;
        private Point pendingClickPosition;

        internal PlayerForm(string file)
        {
            initialFile = file;
            BuildInterface();
            ConfigureWindow();
            ConfigureTimers();
        }

        private void ConfigureWindow()
        {
            Text = "Luma Player " + Program.DisplayVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 500);
            ClientSize = new Size(1120, 700);
            BackColor = Color.White;
            KeyPreview = true;
            AllowDrop = true;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            KeyDown += OnKeyDown;
            FormClosing += OnFormClosing;
            Shown += OnShown;
            Activated += delegate { InstallInputHooks(); };
            Deactivate += delegate { RemoveInputHooks(); };
            Resize += delegate { ApplyRoundedCorners(); };
        }

        private void BuildInterface()
        {
            SuspendLayout();

            infoBar = new Panel();
            infoBar.Dock = DockStyle.Top;
            infoBar.Height = 44;
            infoBar.BackColor = Color.White;
            infoBar.Padding = new Padding(14, 6, 12, 6);

            Panel brandMark = new Panel();
            brandMark.Size = new Size(28, 28);
            brandMark.Location = new Point(14, 8);
            brandMark.BackColor = Color.FromArgb(40, 113, 245);
            brandMark.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(40, 113, 245)))
                    e.Graphics.FillEllipse(brush, 0, 0, 27, 27);
                Point[] triangle = new Point[] { new Point(11, 7), new Point(11, 21), new Point(21, 14) };
                using (SolidBrush brush = new SolidBrush(Color.White))
                    e.Graphics.FillPolygon(brush, triangle);
            };

            titleLabel = new Label();
            titleLabel.AutoEllipsis = true;
            titleLabel.Text = "Luma Player " + Program.DisplayVersion;
            titleLabel.Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = Color.FromArgb(25, 31, 42);
            titleLabel.Location = new Point(52, 3);
            titleLabel.Size = new Size(700, 21);

            statusLabel = new Label();
            statusLabel.AutoEllipsis = true;
            statusLabel.Text = "Lightweight build " + Program.DisplayVersion + " • Ready";
            statusLabel.ForeColor = Color.FromArgb(116, 124, 139);
            statusLabel.Location = new Point(53, 23);
            statusLabel.Size = new Size(700, 16);

            openButton = CreateTextButton("Open media", 94);
            openButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            openButton.Location = new Point(1008, 6);
            openButton.Click += delegate { OpenVideoDialog(); };
            toolTip.SetToolTip(openButton, "Open a video or audio file");

            infoBar.Controls.Add(brandMark);
            infoBar.Controls.Add(titleLabel);
            infoBar.Controls.Add(statusLabel);
            infoBar.Controls.Add(openButton);
            infoBar.Resize += delegate
            {
                openButton.Left = infoBar.ClientSize.Width - openButton.Width - 16;
                titleLabel.Width = Math.Max(160, openButton.Left - titleLabel.Left - 16);
                statusLabel.Width = titleLabel.Width;
            };

            controlsPanel = new Panel();
            controlsPanel.Dock = DockStyle.Bottom;
            controlsPanel.Height = 84;
            controlsPanel.BackColor = Color.White;
            controlsPanel.Padding = new Padding(12, 2, 12, 6);
            controlsPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(235, 238, 243)))
                    e.Graphics.DrawLine(pen, 0, 0, controlsPanel.Width, 0);
            };

            timeline = new TimelineBar();
            timeline.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            timeline.Location = new Point(12, 2);
            timeline.Height = 20;
            timeline.Width = 1088;
            timeline.SeekRequested += delegate(double seconds) { SeekAbsolute(seconds); };
            timeline.NudgeRequested += delegate(double seconds) { SeekRelative(seconds); };
            toolTip.SetToolTip(timeline, "Mouse wheel: 0.1 second forward/backward");

            timeLabel = new Label();
            timeLabel.Text = "00:00.000  /  00:00.000";
            timeLabel.ForeColor = Color.FromArgb(94, 103, 120);
            timeLabel.Location = new Point(16, 21);
            timeLabel.Size = new Size(205, 18);

            volumeLabel = new Label();
            volumeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            volumeLabel.TextAlign = ContentAlignment.MiddleRight;
            volumeLabel.Text = "Volume 70%";
            volumeLabel.ForeColor = Color.FromArgb(94, 103, 120);
            volumeLabel.Location = new Point(980, 21);
            volumeLabel.Size = new Size(120, 18);

            previousButton = CreateIconButton("|◀");
            frameBackButton = CreateIconButton("◀|");
            playButton = CreatePrimaryButton("▶");
            frameForwardButton = CreateIconButton("|▶");
            nextButton = CreateIconButton("▶|");
            captureButton = CreateTextButton("Capture", 78);
            recordButton = CreateTextButton("● Record", 84);

            previousButton.Click += delegate { PlayAdjacent(-1); };
            frameBackButton.Click += delegate { FrameStep(false); };
            playButton.Click += delegate { TogglePause(); };
            frameForwardButton.Click += delegate { FrameStep(true); };
            nextButton.Click += delegate { PlayAdjacent(1); };
            captureButton.Click += delegate { TakeScreenshot(); };
            recordButton.Click += delegate { ToggleRecording(); };

            toolTip.SetToolTip(previousButton, "Previous media file in this folder");
            toolTip.SetToolTip(frameBackButton, "Previous frame");
            toolTip.SetToolTip(playButton, "Play / Pause");
            toolTip.SetToolTip(frameForwardButton, "Next frame");
            toolTip.SetToolTip(nextButton, "Next media file in this folder");
            toolTip.SetToolTip(captureButton, "Save screenshot");
            toolTip.SetToolTip(recordButton, "Start / stop video recording");

            speedSlider = new SpeedSlider();
            speedSlider.Size = new Size(158, 34);
            speedSlider.Value = 1.0;
            speedSlider.ValueChanged += OnSpeedChanged;
            toolTip.SetToolTip(speedSlider, "Drag to change playback speed; double-click resets to 1.00x");

            controlsPanel.Controls.Add(timeline);
            controlsPanel.Controls.Add(timeLabel);
            controlsPanel.Controls.Add(volumeLabel);
            controlsPanel.Controls.Add(previousButton);
            controlsPanel.Controls.Add(frameBackButton);
            controlsPanel.Controls.Add(playButton);
            controlsPanel.Controls.Add(frameForwardButton);
            controlsPanel.Controls.Add(nextButton);
            controlsPanel.Controls.Add(captureButton);
            controlsPanel.Controls.Add(recordButton);
            controlsPanel.Controls.Add(speedSlider);

            controlsPanel.Resize += LayoutBottomControls;

            videoPanel = new Panel();
            videoPanel.Dock = DockStyle.Fill;
            videoPanel.BackColor = Color.FromArgb(9, 12, 18);
            videoPanel.AllowDrop = true;
            videoPanel.DragEnter += OnDragEnter;
            videoPanel.DragDrop += OnDragDrop;

            emptyLabel = new Label();
            emptyLabel.AutoSize = false;
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.ForeColor = Color.FromArgb(190, 197, 210);
            emptyLabel.Font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point);
            emptyLabel.Text = "Drop a video or audio file here";
            emptyLabel.Cursor = Cursors.Hand;
            emptyLabel.Click += delegate { OpenVideoDialog(); };
            videoPanel.Controls.Add(emptyLabel);

            Controls.Add(videoPanel);
            Controls.Add(controlsPanel);
            Controls.Add(infoBar);

            RegisterVolumeWheel(this);
            ResumeLayout(true);
        }

        private void ConfigureTimers()
        {
            uiTimer.Interval = 200;
            uiTimer.Tick += OnUiTimer;
        }

        private void OnShown(object sender, EventArgs e)
        {
            ApplyRoundedCorners();
            InstallInputHooks();
            uiTimer.Start();
            if (!String.IsNullOrWhiteSpace(initialFile) && File.Exists(initialFile))
                LoadVideo(Path.GetFullPath(initialFile));
        }

        private void LayoutBottomControls(object sender, EventArgs e)
        {
            timeline.Width = Math.Max(200, controlsPanel.ClientSize.Width - 24);
            volumeLabel.Left = controlsPanel.ClientSize.Width - volumeLabel.Width - 16;

            int gap = 6;
            int total = previousButton.Width + frameBackButton.Width + playButton.Width + frameForwardButton.Width + nextButton.Width
                + captureButton.Width + recordButton.Width + speedSlider.Width + gap * 7;
            int x = Math.Max(10, (controlsPanel.ClientSize.Width - total) / 2);
            int y = 43;

            Control[] ordered = new Control[]
            {
                previousButton, frameBackButton, playButton, frameForwardButton, nextButton,
                captureButton, recordButton, speedSlider
            };

            for (int i = 0; i < ordered.Length; i++)
            {
                ordered[i].Left = x;
                ordered[i].Top = y;
                x += ordered[i].Width + gap;
            }
        }

        private Button CreateIconButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(40, 32);
            button.Font = new Font("Segoe UI Symbol", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
            StyleButton(button, false);
            return button;
        }

        private Button CreatePrimaryButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(42, 36);
            button.Font = new Font("Segoe UI Symbol", 12f, FontStyle.Bold, GraphicsUnit.Point);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(40, 113, 245);
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Button CreateTextButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(width, 32);
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold, GraphicsUnit.Point);
            StyleButton(button, false);
            return button;
        }

        private void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = primary ? Color.FromArgb(40, 113, 245) : Color.FromArgb(246, 248, 251);
            button.ForeColor = primary ? Color.White : Color.FromArgb(35, 42, 55);
            button.Cursor = Cursors.Hand;
        }

        private bool EnsureEngine()
        {
            if (mpv != IntPtr.Zero) return true;

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv-2.dll");
            if (!File.Exists(dllPath))
            {
                MessageBox.Show(
                    "Playback engine is missing. Reinstall Luma Player using the offline Windows installer.",
                    "Luma Player",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            try
            {
                videoPanel.CreateControl();
                mpv = MpvNative.mpv_create();
                if (mpv == IntPtr.Zero) throw new InvalidOperationException("Could not create the playback engine.");

                MpvNative.mpv_set_option_string(mpv, "terminal", "no");
                MpvNative.mpv_set_option_string(mpv, "msg-level", "all=no");
                MpvNative.mpv_set_option_string(mpv, "osc", "no");
                MpvNative.mpv_set_option_string(mpv, "input-default-bindings", "no");
                MpvNative.mpv_set_option_string(mpv, "input-vo-keyboard", "no");
                MpvNative.mpv_set_option_string(mpv, "window-dragging", "no");
                MpvNative.mpv_set_option_string(mpv, "hwdec", "auto");
                MpvNative.mpv_set_option_string(mpv, "vo", "gpu");
                MpvNative.mpv_set_option_string(mpv, "gpu-api", "d3d11");
                MpvNative.mpv_set_option_string(mpv, "keep-open", "yes");
                MpvNative.mpv_set_option_string(mpv, "autoload-files", "no");
                MpvNative.mpv_set_option_string(mpv, "demuxer-max-bytes", "32MiB");
                MpvNative.mpv_set_option_string(mpv, "demuxer-max-back-bytes", "8MiB");
                MpvNative.mpv_set_option_string(mpv, "sub-ass-override", "no");
                MpvNative.mpv_set_option_string(mpv, "volume", "70");
                MpvNative.mpv_set_option_string(mpv, "screenshot-format", "png");

                long windowId = videoPanel.Handle.ToInt64();
                MpvNative.mpv_set_option(mpv, "wid", MpvNative.MPV_FORMAT_INT64, ref windowId);

                int result = MpvNative.mpv_initialize(mpv);
                if (result < 0) throw new InvalidOperationException("Playback engine initialization failed.");

                emptyLabel.Visible = false;
                return true;
            }
            catch (Exception exception)
            {
                if (mpv != IntPtr.Zero)
                {
                    try { MpvNative.mpv_terminate_destroy(mpv); }
                    catch { }
                    mpv = IntPtr.Zero;
                }
                MessageBox.Show(exception.Message, "Luma Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void LoadVideo(string path)
        {
            if (!File.Exists(path) || !IsSupportedMedia(path)) return;
            if (!EnsureEngine()) return;

            StopRecording(false);
            currentFile = Path.GetFullPath(path);
            int generation = ++queueGeneration;
            folderFiles.Clear();
            currentIndex = -1;
            endHandled = false;
            timeline.Duration = 0.0;
            timeline.Position = 0.0;

            MpvNative.Command(mpv, "loadfile", currentFile, "replace");
            titleLabel.Text = Path.GetFileName(currentFile);
            statusLabel.Text = "Loading with hardware acceleration…";
            Text = Path.GetFileName(currentFile) + " — Luma Player " + Program.DisplayVersion;
            BuildFolderQueueAsync(currentFile, generation);
        }

        private void BuildFolderQueueAsync(string selectedFile, int generation)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> discovered = new List<string>();
                int selectedIndex = -1;
                try
                {
                    string folder = Path.GetDirectoryName(selectedFile);
                    if (!String.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        foreach (string file in Directory.EnumerateFiles(folder))
                        {
                            if (IsSupportedMedia(file)) discovered.Add(file);
                        }
                        discovered.Sort(delegate(string left, string right)
                        {
                            return NativeMethods.StrCmpLogicalW(Path.GetFileName(left), Path.GetFileName(right));
                        });
                    }

                    for (int i = 0; i < discovered.Count; i++)
                    {
                        if (String.Equals(Path.GetFullPath(discovered[i]), selectedFile, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }
                catch { }

                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(delegate
                    {
                        if (generation != queueGeneration || !String.Equals(currentFile, selectedFile, StringComparison.OrdinalIgnoreCase))
                            return;
                        folderFiles.Clear();
                        folderFiles.AddRange(discovered);
                        currentIndex = selectedIndex;
                    }));
                }
                catch { }
            });
        }

        private static bool IsSupportedMedia(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < VideoExtensions.Length; i++)
            {
                if (String.Equals(extension, VideoExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            for (int i = 0; i < AudioExtensions.Length; i++)
            {
                if (String.Equals(extension, AudioExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsAudioFile(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < AudioExtensions.Length; i++)
            {
                if (String.Equals(extension, AudioExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void PlayAdjacent(int direction)
        {
            if (folderFiles.Count == 0 || currentIndex < 0) return;
            int target = currentIndex + direction;
            if (target < 0) target = folderFiles.Count - 1;
            if (target >= folderFiles.Count) target = 0;
            LoadVideo(folderFiles[target]);
        }

        private void TogglePause()
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            MpvNative.Command(mpv, "cycle", "pause");
        }

        private void FrameStep(bool forward)
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            StopRecording(true);
            MpvNative.Command(mpv, forward ? "frame-step" : "frame-back-step");
        }

        private void SeekAbsolute(double seconds)
        {
            if (mpv == IntPtr.Zero) return;
            StopRecording(true);
            MpvNative.Command(mpv, "seek", seconds.ToString("0.000", CultureInfo.InvariantCulture), "absolute+exact");
        }

        private void SeekRelative(double seconds)
        {
            if (mpv == IntPtr.Zero || Math.Abs(seconds) < 0.0001) return;
            StopRecording(true);
            MpvNative.Command(mpv, "seek", seconds.ToString("0.000", CultureInfo.InvariantCulture), "relative+exact");
        }

        private void AdjustVolume(double delta)
        {
            if (mpv == IntPtr.Zero) return;
            MpvNative.Command(mpv, "add", "volume", delta.ToString("0.0", CultureInfo.InvariantCulture));
        }

        private void OnSpeedChanged(object sender, EventArgs e)
        {
            if (mpv == IntPtr.Zero) return;
            MpvNative.Command(mpv, "set", "speed", speedSlider.Value.ToString("0.00", CultureInfo.InvariantCulture));
        }

        private void TakeScreenshot()
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            if (IsAudioFile(currentFile))
            {
                ShowStatus("Screenshot is available while playing video files", 4.0);
                return;
            }
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Luma Player", "Screenshots");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Luma_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            int result = MpvNative.Command(mpv, "screenshot-to-file", path, "window");
            ShowStatus(result >= 0 ? "Screenshot saved: " + path : "Screenshot could not be saved", 4.0);
        }

        private void ToggleRecording()
        {
            if (recording) StopRecording(true);
            else StartRecording();
        }

        private void StartRecording()
        {
            if (mpv == IntPtr.Zero || String.IsNullOrEmpty(currentFile)) return;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Luma Player", "Recordings");
            Directory.CreateDirectory(folder);
            string extension = IsAudioFile(currentFile) ? ".mka" : ".mkv";
            string path = Path.Combine(folder, "Luma_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + extension);
            int result = MpvNative.Command(mpv, "set", "stream-record", path);
            if (result >= 0)
            {
                recording = true;
                recordButton.Text = "■  Stop";
                recordButton.BackColor = Color.FromArgb(255, 235, 237);
                recordButton.ForeColor = Color.FromArgb(207, 45, 62);
                statusLabel.Text = "Recording… seeking will safely finish this recording";
            }
            else
            {
                ShowStatus("Recording could not be started", 4.0);
            }
        }

        private void StopRecording(bool notify)
        {
            if (!recording || mpv == IntPtr.Zero) return;
            MpvNative.Command(mpv, "set", "stream-record", "");
            recording = false;
            recordButton.Text = "●  Record";
            recordButton.BackColor = Color.FromArgb(246, 248, 251);
            recordButton.ForeColor = Color.FromArgb(35, 42, 55);
            if (notify) ShowStatus("Recording saved in Videos\\Luma Player\\Recordings", 4.0);
        }

        private void OnUiTimer(object sender, EventArgs e)
        {
            if (mpv == IntPtr.Zero) return;

            double duration;
            double position;
            double volume;
            int paused;
            int eof;

            if (MpvNative.mpv_get_property_double(mpv, "duration", MpvNative.MPV_FORMAT_DOUBLE, out duration) >= 0)
                timeline.Duration = duration;
            else duration = timeline.Duration;

            if (MpvNative.mpv_get_property_double(mpv, "time-pos", MpvNative.MPV_FORMAT_DOUBLE, out position) >= 0)
                timeline.Position = position;
            else position = timeline.Position;

            if (MpvNative.mpv_get_property_double(mpv, "volume", MpvNative.MPV_FORMAT_DOUBLE, out volume) >= 0)
            {
                currentVolume = volume;
                volumeLabel.Text = "Volume " + Math.Round(volume).ToString(CultureInfo.InvariantCulture) + "%";
            }

            if (MpvNative.mpv_get_property_flag(mpv, "pause", MpvNative.MPV_FORMAT_FLAG, out paused) >= 0)
                playButton.Text = paused != 0 ? "▶" : "Ⅱ";

            timeLabel.Text = FormatTime(position) + "  /  " + FormatTime(duration);
            if (!recording && !String.IsNullOrEmpty(currentFile) && DateTime.UtcNow >= transientStatusUntil)
                statusLabel.Text = folderFiles.Count > 0
                    ? "Media " + (currentIndex + 1).ToString(CultureInfo.InvariantCulture) + " of " + folderFiles.Count.ToString(CultureInfo.InvariantCulture) + " in folder"
                    : "Playing";

            if (MpvNative.mpv_get_property_flag(mpv, "eof-reached", MpvNative.MPV_FORMAT_FLAG, out eof) >= 0)
            {
                if (eof != 0 && !endHandled && folderFiles.Count > 1)
                {
                    endHandled = true;
                    if (currentIndex >= 0 && currentIndex < folderFiles.Count - 1)
                        LoadVideo(folderFiles[currentIndex + 1]);
                }
                else if (eof == 0)
                {
                    endHandled = false;
                }
            }

        }

        private static string FormatTime(double seconds)
        {
            if (Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0.0) seconds = 0.0;
            TimeSpan value = TimeSpan.FromSeconds(seconds);
            if (value.TotalHours >= 1.0)
                return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}.{3:000}", (int)value.TotalHours, value.Minutes, value.Seconds, value.Milliseconds);
            return String.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:000}", (int)value.TotalMinutes, value.Seconds, value.Milliseconds);
        }

        private void ShowStatus(string message, double seconds)
        {
            statusLabel.Text = message;
            transientStatusUntil = DateTime.UtcNow.AddSeconds(seconds);
        }

        private void OpenVideoDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Open media";
                dialog.Filter = "Media files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.flv;*.ts;*.mts;*.m2ts;*.mpg;*.mpeg;*.vob;*.ogv;*.3gp;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.alac;*.ape;*.ac3;*.dts|Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.flv;*.ts;*.mts;*.m2ts;*.mpg;*.mpeg;*.vob;*.ogv;*.3gp|Audio files|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.alac;*.ape;*.ac3;*.dts|All files|*.*";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadVideo(dialog.FileName);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i]) && IsSupportedMedia(files[i]))
                {
                    LoadVideo(files[i]);
                    return;
                }
            }
        }

        private void RegisterVolumeWheel(Control root)
        {
            if (!(root is TimelineBar))
            {
                root.MouseWheel += delegate(object sender, MouseEventArgs e)
                {
                    if (e.Delta != 0) AdjustVolume((e.Delta / 120.0) * 2.0);
                };
            }
            foreach (Control child in root.Controls)
                RegisterVolumeWheel(child);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (HandleShortcutKey(e.KeyCode, e.Control))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private bool HandleShortcutKey(Keys key, bool control)
        {
            if (key == Keys.Space) TogglePause();
            else if (key == Keys.F || key == Keys.F11) ToggleFullscreen();
            else if (key == Keys.Escape && fullscreen) ToggleFullscreen();
            else if (key == Keys.Left && control) FrameStep(false);
            else if (key == Keys.Right && control) FrameStep(true);
            else if (key == Keys.Left) SeekRelative(-5.0);
            else if (key == Keys.Right) SeekRelative(5.0);
            else if (key == Keys.Up) AdjustVolume(2.0);
            else if (key == Keys.Down) AdjustVolume(-2.0);
            else if (key == Keys.S) TakeScreenshot();
            else if (key == Keys.R) ToggleRecording();
            else if (key == Keys.PageUp) PlayAdjacent(-1);
            else if (key == Keys.PageDown) PlayAdjacent(1);
            else return false;
            return true;
        }

        private void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                savedBorderStyle = FormBorderStyle;
                savedBounds = Bounds;
                savedWindowState = WindowState;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
                TopMost = true;
                infoBar.Visible = false;
                controlsPanel.Visible = false;
                fullscreen = true;
            }
            else
            {
                TopMost = false;
                FormBorderStyle = savedBorderStyle;
                WindowState = FormWindowState.Normal;
                Bounds = savedBounds;
                WindowState = savedWindowState;
                infoBar.Visible = true;
                controlsPanel.Visible = true;
                fullscreen = false;
            }
        }

        private void HandleVideoLeftClick()
        {
            DateTime now = DateTime.UtcNow;
            Point position = Cursor.Position;
            TimeSpan elapsed = now - pendingClickTime;
            Size threshold = SystemInformation.DoubleClickSize;
            bool near = Math.Abs(position.X - pendingClickPosition.X) <= threshold.Width
                && Math.Abs(position.Y - pendingClickPosition.Y) <= threshold.Height;

            if (pendingClickTime != DateTime.MinValue
                && elapsed.TotalMilliseconds <= SystemInformation.DoubleClickTime && near)
            {
                pendingClickTime = DateTime.MinValue;
                TogglePause();
                ToggleFullscreen();
            }
            else
            {
                pendingClickTime = now;
                pendingClickPosition = position;
                TogglePause();
            }
        }

        private void ShowVideoContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = Font;
            menu.RenderMode = ToolStripRenderMode.System;

            ToolStripItem open = menu.Items.Add("Open media…");
            open.Click += delegate { OpenVideoDialog(); };
            ToolStripItem pause = menu.Items.Add("Play / Pause");
            pause.Click += delegate { TogglePause(); };
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem previous = menu.Items.Add("Previous media");
            previous.Click += delegate { PlayAdjacent(-1); };
            ToolStripItem next = menu.Items.Add("Next media");
            next.Click += delegate { PlayAdjacent(1); };
            ToolStripItem capture = menu.Items.Add("Take screenshot");
            capture.Click += delegate { TakeScreenshot(); };
            ToolStripItem record = menu.Items.Add(recording ? "Stop recording" : "Start recording");
            record.Click += delegate { ToggleRecording(); };

            ToolStripMenuItem speed = new ToolStripMenuItem("Playback speed");
            string[] labels = new string[] { "0.25×", "0.50×", "0.75×", "1.00×", "1.25×", "1.50×", "2.00×", "3.00×", "4.00×" };
            double[] values = new double[] { 0.25, 0.50, 0.75, 1.00, 1.25, 1.50, 2.00, 3.00, 4.00 };
            for (int i = 0; i < labels.Length; i++)
            {
                int captured = i;
                ToolStripItem item = speed.DropDownItems.Add(labels[i]);
                item.Click += delegate
                {
                    speedSlider.Value = values[captured];
                };
            }
            menu.Items.Add(speed);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem full = menu.Items.Add(fullscreen ? "Exit fullscreen" : "Fullscreen");
            full.Click += delegate { ToggleFullscreen(); };

            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(Cursor.Position);
        }

        private void InstallInputHooks()
        {
            if (mouseHook != IntPtr.Zero && keyboardHook != IntPtr.Zero) return;
            IntPtr module = NativeMethods.GetModuleHandle(null);
            if (mouseHookDelegate == null) mouseHookDelegate = new NativeMethods.LowLevelMouseProc(LowLevelMouseHook);
            if (keyboardHookDelegate == null) keyboardHookDelegate = new NativeMethods.LowLevelKeyboardProc(LowLevelKeyboardHook);
            if (mouseHook == IntPtr.Zero)
                mouseHook = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, mouseHookDelegate, module, 0);
            if (keyboardHook == IntPtr.Zero)
                keyboardHook = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookDelegate, module, 0);

            if (mouseHook == IntPtr.Zero || keyboardHook == IntPtr.Zero)
                ShowStatus("Some input shortcuts could not be initialized", 5.0);
        }

        private IntPtr LowLevelMouseHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && IsPlayerForeground() && videoPanel != null && videoPanel.IsHandleCreated)
            {
                NativeMethods.MouseHookData data = (NativeMethods.MouseHookData)Marshal.PtrToStructure(
                    lParam, typeof(NativeMethods.MouseHookData));
                Point pointer = new Point(data.point.x, data.point.y);
                Rectangle videoBounds = videoPanel.RectangleToScreen(videoPanel.ClientRectangle);

                if (videoBounds.Contains(pointer))
                {
                    int message = wParam.ToInt32();
                    if (message == WM_LBUTTONDOWN)
                    {
                        BeginInvoke(new Action(HandleVideoLeftClick));
                        return new IntPtr(1);
                    }
                    if (message == WM_RBUTTONUP)
                    {
                        BeginInvoke(new Action(ShowVideoContextMenu));
                        return new IntPtr(1);
                    }
                    if (message == WM_MOUSEWHEEL)
                    {
                        int delta = (short)((data.mouseData >> 16) & 0xffff);
                        BeginInvoke(new Action(delegate { AdjustVolume((delta / 120.0) * 2.0); }));
                        return new IntPtr(1);
                    }
                }
            }
            return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        private IntPtr LowLevelKeyboardHook(int code, IntPtr wParam, IntPtr lParam)
        {
            int message = wParam.ToInt32();
            if (code >= 0 && IsPlayerForeground() && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                NativeMethods.KeyboardHookData data = (NativeMethods.KeyboardHookData)Marshal.PtrToStructure(
                    lParam, typeof(NativeMethods.KeyboardHookData));
                bool control = (NativeMethods.GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
                if (HandleShortcutKey((Keys)data.virtualKey, control))
                    return new IntPtr(1);
            }
            return NativeMethods.CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        private bool IsPlayerForeground()
        {
            return IsHandleCreated && NativeMethods.GetForegroundWindow() == Handle;
        }

        private void RemoveInputHooks()
        {
            if (mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
            if (keyboardHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            uiTimer.Stop();
            StopRecording(false);
            RemoveInputHooks();

            if (mpv != IntPtr.Zero)
            {
                try { MpvNative.mpv_terminate_destroy(mpv); }
                catch { }
                mpv = IntPtr.Zero;
            }
        }

        private void ApplyRoundedCorners()
        {
            if (!IsHandleCreated) return;
            int preference = 2;
            try { NativeMethods.DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int)); }
            catch { }
        }

        private static class NativeMethods
        {
            internal delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);
            internal delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

            [StructLayout(LayoutKind.Sequential)]
            internal struct NativePoint
            {
                internal int x;
                internal int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct MouseHookData
            {
                internal NativePoint point;
                internal uint mouseData;
                internal uint flags;
                internal uint time;
                internal UIntPtr extraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct KeyboardHookData
            {
                internal uint virtualKey;
                internal uint scanCode;
                internal uint flags;
                internal uint time;
                internal UIntPtr extraInfo;
            }

            [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowsHookExW")]
            internal static extern IntPtr SetWindowsHookEx(
                int hookType, LowLevelMouseProc callback, IntPtr module, uint threadId);

            [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowsHookExW")]
            internal static extern IntPtr SetWindowsHookEx(
                int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UnhookWindowsHookEx(IntPtr hook);

            [DllImport("user32.dll")]
            internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            internal static extern short GetAsyncKeyState(int virtualKey);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr GetModuleHandle(string moduleName);

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            internal static extern int StrCmpLogicalW(string left, string right);

            [DllImport("dwmapi.dll")]
            internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        }

        private sealed class SpeedSlider : Control
        {
            private const double Minimum = 0.25;
            private const double Maximum = 4.00;
            private double currentValue = 1.00;
            private bool dragging;

            internal event EventHandler ValueChanged;

            internal SpeedSlider()
            {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                BackColor = Color.White;
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
                SetStyle(ControlStyles.Selectable | ControlStyles.StandardDoubleClick, true);
            }

            internal double Value
            {
                get { return currentValue; }
                set
                {
                    double adjusted = Math.Max(Minimum, Math.Min(Maximum, Math.Round(value * 20.0) / 20.0));
                    if (Math.Abs(adjusted - currentValue) < 0.001) return;
                    currentValue = adjusted;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);

                string label = currentValue.ToString("0.00", CultureInfo.InvariantCulture) + "×";
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(58, 66, 81)))
                    e.Graphics.DrawString(label, Font, textBrush, new RectangleF(0, 8, 48, 20));

                float left = 51f;
                float right = Math.Max(left + 12f, Width - 8f);
                float y = Height / 2f;
                float ratio = (float)((currentValue - Minimum) / (Maximum - Minimum));
                float knobX = left + (right - left) * ratio;

                using (Pen rail = new Pen(Color.FromArgb(221, 226, 234), 4f))
                {
                    rail.StartCap = LineCap.Round;
                    rail.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(rail, left, y, right, y);
                }
                using (Pen fill = new Pen(Color.FromArgb(40, 113, 245), 4f))
                {
                    fill.StartCap = LineCap.Round;
                    fill.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(fill, left, y, knobX, y);
                }
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(36, 0, 0, 0)))
                    e.Graphics.FillEllipse(shadow, knobX - 7, y - 7, 14, 14);
                using (SolidBrush knob = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(knob, knobX - 5, y - 5, 10, 10);
                using (Pen outline = new Pen(Color.FromArgb(40, 113, 245), 2f))
                    e.Graphics.DrawEllipse(outline, knobX - 5, y - 5, 10, 10);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                Capture = true;
                UpdateFromMouse(e.X);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (dragging) UpdateFromMouse(e.X);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left) return;
                dragging = false;
                Capture = false;
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);
                if (e.Button == MouseButtons.Left) Value = 1.00;
            }

            private void UpdateFromMouse(int x)
            {
                double ratio = (x - 51.0) / Math.Max(12.0, Width - 59.0);
                ratio = Math.Max(0.0, Math.Min(1.0, ratio));
                Value = Minimum + (Maximum - Minimum) * ratio;
            }
        }
    }
}
