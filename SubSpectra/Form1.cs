using LibVLCSharp.Shared;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Drawing.Imaging;
using ClosedXML.Excel;

namespace SubSpectra {
    public partial class Form1 : Form {
        // --- Drag support ---
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public Panel[] panels = new Panel[5];

        private LibVLC libVLC;
        private LibVLCSharp.Shared.MediaPlayer media_player, _4_media_player;

        private int panel_idx = 0;

        private Bitmap _1_original_img, _1_hide_original;
        private Bitmap _1_preview_img, _1_hide_preview;

        private readonly int img_length = 20;
        private readonly int video_length = 40;
        private readonly string ffmpeg_path = @"C:\ffmpeg\bin\ffmpeg.exe"; // FFMPEG's default location

        private Bitmap _3_selected;
        private string _3_img_path;
        private int channel_index = 0; // R -> 0, G -> 1, B -> 2, A -> 3
        private int bit_layer = 0; // R0 default
        private readonly string[] channels = { "R", "G", "B", "A" };

        private readonly int maxFrames = 50; // change the number of frames to test on a video
        private readonly int cell_width = 30; // change the width of the column "Frame" from the excel file

        private string img_5_path;

        public void init_panels() {
            panels[0] = panel_create_image;
            panels[1] = panel_create_video;
            panels[2] = panel_test_image;
            panels[3] = panel_test_video;
            panels[4] = panel_other;

            for (int i = 1; i < 5; i++)
                panels[i].Hide();
        }

        public void refresh(int idx) {
            panel_idx = idx;

            foreach (var panel in panels)
                panel.Hide();

            panels[panel_idx].Show();

            media_player?.Stop();
            _4_media_player?.Stop();
        }

        public void run_ffmpeg(string arguments, int timeoutMs = 120_000) {
            var psi = new ProcessStartInfo {
                FileName = ffmpeg_path,
                Arguments = "-hide_banner -nostats " + arguments, // reduce chatter
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true }) {
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (!proc.WaitForExit(timeoutMs)) {
                    try { proc.Kill(true); } catch { /* ignore */ }
                    throw new Exception("FFmpeg timed out.");
                }

                // Make sure async readers finish
                proc.WaitForExit();

                var outText = stdout.ToString();
                var errText = stderr.ToString();

                // Optional: inspect logs
                Console.WriteLine(outText);
                Console.WriteLine(errText);

                if (proc.ExitCode != 0)
                    throw new Exception("FFmpeg failed with error:\n" + errText);
            }
        }

        public Form1() {
            InitializeComponent();
            init_panels();
            refresh(0);

            Core.Initialize(); // LibVLCSharp

            string musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "background.mp3");

            var vlcDir = Path.Combine(AppContext.BaseDirectory, "vlc");
            var pluginPath = Path.Combine(vlcDir, "plugins");

            libVLC = new LibVLC($"--plugin-path={pluginPath}", vlcDir);

            media_player = new LibVLCSharp.Shared.MediaPlayer(libVLC);
            _4_media_player = new LibVLCSharp.Shared.MediaPlayer(libVLC);

            _2_original_video.MediaPlayer = media_player;
            _4_original_video.MediaPlayer = _4_media_player;

            _1_combo.SelectedIndex = 0;
            _1_combo.DrawMode = DrawMode.OwnerDrawFixed;

            _1_combo.DrawItem += (s, e) => {
                if (e.Index < 0)
                    return;

                e.Graphics.FillRectangle(System.Drawing.Brushes.White, e.Bounds);

                string text = _1_combo.Items[e.Index].ToString();
                e.Graphics.DrawString(text, e.Font, System.Drawing.Brushes.Black, e.Bounds.X, e.Bounds.Y);

                e.DrawFocusRectangle(); // optional, you can remove this
            };

            // panel menu - zoom
            img_title.SizeMode = PictureBoxSizeMode.Zoom;
            img_create_image.SizeMode = PictureBoxSizeMode.Zoom;
            img_create_video.SizeMode = PictureBoxSizeMode.Zoom;
            img_test_image.SizeMode = PictureBoxSizeMode.Zoom;
            img_test_video.SizeMode = PictureBoxSizeMode.Zoom;
            img_other.SizeMode = PictureBoxSizeMode.Zoom;
            img_exit.SizeMode = PictureBoxSizeMode.Zoom;
            img_about.SizeMode = PictureBoxSizeMode.Zoom;

            // create menu - zoom
            img_create_select.SizeMode = PictureBoxSizeMode.Zoom;
            img_create_preview.SizeMode = PictureBoxSizeMode.Zoom;
            img_create_rgb.SizeMode = PictureBoxSizeMode.Zoom;
            img_create_hide.SizeMode = PictureBoxSizeMode.Zoom;

            // test image - image
            img_3_select.SizeMode = PictureBoxSizeMode.Zoom;
            img_3_preview.SizeMode = PictureBoxSizeMode.Zoom;

            // other tests - image
            img_5_select.SizeMode = PictureBoxSizeMode.Zoom;

            // panel menu - image
            img_title.Image = Image.FromFile(@"img/title.png");
            img_create_image.Image = Image.FromFile(@"img/icons8-edit-image-100.png");
            img_create_video.Image = Image.FromFile(@"img/icons8-video-100_2.png");
            img_test_image.Image = Image.FromFile(@"img/icons8-image-64.png");
            img_test_video.Image = Image.FromFile(@"img/icons8-video-100_2.png");
            img_other.Image = Image.FromFile(@"img/icons8-follow-each-other-96.png");
            img_exit.Image = Image.FromFile(@"img/icons8-emergency-exit-100.png");
            img_about.Image = Image.FromFile(@"img/icons8-question-cursor-50.png");

            // create menu - image
            img_create_hide.Image = Image.FromFile(@"img/icons8-edit-image-100.png");

            // test image - image
            img_3_select.Image = Image.FromFile(@"img/icons8-edit-image-100.png");
            img_3_preview.Image = Image.FromFile(@"img/icons8-edit-image-100.png");

