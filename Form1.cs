using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;

namespace server
{
    public partial class Form1 : Form
    {
        FirestoreDb firestoreDb;
        StorageClient storageClient;
        public Form1()
        {
            InitializeComponent();
            // cấu hình gì đó tới firebase copy anh chat nên không biết 
            InitializeFirestore();
            //lấy dữ 
            loadData();
        }
        private void InitializeFirestore()
        {
            // Đường dẫn đến tệp google-services.json
            string path = AppDomain.CurrentDomain.BaseDirectory + @"google-services.json";

            // Đặt biến môi trường cho Firebase
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);

            // Khởi tạo Firestore với ID dự án của bạn
            firestoreDb = FirestoreDb.Create("ltmcb-7d1a6");
        }
        private TcpListener tcpListener;
        private TcpClient tcpClient;
        private bool isServerRunninng = false;

        Dictionary<string, TcpClient> id_tcpclient;
        List<string> id_list;
        
        //lấy id từ firebase
        private async void loadData()
        {
            id_list = new List<string>();
            CollectionReference collectionReference = firestoreDb.Collection("UserData");
            QuerySnapshot snapshot = await collectionReference.GetSnapshotAsync();
            foreach (var item in snapshot.Documents)
            {
                id_list.Add(item.Id);
            }
        }
        //chạy server
        private void btn_start_Click(object sender, EventArgs e)
        {
            if (isServerRunninng)
            {
                isServerRunninng = false;
                tcpListener.Stop();
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText($"{DateTime.Now} : Server stop listen on port 8080.\r\n");
                }));
                btn_start.Text = "Start";
            }
            else
            {
                isServerRunninng = true;
                Thread serverThread = new Thread(listen);
                serverThread.Start();
                serverThread.IsBackground = true;
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText($"{DateTime.Now} : Server is listening on port 8080.\r\n");
                }));
                btn_start.Text = "Stop";
            }
        }
        // thread dùng để lắng nghe các yêu cầu muốn kết nối tới server
        private void listen()
        {
            //tạo ip cho server + port
            IPEndPoint iPEndPoint = new IPEndPoint (IPAddress.Any, 8080);
            //gán cho 1 tcplistener
            tcpListener = new TcpListener(iPEndPoint);
            tcpListener.Start(); //chạy nó
            id_tcpclient = new Dictionary<string, TcpClient>();

            while (isServerRunninng)
            {
                try
                {
                    //chấp nhận kết nối và gán nó cho 1 tcpclient
                    tcpClient = tcpListener.AcceptTcpClient();
                    StreamWriter writer = new StreamWriter(tcpClient.GetStream());
                    StreamReader reader = new StreamReader(tcpClient.GetStream());
                    writer.AutoFlush = true;

                    string id = reader.ReadLine(); // nhận id từ client gửi 
                    //tạo 1 cặp id_tcpclient để phần biết các tcpclient để gửi đến chính xác 
                    id_tcpclient.Add(id, tcpClient);

                    //thread dùng để server và client giao tiếp với nhau
                    Thread receiveThread = new Thread(() => receiveFromCLient(id));
                    receiveThread.Start();
                    receiveThread.IsBackground = true;
                }
                catch
                {

                }
            }
        }
        class Message
        {
            public string sender { get; set; }
            public string id_sender { get; set; }
            public string receiver { get; set; }
            public string id_receiver { get; set; }
            public string data { get; set; }
        }
        private void receiveFromCLient(string id)
        {
            //lấy tcpclient với id tương ứng 
            TcpClient tcpClient = id_tcpclient[id];
            StreamReader reader = new StreamReader(tcpClient.GetStream());
            StreamWriter writer = new StreamWriter (tcpClient.GetStream());
            writer.AutoFlush = true;

            //lặp đến khi tắt kết nối 
            while (tcpClient.Connected)
            {
                try
                {
                    string signalFromClient = reader.ReadLine();

                    if (signalFromClient == "Message")
                    {
                        string data = reader.ReadLine().Trim();
                        Message message = JsonConvert.DeserializeObject<Message>(data);
                        Invoke(new Action(() =>
                        {
                            richTextBox1.AppendText($"Nguoi gui: {message.sender} ({message.id_sender})," +
                                                $" Noi dung: {message.data}, " +
                                                $"Nguoi nhan: {message.receiver}({message.id_receiver})\r\n");
                        }));
                        foreach (var item in id_tcpclient)
                        {
                            if (item.Key.CompareTo(message.id_receiver) == 0)
                            {
                                StreamWriter receiver = new StreamWriter(item.Value.GetStream());
                                receiver.AutoFlush = true;
                                receiver.WriteLine("Message|"+data);
                                break;
                            }
                        }
                    }
                }
                catch
                {

                }
            }
        }
    }
}
