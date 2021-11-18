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
    const int playerAccountRecord = 1;
    string playerAccountsFilePath;
    int playerWaitingForMatchWithID = -1;
    //  LinkedList<GameRoom> gameRooms;
    List<GameRoom> gameRooms;
    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
        playerAccountsFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt";
        playerAccounts = new LinkedList<PlayerAccount>();
        LoadPlayerAccounts();
        gameRooms = new List<GameRoom>();
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
      
        if (signifier == ClientToServerSignifier.createAccount)
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
                PlayerAccount newplayerAccount = new PlayerAccount(n, p, id);
                playerAccounts.AddLast(newplayerAccount);
                SendMessageToClient(ServerToClientSignifier.AccountCreationComplete + "," + n, id);

                SavePlayerAccounts();
            }
        }
        else if(signifier == ClientToServerSignifier.Login)
		{
            Debug.Log("Login");
            bool hasNameBeenFound = false;
            bool masHasBeenSentToClient = false;
            string n = csv[1];
            string p = csv[2];
            foreach (PlayerAccount pa in playerAccounts)
			{
                if(pa.name == n)
				{
                    hasNameBeenFound = true;
                    if(pa.password == p)
					{
                        SendMessageToClient(ServerToClientSignifier.LoginComplete + ","+ n, id);
                        pa.id = id;
                        masHasBeenSentToClient = true;
                    }
                    else
					{
                        SendMessageToClient(ServerToClientSignifier.LoginFailed + "", id);
                        masHasBeenSentToClient = true;

                    }
                }
                
			}
            if(!hasNameBeenFound)
			{
                if(!masHasBeenSentToClient)
				{
                    SendMessageToClient(ServerToClientSignifier.LoginFailed + "", id);

                }
            }
        }
        else if (signifier == ClientToServerSignifier.JoinQueueForGameRoom)
		{
            Debug.Log("waiting for other player");
            if (playerWaitingForMatchWithID == -1)
            {
                playerWaitingForMatchWithID = id;
            }
            else
			{
                GameRoom gr = new GameRoom(playerWaitingForMatchWithID, id);
                gameRooms.Add(gr);
                gr.gameTurn = (Random.Range(0, 2) == 0) ? gr.playerID1 : gr.playerID2;
                SendMessageToClient(ServerToClientSignifier.GameStart + "", gr.playerID1);
                SendMessageToClient(ServerToClientSignifier.GameStart + "", gr.playerID2);
                playerWaitingForMatchWithID = -1;

                string txtMsg = "It's "+GetPlayerAccountByID(gr.gameTurn).name+ "'s turn.";
                
                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.playerID1);
                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.playerID2);
                
                int token = Random.Range(0, 2);
                gr.token = token;
                int token2 = (token == 0) ? 1 : 0;
                gr.token2 = token2;
                SendMessageToClient(ServerToClientSignifier.TurnInGame + "," + token+"," + token2 + "," + GetPlayerAccountByID(gr.playerID1).name + "," + GetPlayerAccountByID(gr.playerID2).name, gr.playerID1);
                SendMessageToClient(ServerToClientSignifier.TurnInGame +"," + token2 + "," + token + "," + GetPlayerAccountByID(gr.playerID2).name + "," + GetPlayerAccountByID(gr.playerID1).name, gr.playerID2);
				
			}
		}
        else if (signifier == ClientToServerSignifier.requestToObserveGame)
        {
            if(gameRooms.Count>0)
			{
                gameRooms[Random.Range(0, gameRooms.Count)].observersID.Add(id);
                SendMessageToClient(ServerToClientSignifier.ObserveGameAccepted + "", id);
                GameRoom gr = GetGameRoomWithClientID(id);
                SendMessageToClient(ServerToClientSignifier.TurnInGame + "," + gr.token + "," + gr.token2 + "," + GetPlayerAccountByID(gr.playerID1).name + "," + GetPlayerAccountByID(gr.playerID2).name, id);
                string txtMsg = "It's " + GetPlayerAccountByID(gr.gameTurn).name + "'s turn.";

                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, id);


            }
            else
			{
                SendMessageToClient(ServerToClientSignifier.ObserveGameFailed + "", id);

            }
        }
        else if (signifier == ClientToServerSignifier.GameRoomPlay)
        {
            GameRoom gr = GetGameRoomWithClientID(id);
            if(gr!= null)
			{
                if(gr.playerID1 == id)
				{
                   // send
				}
			}
        }
        else if(signifier == ClientToServerSignifier.SendTextMessage)
		{
            string txtMsg = csv[1] + ": " + csv[2];
            GameRoom gr = GetGameRoomWithClientID(id);
            if (gr != null)
            {
                SendMessageToClient(ServerToClientSignifier.TextChatMeassage + "," + txtMsg, gr.playerID1);
                SendMessageToClient(ServerToClientSignifier.TextChatMeassage + "," +txtMsg, gr.playerID2);
                for (int i = 0; i < gr.observersID.Count; i++)
                {
                    SendMessageToClient(ServerToClientSignifier.TextChatMeassage + "," + txtMsg, gr.observersID[i]);

                }
            }
        }
        else if (signifier == ClientToServerSignifier.SendChoosenToken)
        {
            
            GameRoom gr = GetGameRoomWithClientID(id);
            if (gr != null)
            {
                int indx = int.Parse(csv[1]);
               if (gr.gameTurn == id)
				{
                    if(gr.gameBoard[indx] == -1)
					{
                        gr.gameBoard[indx] = id;
                        Debug.Log("Player1 id:  " + gr.playerID1 + "Player2 id:  " + gr.playerID2+"   player played id: "+ id);
                        SendMessageToClient(ServerToClientSignifier.sendChoosenTokenByPlayer + "," + GetPlayerAccountByID(gr.gameTurn).name + "," + indx,  gr.playerID1);
                        SendMessageToClient(ServerToClientSignifier.sendChoosenTokenByPlayer + "," + GetPlayerAccountByID(gr.gameTurn).name + "," + indx, gr.playerID2);
						for (int i = 0; i < gr.observersID.Count; i++)
						{
                            if(gr.playerID1==id)
                              SendMessageToClient(ServerToClientSignifier.sendChoosenTokensToObservers + "," + indx + "," + gr.token, gr.observersID[i]);
                            else
                                SendMessageToClient(ServerToClientSignifier.sendChoosenTokensToObservers + "," + indx + "," + gr.token2, gr.observersID[i]);


                        }
                        int newId = gr.CheckGameBoard();
                        if (newId != -1)
                        {
                            if(newId == -2)
							{
                                string txtMsg = "Tied.";

                                SendMessageToClient(ServerToClientSignifier.sendGameStatus+ "," + txtMsg, gr.playerID1);
                                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.playerID2);
                                gr.gameTurn = -1;
                                for (int i = 0; i < gr.observersID.Count; i++)
                                {
                                    SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.observersID[i]);

                                }
                            }
                           else if (newId == gr.playerID1)
                            {
                                
                                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + "You Won!", gr.playerID1);
                                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + "Game Over!", gr.playerID2);
                                gr.gameTurn = -1;
                                for (int i = 0; i < gr.observersID.Count; i++)
                                {
                                    SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + GetPlayerAccountByID(gr.playerID1).name +" Won!", gr.observersID[i]);

                                }
                            }
                          else if (newId == gr.playerID2)
                            {
                                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + "Game Over!", gr.playerID1);
                                SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + "You Won!", gr.playerID2);
                                gr.gameTurn = -1;
                                for (int i = 0; i < gr.observersID.Count; i++)
                                {
                                    SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + GetPlayerAccountByID(gr.playerID2).name + " Won!", gr.observersID[i]);

                                }
                            }
                        }
                        else
                        {
                            gr.gameTurn = (gr.gameTurn == gr.playerID1) ? gr.playerID2 : gr.playerID1;

                            string txtMsg = "It's " + GetPlayerAccountByID(gr.gameTurn).name + "'s turn.";

                            SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.playerID1);
                            SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.playerID2); 
                            for (int i = 0; i < gr.observersID.Count; i++)
                            {
                                  SendMessageToClient(ServerToClientSignifier.sendGameStatus + "," + txtMsg, gr.observersID[i]);
                               
                            }
                        }
                    }
                }
            }
        }
    }

    private void SavePlayerAccounts()
	{
        StreamWriter sw = new StreamWriter(playerAccountsFilePath);
		foreach (PlayerAccount pa in playerAccounts)
		{
            sw.WriteLine(playerAccountRecord + "," + pa.name + "," + pa.password);
		}
        sw.Close();
	}

    private void LoadPlayerAccounts()
	{
        if (File.Exists(playerAccountsFilePath))
        {
            StreamReader sr = new StreamReader(playerAccountsFilePath);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');
                int signifier = int.Parse(csv[0]);
                if (signifier == playerAccountRecord)
                {
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2], -1);
                    playerAccounts.AddLast(pa);
                }

            }
            sr.Close();
        }
	}

    private GameRoom GetGameRoomWithClientID(int id)
	{
		foreach (GameRoom gr in gameRooms)
		{
            if(gr.playerID1 == id || gr.playerID2 == id)
			{
                return gr;
			}
		    for (int i = 0; i < gr.observersID.Count; i++)
		    {
                if(gr.observersID[i]==id)
				{
                    return gr;

                }
            }
		}
        return null;
	}

    public PlayerAccount GetPlayerAccountByID(int id)
    {
        foreach (PlayerAccount pa in playerAccounts)
        {
            if(pa.id == id)
            return pa;
        }
       
        return null;
    }
}

