using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Linq;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.Paths;
using Utilities.Services;

namespace ActEditor.Core.Scripting.Scripts {
	public class SpriteExportNormal : IActScript {
		public SpriteExportNormal() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		#region IActScript Members

		public object DisplayName {
			get { return "__IndexOverride,12__%Export all sprites..."; }
		}

		public string Group {
			get { return "File"; }
		}

		public string InputGesture {
			get { return "{ActEditor.SpriteExport}"; }
		}

		public string Image {
			get { return "export.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				string path = TkPathRequest.Folder<ActEditorConfiguration>("ExtractingServiceLastPath");

				if (path != null) {
					string name = "image_{0:0000}";

					int i = 0;
					int numberOfImages = act.Sprite.NumberOfImagesLoaded;

					TaskManager.DisplayTaskC("Export", "Exporting sprites...", () => i, numberOfImages, isCancelling => {
						int count = act.Sprite.NumberOfImagesLoaded;

						for (; i < count; i++) {
							var im = act.Sprite.Images[i].Copy();

							if (im.GrfImageType == GrfImageType.Indexed8) {
								im.Save(GrfPath.Combine(path, String.Format(name, i) + ".bmp"));
							}
							else {
								im.Save(GrfPath.Combine(path, String.Format(name, i) + ".png"));
							}
						}
					});

					OpeningService.FileOrFolder(path);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		#endregion
	}

	public class SpriteExport : IActScript {
		public SpriteExport() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		#region IActScript Members

		public object DisplayName {
			get { return "__IndexOverride,14__%Export all sprites (adv)..."; }
		}

		public string Group {
			get { return "File"; }
		}

		public string InputGesture {
			get { return "{ActEditor.SpriteExportAdvanced}"; }
		}

		public string Image {
			get { return "export.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			var dialog = new ExportSpriteDialog(ActEditorWindow.Instance);
			dialog.Owner = WpfUtilities.TopWindow;
			dialog.Show();
			IsEnabled = false;
			dialog.Closed += delegate { 
				IsEnabled = true;
				dialog.Owner.Focus();
			};
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return IsEnabled;
		}

		#endregion
	}

	public class ItemSpriteExport : IActScript {
		private const int _itemSize = 24;
		private const int _backgroundColor = 0xff00ff;
		private const int _collectionWidth = 75;
		private const int _collectionHeight = 100;
		private const int _collectionMargin = 12;
		private const int _collectionBackgroundColor = 0xffffff;
		private const string _collectionTemplateResource = "pack://application:,,,/Resources/Nielily_collection.png";

		public ItemSpriteExport() {
			IsEnabled = true;
		}

		public bool IsEnabled { get; set; }

		public object DisplayName {
			get { return "__IndexOverride,13__%Bulk export item sprites..."; }
		}

		public string Group {
			get { return "File"; }
		}

		public string InputGesture {
			get { return null; }
		}

		public string Image {
			get { return "export.png"; }
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			var tabs = ActEditorWindow.Instance?.TabEngine.GetTabs()
				.Where(tab => tab.Act != null && tab.Act.Sprite.NumberOfImagesLoaded > 0)
				.ToList();

			if (tabs == null || tabs.Count == 0)
				return;

			try {
				string folder = TkPathRequest.Folder<ActEditorConfiguration>("ExtractingServiceLastPath");

				if (folder == null)
					return;

				string spriteFolder = GrfPath.Combine(folder, "sprite", "¾ÆÀÌÅÛ");
				string textureFolder = GrfPath.Combine(folder, "texture", "À¯ÀúÀÎÅÍÆäÀÌ½º", "item");
				string collectionFolder = GrfPath.Combine(folder, "texture", "À¯ÀúÀÎÅÍÆäÀÌ½º", "collection");
				Directory.CreateDirectory(spriteFolder);
				Directory.CreateDirectory(textureFolder);
				Directory.CreateDirectory(collectionFolder);

				int current = 0;
				var exportedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				TaskManager.DisplayTaskC("Bulk export", "Exporting item sprites...", () => current, tabs.Count, isCancelling => {
					foreach (var tab in tabs) {
						if (isCancelling())
							break;

						string name = Path.GetFileNameWithoutExtension(tab.Act.LoadedPath);
						if (String.IsNullOrEmpty(name))
							name = "item_" + (current + 1).ToString("0000");
						else
							name = RemoveGenderPrefix(name);

						string baseName = name;
						int duplicateIndex = 2;
						while (!exportedNames.Add(name))
							name = baseName + "_" + duplicateIndex++;

						CreateItemAssets(tab.Act.Sprite.Images[0], GrfPath.Combine(textureFolder, name + ".bmp"), GrfPath.Combine(spriteFolder, name), GrfPath.Combine(collectionFolder, name + ".bmp"));
						current++;
					}
				});
				OpeningService.FileOrFolder(folder);
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return ActEditorWindow.Instance?.TabEngine.GetTabs().Any(tab => tab.Act != null && tab.Act.Sprite.NumberOfImagesLoaded > 0) == true;
		}

		private static void CreateItemAssets(GrfImage sprite, string bitmapPath, string spritePath, string collectionBitmapPath) {
			GrfImage source = sprite.Copy();
			byte[] pixels = new byte[_itemSize * _itemSize];
			byte[] palette = new byte[256 * 4];
			Dictionary<int, byte> colorIndexes = new Dictionary<int, byte>();
			bool[] usedIndexes = new bool[256];

			palette[0] = (byte)(_backgroundColor & 0xff);
			palette[1] = (byte)(_backgroundColor >> 8 & 0xff);
			palette[2] = (byte)(_backgroundColor >> 16 & 0xff);
			usedIndexes[0] = true;
			colorIndexes[_backgroundColor] = 0;

			for (int y = 0; y < _itemSize; y++) {
				int sourceY = y * source.Height / _itemSize;

				for (int x = 0; x < _itemSize; x++) {
					int sourceX = x * source.Width / _itemSize;
					int color;

					if (!TryGetSpriteColor(source, sourceX, sourceY, out color))
						continue;

					pixels[y * _itemSize + x] = GetPaletteIndex(color, palette, colorIndexes, usedIndexes);
				}
			}

			GrfImage itemImage = new GrfImage(pixels, _itemSize, _itemSize, GrfImageType.Indexed8, palette);
			itemImage.Save(bitmapPath);

			Spr itemSprite = new Spr();
			SpriteIndex spriteIndex = itemSprite.InsertAny(itemImage);
			itemSprite.Save(spritePath + ".spr");

			Act itemAct = new Act(itemSprite);
			GRF.FileFormats.ActFormat.Action action = new GRF.FileFormats.ActFormat.Action();
			Frame frame = new Frame();
			frame.Layers.Add(new Layer(spriteIndex));
			action.Frames.Add(frame);
			itemAct.Actions.Add(action);
			itemAct.Save(spritePath + ".act");

			CreateCollectionBitmap(sprite, collectionBitmapPath);
		}

		private static string RemoveGenderPrefix(string name) {
			string[] prefixes = { "¿©_", "³²_" };

			foreach (string prefix in prefixes) {
				if (name.StartsWith(prefix, StringComparison.Ordinal))
					return name.Substring(prefix.Length);
			}

			return name;
		}

		private static void CreateCollectionBitmap(GrfImage sprite, string outputPath) {
			GrfImage source = sprite.Copy();
			byte[] pixels = new byte[_collectionWidth * _collectionHeight];
			byte[] palette = new byte[256 * 4];
			Dictionary<int, byte> colorIndexes = new Dictionary<int, byte>();
			bool[] usedIndexes = new bool[256];
			palette[0] = (byte)(_collectionBackgroundColor & 0xff);
			palette[1] = (byte)(_collectionBackgroundColor >> 8 & 0xff);
			palette[2] = (byte)(_collectionBackgroundColor >> 16 & 0xff);
			usedIndexes[0] = true;
			colorIndexes[_collectionBackgroundColor] = 0;
			DrawCollectionTemplate(LoadCollectionTemplate(), pixels, palette, colorIndexes, usedIndexes);

			int availableWidth = _collectionWidth - _collectionMargin * 2;
			int availableHeight = _collectionHeight - _collectionMargin * 2;
			double scale = Math.Min((double)availableWidth / source.Width, (double)availableHeight / source.Height);
			int width = Math.Max(1, (int)Math.Round(source.Width * scale));
			int height = Math.Max(1, (int)Math.Round(source.Height * scale));
			int offsetX = (_collectionWidth - width) / 2;
			int offsetY = (_collectionHeight - height) / 2;

			for (int y = 0; y < height; y++) {
				int sourceY = y * source.Height / height;

				for (int x = 0; x < width; x++) {
					int sourceX = x * source.Width / width;
					int color;

					if (!TryGetSpriteColor(source, sourceX, sourceY, out color))
						continue;

					pixels[(offsetY + y) * _collectionWidth + offsetX + x] = GetPaletteIndex(color, palette, colorIndexes, usedIndexes);
				}
			}

			new GrfImage(pixels, _collectionWidth, _collectionHeight, GrfImageType.Indexed8, palette).Save(outputPath);
		}

		private static GrfImage LoadCollectionTemplate() {
			var resource = Application.GetResourceStream(new Uri(_collectionTemplateResource, UriKind.Absolute));

			if (resource == null)
				throw new InvalidOperationException("The collection template could not be loaded.");

			using (resource.Stream)
			using (MemoryStream output = new MemoryStream()) {
				resource.Stream.CopyTo(output);
				return new GrfImage(output.ToArray());
			}
		}

		private static void DrawCollectionTemplate(GrfImage template, byte[] pixels, byte[] palette, Dictionary<int, byte> colorIndexes, bool[] usedIndexes) {
			if (template.Width != _collectionWidth || template.Height != _collectionHeight)
				throw new InvalidOperationException("The collection template must be 75x100 pixels.");

			for (int y = 0; y < _collectionHeight; y++) {
				for (int x = 0; x < _collectionWidth; x++) {
					int color;
					if (!TryGetSpriteColor(template, x, y, out color))
						continue;

					pixels[y * _collectionWidth + x] = GetPaletteIndex(color, palette, colorIndexes, usedIndexes);
				}
			}
		}

		private static bool TryGetSpriteColor(GrfImage source, int x, int y, out int color) {
			int offset = y * source.Width + x;

			if (source.GrfImageType == GrfImageType.Indexed8) {
				byte index = source.Pixels[offset];
				if (index == 0) {
					color = 0;
					return false;
				}

				color = GetPaletteColor(source.Palette, index);
				return true;
			}

			if (source.GrfImageType != GrfImageType.Bgra32)
				source.Convert(GrfImageType.Bgra32);

			offset *= 4;
			if (source.Pixels[offset + 3] == 0) {
				color = 0;
				return false;
			}

			// GrfImage stores BGRA32 pixels, while indexed palettes use RGBA.
			color = source.Pixels[offset + 2] | source.Pixels[offset + 1] << 8 | source.Pixels[offset] << 16;
			return true;
		}

		private static int GetPaletteColor(byte[] palette, byte index) {
			int offset = index * 4;
			return palette[offset] | palette[offset + 1] << 8 | palette[offset + 2] << 16;
		}

		private static byte GetPaletteIndex(int color, byte[] palette, Dictionary<int, byte> colorIndexes, bool[] usedIndexes) {
			byte index;
			if (colorIndexes.TryGetValue(color, out index))
				return index;

			for (int i = 0; i < usedIndexes.Length; i++) {
				if (usedIndexes[i])
					continue;

				usedIndexes[i] = true;
				palette[i * 4] = (byte)color;
				palette[i * 4 + 1] = (byte)(color >> 8);
				palette[i * 4 + 2] = (byte)(color >> 16);
				palette[i * 4 + 3] = 0;
				index = (byte)i;
				colorIndexes[color] = index;
				return index;
			}

			return FindClosestPaletteIndex(color, palette);
		}

		private static byte FindClosestPaletteIndex(int color, byte[] palette) {
			int blue = color & 0xff;
			int green = color >> 8 & 0xff;
			int red = color >> 16 & 0xff;
			int bestDistance = Int32.MaxValue;
			byte bestIndex = 0;

			for (int i = 0; i < 256; i++) {
				int paletteColor = GetPaletteColor(palette, (byte)i);
				int blueDifference = blue - (paletteColor & 0xff);
				int greenDifference = green - (paletteColor >> 8 & 0xff);
				int redDifference = red - (paletteColor >> 16 & 0xff);
				int distance = blueDifference * blueDifference + greenDifference * greenDifference + redDifference * redDifference;

				if (distance < bestDistance) {
					bestDistance = distance;
					bestIndex = (byte)i;
				}
			}

			return bestIndex;
		}
	}
}
