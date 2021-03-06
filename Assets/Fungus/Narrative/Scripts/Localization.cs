﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ideafixxxer.CsvParser;

namespace Fungus
{

	/**
	 * Multi-language localization support.
	 */
	public class Localization : MonoBehaviour
	{
		/**
		 * Language to use at startup, usually defined by a two letter language code (e.g DE = German)
		 */
		[Tooltip("Language to use at startup, usually defined by a two letter language code (e.g DE = German)")]
		public string activeLanguage = "";

		protected static Dictionary<string, string> localizedStrings = new Dictionary<string, string>();

		/**
		 * Temp storage for a single item of standard text and its localizations
		 */
		protected class TextItem
		{
			public string description = "";
			public string standardText = "";
			public Dictionary<string, string> localizedStrings = new Dictionary<string, string>();
		}

		/**
		 * CSV file containing localization data which can be easily edited in a spreadsheet tool.
		 */
		 [Tooltip("CSV file containing localization data which can be easily edited in a spreadsheet tool")]
		 public TextAsset localizationFile;

		/**
		 * Stores any notification message from export / import methods.
		 */
		[NonSerialized]
		public string notificationText = "";

		public virtual void Start()
		{
			if (localizationFile != null &&
			    localizationFile.text.Length > 0)
			{
				SetActiveLanguage(activeLanguage);
			}
		}

		/**
		 * Looks up the specified string in the localized strings table.
		 * For this to work, a localization file and active language must have been set previously.
		 * Return null if the string is not found.
		 */
		public static string GetLocalizedString(string stringId)
		{
			if (localizedStrings == null)
			{
				return null;
			}

			if (localizedStrings.ContainsKey(stringId))
			{
				return localizedStrings[stringId];
			}

			return null;
		}

		/**
		 * Convert all text items and localized strings to an easy to edit CSV format.
		 */
		public virtual string GetCSVData()
		{
			// Collect all the text items present in the scene
			Dictionary<string, TextItem> textItems = FindTextItems();

			// Update text items with localization data from CSV file
			if (localizationFile != null &&
			    localizationFile.text.Length > 0)
			{
				AddCSVDataItems(textItems, localizationFile.text);
			}

			// Build CSV header row and a list of the language codes currently in use
			string csvHeader = "Key,Description,Standard";
			List<string> languageCodes = new List<string>();
			foreach (TextItem textItem in textItems.Values)
			{
				foreach (string languageCode in textItem.localizedStrings.Keys)
				{
					if (!languageCodes.Contains(languageCode))
					{
						languageCodes.Add(languageCode);
						csvHeader += "," + languageCode;
					}
				}
			}

			// Build the CSV file using collected text items
			int rowCount = 0;
			string csvData = csvHeader + "\n";
			foreach (string stringId in textItems.Keys)
			{
				TextItem textItem = textItems[stringId];

				string row = CSVSupport.Escape(stringId);
				row += "," + CSVSupport.Escape(textItem.description);
				row += "," + CSVSupport.Escape(textItem.standardText);

				foreach (string languageCode in languageCodes)
				{
					if (textItem.localizedStrings.ContainsKey(languageCode))
					{
						row += "," + CSVSupport.Escape(textItem.localizedStrings[languageCode]);
					}
					else
					{
						row += ","; // Empty field
					}
				}

				csvData += row + "\n";
				rowCount++;
			}

			notificationText = "Exported " + rowCount + " localization text items.";

			return csvData;
		}

