using System.Collections;
using System.Text;
using UnityEngine;

public class Client : MonoBehaviour
{
    Network.Client client = null;

    // Start is called before the first frame update
    void Start()
    {
        client = new Network.Client();
        client.Connect("127.0.0.1", 8989);
        StartCoroutine(SentMsg());
    }

    IEnumerator SentMsg()
    {
        int cnt = 0;
        while (true)
        {
            yield return new WaitForSeconds(1f);
            byte[] msg = Encoding.UTF8.GetBytes("str" + cnt);
            cnt++;
            client.SendPacket(msg);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
