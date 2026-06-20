using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace App.Ui
{
    public sealed class MainForm : Form
    {
        private readonly Panel _canvas;
        private readonly ComboBox _colorCombo;
        private readonly NumericUpDown _widthUpDown;
        private readonly Button _clearButton;
        private readonly Button _saveButton;
        private readonly List<Stroke> _strokes;
        private Stroke _currentStroke;
        private bool _isDrawing;

        public MainForm()
        {
            this.Text = "Paint";
            this.ClientSize = new Size(640, 480);
            this.StartPosition = FormStartPosition.CenterScreen;

            _strokes = new List<Stroke>();

            _canvas = new Panel();
            _canvas.Location = new Point(0, 0);
            _canvas.Size = new Size(640, 400);
            _canvas.BackColor = Color.White;
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseUp += Canvas_MouseUp;
            _canvas.Paint += Canvas_Paint;
            this.Controls.Add(_canvas);

            _colorCombo = new ComboBox();
            _colorCombo.Location = new Point(10, 410);
            _colorCombo.Size = new Size(120, 20);
            _colorCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _colorCombo.Items.Add("Black");
            _colorCombo.Items.Add("Red");
            _colorCombo.Items.Add("Blue");
            _colorCombo.Items.Add("Green");
            _colorCombo.SelectedIndex = 0;
            this.Controls.Add(_colorCombo);

            _widthUpDown = new NumericUpDown();
            _widthUpDown.Location = new Point(140, 410);
            _widthUpDown.Size = new Size(60, 20);
            _widthUpDown.Minimum = 1;
            _widthUpDown.Maximum = 10;
            _widthUpDown.Value = 1;
            this.Controls.Add(_widthUpDown);

            _clearButton = new Button();
            _clearButton.Text = "Clear";
            _clearButton.Location = new Point(210, 410);
            _clearButton.Size = new Size(75, 23);
            _clearButton.Click += ClearButton_Click;
            this.Controls.Add(_clearButton);

            _saveButton = new Button();
            _saveButton.Text = "Save";
            _saveButton.Location = new Point(290, 410);
            _saveButton.Size = new Size(75, 23);
            _saveButton.Click += SaveButton_Click;
            this.Controls.Add(_saveButton);
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDrawing = true;
                Point startPoint = e.Location;
                Color selectedColor = GetSelectedColor();
                int penWidth = (int)_widthUpDown.Value;
                _currentStroke = new Stroke(selectedColor, penWidth);
                _currentStroke.Points.Add(startPoint);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing && _currentStroke != null)
            {
                _currentStroke.Points.Add(e.Location);
                _canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _isDrawing)
            {
                _isDrawing = false;
                if (_currentStroke != null && _currentStroke.Points.Count > 0)
                {
                    _strokes.Add(_currentStroke);
                }
                _currentStroke = null;
                _canvas.Invalidate();
            }
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            foreach (Stroke stroke in _strokes)
            {
                using (Pen pen = new Pen(stroke.Color, stroke.Width))
                {
                    if (stroke.Points.Count > 1)
                    {
                        e.Graphics.DrawLines(pen, stroke.Points.ToArray());
                    }
                }
            }

            if (_currentStroke != null && _currentStroke.Points.Count > 1)
            {
                using (Pen pen = new Pen(_currentStroke.Color, _currentStroke.Width))
                {
                    e.Graphics.DrawLines(pen, _currentStroke.Points.ToArray());
                }
            }
        }

        private Color GetSelectedColor()
        {
            switch (_colorCombo.SelectedIndex)
            {
                case 0: return Color.Black;
                case 1: return Color.Red;
                case 2: return Color.Blue;
                case 3: return Color.Green;
                default: return Color.Black;
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            _strokes.Clear();
            _canvas.Invalidate();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            using (Bitmap bitmap = new Bitmap(_canvas.Width, _canvas.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                    foreach (Stroke stroke in _strokes)
                    {
                        using (Pen pen = new Pen(stroke.Color, stroke.Width))
                        {
                            if (stroke.Points.Count > 1)
                            {
                                g.DrawLines(pen, stroke.Points.ToArray());
                            }
                        }
                    }
                }

                try
                {
                    bitmap.Save("drawing.png", ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save image: " + ex.Message);
                }
            }
        }

        private class Stroke
        {
            public Color Color { get; set; }
            public int Width { get; set; }
            public List<Point> Points { get; set; }

            public Stroke(Color color, int width)
            {
                Color = color;
                Width = width;
                Points = new List<Point>();
            }
        }
    }
}