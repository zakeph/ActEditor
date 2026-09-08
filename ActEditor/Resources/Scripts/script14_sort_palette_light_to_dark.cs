using System;
using System.Collections.Generic;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Image;

namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Sort used palette colors by tone"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "{Scripts.SortPaletteByTone}"; }
		}

		public string Image {
			get { return "gradient.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null || act.Sprite == null || act.Sprite.Palette == null || act.Sprite.NumberOfIndexed8Images <= 0)
				return;

			bool commandsStarted = false;

			try {
				byte[] palette = _getPalette(act);

				if (palette == null) {
					ErrorHandler.HandleException("No palette bytes were found.", ErrorLevel.Warning);
					return;
				}

				bool[] used = _getUsedPaletteIndexes(act);
				Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
				List<int> transparentUsed = new List<int>();

				for (int i = 1; i < 256; i++) {
					if (!used[i])
						continue;

					if (_isTransparentMarker(palette, i)) {
						transparentUsed.Add(i);
					}
					else {
						int groupKey = _getToneGroup(palette, i);

						if (!groups.ContainsKey(groupKey))
							groups[groupKey] = new List<int>();

						groups[groupKey].Add(i);
					}
				}

				int realColorCount = 0;
				List<int> groupKeys = new List<int>(groups.Keys);
				groupKeys.Sort();

				foreach (int groupKey in groupKeys) {
					groups[groupKey].Sort((left, right) => _compareByValue(palette, left, right));
					realColorCount += groups[groupKey].Count;
				}

				if (realColorCount <= 1) {
					ErrorHandler.HandleException("Not enough used palette colors to sort.", ErrorLevel.NotSpecified);
					return;
				}

				byte[] sortedPalette = _copy(palette);
				_markAllAsTransparent(sortedPalette);

				byte[] remap = new byte[256];

				for (int i = 0; i < 256; i++) {
					remap[i] = (byte)i;
				}

				foreach (int index in transparentUsed) {
					remap[index] = 0;
				}

				int targetIndex = 1;

				foreach (int groupKey in groupKeys) {
					foreach (int sourceIndex in groups[groupKey]) {
						if (targetIndex > 255)
							break;

						Buffer.BlockCopy(palette, sourceIndex * 4, sortedPalette, targetIndex * 4, 4);
						remap[sourceIndex] = (byte)targetIndex;
						targetIndex++;
					}
				}

				if (_isIdentity(remap) && _byteArrayCompare(palette, sortedPalette))
					return;

				act.Commands.BeginNoDelay();
				commandsStarted = true;

				for (int i = 0; i < act.Sprite.NumberOfIndexed8Images; i++) {
					GrfImage sourceImage = act.Sprite.Images[i];

					if (sourceImage == null || sourceImage.Pixels == null)
						continue;

					GrfImage image = sourceImage.Copy();

					if (image.Pixels == null)
						continue;

					_remapImagePixels(image.Pixels, remap);
					act.Commands.SpriteReplaceAt(i, image);
				}

				act.Commands.SpriteSetPalette(sortedPalette);
			}
			catch (Exception err) {
				if (commandsStarted)
					act.Commands.CancelEdit();

				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				if (commandsStarted)
					act.Commands.End();

				act.InvalidateVisual();
				act.InvalidatePaletteVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && act.Sprite != null && act.Sprite.Palette != null && _getPalette(act) != null && act.Sprite.NumberOfIndexed8Images > 0;
		}

		private byte[] _getPalette(Act act) {
			if (act.Sprite.Palette.BytePalette != null && act.Sprite.Palette.BytePalette.Length >= 1024)
				return act.Sprite.Palette.BytePalette;

			for (int i = 0; i < act.Sprite.NumberOfIndexed8Images; i++) {
				GrfImage image = act.Sprite.Images[i];

				if (image != null && image.Palette != null && image.Palette.Length >= 1024)
					return image.Palette;
			}

			return null;
		}

		private bool[] _getUsedPaletteIndexes(Act act) {
			bool[] used = new bool[256];

			for (int i = 0; i < act.Sprite.NumberOfIndexed8Images; i++) {
				GrfImage image = act.Sprite.Images[i];

				if (image == null || image.Pixels == null)
					continue;

				byte[] pixels = image.Pixels;

				for (int k = 0; k < pixels.Length; k++) {
					used[pixels[k]] = true;
				}
			}

			return used;
		}

		private int _getToneGroup(byte[] palette, int index) {
			int offset = index * 4;
			byte r = palette[offset + 0];
			byte g = palette[offset + 1];
			byte b = palette[offset + 2];
			double saturation = _getSaturation(r, g, b);

			if (saturation < 0.04d)
				return _getValue(r, g, b) > 220d ? 0 : 8;

			double hue = _getHue(r, g, b);

			if (hue >= 45d && hue < 72d)
				return 1; // yellow
			if (hue >= 20d && hue < 45d)
				return 2; // orange/brown
			if (hue < 20d || hue >= 345d)
				return 3; // red
			if (hue >= 72d && hue < 170d)
				return 4; // green
			if (hue >= 170d && hue < 205d)
				return 5; // cyan
			if (hue >= 205d && hue < 260d)
				return 6; // blue
			if (hue >= 260d && hue < 320d)
				return 7; // purple

			return 3;
		}

		private int _compareByValue(byte[] palette, int left, int right) {
			double valueLeft = _getValue(palette, left);
			double valueRight = _getValue(palette, right);
			int result = valueRight.CompareTo(valueLeft);

			if (result != 0)
				return result;

			result = _getHue(palette, left).CompareTo(_getHue(palette, right));

			if (result != 0)
				return result;

			return left.CompareTo(right);
		}

		private double _getValue(byte[] palette, int index) {
			int offset = index * 4;
			return _getValue(palette[offset + 0], palette[offset + 1], palette[offset + 2]);
		}

		private double _getValue(byte r, byte g, byte b) {
			double rl = _linearize(r / 255d);
			double gl = _linearize(g / 255d);
			double bl = _linearize(b / 255d);

			return (0.2126d * rl + 0.7152d * gl + 0.0722d * bl) * 255d;
		}

		private double _linearize(double value) {
			return value <= 0.04045d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
		}

		private double _getHue(byte[] palette, int index) {
			int offset = index * 4;
			return _getHue(palette[offset + 0], palette[offset + 1], palette[offset + 2]);
		}

		private double _getHue(byte r, byte g, byte b) {
			double rd = r / 255d;
			double gd = g / 255d;
			double bd = b / 255d;
			double max = Math.Max(rd, Math.Max(gd, bd));
			double min = Math.Min(rd, Math.Min(gd, bd));
			double delta = max - min;

			if (delta == 0d)
				return 0d;

			double hue;

			if (max == rd)
				hue = 60d * (((gd - bd) / delta) % 6d);
			else if (max == gd)
				hue = 60d * (((bd - rd) / delta) + 2d);
			else
				hue = 60d * (((rd - gd) / delta) + 4d);

			return hue < 0d ? hue + 360d : hue;
		}

		private double _getSaturation(byte r, byte g, byte b) {
			double rd = r / 255d;
			double gd = g / 255d;
			double bd = b / 255d;
			double max = Math.Max(rd, Math.Max(gd, bd));
			double min = Math.Min(rd, Math.Min(gd, bd));

			return max == 0d ? 0d : (max - min) / max;
		}

		private bool _isTransparentMarker(byte[] palette, int index) {
			int offset = index * 4;
			byte r = palette[offset + 0];
			byte g = palette[offset + 1];
			byte b = palette[offset + 2];
			byte a = palette[offset + 3];

			if (a == 0)
				return true;

			if (r == 255 && g == 0 && b == 255)
				return true;

			// Some sprites carry the transparency marker as a near-magenta value
			// after palette edits. Treat those as empty too so they never enter
			// the sorted color ramps.
			if (r >= 240 && g <= 45 && b >= 240)
				return true;

			return r >= 210 && g <= 80 && b >= 210 && _getSaturation(r, g, b) >= 0.65d;
		}

		private void _markAllAsTransparent(byte[] palette) {
			for (int i = 1; i < 256; i++) {
				int offset = i * 4;
				palette[offset + 0] = 255;
				palette[offset + 1] = 0;
				palette[offset + 2] = 255;
				palette[offset + 3] = 255;
			}
		}

		private bool _byteArrayCompare(byte[] left, byte[] right) {
			if (left == null || right == null || left.Length != right.Length)
				return false;

			for (int i = 0; i < left.Length; i++) {
				if (left[i] != right[i])
					return false;
			}

			return true;
		}

		private byte[] _copy(byte[] bytes) {
			byte[] copy = new byte[bytes.Length];
			Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
			return copy;
		}

		private bool _isIdentity(byte[] remap) {
			for (int i = 0; i < remap.Length; i++) {
				if (remap[i] != i)
					return false;
			}

			return true;
		}

		private void _remapImagePixels(byte[] pixels, byte[] remap) {
			for (int i = 0; i < pixels.Length; i++) {
				pixels[i] = remap[pixels[i]];
			}
		}
	}
}
