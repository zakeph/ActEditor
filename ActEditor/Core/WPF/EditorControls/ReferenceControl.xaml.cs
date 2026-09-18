using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ActEditor.Core.WPF.EditorControls.ActSelectorComponents;
using ActEditor.Tools.GrfShellExplorer;
using ActEditor.Tools.PaletteSheetGenerator;
using ErrorManager;
using GRF;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.PalFormat;
using GRF.FileFormats.SprFormat;
using GRF.IO;
using GrfToWpfBridge;
using GrfToWpfBridge.ActRenderer.ActSelectorComponents;
using TokeiLibrary;
using TokeiLibrary.Paths;
using TokeiLibrary.WPF;
using Utilities;
using Utilities.Extension;
using Utilities.Services;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for ReferenceControl.xaml
	/// </summary>
	public partial class ReferenceControl : UserControl {
		#region Delegates

		public delegate void ReferenceFrameEventHandler(object sender);

		#endregion

		private readonly TabAct _actEditor;
		private readonly string _defaultFemale;
		private readonly string _defaultMale;
		private List<SimpleButton> _fancyButtons;
		private LayerControl _layerControl;
		private readonly string _name;
		private string _filePath;
		private string _resourceActPath;
		private bool _initializingPalette;
		private bool _initializingStyle;
		private ZMode _mode;
		private bool _sex;
		private bool _directional;

		public string ActName {
			get {
				return _name;
			}
		}

		public ReferenceControl() {
			InitializeComponent();
		}

		public ReferenceControl(TabAct actEditor, string defaultMale, string defaultFemale, string name, bool directional) {
			_actEditor = actEditor;
			_defaultMale = defaultMale;
			_defaultFemale = defaultFemale;
			_name = name;
			_directional = directional;

			_mode = Int32.Parse(ActEditorConfiguration.ConfigAsker["[ActEditor - Mode - " + name + "]", "0"]) == 0 ? ZMode.Front : ZMode.Back;
			_sex = Boolean.Parse(ActEditorConfiguration.ConfigAsker["[ActEditor - Gender - " + name + "]", "true"]);
			_filePath = ActEditorConfiguration.ConfigAsker["[ActEditor - Path - " + name + "]", ""];
			_resourceActPath = ActEditorConfiguration.ConfigAsker["[ActEditor - Resource path - " + name + "]", ""];

			InitializeComponent();

			if (directional) {
				InitializeDirectionalArrows();
			}

			InitializeLayerAndHeaderComponent();

			// Restore the palette before the enabled-state binder creates the ACT/SPR.
			// Otherwise the saved ID is visible after startup, but the sprite keeps its
			// default palette until the user changes the combo box manually.
			if (name == "Head" || name == "Body") {
				InitializePaletteComponent();
			}

			if (name == "Head") {
				InitializeStyleComponent();
			}

			// Must be executed last because if enabled, it will read the properties above
			InitializeReferenceConfigComponents();

			_updateGenderButton();

			FilePathChanged += new ReferenceFrameEventHandler(_referenceFrame_FilePathChanged);

			_referenceFrame_FilePathChanged(null);

			if (name == "Head" || name == "Other" || name == "Garment" || name == "Body") {
				_buttonAnchor.Visibility = Visibility.Visible;
				_buttonAnchor.IsEnabled = true;

				_cbAnchor.Visibility = Visibility.Visible;
				_cbAnchor.IsEnabled = true;
			}

			if (name == "Body") {
				_buttonChange.Visibility = Visibility.Visible;
			}

			InitializeAnchorComponent();
			InitializeSpriteRetrieveComponent();
		}

		private void InitializePaletteComponent() {
			_initializingPalette = true;
			_palettePanel.Visibility = Visibility.Visible;
			_paletteId.ItemsSource = Enumerable.Range(0, 956);

			int paletteId;
			if (!Int32.TryParse(ActEditorConfiguration.ConfigAsker["[ActEditor - Palette - " + _name + "]", "0"], out paletteId))
				paletteId = 0;

			_paletteId.SelectedItem = Math.Max(0, Math.Min(955, paletteId));
			_initializingPalette = false;
		}

		private void InitializeStyleComponent() {
			_stylePanel.Visibility = Visibility.Visible;
			ReloadStyleChoices();
		}

		private void ReloadStyleChoices() {
			_initializingStyle = true;
			try {
				string genderFolder = _sex ? "¿©" : "³²";
				string folder = @"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\" + genderFolder + @"\";
				string encodedFolder = EncodingService.FromAnyToDisplayEncoding(folder);
				var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var container in _actEditor.ActEditor.MetaGrf.Containers.Values) {
					foreach (var entry in container.FileTable.EntriesInDirectory(encodedFolder, SearchOption.AllDirectories)) {
						string path = entry.RelativePath;
						if (path.IsExtension(".act") && _actEditor.ActEditor.MetaGrf.Exists(path.ReplaceExtension(".spr")))
							paths.Add(path);
					}
				}

				var choices = paths.Select(path => new HeadStyleChoice(path, GetHeadStyleName(path, genderFolder)))
					.OrderBy(choice => choice.SortId)
					.ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
					.ToList();
				choices.Insert(0, new HeadStyleChoice(null, "Default hairstyle"));
				_styleId.ItemsSource = choices;
				_styleId.SelectedItem = choices.FirstOrDefault(choice => String.Equals(choice.ActPath, _resourceActPath, StringComparison.OrdinalIgnoreCase));
				if (_styleId.SelectedItem == null && choices.Count > 0)
					_styleId.SelectedIndex = 0;
			}
			finally {
				_initializingStyle = false;
			}
		}

		private static string GetHeadStyleName(string path, string genderFolder) {
			string name = Path.GetFileNameWithoutExtension(path);
			string suffix = "_" + genderFolder;
			int suffixIndex = name.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
			if (suffixIndex >= 0)
				name = name.Remove(suffixIndex) + name.Substring(suffixIndex + suffix.Length);

			int id;
			return Int32.TryParse(name, out id) ? String.Format("Hairstyle #{0:000}", id) : name;
		}

		private void InitializeSpriteRetrieveComponent() {
			_buttonSprite.DragEnter += (s, e) => {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".act"))) {
						_buttonSprite.Background = new SolidColorBrush(Color.FromArgb(255, 138, 247, 160));
						e.Effects = DragDropEffects.All;
					}
				}
			};

			_buttonSprite.DragLeave += (s, e) => {
				_buttonSprite.Background = Brushes.Transparent;
			};

			_buttonSprite.Drop += (s, e) => {
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true)) {
					string[] files = e.Data.GetData(DataFormats.FileDrop, true) as string[];

					if (files != null && files.Length > 0 && files.Any(p => p.IsExtension(".act"))) {
						FilePath = files.First(p => p.IsExtension(".act"));
						_buttonSprite.Background = Brushes.Transparent;
						e.Handled = true;
					}
				}
			};
		}

		private void InitializeAnchorComponent() {
			_cbAnchor.DropDownOpened += delegate { _buttonAnchor.IsStatePressed = true; };

			_cbAnchor.DropDownClosed += delegate {
				_buttonAnchor.IsStatePressed = false;
				Keyboard.Focus(_actEditor._gridPrimary);
			};

			_cbAnchor.SelectionChanged += new SelectionChangedEventHandler(_cbAnchor_SelectionChanged);
			_cbAnchor_SelectionChanged(null, null);

			if (_name == "Nearby") {
				_buttonAnchor.Visibility = Visibility.Collapsed;
				_cbAnchor.Visibility = Visibility.Collapsed;
			}
		}

		private void InitializeLayerAndHeaderComponent() {
			_layerControl = new LayerControl(_actEditor, _name);
			_layerControl.IsEnabled = false;
			_sp.Children.Add(_layerControl);
			_header.HideIdAndSprite();

			if (_directional)
				_cdLayerControl.Width = new GridLength(371);
		}

		private void InitializeReferenceConfigComponents() {
			TextBlock tb = (TextBlock)_refZState.FindName("_tbIdentifier");
			tb.Margin = new Thickness(2);
			tb.FontSize = 12;
			tb.SetResourceReference(Control.ForegroundProperty, "TextForeground");

			Grid grid = ((Grid)((Grid)((Border)_refZState.FindName("_border")).Child).Children[2]);

			grid.HorizontalAlignment = HorizontalAlignment.Stretch;
			grid.Margin = new Thickness(0, 0, 2, 0);
			grid.ColumnDefinitions[0] = new ColumnDefinition();
			grid.ColumnDefinitions[1] = new ColumnDefinition { Width = new GridLength(-1, GridUnitType.Auto) };
			var child1 = grid.Children[0];
			var child2 = grid.Children[1];

			child1.SetValue(Grid.ColumnProperty, 1);
			child2.SetValue(Grid.ColumnProperty, 0);

			_refZState.Click += delegate {
				Mode = Mode == ZMode.Front ? ZMode.Back : ZMode.Front;
				UpdateRefZStateButtonImage();
			};

			UpdateRefZStateButtonImage();

			string enabledSettingName = "[ActEditor - IsEnabled - " + _name + "]";
			Binder.Bind(_cbRef, () => Boolean.Parse(ActEditorConfiguration.ConfigAsker[enabledSettingName, "false"]), v => ActEditorConfiguration.ConfigAsker[enabledSettingName] = v.ToString(), delegate {
				bool isEnabled = Boolean.Parse(ActEditorConfiguration.ConfigAsker[enabledSettingName]);

				_layerControl.IsEnabled = isEnabled;
				_rectangleVisibility.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;
				_rectangleVisibility.IsHitTestVisible = !isEnabled;

				if (isEnabled) {
					Update(true);
				}
				else {
					OnUpdated();
					_actEditor.OnReferencesChanged();
				}
			}, true);

			_cbRef.Content = _name;
			WpfUtilities.AddMouseInOutUnderline(_cbRef);
		}

		private void UpdateRefZStateButtonImage() {
			bool isFront = Mode == 0;

			_refZState.ImagePath = isFront ? "front.png" : "back.png";
			_refZState.TextHeader = isFront ? "Front" : "Back";
		}

		private void InitializeDirectionalArrows() {
			try {
				if (_directional) {
					_fancyButtons = new SimpleButton[] { _fancyButton0, _fancyButton1, _fancyButton2, _fancyButton3, _fancyButton4, _fancyButton5, _fancyButton6, _fancyButton7 }.ToList();

					ActIndexSelectorHelper.BuildDirectionalActionSelectorUI(_fancyButtons, true);

					_grid.Visibility = Visibility.Visible;
				}
			}
			catch {
			}
		}

		private void _updateGenderButton() {
			if (_directional) {
				_gender.Visibility = Visibility.Collapsed;
				return;
			}

			if (!String.IsNullOrEmpty(_filePath) || !String.IsNullOrEmpty(_resourceActPath)) {
				_gender.Visibility = Visibility.Collapsed;
			}
			else {	
				_gender.IsStatePressed = _sex;
				_gender.ImagePath = _sex ? "female.png" : "male.png";
			}
		}

		public bool ShowReference {
			get { return _cbRef.Dispatch(() => _cbRef.IsChecked == true); }
		}

		public ZMode Mode {
			get { return _mode; }
			set {
				if (_mode == value) return;

				_mode = value;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Mode - " + _name + "]"] = value == ZMode.Front ? "0" : "1";

				if (_mode == ZMode.Back) {
					_actEditor.References.Remove(this);
					_actEditor.References.Insert(0, this);
				}
				else {
					_actEditor.References.Remove(this);
					_actEditor.References.Add(this);
				}

				Update(false);
			}
		}

		public Act Act { get; set; }
		public Spr Spr { get; set; }

		public string FilePath {
			get { return _filePath; }
			set {
				_resourceActPath = null;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Resource path - " + _name + "]"] = "";
				_filePath = value;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Path - " + _name + "]"] = value;
				OnFilePathChanged();
				Update(true);
			}
		}

		public event ReferenceFrameEventHandler Updated;

		public void OnUpdated() {
			ReferenceFrameEventHandler handler = Updated;
			if (handler != null) handler(this);
		}

		public event ReferenceFrameEventHandler FilePathChanged;

		public void OnFilePathChanged() {
			ReferenceFrameEventHandler handler = FilePathChanged;
			if (handler != null) handler(this);
		}

		public void Init() {
			if (_name == "Head" || _name == "Other" || _name == "Garment" || _name == "Body") {
				if (_name == "Body")
					_cbAnchor.SelectedIndex = 3;

				if (_name == "Head") {
					int previousIndex = 3;

					_actEditor.References.First(p => p._name == "Body")._cbRef.Checked += delegate {
						previousIndex = _cbAnchor.SelectedIndex;
						_cbAnchor.SelectedIndex = 0;
					};
					_actEditor.References.First(p => p._name == "Body")._cbRef.Unchecked += delegate { _cbAnchor.SelectedIndex = previousIndex; };
				}

				_cbAnchor.SelectionChanged += (e, a) => {
					_frame_Updated(e);
					_actEditor.OnReferencesChanged();
				};

				Updated += _frame_Updated;
				//_actEditor.References.First(p => p.Frame._name == "Body").Frame.Updated += new ReferenceFrameEventHandler(_frame_Updated);
				_actEditor.References.First(p => p._name == "Nearby").Updated += new ReferenceFrameEventHandler(_frame_Updated);
				_actEditor.ActLoaded += e => {
					_frame_Updated(null);

					if (_actEditor.Act != null) {
						foreach (var reference in _actEditor.References) {
							if (reference.Act != null && reference.Act.Name == "Body") {
								if (ActEditorConfiguration.ReverseAnchor) {
									_actEditor.Act.AnchoredTo = reference.Act;
									reference.Act.AnchoredTo = null;
									break;
								}

								reference.RefreshSelection();
								break;
							}
						}
					}

					_actEditor._rendererPrimary.Update();
				};
			}

			_actEditor.OnReferencesChanged();
		}

		public void RefreshSelection() {
			_frame_Updated(null);
		}

		private void _frame_Updated(object sender) {
			ReferenceControl refCtr;
			int index = _cbAnchor.SelectedIndex;

			if (Act != null)
				Act.AnchoredTo = null;

			switch (_cbAnchor.SelectedIndex) {
				case 0:
				case 1:
				case 2:
					refCtr = _actEditor.References.First(p => p._name == (index == 0 ? "Body" : index == 1 ? "Other" : "Nearby"));
					if (Act != null && ShowReference && refCtr.ShowReference) {
						Act.AnchoredTo = refCtr.Act;
					}
					break;
				case 3:
					if (Act != null && ShowReference && _actEditor.Act != null) {
						Act.AnchoredTo = _actEditor.Act;
					}
					break;
			}

			if (ActEditorConfiguration.ReverseAnchor && _name == "Body" && _actEditor.Act != null && Act != null) {
				_actEditor.Act.AnchoredTo = Act;
				Act.AnchoredTo = null;
			}

			// There is always a render pass after a frame update or act loading event
		}

		public void Update(bool updateSprite) {
			if (updateSprite) {
				MakeAct(true);
				Act.Name = _name;
			}

			_layerControl.ReferenceSetAndUpdate(Act);
			OnUpdated();
			_actEditor.OnReferencesChanged();
		}

		public void Reset() {
			if (_cbAnchor.SelectedIndex == 3) {
				if (Act != null)
					Act.AnchoredTo = null;
			}
		}

		private void _cbAnchor_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			_cbAnchor.Items.Cast<ComboBoxItem>().ToList().ForEach(p => p.SetValue(FontWeightProperty, FontWeights.Normal));

			if (_cbAnchor.SelectedItem != null) {
				((ComboBoxItem) _cbAnchor.SelectedItem).SetValue(FontWeightProperty, FontWeights.Bold);
			}
		}

		private void _referenceFrame_FilePathChanged(object sender) {
			bool usesCustomSprite = !String.IsNullOrEmpty(FilePath) || !String.IsNullOrEmpty(_resourceActPath);
			_reset.Visibility = usesCustomSprite ? Visibility.Visible : Visibility.Hidden;

			if (!_directional)
				_gender.Visibility = usesCustomSprite ? Visibility.Collapsed : Visibility.Visible;
		}

		private void _buttonSprite_Click(object sender) {
			try {
				string fileName = ActEditorConfiguration.ExtractingServiceLastPath;

				if (FilePath != null && File.Exists(FilePath)) {
					fileName = FilePath;
				}

				string file = TkPathRequest.OpenFile<ActEditorConfiguration>("ExtractingServiceLastPath", "fileName", fileName, "filter", "Act or Container Files|*.act;*.grf;*.rgz;*.gpf;*.thor|Act Files|*.act|Container Files|*.grf;*.rgz;*.gpf;*.thor");

				if (file != null) {
					if (file.IsExtension(".grf", ".rgz", ".gpf", ".thor")) {
						GrfExplorer explorer = new GrfExplorer(file, SelectMode.Act);
						if (explorer.ShowDialog() == true) {
							file = file + "?" + explorer.SelectedItem;
						}
						else
							return;
					}

					FilePath = file;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _buttonChange_Click(object sender) {
			try {
				var dialog = new SpriteReferenceSelectorDialog(_name, _sex, _actEditor.ActEditor.MetaGrf) { Owner = Window.GetWindow(this) };

				if (dialog.ShowDialog() == true) {
					_sex = dialog.SelectedFemale;
					ActEditorConfiguration.ConfigAsker["[ActEditor - Gender - " + _name + "]"] = _sex.ToString();
					_resourceActPath = dialog.SelectedRelativeActPath;
					ActEditorConfiguration.ConfigAsker["[ActEditor - Resource path - " + _name + "]"] = _resourceActPath;
					_filePath = dialog.SelectedContainerPath + "?" + _resourceActPath;
					ActEditorConfiguration.ConfigAsker["[ActEditor - Path - " + _name + "]"] = _filePath;
					OnFilePathChanged();
					_updateGenderButton();
					Update(true);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _reset_Click(object sender, RoutedEventArgs e) {
			try {
				FilePath = null;
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _paletteId_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_initializingPalette || _paletteId.SelectedItem == null)
				return;

			ActEditorConfiguration.ConfigAsker["[ActEditor - Palette - " + _name + "]"] = ((int)_paletteId.SelectedItem).ToString();
			Update(true);
		}

		private void _styleId_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_initializingStyle || !(_styleId.SelectedItem is HeadStyleChoice choice))
				return;

			_resourceActPath = choice.ActPath;
			_filePath = choice.ActPath ?? "";
			ActEditorConfiguration.ConfigAsker["[ActEditor - Resource path - " + _name + "]"] = _resourceActPath ?? "";
			ActEditorConfiguration.ConfigAsker["[ActEditor - Path - " + _name + "]"] = _filePath;
			OnFilePathChanged();
			Update(true);
		}

		private void _fancyButton_Click(object sender, RoutedEventArgs e) {
			_fancyButtons.ForEach(p => p.IsStatePressed = false);

			var fb = (SimpleButton) sender;
			fb.IsStatePressed = true;

			int offsetX = 0;
			int offsetY = 0;

			int[] offsetsX = { 0, -25, -25, -25, 0, 25, 25, 25 };
			int[] offsetsY = { 30, 30, 0, -30, -30, -30, 0, 30 };

			int idx = _fancyButtons.IndexOf(fb);
			offsetX += offsetsX[idx];
			offsetY += offsetsY[idx];

			_layerControl._tbOffsetX.SetValue(offsetX);
			_layerControl._tbOffsetY.SetValue(offsetY);
		}

		private void _buttonAnchor_Click(object sender, RoutedEventArgs e) {
			_cbAnchor.IsDropDownOpen = true;
		}

		private void _gender_Click(object sender, RoutedEventArgs e) {
			Sex = !Sex;
		}

		public string ReferenceName {
			get { return _name; }
		}

		public bool Sex {
			get { return _sex; }
			set {
				_sex = value;
				ActEditorConfiguration.ConfigAsker["[ActEditor - Gender - " + _name + "]"] = value.ToString();
				if (_name == "Head")
					ReloadStyleChoices();
				_updateGenderButton();
				Update(true);
			}
		}

		public void MakeAct(bool force = false) {
			if (Act != null && Spr != null && !force) {
				return;
			}

			byte[] dataAct = ApplicationManager.GetResource((_sex ? _defaultFemale : _defaultMale) + ".act");
			byte[] dataSpr = ApplicationManager.GetResource((_sex ? _defaultFemale : _defaultMale) + ".spr");

			if (!String.IsNullOrEmpty(_resourceActPath)) {
				byte[] resourceAct = _actEditor.ActEditor.MetaGrf.GetData(_resourceActPath);
				byte[] resourceSpr = _actEditor.ActEditor.MetaGrf.GetData(_resourceActPath.ReplaceExtension(".spr"));

				if (resourceAct != null && resourceSpr != null) {
					dataAct = resourceAct;
					dataSpr = resourceSpr;
				}
			}

			if (FilePath != null && FilePath.IsExtension(".spr", ".act")) {
				TkPath path = FilePath;

				if (File.Exists(path.FilePath)) {
					try {
						dataAct = GrfPath.GetData(path);
						if (dataAct == null)
							throw new FileNotFoundException("File not found : " + path);
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}

					try {
						path = path.GetFullPath().ReplaceExtension(".spr");
						dataSpr = GrfPath.GetData(path);
						if (dataSpr == null)
							throw new FileNotFoundException("File not found : " + path);
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
				}
			}

			Spr = new Spr(dataSpr);
			ApplySelectedPalette(Spr);
			Act = new Act(dataAct, Spr);
		}

		private void ApplySelectedPalette(Spr sprite) {
			if ((_name != "Head" && _name != "Body") || _paletteId.SelectedItem == null || String.IsNullOrEmpty(_resourceActPath))
				return;

			int paletteId = (int)_paletteId.SelectedItem;
			string gender = _sex ? GrfStrings.GenderFemale : GrfStrings.GenderMale;
			string fileName = Path.GetFileNameWithoutExtension(_resourceActPath);
			string genderSuffix = "_" + gender;
			int genderIndex = fileName.LastIndexOf(genderSuffix, StringComparison.OrdinalIgnoreCase);

			if (genderIndex >= 0)
				fileName = fileName.Remove(genderIndex) + fileName.Substring(genderIndex + genderSuffix.Length);

			var paths = new List<string>();

			if (_name == "Head") {
				string headId = fileName.Split('_')[0];
				paths.Add(String.Format(@"data\palette\¸Ó¸®\¸Ó¸®{0}_{1}_{2}.pal", headId, gender, paletteId));
			}
			else {
				string spriteName = fileName;
				while (spriteName.EndsWith("_1", StringComparison.OrdinalIgnoreCase) || spriteName.EndsWith("_2", StringComparison.OrdinalIgnoreCase))
					spriteName = spriteName.Substring(0, spriteName.Length - 2);

				string paletteName = spriteName;
				try {
					var bodyResource = new BodySpritesLoader().Load().Resources.Values.FirstOrDefault(value => String.Equals(value.Sprite, spriteName, StringComparison.OrdinalIgnoreCase));
					if (bodyResource != null && !String.IsNullOrEmpty(bodyResource.Palette))
						paletteName = bodyResource.Palette;
				}
				catch {
				}

				paths.Add(String.Format(@"data\palette\¸ö\{0}_{1}_{2}.pal", paletteName, gender, paletteId));
				paths.Add(String.Format(@"data\palette\¸ö\{0}_{1}.pal", paletteName, paletteId));
			}

			byte[] palette = null;
			foreach (string path in paths) {
				palette = _actEditor.ActEditor.MetaGrf.GetData(EncodingService.FromAnyToDisplayEncoding(path)) ?? _actEditor.ActEditor.MetaGrf.GetData(path);
				if (palette != null)
					break;
			}

			if (palette == null)
				return;

			// Ragnarok palettes do not store a usable alpha channel.  Applying the
			// raw bytes makes every indexed colour transparent in the renderer.
			sprite.Palette = new Pal(palette, Pal.FormatMode.NoTransparencyExceptFirstPixel);
		}

		private sealed class HeadStyleChoice {
			public string ActPath { get; private set; }
			public string DisplayName { get; private set; }
			public int SortId { get; private set; }

			public HeadStyleChoice(string actPath, string displayName) {
				ActPath = actPath;
				DisplayName = displayName;
				if (String.IsNullOrEmpty(actPath)) {
					SortId = -1;
					return;
				}
				string digits = new string(Path.GetFileNameWithoutExtension(actPath).TakeWhile(Char.IsDigit).ToArray());
				int sortId;
				SortId = Int32.TryParse(digits, out sortId) ? sortId : Int32.MaxValue;
			}

			public override string ToString() {
				return DisplayName;
			}
		}
	}
}