		/**
		 * Builds a dictionary of localizable text items in the scene.
		 */
		protected Dictionary<string, TextItem> FindTextItems()
		{
			Dictionary<string, TextItem> textItems = new Dictionary<string, TextItem>();

			// Export all character names
			foreach (Character character in GameObject.FindObjectsOfType<Character>())
			{
				// String id for character names is CHARACTER.<Character Name>
				TextItem textItem = new TextItem();
				textItem.standardText = character.nameText;
				textItem.description = character.description;
				string stringId = "CHARACTER." + character.nameText;
				textItems[stringId] = textItem;
			}

			// Export all Say and Menu commands in the scene
			// To make it easier to localize, we preserve the command order in each exported block.
			Flowchart[] flowcharts = GameObject.FindObjectsOfType<Flowchart>();
			foreach (Flowchart flowchart in flowcharts)
			{
				// If no localization id has been set then use the Flowchart name
				string localizationId = flowchart.localizationId;
				if (localizationId.Length == 0)
				{
					localizationId = flowchart.name;
				}

				Block[] blocks = flowchart.GetComponentsInChildren<Block>();
				foreach (Block block in blocks)
				{
					foreach (Command command in block.commandList)
					{
						string stringId = "";
						string standardText = "";
						string description = "";

						System.Type type = command.GetType();
						if (type == typeof(Say))
						{
							// String id for Say commands is SAY.<Flowchart id>.<Command id>.<Character Name>
							Say sayCommand = command as Say;
							standardText = sayCommand.storyText;
							description = sayCommand.description;
							stringId = "SAY." + localizationId + "." + sayCommand.itemId + ".";
							if (sayCommand.character != null)
							{
								stringId += sayCommand.character.nameText;
							}
						}
						else if (type == typeof(Menu))
						{							
							// String id for Menu commands is MENU.<Flowchart id>.<Command id>
							Menu menuCommand = command as Menu;
							standardText = menuCommand.text;
							description = menuCommand.description;
							stringId = "MENU." + localizationId + "." + menuCommand.itemId;
						}
						else
						{
							continue;
						}
						
						TextItem textItem = null;
						if (textItems.ContainsKey(stringId))
						{
							textItem = textItems[stringId];
						}
						else
						{
							textItem = new TextItem();
							textItems[stringId] = textItem;
						}
						
						// Update basic properties,leaving localised strings intact
						textItem.standardText = standardText;
						textItem.description = description;
					}
				}
			}
			
			return textItems;
		}

		/**
		 * Adds localized strings from CSV file data to a dictionary of text items in the scene.
		 */
		protected virtual void AddCSVDataItems(Dictionary<string, TextItem> textItems, string csvData)
		{
			CsvParser csvParser = new CsvParser();
			string[][] csvTable = csvParser.Parse(csvData);

			if (csvTable.Length <= 1)
			{
				// No data rows in file
				return;
			}

			// Parse header row
			string[] columnNames = csvTable[0];
			
			for (int i = 1; i < csvTable.Length; ++i)
			{
				string[] fields = csvTable[i];
				if (fields.Length < 3)
				{
					// No standard text or localized string fields present
					continue;
				}
				
				string stringId = fields[0];

				if (!textItems.ContainsKey(stringId))
				{
					if (stringId.StartsWith("CHARACTER.") || 
					    stringId.StartsWith("SAY.") || 
					    stringId.StartsWith("MENU."))
					{
						// If it's a 'built-in' type this probably means that item has been deleted from its flowchart,
						// so there's no need to add a text item for it.
						continue;
					}

					// Key not found. Assume it's a custom string that we want to retain, so add a text item for it.
					TextItem newTextItem = new TextItem();
					newTextItem.description = CSVSupport.Unescape(fields[1]);
					newTextItem.standardText = CSVSupport.Unescape(fields[2]);
					textItems[stringId] = newTextItem;
				}

				TextItem textItem = textItems[stringId];

				for (int j = 3; j < fields.Length; ++j)
				{
					if (j >= columnNames.Length)
					{
						continue;
					}
					string languageCode = columnNames[j];
					string languageEntry = CSVSupport.Unescape(fields[j]);
					
					if (languageEntry.Length > 0)
					{
						textItem.localizedStrings[languageCode] = languageEntry;
					}
				}
			}
		}

