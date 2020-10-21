using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using UnityEngine.UIElements;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    //***
    public GameObject Player;
    private GameObject self;
    float speed = 5;
    float P_ID;

    
    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);

        //***
        P_ID = UnityEngine.Random.value;
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("Player Respound");

        //****
        NewPlayer n = new NewPlayer();
        GameObject PlayerInt = Instantiate(Player, new Vector3(-5.0f, 0.0f, 0.0f), Quaternion.identity);
        PlayerInt.AddComponent<PlayerMove>();//get move class
        n.location = PlayerInt.transform.position;
        self = PlayerInt;
        n.ID = P_ID;
        SendToServer(JsonUtility.ToJson(n));
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            break;

            //***
            case Commands.NEW_PLAYER:
                NewPlayer neMsg = JsonUtility.FromJson<NewPlayer>(recMsg);
                Debug.Log("New Player Joining!");
                if (P_ID != neMsg.ID)
                    Instantiate(Player, new Vector3(5.0f,0.0f,0.0f), Quaternion.identity);
                break;
            case Commands.PLAYER_INPUT:
                PlayerInputMsg move = JsonUtility.FromJson<PlayerInputMsg>(recMsg);
                Debug.Log("Player moving!");
                foreach (GameObject cp in GameObject.FindGameObjectsWithTag("Player"))
                {
                    if (cp != self)
                        cp.transform.position = move.move;
                }
                break;

            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   


    //player moving
    public void PlayerMove(GameObject s)
    {
        PlayerInputMsg PM = new PlayerInputMsg();

        /*
        Vector2 movementVector = new Vector2(Input.GetAxis("Horizontal"),
                                             Input.GetAxis("Vertical"));
        movementVector *= speed;
        PM.move = s.transform.position= movementVector;*/
        PM.move = s.transform.position;
        PM.ID = P_ID;


        SendToServer(JsonUtility.ToJson(PM));
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
            return;
        
        if(self)
            //player move
            PlayerMove(self);

        

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
                OnConnect();
            
            else if (cmd == NetworkEvent.Type.Data)
                OnData(stream);
            
            else if (cmd == NetworkEvent.Type.Disconnect)
                OnDisconnect();
            

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}