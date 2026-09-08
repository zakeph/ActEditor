using System;
using System.IO;

namespace ActEditor.Core {
	public static class SpriteSaveCompatibility {
		public static void NormalizePaletteTail(string file) {
			if (String.IsNullOrEmpty(file) || !File.Exists(file))
				return;

			byte[] data = File.ReadAllBytes(file);
			int paletteOffset = _getPaletteOffset(data);

			if (paletteOffset < 0)
				return;

			int paletteLength = data.Length - paletteOffset;

			if (paletteLength != 1027)
				return;

			if (paletteOffset + 7 > data.Length)
				return;

			if (data[paletteOffset + 4] != 0 || data[paletteOffset + 5] != 0 || data[paletteOffset + 6] != 0)
				return;

			byte[] fixedData = new byte[data.Length - 3];
			Buffer.BlockCopy(data, 0, fixedData, 0, paletteOffset + 4);
			Buffer.BlockCopy(data, paletteOffset + 7, fixedData, paletteOffset + 4, data.Length - paletteOffset - 7);
			File.WriteAllBytes(file, fixedData);
		}

		private static int _getPaletteOffset(byte[] data) {
			if (data == null || data.Length < 8)
				return -1;

			if (data[0] != 'S' || data[1] != 'P')
				return -1;

			int indexedCount = BitConverter.ToUInt16(data, 4);
			int bgra32Count = BitConverter.ToUInt16(data, 6);
			int offset = 8;

			for (int i = 0; i < indexedCount; i++) {
				if (offset + 6 > data.Length)
					return -1;

				int dataLength = BitConverter.ToUInt16(data, offset + 4);
				offset += 6 + dataLength;
			}

			for (int i = 0; i < bgra32Count; i++) {
				if (offset + 4 > data.Length)
					return -1;

				int width = BitConverter.ToUInt16(data, offset);
				int height = BitConverter.ToUInt16(data, offset + 2);
				long nextOffset = offset + 4L + width * height * 4L;

				if (nextOffset > data.Length)
					return -1;

				offset = (int)nextOffset;
			}

			return offset;
		}
	}
}
