using System;
using System.Net.Sockets;

namespace MultiChatClient {
    public class AsyncObject {
        public byte[] Buffer;
        public Socket WorkingSocket;
        public readonly int BufferSize;
        public AsyncObject(int bufferSize) {
            BufferSize = bufferSize;
            Buffer = new byte[BufferSize];
        }

        public void ClearBuffer() {
            Array.Clear(Buffer, 0, BufferSize);
        }
    }
}