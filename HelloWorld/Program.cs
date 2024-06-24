using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace HelloWorld
{
	internal class Program
	{
		const int SEGMENT_BITS = 0x7F;
		const int CONTINUE_BIT = 0x80;
		static void Main(string[] args)
		{
			Console.WriteLine("Server starting");
			TcpListener server = new(System.Net.IPAddress.Any, 25565);

			server.Start();
			Console.WriteLine("Server started");
			while (true)
			{
				Console.WriteLine("Waiting for a connection...");

				using (TcpClient client = server.AcceptTcpClient())
				{
					Console.WriteLine($"Client connected");
					var stream = client.GetStream();

					try
					{

						int next_state = ProcessHandshake(stream);
						while (next_state == 1)
						{
							if (!client.Connected) break;
							int length = ReadVarInt(stream);
							Console.WriteLine($"Packet size: {length}");
							byte[] data_bytes = new byte[length];
							stream.ReadExactly(data_bytes, 0, length);
							using (var data_stream = new MemoryStream(data_bytes))
							{
								int id = ReadVarInt(data_stream);
								Console.WriteLine($"Packet id: {id}");
								switch (id)
								{
									case 0:
										ProcessStatusRequest(stream);
										break;
									case 1:
										ProcessPingPacket(stream, data_stream);
										break;
									default:
										Console.WriteLine($"Cannot handle packet");
										break;
								}
								if (id == 1) break;
							}
							if (!client.Connected) break;
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error: {ex}");
					}
					client.Dispose();
				}
				Console.WriteLine("Disconnected");
			}
		}
		

		static int ReadVarInt(Stream stream) {
			int value = 0;
			int position = 0;
			int currentByte;

			while (true)
			{
				currentByte = stream.ReadByte();
				if (currentByte == -1) throw new SocketException(1,"Unexpected EOF");
				value |= (currentByte & SEGMENT_BITS) << position;

				if ((currentByte & CONTINUE_BIT) == 0) break;

				position += 7;

				if (position >= 32) throw new IndexOutOfRangeException("VarInt is too big");
			}

			return value;
		}
		static string ReadString(Stream stream)
		{
			int length = ReadVarInt(stream);
			var bytes = new byte[length];
			stream.ReadExactly(bytes, 0, length);
			return Encoding.UTF8.GetString(bytes);
		}

		static void WriteVarInt(Stream stream, int value) {
			while (true)
			{
				if ((value & ~SEGMENT_BITS) == 0)
				{
					stream.WriteByte((byte)value);
					return;
				}
				
				stream.WriteByte((byte)((value & SEGMENT_BITS) | CONTINUE_BIT));

				value >>>= 7;
			}
		}
		static void WriteString(Stream stream, string text) { 
			var bytes = Encoding.UTF8.GetBytes(text);
			WriteVarInt(stream, bytes.Length);
			stream.Write(bytes, 0, bytes.Length);
		}

		static int ProcessHandshake(Stream stream) { 
			int length = ReadVarInt(stream);
			Console.WriteLine($"Packet size: {length}");
			byte[] data_bytes = new byte[length];
			stream.ReadExactly(data_bytes, 0, length);

			using (MemoryStream data_stream = new MemoryStream(data_bytes))
			{
				int id = ReadVarInt(data_stream);
				Console.WriteLine($"Packet id: {id}");

				Console.WriteLine($"Protocol Version: {ReadVarInt(data_stream)}");
				Console.WriteLine($"Address used to connect: {ReadString(data_stream)}");
				byte[] short_bytes = new byte[2];
				data_stream.ReadExactly(short_bytes, 0, 2);
				Console.WriteLine($"Server port: {BinaryPrimitives.ReadUInt16BigEndian(short_bytes)}");
				int next_state = ReadVarInt(data_stream);
				Console.WriteLine($"Next State: {next_state}");
				return next_state;
			}
		}

		static void ProcessStatusRequest(Stream stream) {
			using (MemoryStream data = new()) {
				WriteVarInt(data, 0);
				WriteString(data, """
{
    ""version"": {
        ""name"": ""1.19.4"",
        ""protocol"": 762
    },
    ""players"": {
        ""max"": 100,
        ""online"": 5,
        ""sample"": [
            {
                ""name"": ""thinkofdeath"",
                ""id"": ""4566e69f-c907-48ee-8d71-d7ba5aa00d20""
            }
        ]
    },
    ""description"": {
        ""text"": ""Hello, world!""
    },
    ""favicon"": ""data:image/png;base64,

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"",
    ""enforcesSecureChat"": false,
    ""previewsChat"": false
}
""");

				var bytes = data.GetBuffer();
				WriteVarInt(stream, bytes.Length);
				stream.Write(bytes,0,bytes.Length);
				Console.WriteLine("Answered the status request.");
			}
		}
		static void ProcessPingPacket(Stream stream, Stream read_only_data) {
			var keepalive = new byte[8];
			read_only_data.ReadExactly(keepalive, 0, 8);

			using (MemoryStream data = new())
			{
				WriteVarInt(data, 1);
				data.Write(keepalive);

				var bytes = data.GetBuffer();
				WriteVarInt(stream, bytes.Length);
				stream.Write(bytes, 0, bytes.Length);
				Console.WriteLine("Answered the ping request.");
			}
		}
	}
}
