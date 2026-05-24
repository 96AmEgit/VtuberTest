using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 5005;

    // 動かしたいUnityのオブジェクト（インスペクターからアタッチ）
    public Transform targetObject;

    private string latestJsonData = "";
    private bool isDataReceived = false;

    // スケール調整用（MediaPipeは0~1空間なので、Unity用に適度に大きくする）
    public float multiplier = 10f;

    void Start()
    {
        // メインスレッドが止まらないように、受信は別スレッド（バックグラウンド）で行う
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    // 動かしたいターゲットをユニティちゃんの「Head」にする
public Transform headBone; 
float smoothedX, smoothedY;

void Update() {
    if (isDataReceived) {
        float rawX = ExtractValue(latestJsonData, "NOSE", "x");
        float rawY = ExtractValue(latestJsonData, "NOSE", "y");

        // Lerpで滑らかにする (0.1fを小さくするほどゆっくり、大きくするとキビキビ動く)
        smoothedX = Mathf.Lerp(smoothedX, rawX, 0.1f);
        smoothedY = Mathf.Lerp(smoothedY, rawY, 0.1f);

        // 位置ではなく回転に変換する例（鼻が右に行ったら頭を右に30度向ける、など）
        float yaw = (smoothedX - 0.5f) * 60f;   // 左右
        float pitch = (smoothedY - 0.5f) * 40f; // 上下
        
        headBone.localRotation = Quaternion.Euler(pitch, yaw, 0);
    }
}
    }

    // バックグラウンドで常に動き続ける受信処理
    private void ReceiveData()
    {
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);

                latestJsonData = Encoding.UTF8.GetString(data);
                isDataReceived = true;
            }
            catch (Exception e)
            {
                Debug.LogError("UDP受信エラー: " + e.Message);
            }
        }
    }

    // JSONから特定の数値を抜き出すための簡易関数
    private float ExtractValue(string json, string part, string axis)
    {
        int partIndex = json.IndexOf("\"" + part + "\"");
        int axisIndex = json.IndexOf("\"" + axis + "\"", partIndex);
        int colonIndex = json.IndexOf(":", axisIndex);
        int commaIndex = json.IndexOf(",", colonIndex);
        if (commaIndex == -1 || commaIndex > json.IndexOf("}", colonIndex))
        {
            commaIndex = json.IndexOf("}", colonIndex);
        }
        string valueStr = json.Substring(colonIndex + 1, commaIndex - colonIndex - 1);
        return float.Parse(valueStr.Trim());
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}
