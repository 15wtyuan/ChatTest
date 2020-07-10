using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class DispatcherManager : MonoBehaviour
{
    Network.Client client = null;
    // Start is called before the first frame update
    void Start()
    {
        client = new Network.Client();
        client.Connect("127.0.0.1", 8989);
    }

    // Update is called once per frame
    void Update()
    {
        while (true)
        {
            Network.Message message = client.GetMessage();
            if (message == null)
                break;
            if (message.type == Network.MessageType.DataReceived)
            {
                
            }
        }
    }
}

public class MsgType
{
    public const int NameMsg = 1;
    public const int Msg = 2;
}

public abstract class MsgBaseCtrl
{
    public abstract int MsgID { get; }
    public abstract void OnReceiveData(Network.Message message);
}

public class NameMsgCtrl : MsgBaseCtrl
{
    public override int MsgID { get => MsgType.NameMsg; }
    public override void OnReceiveData(Network.Message message)
    {

    }
}

public class MsgCtrl : MsgBaseCtrl
{
    public override int MsgID { get => MsgType.Msg; }

    public override void OnReceiveData(Network.Message message)
    {

    }
}
