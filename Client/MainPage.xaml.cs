using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0xc0a

namespace Client
{
    /// <summary>
    /// Página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Mantener una referencia al socket a nivel de clase
        private StreamSocket socket = null;
        private List<String> llTipusCon;
        private List<String> llUsuaris;
        public MainPage()
        {
            this.InitializeComponent();

            InitAll();

        }

        private void InitAll()
        {
            btnSend.Click += BtnSend_Click;
            llTipusCon = new List<string> { "DES", "INVENTAT" };
            cmbTipusConnexio.ItemsSource = llTipusCon;
            cmbTipusConnexio.SelectedIndex = 0;
            llUsuaris = new List<string> { "USUARI 1", "USUARI 2", "USUARI 3" };
            cmbUsers.ItemsSource = llUsuaris;
            cmbUsers.SelectedIndex = 0;
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Verificar si el socket ya existe y está conectado
                if (socket == null || !IsSocketConnected(socket))
                {
                    // Crear el socket si no existe o no está conectado
                    socket = new StreamSocket();

                    // Conectar al servidor especificando la dirección IP y el puerto
                    await socket.ConnectAsync(new Windows.Networking.HostName(txbIp.Text), txbPort.Text);

                    // Inicia la escucha de mensajes después de establecer la conexión
                    var listenTask = ListenForMessagesAsync(socket, new CancellationToken());
                }

                // Escribir un mensaje al servidor utilizando la conexión existente
                string message = txbInput.Text;
                DataWriter writer = new DataWriter(socket.OutputStream);

                message = ConfigurarText(message);

                writer.WriteString(message);
                await writer.StoreAsync();
                writer.DetachStream();
                writer.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar con el servidor: {ex.Message}");
            }
        }

        private String ConfigurarText(String message)
        {
            String user = "";
            if (cmbUsers.SelectedIndex > -1)
            {
                user = cmbUsers.SelectedItem.ToString() + ": ";
                message = user + message;
            }
            else
            {
                message = "Anonymous: " + message;
            }

            if (cmbTipusConnexio.SelectedIndex > -1) {
                message = EncriptarMissatge(message);
            }
            return message;
        }

        private String EncriptarMissatge(String missatge)
        {

            if ("DES".Equals(cmbTipusConnexio.SelectedItem.ToString()))
            {

                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                {
                    // Aquí deberías establecer tu clave y IV. Esto es solo un ejemplo.
                    des.Key = Encoding.ASCII.GetBytes("12345678"); // La clave DEBE ser de 8 bytes
                    des.IV = Encoding.ASCII.GetBytes("12345678"); // El IV DEBE ser de 8 bytes

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        CryptoStream cryptoStream = new CryptoStream(memoryStream, des.CreateEncryptor(), CryptoStreamMode.Write);

                        byte[] inputBytes = Encoding.UTF8.GetBytes(missatge);
                        cryptoStream.Write(inputBytes, 0, inputBytes.Length);
                        cryptoStream.FlushFinalBlock();

                        missatge = Convert.ToBase64String(memoryStream.ToArray());
                    }
                }

            }
            else if ("INVENTAT".Equals(cmbTipusConnexio.SelectedItem.ToString()))
            {
                missatge = EncriptacioInventada(missatge);
            }

            return missatge;

        }

        private String EncriptacioInventada(String msg)
        {
            String sortida = "";
            foreach (char c in msg)
            {
                sortida += (char)((int)c+3);
            }
            return sortida;
        }

        private String DesencriptacioInventada(String msg)
        {
            String sortida = "";
            foreach (char c in msg)
            {
                sortida += (char)((int)c - 3);
            }
            return sortida;
        }

        private String DesencriptarMissatge(String missatge)
        {
            if (cmbTipusConnexio.SelectedIndex < 0)
            {
                return missatge;
            }
            if ("DES".Equals(cmbTipusConnexio.SelectedItem.ToString()))
            {

                using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                {
                    // Establecer la misma clave y IV usados para encriptar.
                    des.Key = Encoding.ASCII.GetBytes("12345678"); // La clave DEBE ser de 8 bytes
                    des.IV = Encoding.ASCII.GetBytes("12345678"); // El IV DEBE ser de 8 bytes

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        CryptoStream cryptoStream = new CryptoStream(memoryStream,
                            des.CreateDecryptor(), CryptoStreamMode.Write);

                        // Convertir el mensaje encriptado de Base64 a bytes
                        byte[] inputBytes = Convert.FromBase64String(missatge);
                        cryptoStream.Write(inputBytes, 0, inputBytes.Length);
                        cryptoStream.FlushFinalBlock();

                        // Convertir los bytes desencriptados de vuelta a string
                        byte[] decryptedBytes = memoryStream.ToArray();
                        return Encoding.UTF8.GetString(decryptedBytes, 0, decryptedBytes.Length);
                    }
                }

            }
            else if ("INVENTAT".Equals(cmbTipusConnexio.SelectedItem.ToString()))
            {
                missatge = DesencriptacioInventada(missatge);
            }

            return missatge;

        }

        // Método para comprobar si el socket está conectado
        private bool IsSocketConnected(StreamSocket socket)
        {
            // Esta es una forma simplificada de comprobar si un socket está conectado
            // Puedes necesitar una lógica más robusta dependiendo de tu aplicación
            return socket != null && (socket.InputStream != null) && (socket.OutputStream != null);
        }

        private async Task ListenForMessagesAsync(StreamSocket socket, CancellationToken cancellationToken)
        {
            DataReader reader = new DataReader(socket.InputStream)
            {
                InputStreamOptions = InputStreamOptions.Partial
            };

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Asegurarse de tener suficiente para leer el tamaño del mensaje
                    await reader.LoadAsync(sizeof(uint));
                    if (reader.UnconsumedBufferLength < sizeof(uint))
                    {
                        continue; // Si no hay suficiente para leer el tamaño, algo fue mal.
                    }

                    // Lee el tamaño del mensaje como un entero de 4 bytes
                    uint messageSize = reader.ReadUInt32();
                    
                    // Ahora lee el mensaje basado en el tamaño
                    await reader.LoadAsync(messageSize);
                    if (reader.UnconsumedBufferLength < messageSize)
                    {
                        continue; // Si no hay suficiente para leer el mensaje completo, algo fue mal.
                    }

                    // Lee el mensaje completo
                    string message = reader.ReadString(messageSize);

                    // Actualizar el UI con el mensaje recibido
                    var ignored = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {

                        if (lvMessages.Items.Count > 5)
                        {
                            lvMessages.Items.RemoveAt(0);
                        }
                        
                        lvMessages.Items.Add(DesencriptarMissatge(message));
                        txbInput.Text = "";
                    });
                }
            }
            catch (Exception ex)
            {
                // Ocurrió una excepción inesperada, manejar según sea necesario
                Console.WriteLine($"Error al leer del socket: {ex.Message}");
            }
            finally
            {
                reader.Dispose();
            }
        }

        // Función auxiliar para ajustar el endianness del tamaño del mensaje
        private uint ReverseBytes(uint value)
        {
            return (value >> 24) |
                   ((value << 8) & 0x00FF0000) |
                   ((value >> 8) & 0x0000FF00) |
                   (value << 24);
        }
    }
}
