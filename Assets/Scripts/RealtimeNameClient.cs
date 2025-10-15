using System;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
#define USE_NATIVE_WEBSOCKET
#endif

#if USE_NATIVE_WEBSOCKET
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Listens for real-time name updates from the Node.js server and exposes an event when they arrive.
/// Requires the NativeWebSocket package: https://github.com/endel/NativeWebSocket
/// </summary>
public class RealtimeNameClient : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "ws://localhost:3000/ws";

    [SerializeField]
    private UnityEvent<string> onNameChanged;

    [SerializeField]
    private Text targetLabel;

    private WebSocket webSocket;

    private async void Awake()
    {
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (webSocket != null)
        {
            await webSocket.Close();
        }

        webSocket = new WebSocket(serverUrl);

        webSocket.OnOpen += () =>
        {
            Debug.Log("[RealtimeNameClient] Connected to name server.");
        };

        webSocket.OnError += error =>
        {
            Debug.LogError($"[RealtimeNameClient] WebSocket error: {error}");
        };

        webSocket.OnClose += code =>
        {
            Debug.LogWarning($"[RealtimeNameClient] Connection closed: {code}");
        };

        webSocket.OnMessage += HandleMessage;

        try
        {
            await webSocket.Connect();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RealtimeNameClient] Failed to connect: {exception.Message}");
        }
    }

    private void HandleMessage(byte[] payload)
    {
        var message = Encoding.UTF8.GetString(payload);

        try
        {
            var data = JsonUtility.FromJson<NamePayload>(message);

            if (data != null && data.type == "name:update")
            {
                DispatchName(data.name);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RealtimeNameClient] Failed to parse message: {exception.Message}");
        }
    }

    private void DispatchName(string value)
    {
        onNameChanged?.Invoke(value);

        if (targetLabel != null)
        {
            targetLabel.text = value;
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        webSocket?.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        await CloseAsync();
    }

    private async void OnDestroy()
    {
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (webSocket != null)
        {
            try
            {
                await webSocket.Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RealtimeNameClient] Error while closing: {exception.Message}");
            }
        }
    }

    [Serializable]
    private class NamePayload
    {
        public string type;
        public string name;
    }
}
#else
/// <summary>
/// Placeholder implementation that keeps the component valid when NativeWebSocket is missing.
/// </summary>
public class RealtimeNameClient : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "ws://localhost:3000/ws";

    private void Awake()
    {
        Debug.LogWarning("[RealtimeNameClient] NativeWebSocket package is not installed; realtime updates are disabled.");
    }
}
#endif
