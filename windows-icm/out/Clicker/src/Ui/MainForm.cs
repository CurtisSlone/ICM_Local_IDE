using System;
using System.Drawing;
using System.Windows.Forms;

namespace App.Ui
{
    public sealed partial class MainForm : Form
    {
        private Button _clickButton;
        private Label _clickLabel;
        private int _clickCount;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Clicker";
            this.Size = new Size(300, 160);
            this.StartPosition = FormStartPosition.CenterScreen;

            _clickButton = new Button();
            _clickButton.Text = "Click me";
            _clickButton.Size = new Size(100, 30);
            _clickButton.Location = new Point(100, 20);
            _clickButton.Click += ClickButton_Click;

            _clickLabel = new Label();
            _clickLabel.Text = "Clicks: 0";
            _clickLabel.Size = new Size(200, 23);
            _clickLabel.Location = new Point(50, 70);
            _clickLabel.TextAlign = ContentAlignment.MiddleCenter;

            this.Controls.Add(_clickButton);
            this.Controls.Add(_clickLabel);
        }

        private void ClickButton_Click(object sender, EventArgs e)
        {
            _clickCount++;
            _clickLabel.Text = "Clicks: " + _clickCount.ToString();
        }
    }
}
