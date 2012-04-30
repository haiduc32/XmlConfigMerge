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
		/// <summary>
		/// Opens a file containing XPath regex replacement values, and returns the loaded list.
		/// </summary>
		/// <param name="filename">The file with the XPath values.</param>
		/// <returns>The list of replacement values.</returns>
		public static List<XPathRegexReplacement> Load(string filename)
		{
			//"[^"]+"|\S+ should split by "param" param "param param"
			//spaces or ""
			Regex paramRegex = new Regex("\"[^\"]+\"|\\S+");

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

				MatchCollection paramMatches = paramRegex.Matches(trimmedLine);

				if (paramMatches.Count < 2)
				{
					throw new ApplicationException(
						string.Format("Found a line in file with not enough parameters. Line {0}", lineNumber));
				}

				string xPath = paramMatches[0].Value.Trim('"');
				string replaceWith = paramMatches[1].Value.Trim('"');

				Regex replacePattern = null;
				if (paramMatches.Count > 2) replacePattern = new Regex(paramMatches[2].Value.Trim('"'), RegexOptions.IgnoreCase);

				result.Add(new XPathRegexReplacement(xPath, replaceWith, replacePattern));
			}

			return result;
		}
	}
}