public class PlayerAccount
{
    public string name, password;
    public int id;
    public PlayerAccount (string name, string password,int id)
	{
        this.name = name;
        this.password = password;
        this.id = id;
	}
    
}

public class GameRoom
{
    public int playerID1, playerID2;
    public int token, token2;
    public int gameTurn;
    public List<int> gameBoard = new List<int>();
    public List<int> observersID = new List<int>();
    public GameRoom(int id1, int id2)
	{
        playerID1 = id1;
        playerID2 = id2;
		for (int i = 0; i < 9; i++)
		{
            gameBoard.Add(-1);
		}
	}

    public int CheckGameBoard()
	{
        bool isFull = true;
        int newId = -1;
        if (gameBoard[0] == gameBoard[1] && gameBoard[1] == gameBoard[2] && gameBoard[0] != -1)
        {
            newId = gameBoard[0];

            return newId;
        }
        else if (gameBoard[0] == gameBoard[3] && gameBoard[3] == gameBoard[6] && gameBoard[0] != -1)
        {
            newId = gameBoard[0];
            return newId;
        }
        else if (gameBoard[0] == gameBoard[4] && gameBoard[4] == gameBoard[8] && gameBoard[0] != -1)
        {
            newId = gameBoard[0];
            return newId;
        }
        else if (gameBoard[2] == gameBoard[4] && gameBoard[4] == gameBoard[6] && gameBoard[2] != -1)
        {
            newId = gameBoard[2];
            return newId;
        }
        else if (gameBoard[2] == gameBoard[5] && gameBoard[5] == gameBoard[8] && gameBoard[2] != -1)
        {
            newId = gameBoard[2];
            return newId;
        }
        else if (gameBoard[6] == gameBoard[7] && gameBoard[7] == gameBoard[8] && gameBoard[6] != -1)
        {
            newId = gameBoard[6];
            return newId;
        }
        else if (gameBoard[3] == gameBoard[4] && gameBoard[4] == gameBoard[5] && gameBoard[3] != -1)
        {
            newId = gameBoard[3];
            return newId;
        }
        else if (gameBoard[1] == gameBoard[7] && gameBoard[7] == gameBoard[4] && gameBoard[1] != -1)
        {
            newId = gameBoard[1];
            return newId;
        }
        else
        {
            for (int i = 0; i < 9; i++)
            {
                if (gameBoard[i] == -1)
                {
                    isFull = false;
                    return -1;
                }
            }
            if (isFull)
                return -2;
        }
        return newId;
	}

    
}
public static class ClientToServerSignifier
{
    public const int createAccount = 1;
    public const int Login = 2;
    public const int JoinQueueForGameRoom = 3;
    public const int GameRoomPlay = 4;
    public const int SendTextMessage = 5;
    public const int SendChoosenToken = 6;
    public const int requestToObserveGame = 7;

}


public static class ServerToClientSignifier
{
    public const int LoginComplete = 1;
    public const int LoginFailed = 2;
    public const int AccountCreationComplete = 3;
    public const int AccountCreationFailed = 4;
    public const int GameStart = 5;
    public const int TextChatMeassage = 6;
    public const int TurnInGame = 7;
    public const int sendChoosenTokenByPlayer = 8;
    public const int SendwinLoseTie = 9;
    public const int sendGameStatus = 10;
    public const int ObserveGameAccepted = 11;
    public const int ObserveGameFailed = 12;
    public const int sendChoosenTokensToObservers = 13;
}