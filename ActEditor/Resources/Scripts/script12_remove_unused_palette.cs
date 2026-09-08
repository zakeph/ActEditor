using System;
using System.Collections.Generic;
using GRF.FileFormats.ActFormat;
using GRF.Image;

namespace Scripts {
    public class Script : IActScript {
        public object DisplayName {
            get { return "Remove unused palette colors"; }
        }

        public string Group {
            get { return "Scripts"; }
        }

        public string InputGesture {
			get { return "{Scripts.RemoveUnusedPaletteColors}"; }
        }

        public string Image {
            get { return "delete.png"; }
        }

        public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
            if (act == null || act.Sprite == null || act.Sprite.Palette == null || act.Sprite.Palette.BytePalette == null)
                return;

            HashSet<byte> unusedIndexes = act.Sprite.GetUnusedPaletteIndexes();
			byte[] palette = _copy(act.Sprite.Palette.BytePalette);
			
            for (int i = 1; i < 256; i++) {
                if (unusedIndexes.Contains((byte)i)) {
                    palette[4 * i + 0] = 255;
                    palette[4 * i + 1] = 0;
                    palette[4 * i + 2] = 255;
                    palette[4 * i + 3] = 255;
                }
            }
			
			act.Commands.SpriteSetPalette(palette);
        }

        public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
            return act != null && act.Sprite != null && act.Sprite.Palette != null && act.Sprite.Palette.BytePalette != null;
        }

		private byte[] _copy(byte[] bytes) {
			if (bytes == null)
				return null;

			byte[] copy = new byte[bytes.Length];
			Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
			return copy;
		}
    }
}
