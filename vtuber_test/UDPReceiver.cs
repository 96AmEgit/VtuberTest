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

    void Update()
    {
        // データの受信があったら、メインスレッド（Update内）でオブジェクトに反映
        if (isDataReceived && !string.IsNullOrEmpty(latestJsonData))
        {
            try
            {
                // 簡易的な文字列解析（JsonUtilityの制限を避けるため、今回はNOSEのXYZを文字検索で強引に抽出）
                // 本格的にはNewtonsoft Jsonなどの外部ライブラリを使うと綺麗に直せます
                if (latestJsonData.Contains("NOSE"))
                {
                    float x = ExtractValue(latestJsonData, "NOSE", "x");
                    float y = ExtractValue(latestJsonData, "NOSE", "y");
                    float z = ExtractValue(latestJsonData, "NOSE", "z");

                    // Unityの座標系に変換（MediaPipeの0~1の中心を0にするため0.5を引く）
                    Vector3 newPosition = new Vector3((x - 0.5f) * multiplier, (y - 0.5f) * multiplier, z * multiplier);
                    
                    // オブジェクトを動かす
                    targetObject.position = newPosition;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("データ反映エラー: " + e.Message);
            }
            isDataReceived = false;
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
