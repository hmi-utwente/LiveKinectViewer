using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Linq;

#if !UNITY_EDITOR && UNITY_METRO
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Networking;
#else
using System.Net;
using System.Net.Sockets;
#endif

[Serializable]
public class RegisterObject {
    public string packageType = "register";
    public string socketID;
    public bool isSender;
    public string localIP;
}

[Serializable]
public class AnswerObject {
    public string type;
    public string address;
    public int port;
}

public class IMPRESS_UDPClient : MonoBehaviour {
    public bool debug = false;
    public string socketID;
    public string remoteAddress = "";
    public int remotePort;

    public bool useMatchmakingServer = true;
    public bool isSender;
    public string _serverHostname = "impress.mooo.com";
    public int _serverPort = 6312;

    private string localIP = "";

    private Task _sendTask;
    private bool _sendRunning = false;
    ManualResetEvent send_MRSTE = new ManualResetEvent(false);
    private Task _listenTask;
    private bool _listenRunning = false;
    private Queue<byte[]> _sendQueue = new Queue<byte[]>();
    private Queue<byte[]> _receiveQueue = new Queue<byte[]>();
    Dictionary<UInt32, byte[][]> _dataParts = new Dictionary<UInt32, byte[][]>();

    private int headerLen = 13;
    private int cutoffLength;

    private float registerInterval = 2F;
    private float lastRegister = -1.5F;

    private float connectionTimeout = 5F;
    private float lastReceivedHB = 0F;

    private float HBInterval = 2F;
    private float lastSentHB = -1.5F;

    private float currentTime = 0;

    // Remote Client
    public bool connected { get; private set; } = false;


#if !UNITY_EDITOR && UNITY_METRO
    private DatagramSocket udpClient;
#else
    private UdpClient udpClient;
#endif

    // Use this for initialization
    async void Start() {
        cutoffLength = 60000 - headerLen;

        localIP = GetLocalIPAddress();
        _listenTask = Task.Run(() => DataListener());
        await Task.Delay(1000);
        _sendTask = Task.Run(() => DataSender());
    }

    // Update is called once per frame
    void Update() {
        currentTime = Time.time;
        if (useMatchmakingServer) {
            if (connected && Time.time > lastReceivedHB + connectionTimeout) {
                connected = false;
            }

            if (connected) {
                if (Time.time > lastSentHB + HBInterval) {
                    lastSentHB = Time.time;
                    Punch();
                }
            }

            if (!connected) {
                if (Time.time > lastRegister + registerInterval) {
                    lastRegister = Time.time;
                    Register();
                }
            }
        } else if (!connected) {
            connected = true;
        }
    }

    public void SendData(byte[] dataBufferToSend) {
        if (connected) {
            lock (_sendQueue) {
                _sendQueue.Enqueue(dataBufferToSend);
                send_MRSTE.Set();
                send_MRSTE.Reset();
            }
        }
    }


    UInt32 packageSequenceID = 0;
    public void SendSplitData(byte[] nextPacket) {
        if (connected) {
            if (nextPacket.Length != 0) {
                packageSequenceID++;
                UInt32 partsAm = (UInt32)((nextPacket.Length + cutoffLength - 1) / cutoffLength); // Round Up The Result Of Integer Division
                UInt32 currentPart = 0;

                while (nextPacket.Length > 0) {
                    byte[] cutData = new byte[0];
                    if (nextPacket.Length > cutoffLength) {
                        cutData = new byte[cutoffLength];
                        Array.Copy(nextPacket, cutData, cutoffLength);

                        int remainingLen = nextPacket.Length - cutoffLength;
                        byte[] remainder = new byte[remainingLen];
                        Array.Copy(nextPacket, cutoffLength, remainder, 0, remainingLen);
                        nextPacket = remainder;
                    } else {
                        cutData = nextPacket;
                        nextPacket = new byte[0];
                    }

                    byte[] sendBytes;
                    using (MemoryStream fs = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(fs)) {
                        writer.Write((byte)20);
                        writer.Write(packageSequenceID);
                        writer.Write(partsAm);
                        writer.Write(currentPart++);
                        writer.Write(cutData);
                        sendBytes = fs.ToArray();
                    }
                    SendData(sendBytes);
                }
            }
        }
    }

