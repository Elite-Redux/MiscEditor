using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AbilityEditor
{
	public class Ability(string EnumValue, string Name, List<string> Description)
	{
		public string EnumValue { get; } = EnumValue;
		public string Name { get; } = Name;
		public List<string> Description { get; } = Description.Concat(["", ""]).Take(2).ToList();
	}

	public record AbilityMarker(string DefineName, string EnumValue, int Offset);

	public record AbilityList(ObservableCollection<Ability> Abilities, List<AbilityMarker> AbilityMarkers);

	partial class AbilityLoader(string ErDestination)
	{
		private string EnumPath { get; } = $"{ErDestination}/include/constants/abilities.h";
		private string TextPath { get; } = $"{ErDestination}/src/data/text/abilities.h";

		private const string NAME = "name";
		private const string MARKER = "marker";
		private const string VALUE = "value";
		private const string OFFSET = "offset";

		[GeneratedRegex(@"^\s*#define\s+(?<name>ABILITY_\w+)\s+(?<value>\d+)\s*(//.*)?$")]
		private static partial Regex AbilityLine();
		[GeneratedRegex(@"^\s*#define\s+(?<name>ABILITY_\w+)\s+\(\s*(?<marker>ABILITIES_\w+)(\s+\+\s+(?<offset>\d+))?\)\s*(//.*)?$")]
		private static partial Regex AbilityLineReferential();
		[GeneratedRegex(@"^\s*#define\s+(?<name>ABILITIES_\w+)\s+(?<value>\d+)\s*(//.*)?$")]
		private static partial Regex MarkerLine();
		[GeneratedRegex(@"^\s*#define\s+(?<name>ABILITIES_\w+)\s+\(\s*(?<marker>ABILITY_\w+)(\s+\+\s+(?<offset>\d+)?)\)\s*(//.*)?$")]
		private static partial Regex MarkerLineReferential();

		private (List<string> abilities, List<AbilityMarker> abilityMarkers) ReadEnums()
		{
			using var abilityStream = File.Open(EnumPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using StreamReader abilityReader = new(abilityStream);

			List<string> abilityEnums = [];
			Dictionary<string, AbilityMarker> markers = [];
			Dictionary<string, int> intEnums = [];

			while (!abilityReader.EndOfStream)
			{
				string line = abilityReader.ReadLine()!;
				if (TryMatch(AbilityLine(), line, out var match))
				{
					intEnums[match.Groups[NAME].Value] = int.Parse(match.Groups[VALUE].Value);
				}
				else if (TryMatch(AbilityLineReferential(), line, out match))
				{
					int offset = match.Groups[OFFSET].Success ? int.Parse(match.Groups[OFFSET].Value) : 0;
					AbilityMarker marker = markers[match.Groups[MARKER].Value];
					intEnums[match.Groups[NAME].Value] = intEnums[marker.EnumValue] + marker.Offset + offset;
				}
				else if (TryMatch(MarkerLine(), line, out match))
				{
					int number = int.Parse(match.Groups[VALUE].Value);
					markers[match.Groups[NAME].Value] = new(DefineName: match.Groups[NAME].Value,
						EnumValue: intEnums.First(it => it.Value == number - 1)!.Key,
						Offset: 1);
				}
				else if (TryMatch(MarkerLineReferential(), line, out match))
				{
					markers[match.Groups[NAME].Value] = new(DefineName: match.Groups[NAME].Value,
						EnumValue: match.Groups[MARKER].Value,
						Offset: match.Groups[OFFSET].Success ? int.Parse(match.Groups[OFFSET].Value) : 0);
				}
			}
			abilityEnums = [.. intEnums.Keys.OrderBy(it => intEnums[it])];

			return (abilityEnums, markers.Values.ToList());
		}

		[GeneratedRegex(@"^\s*static const u8 (?<name>s\w+)\s*\[\] = _\(""(?<value>[^""]*)""\);+\s*(//.*)?$")]
		private static partial Regex DescriptionLine();
		[GeneratedRegex(@"^\s*\[(?<name>ABILITY_\w+)\]\s+=\s+_\(""(?<value>[^""]*)""\)\s*,?\s*(//.*)?$")]
		private static partial Regex AbilityNameLine();
		[GeneratedRegex(@"^\s*\[(?<name>ABILITY_\w+)\]\s+=\s+(?<value>s\w+)\s*,?\s*(//.*)?$")]
		private static partial Regex AbilityDescriptionReferenceLine();

		private (Dictionary<string, string> descriptions, Dictionary<string, string> names, Dictionary<string, string> descriptionReferences) ReadText()
		{
			using FileStream textStream = File.Open(TextPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using StreamReader textReader = new(textStream);
			Dictionary<string, string> descriptions = [];
			Dictionary<string, string> names = [];
			Dictionary<string, string> descriptionReferences = [];
			while (!textReader.EndOfStream)
			{
				string line = textReader.ReadLine()!;
				if (TryMatch(DescriptionLine(), line, out var match))
				{
					descriptions[match.Groups[NAME].Value] = match.Groups[VALUE].Value;
				}
				else if (TryMatch(AbilityNameLine(), line, out match))
				{
					names[match.Groups[NAME].Value] = match.Groups[VALUE].Value;
				}
				else if (TryMatch(AbilityDescriptionReferenceLine(), line, out match))
				{
					descriptionReferences[match.Groups[NAME].Value] = match.Groups[VALUE].Value;
				}
			}

			return (descriptions, names, descriptionReferences);
		}

		private static bool TryMatch(Regex regex, string line, out Match match)
		{
			match = regex.Match(line);
			return match.Success;
		}

		public AbilityList GetAbilities()
		{
			var (abilities, markers) = ReadEnums();
			var (descriptions, names, descriptionReferences) = ReadText();

			return new(AbilityMarkers: [.. markers],
				Abilities: new(abilities.Select(it => new Ability(
					EnumValue: it,
					Name: names[it],
					Description: [.. descriptions[descriptionReferences[it]].Split(@"\n")]))));
		}

		public void WriteEnums(AbilityList abilityList)
		{
			using var fileStream = File.Open(EnumPath, FileMode.Create, FileAccess.Write, FileShare.None);
			using StreamWriter writer = new(fileStream);

			Dictionary<string, AbilityMarker> markers = abilityList.AbilityMarkers.Where(it => it.DefineName != "ABILITIES_COUNT_CUSTOM").ToDictionary(it => it.EnumValue);
			writer.WriteLine("#ifndef GUARD_CONSTANTS_ABILITIES_H");
			writer.WriteLine("#define GUARD_CONSTANTS_ABILITIES_H");
			writer.WriteLine();
			int value = 0;
			foreach (var ability in abilityList.Abilities)
			{
				writer.WriteLine($"#define {ability.EnumValue} {value++} // {string.Join(" ", ability.Description)}".TrimEnd(' ', '/'));
				if (markers.TryGetValue(ability.EnumValue, out var marker))
				{
					writer.WriteLine();
					writer.WriteLine($"#define {marker.DefineName} ({marker.EnumValue}{(marker.Offset != 0 ? " + " + marker.Offset.ToString() : "")})");
					writer.WriteLine();
				}
			}
			writer.WriteLine();
			writer.WriteLine($"#define ABILITIES_COUNT_CUSTOM ({abilityList.Abilities.Last().EnumValue} + 1)");
			writer.WriteLine();
			writer.WriteLine("#define ABILITIES_COUNT ABILITIES_COUNT_CUSTOM");
			writer.WriteLine();
			writer.WriteLine("#endif  // GUARD_CONSTANTS_ABILITIES_H");
		}

		public void WriteText(AbilityList abilityList)
		{
			using var fileStream = File.Open(TextPath, FileMode.Create, FileAccess.Write, FileShare.None);
			using StreamWriter writer = new(fileStream);

			string GetConstName(string enumValue) => "s"
				+ string.Concat(enumValue.Split('_').Select(it => it.ToLower()).Select(CultureInfo.InvariantCulture.TextInfo.ToTitleCase))
				+ "Description";

			foreach (var ability in abilityList.Abilities)
			{
				writer.WriteLine($"static const u8 {GetConstName(ability.EnumValue)}[] = "
					+ $"_(\"{string.Join(@"\n", ability.Description.Take(2).Where(it => !string.IsNullOrWhiteSpace(it)))}\");");
			}

			writer.WriteLine();
			writer.WriteLine("const u8 gAbilityNames[ABILITIES_COUNT][ABILITY_NAME_LENGTH + 1] =");
			writer.WriteLine("{");

			foreach (var ability in abilityList.Abilities)
			{
				writer.WriteLine($"    [{ability.EnumValue}] = _(\"{ability.Name}\"),");
			}

			writer.WriteLine("};");
			writer.WriteLine();
			writer.WriteLine("const u8 *const gAbilityDescriptionPointers[ABILITIES_COUNT] =");
			writer.WriteLine("{");

			foreach (var ability in abilityList.Abilities)
			{
				writer.WriteLine($"    [{ability.EnumValue}] = {GetConstName(ability.EnumValue)},");
			}

			writer.WriteLine("};");
		}
	}
}
