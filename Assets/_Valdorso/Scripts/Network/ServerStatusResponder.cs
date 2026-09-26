using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using Valdorso.Server;

namespace Valdorso.Network
{
    /// <summary>Quello che il server racconta a chi bussa: niente dati personali.</summary>
    [Serializable]
    public class ServerStatusInfo
    {
        public string version;
        public int players;
        public int maxPlayers;
        public bool acceptingAccounts;
        public string message; // annunci dell'Amministratore (v0.1.6)
    }

    /// <summary>
    /// La "porticina" dello stato del server: finché il server è acceso risponde, sulla porta statusPort (UDP),
    /// a chi chiede versione e numero di giocatori. Va sul NetworkManager.
    /// La richiesta deve essere lunga quanto la risposta, così il server non moltiplica il traffico di nessuno.
    /// </summary>
    public class ServerStatusResponder : MonoBehaviour
    {
        public const string RequestHeader = "VALDORSO-STATO?";
        public const int RequestSize = 256;

        UdpClient socket;
        Thread thread;
        volatile bool running;
        volatile int players;
        volatile int maxPlayers;
        volatile bool acceptingAccounts;
        string version;

        void Awake()
        {
            version = Application.version;
        }

        void Update()
        {
            bool serverOn = NetworkServer.active;
            if (serverOn && !running) StartListening();
            else if (!serverOn && running) StopListening();

            if (!running) return;
            int count = 0;
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                if (conn.isAuthenticated) count++;
            players = count;
            maxPlayers = NetworkManager.singleton != null ? NetworkManager.singleton.maxConnections : 0;
            acceptingAccounts = ServerSettings.Current.allowNewAccounts;
        }

        void OnDestroy() => StopListening();
        void OnApplicationQuit() => StopListening();

        void StartListening()
        {
            running = true;
            int port = ServerConfig.Current.statusPort;
            try
            {
                socket = new UdpClient(AddressFamily.InterNetworkV6);
                socket.Client.DualMode = true; // accetta sia IPv4 sia IPv6
                socket.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Valdorso] Stato del server non disponibile sulla porta {port}: {e.Message}");
                socket?.Close();
                socket = null;
                return;
            }

            thread = new Thread(Listen) { IsBackground = true, Name = "Valdorso stato del server" };
            thread.Start();
            Debug.Log($"[Valdorso] Stato del server in ascolto sulla porta {port} (UDP).");
        }

        void StopListening()
        {
            running = false;
            try { socket?.Close(); } catch { }
            socket = null;
            thread = null;
        }

        void Listen()
        {
            UdpClient listener = socket;
            while (running && listener != null)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.IPv6Any, 0);
                    byte[] data = listener.Receive(ref remote);
                    if (data.Length < RequestSize) continue;
                    if (Encoding.ASCII.GetString(data, 0, RequestHeader.Length) != RequestHeader) continue;

                    var info = new ServerStatusInfo
                    {
                        version = version,
                        players = players,
                        maxPlayers = maxPlayers,
                        acceptingAccounts = acceptingAccounts,
                        message = string.Empty
                    };
                    byte[] reply = Encoding.UTF8.GetBytes(JsonUtility.ToJson(info));
                    listener.Send(reply, reply.Length, remote);
                }
                catch (ObjectDisposedException)
                {
                    break; // il server si è spento
                }
                catch (SocketException)
                {
                    if (!running) break; // altrimenti è un errore passeggero: si continua ad ascoltare
                }
            }
        }
    }

    /// <summary>Il menu "bussa" alla porticina del server e aspetta la risposta per poco tempo.</summary>
    public static class ServerStatusProbe
    {
        /// <summary>Restituisce lo stato del server, oppure null se non risponde.</summary>
        public static async Task<ServerStatusInfo> QueryAsync(string address, int port, int timeoutMs = 2000)
        {
            try
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(address);
                IPAddress target = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork);
                if (target == null && addresses.Length > 0) target = addresses[0];
                if (target == null) return null;

                using (var udp = new UdpClient(target.AddressFamily))
                {
                    byte[] request = new byte[ServerStatusResponder.RequestSize];
                    Encoding.ASCII.GetBytes(ServerStatusResponder.RequestHeader).CopyTo(request, 0);
                    await udp.SendAsync(request, request.Length, new IPEndPoint(target, port));

                    Task<UdpReceiveResult> receive = udp.ReceiveAsync();
                    receive.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    Task finished = await Task.WhenAny(receive, Task.Delay(timeoutMs));
                    if (finished != receive || receive.IsFaulted) return null;

                    UdpReceiveResult result = receive.Result;
                    return JsonUtility.FromJson<ServerStatusInfo>(Encoding.UTF8.GetString(result.Buffer));
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
