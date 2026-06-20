using System;
using System.Drawing;
using System.Windows.Forms;
using App.Core;

namespace App.Ui
{
    public sealed class MainForm : Form
    {
        private Game game;
        private Button[,] buttons;
        private Label statusLabel;
        private Button newGameButton;

        public MainForm()
        {
            this.game = new Game();
            this.buttons = new Button[3, 3];

            this.Text = "Tic Tac Toe";
            this.ClientSize = new Size(200, 280);
            this.StartPosition = FormStartPosition.CenterScreen;

            this.statusLabel = new Label();
            this.statusLabel.Text = "Player X's turn";
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new Point(20, 20);
            this.Controls.Add(this.statusLabel);

            int buttonSize = 60;
            int buttonSpacing = 5;
            int gridWidth = 3 * buttonSize + 2 * buttonSpacing;
            int startX = (this.ClientSize.Width - gridWidth) / 2;
            int startY = 50;

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    Button button = new Button();
                    button.Size = new Size(buttonSize, buttonSize);
                    button.Location = new Point(startX + col * (buttonSize + buttonSpacing), startY + row * (buttonSize + buttonSpacing));
                    button.Click += OnButtonClick;
                    this.Controls.Add(button);
                    this.buttons[row, col] = button;
                }
            }

            this.newGameButton = new Button();
            this.newGameButton.Text = "New Game";
            this.newGameButton.Size = new Size(100, 30);
            this.newGameButton.Location = new Point((this.ClientSize.Width - this.newGameButton.Width) / 2, startY + 3 * (buttonSize + buttonSpacing) + 10);
            this.newGameButton.Click += OnNewGameClick;
            this.Controls.Add(this.newGameButton);

            UpdateBoard();
        }

        private void OnButtonClick(object sender, EventArgs e)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (this.buttons[row, col] == sender)
                    {
                        if (this.game.Play(row, col))
                        {
                            UpdateBoard();
                        }
                        return;
                    }
                }
            }
        }

        private void OnNewGameClick(object sender, EventArgs e)
        {
            this.game.Reset();
            UpdateBoard();
        }

        private void UpdateBoard()
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    char cellValue = this.game.CellAt(row, col);
                    this.buttons[row, col].Text = cellValue.ToString();
                }
            }

            char winner = this.game.Winner();
            if (winner != ' ')
            {
                this.statusLabel.Text = "Player " + winner + " wins!";
            }
            else if (this.game.IsDraw())
            {
                this.statusLabel.Text = "It's a draw!";
            }
            else
            {
                this.statusLabel.Text = "Player " + this.game.CurrentPlayer + "'s turn";
            }
        }
    }
}