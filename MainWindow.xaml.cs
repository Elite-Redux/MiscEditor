﻿using Microsoft.Win32;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AbilityEditor
{
	public class ViewModel : INotifyPropertyChanged
	{
		public ViewModel()
		{
			_abilities = new AbilityList([], []);
			_selectedAbility = null;
			_moves = new MoveList([], [], [], [], [], "");
			_selectedAbility = null;
		}

		private AbilityList _abilities;
		public AbilityList Abilities
		{
			get => _abilities;
			set
			{
				_abilities = value;

				OnPropertyChanged();

				SelectedAbility = Abilities.Abilities.FirstOrDefault(a => a.EnumValue == SelectedAbility?.EnumValue);
			}
		}

		private Ability? _selectedAbility;
		public Ability? SelectedAbility
		{
			get => _selectedAbility;
			set
			{
				_selectedAbility = value;
				OnPropertyChanged();
			}
		}

		private MoveList _moves;
		public MoveList Moves
		{
			get => _moves;
			set
			{
				_moves = value;

				OnPropertyChanged();

				SelectedMove = Moves.Moves.FirstOrDefault(a => a.EnumValue == SelectedAbility?.EnumValue);
			}
		}

		private Move? _selectedMove;
		public Move? SelectedMove
		{
			get => _selectedMove;
			set
			{
				_selectedMove = value;
				OnPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string name = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		string ErFolder { get; set; } = "";

		ViewModel viewModel { get; } = new ViewModel();

		public MainWindow()
		{
			InitializeComponent();
			DataContext = viewModel;
		}

		void ChooseErFolder(object Sender, RoutedEventArgs e)
		{
			OpenFolderDialog dialog = new()
			{
				Title = "Choose ER Folder",
				InitialDirectory = ErFolder,
				Multiselect = false,
			};
			if (dialog.ShowDialog() == true)
			{
				ErFolder = dialog.FolderName;
				ImportButton.IsEnabled = true;
				LoadData();
			}
		}

		void LoadData()
		{
			try
			{
				AbilityLoader loader = new(ErFolder);
				viewModel.Abilities = loader.GetAbilities();

				MoveLoader moveLoader = new(ErFolder);
				viewModel.Moves = moveLoader.ReadMoveList();

				if (!Tabs.IsEnabled)
				{
					Tabs.IsEnabled = true;
					MovesTab.IsEnabled = true;
					AbilityTab.IsEnabled = true;
					Tabs.SelectedItem = AbilityTab;
					ExportButton.IsEnabled = true;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed parsing data: {ex}", "Failed loading data", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void AddAbility(object sender, RoutedEventArgs e)
		{
			int count = viewModel.Abilities.Abilities.Count(it => it.EnumValue.StartsWith("ABILITY_NEW"));
			viewModel.Abilities.Abilities.Add(new Ability(count == 0 ? "ABILITY_NEW" : $"ABILITY_NEW_{count + 1}", "New Ability", ["Placeholder"]));
			viewModel.SelectedAbility = viewModel.Abilities.Abilities.Last();
		}

		[GeneratedRegex(@"^[a-zA-Z][a-zA-Z_0-9]+$")]
		private static partial Regex ValidEnum();

		private void SaveAbility(object sender, RoutedEventArgs e)
		{
			var selectedAbility = viewModel.SelectedAbility;
			if (selectedAbility == null) { return; }

			string newEnumValue = AbilityEnumTextBox.Text.Trim();
			if (!newEnumValue.StartsWith("ABILITY_"))
			{
				MessageBox.Show("Ability enum must start with ABILITY_", "Could Not Save", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (selectedAbility.EnumValue != newEnumValue && viewModel.Abilities.Abilities.Any(it => it.EnumValue == newEnumValue))
			{
				MessageBox.Show("Enum value already exists", "Could Not Save", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (!ValidEnum().IsMatch(newEnumValue))
			{
				MessageBox.Show("Enum value must start with a letter and contain only letters, numbers, and underscores", "Could Not Save", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			Ability newAbility = new(Name: AbilityNameTextBox.Text.Trim(),
				EnumValue: newEnumValue,
				Description: [AbilityDescription1TextBox.Text.Trim(), AbilityDescription2TextBox.Text.Trim()]);
			viewModel.Abilities.Abilities[viewModel.Abilities.Abilities.IndexOf(selectedAbility)] = newAbility;
			viewModel.SelectedAbility = newAbility;
		}

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			AbilityLoader loader = new AbilityLoader(ErFolder);
			loader.WriteEnums(viewModel.Abilities);
			loader.WriteText(viewModel.Abilities);

			MoveLoader moveLoader = new MoveLoader(ErFolder);
			moveLoader.WriteMoveList(viewModel.Moves);
		}

		private void ImportButton_Click(object sender, RoutedEventArgs e)
		{
			LoadData();
		}

		private void RemoveAbility(object sender, RoutedEventArgs e)
		{
			if (viewModel.SelectedAbility == null) return;
			MessageBoxResult messageBoxResult = MessageBox.Show($"Are you sure you want to delete ability {viewModel.SelectedAbility.Name}?", "Delete Confirmation", MessageBoxButton.YesNo);
			if (messageBoxResult == MessageBoxResult.Yes)
			{
				viewModel.Abilities.Abilities.Remove(viewModel.SelectedAbility);
				viewModel.SelectedAbility = null;
			}
		}

		private void AddMove(object sender, RoutedEventArgs e)
		{
			int currentNewMoves = viewModel.Moves.Moves.Count(it => it.EnumValue.StartsWith("MOVE_NEW_MOVE"));
			viewModel.Moves.Moves.Add(new(currentNewMoves == 0 ? "MOVE_NEW_MOVE" : $"MOVE_NEW_MOVE_{currentNewMoves + 1}"));
			viewModel.SelectedMove = viewModel.Moves.Moves.Last();
		}

		private void RemoveMove(object sender, RoutedEventArgs e)
		{
			if (viewModel.SelectedMove == null) return;
			MessageBoxResult messageBoxResult = MessageBox.Show($"Are you sure you want to delete move {viewModel.SelectedMove.Name}?", "Delete Confirmation", MessageBoxButton.YesNo);
			if (messageBoxResult == MessageBoxResult.Yes)
			{
				viewModel.Moves.Moves.Remove(viewModel.SelectedMove);
				viewModel.SelectedAbility = null;
			}
		}

		private void SaveMove(object sender, RoutedEventArgs e)
		{
			if (viewModel.SelectedMove == null) return;

			string newEnumValue = MoveEnumTextBox.Text.Trim();
			if (!newEnumValue.StartsWith("MOVE_"))
			{
				MessageBox.Show("Move enum must start with MOVE_", "Could Not Save", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (viewModel.SelectedMove.EnumValue != newEnumValue && viewModel.Moves.Moves.Any(it => it.EnumValue == newEnumValue))
			{
				MessageBox.Show("Enum value already exists", "Could Not Save", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (!ValidEnum().IsMatch(newEnumValue))
			{
				MessageBox.Show("Enum value must start with a letter and contain only letters, numbers, and underscores", "Could Not Save", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var newMove = viewModel.SelectedMove with
			{
				EnumValue = newEnumValue,
				Name = MoveNameTextBox.Text.Trim(),
				ShortName = MoveShortNameTextBox.Text.Trim(),
				DescriptionTwoLine = [Move2LineTextBox.Text.Trim(), Move2Line2TextBox.Text.Trim()],
				DescriptionFourLine = [Move4LineTextBox.Text.Trim(), Move4Line2TextBox.Text.Trim(), Move4Line3TextBox.Text.Trim(), Move4Line4TextBox.Text.Trim()],
				BattleMove = viewModel.SelectedMove.BattleMove with { EnumValue = MoveEnumTextBox.Text.Trim() },
			};
			viewModel.Moves.Moves[viewModel.Moves.Moves.IndexOf(viewModel.SelectedMove)] = newMove;
			viewModel.SelectedMove = newMove;
		}
	}
}