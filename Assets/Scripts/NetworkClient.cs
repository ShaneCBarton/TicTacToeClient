using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;
using UnityEngine.UI;
using TMPro;
using static TicTacToe;

public class NetworkClient : MonoBehaviour
{
    private enum UIState { Login, AccountCreation, Room, Playing, Feedback, Waiting }
    private UIState currentState;
    private enum PlayerRole { Player1, Player2, Spectator, None }
    private PlayerRole currentRole = PlayerRole.None;

    [Header("Login")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField loginUsernameField;
    [SerializeField] private TMP_InputField loginPasswordField;

    [Header("AccountCreation")]
    [SerializeField] private GameObject accountCreationPanel;
    [SerializeField] private TMP_InputField createUsernameField;
    [SerializeField] private TMP_InputField createPasswordField;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Room")]
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private TMP_InputField roomNameField;
    [SerializeField] private TextMeshProUGUI roomFeedbackText;

    [Header("Waiting")]
    [SerializeField] private GameObject waitingPanel;
    [SerializeField] private TextMeshProUGUI waitingFeedbackText;

    [Header("Playing")]
    [SerializeField] private GameObject playingPanel;

    [Header("TicTacToe Game")]
    [SerializeField] private Button[] boardButtons;
    [SerializeField] private TextMeshProUGUI currentPlayerText;
    [SerializeField] private TextMeshProUGUI gameStatusText;
    private TicTacToe game;

    NetworkDriver networkDriver;
    NetworkConnection networkConnection;
    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;
    const ushort NetworkPort = 9001;
    const string IPAddress = "10.0.0.33";

    private string playerName;
    private bool isMyTurn = false;

    private void ChangeUIState(UIState newState)
    {
        currentState = newState;

        loginPanel.SetActive(false);
        accountCreationPanel.SetActive(false);
        roomPanel.SetActive(false);
        waitingPanel.SetActive(false);
        playingPanel.SetActive(false);
        feedbackText.gameObject.SetActive(false);

        switch (currentState)
        {
            case UIState.Login:
                loginPanel.SetActive(true);
                break;
            case UIState.AccountCreation:
                accountCreationPanel.SetActive(true);
                break;
            case UIState.Room:
                roomPanel.SetActive(true);
                break;
            case UIState.Playing:
                playingPanel.SetActive(true);
                break;
            case UIState.Feedback:
                feedbackText.gameObject.SetActive(true);
                break;
            case UIState.Waiting:
                waitingPanel.SetActive(true);
                DisplayWaitingFeedback("Waiting for player to join.");
                break;
        }
    }

    public void OnLoginButtonClicked()
    {
        string username = loginUsernameField.text;
        string password = loginPasswordField.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            DisplayFeedback("Error: Please enter both a username and password.");
            return;
        }

        SendMessageToServer($"Login:{username}:{password}");
    }

    public void OnCreateAccountButtonClicked()
    {
        string username = createUsernameField.text;
        string password = createPasswordField.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            DisplayFeedback("Error: Please enter both a username and password to create an account.");
            return;
        }

