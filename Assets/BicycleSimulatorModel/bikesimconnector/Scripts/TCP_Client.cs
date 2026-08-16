using System;
using System.Net.Sockets;
using System.Text;
using System.IO;  //for Debug file writing
using UnityEngine;
using System.Net;
using System.Threading;
 

public class tcp_client : MonoBehaviour
{
    private TcpClient tcpClient;
    private NetworkStream stream;

    bool ERG_mode = false; // we do not want to use erg mode.

    bool wasBrakePressed = false;
    public double brakeDeceleration = 1.5; // m/s^2
    double simulationStepTime = 0.02; // You need to provide this
    bool isBrakePressed = false;

    private bool initDone = false;
    private int initState = 0;

    public int watt_power = 10;
    private int _previous_watt_power=0;

    public double resistanceValue = 10; 
    private double _previous_resistanceValue = 0; 
    private Socket _clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
    private byte[] _recieveBuffer = new byte[8142];
     // IP and port of the Wahoo Kickr bike
    private string serverIp = "192.168.0.2";
    private int serverPort = 36866; // Replace with the actual port used by the bike
    private string logFilePath; // for debugging
    // public int ByteNumberGlobal=31;
    bool isBit8PressedLeft = false;
    bool isBit8PressedRight = false;
    public double currentWahooVelocity;
    public int userPowerInput;
    public double targetOutputVelocity;
    // public double currentPower;
    public int targetOutputPower;

    // Connection status tracking
    private bool isConnected = false;
    private float reconnectTimer = 0f;
    private float reconnectInterval = 5f; // Try to reconnect every 5 seconds
    private int maxReconnectAttempts = 10;
    private int currentReconnectAttempts = 0;

    byte[] dataToSend1 = { 0x01, 0x01, 0x00, 0x00, 0x00, 0x00 }; // Example data, replace with actual
    
    byte[] dataToSend2 = {
        0x01, 0x02, 0x01, 0x00, 0x00, 0x10, 0x00, 0x00, 0x18, 0x0a,
        0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b,
        0x34, 0xfb
    };

        // third message
    byte[] dataToSend3 = {
        0x01, 0x02, 0x02, 0x00, 0x00, 0x10, 0x00, 0x00, 
        0x18, 0x18, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x02, 
        0x03, 0x00, 0x00, 0x10, 0x00, 0x00, 0x18, 0x26, 
        0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80,
        0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x02, 0x04, 0x00, 
        0x00, 0x10, 0x00, 0x00, 0x18, 0x1c, 0x00, 0x00, 
        0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 
        0x34, 0xfb, 0x01, 0x02, 0x05, 0x00, 0x00, 0x10, 
        0xa0, 0x26, 0xee, 0x0d, 0x0a, 0x7d, 0x4a, 0xb3,
        0x97, 0xfa, 0xf1, 0x50, 0x0f, 0x9f, 0xeb, 0x8b
    };


    // fourth message
    byte[] dataToSend4 = {
    0x01, 0x03, 0x06, 0x00, 0x00, 0x10, 0x00, 0x00, 0x2a, 0x29,
    0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b,
    0x34, 0xfb
    };
    // 5. message

    byte[] dataToSend5 = {
        0x01, 0x03, 0x07, 0x00, 0x00, 0x10, 0x00, 0x00, 0x2a, 0x25, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x03, 0x08, 0x00, 0x00, 0x10, 0x00, 0x00, 0x2a, 0x27,
        0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x03, 0x09, 0x00,
        0x00, 0x10, 0x00, 0x00, 0x2a, 0x26, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b,
        0x34, 0xfb, 0x01, 0x05, 0x0a, 0x00, 0x00, 0x11, 0x00, 0x00, 0x2a, 0x63, 0x00, 0x00, 0x10, 0x00,
        0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x01, 0x05, 0x0b, 0x00, 0x00, 0x11, 0xa0,
        0x26, 0xe0, 0x05, 0x0a, 0x7d, 0x4a, 0xb3, 0x97, 0xfa, 0xf1, 0x50, 0x0f, 0x9f, 0xeb, 0x8b, 0x01,
        0x01, 0x03, 0x0c, 0x00, 0x00, 0x10, 0x00, 0x00, 0x2a, 0xcc, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x05, 0x0d, 0x00, 0x00, 0x11, 0x00, 0x00, 0x2a, 0xd9,
        0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x01, 0x05, 0x0e,
        0x00, 0x00, 0x11, 0x00, 0x00, 0x2a, 0xd2, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f,
        0x9b, 0x34, 0xfb, 0x01, 0x01, 0x05, 0x0f, 0x00, 0x00, 0x11, 0x00, 0x00, 0x2a, 0xda, 0x00, 0x00,
        0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01, 0x01, 0x03, 0x10, 0x00, 0x00,
        0x10, 0x00, 0x00, 0x2a, 0x98, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 0x5f, 0x9b, 0x34,
        0xfb, 0x01, 0x05, 0x11, 0x00, 0x00, 0x11, 0xa0, 0x26, 0xe0, 0x3c, 0x0a, 0x7d, 0x4a, 0xb3, 0x97,
        0xfa, 0xf1, 0x50, 0x0f, 0x9f, 0xeb, 0x8b, 0x01
    };