		/**
		 * Scan a localization CSV file and copies the strings for the specified language code
		 * into the text properties of the appropriate scene objects.
		 */
		public virtual void SetActiveLanguage(string languageCode, bool forceUpdateSceneText = false)
		{
			if (!Application.isPlaying)
			{
				// This function should only ever be called when the game is playing (not in editor).
				return;
			}

			if (localizationFile == null)
			{
				// No localization file set
				return;
			}

			localizedStrings.Clear();

			CsvParser csvParser = new CsvParser();
			string[][] csvTable = csvParser.Parse(localizationFile.text);

			if (csvTable.Length <= 1)
			{
				// No data rows in file
				return;
			}

			// Parse header row
			string[] columnNames = csvTable[0];

			if (columnNames.Length < 3)
			{
				// No languages defined in CSV file
				return;
			}

			// First assume standard text column and then look for a matching language column
			int languageIndex = 2;
			for (int i = 3; i < columnNames.Length; ++i)
			{
				if (columnNames[i] == languageCode)
				{
					languageIndex = i;
					break;
				}
			}

			if (languageIndex == 2)
			{
				// Using standard text column
				// Add all strings to the localized strings dict, but don't replace standard text in the scene.
				// This allows string substitution to work for both standard and localized text strings.
				for (int i = 1; i < csvTable.Length; ++i)
				{
					string[] fields = csvTable[i];
					if (fields.Length < 3)
					{
						continue;
					}

					localizedStrings[fields[0]] = fields[languageIndex];
				}

				// Early out unless we've been told to force the scene text to update.
				// This happens when the Set Language command is used to reset back to the standard language.
				if (!forceUpdateSceneText)
				{
					return;
				}
			}

			// Using a localized language text column
			// 1. Add all localized text to the localized strings dict
			// 2. Update all scene text properties with localized versions

			// Cache a lookup table of characters in the scene
			Dictionary<string, Character> characterDict = new Dictionary<string, Character>();
			foreach (Character character in GameObject.FindObjectsOfType<Character>())
			{
				characterDict[character.nameText] = character;
			}

			// Cache a lookup table of flowcharts in the scene
			Dictionary<string, Flowchart> flowchartDict = new Dictionary<string, Flowchart>();
			foreach (Flowchart flowchart in GameObject.FindObjectsOfType<Flowchart>())
			{
				// If no localization id has been set then use the Flowchart name
				string localizationId = flowchart.localizationId;
				if (localizationId.Length == 0)
				{
					localizationId = flowchart.name;
				}

				flowchartDict[localizationId] = flowchart;
			}

			for (int i = 1; i < csvTable.Length; ++i)
			{
				string[] fields = csvTable[i];

				if (fields.Length < languageIndex + 1)
				{
					continue;
				}
				
				string stringId = fields[0];
				string languageEntry = CSVSupport.Unescape(fields[languageIndex]);
					
				if (languageEntry.Length > 0)
				{
					localizedStrings[stringId] = languageEntry;
					PopulateTextProperty(stringId, languageEntry, flowchartDict, characterDict);
				}
			}
		}

