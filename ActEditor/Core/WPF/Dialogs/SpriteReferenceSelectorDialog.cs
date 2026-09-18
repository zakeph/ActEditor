using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GRF.Core.GroupedGrf;
using GRF.IO;
using TokeiLibrary.WPF.Styles;
using Utilities.Extension;

namespace ActEditor.Core.WPF.Dialogs {
	/// <summary>
	/// Selects a bundled human head or body ACT/SPR pair.
	/// </summary>
	public class SpriteReferenceSelectorDialog : TkWindow {
		private const string HumanFolder = "ÀÎ°£Á·";
		private const string HeadFolder = "¸Ó¸®Åë";
		private const string BodyFolder = "¸öÅë";
		private const string MaleFolder = "³²";
		private const string FemaleFolder = "¿©";

		private readonly string _referenceName;
		private readonly MultiGrfReader _resources;
		private readonly TabControl _genderTabs;
		private readonly TextBox _searchBox;
		private readonly ListBox _spriteList;
		private readonly List<SpriteChoice>[] _choices = { new List<SpriteChoice>(), new List<SpriteChoice>() };

		public string SelectedActPath { get; private set; }
		public bool SelectedFemale { get; private set; }

		public string SelectedRelativeActPath { get; private set; }
		public string SelectedContainerPath { get; private set; }

		public SpriteReferenceSelectorDialog(string referenceName, bool female, MultiGrfReader resources)
			: base("Change " + referenceName, "advanced.png", SizeToContent.Manual, ResizeMode.CanResize) {
			_referenceName = referenceName;
			_resources = resources;
			Width = 520;
			Height = 620;
			MinWidth = 380;
			MinHeight = 360;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			var root = new Grid { Margin = new Thickness(8) };
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition());
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			var explanation = new TextBlock {
				Text = referenceName == "Head" ? "Choose a hairstyle (.act + .spr)" : "Choose a job body (.act + .spr)",
				Margin = new Thickness(3, 0, 3, 6)
			};
			root.Children.Add(explanation);

			_searchBox = new TextBox { Margin = new Thickness(3), ToolTip = "Filter by sprite name" };
			_searchBox.TextChanged += delegate { RefreshChoices(); };
			Grid.SetRow(_searchBox, 1);
			root.Children.Add(_searchBox);

			_genderTabs = new TabControl { Margin = new Thickness(3) };
			_genderTabs.Items.Add(new TabItem { Header = "Male" });
			_genderTabs.Items.Add(new TabItem { Header = "Female" });
			_genderTabs.SelectionChanged += delegate { RefreshChoices(); };
			Grid.SetRow(_genderTabs, 2);
			root.Children.Add(_genderTabs);

			_spriteList = new ListBox { Margin = new Thickness(0, 28, 0, 0) };
			_spriteList.MouseDoubleClick += delegate {
				if (_spriteList.SelectedItem != null)
					AcceptSelection();
			};
			_spriteList.KeyDown += delegate(object sender, KeyEventArgs e) {
				if (e.Key == Key.Enter && _spriteList.SelectedItem != null) {
					AcceptSelection();
					e.Handled = true;
				}
			};
			Grid.SetRow(_spriteList, 2);
			root.Children.Add(_spriteList);

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			var select = new Button { Content = "Change", MinWidth = 90, Height = 26, Margin = new Thickness(3), IsDefault = true };
			select.Click += delegate { AcceptSelection(); };
			var cancel = new Button { Content = "Cancel", MinWidth = 90, Height = 26, Margin = new Thickness(3), IsCancel = true };
			buttons.Children.Add(select);
			buttons.Children.Add(cancel);
			Grid.SetRow(buttons, 3);
			root.Children.Add(buttons);

			Content = root;
			LoadChoices();
			_genderTabs.SelectedIndex = female ? 1 : 0;
			RefreshChoices();
			Loaded += delegate { _searchBox.Focus(); };
		}

		private void LoadChoices() {
			if (_resources == null)
				throw new InvalidOperationException("The configured GRF resources are not available yet.");

			string kindFolder = _referenceName == "Head" ? HeadFolder : BodyFolder;
			LoadGender(kindFolder, MaleFolder, _choices[0]);
			LoadGender(kindFolder, FemaleFolder, _choices[1]);
		}

