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
                // Establecer la dirección IP y el puerto para el servidor
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                int port = 8090;

                // Crear el TcpListener
                TcpListener server = new TcpListener(localAddr, port);

                // Iniciar el TcpListener
                server.Start();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ComprovarMida(LogMessages);
                    LogMessages.Add($"Servidor escuchando en {localAddr}:{port}");
                });
                Console.WriteLine($"Servidor escuchando en {localAddr}:{port}");

                // Bucle infinito para aceptar clientes
                while (true)
                {
                    Console.WriteLine("Esperando por conexiones...");
                    TcpClient client = server.AcceptTcpClient();
                    clients.Add(client);
                    Console.WriteLine("Conexión aceptada.");
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
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        static void ProcessConnection(object clientObj)
        {
            TcpClient client = clientObj as TcpClient;
            if (client == null) return;

            NetworkStream stream = client.GetStream();

            try
            {
                while (true) // Mantener la conexión abierta
                {
                    if (stream.DataAvailable) // Verifica si hay datos para leer
                    {
                        byte[] bytes = new byte[256];
                        string data = null;

                        int i;
                        // Bucle para recibir todos los datos enviados por el cliente
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            data = Encoding.ASCII.GetString(bytes, 0, i);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ComprovarMida(LogMessages);
                                LogMessages.Add($"Mensaje recibido: {data}");
                            });
                            Console.WriteLine($"Mensaje recibido: {data}");
                            EnviarResposta(stream, data);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error en la conexión: {e.Message}");
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
                        // Enviar tamaño del mensaje
                        clientStream.Write(sizePrefix, 0, sizePrefix.Length);
                        // Enviar mensaje
                        clientStream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error al enviar respuesta: {e.Message}");
                        // Considera remover al cliente de 'clients' si no está más conectado
                    }
                }
            }
            Console.WriteLine("Respuesta enviada a todos los clientes.");
        }

    }
}