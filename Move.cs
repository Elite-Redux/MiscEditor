using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace AbilityEditor
{
	public record Move(string EnumValue, string Name, string ShortName, string Animation, ImmutableList<string> DescriptionTwoLine, ImmutableList<string> DescriptionFourLine, BattleMove BattleMove)
	{
		public Move(string enumValue = "MOVE_NEW_MOVE") : this(EnumValue: enumValue,
			Name: "New Move",
			ShortName: "New Move",
			Animation: "Move_NONE",
			DescriptionTwoLine: [],
			DescriptionFourLine: [],
			BattleMove: new(enumValue))
		{ }
	}

	public enum PokeType
	{
		NONE,
		NORMAL,
		FIGHTING,
		FLYING,
		POISON,
		GROUND,
		ROCK,
		BUG,
		GHOST,
		STEEL,
		MYSTERY,
		FIRE,
		WATER,
		GRASS,
		ELECTRIC,
		PSYCHIC,
		ICE,
		DRAGON,
		DARK,
		FAIRY,
	}

	public enum MoveTarget
	{
		SELECTED = 0x0,
		DEPENDS = 0x1,
		USER_OR_SELECTED = 0x2,
		RANDOM = 0x4,
		BOTH = 0x8,
		USER = 0x10,
		FOES_AND_ALLY = 0x20,
		OPPONENTS_FIELD = 0x40,
		ALLY = 0x80,
		ALL_BATTLERS = 0x100 | USER,
	}

	public enum MoveSplit
	{
		PHYSICAL,
		SPECIAL,
		STATUS,
	}

	public record BattleMove(string EnumValue,
		string Effect,
		int Power,
		PokeType Type,
		PokeType Type2,
		int Accuracy,
		int Pp,
		int SecondaryEffectChance,
		HashSet<string> Flags,
		MoveSplit Split,
		MoveTarget Target,
		string Argument,
		int Priority)
	{
		public BattleMove(string enumValue = "") : this(EnumValue: enumValue,
				Effect: "EFFECT_PLACEHOLDER",
				Power: 0,
				Type: PokeType.NORMAL,
				Type2: PokeType.NORMAL,
				Accuracy: 0,
				Pp: 0,
				SecondaryEffectChance: 0,
				Flags: [],
				Split: MoveSplit.PHYSICAL,
				Target: MoveTarget.SELECTED,
				Argument: "",
				Priority: 0)
		{ }
	}

	public record MoveReference(string Define, string ReferencedEnum, int Offset);

	public record MoveList(ObservableCollection<Move> Moves,
		ImmutableList<MoveReference> MoveReferences,
		ImmutableList<string> Flags1,
		ImmutableList<string> Flags2,
		ImmutableList<string> Effects,
		string IntimidateBlock);

	public partial class MoveLoader(string ErFolder)
	{
		private const string NAME = "name";
		private const string MARKER = "marker";
		private const string VALUE = "value";
		private const string OFFSET = "offset";

		private const int SHORT_NAME_LENGTH = 12;
		private const int LONG_NAME_LENGTH = 18;

		private static bool TryMatch(Regex regex, string line, out Match match)
		{
			match = regex.Match(line);
			return match.Success;
		}

		private readonly string EnumFile = ErFolder + @"\include\constants\moves.h";

		[GeneratedRegex(@"^\s*#define (?<name>MOVE_\w+)\s+(?<value>\d+)")]
		private static partial Regex EnumRegex();
		[GeneratedRegex(@"^\s*#define (?<name>MOVES_\w+)\s+(?<value>\d+)")]
		private static partial Regex EnumReferenceNumericRegex();
		[GeneratedRegex(@"^\s*#define (?<name>MOVES_\w+)\s+\(?(?<marker>MOVE_\w+)\s+(\+\s+(?<offset>\d+))\)?")]
		private static partial Regex EnumReferenceRegex();

		private (List<string> enums, List<MoveReference> references) ReadEnums()
		{
			using var stream = File.Open(EnumFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			Dictionary<int, string> enums = [];
			List<MoveReference> references = [];
			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine()!;

				if (TryMatch(EnumRegex(), line, out var match))
				{
					enums[int.Parse(match.Groups[VALUE].Value)] = match.Groups[NAME].Value;
				}
				else if (TryMatch(EnumReferenceNumericRegex(), line, out match))
				{
					int value = int.Parse(match.Groups[VALUE].Value) - 1;
					references.Add(new(Define: match.Groups[NAME].Value,
						ReferencedEnum: enums[value],
						Offset: 1));
				}
				else if (TryMatch(EnumReferenceRegex(), line, out match))
				{
					references.Add(new(Define: match.Groups[NAME].Value,
						ReferencedEnum: match.Groups[MARKER].Value,
						Offset: match.Groups[OFFSET].Success ? int.Parse(match.Groups[OFFSET].Value) : 0));
				}
			}

			return (enums.OrderBy(it => it.Key).Select(it => it.Value).ToList(), references);
		}

		private void WriteEnums(MoveList moves)
		{
			using var stream = File.Open(EnumFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			writer.WriteLine("#ifndef GUARD_CONSTANTS_MOVES_H");
			writer.WriteLine("#define GUARD_CONSTANTS_MOVES_H");
			writer.WriteLine();

			Dictionary<string, MoveReference> references = moves.MoveReferences.Where(it => it.Define != "MOVES_COUNT_DARKY").ToDictionary(it => it.ReferencedEnum);
			int index = 0;

			foreach (var move in moves.Moves)
			{
				writer.WriteLine($"#define {move.EnumValue} {index++}");

				if (references.TryGetValue(move.EnumValue, out var reference))
				{
					writer.WriteLine();
					writer.WriteLine($"#define {reference.Define} ({reference.ReferencedEnum}{(reference.Offset != 0 ? " + " + reference.Offset.ToString() : "")})");
				}
			}

			writer.WriteLine();
			writer.WriteLine($"#define MOVES_COUNT_DARKY ({moves.Moves.Last().EnumValue} + 1)");
			writer.WriteLine();
			writer.WriteLine("#define MOVES_COUNT MOVES_COUNT_DARKY");
			writer.WriteLine();
			writer.WriteLine("#define EFFECTIVENESS_NEUTRAL             1.0");
			writer.WriteLine("#define EFFECTIVENESS_NOT_VERY_EFFECTIVE  0.5");
			writer.WriteLine("#define EFFECTIVENESS_SUPER_EFFECTIVE     2.0");
			writer.WriteLine("#define EFFECTIVENESS_SUPER_FROM_NOT_VERY 4.0");
			writer.WriteLine();
			writer.WriteLine("#endif  // GUARD_CONSTANTS_MOVES_H");
		}

		private readonly string BattleMovesFile = ErFolder + @"\src\data\battle_moves.h";

		[GeneratedRegex(@"^\s*\[\s*(?<name>MOVE_\w+)\s*\]\s+=\s*\{?")]
		private static partial Regex BattleMoveStartRegex();
		[GeneratedRegex(@"^\s*\.effect\s+=\s+(?<value>EFFECT_\w+)\s*,?")]
		private static partial Regex BattleMoveEffectRegex();
		[GeneratedRegex(@"^\s*\.power\s+=\s+(?<value>\d+)\s*,?")]
		private static partial Regex BattleMovePowerRegex();
		[GeneratedRegex(@"^\s*\.accuracy\s+=\s+(?<value>\d+)\s*,?")]
		private static partial Regex BattleMoveAccuracyRegex();
		[GeneratedRegex(@"^\s*\.pp\s+=\s+(?<value>\d+)\s*,?")]
		private static partial Regex BattleMovePpRegex();
		[GeneratedRegex(@"^\s*\.priority\s+=\s+(?<value>-?\d+)\s*,?")]
		private static partial Regex BattleMovePriorityRegex();
		[GeneratedRegex(@"^\s*\.secondaryEffectChance\s+=\s+(?<value>\d+)\s*,?")]
		private static partial Regex BattleMoveSecondaryEffectChanceRegex();
		[GeneratedRegex(@"^\s*\.type\s+=\s+TYPE_(?<value>\w+)\s*,?")]
		private static partial Regex BattleMoveTypeRegex();
		[GeneratedRegex(@"^\s*\.type2\s+=\s+TYPE_(?<value>\w+)\s*,?")]
		private static partial Regex BattleMoveType2Regex();
		[GeneratedRegex(@"^\s*\.target\s+=\s+(?<value>MOVE_TARGET_\w+(\s+\|\s+MOVE_TARGET\w+)*)\s*,?")]
		private static partial Regex BattleMoveTargetRegex();
		[GeneratedRegex(@"(\s|^)MOVE_TARGET_(?<value>\w+)(\s|$)")]
		private static partial Regex BattleMoveTargetPieceRegex();
		[GeneratedRegex(@"^\s*\.split\s+=\s+SPLIT_(?<value>\w+)\s*,?")]
		private static partial Regex BattleMoveSplitRegex();
		[GeneratedRegex(@"^\s*\.flags2?\s+=\s+(?<value>FLAG_\w+(\s+\|\s+FLAG_\w+)*)\s*,?")]
		private static partial Regex BattleMoveFlagsRegex();
		[GeneratedRegex(@"(\s|^)(?<value>FLAG_\w+)(\s|$)")]
		private static partial Regex BattleMoveFlagsPieceRegex();
		[GeneratedRegex(@"^\s*\.argument\s+=\s+(?<value>[^,]+)\s*,?")]
		private static partial Regex BattleMoveArgumentRegex();
		[GeneratedRegex(@"^\s*\}\s*,")]
		private static partial Regex BattleMoveEndRegex();

		private (Dictionary<string, BattleMove> battleMoves, string intimidateBlock) ReadBattleMoves(ImmutableHashSet<string> validEffects, ImmutableHashSet<string> validFlags)
		{
			using var stream = File.Open(BattleMovesFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			Dictionary<string, BattleMove> battleMoves = [];
			BattleMove currentMove = new();

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine()!;

				if (TryMatch(BattleMoveStartRegex(), line, out var match))
				{
					currentMove = new(match.Groups[NAME].Value);
				}
				else if (TryMatch(BattleMoveEffectRegex(), line, out match))
				{
					if (!validEffects.Contains(match.Groups[VALUE].Value))
					{
						throw new Exception($"Unexpected effect {match.Groups[VALUE].Value}");
					}
					currentMove = currentMove with { Effect = match.Groups[VALUE].Value };
				}
				else if (TryMatch(BattleMovePowerRegex(), line, out match))
				{
					currentMove = currentMove with { Power = int.Parse(match.Groups[VALUE].Value) };
				}
				else if (TryMatch(BattleMoveAccuracyRegex(), line, out match))
				{
					currentMove = currentMove with { Accuracy = int.Parse(match.Groups[VALUE].Value) };
				}
				else if (TryMatch(BattleMovePpRegex(), line, out match))
				{
					currentMove = currentMove with { Pp = int.Parse(match.Groups[VALUE].Value) };
				}
				else if (TryMatch(BattleMovePriorityRegex(), line, out match))
				{
					currentMove = currentMove with { Priority = int.Parse(match.Groups[VALUE].Value) };
				}
				else if (TryMatch(BattleMoveSecondaryEffectChanceRegex(), line, out match))
				{
					currentMove = currentMove with { SecondaryEffectChance = int.Parse(match.Groups[VALUE].Value) };
				}
				else if (TryMatch(BattleMoveTypeRegex(), line, out match))
				{
					currentMove = currentMove with { Type = (PokeType)Enum.Parse(typeof(PokeType), match.Groups[VALUE].Value)! };
				}
				else if (TryMatch(BattleMoveType2Regex(), line, out match))
				{
					currentMove = currentMove with { Type2 = (PokeType)Enum.Parse(typeof(PokeType), match.Groups[VALUE].Value)! };
				}
				else if (TryMatch(BattleMoveTargetRegex(), line, out match))
				{
					currentMove = currentMove with
					{
						Target = BattleMoveTargetPieceRegex().Matches(match.Groups[VALUE].Value)
						.Select(it => it.Groups[VALUE].Value)
						.Select(it => (MoveTarget)Enum.Parse(typeof(MoveTarget), it)!)
						.Aggregate((it, acc) => it | acc)
					};
				}
				else if (TryMatch(BattleMoveSplitRegex(), line, out match))
				{
					currentMove = currentMove with { Split = (MoveSplit)Enum.Parse(typeof(MoveSplit), match.Groups[VALUE].Value)! };
				}
				else if (TryMatch(BattleMoveFlagsRegex(), line, out match))
				{
					HashSet<string> flags = BattleMoveFlagsPieceRegex().Matches(match.Groups[VALUE].Value)
						.Select(it => it.Groups[VALUE].Value)
						.ToHashSet();

					if (!flags.IsSubsetOf(validFlags))
					{
						throw new Exception($"Unexpected flags {string.Join(", ", flags.DistinctBy(it => !validFlags.Contains(it)))}");
					}

					flags.UnionWith(currentMove.Flags);
					currentMove = currentMove with { Flags = flags };
				}
				else if (TryMatch(BattleMoveArgumentRegex(), line, out match))
				{
					currentMove = currentMove with { Argument = match.Groups[VALUE].Value };
				}
				else if (TryMatch(BattleMoveEndRegex(), line, out match))
				{
					if (!string.IsNullOrWhiteSpace(currentMove.EnumValue))
					{
						battleMoves[currentMove.EnumValue] = currentMove;
					}
					currentMove = new();
				}
				else if (line.Contains(@"const struct IntimidateCloneData gIntimidateCloneData[NUM_INTIMIDATE_CLONES] ="))
				{
					return (battleMoves, line + "\n" + reader.ReadToEnd());
				}
			}

			return (battleMoves, "");
		}

		private string Indent(int n) => new(' ', 4 * n);

		private void WriteBattleMoves(MoveList moves)
		{
			using var stream = File.Open(BattleMovesFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			writer.WriteLine("const struct BattleMove gBattleMoves[MOVES_COUNT] =");
			writer.WriteLine("{");

			var allMoveTargets = Enum.GetValues(typeof(MoveTarget));
			Array.Reverse(allMoveTargets);

			foreach (var battleMove in moves.Moves.Select(it => it.BattleMove))
			{
				writer.WriteLine(Indent(1) + $"[{battleMove.EnumValue}] =");
				writer.WriteLine(Indent(1) + "{");

				writer.WriteLine(Indent(2) + $".effect = {battleMove.Effect},");
				writer.WriteLine(Indent(2) + $".power = {battleMove.Power},");
				writer.WriteLine(Indent(2) + $".type = TYPE_{battleMove.Type},");
				if (battleMove.Type2 != PokeType.NORMAL)
				{
					writer.WriteLine(Indent(2) + $".type2 = TYPE_{battleMove.Type2},");
				}
				writer.WriteLine(Indent(2) + $".accuracy = {battleMove.Accuracy},");
				writer.WriteLine(Indent(2) + $".pp = {battleMove.Pp},");
				writer.WriteLine(Indent(2) + $".secondaryEffectChance = {battleMove.SecondaryEffectChance},");
				MoveTarget target = battleMove.Target;
				List<MoveTarget> targets = [];
				if (target == MoveTarget.SELECTED)
				{
					targets.Add(MoveTarget.SELECTED);
				}
				else
				{
					foreach (MoveTarget possibleTarget in allMoveTargets)
					{
						if ((target & possibleTarget) == possibleTarget)
						{
							target &= ~possibleTarget;
							targets.Add(possibleTarget);
							if (target == 0)
							{
								break;
							}
						}
					}

					targets.Reverse();
				}
				writer.WriteLine(Indent(2) + $".target = {string.Join(" | ", targets.Select(it => "MOVE_TARGET_" + it.ToString()))},");
				if (battleMove.Priority != 0)
				{
					writer.WriteLine(Indent(2) + $".priority = {battleMove.Priority},");
				}
				var flags1 = moves.Flags1.Intersect(battleMove.Flags);
				if (flags1.Any())
				{
					writer.WriteLine(Indent(2) + $".flags = {string.Join(" | ", flags1)},");
				}
				var flags2 = moves.Flags2.Intersect(battleMove.Flags);
				if (flags2.Any())
				{
					writer.WriteLine(Indent(2) + $".flags2 = {string.Join(" | ", flags2)},");
				}
				writer.WriteLine(Indent(2) + $".split = SPLIT_{battleMove.Split},");
				if (!string.IsNullOrWhiteSpace(battleMove.Argument))
				{
					writer.WriteLine(Indent(2) + $".argument = {battleMove.Argument},");
				}

				writer.WriteLine(Indent(1) + "},");
			}

			writer.WriteLine("};");
			writer.WriteLine();
			writer.Write(moves.IntimidateBlock);
		}

		private readonly string FlagsFile = ErFolder + @"\include\constants\pokemon.h";

		[GeneratedRegex(@"^\s*#define (?<name>FLAG_\w+)\s+\(1\s+<<\s+(?<value>\d+)\)")]
		private static partial Regex FlagRegex();

		private (List<string> Flags1, List<string> Flags2) ReadFlags()
		{
			using var stream = File.Open(FlagsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			Dictionary<int, string> flags1 = new(32);
			List<string> flags2 = [];

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine()!;

				if (TryMatch(FlagRegex(), line, out var match))
				{
					var position = int.Parse(match.Groups[VALUE].Value);
					if (flags1.ContainsKey(position))
					{
						flags2.Add(match.Groups[NAME].Value);
					}
					else
					{
						flags1[position] = match.Groups[NAME].Value;
					}
				}
			}

			return (flags1.OrderBy(it => it.Key).Select(it => it.Value).ToList(), flags2);
		}

		private void WriteFlags(MoveList moves)
		{
			List<string> ReadAllFlagData()
			{
				using var stream = File.Open(FlagsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				using var reader = new StreamReader(stream);

				List<string> flags = [];
				while (!reader.EndOfStream)
				{
					flags.Add(reader.ReadLine()!);
				}

				return flags;
			}

			var oldData = ReadAllFlagData();

			using var stream = File.Open(FlagsFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			foreach (var line in oldData.TakeWhile(it => !FlagRegex().IsMatch(it)))
			{
				writer.WriteLine(line);
			}

			int targetColumn = (int)(Math.Ceiling(("#define ".Length + moves.Flags1.Concat(moves.Flags2).Max(it => it.Length)) / 4.0) * 4) - ("#define ".Length + 1);

			int counter = 0;
			foreach (var flag in moves.Flags1)
			{
				writer.WriteLine($"#define {flag} {new string(' ', targetColumn - flag.Length)}(1 << {counter++})");
			}

			writer.WriteLine();
			writer.WriteLine("// Battle move Flags 2");

			counter = 0;
			foreach (var flag in moves.Flags2)
			{
				writer.WriteLine($"#define {flag} {new string(' ', targetColumn - flag.Length)}(1 << {counter++})");
			}

			foreach (var line in oldData.Reverse<string>().TakeWhile(it => !FlagRegex().IsMatch(it)).Reverse())
			{
				writer.WriteLine(line);
			}
		}

		private readonly string EffectsFile = ErFolder + @"\include\constants\battle_move_effects.h";

		[GeneratedRegex(@"^\s*#define (?<name>EFFECT_\w+)\s+(?<value>\d+)")]
		private static partial Regex EffectsRegex();

		private List<string> ReadEffects()
		{
			using var stream = File.Open(EffectsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			Dictionary<int, string> effects = [];

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine()!;

				if (TryMatch(EffectsRegex(), line, out var match))
				{
					effects[int.Parse(match.Groups[VALUE].Value)] = match.Groups[NAME].Value;
				}
			}

			return effects.OrderBy(it => it.Key).Select(it => it.Value).ToList();
		}

		private void WriteEffects(MoveList moves)
		{
			using var stream = File.Open(EffectsFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			writer.WriteLine("#ifndef GUARD_CONSTANTS_BATTLE_MOVE_EFFECTS_H");
			writer.WriteLine("#define GUARD_CONSTANTS_BATTLE_MOVE_EFFECTS_H");
			writer.WriteLine();

			int counter = 0;
			foreach (var effect in moves.Effects)
			{
				writer.WriteLine($"#define {effect} {counter++}");
			}

			writer.WriteLine();
			writer.WriteLine($"#define NUM_BATTLE_MOVE_EFFECTS ({moves.Effects.Last()} + 1)");
			writer.WriteLine();
			writer.WriteLine("#endif  // GUARD_CONSTANTS_BATTLE_MOVE_EFFECTS_H");
		}

		private readonly string AnimationsFile = ErFolder + @"\data\battle_anim_scripts.s";

		[GeneratedRegex(@"^\s*\.4byte (?<name>Move_\w+)")]
		private static partial Regex AnimationRegex();
		[GeneratedRegex(@"^\s*(?!@|\.4byte Move_\w+)[^\s]")]
		private static partial Regex AnimationEndRegex();

		private const string AnimationStart = "gBattleAnims_Moves::";

		private List<string> ReadAnimations()
		{
			using var stream = File.Open(AnimationsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			while (!reader.ReadLine()!.Contains(AnimationStart)) { }

			List<string> animations = [];

			while (true)
			{
				var line = reader.ReadLine()!;

				if (TryMatch(AnimationRegex(), line, out var match))
				{
					animations.Add(match.Groups[NAME].Value);
				}
				else if (TryMatch(AnimationEndRegex(), line, out _))
				{
					break;
				}
			}

			return animations;
		}

		private void WriteAnimations(MoveList moves)
		{
			List<string> ReadAllAnimationData()
			{
				using var stream = File.Open(AnimationsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				using var reader = new StreamReader(stream);

				List<string> animations = [];
				while (!reader.EndOfStream)
				{
					animations.Add(reader.ReadLine()!);
				}

				return animations;
			}

			var oldData = ReadAllAnimationData();

			using var stream = File.Open(AnimationsFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			int index = 0;
			foreach (var line in oldData)
			{
				writer.WriteLine(line);
				index++;

				if (line.Contains(AnimationStart))
				{
					break;
				}
			}

			foreach (var (animation, enumValue) in moves.Moves.Select(it => (it.Animation, it.EnumValue)))
			{
				string comment = !animation.Replace("_", "").Equals(enumValue.Replace("_", ""), StringComparison.InvariantCultureIgnoreCase) ? $" @ {enumValue}" : "";
				writer.WriteLine($"\t.4byte {animation}{comment}");
			}

			writer.WriteLine();
			foreach (var line in oldData.Skip(index).SkipWhile(it => !AnimationEndRegex().IsMatch(it)))
			{
				writer.WriteLine(line);
			}
		}

		private readonly string MoveDescriptionsFile = ErFolder + @"\src\data\text\move_descriptions.h";

		[GeneratedRegex(@"^\s*static\s+const\s+u8\s+(?<name>s\w+)\[\]\s+=\s*_\(\s*(//.*)?$")]
		private static partial Regex MoveDescriptionStartRegex();
		[GeneratedRegex(@"^\s*""(?<value>[^""]*)\\n""\s*(//.*)?$")]
		private static partial Regex MoveDescriptionContinueRegex();
		[GeneratedRegex(@"^\s*""(?<value>[^""]*)""\);")]
		private static partial Regex MoveDescriptionFinishRegex();
		[GeneratedRegex(@"^\s*\[\s*(?<name>MOVE_\w+)\s+-\s+1\s*\]\s+=\s+(?<marker>s\w+),")]
		private static partial Regex MoveDescriptionReferenceRegex();
		[GeneratedRegex(@"^\s*static\s+const\s+u8\s+(?<name>sMoveFourLineDescription_\w+)\[\]\s+=\s*_\(\s*""(?<value>[^""]*)""\s*\);\s*(//.*)?$")]
		private static partial Regex MoveDescriptionFourLineStartRegex();
		[GeneratedRegex(@"^\s*\[\s*(?<name>MOVE_\w+)\s+-\s+1\s*\]\s+=\s+(?<marker>sMoveFourLineDescription_\w+),")]
		private static partial Regex MoveDescriptionFourLineReferenceRegex();

		private Dictionary<string, (List<string> descriptionTwoLine, List<string> descriptionFourLine)> ReadMoveDescriptions()
		{
			using var stream = File.Open(MoveDescriptionsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			Dictionary<string, List<string>> twoLineDescriptions = [];
			Dictionary<string, string> twoLineDescriptionReferences = [];

			Dictionary<string, string> fourLineDescriptions = [];
			Dictionary<string, string> fourLineDescriptionReferences = [];

			List<string> currentTwoLineDescription = [];
			string currentTwoLineName = "";

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine()!;

				if (TryMatch(MoveDescriptionFourLineStartRegex(), line, out var match))
				{
					fourLineDescriptions[match.Groups[NAME].Value] = match.Groups[VALUE].Value;
				}
				else if (TryMatch(MoveDescriptionFourLineReferenceRegex(), line, out match))
				{
					fourLineDescriptionReferences[match.Groups[NAME].Value] = match.Groups[MARKER].Value;
				}
				else if (TryMatch(MoveDescriptionReferenceRegex(), line, out match))
				{
					twoLineDescriptionReferences[match.Groups[NAME].Value] = match.Groups[MARKER].Value;
				}
				else if (TryMatch(MoveDescriptionStartRegex(), line, out match))
				{
					currentTwoLineDescription = [];
					currentTwoLineName = match.Groups[NAME].Value;
				}
				else if (TryMatch(MoveDescriptionContinueRegex(), line, out match))
				{
					currentTwoLineDescription.Add(match.Groups[VALUE].Value);
				}
				else if (TryMatch(MoveDescriptionFinishRegex(), line, out match))
				{
					currentTwoLineDescription.Add(match.Groups[VALUE].Value);
					twoLineDescriptions[currentTwoLineName] = currentTwoLineDescription;
				}
			}

			return fourLineDescriptionReferences.Keys.ToDictionary(it => it, it => (
				twoLineDescriptions[twoLineDescriptionReferences[it]],
				fourLineDescriptions[fourLineDescriptionReferences[it]].Split("\\n").ToList()));
		}

		string ToTitleCase(string enumValue) => string.Concat(enumValue.Split('_').Select(it => it.ToLower()).Select(CultureInfo.InvariantCulture.TextInfo.ToTitleCase));

		private void WriteDescriptions(MoveList moves)
		{
			using var stream = File.Open(MoveDescriptionsFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			string twoLineName(string enumValue) => $"sMoveTwoLineDescription_{ToTitleCase(enumValue)}";
			string fourLineName(string enumValue) => $"sMoveFourLineDescription_{ToTitleCase(enumValue)}";

			var parseMoves = moves.Moves.Skip(1);

			foreach (var move in parseMoves)
			{
				var descriptions = move.DescriptionTwoLine.Where(it => !string.IsNullOrWhiteSpace(it));

				if (!descriptions.Any())
				{
					writer.WriteLine($"static const u8 ${twoLineName(move.EnumValue)}[] = _(");
					writer.WriteLine(Indent(1) + "\"\");");
				}
				else
				{
					writer.WriteLine($"static const u8 {twoLineName(move.EnumValue)}[] = _(");

					foreach (var line in descriptions.SkipLast(1))
					{
						writer.WriteLine(Indent(1) + $"\"{line}\\n\"");
					}
					writer.WriteLine(Indent(1) + $"\"{descriptions.Last()}\");");
				}
				writer.WriteLine();
			}

			writer.WriteLine("// MOVE_NONE is ignored in this table. Make sure to always subtract 1 before getting the right pointer.");
			writer.WriteLine("const u8 *const gMoveDescriptionPointers[MOVES_COUNT - 1] =");
			writer.WriteLine("{");

			foreach (var move in parseMoves)
			{
				writer.WriteLine(Indent(1) + $"[{move.EnumValue} - 1] = {twoLineName(move.EnumValue)},");
			}

			writer.WriteLine("};");
			writer.WriteLine();

			foreach (var move in parseMoves)
			{
				var descriptions = move.DescriptionFourLine.Where(it => !string.IsNullOrWhiteSpace(it));
				writer.WriteLine($"static const u8 {fourLineName(move.EnumValue)}[] = _(\"{string.Join("\\n", descriptions)}\");");
			}

			writer.WriteLine();
			writer.WriteLine("const u8 *const gMoveFourLineDescriptionPointers[MOVES_COUNT - 1] = {");

			foreach (var move in parseMoves)
			{
				writer.WriteLine(Indent(1) + $"[{move.EnumValue} - 1] = {fourLineName(move.EnumValue)},");
			}

			writer.WriteLine("};");
		}

		private readonly string MoveNamesFile = ErFolder + @"\src\data\text\move_names.h";

		[GeneratedRegex(@"^\s*\[\s*(?<name>MOVE_\w+)\s*\]\s+=\s*_\(""(?<value>[^""]+)""\),")]
		private static partial Regex MoveNameRegex();

		private const string LongNameStart = "const u8 gMoveNamesLong";

		private (Dictionary<string, string> shortName, Dictionary<string, string> longName) ReadNames()
		{
			using var stream = File.Open(MoveNamesFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using var reader = new StreamReader(stream);

			Dictionary<string, string> shortNames = [];
			Dictionary<string, string> longNames = [];

			Dictionary<string, string> writeTo = shortNames;

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine()!;

				if (line.Contains(LongNameStart))
				{
					writeTo = longNames;
				}
				else if (TryMatch(MoveNameRegex(), line, out var match))
				{
					writeTo[match.Groups[NAME].Value] = match.Groups[VALUE].Value;
				}
			}

			return (shortNames, longNames);
		}

		public MoveList ReadMoveList()
		{
			var (enums, references) = ReadEnums();

			var (flags1, flags2) = ReadFlags();

			var effects = ReadEffects();

			var (battleMoves, intimidateBlock) = ReadBattleMoves([.. effects], [.. flags1, .. flags2]);

			var (shortNames, longNames) = ReadNames();

			var descriptions = ReadMoveDescriptions();

			var animations = ReadAnimations().Zip(enums).ToDictionary(it => it.Second, it => it.First);

			return new(Moves: new(enums.Select(it => new Move(EnumValue: it,
				ShortName: shortNames.TryGetValue(it, out var name) ? name : it.Replace("_", ""),
				Name: longNames.TryGetValue(it, out name) ? name : it.Replace("_", ""),
				Animation: animations.TryGetValue(it, out var animation) ? animation : "Move_NONE",
				DescriptionTwoLine: descriptions.TryGetValue(it, out var moveDescription) ? [.. moveDescription.descriptionTwoLine!] : ["Not implemented."],
				DescriptionFourLine: descriptions.TryGetValue(it, out moveDescription) ? [.. moveDescription.descriptionFourLine!] : ["Not implemented."],
				BattleMove: battleMoves.TryGetValue(it, out var battleMove) ? battleMove : new(it)))),
			MoveReferences: [.. references!],
			Flags1: [.. flags1!],
			Flags2: [.. flags2!],
			Effects: [.. effects!],
			IntimidateBlock: intimidateBlock);
		}

		private void WriteNames(MoveList moves)
		{
			using var stream = File.Open(MoveNamesFile, FileMode.Create, FileAccess.Write, FileShare.None);
			using var writer = new StreamWriter(stream);

			writer.WriteLine("const u8 gMoveNames[MOVES_COUNT][MOVE_NAME_LENGTH + 1] =");
			writer.WriteLine("{");

			foreach (var move in moves.Moves)
			{
				writer.WriteLine(Indent(1) + $"[{move.EnumValue}] = _(\"{move.ShortName[0..Math.Min(move.ShortName.Length, SHORT_NAME_LENGTH)]}\"),");
			}

			writer.WriteLine("};");
			writer.WriteLine();
			writer.WriteLine("// Second table with longer move names for places where they fit.");
			writer.WriteLine("// TODO: Change both move name tables into a table of pointers so strings can be reused.");
			writer.WriteLine("const u8 gMoveNamesLong[MOVES_COUNT][LONG_MOVE_NAME_LENGTH + 1] =");
			writer.WriteLine("{");

			foreach (var move in moves.Moves)
			{
				writer.WriteLine(Indent(1) + $"[{move.EnumValue}] = _(\"{move.Name[0..Math.Min(move.Name.Length, LONG_NAME_LENGTH)]}\"),");
			}

			writer.WriteLine("};");
		}

		public void WriteMoveList(MoveList moveList)
		{
			WriteEnums(moveList);
			WriteBattleMoves(moveList);
			WriteFlags(moveList);
			WriteEffects(moveList);
			WriteAnimations(moveList);
			WriteDescriptions(moveList);
			WriteNames(moveList);
		}
	}
}