    // disable ERG Mode (some power smoothing mode we do not want)
    byte[] disableERGMessage = {
        0x01, 0x04, 0x1d, 0x00, 0x00, 0x17, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x11, 0x00,
        0x00, 0x64, 0x00, 0x28, 0x33
    };

    // messages at connection time
    // message 6

    byte[] data6 = {
        0x01, 0x04, 0x12, 0x00, 0x00, 0x11, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x00
    };

    byte[] data7 = {
        0x01, 0x04, 0x13, 0x00, 0x00, 0x11, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x01
    };

    byte[] data8 = {
        0x01, 0x04, 0x14, 0x00, 0x00, 0x11, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x00
    };

    byte[] data9 = {
        0x01, 0x04, 0x15, 0x00, 0x00, 0x11, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x07
    };

    byte[] data10 = {
        0x01, 0x04, 0x16, 0x00, 0x00, 0x17, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x11, 0x00,
        0x00, 0xf9, 0x00, 0x28, 0x33
    };


    // send some time later
    byte[] data11 = {
        0x01, 0x04, 0x17, 0x00, 0x00, 0x13, 0x00, 0x00,
        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x05, 0x46, 0x00
    };

    private void SetupServer()
    {
        try
        {
            LogToFile($"[TCP-Client] Attempting to connect to {serverIp}:{serverPort}");
          //  Debug.Log($"[TCP-Client] Attempting to connect to {serverIp}:{serverPort}");
            
            // Check if already connected and close existing connection
            if (_clientSocket != null && _clientSocket.Connected)
            {
                _clientSocket.Close();
            }
            
            // Create new socket
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            // Set socket options for better reliability
            _clientSocket.ReceiveTimeout = 5000; // 5 second timeout
            _clientSocket.SendTimeout = 5000;
            _clientSocket.NoDelay = true;
            
            // Connect to the server
            _clientSocket.Connect(new IPEndPoint(IPAddress.Parse(serverIp), serverPort));
            
            isConnected = true;
            currentReconnectAttempts = 0;
            LogToFile("[TCP-Client] Successfully connected to bike!");
          //  Debug.Log("[TCP-Client] Successfully connected to bike!");
            
            // Start receiving data
            _clientSocket.BeginReceive(_recieveBuffer, 0, _recieveBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
        }
        catch (SocketException ex)
        {
            isConnected = false;
            string errorMsg = $"[TCP-Client] Socket connection failed: {ex.Message} (ErrorCode: {ex.ErrorCode})";
            LogToFile(errorMsg);
          //  Debug.LogError(errorMsg);
            
            // Handle specific error cases
            switch (ex.ErrorCode)
            {
                case 10061: // Connection refused
                  //  Debug.LogError("[TCP-Client] Connection refused - Check if the bike is on and listening on the specified port");
                    break;
                case 10060: // Connection timeout
                  //  Debug.LogError("[TCP-Client] Connection timeout - Check network connectivity and IP address");
                    break;
                case 11001: // Host not found
                  //  Debug.LogError("[TCP-Client] Host not found - Check IP address");
                    break;
            }
        }
        catch (Exception ex)
        {
            isConnected = false;
            string errorMsg = $"[TCP-Client] General connection error: {ex.Message}";
            LogToFile(errorMsg);
          //  Debug.LogError(errorMsg);
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////
    void LogToFile(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                File.AppendAllText(logFilePath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + "\n");
            }
        }
        catch (Exception ex)
        {
          //  Debug.LogWarning($"[TCP-Client] Failed to write to log file: {ex.Message}");
        }
    }
    //////////////////////////////////////////////////////////////////////////////////////////////////////
    
    private void ReceiveCallback(IAsyncResult AR)
    {
        try
        {
            //Check how much bytes are recieved and call EndRecieve to finalize handshake
            int recieved = _clientSocket.EndReceive(AR);
     
            // Debug.Log(recieved);

            if(recieved <= 0)
            {
              //  Debug.LogWarning("[TCP-Client] Received 0 bytes - connection may be closed");
                isConnected = false;
                return;
            }
            
            //Copy the recieved data into new buffer , to avoid null bytes
            byte[] recData = new byte[recieved];
            Buffer.BlockCopy(_recieveBuffer,0,recData,0,recieved);
     
            //Process data here the way you want , all your bytes will be stored in recData

            int l = 24; // 6 19 24 30 32 38

            // Debug.Log("Message length: " + recData.Length);
            if (recData.Length == l) // Check if we are already receiving propoer speed and power data
            {
                initDone = true; // if we do receive data, we are done with the initialization previously, so we cna skip the init this time
            }
            // initalize bike
            if (!initDone){
                
                // Debug.Log("state: "+initState);
                
                if (initState == 1 ){
                    initState++;
                }
                if (initState == 2 ){
                    initState++;
                    SendData(dataToSend2); 
                }
                if (initState == 3){
                    initState++;
                    SendData(dataToSend3);
                }
                if (initState == 4){
                    initState++;
                }
                if (initState == 5){
                    initState++;
                }
                if (initState == 6){
                    initState++;
                }
                if (initState == 7){
                    initState++;
                }
                if (initState == 8){
                    initState++;
                }
                if (initState == 9){
                    initState++;
                }
                if (initState == 10){
                    initState++;
                    SendData(dataToSend4); 
                }
                if (initState == 11){
                    initState++;
                } 
                if (initState == 12){
                    SendData(dataToSend5); 
                    initState++;
                } 
                if (initState == 13){
                    SendData(disableERGMessage);
                  //  Debug.Log("TCP Connection established to Bike!");
                    LogToFile("TCP Connection established to Bike!");
                    initDone = true;
                }

            }
            // receive common data
            else{

            }
     
            // //Copy the recieved data into new buffer , to avoid null bytes
            // byte[] recData = new byte[recieved];
            // Buffer.BlockCopy(_recieveBuffer,0,recData,0,recieved);
     
            //Process data here the way you want , all your bytes will be stored in recData

            // int l = 24; // 6 19 24 30 32 38

            // Debug.Log("Message length: " + l);

            if (recData.Length == l) // look at 24 or 32 length
            {
                StringBuilder sb = new StringBuilder(recData.Length * 2);

                foreach (byte b in recData)
                {
                    sb.AppendFormat("{0:X2} ", b);
                }


                int[] intData = new int[recData.Length];
                for (int i = 0; i < recData.Length; i++) {
                    intData[i] = recData[i] & 0xFF;
                }

                int SpeedByteStart = 19;
                currentWahooVelocity = intData[SpeedByteStart]*3.6 + intData[SpeedByteStart-1]*3.6/255f;            
                // currentPower = 0;
                // currentPower = intData[ByteNumberGlobal];

                int powerByteStart = 22;
                userPowerInput = (intData[powerByteStart] << 8) | intData[powerByteStart - 1];
              //  Debug.Log("[TCP-Client] User Power Input: " + userPowerInput); // here is your power @tian
                LogToFile("[TCP-Client] User Power Input: " + userPowerInput);
                
            }

            int brake_msg_length = 30;
            // for the 30 length message : BRAKING
            if (recData.Length == brake_msg_length) {
                StringBuilder sb = new StringBuilder(recData.Length * 2);

                foreach (byte b in recData)
                {
                    sb.AppendFormat("{0:X2} ", b);
                }

                // Debug.Log("30 Data: " + sb.ToString());

                 int[] intData = new int[recData.Length];
                for (int i = 0; i < recData.Length; i++) {
                    intData[i] = recData[i] & 0xFF;
                }

                int brakeByteLeft = 18;
                int brakeByteRight = 24;

                
                StringBuilder binaryOutputLeft = new StringBuilder();
                binaryOutputLeft.Append(Convert.ToString(intData[brakeByteLeft], 2).PadLeft(8, '0'));
                isBit8PressedLeft = (intData[brakeByteLeft] & (1 << 7)) != 0;

                // Debug.Log("[test] Left Brake: " + isBit8PressedLeft);

                StringBuilder binaryOutputRight = new StringBuilder();
                binaryOutputRight.Append(Convert.ToString(intData[brakeByteRight], 2).PadLeft(8, '0'));
                isBit8PressedRight = (intData[brakeByteRight] & (1 << 7)) != 0;

                // Debug.Log("[test] Right Brake: " + isBit8PressedRight);
                

                //isBrakePressed = isBit8PressedLeft || isBit8PressedRight;
                isBrakePressed = isBit8PressedLeft;
            }

            //Start receiving again
            if (_clientSocket != null && _clientSocket.Connected)
            {
                _clientSocket.BeginReceive(_recieveBuffer,0,_recieveBuffer.Length,SocketFlags.None,new AsyncCallback(ReceiveCallback),null);
            }
        }
        catch (SocketException ex)
        {
            isConnected = false;
            string errorMsg = $"[TCP-Client] Socket receive error: {ex.Message} (ErrorCode: {ex.ErrorCode})";
            LogToFile(errorMsg);
          //  Debug.LogError(errorMsg);
        }
        catch (Exception ex)
        {
            isConnected = false;
            string errorMsg = $"[TCP-Client] Receive callback error: {ex.Message}";
            LogToFile(errorMsg);
          //  Debug.LogError(errorMsg);
        }
    }
 
    private void SendData(byte[] data)
    {
        try
        {
            if (_clientSocket != null && _clientSocket.Connected)
            {
                SocketAsyncEventArgs socketAsyncData = new SocketAsyncEventArgs();
                socketAsyncData.SetBuffer(data,0,data.Length);
                _clientSocket.SendAsync(socketAsyncData);
                
                LogToFile($"[TCP-Client] Sent {data.Length} bytes to bike");
            }
            else
            {
              //  Debug.LogWarning("[TCP-Client] Cannot send data - socket not connected");
            }
        }
        catch (Exception ex)
        {
            string errorMsg = $"[TCP-Client] Send data error: {ex.Message}";
            LogToFile(errorMsg);
          //  Debug.LogError(errorMsg);
            isConnected = false;
        }
    }

    // Public method to check connection status
    public bool IsConnected()
    {
        return isConnected && _clientSocket != null && _clientSocket.Connected;
    }

    // Public method to manually trigger reconnection
    public void ForceReconnect()
    {
      //  Debug.Log("[TCP-Client] Force reconnect requested");
        isConnected = false;
        initDone = false;
        initState = 0;
        currentReconnectAttempts = 0;
    }

    void Start()
    {
        ////////////////////////////////////////////////////////////////////////////////////////////
        // 定义日志文件路径
        string logFileName = "WahooLog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        logFilePath = Path.Combine(Application.persistentDataPath, logFileName);

        // 创建文件并写入头部信息
        File.WriteAllText(logFilePath, "=== Wahoo Log Start ===\n");
      //  Debug.Log("Log file saved to: " + logFilePath);
        
        // Log platform and build information
        LogToFile($"Platform: {Application.platform}");
        LogToFile($"Is Editor: {Application.isEditor}");
        LogToFile($"Unity Version: {Application.unityVersion}");
        LogToFile($"Internet Reachability: {Application.internetReachability}");
        ////////////////////////////////////////////////////////////////////////////////////////////
        
        // Startup sequence for the bike
        SetupServer();
        
        if (isConnected)
        {
            SendData(dataToSend1); // this is the first message we send to he bike
            initState++;
        }
    }

    
    void Update()
    {
        // Handle reconnection logic
        if (!isConnected && currentReconnectAttempts < maxReconnectAttempts)
        {
            reconnectTimer += Time.deltaTime;
            if (reconnectTimer >= reconnectInterval)
            {
                reconnectTimer = 0f;
                currentReconnectAttempts++;
              //  Debug.Log($"[TCP-Client] Reconnection attempt {currentReconnectAttempts}/{maxReconnectAttempts}");
                LogToFile($"[TCP-Client] Reconnection attempt {currentReconnectAttempts}/{maxReconnectAttempts}");
                
                // Reset initialization state
                initDone = false;
                initState = 0;
                
                SetupServer();
                
                if (isConnected)
                {
                    SendData(dataToSend1);
                    initState++;
                }
            }
        }
        
        // setting the power of the bike
        if (initDone && isConnected)
        {
            // this is for ERG Mode which we do not use
            if (ERG_mode){
                if (watt_power != _previous_watt_power)
                {

                    byte[] dataToSendPower = {
                        0x01, 0x04, 0x1b, 0x00, 0x00, 0x13, 
                        0x00, 0x00, 0x2a, 0xd9, 0x00, 0x00, 
                        0x10, 0x00, 0x80, 0x00, 0x00, 0x80, 
                        0x5f, 0x9b, 0x34, 0xfb, 0x05, (byte) watt_power,  0x00,
                    };
                    SendData(dataToSendPower); // this is an issue regarding message frequency
                    // Debug.Log("new Power: "+watt_power);
                    SendData(disableERGMessage);
                    _previous_watt_power = watt_power;
                }
            } 
            // non erg mode, no smoothing etc applied
            // we cannot use power here. we have to use resistance
            else {
                if (resistanceValue != _previous_resistanceValue){


                    if (resistanceValue < 0 || resistanceValue > 100) {
                      //  Debug.LogError("Percentage must be between 0 and 100");
                    }

                    // Scale the percentage to the full range of a 16-bit integer
                    int scaledValue = (int)((resistanceValue / 100.0) * 749); // 749 is the maximum in swift

                    // Extract the LSB and MSB
                    byte resistanceValueLSB = (byte)(scaledValue & 0xFF);
                    byte resistanceValueMSB = (byte)((scaledValue >> 8) & 0xFF);

                    // byte[] percentageAsBytes = new byte[] {lsb, msb};

                    byte[] dataResitanceValue = {
                        0x01, 0x04, 0x31, 0x00, 0x00, 0x17, 0x00, 0x00, // actually there is a counter in byte 3 which we do not use right now
                        0x2a, 0xd9, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
                        0x00, 0x80, 0x5f, 0x9b, 0x34, 0xfb, 0x11, 0x00,
                        0x00, resistanceValueLSB, resistanceValueMSB, 0x28, 0x33    
                    };
                    SendData(dataResitanceValue);
                    _previous_resistanceValue = resistanceValue;
                }
            }

            // braking
            if (isBrakePressed) {
                // If brake is pressed, calculate the amount of velocity to subtract
                // Debug.Log("[TCP-Client] Brake pressed");

                double velocityToSubtract = brakeDeceleration * simulationStepTime; // delceration gain * time
                targetOutputVelocity = currentWahooVelocity - velocityToSubtract;
                if (targetOutputVelocity <= 0) {
                    targetOutputVelocity = 0;
                }
                if (currentWahooVelocity <= 0) {
                    currentWahooVelocity = 0;
                }

            } else{
                targetOutputVelocity = currentWahooVelocity;
                targetOutputPower = userPowerInput;
            }
            // Debug.Log("[TCP-Client] targetOutputVelocity: " + Math.Round(targetOutputVelocity, 1));
            // Debug.Log("[TCP-Client] isBrakePressed: " + isBrakePressed);
            // Debug.Log("[TCP-Client] currentWahooVelocity: " + Math.Round(currentWahooVelocity, 1));
            //LogToFile("[TCP-Client] currentWahooVelocity: " + Math.Round(currentWahooVelocity, 1));
            // Debug.Log("Left Brake: " + isBit8PressedLeft);
            // Debug.Log("Right Brake: " + isBit8PressedRight);

            
        }        
    }

    void OnDestroy()
    {
        try
        {
            isConnected = false;
            if (_clientSocket != null)
            {
                if (_clientSocket.Connected)
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                }
                _clientSocket.Close();
                _clientSocket.Dispose();
            }
            LogToFile("[TCP-Client] Connection closed properly on destroy");
          //  Debug.Log("[TCP-Client] Connection closed properly on destroy");
        }
        catch (Exception ex)
        {
          //  Debug.LogWarning($"[TCP-Client] Error during cleanup: {ex.Message}");
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
          //  Debug.Log("[TCP-Client] Application paused - closing connection");
            LogToFile("[TCP-Client] Application paused - closing connection");
            isConnected = false;
        }
        else
        {
          //  Debug.Log("[TCP-Client] Application resumed - will attempt reconnection");
            LogToFile("[TCP-Client] Application resumed - will attempt reconnection");
            ForceReconnect();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
          //  Debug.Log("[TCP-Client] Application lost focus");
            LogToFile("[TCP-Client] Application lost focus");
        }
        else
        {
          //  Debug.Log("[TCP-Client] Application gained focus");
            LogToFile("[TCP-Client] Application gained focus");
            if (!isConnected)
            {
                ForceReconnect();
            }
        }
    }
}
