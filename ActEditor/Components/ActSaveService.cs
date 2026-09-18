using ActEditor.ApplicationConfiguration;
using ActEditor.Core;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.GrfSystem;
using GRF.Image;
using GrfToWpfBridge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles;
using Utilities;
using Utilities.Extension;
using Imaging = ActImaging.Imaging;

namespace ActEditor.Components {
	public class ActSaveService {
		public class SaveFormat {
			public string Name;
			public string Filter;
			public SaveMode Mode;
			public string RequiredExtension;
			public string AnyExtension;

			public string[] GetRequiredExtensions() {
				return RequiredExtension.Replace("*", "").Split(';');
			}

			public string[] GetAnyExtensions() {
				return AnyExtension.Replace("*", "").Split(';');
			}
		}

		public class SaveContext {
			public TabAct Tab;
			public string FilePath;
			public SaveMode Mode;
		}

		public class SaveResult {
			public string NewFilePath;
			public bool AddToRecentFiles;
			public bool IsNewCleared;
		}

		public enum SaveMode {
			ActAndSpr,
			ActSprPal,
			ActOnly,
			PaletteOnly,
			SpriteOnly,
			Gif,
			AnimatedPng,
			Image
		}

		public string ResolveInitialPath(TabAct tab) {
			var fileName = ActEditorConfiguration.AppLastPath;

			if (Path.GetFileNameWithoutExtension(fileName) != Path.GetFileNameWithoutExtension(tab.Act.LoadedPath)) {
				fileName = tab.Act.LoadedPath;
			}

			return fileName;
		}

		private List<SaveFormat> _saveFormats = new List<SaveFormat> {
			new SaveFormat { Name = "Act and Spr files", Filter = "*.act;*.spr", Mode = SaveMode.ActAndSpr, RequiredExtension = "*.act;*.spr" },
			new SaveFormat { Name = "Act, Spr and Pal files", Filter = "*.act;*.spr;*.pal", Mode = SaveMode.ActSprPal, RequiredExtension = "*.act" },
			new SaveFormat { Name = "Animation files", Filter = "*.act", Mode = SaveMode.ActOnly, RequiredExtension = "*.act", AnyExtension = "*.act" },
			new SaveFormat { Name = "Palette files", Filter = "*.pal", Mode = SaveMode.PaletteOnly, RequiredExtension = "*.pal", AnyExtension = "*.pal" },
			new SaveFormat { Name = "Sprite files", Filter = "*.spr", Mode = SaveMode.SpriteOnly, RequiredExtension = "*.spr", AnyExtension = "*.spr" },
			new SaveFormat { Name = "Gif files", Filter = "*.gif", Mode = SaveMode.Gif, RequiredExtension = "*.gif", AnyExtension = "*.gif" },
			new SaveFormat { Name = "Animated PNG files", Filter = "*.png;*.apng", Mode = SaveMode.AnimatedPng, RequiredExtension = "*.png;*.apng", AnyExtension = "*.apng" },
			new SaveFormat { Name = "Image files", Filter = "*.bmp;*.png;*.jpg;*.tga", Mode = SaveMode.Image, RequiredExtension = "*.bmp;*.png;*.jpg;*.tga", AnyExtension = "*.bmp;*.png;*.jpg;*.tga" },
		};

		public SaveContext CreateSaveContext(TabAct tab) {
			if (tab.Act == null)
				return null;

			string fileName = ResolveInitialPath(tab);

			string file = TkPathRequest.SaveFile<ActEditorConfiguration>("AppLastPath",
				"fileName", fileName,
				"filter", Methods.Aggregate(_saveFormats.Select(p => p.Name + "|" + p.Filter).ToList(), "|"));
			if (file == null) return null;

			var dialog = TkPathRequest.LatestSaveFileDialog;

			return new SaveContext {
				Tab = tab,
				FilePath = file,
				Mode = ResolveSaveMode(file, dialog.FilterIndex - 1)
			};
		}

		public SaveMode ResolveSaveMode(string file, int filterIndex) {
			if (filterIndex < 0 || filterIndex >= _saveFormats.Count) {
				throw new Exception("Unable to find a matching save mode.");
			}

			var format = _saveFormats[filterIndex];

			if (file.IsExtension(format.GetRequiredExtensions())) {
				return format.Mode;
			}

			// If not a direct match, fallback to extension rather than the selected filter index
			for (int i = 0; i < _saveFormats.Count; i++) {
				format = _saveFormats[i];

				if (string.IsNullOrEmpty(format.AnyExtension))
					continue;
				if (file.IsExtension(format.GetAnyExtensions()))
					return format.Mode;
			}

			throw new Exception("File extension does not match the save mode.");
		}

