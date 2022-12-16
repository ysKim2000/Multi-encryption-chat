using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace MultiChatClient {
    public partial class ChatForm : Form {
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppender;
        Socket mainSock;
        IPAddress thisAddress;
        string nameID;

        public ChatForm() {
            InitializeComponent();
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _textAppender = new AppendTextDelegate(AppendText);
        }

        void AppendText(Control ctrl, string s) {
            if (ctrl.InvokeRequired) ctrl.Invoke(_textAppender, ctrl, s);
            else {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }

        void OnFormLoaded(object sender, EventArgs e) {

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

        void OnConnectToServer(object sender, EventArgs e) {
            if (mainSock.Connected) {
                MsgBoxHelper.Error("이미 연결되어 있습니다!");
                return;
            }

            int port=15000; // port 고정

            nameID = txtID.Text; // input ID

            AppendText(txtHistory, string.Format("서버: @{0}, port: 15000, ID: @{1}", txtAddress.Text, nameID));
            try {
                mainSock.Connect(txtAddress.Text, port); }
            catch (Exception ex) {
                MsgBoxHelper.Error("연결에 실패했습니다!\n오류 내용: {0}", MessageBoxButtons.OK, ex.Message);
                return;
            }
            AppendText(txtHistory, "서버와 연결되었습니다."); // 연결 완료

            AsyncObject obj = new AsyncObject(4096); // 수신 대기 receive 상태
            obj.WorkingSocket = mainSock;
            mainSock.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0, DataReceived, obj);
        }
        
        void DataReceived(IAsyncResult ar) {
            AsyncObject obj = (AsyncObject) ar.AsyncState;

            int received = obj.WorkingSocket.EndReceive(ar); // receive 종료

            if (received <= 0) { // 받은 데이터가 없으면
                obj.WorkingSocket.Close(); // 종료
                return;
            }

            string text = Encoding.UTF8.GetString(obj.Buffer).Trim('\0');
            Console.WriteLine(text);
            text = AES_decrypt(text, "01234567890123456789012345678901");

            Console.WriteLine(text);

            string[] tokens = text.Split('/');
            string id = tokens[0];  // ID
            string msg = tokens[1]; // message

            AppendText(txtHistory, string.Format("[받음]{0}: {1}", id, msg)); // winform UI TEXT message

            obj.ClearBuffer();

            obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj); // 수신 대기
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

            string pro = nameID + '/' + tts ;
            string text = AES_encrypt(pro, "01234567890123456789012345678901"); // AES 암호화
            Console.WriteLine(text);
            byte[] bDts = Encoding.UTF8.GetBytes(text);

            mainSock.Send(bDts); // Send: 서버 전송

            txtTTS.Clear(); // Clear
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mainSock!=null) {
                mainSock.Disconnect(false); // 연결 끊기
                mainSock.Close(); // 종료
            }

        }

        // AES256 암호화
        // RijndaelManaged와 CryptoStream을 이용하여 암호화
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

        private void enter_pressed(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (!mainSock.IsBound) // 서버 대기 중?
                {
                    MsgBoxHelper.Warn("서버가 실행되고 있지 않습니다!");
                    return;
                }

                string tts = txtTTS.Text.Trim();
                if (string.IsNullOrEmpty(tts))
                {
                    MsgBoxHelper.Warn("텍스트가 입력되지 않았습니다!");
                    txtTTS.Focus();
                    return;
                }

                string pro = nameID + '/' + tts + "/sh/11";
                string text = AES_encrypt(pro, "01234567890123456789012345678901");
                Console.WriteLine(text);

                byte[] bDts = Encoding.UTF8.GetBytes(text);

                mainSock.Send(bDts);

                txtTTS.Clear();
            }
        }

        private void txtHistory_TextChanged(object sender, EventArgs e)
        {


        }
    }
}