    public byte[] GetNewData() {
        byte[] returnBytes = null;
        lock (_receiveQueue) {
            if (_receiveQueue.Count > 0) {
                returnBytes = _receiveQueue.Dequeue();
            }
        }
        return returnBytes;
    }

    public void Close() {
        _sendRunning = false;
        _listenRunning = false;
#if UNITY_EDITOR
        udpClient.Close();
#endif
    }

    private void OnApplicationQuit() {
        Close();
    }

    public static string GetLocalIPAddress() {
        string localIP = "";
#if !UNITY_EDITOR && UNITY_METRO
        foreach (HostName localHostName in NetworkInformation.GetHostNames()) {
            if (localHostName.IPInformation != null) {
                if (localHostName.Type == HostNameType.Ipv4) {
                    localIP = localHostName.ToString();
                    break;
                }
            }
        }
#else
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }
#endif
        return localIP;
    }

    //------------- LISTEN STUFF -----------------

    private async Task DataListener() {

#if !UNITY_EDITOR && UNITY_METRO
        udpClient = new DatagramSocket();
        udpClient.MessageReceived += Listener_MessageReceived;
        try {
            await udpClient.BindEndpointAsync(null, "0");
            if(debug) Debug.Log("Listening on port: " + udpClient.Information.LocalPort);
        } catch (Exception e) {
            if(debug) Debug.Log("DATA LISTENER START EXCEPTION: " + e.ToString());
            if(debug) Debug.Log(SocketError.GetStatus(e.HResult).ToString());
            return;
        }

#else
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
        udpClient = new UdpClient(anyIP);
        int listenPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
        if (debug) Debug.Log("Client listening on " + listenPort);

        _listenRunning = true;
        while (_listenRunning) {
            //UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();
            //byte[] receivedPackage = receivedResults.Buffer;
            try {
                byte[] receivedPackage = udpClient.Receive(ref anyIP);
                HandleReceivedData(receivedPackage);
            } catch (Exception e) {
                //Debug.Log(e);
            }
        }
        udpClient.Close();
        if (debug) Debug.Log("DataListener Stopped");
#endif
    }

#if !UNITY_EDITOR && UNITY_METRO
    private async void Listener_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args) {
        try {
            Stream streamIn = args.GetDataStream().AsStreamForRead();
            MemoryStream ms = ToMemoryStream(streamIn);
            byte[] receivedPackage = ms.ToArray();
            HandleReceivedData(receivedPackage);
        } catch (Exception e) {
            if(debug) Debug.Log("DATA LISTENER EXCEPTION: " + e.ToString());
            if(debug) Debug.Log(SocketError.GetStatus(e.HResult).ToString());
            return;
        }
    }
    static MemoryStream ToMemoryStream(Stream input) {
        try {                                         // Read and write in
            byte[] block = new byte[0x1000];       // blocks of 4K.
            MemoryStream ms = new MemoryStream();
            while (true) {
                int bytesRead = input.Read(block, 0, block.Length);
                if (bytesRead == 0) return ms;
                ms.Write(block, 0, bytesRead);
            }
        } finally { }
    }
