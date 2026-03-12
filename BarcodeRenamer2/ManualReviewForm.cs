using System;
using System.Drawing;
using System.Windows.Forms;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 人工审核窗体
    /// </summary>
    public class ManualReviewForm : Form
    {
        private readonly FileItem fileItem;
        private readonly Action<FileItem, string> onSave;

        private PictureBox pictureBox;
        private TextBox txtBarcode;
        private Button btnSave;
        private Button btnCancel;
        private Label lblFileName;

        public ManualReviewForm(FileItem fileItem, Action<FileItem, string> onSave)
        {
            this.fileItem = fileItem;
            this.onSave = onSave;

            InitializeComponents();
            LoadImage();
        }

        private void InitializeComponents()
        {
            // 窗体设置
            this.Text = "人工审核 - 条形码识别";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 文件名标签
            lblFileName = new Label
            {
                Text = $"文件名: {fileItem.FileName}",
                Location = new Point(10, 10),
                Size = new Size(760, 20),
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold)
            };

            // 图片显示框
            pictureBox = new PictureBox
            {
                Location = new Point(10, 40),
                Size = new Size(760, 420),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // 条形码输入框标签
            Label lblBarcode = new Label
            {
                Text = "条形码内容:",
                Location = new Point(10, 480),
                Size = new Size(100, 25),
                Font = new Font("Microsoft YaHei UI", 9)
            };

            // 条形码输入框
            txtBarcode = new TextBox
            {
                Location = new Point(110, 480),
                Size = new Size(560, 25),
                Font = new Font("Microsoft YaHei UI", 10)
            };

            // 保存按钮
            btnSave = new Button
            {
                Text = "保存",
                Location = new Point(680, 478),
                Size = new Size(90, 28),
                Font = new Font("Microsoft YaHei UI", 9),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.Click += BtnSave_Click;

            // 取消按钮
            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(680, 520),
                Size = new Size(90, 28),
                Font = new Font("Microsoft YaHei UI", 9),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            // 添加控件
            this.Controls.AddRange(new Control[] {
                lblFileName,
                pictureBox,
                lblBarcode,
                txtBarcode,
                btnSave,
                btnCancel
            });
        }

        private void LoadImage()
        {
            try
            {
                if (System.IO.File.Exists(fileItem.FilePath))
                {
                    using (var image = Image.FromFile(fileItem.FilePath))
                    {
                        pictureBox.Image = new Bitmap(image);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string barcode = txtBarcode.Text.Trim();

            if (string.IsNullOrWhiteSpace(barcode))
            {
                MessageBox.Show("请输入条形码内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            onSave?.Invoke(fileItem, barcode);
            this.DialogResult = DialogResult.OK;
        }
    }
}
