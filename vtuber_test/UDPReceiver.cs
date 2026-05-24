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

    [Header("アバター設定")]
    [Tooltip("ユニティちゃんの Head（または Neck）ボーンをアタッチしてください")]
    public Transform headBone;

    [Header("動きの調整")]
    [Tooltip("左右の振り幅")]
    public float yawMultiplier = 60f;
    [Tooltip("上下の振り幅")]
    public float pitchMultiplier = 40f;
    [Tooltip("動きの滑らかさ (0.01 ～ 1.0)")]
    public float smoothFactor = 0.1f;

    private string latestJsonData = "";
    private bool isDataReceived = false;

    // 滑らかさ計算用の内部変数
    private float smoothedX = 0.5f;
    private float smoothedY = 0.5f;

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void Update()
    {
        if (isDataReceived && !string.IsNullOrEmpty(latestJsonData))
        {
            try
            {
                if (latestJsonData.Contains("NOSE"))
                {
                    // JSONから鼻のX, Y座標を抽出
                    float rawX = ExtractValue(latestJsonData, "NOSE", "x");
                    float rawY = ExtractValue(latestJsonData, "NOSE", "y");

                    // Lerpを使った滑らかな補間処理
                    smoothedX = Mathf.Lerp(smoothedX, rawX, smoothFactor);
                    smoothedY = Mathf.Lerp(smoothedY, rawY, smoothFactor);

                    // 中心(0.5)を基準に角度を計算
                    // ※カメラの反転状態やモデルの仕様に合わせて、マイナスを付けて向きを反転させています
                    float yaw = -(smoothedX - 0.5f) * yawMultiplier;
                    float pitch = (smoothedY - 0.5f) * pitchMultiplier;

                    // ボーンの回転を上書き（X軸＝上下、Y軸＝左右、Z軸＝傾き）
                    if (headBone != null)
                    {
                        headBone.localRotation = Quaternion.Euler(pitch, yaw, 0);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("データ反映エラー: " + e.Message);
            }
            isDataReceived = false;
        }
    }

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

    // JSONから数値を抽出する関数
    private float ExtractValue(string json, string part, string axis)
    {
        int partIndex = json.IndexOf("\"" + part + "\"");
        if (partIndex == -1) return 0.5f;

        int axisIndex = json.IndexOf("\"" + axis + "\"", partIndex);
        if (axisIndex == -1) return 0.5f;

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
