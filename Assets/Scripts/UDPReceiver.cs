using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UDPReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    public int listenPort = 9050;

    [Header("Received Control Values")]
    public float throttle = 0f;
    public float yaw = 0f;
    public float pitch = 0f;
    public float roll = 0f;

    [Header("Status")]
    public bool isReceiving = false;
    public string lastCommand = "";
    public string localIPAddress = "";
    public string lastSenderIP = "None";
    [System.NonSerialized] public float lastPacketTime = -1f;

    // Thread-safe flag for packet received (set in thread, read in main thread)
    private volatile bool packetReceivedFlag = false;
    private string pendingSenderIP = "";

    [Header("UI Display")]
    [Tooltip("Reference to DroneController to check if UDP input is active.")]
    public DroneController droneController;

    // Threading
    private Thread receiveThread;
    private UdpClient udpClient;
    private bool isRunning = false;

    // Thread-safe lock for value access
    private readonly object valueLock = new object();

    // Command queue for main thread execution (Unity API must be called from main thread)
    private readonly Queue<Action> mainThreadCommands = new Queue<Action>();
    private readonly object commandLock = new object();

    // Events for commands
    public event Action OnPauseCommand;
    public event Action OnResetCommand;
    public event Action<string> OnModeCommand;

    /// <summary>
    /// Struct for atomic snapshot of all input values.
    /// </summary>
    public struct InputSnapshot
    {
        public float throttle;
        public float yaw;
        public float pitch;
        public float roll;
    }

    /// <summary>
    /// Get a thread-safe snapshot of all input values atomically.
    /// </summary>
    public InputSnapshot GetInputSnapshot()
    {
        lock (valueLock)
        {
            return new InputSnapshot
            {
                throttle = Mathf.Clamp(this.throttle, -1f, 1f),
                yaw = Mathf.Clamp(this.yaw, -1f, 1f),
                pitch = Mathf.Clamp(this.pitch, -1f, 1f),
                roll = Mathf.Clamp(this.roll, -1f, 1f)
            };
        }
    }

    void Start()
    {
        localIPAddress = GetLocalIPAddress();
        Debug.Log($"=== UDP RECEIVER ===");
        Debug.Log($"Your PC IP Address: {localIPAddress}");
        Debug.Log($"Listening on port: {listenPort}");
        Debug.Log($"Enter this IP in your mobile controller app!");
        Debug.Log($"====================");
        InitializeUDP();
    }

    void Update()
    {
        // Process queued commands on main thread (Unity API requirement)
        ProcessMainThreadCommands();

        // Update packet time on main thread (Time.time can only be called from main thread)
        if (packetReceivedFlag)
        {
            packetReceivedFlag = false;
            lastPacketTime = Time.time;
            lastSenderIP = pendingSenderIP;
        }
    }

    private void ProcessMainThreadCommands()
    {
        lock (commandLock)
        {
            while (mainThreadCommands.Count > 0)
            {
                Action command = mainThreadCommands.Dequeue();
                try
                {
                    command?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UDP] Error executing command on main thread: {e.Message}");
                }
            }
        }
    }

    private void QueueMainThreadCommand(Action command)
    {
        lock (commandLock)
        {
            mainThreadCommands.Enqueue(command);
        }
    }

    string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // Return the first IPv4 address that's not loopback
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
            return "Unable to find IP";
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting IP: {e.Message}");
            return "Error";
        }
    }

    void InitializeUDP()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;

            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log($"UDP Receiver started on port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP receiver: {e.Message}");
        }
    }

    void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);

                ProcessMessage(message);
                isReceiving = true;

                // Store sender IP for main thread to update (avoid Unity API in thread)
                pendingSenderIP = remoteEndPoint.Address.ToString();
                packetReceivedFlag = true;
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogWarning($"UDP Receive Error: {e.Message}");
            }
        }
    }

    void ProcessMessage(string message)
    {
        // Check for commands - queue them for main thread execution
        if (message == "PAUSE")
        {
            lastCommand = "PAUSE";
            QueueMainThreadCommand(() => OnPauseCommand?.Invoke());
            return;
        }

        if (message == "RESET")
        {
            lastCommand = "RESET";
            Debug.Log("[UDP] RESET command received, queuing for main thread execution");
            QueueMainThreadCommand(() => OnResetCommand?.Invoke());
            return;
        }

        // Check for MODE command (format: "MODE ANGLE", "MODE ACRO", "MODE HORIZON")
        if (message.StartsWith("MODE "))
        {
            string modeName = message.Substring(5).Trim().ToUpper();
            lastCommand = $"MODE {modeName}";
            QueueMainThreadCommand(() => OnModeCommand?.Invoke(modeName));
            return;
        }

        // Parse control data: throttle,yaw,pitch,roll
        string[] values = message.Split(',');

        if (values.Length == 4)
        {
            try
            {
                float parsedThrottle = float.Parse(values[0]);
                float parsedYaw = float.Parse(values[1]);
                float parsedPitch = float.Parse(values[2]);
                float parsedRoll = float.Parse(values[3]);

                // Thread-safe update of all values atomically
                lock (valueLock)
                {
                    throttle = parsedThrottle;
                    yaw = parsedYaw;
                    pitch = parsedPitch;
                    roll = parsedRoll;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse control data: {e.Message}");
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;

        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Abort();

        if (udpClient != null)
            udpClient.Close();

        Debug.Log("UDP Receiver stopped");
    }

    void OnApplicationQuit()
    {
        OnDestroy();
    }

    // Display connection info and received values only when UDP input is active
    void OnGUI()
    {
        // Only show UDP info if input source is UDP
        if (droneController != null && droneController.CurrentInputSource != DroneController.InputSource.UDP)
            return;

        // If droneController not assigned, try to find it (fallback)
        if (droneController == null)
        {
            droneController = FindObjectOfType<DroneController>();
            if (droneController != null && droneController.CurrentInputSource != DroneController.InputSource.UDP)
                return;
        }

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;

        // Always show IP address prominently (important for connecting mobile app)
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(10, 10, 500, 30), $"PC IP: {localIPAddress}  Port: {listenPort}", style);

        style.normal.textColor = isReceiving ? Color.green : Color.yellow;
        GUI.Label(new Rect(10, 40, 500, 30), $"Status: {(isReceiving ? $"CONNECTED to {lastSenderIP}" : "WAITING FOR CONTROLLER...")}", style);

        if (Application.isEditor || isReceiving)
        {
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 70, 300, 30), $"Throttle: {throttle:F2}", style);
            GUI.Label(new Rect(10, 100, 300, 30), $"Yaw: {yaw:F2}", style);
            GUI.Label(new Rect(10, 130, 300, 30), $"Pitch: {pitch:F2}", style);
            GUI.Label(new Rect(10, 160, 300, 30), $"Roll: {roll:F2}", style);

            if (!string.IsNullOrEmpty(lastCommand))
                GUI.Label(new Rect(10, 190, 300, 30), $"Last Command: {lastCommand}", style);
        }
    }
}