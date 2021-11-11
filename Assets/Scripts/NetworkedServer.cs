using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    LinkedList<PlayerAccount> playerAccounts;
    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
        playerAccounts = new LinkedList<PlayerAccount>();

    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);
        string[] csv = msg.Split(',');
      
        int signifier = int.Parse(csv[0]);

        if(signifier == ClientToServerSignifier.createAccount)
		{
            Debug.Log("Create Account");
           
            string n = csv[1];
            string p = csv[2];
            bool nameInUse = false;
			foreach (PlayerAccount pa in playerAccounts)
			{
                if (pa.name == n)
                {
                    nameInUse = true;
                    break;
                }
			}

            if(nameInUse)
			{
                SendMessageToClient(ServerToClientSignifier.AccountCreationFailed + "", id);
			}
            else
			{
                PlayerAccount newplayerAccount = new PlayerAccount(n, p);
                playerAccounts.AddLast(newplayerAccount);
                SendMessageToClient(ServerToClientSignifier.AccountCreationComplete + "", id);


            }
        }
        else if(signifier == ClientToServerSignifier.Login)
		{
            Debug.Log("Login");
        }
    }
}

public class PlayerAccount
{
    public string name, password;
    public PlayerAccount (string name, string password)
	{
        this.name = name;
        this.password = password;
	}
    
}

public static class ClientToServerSignifier
{
    public const int createAccount = 1;
    public const int Login = 2;
}


public static class ServerToClientSignifier
{
    public const int LoginComplete = 1;
    public const int LoginFailed = 2;
    public const int AccountCreationComplete = 3;
    public const int AccountCreationFailed = 4;
}