		/**
		 * Populates the text property of a single scene object with a new text value.
		 */
		public virtual bool PopulateTextProperty(string stringId, 
		                                       	 string newText, 
		                                       	 Dictionary<string, Flowchart> flowchartDict,
		                                       	 Dictionary<string, Character> characterDict)
		{
			string[] idParts = stringId.Split('.');
			if (idParts.Length == 0)
			{
				return false;
			}

			string stringType = idParts[0];
			if (stringType == "SAY")
			{
				if (idParts.Length != 4)
				{
					return false;
				}

				string flowchartId = idParts[1];
				if (!flowchartDict.ContainsKey(flowchartId))
				{
					return false;
				}
				Flowchart flowchart = flowchartDict[flowchartId];
	
				int itemId = int.Parse(idParts[2]);
				
				if (flowchart != null)
				{
					foreach (Say say in flowchart.GetComponentsInChildren<Say>())
					{
						if (say.itemId == itemId &&
						    say.storyText != newText)
						{
							#if UNITY_EDITOR
							Undo.RecordObject(say, "Set Text");
							#endif

							say.storyText = newText;
							return true;
						}
					}
				}
			}
			else if (stringType == "MENU")
			{
				if (idParts.Length != 3)
				{
					return false;
				}
				
				string flowchartId = idParts[1];
				if (!flowchartDict.ContainsKey(flowchartId))
				{
					return false;
				}
				Flowchart flowchart = flowchartDict[flowchartId];

				int itemId = int.Parse(idParts[2]);
				
				if (flowchart != null)
				{
					foreach (Menu menu in flowchart.GetComponentsInChildren<Menu>())
					{
						if (menu.itemId == itemId &&
						    menu.text != newText)
						{
							#if UNITY_EDITOR
							Undo.RecordObject(menu, "Set Text");
							#endif

							menu.text = newText;
							return true;
						}
					}
				}
			}
			else if (stringType == "CHARACTER")
			{
				if (idParts.Length != 2)
				{
					return false;
				}
				
				string characterName = idParts[1];
				if (!characterDict.ContainsKey(characterName))
				{
					return false;
				}

				Character character = characterDict[characterName];
				if (character != null &&
				    character.nameText != newText)
				{
					#if UNITY_EDITOR
					Undo.RecordObject(character, "Set Text");
					#endif

					character.nameText = newText;
					return true;
				}
			}

			return false;
		}

		/**
		 * Returns all standard text for SAY & MENU commands in the scene using an
		 * easy to edit custom text format.
		 */
		public virtual string GetStandardText()
		{
			// Collect all the text items present in the scene
			Dictionary<string, TextItem> textItems = FindTextItems();

			string textData = "";
			int rowCount = 0;
			foreach (string stringId in textItems.Keys)
			{
				if (!stringId.StartsWith("SAY.") && !(stringId.StartsWith("MENU.")))
				{
					continue;
				}

				TextItem languageItem = textItems[stringId];

				textData += "#" + stringId + "\n";
				textData += languageItem.standardText.Trim() + "\n\n";
				rowCount++;
			}

			notificationText = "Exported " + rowCount + " standard text items.";
			
			return textData;
		}

		/**
		 * Sets standard text on scene objects by parsing a text data file.
		 */
		public virtual void SetStandardText(string textData)
		{
			// Cache a lookup table of characters in the scene
			Dictionary<string, Character> characterDict = new Dictionary<string, Character>();
			foreach (Character character in GameObject.FindObjectsOfType<Character>())
			{
				characterDict[character.nameText] = character;
			}
			
			// Cache a lookup table of flowcharts in the scene
			Dictionary<string, Flowchart> flowchartDict = new Dictionary<string, Flowchart>();
			foreach (Flowchart flowchart in GameObject.FindObjectsOfType<Flowchart>())
			{
				// If no localization id has been set then use the Flowchart name
				string localizationId = flowchart.localizationId;
				if (localizationId.Length == 0)
				{
					localizationId = flowchart.name;
				}

				flowchartDict[localizationId] = flowchart;
			}

			string[] lines = textData.Split('\n');

			int updatedCount = 0;

			string stringId = "";
			string buffer = "";
			foreach (string line in lines)
			{
				// Check for string id line	
				if (line.StartsWith("#"))
				{
					if (stringId.Length > 0)
					{
						// Write buffered text to the appropriate text property
						if (PopulateTextProperty(stringId, buffer.Trim(), flowchartDict, characterDict))
						{
							updatedCount++;
						}
					}

					// Set the string id for the follow text lines
					stringId = line.Substring(1, line.Length - 1);
					buffer = "";
				}
				else
				{
					buffer += line;
				}
			}

			// Handle last buffered entry
			if (stringId.Length > 0)
			{
				if (PopulateTextProperty(stringId, buffer.Trim(), flowchartDict, characterDict))
				{
					updatedCount++;
				}
			}

			notificationText = "Updated " + updatedCount + " standard text items.";
		}
	}

}