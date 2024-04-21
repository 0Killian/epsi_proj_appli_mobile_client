using System.Runtime.InteropServices;
using System.Text;

namespace epsi_projet_mobile_client.Services;

public class Protocol
{
    private const ulong HeaderSignature = 0x74DE3F8276ABC849;
    
    public enum MessageType : byte
    {
        Hello = 0,
        Login = 1,
        LoginSuccess = 2,
        LoginFailure = 3,
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MessageHeader
    {
        public uint Size;
        public byte Hash;
        public MessageType Type;
    }

    
    private interface IMessageData<out T>
    {
        public static abstract MessageType MessageType { get; }
        
        public byte[] MessageData { get; }
        
        public static abstract T Parse(byte[] dataRaw);
    }
    
    private struct HelloMessageData : IMessageData<HelloMessageData>
    {
        public uint Challenge;
        public ulong SessionId;

        public static MessageType MessageType => MessageType.Hello;

        public byte[] MessageData
        {
            get
            {
                var raw = new byte[4 + 8];
                BitConverter.GetBytes(Challenge).CopyTo(raw, 0);
                BitConverter.GetBytes(SessionId).CopyTo(raw, 4);
                return raw;
            }
        }

        public static HelloMessageData Parse(byte[] dataRaw)
        {
            return new HelloMessageData
            {
                Challenge = BitConverter.ToUInt32(dataRaw, 0),
                SessionId = BitConverter.ToUInt64(dataRaw, 4)
            };
        }
    }
    
    private struct LoginMessageData : IMessageData<LoginMessageData>
    {
        public uint Challenge;
        public string Username;
        public string Password;
        public ulong SessionId;

        public static MessageType MessageType => MessageType.Login;

        public byte[] MessageData
        {
            get
            {
                var raw = new byte[4 + 1 + 1 + 8 + Username.Length + Password.Length];
                
                BitConverter.GetBytes(Challenge).CopyTo(raw, 0);
                raw[4] = (byte)Username.Length;
                raw[5] = (byte)Password.Length;
                BitConverter.GetBytes(SessionId).CopyTo(raw, 6);
                Encoding.UTF8.GetBytes(Username).CopyTo(raw, 14);
                Encoding.UTF8.GetBytes(Password).CopyTo(raw, 14 + Username.Length);
                
                return raw;
            }
        }

        public static LoginMessageData Parse(byte[] dataRaw)
        {
            return new LoginMessageData
            {
                Challenge = BitConverter.ToUInt32(dataRaw, 0),
                SessionId = BitConverter.ToUInt64(dataRaw, 6),
                Username = BitConverter.ToString(dataRaw, 14, dataRaw[4]),
                Password = BitConverter.ToString(dataRaw, 14 + dataRaw[4], dataRaw[5])
            };
        }
    }

    public struct LoginSuccessMessageData : IMessageData<LoginSuccessMessageData>
    {
        public ulong SessionId;

        public static MessageType MessageType => MessageType.LoginSuccess;
        public byte[] MessageData => BitConverter.GetBytes(SessionId);

        public static LoginSuccessMessageData Parse(byte[] dataRaw)
        {
            return new LoginSuccessMessageData
            {
                SessionId = BitConverter.ToUInt64(dataRaw, 0)
            };
        }
    }
    
    public struct LoginFailureMessageData : IMessageData<LoginFailureMessageData>
    {
        public ulong SessionId;
        public string Message;

        public static MessageType MessageType => MessageType.LoginFailure;

        public byte[] MessageData
        {
            get
            {
                var messageRaw = new byte[8 + 1 + Message.Length];

                BitConverter.GetBytes(SessionId).CopyTo(messageRaw, 0);
                messageRaw[8] = (byte)Message.Length;
                Encoding.UTF8.GetBytes(Message).CopyTo(messageRaw, 9);
                
                return messageRaw;
            }
        }