		public SaveResult ExecuteSave(SaveContext sc) {
			switch (sc.Mode) {
				case SaveMode.ActAndSpr:
					return _saveActAndSpr(sc);
				case SaveMode.ActSprPal:
					return _saveActSprPal(sc);
				case SaveMode.ActOnly:
					return _saveActOnly(sc);
				case SaveMode.PaletteOnly:
					return _savePalOnly(sc);
				case SaveMode.SpriteOnly:
					return _saveSprOnly(sc);
				case SaveMode.Gif:
					return _saveGif(sc);
				case SaveMode.AnimatedPng:
					return _saveAnimatedPng(sc);
				case SaveMode.Image:
					return _saveImage(sc);
				default:
					throw new InvalidOperationException("Unknown save mode.");
			}
		}

		private SaveResult _saveActAndSpr(SaveContext sc) {
			var act = sc.Tab.Act;
			var actPath = sc.FilePath.ReplaceExtension(".act");

			act.SaveWithSprite(actPath);
			SpriteSaveCompatibility.NormalizePaletteTail(actPath.ReplaceExtension(".spr"));
			act.LoadedPath = actPath;
			act.Commands.SaveCommandIndex();

			return new SaveResult {
				AddToRecentFiles = true,
				IsNewCleared = true,
				NewFilePath = actPath,
			};
		}

		private SaveResult _saveActSprPal(SaveContext sc) {
			var act = sc.Tab.Act;
			var actPath = sc.FilePath.ReplaceExtension(".act");

			act.SaveWithSprite(actPath);
			SpriteSaveCompatibility.NormalizePaletteTail(actPath.ReplaceExtension(".spr"));
			File.WriteAllBytes(actPath.ReplaceExtension(".pal"), act.Sprite.Palette.BytePalette);
			act.LoadedPath = actPath;
			act.Commands.SaveCommandIndex();

			return new SaveResult {
				AddToRecentFiles = true,
				IsNewCleared = true,
				NewFilePath = actPath,
			};
		}

		private SaveResult _saveActOnly(SaveContext sc) {
			var act = sc.Tab.Act;
			var actPath = sc.FilePath.ReplaceExtension(".act");

			act.Save(actPath);

			return new SaveResult {
				AddToRecentFiles = true,
				NewFilePath = actPath,
			};
		}

		private SaveResult _savePalOnly(SaveContext sc) {
			var act = sc.Tab.Act;
			File.WriteAllBytes(sc.FilePath, act.Sprite.Palette.BytePalette);

			return new SaveResult();
		}

		private SaveResult _saveSprOnly(SaveContext sc) {
			var act = sc.Tab.Act;
			var sprPath = sc.FilePath.ReplaceExtension(".spr");
			act.Sprite.Save(sprPath);
			SpriteSaveCompatibility.NormalizePaletteTail(sprPath);

			return new SaveResult();
		}

		private SaveResult _saveGif(SaveContext sc) {
			return _saveAnimation(sc, false);
		}

		private SaveResult _saveAnimatedPng(SaveContext sc) {
			return _saveAnimation(sc, true);
		}

