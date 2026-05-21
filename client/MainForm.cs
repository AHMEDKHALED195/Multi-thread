using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CompressionClient
{
    public class MainForm : Form
    {
        private const int BUFFER_SIZE = 8192;
        private const string DEFAULT_IP = "127.0.0.1";
        private const int DEFAULT_PORT = 9000;

        private TextBox txtIP, txtPort, txtFilePath, txtLog;
        private Button btnBrowse, btnSend, btnSave;
        private Label lblOriginalVal, lblCompressedVal, lblRatioVal;
        private ProgressBar progressBar;

        private string _filePath = "";
        private byte[] _compressedData = null;
        private string _originalName = "";

        public MainForm()
        {
            this.Text = "Compression Client";
            this.Size = new Size(500, 540);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.White;

            BuildUI();
        }

        private void BuildUI()
        {
            // ── Server Settings Group ────────────────────────
            var grpConn = new GroupBox { Text = "Server Settings", Left = 15, Top = 10, Width = 455, Height = 65 };

            var lblIP = new Label { Text = "IP Address:", Left = 10, Top = 25, AutoSize = true };
            txtIP = new TextBox { Left = 85, Top = 22, Width = 130, Text = DEFAULT_IP };

            var lblPort = new Label { Text = "Port:", Left = 230, Top = 25, AutoSize = true };
            txtPort = new TextBox { Left = 265, Top = 22, Width = 60, Text = DEFAULT_PORT.ToString() };

            grpConn.Controls.AddRange(new Control[] { lblIP, txtIP, lblPort, txtPort });

            // ── File Group ───────────────────────────────────
            var grpFile = new GroupBox { Text = "File", Left = 15, Top = 85, Width = 455, Height = 65 };

            var lblFile = new Label { Text = "Selected:", Left = 10, Top = 25, AutoSize = true };
            txtFilePath = new TextBox { Left = 75, Top = 22, Width = 270, ReadOnly = true, BackColor = Color.WhiteSmoke };

            btnBrowse = new Button
            {
                Text = "Browse...",
                Left = 355,
                Top = 20,
                Width = 85,
                Height = 28,
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBrowse.Click += BtnBrowse_Click;

            grpFile.Controls.AddRange(new Control[] { lblFile, txtFilePath, btnBrowse });

            // ── Send Button + ProgressBar ────────────────────
            btnSend = new Button
            {
                Text = "Send & Compress",
                Left = 15,
                Top = 162,
                Width = 150,
                Height = 32,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSend.Click += BtnSend_Click;

            progressBar = new ProgressBar
            {
                Left = 175,
                Top = 167,
                Width = 295,
                Height = 22,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 0
            };

            // ── Results Group ────────────────────────────────
            var grpResult = new GroupBox { Text = "Results", Left = 15, Top = 205, Width = 455, Height = 90 };

            var lblOrig = new Label { Text = "Original:", Left = 10, Top = 25, AutoSize = true };
            lblOriginalVal = new Label { Text = "--", Left = 75, Top = 25, AutoSize = true, ForeColor = Color.SteelBlue, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            var lblComp = new Label { Text = "Compressed:", Left = 10, Top = 52, AutoSize = true };
            lblCompressedVal = new Label { Text = "--", Left = 90, Top = 52, AutoSize = true, ForeColor = Color.SteelBlue, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            var lblRat = new Label { Text = "Saved:", Left = 200, Top = 52, AutoSize = true };
            lblRatioVal = new Label { Text = "--", Left = 245, Top = 52, AutoSize = true, ForeColor = Color.Green, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            btnSave = new Button
            {
                Text = "Save Compressed File",
                Left = 320,
                Top = 45,
                Width = 125,
                Height = 28,
                BackColor = Color.SlateBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSave.Click += BtnSave_Click;

            grpResult.Controls.AddRange(new Control[]
            {
                lblOrig, lblOriginalVal, lblComp, lblCompressedVal,
                lblRat, lblRatioVal, btnSave
            });

            // ── Log Group ────────────────────────────────────
            var grpLog = new GroupBox { Text = "Log", Left = 15, Top = 305, Width = 455, Height = 185 };

            txtLog = new TextBox
            {
                Left = 10,
                Top = 20,
                Width = 430,
                Height = 150,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(245, 245, 245),
                Font = new Font("Consolas", 8.5f)
            };

            grpLog.Controls.Add(txtLog);

            // ── Add all groups to Form ───────────────────────
            this.Controls.AddRange(new Control[]
            {
                grpConn, grpFile, btnSend, progressBar, grpResult, grpLog
            });
        }

        // ── Events ───────────────────────────────────────────

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Title = "Select a file", Filter = "All Files (*.*)|*.*" })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _filePath = dlg.FileName;
                    _originalName = Path.GetFileName(_filePath);
                    txtFilePath.Text = _originalName;
                    btnSend.Enabled = true;
                    Log($"Selected: {_filePath}");
                }
            }
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtPort.Text, out int port))
            {
                MessageBox.Show("Invalid port number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetBusy(true);
            _compressedData = null;
            btnSave.Enabled = false;
            lblOriginalVal.Text = "--";
            lblCompressedVal.Text = "--";
            lblRatioVal.Text = "--";

            try
            {
                await Task.Run(() => Transfer(txtIP.Text.Trim(), port));
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                MessageBox.Show(ex.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_compressedData == null) return;

            using (var dlg = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_originalName) + ".gz",
                Filter = "GZip Files (*.gz)|*.gz|All Files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllBytes(dlg.FileName, _compressedData);
                    Log($"Saved to: {dlg.FileName}");
                    MessageBox.Show("File saved successfully!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // ── Transfer ─────────────────────────────────────────

        private void Transfer(string ip, int port)
        {
            byte[] data = File.ReadAllBytes(_filePath);
            long originalSize = data.Length;

            Log($"Connecting to {ip}:{port} ...");

            using (var client = new TcpClient())
            {
                client.Connect(ip, port);
                Log("Connected.");

                using (var ns = client.GetStream())
                {
                    WriteInt64(ns, originalSize);
                    ns.Write(data, 0, data.Length);
                    ns.Flush();
                    Log($"Sent {FormatBytes(originalSize)}. Waiting for response...");

                    long compSize = ReadInt64(ns);
                    _compressedData = ReadExact(ns, compSize);
                    double ratio = (1.0 - (double)compSize / originalSize) * 100;

                    Log($"Done. {FormatBytes(originalSize)} -> {FormatBytes(compSize)} ({ratio:F1}% saved)");

                    Invoke((Action)(() =>
                    {
                        lblOriginalVal.Text = FormatBytes(originalSize);
                        lblCompressedVal.Text = FormatBytes(compSize);
                        lblRatioVal.Text = $"{ratio:F1}%";
                        lblRatioVal.ForeColor = ratio > 0 ? Color.Green : Color.OrangeRed;
                        btnSave.Enabled = true;
                    }));
                }
            }
        }

        // ── Network Helpers ──────────────────────────────────

        static void WriteInt64(NetworkStream s, long v)
        {
            byte[] b = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            s.Write(b, 0, 8);
        }

        static long ReadInt64(NetworkStream s)
        {
            byte[] b = ReadExact(s, 8);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToInt64(b, 0);
        }

        static byte[] ReadExact(NetworkStream s, long count)
        {
            byte[] buf = new byte[count];
            long got = 0;
            while (got < count)
            {
                int n = s.Read(buf, (int)got, (int)Math.Min(8192, count - got));
                if (n == 0) throw new EndOfStreamException("Connection lost.");
                got += n;
            }
            return buf;
        }

        // ── UI Helpers ───────────────────────────────────────

        private void Log(string msg) =>
            Invoke((Action)(() => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n")));

        private void SetBusy(bool busy) =>
            Invoke((Action)(() =>
            {
                btnSend.Enabled = !busy;
                btnBrowse.Enabled = !busy;
                progressBar.MarqueeAnimationSpeed = busy ? 30 : 0;
            }));

        static string FormatBytes(long b)
        {
            if (b < 1024) return $"{b} B";
            if (b < 1048576) return $"{b / 1024.0:F2} KB";
            return $"{b / 1048576.0:F2} MB";
        }
    }
}