using ActImaging;
using GRF.FileFormats.ActFormat;
using GRF.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ActEditor.Components {
	internal static class ApngEncoder {
		private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

		public static void Save(string filePath, Act act, int actionIndex, IProgress progress, string[] extra) {
			int indexFrom = 0;
			int indexTo = act[actionIndex].NumberOfFrames;
			int delay = (int)Math.Ceiling(act[actionIndex].AnimationSpeed * 25);
			int margin = 0;
			bool uniform = true;
			Color guideLinesColor = Colors.Transparent;
			BitmapScalingMode scaling = BitmapScalingMode.NearestNeighbor;

			for (int i = 0; i + 1 < extra.Length; i += 2) {
				string value = extra[i + 1];
				switch (extra[i]) {
					case "indexFrom": indexFrom = Int32.Parse(value); break;
					case "indexTo": indexTo = Int32.Parse(value); break;
					case "uniform": uniform = Boolean.Parse(value); break;
					case "guideLinesColor": guideLinesColor = (Color)ColorConverter.ConvertFromString(value); break;
					case "scaling": scaling = (BitmapScalingMode)Enum.Parse(typeof(BitmapScalingMode), value); break;
					case "delay": delay = Int32.Parse(value); break;
					case "delayFactor": delay = (int)(delay * Single.Parse(value, CultureInfo.InvariantCulture)); break;
					case "margin": margin = Int32.Parse(value, CultureInfo.InvariantCulture); break;
				}
			}

			var images = Imaging.GenerateImages(act, actionIndex, uniform, guideLinesColor, margin, scaling);
			indexFrom = Math.Max(0, indexFrom);
			indexTo = Math.Min(images.Count, indexTo);
			var frames = images.Skip(indexFrom).Take(Math.Max(0, indexTo - indexFrom))
				.Select(image => ToRgbaFrame(image, scaling)).ToList();

			if (frames.Count == 0)
				return;

			string directory = Path.GetDirectoryName(filePath);
			if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
				stream.Write(PngSignature, 0, PngSignature.Length);
				WriteHeader(stream, frames[0].Width, frames[0].Height);
				WriteAnimationControl(stream, frames.Count);

				uint sequence = 0;
				for (int i = 0; i < frames.Count; i++) {
					WriteFrameControl(stream, sequence++, frames[i], delay);
					byte[] compressed = Compress(frames[i]);

					if (i == 0) {
						WriteChunk(stream, "IDAT", compressed);
					}
					else {
						using (var data = new MemoryStream()) {
							WriteUInt32(data, sequence++);
							data.Write(compressed, 0, compressed.Length);
							WriteChunk(stream, "fdAT", data.ToArray());
						}
					}

					if (progress != null) {
						progress.Progress = (i + 1) * 100f / frames.Count;
						if (progress.IsCancelling) {
							progress.IsCancelled = true;
							return;
						}
					}
				}

				WriteChunk(stream, "IEND", new byte[0]);
			}
		}

		private static RgbaFrame ToRgbaFrame(ImageSource source, BitmapScalingMode scaling) {
			BitmapFrame rendered = Imaging.ForceRender(source, scaling);
			int width = rendered.PixelWidth;
			int height = rendered.PixelHeight;
			byte[] bgra = new byte[width * height * 4];
			rendered.CopyPixels(bgra, width * 4, 0);

			byte[] rgba = new byte[bgra.Length];
			for (int i = 0; i < bgra.Length; i += 4) {
				byte alpha = bgra[i + 3];
				if (alpha == 0) {
					rgba[i] = rgba[i + 1] = rgba[i + 2] = rgba[i + 3] = 0;
				}
				else {
					// ForceRender returns premultiplied BGRA. PNG stores straight alpha;
					// leaving it premultiplied makes viewers apply alpha a second time.
					rgba[i] = Unpremultiply(bgra[i + 2], alpha);
					rgba[i + 1] = Unpremultiply(bgra[i + 1], alpha);
					rgba[i + 2] = Unpremultiply(bgra[i], alpha);
					rgba[i + 3] = alpha;
				}
			}

			return new RgbaFrame(width, height, rgba);
		}

		private static byte Unpremultiply(byte value, byte alpha) {
			return (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
		}

		private static byte[] Compress(RgbaFrame frame) {
			byte[] scanlines = new byte[(frame.Width * 4 + 1) * frame.Height];
			for (int y = 0; y < frame.Height; y++) {
				int destination = y * (frame.Width * 4 + 1);
				scanlines[destination] = 0;
				Buffer.BlockCopy(frame.Pixels, y * frame.Width * 4, scanlines, destination + 1, frame.Width * 4);
			}

			using (var output = new MemoryStream()) {
				output.WriteByte(0x78);
				output.WriteByte(0x9c);
				using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true)) {
					deflate.Write(scanlines, 0, scanlines.Length);
				}
				WriteUInt32(output, Adler32(scanlines));
				return output.ToArray();
			}
		}

		private static void WriteHeader(Stream stream, int width, int height) {
			using (var data = new MemoryStream()) {
				WriteUInt32(data, (uint)width);
				WriteUInt32(data, (uint)height);
				data.WriteByte(8);
				data.WriteByte(6);
				data.WriteByte(0);
				data.WriteByte(0);
				data.WriteByte(0);
				WriteChunk(stream, "IHDR", data.ToArray());
			}
		}

		private static void WriteAnimationControl(Stream stream, int frameCount) {
			using (var data = new MemoryStream()) {
				WriteUInt32(data, (uint)frameCount);
				WriteUInt32(data, 0);
				WriteChunk(stream, "acTL", data.ToArray());
			}
		}

		private static void WriteFrameControl(Stream stream, uint sequence, RgbaFrame frame, int delayMilliseconds) {
			using (var data = new MemoryStream()) {
				WriteUInt32(data, sequence);
				WriteUInt32(data, (uint)frame.Width);
				WriteUInt32(data, (uint)frame.Height);
				WriteUInt32(data, 0);
				WriteUInt32(data, 0);
				WriteUInt16(data, (ushort)Math.Max(1, Math.Min(UInt16.MaxValue, delayMilliseconds)));
				WriteUInt16(data, 1000);
				data.WriteByte(0);
				data.WriteByte(0);
				WriteChunk(stream, "fcTL", data.ToArray());
			}
		}

		private static void WriteChunk(Stream stream, string type, byte[] data) {
			byte[] typeBytes = Encoding.ASCII.GetBytes(type);
			WriteUInt32(stream, (uint)data.Length);
			stream.Write(typeBytes, 0, typeBytes.Length);
			stream.Write(data, 0, data.Length);
			byte[] crcData = new byte[typeBytes.Length + data.Length];
			Buffer.BlockCopy(typeBytes, 0, crcData, 0, typeBytes.Length);
			Buffer.BlockCopy(data, 0, crcData, typeBytes.Length, data.Length);
			WriteUInt32(stream, Crc32(crcData));
		}

		private static void WriteUInt16(Stream stream, ushort value) {
			stream.WriteByte((byte)(value >> 8));
			stream.WriteByte((byte)value);
		}

		private static void WriteUInt32(Stream stream, uint value) {
			stream.WriteByte((byte)(value >> 24));
			stream.WriteByte((byte)(value >> 16));
			stream.WriteByte((byte)(value >> 8));
			stream.WriteByte((byte)value);
		}

		private static uint Adler32(byte[] data) {
			const uint mod = 65521;
			uint a = 1;
			uint b = 0;
			foreach (byte value in data) {
				a = (a + value) % mod;
				b = (b + a) % mod;
			}
			return (b << 16) | a;
		}

		private static uint Crc32(byte[] data) {
			uint crc = 0xffffffff;
			foreach (byte value in data) {
				crc ^= value;
				for (int bit = 0; bit < 8; bit++)
					crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
			}
			return crc ^ 0xffffffff;
		}

		private sealed class RgbaFrame {
			public readonly int Width;
			public readonly int Height;
			public readonly byte[] Pixels;

			public RgbaFrame(int width, int height, byte[] pixels) {
				Width = width;
				Height = height;
				Pixels = pixels;
			}
		}
	}
}
