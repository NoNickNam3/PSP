using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
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

namespace Client2
{
    /// <summary>
    /// Página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
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
                if (socket == null || !IsSocketConnected(socket))
                {
                    socket = new StreamSocket();
                    await socket.ConnectAsync(new Windows.Networking.HostName(txbIp.Text), txbPort.Text);
                    var listenTask = ListenForMessagesAsync(socket, new CancellationToken());
                }

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

            if (cmbTipusConnexio.SelectedIndex > -1)
            {
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
                    des.Key = Encoding.ASCII.GetBytes("12345678");
                    des.IV = Encoding.ASCII.GetBytes("12345678");

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
                sortida += (char)((int)c + 3);
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
                    des.Key = Encoding.ASCII.GetBytes("12345678");
                    des.IV = Encoding.ASCII.GetBytes("12345678");

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        CryptoStream cryptoStream = new CryptoStream(memoryStream,
                            des.CreateDecryptor(), CryptoStreamMode.Write);
                        byte[] inputBytes = Convert.FromBase64String(missatge);
                        cryptoStream.Write(inputBytes, 0, inputBytes.Length);
                        cryptoStream.FlushFinalBlock();
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

        private bool IsSocketConnected(StreamSocket socket)
        {
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
                    await reader.LoadAsync(sizeof(uint));
                    if (reader.UnconsumedBufferLength < sizeof(uint))
                    {
                        continue;
                    }

                    uint messageSize = reader.ReadUInt32();
                    await reader.LoadAsync(messageSize);
                    if (reader.UnconsumedBufferLength < messageSize)
                    {
                        continue;
                    }

                    string message = reader.ReadString(messageSize);
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
                Console.WriteLine($"Error al leer del socket: {ex.Message}");
            }
            finally
            {
                reader.Dispose();
            }
        }

        private uint ReverseBytes(uint value)
        {
            return (value >> 24) |
                   ((value << 8) & 0x00FF0000) |
                   ((value >> 8) & 0x0000FF00) |
                   (value << 24);
        }
    }
}
