using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;

namespace Server
{
    public partial class MainWindow : Window
    {
        public static ObservableCollection<string> LogMessages { get; set; } = new ObservableCollection<string>();
        private static ConcurrentBag<TcpClient> clients = new ConcurrentBag<TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
            btnStartServer.Click += StartServer;
            lvLog.ItemsSource = LogMessages;
        }

        private void StartServer(object sender, RoutedEventArgs e)
        {
            Thread serverThread = new Thread(StartTcpServer);
            serverThread.Start();
        }

        static void StartTcpServer()
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                int port = 8090;
                TcpListener server = new TcpListener(localAddr, port);
                server.Start();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ComprovarMida(LogMessages);
                    LogMessages.Add($"Servidor escuchando en {localAddr}:{port}");
                });

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    clients.Add(client);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ComprovarMida(LogMessages);
                        LogMessages.Add("Conexión aceptada.");
                    });
                    Thread clientThread = new Thread(ProcessConnection);
                    clientThread.Start(client);
                }
            }
            catch (Exception e)
            {
            }
        }

        static void ProcessConnection(object clientObj)
        {
            TcpClient client = clientObj as TcpClient;
            if (client == null) return;

            NetworkStream stream = client.GetStream();

            try
            {
                while (true)
                {
                    if (stream.DataAvailable)
                    {
                        byte[] bytes = new byte[256];
                        string data = null;

                        int i;
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            data = Encoding.ASCII.GetString(bytes, 0, i);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ComprovarMida(LogMessages);
                                LogMessages.Add($"Mensaje recibido: {data}");
                            });
                            EnviarResposta(stream, data);
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }
            finally
            {
                client.Close();
            }
        }

        private static void ComprovarMida(ObservableCollection<string> logMessages)
        {
            while (logMessages.Count > 10)
            {
                logMessages.RemoveAt(0);
            }
        }

        private static void EnviarResposta(NetworkStream stream, string data)
        {
            byte[] responseBytes = Encoding.ASCII.GetBytes(data + "\n");
            byte[] sizePrefix = BitConverter.GetBytes(responseBytes.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizePrefix);

            foreach (var client in clients)
            {
                if (client.Connected)
                {
                    try
                    {
                        NetworkStream clientStream = client.GetStream();
                        clientStream.Write(sizePrefix, 0, sizePrefix.Length);
                        clientStream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
        }
    }
}