		private void LoadGender(string kindFolder, string genderFolder, List<SpriteChoice> output) {
			string path = "data\\sprite\\" + HumanFolder + "\\" + kindFolder + "\\" + genderFolder + "\\";
			string encodedPath = Utilities.Services.EncodingService.FromAnyToDisplayEncoding(path);
			var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var container in _resources.Containers.Values) {
				foreach (var entry in container.FileTable.EntriesInDirectory(encodedPath, SearchOption.AllDirectories)) {
					string resourcePath = entry.RelativePath;

					if (!resourcePath.IsExtension(".act") || !foundPaths.Add(resourcePath))
						continue;

					string actPath = resourcePath;
					if (!_resources.Exists(actPath.ReplaceExtension(".spr")))
						continue;

					string name = Path.GetFileNameWithoutExtension(actPath);
					string suffix = "_" + genderFolder;
					int genderIndex = name.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);

					if (genderIndex >= 0)
						name = name.Remove(genderIndex) + name.Substring(genderIndex + suffix.Length);

					string relativePath = actPath.StartsWith(encodedPath, StringComparison.OrdinalIgnoreCase) ? actPath.Substring(encodedPath.Length) : Path.GetFileName(actPath);
					string relativeDirectory = Path.GetDirectoryName(relativePath);
					string displayName = GetEnglishDisplayName(kindFolder, name, relativeDirectory, output.Count + 1);
					output.Add(new SpriteChoice(displayName, actPath, container.FileName));
				}
			}

			output.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName));
		}

		private static string GetEnglishDisplayName(string kindFolder, string name, string relativeDirectory, int fallbackIndex) {
			if (kindFolder == HeadFolder) {
				int headId;
				return Int32.TryParse(name, out headId) ? String.Format("Hairstyle #{0:000}", headId) : ToSafeEnglishName(name, "Hairstyle", fallbackIndex);
			}

			string normalized = name.Trim(' ', '\'', '"');
			while (normalized.EndsWith("_1", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("_2", StringComparison.OrdinalIgnoreCase))
				normalized = normalized.Substring(0, normalized.Length - 2);

			string englishName;
			if (JobNames.TryGetValue(normalized, out englishName)) {
				if (!String.IsNullOrEmpty(relativeDirectory)) {
					if (relativeDirectory.Equals("costume_1", StringComparison.OrdinalIgnoreCase))
						return englishName + " (Costume 1)";
					if (relativeDirectory.Equals("costume_2", StringComparison.OrdinalIgnoreCase))
						return englishName + " (Costume 2)";
				}

				return englishName;
			}

			return ToSafeEnglishName(normalized, "Unknown job", fallbackIndex);
		}

		private static string ToSafeEnglishName(string value, string fallback, int fallbackIndex) {
			if (value.All(character => character >= 32 && character <= 126))
				return value.Replace('_', ' ');

			return fallback + " #" + fallbackIndex;
		}

		private static readonly Dictionary<string, string> JobNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
			{ "ÃÊº¸ÀÚ", "Novice" }, { "½´ÆÛ³ëºñ½º", "Super Novice" },
			{ "¼ºÁ÷ÀÚ", "Acolyte" }, { "¼ºÁ÷ÀÚ_h", "Acolyte (Alt.)" }, { "±Ã¼ö", "Archer" }, { "¸¶¹ý»ç", "Magician" }, { "»óÀÎ", "Merchant" }, { "°Ë»ç", "Swordsman" }, { "µµµÏ", "Thief" },
			{ "ÇÁ¸®½ºÆ®", "Priest" }, { "ÇÁ¸®½ºÆ®_h", "Priest (Alt.)" }, { "¼ºÅõ»ç", "Priest (Alt. 2)" }, { "ÇåÅÍ", "Hunter" }, { "ÇåÅÍ_h", "Hunter (Alt.)" }, { "À§Àúµå", "Wizard" }, { "À§Àúµå_h", "Wizard (Alt.)" }, { "Á¦Ã¶°ø", "Blacksmith" }, { "Á¦Ã¶°ø_h", "Blacksmith (Alt.)" }, { "±â»ç", "Knight" }, { "±â»ç_h", "Knight (Alt.)" }, { "¾î¼¼½Å", "Assassin" }, { "¾î¼¼½Å_h", "Assassin (Alt.)" },
			{ "¸ùÅ©", "Monk" }, { "¸ùÅ©_h", "Monk (Alt.)" }, { "¹Ùµå", "Bard" }, { "¹Ùµå_h", "Bard (Alt.)" }, { "¹«Èñ", "Dancer" }, { "¹«Èñ_h", "Dancer (Alt.)" }, { "¼¼ÀÌÁö", "Sage" }, { "¼¼ÀÌÁö_h", "Sage (Alt.)" }, { "¿¬±Ý¼ú»ç", "Alchemist" }, { "¿¬±Ý¼ú»ç_h", "Alchemist (Alt.)" }, { "Å©·ç¼¼ÀÌ´õ", "Crusader" }, { "Å©·ç¼¼ÀÌ´õ_h", "Crusader (Alt.)" }, { "·Î±×", "Rogue" }, { "·Î±×_h", "Rogue (Alt.)" },
			{ "ÇÏÀÌÇÁ¸®", "High Priest" }, { "¼ºÅõ»ç2", "High Priest (Alt.)" }, { "½º³ªÀÌÆÛ", "Sniper" }, { "ÇÏÀÌÀ§Àúµå", "High Wizard" }, { "È­ÀÌÆ®½º¹Ì½º", "Whitesmith" }, { "·Îµå³ªÀÌÆ®", "Lord Knight" }, { "¾î½Ø½ÅÅ©·Î½º", "Assassin Cross" },
			{ "Ã¨ÇÇ¿Â", "Champion" }, { "Å¬¶ó¿î", "Clown" }, { "Áý½Ã", "Gypsy" }, { "ÇÁ·ÎÆä¼­", "Professor" }, { "Å©¸®¿¡ÀÌÅÍ", "Creator" }, { "ÆÈ¶óµò", "Paladin" }, { "½ºÅäÄ¿", "Stalker" },
			{ "¾ÆÅ©ºñ¼ó", "Archbishop" }, { "·¹ÀÎÁ®", "Ranger" }, { "¿ö·Ï", "Warlock" }, { "¹ÌÄÉ´Ð", "Mechanic" }, { "·é³ªÀÌÆ®", "Rune Knight" }, { "±æ·ÎÆ¾Å©·Î½º", "Guillotine Cross" },
			{ "½´¶ó", "Sura" }, { "¹Î½ºÆ®·²", "Minstrel" }, { "¿ø´õ·¯", "Wanderer" }, { "¼Ò¼­·¯", "Sorcerer" }, { "Á¦³×¸¯", "Genetic" }, { "°¡µå", "Royal Guard" }, { "½¦µµ¿ìÃ¼ÀÌ¼­", "Shadow Chaser" },
			{ "°Ç³Ê", "Gunslinger" }, { "´ÑÀÚ", "Ninja" }, { "ÅÂ±Ç¼Ò³â", "Taekwon" }, { "±Ç¼º", "Star Gladiator" }, { "¼Ò¿ï¸µÄ¿", "Soul Linker" }, { "¼ºÁ¦", "Star Emperor" }, { "¼Ò¿ï¸®ÆÛ", "Soul Reaper" },
			{ "³ëºñ½ºÆ÷¸µ", "Poring Novice" }, { "½´ÆÛ³ëºñ½ºÆ÷¸µ", "Poring Super Novice" }, { "º¹»ç¾ËÆÄÄ«", "Alpaca Acolyte" }, { "Å¸Á¶±Ã¼ö", "Ostrich Archer" }, { "¿©¿ì¸¶¹ý»ç", "Nine Tail Magician" }, { "»óÀÎ¸äµÅÁö", "Savage Merchant" }, { "ÆäÄÚ°Ë»ç", "Peco Peco Swordsman" }, { "ÄÌº£·Î½ºµµµÏ", "Galleon Thief" },
			{ "ÇÁ¸®½ºÆ®¾ËÆÄÄ«", "Alpaca Priest" }, { "Å¸Á¶ÇåÅÍ", "Ostrich Hunter" }, { "¿©¿ìÀ§Àúµå", "Nine Tail Wizard" }, { "Á¦Ã¶°ø¸äµÅÁö", "Savage Blacksmith" }, { "»çÀÚ±â»ç", "King Lion Knight" }, { "ÄÌº£·Î½º¾î½ê½Å", "Galleon Assassin" },
			{ "¸ùÅ©¾ËÆÄÄ«", "Alpaca Monk" }, { "Å¸Á¶¹Ùµå", "Ostrich Bard" }, { "Å¸Á¶¹«Èñ", "Ostrich Dancer" }, { "¿©¿ì¼¼ÀÌÁö", "Nine Tail Sage" }, { "¿¬±Ý¼ú»ç¸äµÅÁö", "Savage Alchemist" }, { "»çÀÚÅ©·ç¼¼ÀÌ´õ", "King Lion Crusader" }, { "ÄÌº£·Î½º·Î±×", "Galleon Rogue" },
			{ "ÇÏÀÌÇÁ¸®½ºÆ®¾ËÆÄÄ«", "Alpaca High Priest" }, { "Å¸Á¶½º³ªÀÌÆÛ", "Ostrich Sniper" }, { "¿©¿ìÇÏÀÌÀ§Àúµå", "Nine Tail High Wizard" }, { "È­ÀÌÆ®½º¹Ì½º¸äµÅÁö", "Savage Whitesmith" }, { "»çÀÚ·Îµå³ªÀÌÆ®", "King Lion Lord Knight" }, { "ÄÌº£·Î½º¾î½ê½ÅÅ©·Î½º", "Galleon Assassin Cross" },
			{ "Ã¨ÇÇ¿Â¾ËÆÄÄ«", "Alpaca Champion" }, { "Å¸Á¶Å©¶ó¿î", "Ostrich Clown" }, { "Å¸Á¶Â¤½Ã", "Ostrich Gypsy" }, { "¿©¿ìÇÁ·ÎÆä¼­", "Nine Tail Professor" }, { "Å©¸®¿¡ÀÌÅÍ¸äµÅÁö", "Savage Creator" }, { "»çÀÚÆÈ¶óµò", "King Lion Paladin" }, { "ÄÌº£·Î½º½ºÅäÄ¿", "Galleon Stalker" },
			{ "¾ÆÅ©ºñ¼ó¾ËÆÄÄ«", "Alpaca Archbishop" }, { "Å¸Á¶·¹ÀÎÁ®", "Ostrich Ranger" }, { "¿©¿ì¿ö·Ï", "Nine Tail Warlock" }, { "¹ÌÄÉ´Ð¸äµÅÁö", "Savage Mechanic" }, { "»çÀÚ·é³ªÀÌÆ®", "King Lion Rune Knight" }, { "ÄÌº£·Î½º±æ·ÎÆ¾Å©·Î½º", "Galleon Guillotine Cross" },
			{ "½´¶ó¾ËÆÄÄ«", "Alpaca Sura" }, { "Å¸Á¶¹Î½ºÆ®·²", "Ostrich Minstrel" }, { "Å¸Á¶¿ø´õ·¯", "Ostrich Wanderer" }, { "¿©¿ì¼Ò¼­·¯", "Nine Tail Sorcerer" }, { "Á¦³×¸¯¸äµÅÁö", "Savage Genetic" }, { "»çÀÚ·Î¾â°¡µå", "King Lion Royal Guard" }, { "ÄÌº£·Î½º½¦µµ¿ìÃ¼ÀÌ¼­", "Galleon Shadow Chaser" },
			{ "ÅÂ±Ç¼Ò³âÆ÷¸µ", "Poring Taekwon" }, { "µÎ²¨ºñ´ÑÀÚ", "Poison Toad Ninja" }, { "ÆäÄÚ°Ç³Ê", "Bike Gunslinger" }, { "±Ç¼ºÆ÷¸µ", "Poring Star Gladiator" }, { "µÎ²¨ºñ¼Ò¿ï¸µÄ¿", "Poison Toad Soul Linker" }, { "ÇØÅÂ¼ºÁ¦", "Haetae Star Emperor" }, { "ÇØÅÂ¼Ò¿ï¸®ÆÛ", "Haetae Soul Reaper" },
			{ "ÆäÄÚÆäÄÚ_±â»ç", "Peco Peco Knight" }, { "ÆäÄÚÆäÄÚ_±â»ç_h", "Peco Peco Knight (Alt.)" }, { "·ÎµåÆäÄÚ", "Armored Peco Peco Lord Knight" }, { "·é³ªÀÌÆ®»Ú¶ì", "Ferus Rune Knight" }, { "·é³ªÀÌÆ®»Ú¶ì2", "Black Ferus Rune Knight" }, { "·é³ªÀÌÆ®»Ú¶ì3", "White Ferus Rune Knight" }, { "·é³ªÀÌÆ®»Ú¶ì4", "Blue Ferus Rune Knight" }, { "·é³ªÀÌÆ®»Ú¶ì5", "Red Ferus Rune Knight" }, { "·¹ÀÎÁ®´Á´ë", "Warg Ranger" }, { "¸¶µµ±â¾î", "Magic Gear Mechanic" }, { "¸¶µµ¾Æ¸Ó", "Magic Gear Mechanic (jRO)" }, { "±¸ÆäÄÚÅ©·ç¼¼ÀÌ´õ", "Peco Peco Crusader" }, { "½ÅÆäÄÚÅ©·ç¼¼ÀÌ´õ", "Grand Peco Crusader" }, { "½ÅÆäÄÚÅ©·ç¼¼ÀÌ´õ_h", "Grand Peco Crusader (Alt.)" }, { "ÆäÄÚÆÈ¶óµò", "Armored Grand Peco Paladin" }, { "±×¸®Æù°¡µå", "Gryphon Royal Guard" },
			{ "¹«Èñ_¿©_¹ÙÁö", "Pants Dancer" }, { "¹«Èñ¹ÙÁö", "Pants Dancer" }, { "±Ç¼ºÀ¶ÇÕ", "Floating Star Gladiator" }, { "¼ºÁ¦À¶ÇÕ", "Floating Star Emperor" },
			{ "»êÅ¸", "Christmas Costume" }, { "¿©¸§", "Summer Costume" }, { "¿©¸§2", "Summer Costume 2" }, { "°áÈ¥", "Wedding Costume" }, { "ÅÎ½Ãµµ", "Wedding Costume 2" }, { "ÇÑº¹", "Hanbok Costume" }, { "¿ÁÅä¹öÆÐ½ºÆ®", "Oktoberfest Costume" },
			{ "¿î¿µÀÚ", "Game Master" }, { "¿î¿µÀÚ2", "Game Master 2" }, { "°Ë¿ëº´", "Mercenary Fencer" }, { "Ã¢¿ëº´", "Mercenary Spearman" }, { "È°¿ëº´", "Mercenary Bowman" }
		};

		private void RefreshChoices() {
			if (_spriteList == null || _genderTabs == null)
				return;

			int gender = Math.Max(0, _genderTabs.SelectedIndex);
			string filter = _searchBox.Text ?? "";
			_spriteList.ItemsSource = _choices[gender]
				.Where(choice => choice.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
				.ToList();
		}

		private void AcceptSelection() {
			var choice = _spriteList.SelectedItem as SpriteChoice;

			if (choice == null)
				return;

			SelectedActPath = choice.ActPath;
			SelectedRelativeActPath = choice.ActPath;
			SelectedContainerPath = choice.ContainerPath;
			SelectedFemale = _genderTabs.SelectedIndex == 1;
			DialogResult = true;
		}

		private class SpriteChoice {
			public string DisplayName { get; private set; }
			public string ActPath { get; private set; }
			public string ContainerPath { get; private set; }

			public SpriteChoice(string displayName, string actPath, string containerPath) {
				DisplayName = displayName;
				ActPath = actPath;
				ContainerPath = containerPath;
			}

			public override string ToString() {
				return DisplayName;
			}
		}
	}
}