        public static LoginFailureMessageData Parse(byte[] dataRaw)
        {
            return new LoginFailureMessageData
            {
                SessionId = BitConverter.ToUInt64(dataRaw, 0),
                Message = Encoding.UTF8.GetString(dataRaw, 8, dataRaw[8])
            };
        }
    }

    private static byte ComputeHash(IEnumerable<byte> data)
    {
        var hash = data.Aggregate((byte)0, (current, t) => (byte)(current + t));

        return (byte)(255 - hash);
    }

    private readonly BluetoothCharacteristic _reader;
    private readonly BluetoothCharacteristic _writer;
    public ulong? SessionId { get; private set; }

    private static uint DoChallenge(uint challenge)
    {
        return (challenge ^ 0x74DE3F82) + challenge;
    }

    public Protocol(BluetoothCharacteristic reader, BluetoothCharacteristic writer)
    {
        _writer = writer;
        _reader = reader;
    }

    public async Task PerformHandshake()
    {
        var hello = await ReadNextMessage<HelloMessageData>();
        var completedChallenge = DoChallenge(hello.Challenge);

        await SendMessage(new LoginMessageData
        {
            SessionId = hello.SessionId,
            Challenge = completedChallenge,
            Username = "username",
            Password = "password"
        });
        
        var (header, messageData) = await ReadNextMessageAny();
        if (header.Type == MessageType.LoginSuccess)
        {
            SessionId = hello.SessionId;
            return;
        }

        if (header.Type != MessageType.LoginFailure)
        {
            throw new Exception("Invalid message type");
        }
        
        var data = LoginFailureMessageData.Parse(messageData);
        throw new Exception(data.Message);
    }

    private async Task<T> ReadNextMessage<T>() where T : IMessageData<T>
    {
        var (header, dataRaw) = await ReadNextMessageAny();
        
        if (header.Type != T.MessageType)
            throw new Exception("Invalid message type");

        var data = T.Parse(dataRaw);

        if (header.Hash != ComputeHash(dataRaw))
        {
            throw new Exception($"Invalid message hash, found {ComputeHash(dataRaw)} expected {header.Hash}");
        }
        
        return data;
    }

    private async Task<(MessageHeader, byte[])> ReadNextMessageAny()
    {
        var headerSignature = new byte[8];
        if (await _reader.ReadAsync(headerSignature) != headerSignature.Length)
            throw new Exception("Failed to read header signature");

        if (!CheckSignature(headerSignature))
            throw new Exception("Invalid header signature");

        var headerRaw = new byte[Marshal.SizeOf(typeof(MessageHeader))];

        if (await _reader.ReadAsync(headerRaw) != headerRaw.Length)
            throw new Exception("Failed to read header");

        var header = MemoryMarshal.Cast<byte, MessageHeader>(headerRaw)[0];
        var dataRaw = new byte[header.Size];

        if (await _reader.ReadAsync(dataRaw) != dataRaw.Length)
            throw new Exception("Failed to read data");

        return (header, dataRaw);
    }

    private async Task SendMessage<T>(T data) where T : IMessageData<T>
    {
        var dataRaw = data.MessageData;
        var header = new MessageHeader
        {
            Size = (uint)dataRaw.Length,
            Hash = ComputeHash(dataRaw),
            Type = T.MessageType
        };

        var headerRaw = new byte[Marshal.SizeOf<MessageHeader>()];
        var ptr = IntPtr.Zero;

        try
        {
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf<MessageHeader>());
            Marshal.StructureToPtr(header, ptr, true);
            Marshal.Copy(ptr, headerRaw, 0, Marshal.SizeOf<MessageHeader>());
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
        
        _writer.Write(BitConverter.GetBytes(HeaderSignature));
        _writer.Write(headerRaw);
        _writer.Write(dataRaw);
        await _writer.Flush();
    }
    
    private static bool CheckSignature(IEnumerable<byte> signature)
    {
        var correctSignature = BitConverter.GetBytes(HeaderSignature);
        return signature.SequenceEqual(correctSignature);
    }
}