#endif



    private void HandleReceivedData(byte[] inData) {
        if (debug) {
            string dString = Encoding.ASCII.GetString(inData);
            Debug.Log(dString.Length + "  " + (byte)inData[0] + "  " + dString);
        }

        byte magicByte = inData[0];
        if (magicByte == 100) {
            string json = Encoding.ASCII.GetString(inData, 1, inData.Length - 1);
            try {
                AnswerObject obj = JsonUtility.FromJson<AnswerObject>(json);
                if (obj.type == "answer") {
                    remoteAddress = obj.address;
                    remotePort = obj.port;
                    Punch();
                    Punch();
                }
                if (obj.type == "punch") {  // received punch packet from other client -> connection works
                    lastReceivedHB = currentTime;
                    connected = true;
                }
                return; // return if package was a json package
            } catch (Exception e) { Debug.Log(e.ToString()); }
        } else if (magicByte == 20) {
            lastReceivedHB = currentTime;
            byte packageType;
            UInt32 packageSequenceID;
            UInt32 partsAm;
            UInt32 currentPart;

            using (MemoryStream str = new MemoryStream(inData)) {
                using (BinaryReader reader = new BinaryReader(str)) {
                    packageType = reader.ReadByte();
                    packageSequenceID = reader.ReadUInt32();
                    partsAm = reader.ReadUInt32();
                    currentPart = reader.ReadUInt32();
                }
            }
            byte[] data = new byte[inData.Length - headerLen];
            Array.Copy(inData, headerLen, data, 0, inData.Length - headerLen);

            if (debug) Debug.Log("packageSequenceID:  " + packageSequenceID + ", partsAm: " + partsAm + ", currentPart: " + currentPart + ", size: " + inData.Length);
            if (partsAm == 1) {
                lock (_receiveQueue)
                    _receiveQueue.Enqueue(data);
            } else if (partsAm > 1) {
                if (!_dataParts.ContainsKey(packageSequenceID)) {
                    byte[][] parts = new byte[partsAm][];
                    parts[currentPart] = data;
                    _dataParts.Add(packageSequenceID, parts);
                } else {
                    byte[][] parts = _dataParts[packageSequenceID];
                    parts[currentPart] = data;

                    bool dataComplete = true;
                    int concatDataSize = 0;
                    for (int i = 0; i < partsAm; i++) {
                        if (parts[i] == null) {
                            dataComplete = false;
                            break;
                        }
                        concatDataSize += parts[i].Length;
                    }
                    if (dataComplete) {
                        _dataParts.Remove(packageSequenceID);
                        byte[] concatData = new byte[concatDataSize];
                        int idx = 0;
                        for (int i = 0; i < partsAm; i++) {
                            Array.Copy(parts[i], 0, concatData, idx, parts[i].Length);
                            idx += parts[i].Length;
                        }

                        lock (_receiveQueue)
                            _receiveQueue.Enqueue(concatData);
                    }
                }
            }
        } else {
            lastReceivedHB = currentTime;
            lock (_receiveQueue)
                _receiveQueue.Enqueue(inData);
        }
    }


    //------------- SEND STUFF -----------------

    private async void _sendData(byte[] data, string hostName, int port) {
#if !UNITY_EDITOR && UNITY_METRO
        using (var stream = await udpClient.GetOutputStreamAsync(new HostName(hostName), port.ToString())) {
            using (var writer = new DataWriter(stream)) {
                writer.WriteBytes(data);
                await writer.StoreAsync();
            }
        }
#else
        await udpClient.SendAsync(data, data.Length, hostName, port);
#endif
    }

    private void Register() {
        RegisterObject regObj = new RegisterObject();
        regObj.socketID = socketID;
        regObj.isSender = isSender;
        regObj.localIP = localIP;
        string json = JsonUtility.ToJson(regObj);
        byte[] sendBytes = Encoding.ASCII.GetBytes((char)100 + json);
        _sendData(sendBytes, _serverHostname, _serverPort);
    }

    private void Punch() {
        byte[] sendBytes = Encoding.ASCII.GetBytes((char)100 + "{\"type\":\"punch\"}");
        _sendData(sendBytes, remoteAddress, remotePort);
    }

    private async void DataSender() {
        _sendRunning = true;

        while (_sendRunning) {
            send_MRSTE.WaitOne();
            if (debug) Debug.Log("DataSender Unlocked");
            int queueCount = 1;
            while (queueCount > 0) {
                byte[] nextPacket = new byte[0];
                lock (_sendQueue) {
                    queueCount = _sendQueue.Count;
                    if (queueCount > 0) {
                        nextPacket = _sendQueue.Dequeue();
                    }
                }
                if (nextPacket.Length != 0) {
                    _sendData(nextPacket, remoteAddress, remotePort);
                    if (debug) Debug.Log("DataSender Sent Data");
                }
            }

        }
        if (debug) Debug.Log("DataSender Stopped");
    }
}
