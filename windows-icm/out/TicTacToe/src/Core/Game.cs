using System;

namespace App.Core
{
    public class Game
    {
        private char[,] board;
        private char currentPlayer;

        public Game()
        {
            this.board = new char[3, 3];
            this.currentPlayer = 'X';
            this.Reset();
        }

        public char CurrentPlayer
        {
            get
            {
                return this.currentPlayer;
            }
        }

        public char CellAt(int row, int col)
        {
            return this.board[row, col];
        }

        public bool Play(int row, int col)
        {
            if (row < 0 || row > 2 || col < 0 || col > 2)
            {
                return false;
            }

            if (this.board[row, col] != ' ')
            {
                return false;
            }

            if (this.Winner() != ' ')
            {
                return false;
            }

            this.board[row, col] = this.currentPlayer;
            if (this.currentPlayer == 'X')
            {
                this.currentPlayer = 'O';
            }
            else
            {
                this.currentPlayer = 'X';
            }

            return true;
        }

        public char Winner()
        {
            // Check rows
            for (int i = 0; i < 3; i++)
            {
                if (this.board[i, 0] != ' ' && this.board[i, 0] == this.board[i, 1] && this.board[i, 1] == this.board[i, 2])
                {
                    return this.board[i, 0];
                }
            }

            // Check columns
            for (int i = 0; i < 3; i++)
            {
                if (this.board[0, i] != ' ' && this.board[0, i] == this.board[1, i] && this.board[1, i] == this.board[2, i])
                {
                    return this.board[0, i];
                }
            }

            // Check diagonals
            if (this.board[0, 0] != ' ' && this.board[0, 0] == this.board[1, 1] && this.board[1, 1] == this.board[2, 2])
            {
                return this.board[0, 0];
            }

            if (this.board[0, 2] != ' ' && this.board[0, 2] == this.board[1, 1] && this.board[1, 1] == this.board[2, 0])
            {
                return this.board[0, 2];
            }

            return ' ';
        }

        public bool IsDraw()
        {
            if (this.Winner() != ' ')
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (this.board[i, j] == ' ')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void Reset()
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    this.board[i, j] = ' ';
                }
            }

            this.currentPlayer = 'X';
        }
    }
}