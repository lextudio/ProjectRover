// Ported ImageListStreamer to produce Avalonia Bitmaps.
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Avalonia.Media.Imaging;
using Avalonia;

namespace System.Windows.Forms
{
	[Serializable]
	public sealed class ImageListStreamer : ISerializable
	{
		readonly object imageCollection;
		Bitmap[] images;
		PixelSize image_size;
		Avalonia.Media.Color back_color;

		internal ImageListStreamer(object imageCollection)
		{
			this.imageCollection = imageCollection;
		}

		private ImageListStreamer(SerializationInfo info, StreamingContext context)
		{
			byte[] data = (byte[])info.GetValue("Data", typeof(byte[]));
			if (data == null || data.Length <= 4)
				return;

			if (data[0] != 77 || data[1] != 83 || data[2] != 70 || data[3] != 116)
				return;

			using var decoded = GetDecodedStream(data, 4, data.Length - 4);
			decoded.Position = 4;
			using var reader = new BinaryReader(decoded);
			ushort nimages = reader.ReadUInt16();
			reader.ReadUInt16(); // cMaxImage
			reader.ReadUInt16(); // grow
			ushort cx = reader.ReadUInt16();
			ushort cy = reader.ReadUInt16();
			uint bkcolor = reader.ReadUInt32();
			back_color = Avalonia.Media.Color.FromUInt32(bkcolor);
			reader.ReadUInt16(); // flags

			// skip overlay entries
			decoded.Seek(4 * 2, SeekOrigin.Current);

			var buf = decoded.GetBuffer();
			var list = new List<Bitmap>();

			for (int i = 0; i < buf.Length - 1; i++)
			{
				if (buf[i] == 0x42 && buf[i + 1] == 0x4D)
				{
					try
					{
						using var ms = new MemoryStream(buf, i, buf.Length - i);
						var bmp = new Bitmap(ms);
						list.Add(bmp);
						i += Math.Max((int)bmp.Size.Width * (int)bmp.Size.Height / 8, 64);
					}
					catch
					{
						// ignore
					}
				}
			}

			images = list.ToArray();
			image_size = new PixelSize(cx, cy);
		}

		static MemoryStream GetDecodedStream(byte[] bytes, int offset, int size)
		{
			var ms = new MemoryStream();
			ms.Write(bytes, offset, size);
			ms.Position = 0;
			return ms;
		}

		static byte[] header = new byte[] { 77, 83, 70, 116, 73, 76, 1, 1 };

		public void GetObjectData(SerializationInfo si, StreamingContext context)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			writer.Write(header);
			writer.Write((ushort)(images?.Length ?? 0));
			writer.Write((ushort)(images?.Length ?? 0));
			writer.Write((ushort)4);
			writer.Write((ushort)(image_size.Width));
			writer.Write((ushort)(image_size.Height));
			writer.Write((uint)0xFFFFFFFF);
			writer.Write((ushort)0x21);
			si.AddValue("Data", stream.ToArray(), typeof(byte[]));
		}

		internal Bitmap[] Images => images ?? Array.Empty<Bitmap>();

		internal PixelSize ImageSize => image_size;

		internal Avalonia.Media.Color BackColor => back_color;
	}
}

