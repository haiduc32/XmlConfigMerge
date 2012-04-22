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
			List<XPathRegexReplacement> result = new List<XPathRegexReplacement>();
			int lineNumber = 0;
			foreach (string line in File.ReadAllLines(filename))
			{
				lineNumber++;
				//skip comments
				if (line.TrimStart().StartsWith("--")) continue;

				string[] split = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				//check that we have enough data on the line
				if (split.Count() < 2)
				{
					throw new ApplicationException(
						string.Format("Found a line in file with only one parameter. Line {0}", lineNumber));
				}

				string xPath = split[0];
				string replaceWith = split[1];

				//if we have at least 3 separate values, than it is the regex pattern
				Regex replacePattern = null;
				if (split.Count() > 2) replacePattern = new Regex(split[2], RegexOptions.IgnoreCase);

				result.Add(new XPathRegexReplacement(xPath, replaceWith, replacePattern));
			}

			return result;
		}
	}
}
