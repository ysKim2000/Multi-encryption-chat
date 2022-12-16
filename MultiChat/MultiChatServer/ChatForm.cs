using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace MultiChatServer {
    public partial class ChatForm : Form {
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppender;
        Socket mainSock;
        IPAddress thisAddress;
        List<Socket> connectedClients;

        public ChatForm() {
            InitializeComponent();
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _textAppender = new AppendTextDelegate(AppendText);
            connectedClients = new List<Socket>();
        }

        void AppendText(Control ctrl, string s) {
            if (ctrl.InvokeRequired) ctrl.Invoke(_textAppender, ctrl, s);
            else {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        void OnFormLoaded(object sender, EventArgs e) {
            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress addr in he.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    AppendText(txtHistory, addr.ToString());
                }
            }


            if (thisAddress == null)
            {
                thisAddress = IPAddress.Loopback; // local host address
                txtAddress.Text = thisAddress.ToString();
            }
            else
            {
                thisAddress = IPAddress.Parse(txtAddress.Text);
            }
        }
        void BeginStartServer(object sender, EventArgs e) {
            int port;
            if (!int.TryParse(txtPort.Text, out port)) {
                MsgBoxHelper.Error("포트 번호가 잘못 입력되었거나 입력되지 않았습니다.");
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            if (thisAddress == null)
            {
                thisAddress = IPAddress.Loopback; // local host address
                txtAddress.Text = thisAddress.ToString();
            }
            else
            {
                thisAddress = IPAddress.Parse(txtAddress.Text);
            }

            IPEndPoint serverEP = new IPEndPoint(thisAddress, port);
            mainSock.Bind(serverEP); // Bind
            mainSock.Listen(10); // Listen

            AppendText(txtHistory, string.Format("서버 시작: @{0}", serverEP));
            mainSock.BeginAccept(AcceptCallback, null); // 비동기로 client의 연결 요청을 받음
        }


        void AcceptCallback(IAsyncResult ar) {
            Socket client = mainSock.EndAccept(ar); // 연결 요청 수락

            mainSock.BeginAccept(AcceptCallback, null); // 비동기로 client 연결 대기

            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = client;

            connectedClients.Add(client); // 연결된 client 리스트에 추가

            AppendText(txtHistory, string.Format("클라이언트 (@ {0})가 연결되었습니다.", client.RemoteEndPoint));

            client.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj); // client data
        }

        void DataReceived(IAsyncResult ar) {
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            int received = obj.WorkingSocket.EndReceive(ar); // data 수신 종료

            if (received <= 0) { // 데이터 없으면 종료
                obj.WorkingSocket.Disconnect(false);
                obj.WorkingSocket.Close();
                return;
            }

            // 받은 data 텍스트 형식으로 변환
            // var EncryptedTextFilePath = Path.GetFullPath(@"..\EncryptedText.txt");
            var dir = @"C:\Users\ys\Desktop\MultiChat\MultiChatServer";
            var EncryptedTextFile = "EncryptedText.txt";
            var EncryptedTextFilePath = Path.Combine(dir, EncryptedTextFile);
            string text = Encoding.UTF8.GetString(obj.Buffer).Trim('\0');
            string saveEncryptedText = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " " + text + "\n";
            File.AppendAllText(EncryptedTextFilePath, saveEncryptedText, Encoding.Default);

            Console.WriteLine(text);
            //  암호화 text -> 복호화 text
            text = AES_decrypt(text, "01234567890123456789012345678901");
             var DecryptedTextFile = "DecryptedText.txt";
            var DecryptedTextFilePath = Path.Combine(dir, DecryptedTextFile);
            string saveDecryptedText = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " " + text + "\n";
            // var DecryptedTextFilePath = Path.GetFullPath(@"..\DecryptedText.txt");
            File.AppendAllText(DecryptedTextFilePath, saveDecryptedText, Encoding.Default);
            Console.WriteLine(text);

            string[] tokens = text.Split('/');
            string id = tokens[0]; // ID
            string msg = tokens[1]; // Message

            AppendText(txtHistory, string.Format("[받음]{0}: {1}", id, msg));

            Console.WriteLine("연결된 모든 클라이언트 수 : " + connectedClients.Count);
            for (int i = connectedClients.Count - 1; i >= 0; i--)
            {
                Socket socket = connectedClients[i];

                try { socket.Send(obj.Buffer); }
                catch
                {
                    try { socket.Dispose(); } catch { }
                    connectedClients.RemoveAt(i);
                }
            }
            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        void OnSendData(object sender, EventArgs e) {
            if (!mainSock.IsBound) {
                MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                return;
            }
            
            string tts = txtTTS.Text.Trim();
            if (string.IsNullOrEmpty(tts)) {
                MsgBoxHelper.Warn("텍스트가 입력되지 않았습니다!");
                txtTTS.Focus();
                return;
            }

            string text = AES_encrypt("Server" + '/' + tts, "01234567890123456789012345678901");
            Console.WriteLine(text);

            byte[] bDts = Encoding.UTF8.GetBytes(text);

            Console.WriteLine("연결된 모든 클라이언트 수 : " + connectedClients.Count);
            for (int i = connectedClients.Count - 1; i >= 0; i--) {
                Socket socket = connectedClients[i];
                
                try { socket.Send(bDts); } catch {
                    try { socket.Dispose(); } catch { }
                    connectedClients.RemoveAt(i);
                }
            }

            AppendText(txtHistory, string.Format("[보냄]server: {0}", tts));
            txtTTS.Clear();
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try {
                mainSock.Close(); }
            catch { }

        }

        //AES256 암호화
        public string AES_encrypt(string Input, string key)
        {
            RijndaelManaged aes = new RijndaelManaged();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes("0123456789012345");

            var encrypt = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] xBuff = null;
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encrypt, CryptoStreamMode.Write))
                {
                    byte[] xXml = Encoding.UTF8.GetBytes(Input);

                    cs.Write(xXml, 0, xXml.Length);
                }

                xBuff = ms.ToArray();
                string recvdata = Encoding.Default.GetString(xBuff);
            }

            string Output = Convert.ToBase64String(xBuff);
            return Output;
        }
        //AES 256 복호화
        public string AES_decrypt(string Input, string key)
        {
            RijndaelManaged aes = new RijndaelManaged();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes("0123456789012345");
            
            var decrypt = aes.CreateDecryptor();
            byte[] xBuff = null;
            using (var ms = new MemoryStream())
            {

                using (var cs = new CryptoStream(ms, decrypt, CryptoStreamMode.Write))
                {
                    byte[] xXml = Convert.FromBase64String(Input);
                    string recvdata = Encoding.Default.GetString(xXml);
                    cs.Write(xXml, 0, xXml.Length);
                }
                xBuff = ms.ToArray();
            }

            string Output = Encoding.UTF8.GetString(xBuff);
            return Output;
        }

        private void txtHistory_TextChanged(object sender, EventArgs e)
        {

        }
    }
}