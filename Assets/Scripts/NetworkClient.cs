using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;
using UnityEngine.UI;
using TMPro;

public class NetworkClient : MonoBehaviour
{
    private enum UIState { Login, AccountCreation, Feedback }
    private UIState currentState;

    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField loginUsernameField;
    [SerializeField] private TMP_InputField loginPasswordField;
    [SerializeField] private GameObject accountCreationPanel;
    [SerializeField] private TMP_InputField createUsernameField;
    [SerializeField] private TMP_InputField createPasswordField;
    [SerializeField] private TextMeshProUGUI feedbackText;

    NetworkDriver networkDriver;
    NetworkConnection networkConnection;
    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;
    const ushort NetworkPort = 9001;
    const string IPAddress = "10.0.0.33";

    private void ChangeUIState(UIState newState)
    {
        currentState = newState;

        loginPanel.SetActive(currentState == UIState.Login);
        accountCreationPanel.SetActive(currentState == UIState.AccountCreation);
        feedbackText.gameObject.SetActive(currentState == UIState.Feedback);
    }

    public void OnLoginButtonClicked()
    {
        string username = loginUsernameField.text;
        string password = loginPasswordField.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            DisplayFeedback("Please enter both a username and password.");
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
            DisplayFeedback("Please enter both a username and password to create an account.");
            return;
        }

        SendMessageToServer($"CreateAccount:{username}:{password}");
    }

    public void OnNewAccountButtonClicked()
    {
        ChangeUIState(UIState.AccountCreation);
    }

    private void ProcessReceivedMsg(string msg)
    {
        Debug.Log("Msg received = " + msg);

        if (msg.StartsWith("LoginSuccess"))
        {
            DisplayFeedback("Login successful!");
        }
        else if (msg.StartsWith("LoginFailed"))
        {
            DisplayFeedback("Login failed: " + msg.Split(':')[1]);
        }
        else if (msg.StartsWith("AccountCreated"))
        {
            DisplayFeedback("Account created successfully!");
        }
        else if (msg.StartsWith("AccountCreationFailed"))
        {
            DisplayFeedback("Account creation failed: " + msg.Split(':')[1]);
        }
        else
        {
            DisplayFeedback("Unknown response from server.");
        }
    }

    private void DisplayFeedback(string message)
    {
        ChangeUIState(UIState.Feedback);
        feedbackText.text = message;
        Invoke("ReturnToLogin", 2.0f);
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