		private SaveResult _saveAnimation(SaveContext sc, bool animatedPng) {
			var act = sc.Tab.Act;
			var tab = sc.Tab;

			for (int i = 0; i < act.Sprite.NumberOfIndexed8Images; i++) {
				act.Sprite.Images[i].Palette[3] = 0;
			}

			GifSavingDialog dialog = new GifSavingDialog(act, tab.SelectedAction, animatedPng);
			dialog.Owner = WpfUtilities.TopWindow;

			if (ActEditorConfiguration.ActEditorGifHideDialog || dialog.ShowDialog() == true) {
				TaskDialog task = new TaskDialog(animatedPng ? "Saving as animated PNG..." : "Saving as gif...", "app.ico", "Processing frames, please wait...");
				task.ShowFooter(true);
				task.Start(isCancelling => {
					try {
						List<Act> back = new List<Act>();
						List<Act> front = new List<Act>();

						foreach (var reference in tab.References.Where(reference => reference.ShowReference)) {
							if (reference.Mode == ZMode.Back)
								back.Insert(0, reference.Act);
							else
								front.Add(reference.Act);
						}

						var extra = dialog.Dispatch(() => dialog.Extra);
						bool preservePartialAlpha = animatedPng && GetAnimationOption(extra, "preservePartialAlpha", false);
						var exportAct = MergeGifActs(back, act, front, preservePartialAlpha);
						RemoveMissingGifLayers(exportAct);
						if (animatedPng)
							ApngEncoder.Save(sc.FilePath, exportAct, tab.SelectedAction, task, extra);
						else
							Imaging.SaveAsGif(sc.FilePath, exportAct, tab.SelectedAction, task, extra);
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
				}, () => task.Progress);
				task.ShowDialog();
			}

			return new SaveResult();
		}

		private static bool GetAnimationOption(string[] extra, string name, bool defaultValue) {
			for (int i = 0; i + 1 < extra.Length; i += 2) {
				if (extra[i] == name)
					return Boolean.Parse(extra[i + 1]);
			}

			return defaultValue;
		}

		internal static Act MergeGifActs(IEnumerable<Act> back, Act primary, IEnumerable<Act> front) {
			return MergeGifActs(back, primary, front, false);
		}

		internal static Act MergeGifActs(IEnumerable<Act> back, Act primary, IEnumerable<Act> front, bool preservePartialAlpha) {
			var output = new Act(primary);

			foreach (var reference in back) {
				output = MergeGifReference(output, reference, false, preservePartialAlpha);
			}

			foreach (var reference in front) {
				output = MergeGifReference(output, reference, true, preservePartialAlpha);
			}

			if (!preservePartialAlpha) {
				foreach (var image in output.Sprite.Images.Where(image => image.GrfImageType == GrfImageType.Bgra32))
					RemovePartialGifTransparency(image);
			}

			return output;
		}

		private static Act MergeGifReference(Act current, Act reference, bool front, bool preservePartialAlpha) {
			reference = PrepareGifReference(reference, preservePartialAlpha);
			int indexedImagesBeforeMerge = current.Sprite.NumberOfIndexed8Images;
			var layerCounts = current.GetAllFrames().Select(frame => frame.NumberOfLayers).ToList();
			var merged = front
				? Act.MergeAct(new Act[0], current, new[] { reference })
				: Act.MergeAct(new[] { reference }, current, new Act[0]);

			// Act.MergeAct uses the total image count as the BGRA offset. BGRA indexes are
			// relative to the BGRA section, so remove the indexed-image portion again.
			int frameIndex = 0;
			foreach (var frame in merged.GetAllFrames()) {
				int previousLayerCount = layerCounts[frameIndex++];
				int addedLayerCount = frame.NumberOfLayers - previousLayerCount;
				int firstAddedLayer = front ? previousLayerCount : 0;

				for (int i = firstAddedLayer; i < firstAddedLayer + addedLayerCount; i++) {
					var layer = frame.Layers[i];
					if (layer.IsBgra32())
						layer.SpriteIndex -= indexedImagesBeforeMerge;
				}
			}

			return merged;
		}

		private static Act PrepareGifReference(Act reference) {
			return PrepareGifReference(reference, false);
		}

		private static Act PrepareGifReference(Act reference, bool preservePartialAlpha) {
			var prepared = new Act(reference);
			var bgraIndexes = new Dictionary<int, SpriteIndex>();

			// A SPR has one shared Indexed8 palette. References are rendered with their
			// own palettes in the editor, so bake those colors into BGRA before merging.
			for (int i = 0; i < prepared.Sprite.NumberOfIndexed8Images; i++) {
				var image = prepared.Sprite.GetImage(i, GrfImageType.Indexed8);
				if (image == null)
					continue;

				var bgraImage = image.Copy();
				bgraImage.Convert(GrfImageType.Bgra32);
				bgraIndexes[i] = prepared.Sprite.InsertAny(bgraImage);
			}

			foreach (var layer in prepared.GetAllLayers()) {
				SpriteIndex bgraIndex;
				if (layer.IsIndexed8() && bgraIndexes.TryGetValue(layer.SpriteIndex, out bgraIndex)) {
					layer.SprSpriteIndex = bgraIndex;
				}
			}

			if (!preservePartialAlpha) {
				foreach (var image in prepared.Sprite.Images.Where(image => image.GrfImageType == GrfImageType.Bgra32)) {
					RemovePartialGifTransparency(image);
				}
			}

			return prepared;
		}

		internal static void RemovePartialGifTransparency(GrfImage image) {
			// GIF cannot represent partial alpha. Dropping those pixels avoids both
			// a white matte and the visible dot pattern produced by dithering.
			for (int alphaIndex = 3; alphaIndex < image.Pixels.Length; alphaIndex += 4) {
				byte alpha = image.Pixels[alphaIndex];
				if (alpha != 0 && alpha != 255)
					image.Pixels[alphaIndex] = 0;
			}
		}

		internal static void RemoveMissingGifLayers(Act exportAct) {
			// Match the viewport: layers without a sprite image are not drawable.
			// Only modify the merged export copy; retain empty frames and their timing.
			foreach (var frame in exportAct.GetAllFrames()) {
				frame.Layers.RemoveAll(layer => layer == null || exportAct.Sprite.GetImage(layer) == null);
			}
		}

		private SaveResult _saveImage(SaveContext sc) {
			var act = sc.Tab.Act;
			var tab = sc.Tab;

			var imgSource = Imaging.GenerateImage(act, tab.SelectedAction, tab.SelectedFrame);
			PngBitmapEncoder encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(Imaging.ForceRender(imgSource, BitmapScalingMode.NearestNeighbor)));

			using (MemoryStream stream = new MemoryStream()) {
				encoder.Save(stream);

				byte[] data = new byte[stream.Length];
				stream.Seek(0, SeekOrigin.Begin);
				stream.Read(data, 0, data.Length);

				GrfImage grfImage = new GrfImage(data);
				grfImage.Save(sc.FilePath);
			}

			return new SaveResult();
		}

