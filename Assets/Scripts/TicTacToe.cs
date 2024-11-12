// TicTacToe game for client side.

public class TicTacToe
{
    public enum Player { None, X, O }
    private Player[] board = new Player[9];
    public Player currentPlayer;
    private bool gameEnded;

    public TicTacToe()
    {
        ResetBoard();
    }

    public void ResetBoard()
    {
        for (int i = 0; i < 9; i++)
        {
            board[i] = Player.None;
        }
        currentPlayer = Player.X;
        gameEnded = false;
    }

    public bool MakeMove(int index)
    {
        if (board[index] == Player.None && !gameEnded)
        {
            board[index] = currentPlayer;
            if (CheckForWinner())
            {
                gameEnded = true;
                return true;
            }
            SwitchPlayer();
        }
        return false;
    }

    private void SwitchPlayer()
    {
        currentPlayer = (currentPlayer == Player.X) ? Player.O : Player.X;
    }

    private bool CheckForWinner()
    {
        int[,] winningCombos = new int[,]
        {
            { 0, 1, 2 }, { 3, 4, 5 }, { 6, 7, 8 },
            { 0, 3, 6 }, { 1, 4, 7 }, { 2, 5, 8 },
            { 0, 4, 8 }, { 2, 4, 6 }
        };

        for (int i = 0; i < 8; i++)
        {
            int a = winningCombos[i, 0];
            int b = winningCombos[i, 1];
            int c = winningCombos[i, 2];
            if (board[a] != Player.None && board[a] == board[b] && board[a] == board[c])
                return true;
        }
        return false;
    }

    public Player[] GetBoard() => board;
    public Player GetCurrentPlayer() => currentPlayer;
    public bool IsGameEnded() => gameEnded;
}