            // other tests - image
            img_5_select.Image = Image.FromFile(@"img/icons8-edit-image-100.png");

            // image modifiers
            _1_original_img = new Bitmap(img_create_select.Image);
            _1_preview_img = new Bitmap(img_create_select.Image);
            _1_hide_original = new Bitmap(img_create_select.Image);
            _1_hide_preview = new Bitmap(img_create_select.Image);
        }

        private void btn_exit_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void btn_create_image_Click(object sender, EventArgs e) {
            refresh(0);
        }

        private void btn_create_video_Click(object sender, EventArgs e) {
            refresh(1);
        }

        private void btn_test_image_Click(object sender, EventArgs e) {
            refresh(2);
        }

        private void btn_test_video_Click(object sender, EventArgs e) {
            refresh(3);
        }

        private void btn_other_Click(object sender, EventArgs e) {
            refresh(4);
        }

        private void btn_about_Click(object sender, EventArgs e) {
            string url = "https://google.com";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void panel_other_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void panel_test_video_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void panel_test_image_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void panel_create_video_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void panel_create_image_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void panel_menu_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void update_preview() {
            _1_preview_img?.Dispose();
            _1_preview_img = (Bitmap) _1_original_img.Clone();
            img_create_preview.Image = _1_preview_img;
        }

        private void update_hide_preview() {
            _1_hide_preview?.Dispose();
            _1_hide_preview = (Bitmap) _1_hide_original.Clone();
            img_create_hide.Image = _1_hide_preview;
        }

        private void btn_1_select_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png";
                ofd.Title = "Select an image";

                if (ofd.ShowDialog() == DialogResult.OK) {
                    img_create_select.Image = Image.FromFile(ofd.FileName);
                    img_create_preview.Image = Image.FromFile(ofd.FileName);

                    _1_original_img = new Bitmap(ofd.FileName);
                    update_preview();
                }
            }
        }

        private void btn_1_save_Click(object sender, EventArgs e) {
            if (_1_preview_img == null) {
                MessageBox.Show("No image to save!");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog()) {
                sfd.Filter = "JPEG Image|*.jpg|PNG Image|*.png";
                sfd.Title = "Save Image";

                if (sfd.ShowDialog() == DialogResult.OK) {
                    string ext = Path.GetExtension(sfd.FileName).ToLower();

                    try {
                        // Create a completely independent copy
                        using (Bitmap independent = new Bitmap(_1_preview_img.Width, _1_preview_img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                            using (Graphics g = Graphics.FromImage(independent)) {
                                g.DrawImage(_1_preview_img, 0, 0, _1_preview_img.Width, _1_preview_img.Height);
                            }

                            if (ext == ".jpg" || ext == ".jpeg") {
                                ImageCodecInfo jpgEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                                using (EncoderParameters encParams = new EncoderParameters(1)) {
                                    encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                                    independent.Save(sfd.FileName, jpgEncoder, encParams);
                                }
                            } else if (ext == ".png") {
                                independent.Save(sfd.FileName, ImageFormat.Png);
                            }
                        }

                        MessageBox.Show("Image saved successfully at:\n" + sfd.FileName);
                    } catch (Exception ex) {
                        MessageBox.Show("Failed to save image: " + ex.Message);
                    }
                }
            }
        }

        private void btn_1_red_Click(object sender, EventArgs e) {
            if (_1_preview_img == null)
                return; // error

            string msg = txt_1_msg.Text;

            if (msg.Length < img_length)
                msg = msg.PadRight(img_length, ' ');

            bool[] bits = new bool[img_length * 8];

            for (int i = 0; i < img_length; i++) {
                byte b = (byte) msg[i];

                for (int bit = 0; bit < 8; bit++)
                    bits[i * 8 + bit] = (b & (1 << bit)) != 0;
            }

            Rectangle rect = new Rectangle(0, 0, _1_preview_img.Width, _1_preview_img.Height);

            var bmp = _1_preview_img.LockBits(rect, ImageLockMode.ReadWrite, _1_preview_img.PixelFormat);

            // bpp = bytes / pixel
            int bpp = Image.GetPixelFormatSize(_1_preview_img.PixelFormat) / 8;
            int byte_count = Math.Abs(bmp.Stride) * _1_preview_img.Height;

            byte[] pixels = new byte[byte_count];

            Marshal.Copy(bmp.Scan0, pixels, 0, byte_count);

            for (int i = 0; i * bpp < pixels.Length && i < bits.Length; i++) {
                if (bpp >= 3) {
                    pixels[i * bpp + 2] &= 0xFE;
                    if (bits[i])
                        pixels[i * bpp + 2] |= 1;
                }
            }

            Marshal.Copy(pixels, 0, bmp.Scan0, byte_count);
            _1_preview_img.UnlockBits(bmp);

            img_create_preview.Image = _1_preview_img;
        }

        private void btn_1_extract_Click(object sender, EventArgs e) {
            if (_1_preview_img == null) {
                MessageBox.Show("No image loaded!");
                return;
            }

            try {
                Rectangle rect = new Rectangle(0, 0, _1_preview_img.Width, _1_preview_img.Height);
                var bmp = _1_preview_img.LockBits(rect, ImageLockMode.ReadOnly, _1_preview_img.PixelFormat);

                int bpp = Image.GetPixelFormatSize(_1_preview_img.PixelFormat) / 8;
                int byteCount = Math.Abs(bmp.Stride) * _1_preview_img.Height;
                byte[] pixels = new byte[byteCount];
                Marshal.Copy(bmp.Scan0, pixels, 0, byteCount);
                _1_preview_img.UnlockBits(bmp);

                int idx = _1_combo.SelectedIndex;

                switch (idx) {
                    case 0:
                        idx = 2; // red
                        break;
                    case 1:
                        idx = 1; // green
                        break;
                    case 2:
                        idx = 0; // blue
                        break;
                    default:
                        idx = 3; // alpha
                        break;
                }

                bool[] bits = new bool[20 * 8];
                for (int i = 0; i < bits.Length && i * bpp + 2 < pixels.Length; i++) {
                    bits[i] = (pixels[i * bpp + idx] & 1) != 0;
                }

                byte[] extractedBytes = new byte[20];
                for (int i = 0; i < bits.Length; i++) {
                    if (bits[i])
                        extractedBytes[i / 8] |= (byte) (1 << (i % 8));
                }

                string message = Encoding.ASCII.GetString(extractedBytes);

                txt_1_debug.Text = message.Trim();
            } catch (Exception ex) {
                txt_1_debug.Text = "Error: " + ex.Message;
            }
        }

        private void btn_1_green_Click(object sender, EventArgs e) {
            if (_1_preview_img == null)
                return; // error

            string msg = txt_1_msg.Text;

            if (msg.Length < img_length)
                msg = msg.PadRight(img_length, ' ');

            bool[] bits = new bool[img_length * 8];

            for (int i = 0; i < img_length; i++) {
                byte b = (byte) msg[i];

                for (int bit = 0; bit < 8; bit++)
                    bits[i * 8 + bit] = (b & (1 << bit)) != 0;
            }

            Rectangle rect = new Rectangle(0, 0, _1_preview_img.Width, _1_preview_img.Height);

            var bmp = _1_preview_img.LockBits(rect, ImageLockMode.ReadWrite, _1_preview_img.PixelFormat);

            // bpp = bytes / pixel
            int bpp = Image.GetPixelFormatSize(_1_preview_img.PixelFormat) / 8;
            int byte_count = Math.Abs(bmp.Stride) * _1_preview_img.Height;

            byte[] pixels = new byte[byte_count];

            Marshal.Copy(bmp.Scan0, pixels, 0, byte_count);

            for (int i = 0; i * bpp < pixels.Length && i < bits.Length; i++) {
                if (bpp >= 3) {
                    pixels[i * bpp + 1] &= 0xFE;
                    if (bits[i])
                        pixels[i * bpp + 1] |= 1;
                }
            }

            Marshal.Copy(pixels, 0, bmp.Scan0, byte_count);
            _1_preview_img.UnlockBits(bmp);

            img_create_preview.Image = _1_preview_img;
        }

        private void btn_1_blue_Click(object sender, EventArgs e) {
            if (_1_preview_img == null)
                return; // error

            string msg = txt_1_msg.Text;

            if (msg.Length < img_length)
                msg = msg.PadRight(img_length, ' ');

            bool[] bits = new bool[img_length * 8];

            for (int i = 0; i < img_length; i++) {
                byte b = (byte) msg[i];

                for (int bit = 0; bit < 8; bit++)
                    bits[i * 8 + bit] = (b & (1 << bit)) != 0;
            }

            Rectangle rect = new Rectangle(0, 0, _1_preview_img.Width, _1_preview_img.Height);

            var bmp = _1_preview_img.LockBits(rect, ImageLockMode.ReadWrite, _1_preview_img.PixelFormat);

            // bpp = bytes / pixel
            int bpp = Image.GetPixelFormatSize(_1_preview_img.PixelFormat) / 8;
            int byte_count = Math.Abs(bmp.Stride) * _1_preview_img.Height;

            byte[] pixels = new byte[byte_count];

            Marshal.Copy(bmp.Scan0, pixels, 0, byte_count);

            for (int i = 0; i * bpp < pixels.Length && i < bits.Length; i++) {
                if (bpp >= 3) {
                    pixels[i * bpp] &= 0xFE;
                    if (bits[i])
                        pixels[i * bpp] |= 1;
                }
            }

            Marshal.Copy(pixels, 0, bmp.Scan0, byte_count);
            _1_preview_img.UnlockBits(bmp);

            img_create_preview.Image = _1_preview_img;
        }

        private void btn_1_alpha_Click(object sender, EventArgs e) {
            if (_1_preview_img == null)
                return; // error

            string msg = txt_1_msg.Text;

            if (msg.Length < img_length)
                msg = msg.PadRight(img_length, ' ');

            bool[] bits = new bool[img_length * 8];

            for (int i = 0; i < img_length; i++) {
                byte b = (byte) msg[i];

                for (int bit = 0; bit < 8; bit++)
                    bits[i * 8 + bit] = (b & (1 << bit)) != 0;
            }

            Rectangle rect = new Rectangle(0, 0, _1_preview_img.Width, _1_preview_img.Height);

            var bmp = _1_preview_img.LockBits(rect, ImageLockMode.ReadWrite, _1_preview_img.PixelFormat);

            // bpp = bytes / pixel
            int bpp = Image.GetPixelFormatSize(_1_preview_img.PixelFormat) / 8;
            int byte_count = Math.Abs(bmp.Stride) * _1_preview_img.Height;

            byte[] pixels = new byte[byte_count];

            Marshal.Copy(bmp.Scan0, pixels, 0, byte_count);

            for (int i = 0; i * bpp < pixels.Length && i < bits.Length; i++) {
                if (bpp >= 3) {
                    pixels[i * bpp + 3] &= 0xFE;
                    if (bits[i])
                        pixels[i * bpp + 3] |= 1;
                }
            }

            Marshal.Copy(pixels, 0, bmp.Scan0, byte_count);
            _1_preview_img.UnlockBits(bmp);

            img_create_preview.Image = _1_preview_img;
        }

        private void btn_1_hide_select_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png";
                ofd.Title = "Select an image";

                if (ofd.ShowDialog() == DialogResult.OK) {
                    img_create_hide.Image = Image.FromFile(ofd.FileName);

                    _1_hide_original = new Bitmap(ofd.FileName);
                    update_hide_preview();
                }
            }
        }

        private void btn_1_hide_hide_Click(object sender, EventArgs e) {
            try {
                if (_1_original_img == null || _1_hide_original == null) {
                    MessageBox.Show("No images selected.");
                }

                Bitmap base_img = new Bitmap(_1_original_img.Width, _1_original_img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(base_img))
                    g.DrawImage(_1_original_img, 0, 0);

                Bitmap hidden_img = new Bitmap(_1_hide_original);

                _1_preview_img = new Bitmap(base_img.Width, base_img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                for (int y = 0; y < base_img.Height; y++) {
                    for (int x = 0; x < base_img.Width; x++) {
                        System.Drawing.Color pixel = base_img.GetPixel(x, y);
                        bool bit = false;

                        if (x < hidden_img.Width && y < hidden_img.Height) {
                            System.Drawing.Color hid_pixel = hidden_img.GetPixel(x, y);

                            int gray = (hid_pixel.R + hid_pixel.G + hid_pixel.B) / 3;

                            bit = gray > 128; // white = 1, black = 0
                        }

                        int alpha = pixel.A & 0xFE; // no LSB

                        if (bit)
                            alpha |= 1;

                        System.Drawing.Color new_pixel = System.Drawing.Color.FromArgb(alpha, pixel.R, pixel.G, pixel.B);

                        _1_preview_img.SetPixel(x, y, new_pixel);
                    }
                }

                img_create_preview.Image = _1_preview_img;
                MessageBox.Show("Image hidden.");

            } catch (Exception ex) {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void btn_1_hide_extract_Click(object sender, EventArgs e) {
            try {
                if (_1_original_img == null) {
                    MessageBox.Show("No images selected.");
                }

                Bitmap base_img = new Bitmap(_1_original_img.Width, _1_original_img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                for (int y = 0; y < _1_original_img.Height; y++) {
                    for (int x = 0; x < _1_original_img.Width; x++) {
                        System.Drawing.Color pixel = _1_original_img.GetPixel(x, y);

                        bool bit = (pixel.A & 1) == 1;

                        System.Drawing.Color hid_pixel = bit ? System.Drawing.Color.White : System.Drawing.Color.Black;

                        base_img.SetPixel(x, y, hid_pixel);
                    }
                }

                img_create_hide.Image = base_img;
                MessageBox.Show("Image extracted.");

            } catch (Exception ex) {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void btn_2_select_Click(object sender, EventArgs e) {
            using OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = "Video Files|*.mp4;*.mkv;*.avi";

            if (ofd.ShowDialog() == DialogResult.OK) {
                media_player.Stop();

                using Media media = new Media(libVLC, new Uri(ofd.FileName));

                media_player.Play(media);
            }
        }

        private void btn_2_play_Click(object sender, EventArgs e) {
            media_player.Play();
        }

        private void btn_2_pause_Click(object sender, EventArgs e) {
            media_player.Pause();
        }

        private void btn_2_stop_Click(object sender, EventArgs e) {
            media_player.Stop();
        }

        private void panel_slider_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;

            int value = (media_player != null) ? (int) (media_player.Position * 100) : 50;

            g.Clear(System.Drawing.Color.Gray);

            int width = (int) (value / 100.0 * panel_slider.Width);
            g.FillRectangle(System.Drawing.Brushes.Coral, 0, 0, width, panel_slider.Height);

            g.FillEllipse(System.Drawing.Brushes.White, width - 5, 0, 10, panel_slider.Height);
        }

        private void timer_video_Tick(object sender, EventArgs e) {
            if (media_player != null && media_player.Length > 0) {
                panel_slider.Invalidate();
            }
        }

        private void update_slider(int mouseX) {
            int value = (int) ((float) mouseX / panel_slider.Width * 100);

            value = Math.Max(0, Math.Min(100, value));

            if (media_player != null && media_player.Length > 0)
                media_player.Position = value / 100.0f;

            panel_slider.Invalidate();
        }

        private void update_slider_4(int mouseX) {
            int value = (int) ((float) mouseX / _4_panel_slider.Width * 100);

            value = Math.Max(0, Math.Min(100, value));

            if (_4_media_player != null && _4_media_player.Length > 0)
                _4_media_player.Position = value / 100.0f;

            _4_panel_slider.Invalidate();
        }

        private void panel_slider_MouseDown(object sender, MouseEventArgs e) {
            update_slider(e.X);
        }

        private void panel_slider_MouseMove(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                update_slider(e.X);
            }
        }

        private void btn_2_lower_Click(object sender, EventArgs e) {
            if (media_player != null) {
                int vol = media_player.Volume - 10;

                media_player.Volume = Math.Max(0, vol);
                _2_label_volume.Text = media_player.Volume.ToString();
            }
        }

        private void btn_2_up_Click(object sender, EventArgs e) {
            if (media_player != null) {
                int vol = media_player.Volume + 10;

                media_player.Volume = Math.Min(100, vol);
                _2_label_volume.Text = media_player.Volume.ToString();
            }
        }

        private static IEnumerable<bool> BytesToBits(IEnumerable<byte> data) {
            foreach (var b in data)
                for (int bit = 0; bit < 8; bit++)
                    yield return ((b >> bit) & 1) != 0; // LSB-first
        }

        private static byte[] BitsToBytes(IReadOnlyList<bool> bits) {
            int len = bits.Count / 8;
            var bytes = new byte[len];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i])
                    bytes[i / 8] |= (byte) (1 << (i % 8));
            return bytes;
        }
        private void btn_2_hide_Click(object sender, EventArgs e) {
            if (_2_original_video.MediaPlayer?.Media == null) { MessageBox.Show("No video loaded in preview!"); return; }
            if (!File.Exists(ffmpeg_path)) { MessageBox.Show("FFmpeg not found!"); return; }

            string mrl = _2_original_video.MediaPlayer.Media.Mrl;
            string video_path = Uri.UnescapeDataString(new Uri(mrl).LocalPath);
            if (!File.Exists(video_path)) { MessageBox.Show("Video not found: " + video_path); return; }

            string output_path = Path.Combine(
                Path.GetDirectoryName(video_path)!,
                Path.GetFileNameWithoutExtension(video_path) + "_hidden_rgb.mp4"
            );

            // build payload: 'S','S', length(ushort, little-endian) + message bytes (trim/pad as needed)
            string msg = txt_2_msg.Text ?? string.Empty;

            if (msg.Length > video_length)
                msg = msg.Substring(0, video_length);

            byte[] payload = Encoding.ASCII.GetBytes(msg);
            ushort len = (ushort) payload.Length;

            var headerAndPayload = new List<byte>(4 + payload.Length) {
                    (byte)'S', (byte)'S', (byte)(len & 0xFF), (byte)((len >> 8) & 0xFF)
            };

            headerAndPayload.AddRange(payload);

            var allBits = BytesToBits(headerAndPayload).ToList();

            string tmp_folder = Path.Combine(Path.GetTempPath(), "frames_" + Guid.NewGuid().ToString("N"));

            try {
                Directory.CreateDirectory(tmp_folder);
                string frames_pattern = Path.Combine(tmp_folder, "frame_%04d.png");

                run_ffmpeg($"-y -vsync 0 -i \"{video_path}\" \"{frames_pattern}\"");

                var frame_files = Directory.GetFiles(tmp_folder, "frame_*.png").OrderBy(p => p).ToArray();
                if (frame_files.Length == 0) { MessageBox.Show("No frames extracted."); return; }

                if (allBits.Count > frame_files.Length) {
                    int bytesCap = frame_files.Length / 8 - 4; // subtract header (4 bytes)
                    MessageBox.Show($"Not enough frames. Capacity ≈ {Math.Max(bytesCap, 0)} bytes, need {payload.Length} bytes.");
                    return;
                }

                for (int i = 0; i < allBits.Count; i++) {
                    string frame_path = frame_files[i];
                    string tmp_file = frame_path + ".tmp";

                    using (var fs = new FileStream(frame_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var img = Image.FromStream(fs))
                    using (var bmp = new Bitmap(img)) {
                        int x = bmp.Width / 2, y = bmp.Height / 2;
                        var px = bmp.GetPixel(x, y);
                        int r = (px.R & 0xFE) | (allBits[i] ? 1 : 0);
                        bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(px.A, r, px.G, px.B));
                        bmp.Save(tmp_file, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    File.Delete(frame_path);
                    File.Move(tmp_file, frame_path);
                }

                string frames_in = Path.Combine(tmp_folder, "frame_%04d.png");
                string build_args =
                    $"-y -framerate 30 -i \"{frames_in}\" -i \"{video_path}\" " +
                    " -map 0:v:0 -map 1:a? " +
                    " -c:v libx264rgb -crf 0 -preset veryslow -pix_fmt rgb24 " +
                    " -movflags +faststart -shortest " +
                    $" \"{output_path}\"";

                run_ffmpeg(build_args);
                MessageBox.Show("Message hidden!\nSaved at: " + output_path);
            } catch (Exception ex) {
                MessageBox.Show("Error: " + ex.Message);
            } finally {
                try { if (Directory.Exists(tmp_folder)) Directory.Delete(tmp_folder, true); } catch { /* ignore */ }
            }
        }
        private void btn_2_extract_Click(object sender, EventArgs e) {
            try {
                if (_2_original_video?.MediaPlayer?.Media == null) { MessageBox.Show("No video loaded!"); return; }
                if (!File.Exists(ffmpeg_path)) { MessageBox.Show("FFmpeg not found at " + ffmpeg_path); return; }

                string mrl = _2_original_video.MediaPlayer.Media.Mrl;
                string video_path = Uri.UnescapeDataString(new Uri(mrl).LocalPath);
                if (!File.Exists(video_path)) { MessageBox.Show("Video not found at " + video_path); return; }

                string tmp_folder = Path.Combine(Path.GetTempPath(), "frames_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmp_folder);

                try {
                    string frames_pattern = Path.Combine(tmp_folder, "frame_%04d.png");

                    run_ffmpeg($"-y -vsync 0 -i \"{video_path}\" \"{frames_pattern}\"");

                    var frame_files = Directory.GetFiles(tmp_folder, "frame_*.png").OrderBy(p => p).ToArray();

                    if (frame_files.Length < 32) { MessageBox.Show("Not enough frames for header."); return; }

                    bool[] headerBits = new bool[32];

                    for (int i = 0; i < 32; i++) {
                        using (var fs = new FileStream(frame_files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var img = Image.FromStream(fs))
                        using (var bmp = new Bitmap(img)) {
                            int x = bmp.Width / 2, y = bmp.Height / 2;
                            var px = bmp.GetPixel(x, y);
                            headerBits[i] = (px.R & 1) != 0;
                        }
                    }

                    byte[] header = BitsToBytes(headerBits);

                    if (header[0] != (byte) 'S' || header[1] != (byte) 'S') {
                        MessageBox.Show("Magic header not found. This video may not contain an LSB message.");
                        return;
                    }

                    ushort len = (ushort) (header[2] | (header[3] << 8));
                    int payloadBits = len * 8;
                    int totalNeeded = 32 + payloadBits;

                    if (frame_files.Length < totalNeeded) {
                        MessageBox.Show($"Video too short for advertised payload. Need {len} bytes.");
                        return;
                    }

                    bool[] bits = new bool[payloadBits];

                    for (int i = 0; i < payloadBits; i++) {
                        int idx = 32 + i;
                        using (var fs = new FileStream(frame_files[idx], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var img = Image.FromStream(fs))
                        using (var bmp = new Bitmap(img)) {
                            int x = bmp.Width / 2, y = bmp.Height / 2;
                            var px = bmp.GetPixel(x, y);
                            bits[i] = (px.R & 1) != 0;
                        }
                    }

                    byte[] payload = BitsToBytes(bits);
                    string result = Encoding.ASCII.GetString(payload).TrimEnd('\0', ' ');
                    txt_2_result.Text = result.Length == 0 ? "(empty message)" : result;
                } finally {
                    try { if (Directory.Exists(tmp_folder)) Directory.Delete(tmp_folder, true); } catch { /* ignore */ }
                }
            } catch (Exception ex) {
                MessageBox.Show("Extraction error: " + ex.Message);
            }
        }

        private void update_preview_3() {
            if (_3_selected == null)
                return;

            Bitmap preview = new Bitmap(_3_selected.Width, _3_selected.Height);

            for (int y = 0; y < _3_selected.Height; y++) {
                for (int x = 0; x < _3_selected.Width; x++) {
                    System.Drawing.Color px = _3_selected.GetPixel(x, y);

                    int value = channel_index switch {
                        0 => px.R,
                        1 => px.G,
                        2 => px.B,
                        3 => px.A,
                        _ => 0
                    };

                    bool bit = (value & (1 << bit_layer)) != 0;

                    System.Drawing.Color col = bit ? System.Drawing.Color.White : System.Drawing.Color.Black;

                    preview.SetPixel(x, y, col);
                }
            }

            img_3_preview.Image = preview;
        }

        private void btn_3_select_Click(object sender, EventArgs e) {
            using OpenFileDialog ofd = new OpenFileDialog() {
                Filter = "Image Files|*.jpg;*.jpeg;*.png"
            };

            if (ofd.ShowDialog() == DialogResult.OK) {
                _3_selected = new Bitmap(ofd.FileName);

                _3_img_path = ofd.FileName;
                img_3_select.Image = new Bitmap(_3_selected);

                update_preview_3();
            }
        }

        private void btn_3_change_Click(object sender, EventArgs e) {
            channel_index = (channel_index + 1) % 4;
            txt_3_type.Text = channels[channel_index];

            update_preview_3();
        }

        private void btn_3_left_Click(object sender, EventArgs e) {
            bit_layer = (bit_layer - 1 + 8) % 8;
            txt_3_layer.Text = bit_layer.ToString();

            update_preview_3();
        }

        private void btn_3_right_Click(object sender, EventArgs e) {
            bit_layer = (bit_layer + 1) % 8;
            txt_3_layer.Text = bit_layer.ToString();

            update_preview_3();
        }

        private void btn_3_about_chi_Click(object sender, EventArgs e) {
            string url = "https://en.wikipedia.org/wiki/Pearson%27s_chi-squared_test";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void btn_3_about_rs_Click(object sender, EventArgs e) {
            string url = "https://www.researchgate.net/publication/2532941_Reliable_Detection_of_LSB_Steganography_in_Color_and_Grayscale_Images";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void analyze_export(string test, Func<Bitmap, int, int, double> score_func) {
            if (_3_selected == null || _3_img_path == null) {
                MessageBox.Show("No image selected.");
                return;
            }

            var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Results");

            worksheet.Cell(1, 1).Value = "Test";
            worksheet.Cell(1, 2).Value = "Channel";
            worksheet.Cell(1, 3).Value = "Layer";
            worksheet.Cell(1, 4).Value = "Score";
            worksheet.Cell(1, 5).Value = "Status";

            int row = 2;

            for (int col = 0; col < 4; col++) {
                for (int layer = 0; layer < 8; layer++) {
                    double score = score_func(_3_selected, col, layer);
                    string status = score > 0.9 ? "OK" : score > 0.5 ? "Suspect" : "Unnatural";

                    var fill = status == "OK" ? XLColor.LightGreen : status == "Suspect" ? XLColor.Yellow : XLColor.LightCoral;

                    worksheet.Cell(row, 1).Value = test;
                    worksheet.Cell(row, 2).Value = channels[col];
                    worksheet.Cell(row, 3).Value = layer;
                    worksheet.Cell(row, 4).Value = score;
                    worksheet.Cell(row, 5).Value = status;
                    worksheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = fill;
                    row++;
                }
            }

            string out_path = Path.Combine(Path.GetDirectoryName(_3_img_path),
                Path.GetFileNameWithoutExtension(_3_img_path) + $"_{test.Replace(" ", "_")}_report.xlsx");

            workbook.SaveAs(out_path);
            MessageBox.Show("Report saved at " + out_path);
        }

        private double score_chi_square(Bitmap bmp, int channel, int bit) {
            int[] count = new int[2];

            for (int y = 0; y < bmp.Height; y++) {
                for (int x = 0; x < bmp.Width; x++) {
                    System.Drawing.Color px = bmp.GetPixel(x, y);
                    int val = channel switch {
                        0 => px.R,
                        1 => px.G,
                        2 => px.B,
                        3 => px.A,
                        _ => 0
                    };
                    count[(val >> bit) & 1]++;
                }
            }

            int total = count[0] + count[1];
            if (total == 0)
                return 1.0;

            double ratio = (double) Math.Min(count[0], count[1]) / Math.Max(count[0], count[1]);

            // Now normalize this ratio into a score from 0 to 1
            // - ratio close to 1 => natural => score near 1
            // - ratio << 1 => suspicious => score near 0
            return Math.Round(ratio, 6);
        }

        private double score_rs_test(Bitmap bmp, int channel, int bit) {
            int flips = 0, matches = 0;

            for (int y = 0; y < bmp.Height; y++) {
                for (int x = 1; x < bmp.Width; x++) {
                    System.Drawing.Color a = bmp.GetPixel(x - 1, y), b = bmp.GetPixel(x, y);

                    int va = channel == 0 ? a.R : channel == 1 ? a.G : channel == 2 ? a.B : a.A;
                    int vb = channel == 0 ? b.R : channel == 1 ? b.G : channel == 2 ? b.B : b.A;

                    bool bit_a = ((va >> bit) & 1) == 1, bit_b = ((vb >> bit) & 1) == 1;

                    if (bit_a == bit_b)
                        matches++;
                    else
                        flips++;
                }
            }

            int total = matches + flips;

            if (total == 0)
                return 1.0;

            return (double) matches / total;
        }

        private void btn_3_rs_Click(object sender, EventArgs e) {
            analyze_export("RS test", score_rs_test);
        }

        private void btn_3_chi_Click(object sender, EventArgs e) {
            analyze_export("Chi-square test", score_chi_square);
        }

        private void btn_4_select_Click(object sender, EventArgs e) {
            using OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = "Video Files|*.mp4;*.mkv;*.avi";

            if (ofd.ShowDialog() == DialogResult.OK) {
                _4_media_player.Stop();

                using Media media = new Media(libVLC, new Uri(ofd.FileName));

                _4_media_player.Play(media);
            }
        }

        private void btn_4_play_Click(object sender, EventArgs e) {
            _4_media_player.Play();
        }

        private void btn_4_pause_Click(object sender, EventArgs e) {
            _4_media_player.Pause();
        }

        private void btn_4_stop_Click(object sender, EventArgs e) {
            _4_media_player.Stop();
        }

        private void _4_panel_slider_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;

            int value = (_4_media_player != null) ? (int) (_4_media_player.Position * 100) : 50;

            g.Clear(System.Drawing.Color.Gray);

            int width = (int) (value / 100.0 * panel_slider.Width);
            g.FillRectangle(System.Drawing.Brushes.Coral, 0, 0, width, panel_slider.Height);

            g.FillEllipse(System.Drawing.Brushes.White, width - 5, 0, 10, panel_slider.Height);
        }

        private void timer_4_video_Tick(object sender, EventArgs e) {
            if (_4_media_player != null && _4_media_player.Length > 0) {
                _4_panel_slider.Invalidate();
            }
        }

        private void btn_4_lower_Click(object sender, EventArgs e) {
            if (_4_media_player != null) {
                int vol = _4_media_player.Volume - 10;

                _4_media_player.Volume = Math.Max(0, vol);
                _4_label_volume.Text = _4_media_player.Volume.ToString();
            }
        }

        private void btn_4_up_Click(object sender, EventArgs e) {
            if (_4_media_player != null) {
                int vol = _4_media_player.Volume + 10;

                _4_media_player.Volume = Math.Min(100, vol);
                _4_label_volume.Text = _4_media_player.Volume.ToString();
            }
        }

        private void _4_panel_slider_MouseDown(object sender, MouseEventArgs e) {
            update_slider_4(e.X);
        }

        private void _4_panel_slider_MouseUp(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                update_slider_4(e.X);
            }
        }

        private void btn_4_start_Click(object sender, EventArgs e) {
            if (_4_original_video.MediaPlayer?.Media == null) {
                MessageBox.Show("No video loaded in preview!");
                return;
            }
            if (!File.Exists(ffmpeg_path)) {
                MessageBox.Show("FFmpeg not found!");
                return;
            }

            string mrl = _4_original_video.MediaPlayer.Media.Mrl;
            string video_path = Uri.UnescapeDataString(new Uri(mrl).LocalPath);
            if (!File.Exists(video_path)) {
                MessageBox.Show("Video not found: " + video_path);
                return;
            }

            string tmp_folder = Path.Combine(Path.GetTempPath(), "frames_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp_folder);
            string frames_pattern = Path.Combine(tmp_folder, "frame_%04d.png");
            run_ffmpeg($"-y -i \"{video_path}\" -vsync 0 -pix_fmt rgb24 \"{frames_pattern}\"");

            string[] frames = Directory.GetFiles(tmp_folder, "frame_*.png").OrderBy(f => f).ToArray();
            if (frames.Length == 0) {
                MessageBox.Show("No frames extracted");
                return;
            }

            frames = frames.Take(maxFrames).ToArray();

            var wb = new ClosedXML.Excel.XLWorkbook();

            string[] channelNames = { "R", "G", "B" }; // Skip Alpha channel
            for (int channel = 0; channel < channelNames.Length; channel++) {
                var wsChi = wb.Worksheets.Add($"Chi_{channelNames[channel]}");
                var wsRS = wb.Worksheets.Add($"RS_{channelNames[channel]}");

                wsChi.Cell(1, 1).Value = "Frame";
                wsRS.Cell(1, 1).Value = "Frame";

                wsChi.Column(1).Width = cell_width;
                wsRS.Column(1).Width = cell_width;

                for (int b = 0; b <= 1; b++) {
                    wsChi.Cell(1, b + 2).Value = $"Bit {b}";
                    wsRS.Cell(1, b + 2).Value = $"Bit {b}";
                }

                for (int i = 0; i < frames.Length; i++) {
                    wsChi.Cell(i + 2, 1).Value = Path.GetFileName(frames[i]);
                    wsRS.Cell(i + 2, 1).Value = Path.GetFileName(frames[i]);

                    using Bitmap bmp = new Bitmap(frames[i]);
                    for (int b = 0; b <= 1; b++) {
                        double scoreChi = score_chi_square(bmp, channel, b);
                        double scoreRS = score_rs_test(bmp, channel, b);

                        var chiCell = wsChi.Cell(i + 2, b + 2);
                        chiCell.Value = scoreChi;
                        if (scoreChi >= 0.8)
                            chiCell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
                        else if (scoreChi >= 0.4)
                            chiCell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
                        else
                            chiCell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightCoral;

                        var rsCell = wsRS.Cell(i + 2, b + 2);
                        rsCell.Value = scoreRS;
                        if (scoreRS >= 0.8)
                            rsCell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
                        else if (scoreRS >= 0.4)
                            rsCell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;
                        else
                            rsCell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightCoral;
                    }
                }
            }

            string out_path = Path.Combine(Path.GetDirectoryName(video_path)!, Path.GetFileNameWithoutExtension(video_path) + "_video_stego_report.xlsx");
            wb.SaveAs(out_path);

            MessageBox.Show("Analysis complete. Report saved at:\n" + out_path);

            Directory.Delete(tmp_folder, true);
        }

        private void btn_5_select_Click(object sender, EventArgs e) {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.Filter = "Image Files|*.png;*.jpg";
                if (ofd.ShowDialog() == DialogResult.OK) {
                    img_5_path = ofd.FileName;
                    img_5_select.Image = Image.FromFile(img_5_path);
                }
            }
        }

        private void btn_5_start_Click(object sender, EventArgs e) {
            if (string.IsNullOrEmpty(img_5_path) || !File.Exists(img_5_path)) {
                MessageBox.Show("Please select a valid image first.");
                return;
            }

            Bitmap original = new Bitmap(img_5_path);
            string baseName = Path.GetFileNameWithoutExtension(img_5_path);
            string folder = Path.Combine(Path.GetDirectoryName(img_5_path), baseName + "_filters");
            Directory.CreateDirectory(folder);

            // remove each bit layer for each channel
            for (int c = 0; c < 4; c++) { // R = 0, G = 1, B = 2, A = 3
                for (int bit = 0; bit < 8; bit++) {
                    Bitmap copy = new Bitmap(original.Width, original.Height);
                    for (int y = 0; y < original.Height; y++) {
                        for (int x = 0; x < original.Width; x++) {
                            System.Drawing.Color px = original.GetPixel(x, y);
                            byte[] channels = new byte[] { px.R, px.G, px.B, px.A };
                            
                            channels[c] = (byte) (channels[c] & ~(1 << bit));
                            
                            System.Drawing.Color newColor = System.Drawing.Color.FromArgb(channels[3], channels[0], channels[1], channels[2]);
                            copy.SetPixel(x, y, newColor);
                        }
                    }
                    copy.Save(Path.Combine(folder, $"{baseName}_{"RGBA"[c]}_bit{bit}_removed.png"));
                }
            }

            // red, green, blue filters
            void ApplyColorFilter(Func<System.Drawing.Color, System.Drawing.Color> filterFunc, string suffix) {
                Bitmap copy = new Bitmap(original.Width, original.Height);
                for (int y = 0; y < original.Height; y++)
                    for (int x = 0; x < original.Width; x++)
                        copy.SetPixel(x, y, filterFunc(original.GetPixel(x, y)));
                copy.Save(Path.Combine(folder, $"{baseName}_{suffix}.png"));
            }

            ApplyColorFilter(px => System.Drawing.Color.FromArgb(px.A, px.R, 0, 0), "red");
            ApplyColorFilter(px => System.Drawing.Color.FromArgb(px.A, 0, px.G, 0), "green");
            ApplyColorFilter(px => System.Drawing.Color.FromArgb(px.A, 0, 0, px.B), "blue");

            // gray
            ApplyColorFilter(px => {
                int gray = (px.R + px.G + px.B) / 3;
                return System.Drawing.Color.FromArgb(px.A, gray, gray, gray);
            }, "gray");


            // black and white
            ApplyColorFilter(px => {
                int avg = (px.R + px.G + px.B) / 3;
                return avg > 127 ? System.Drawing.Color.White : System.Drawing.Color.Black;
            }, "bw");


            // sepia
            ApplyColorFilter(px => {
                int r = (int) (px.R * 0.393 + px.G * 0.769 + px.B * 0.189);
                int g = (int) (px.R * 0.349 + px.G * 0.686 + px.B * 0.168);
                int b = (int) (px.R * 0.272 + px.G * 0.534 + px.B * 0.131);
                return System.Drawing.Color.FromArgb(px.A, Math.Min(r, 255), Math.Min(g, 255), Math.Min(b, 255));
            }, "sepia");


            // average of n x n blocks
            void BlockAverage(int blockSize, string suffix) {
                Bitmap result = new Bitmap(original.Width, original.Height);
                for (int y = 0; y < original.Height; y++) {
                    for (int x = 0; x < original.Width; x++) {
                        int rs = 0, gs = 0, bs = 0, count = 0;
                        for (int dy = -blockSize / 2; dy <= blockSize / 2; dy++) {
                            for (int dx = -blockSize / 2; dx <= blockSize / 2; dx++) {
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && ny >= 0 && nx < original.Width && ny < original.Height) {
                                    System.Drawing.Color npx = original.GetPixel(nx, ny);
                                    rs += npx.R;
                                    gs += npx.G;
                                    bs += npx.B;
                                    count++;
                                }
                            }
                        }
                        System.Drawing.Color avg = System.Drawing.Color.FromArgb(255, rs / count, gs / count, bs / count);
                        result.SetPixel(x, y, avg);
                    }
                }
                result.Save(Path.Combine(folder, $"{baseName}_{suffix}.png"));
            }

            BlockAverage(3, "3x3");
            BlockAverage(9, "9x9");

            // special 3x3 compare
            Bitmap special = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++) {
                for (int x = 0; x < original.Width; x++) {
                    int rs = 0, gs = 0, bs = 0, count = 0;

                    for (int dy = -1; dy <= 1; dy++) {
                        for (int dx = -1; dx <= 1; dx++) {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < original.Width && ny < original.Height) {
                                System.Drawing.Color npx = original.GetPixel(nx, ny);
                                rs += npx.R;
                                gs += npx.G;
                                bs += npx.B;
                                count++;
                            }
                        }
                    }
                    System.Drawing.Color p = original.GetPixel(x, y);
                    System.Drawing.Color avg = System.Drawing.Color.FromArgb(255, rs / count, gs / count, bs / count);
                    int r = p.R > avg.R ? 255 : 0;
                    int g = p.G > avg.G ? 255 : 0;
                    int b = p.B > avg.B ? 255 : 0;
                    
                    special.SetPixel(x, y, System.Drawing.Color.FromArgb(p.A, r, g, b));
                }
            }
            
            special.Save(Path.Combine(folder, $"{baseName}_special_3x3.png"));

            MessageBox.Show("All filters applied and saved.");
        }

        private void btn_5_about_Click(object sender, EventArgs e) {
           
        }
    }
}