		private bool _isInGrf(Act act) {
			TkPath path = new TkPath(act.LoadedPath);

			return !string.IsNullOrEmpty(path.RelativePath);
		}

		public SaveResult Save(Act act) {
			SaveResult result;

			if (_isInGrf(act)) {
				result = _saveToGrf(act);
			}
			else {
				result = _saveToFileSystem(act);
			}

			if (act.Sprite.RleSaveError) {
				if (ActEditorConfiguration.ShowErrorRleDowngrade) {
					try {
						WindowProvider.WindowOpened += _errorWindowOpened;
						ErrorHandler.HandleException("Some of your sprite image sizes are too large and will not load properly. The SPR file format has been downgraded to 0x200 to avoid losing data, however these sprites will not load properly ingame.\r\n\r\nTo fix this issue, reduce your sprite image sizes. You can also convert your images as Bgra32 instead as this format has no size restriction.");
					}
					finally {
						WindowProvider.WindowOpened -= _errorWindowOpened;
					}
				}
			}

			return new SaveResult {
				IsNewCleared = true,
			};
		}

		private SaveResult _saveToGrf(Act act) {
			TkPath path = new TkPath(act.LoadedPath);

			if (Methods.IsFileLocked(path.FilePath)) {
				throw new Exception("The file " + path.FilePath + " is locked by another process. Try closing other GRF applicactions or use the 'Save as...' option.");
			}

			using (GrfHolder grf = new GrfHolder(path.FilePath)) {
				string temp = TemporaryFilesManager.GetTemporaryFilePath("to_grf_{0:0000}");

				act.Sprite.Save(temp + ".spr");
				SpriteSaveCompatibility.NormalizePaletteTail(temp + ".spr");
				act.Save(temp + ".act");

				grf.Commands.AddFile(path.RelativePath.ReplaceExtension(".act"), File.ReadAllBytes(temp + ".act"));
				grf.Commands.AddFile(path.RelativePath.ReplaceExtension(".spr"), File.ReadAllBytes(temp + ".spr"));

				grf.Save();

				grf.ProcessSaveResult();
			}

			return new SaveResult {
				IsNewCleared = true,
			};
		}

		private SaveResult _saveToFileSystem(Act act) {
			var sprPath = act.LoadedPath.ReplaceExtension(".spr");
			act.Sprite.Save(sprPath);
			SpriteSaveCompatibility.NormalizePaletteTail(sprPath);
			act.Save();
			act.Commands.SaveCommandIndex();

			return new SaveResult {
				IsNewCleared = true,
			};
		}

		private void _errorWindowOpened(TkWindow window) {
			ErrorDialog dialog = window as ErrorDialog;

			if (dialog == null)
				return;

			dialog.Loaded += delegate {
				var grid = (Grid)dialog.Content;
				var footer = (Grid)((Grid)grid.Children[grid.Children.Count - 1]).Children[0];
				var cb = new CheckBox { Content = "Do not show again", Margin = new Thickness(3) };
				WpfUtilities.AddMouseInOutUnderline(cb);
				cb.SetValue(Grid.ColumnProperty, 1);
				cb.HorizontalAlignment = HorizontalAlignment.Left;
				cb.VerticalAlignment = VerticalAlignment.Center;
				footer.Children.Add(cb);

				Binder.Bind(cb, () => !ActEditorConfiguration.ShowErrorRleDowngrade, v => ActEditorConfiguration.ShowErrorRleDowngrade = !v, null, true);
			};
		}
	}
}