        SendMessageToServer($"CreateAccount:{username}:{password}");
    }

    public void OnNewAccountButtonClicked()
    {
        ChangeUIState(UIState.AccountCreation);
    }
    public void OnSendMessageButtonClicked()
    {
        SendMessageToServer("PlayerMessage:Hello opponent!");
    }

    private void ProcessReceivedMsg(string msg)
    {
        Debug.Log("Msg received = " + msg);

        if (msg.StartsWith("LoginSuccess"))
        {
            playerName = loginUsernameField.text;
            DisplayFeedback("Login successful!");
            ChangeUIState(UIState.Room);
        }
        else if (msg.StartsWith("LoginFailed"))
        {
            DisplayFeedback("Error: Login failed: " + msg.Split(':')[1]);
        }
        else if (msg.StartsWith("AccountCreated"))
        {
            DisplayFeedback("No Error: Account created successfully!");
        }
        else if (msg.StartsWith("AccountCreationFailed"))
        {
            DisplayFeedback("Error: Account creation failed: " + msg.Split(':')[1]);
        }
        else if (msg.StartsWith("RoomCreated:"))
        {
            DisplayRoomFeedback("Room created successfully: " + msg.Split(':')[1]);
            ChangeUIState(UIState.Waiting);
        }
        else if (msg.StartsWith("JoinedRoom:"))
        {
            DisplayRoomFeedback("Joined room: " + msg.Split(':')[1]);
            ChangeUIState(UIState.Waiting);
        }
        else if (msg.StartsWith("WaitingForPlayers"))
        {
            ChangeUIState(UIState.Waiting);
        }
        else if (msg.StartsWith("RoomExists:"))
        {
            DisplayRoomFeedback("RoomExists: " + msg.Split(':')[1]);
            string roomName = msg.Split(':')[1];
            SendMessageToServer($"JoinRoom:{roomName}");
        }
        else if (msg.StartsWith("RoomDoesNotExist:"))
        {
            DisplayRoomFeedback("Room does not exist: " + msg.Split(':')[1]);
            string roomName = msg.Split(':')[1];
            SendMessageToServer($"CreateRoom:{roomName}");
        }
        else if (msg.StartsWith("PlayerLeft:"))
        {
            string playerName = msg.Split(':')[1];
            DisplayRoomFeedback($"Player {playerName} has left the room");
            ChangeUIState(UIState.Room);
        }
        else if (msg.StartsWith("GameStarted:"))
        {
            string[] parts = msg.Split(':');
            isMyTurn = parts.Length > 2 && parts[2] == "true";
            game.ResetBoard();
            SetBoardButtonsToEmpty();
            ChangeUIState(UIState.Playing);
            UpdateGameUI();
        }
        else if (msg.StartsWith("YourTurn"))
        {
            isMyTurn = true;
            UpdateGameUI();
        }
        else if (msg.StartsWith("OpponentTurn"))
        {
            isMyTurn = false;
            UpdateGameUI();
        }
        else if (msg.StartsWith("OpponentMessage:"))
        {
            string message = msg.Split(':')[1];
            Debug.Log($"Message from opponent: {message}");
        }
        else if (msg.StartsWith("PlayerMove:"))
        {
            int index = int.Parse(msg.Split(':')[1]);
            SendMessageToServer($"PlayerMove:{roomNameField.text}:{index}");
        }
        else if (msg.StartsWith("GameStarted:"))
        {
            game.ResetBoard();
            SetBoardButtonsToEmpty();
            ChangeUIState(UIState.Playing);
            UpdateGameUI();
        }
        else if (msg.StartsWith("BoardState:"))
        {
            string[] boardState = msg.Split(':')[1].Split(',');
            UpdateBoardFromServer(boardState);
        }
        else if (msg.StartsWith("GameOver:"))
        {
            string result = msg.Split(':')[1];
            DisplayGameResult(result);
        }
        else if (msg.StartsWith("SpectatorAssigned:"))
        {
            currentRole = PlayerRole.Spectator;
            isMyTurn = false;
            ChangeUIState(UIState.Playing);
            UpdateGameUI();
        }
        else
        {
            DisplayFeedback("Unknown response from server.");
        }
    }

    public void OnJoinOrCreateRoomButtonClicked()
    {
        string roomName = roomNameField.text;
        if (string.IsNullOrEmpty(roomName))
        {
            DisplayRoomFeedback("Please enter a room name.");
            return;
        }

        SendMessageToServer($"CheckRoom:{roomName}");
    }

    public void OnLeaveRoomButtonClicked()
    {
        SendMessageToServer($"LeaveRoom:{roomNameField.text}");
    }

    private void DisplayRoomFeedback(string message)
    {
        roomFeedbackText.text = message;
    }

    private void DisplayWaitingFeedback(string message)
    {
        waitingFeedbackText.text = message;
    }

    private void DisplayFeedback(string message)
    {
        ChangeUIState(UIState.Feedback);
        feedbackText.text = message;
        if (message.Contains("Error"))
        {
            Invoke("ReturnToLogin", 2.0f);
        }
    }

    private void ReturnToLogin()
    {
        ChangeUIState(UIState.Login);
    }


    void Start()
    {
        networkDriver = NetworkDriver.Create();
        reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));
        networkConnection = default(NetworkConnection);
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(IPAddress, NetworkPort, NetworkFamily.Ipv4);
        networkConnection = networkDriver.Connect(endpoint);

        game = new TicTacToe();
        InitializeBoardButtons();

        SetBoardButtonsToEmpty();

        ChangeUIState(UIState.Login);
    }

    public void OnDestroy()
    {
        networkConnection.Disconnect(networkDriver);
        networkConnection = default(NetworkConnection);
        networkDriver.Dispose();
    }

    void Update()
    {
        #region Check Input and Send Msg

        if (Input.GetKeyDown(KeyCode.A))
            SendMessageToServer("Hello server's world, sincerely your network client");

        #endregion

        networkDriver.ScheduleUpdate().Complete();

        #region Check for client to server connection

        if (!networkConnection.IsCreated)
        {
            Debug.Log("Client is unable to connect to server");
            return;
        }

        #endregion

        #region Manage Network Events

        NetworkEvent.Type networkEventType;
        DataStreamReader streamReader;
        NetworkPipeline pipelineUsedToSendEvent;

        while (PopNetworkEventAndCheckForData(out networkEventType, out streamReader, out pipelineUsedToSendEvent))
        {
            if (pipelineUsedToSendEvent == reliableAndInOrderPipeline)
                Debug.Log("Network event from: reliableAndInOrderPipeline");
            else if (pipelineUsedToSendEvent == nonReliableNotInOrderedPipeline)
                Debug.Log("Network event from: nonReliableNotInOrderedPipeline");

            switch (networkEventType)
            {
                case NetworkEvent.Type.Connect:
                    Debug.Log("We are now connected to the server");
                    break;
                case NetworkEvent.Type.Data:
                    int sizeOfDataBuffer = streamReader.ReadInt();
                    NativeArray<byte> buffer = new NativeArray<byte>(sizeOfDataBuffer, Allocator.Persistent);
                    streamReader.ReadBytes(buffer);
                    byte[] byteBuffer = buffer.ToArray();
                    string msg = Encoding.Unicode.GetString(byteBuffer);
                    ProcessReceivedMsg(msg);
                    buffer.Dispose();
                    break;
                case NetworkEvent.Type.Disconnect:
                    Debug.Log("Client has disconnected from server");
                    networkConnection = default(NetworkConnection);
                    break;
            }
        }

        #endregion
    }

    private void InitializeBoardButtons()
    {
        for (int i = 0; i < boardButtons.Length; i++)
        {
            int index = i;
            boardButtons[i].onClick.AddListener(() => OnBoardButtonClick(index));
        }
    }

    private void OnBoardButtonClick(int index)
    {
        if (!isMyTurn)
        {
            DisplayGameStatus("Wait for your turn!");
            return;
        }
        SendMessageToServer($"PlayerMove:{roomNameField.text}:{index}");
    }

    private void DisplayGameStatus(string message)
    {
        gameStatusText.text = message;
    }

    private void SetBoardButtonsToEmpty()
    {
        foreach (var button in boardButtons)
        {
            button.GetComponentInChildren<TextMeshProUGUI>().text = "";
        }

        gameStatusText.text = "";
        currentPlayerText.text = "Current Player: X";
    }

    private void UpdateBoardFromServer(string[] boardState)
    {
        for (int i = 0; i < boardState.Length; i++)
        {
            if (boardState[i] == "X")
                game.GetBoard()[i] = Player.X;
            else if (boardState[i] == "O")
                game.GetBoard()[i] = Player.O;
            else
                game.GetBoard()[i] = Player.None;
        }

        UpdateGameUI();
    }

    private void UpdateGameUI()
    {
        Player[] board = game.GetBoard();
        for (int i = 0; i < board.Length; i++)
        {
            boardButtons[i].GetComponentInChildren<TextMeshProUGUI>().text =
                board[i] == Player.X ? "X" :
                board[i] == Player.O ? "O" : "";

            boardButtons[i].interactable = currentRole != PlayerRole.Spectator &&
                                         isMyTurn &&
                                         board[i] == Player.None;
        }

        string statusText = currentRole == PlayerRole.Spectator ?
            "Spectating" :
            $"Current Player: {(isMyTurn ? playerName : "Opponent")} ({game.GetCurrentPlayer()})";
        currentPlayerText.text = statusText;
    }



    private void DisplayGameResult(string result)
    {
        gameStatusText.text = result;
        Invoke("ReturnToRoom", 5.0f);
    }

    private void ReturnToRoom()
    {
        ChangeUIState(UIState.Room);
    }


    private bool PopNetworkEventAndCheckForData(out NetworkEvent.Type networkEventType, out DataStreamReader streamReader, out NetworkPipeline pipelineUsedToSendEvent)
    {
        networkEventType = networkConnection.PopEvent(networkDriver, out streamReader, out pipelineUsedToSendEvent);

        if (networkEventType == NetworkEvent.Type.Empty)
            return false;
        return true;
    }

    public void SendMessageToServer(string msg)
    {
        byte[] msgAsByteArray = Encoding.Unicode.GetBytes(msg);
        NativeArray<byte> buffer = new NativeArray<byte>(msgAsByteArray, Allocator.Persistent);

        DataStreamWriter streamWriter;
        networkDriver.BeginSend(reliableAndInOrderPipeline, networkConnection, out streamWriter);
        streamWriter.WriteInt(buffer.Length);
        streamWriter.WriteBytes(buffer);
        networkDriver.EndSend(streamWriter);

        buffer.Dispose();
    }

}

