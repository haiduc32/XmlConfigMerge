using System;
using System.Text.RegularExpressions;
using System.Net;

namespace Tools.XmlConfigMergeConsole
{
	/// <summary>
	/// Summary description for XPathRegexReplacements.
	/// </summary>
	public class XPathRegexReplacement
	{
		public XPathRegexReplacement(string xPath, string replaceWith, Regex replacePattern)
		{
			XPath = xPath;
			ReplaceWith = WebUtility.HtmlDecode(replaceWith);
			ReplacePattern = replacePattern;
		}

		public string XPath = null;
		public string ReplaceWith = null;
		public Regex ReplacePattern = null;
	}
}
