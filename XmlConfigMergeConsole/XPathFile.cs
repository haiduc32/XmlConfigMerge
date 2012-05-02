using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace Tools.XmlConfigMergeConsole
{
	/// <summary>
	/// Class for loading XPaths and substitutions from a file.
	/// </summary>
	internal static class XPathFile
	{
		//"[^"]+"|\S+ should split by "param" param "param param"
		//spaces or ""
		private static Regex paramRegex = new Regex("\"[^\"]+\"|\\S+");

		/// <summary>
		/// Opens a file containing XPath regex replacement values, and returns the loaded list.
		/// </summary>
		/// <param name="filename">The file with the XPath values.</param>
		/// <returns>The list of replacement values.</returns>
		public static List<XPathRegexReplacement> Load(string filename)
		{
			List<XPathRegexReplacement> result = new List<XPathRegexReplacement>();
			int lineNumber = 0;
			foreach (string line in File.ReadAllLines(filename))
			{
				lineNumber++;
				//skip comments
				if (line.TrimStart().StartsWith("--")) continue;

                //check that the line has any chars
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

				List<string> splitParameters = ParseParams(trimmedLine);

				if (splitParameters.Count < 2)
				{
					throw new ApplicationException(
						string.Format("Found a line in file with not enough parameters. Line {0}", lineNumber));
				}

				string xPath = splitParameters[0];
				string replaceWith = splitParameters[1];

				Regex replacePattern = null;
				if (splitParameters.Count > 2) replacePattern = new Regex(splitParameters[2], RegexOptions.IgnoreCase);

				result.Add(new XPathRegexReplacement(xPath, replaceWith, replacePattern));
			}

			return result;
		}

		private static List<string> ParseParams(string paramLine)
		{
			return paramRegex.Matches(paramLine).OfType<Match>().Select(x => x.Value.Trim('"', ' ')).ToList();
		}
	}
}
