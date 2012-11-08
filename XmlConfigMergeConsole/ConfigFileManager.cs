using System;
using System.Xml;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace Tools.XmlConfigMerge
{
	/// <summary>
	/// Manages config file reading and writing, and optional merging of two config files.
	/// </summary>
	internal class ConfigFileManager
	{
		#region private fields

		private string _configPath;
		private string _masterConfigPath;
		private string _fromLastInstallConfigPath;
		private readonly string _optionalCode;
		private readonly Regex _optionalCodeRegex;
		private XmlDocument _xmlDocument = new XmlDocument();

		#endregion private fields

		#region properties

		public string ConfigPath
		{
			get { return _configPath; }
		}

		public string MasterConfigPath
		{
			get { return _masterConfigPath; }
		}

		public string FromLastInstallConfigPath
		{
			get { return _fromLastInstallConfigPath; }
		}

		public string OptionalCode
		{
			get { return _optionalCode; }
		}

		public XmlDocument XmlDocument
		{
			get { return _xmlDocument; }
		}

		#endregion properties

		#region .ctor

		/// <summary>
		/// Instantiates a new ConfigFileManager.
		/// </summary>
		/// <param name="masterConfigPath"></param>
		/// <param name="mergeFromConfigPath"></param>
		/// <param name="makeMergeFromConfigPathTheSavePath"></param>
		/// <param name="optionalCode">Will check the xpath that start with #code against this code. 
		/// If they will not match, they will be ignored.</param>
		/// <exception cref="Exception">if mergeFromConfigPath is specified but does not exist, and 
		/// makeMergeFromConfigPathTheSavePath is false</exception>
		public ConfigFileManager(string masterConfigPath, string mergeFromConfigPath,
			bool makeMergeFromConfigPathTheSavePath, string optionalCode)
		{
			_masterConfigPath = masterConfigPath;
			_configPath = masterConfigPath;
			_fromLastInstallConfigPath = mergeFromConfigPath;

			_optionalCode = optionalCode;
			if (!string.IsNullOrEmpty(_optionalCode))
			{
				_optionalCodeRegex = new Regex(@"\G#[a-zA-Z0-9_-]+", RegexOptions.IgnoreCase);
			}

			if (mergeFromConfigPath != null && (!File.Exists(mergeFromConfigPath)) && !makeMergeFromConfigPathTheSavePath)
			{
				throw new ApplicationException("Specified mergeFromConfigPath does not exist: " + mergeFromConfigPath);
			}

			try
			{
				using (FileStream rd = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					_xmlDocument.Load(rd);
				}
			}
			catch (Exception ex)
			{
				throw new ApplicationException("Could not open '" + ConfigPath + "': " + ex.Message);
			}

			if (mergeFromConfigPath != null
					&& File.Exists(mergeFromConfigPath))
			{
				Debug.WriteLine("Merging from " + mergeFromConfigPath);
				//Merge approach:
				//	x Use from-last-install config as base
				//	x Merge in any non-existing keyvalue pairs from distrib-config
				//	x Use this merged config, and leave the last-install-config file in place for reference
				//Merge, preserving any comments from distrib-one only

				XmlDocument reInstallConfig = new XmlDocument();
				try
				{
					reInstallConfig.Load(mergeFromConfigPath);
				}
				catch (Exception ex)
				{
					_fromLastInstallConfigPath = null;
					throw new ApplicationException("Could not read existing config '"
							+ mergeFromConfigPath
							+ "': "
							+ ex.Message,
							ex);
				}

				UpdateExistingElementsAndAttribs(reInstallConfig, _xmlDocument);
			}

			if (makeMergeFromConfigPathTheSavePath)
			{
				_configPath = _fromLastInstallConfigPath;
			}
		}

		#endregion .ctor

		#region public methods

		/// <summary>
		/// Search and replace on one or more specified values as specified by a single xpath expressino
		/// </summary>
		/// <param name="xPath"></param>
		/// <param name="replaceWith"></param>
		/// <param name="regexPattern">Optionally specify a regex pattern to search within the found values. 
		///	If a single () group is found within this expression, only this portion is replaced.</param>
		/// <returns></returns>
		/// <exception cref="ApplicationException">When no nodes match xpath-expression and no regexPattern is specified (is null), 
		/// and can't auto-create the node (is not an appSettings expression).</exception>
		public string[] ReplaceXPathValues(string xPath, string replaceWith, Regex regexPattern)
		{
			if (xPath == null || xPath == string.Empty)
			{
				throw new ApplicationException("Required xPath is blank or null");
			}
			if (replaceWith == null)
			{
				throw new ApplicationException("Required replaceWith is null");
			}

			//check that if the xpath starts with an optional code, it matches the current optional code
			if (!string.IsNullOrEmpty(_optionalCode))
			{
				bool foundMatch = false;
				MatchCollection matches = _optionalCodeRegex.Matches(xPath);

				if (matches.Count > 0)
				{
					foreach (Match match in matches)
					{
						if (string.Compare(match.Value.TrimStart('#'), _optionalCode) == 0)
						{
							//have to remove all optional values from the xPath because it is not a standard construction
							int matchesLength = matches.OfType<Match>().Sum(x => x.Length);
							xPath = xPath.Remove(0, matchesLength);

							//we found a match, so we can break the iteration
							foundMatch = true;
							break;
						}
					}

					if (!foundMatch) return null;
				}
			}

			ArrayList ret = new ArrayList();
			XmlNodeList nodes = _xmlDocument.SelectNodes(xPath);

			//if no existing nodes match skip
			if (nodes.Count == 0)
			{
				return null;
			}

			//Proceed with replacements
			bool replacedOneOrMore = false;
			foreach (XmlNode node in nodes)
			{
				string replText = null;
				if (regexPattern != null)
				{
					Match match = regexPattern.Match(node.InnerText);
					if (match.Success)
					{
						//Determine if group match applies
						switch (match.Groups.Count)
						{
							case 1:
								replText = regexPattern.Replace(node.InnerText, replaceWith);
								break;
							case 2:
								replText = string.Empty;
								if (match.Groups[2 - 1].Index > 0)
								{
									replText += node.InnerText.Substring(0, match.Groups[2 - 1].Index);
								}
								replText += replaceWith;
								int firstPostGroupPos = match.Groups[2 - 1].Index + match.Groups[2 - 1].Length;
								if (node.InnerText.Length > firstPostGroupPos)
								{
									replText += node.InnerText.Substring(firstPostGroupPos);
								}
								break;
							default:
								throw new ApplicationException("> 1 regex replace group not supported ("
										+ match.Groups.Count + ") for regex expr: '"
										+ regexPattern.ToString() + "'");
						}
					}
				}
				else
				{
					replText = replaceWith;
				}

				if (replText != null)
				{
					replacedOneOrMore = true;
					node.InnerText = replText;
					if (node is XmlAttribute)
					{
						XmlAttribute keyAttrib = ((XmlAttribute)node).OwnerElement.Attributes["key"];
						if (keyAttrib == null)
						{
							ret.Add(((XmlAttribute)node).OwnerElement.Name + "/@" + node.Name + " set to '" + replText + "'");
						}
						else
						{
							ret.Add(((XmlAttribute)node).OwnerElement.Name + "[@key='" + keyAttrib.InnerText + "']/@" + node.Name + " set to '" + replText + "'");
						}

					}
					else
					{
						ret.Add(node.Name + " set to '" + replText);
					}
				}
			}

			if (!replacedOneOrMore)
			{
				ret.Add("No values matched replace pattern '"
						+ regexPattern.ToString()
						+ "' for '"
						+ xPath + "'");
			}

			return (string[])ret.ToArray(typeof(string));
		}

		public void Save()
		{
			Save(ConfigPath);
		}

		public void Save(string saveAsname)
		{
			//Ensure not set r/o
			if (File.Exists(saveAsname))
			{
				ClearSpecifiedFileAttributes(saveAsname, FileAttributes.ReadOnly);
			}

			_xmlDocument.Save(saveAsname);
		}

		/// <summary>
		/// Merge element and attribute values from one xml doc to another.
		/// </summary>
		/// <param name="fromXdoc"></param>
		/// <param name="toXdoc"></param>
		/// <remarks>
		/// Multiple same-named peer elements, are merged in the ordinal order they appear.
		/// </remarks>
		public static void UpdateExistingElementsAndAttribs(XmlDocument fromXdoc, XmlDocument toXdoc)
		{
			UpdateExistingElementsAndAttribsRecurse(fromXdoc.ChildNodes, toXdoc);
		}

		#endregion public methods

		#region private methods

		private void ClearSpecifiedFileAttributes(string path, FileAttributes fileAttributes)
		{
			File.SetAttributes(path, File.GetAttributes(path) & (FileAttributes)((FileAttributes)0x7FFFFFFF - fileAttributes));
		}

		private static void UpdateExistingElementsAndAttribsRecurse(XmlNodeList fromNodes, XmlNode toParentNode)
		{
			int iSameElement = 0;
			XmlNode lastElement = null;
			foreach (XmlNode node in fromNodes)
			{
				if (node.NodeType != XmlNodeType.Element)
				{
					continue;
				}

				if (lastElement != null
						&& node.Name == lastElement.Name && node.NamespaceURI == lastElement.NamespaceURI)
				{
					iSameElement++;
				}
				else
				{
					iSameElement = 0;
				}
				lastElement = node;

				XmlNode toNode;
				if (node.Attributes["key"] != null)
				{
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, node.Attributes["key"]);
				}
				else if (node.Attributes["name"] != null)
				{
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, node.Attributes["name"]);
				}
				else if (node.Attributes["type"] != null)
				{
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, node.Attributes["type"]);
				}
				else
				{
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, iSameElement);
				}

				if (toNode == null)
				{
					if (node == null)
					{
						throw new ApplicationException("node == null");
					}
					if (node.Name == null)
					{
						throw new ApplicationException("node.Name == null");
					}
					if (toParentNode == null)
					{
						throw new ApplicationException("toParentNode == null");
					}
					if (toParentNode.OwnerDocument == null)
					{
						throw new ApplicationException("toParentNode.OwnerDocument == null");
					}

					Debug.WriteLine("app: " + toParentNode.Name + "/" + node.Name);
					if (node.ParentNode.Name != toParentNode.Name)
					{
						throw new ApplicationException("node.ParentNode.Name != toParentNode.Name: " + node.ParentNode.Name + " !=" + toParentNode.Name);
					}
					try
					{
						toNode = toParentNode.AppendChild(toParentNode.OwnerDocument.CreateElement(node.Name));
					}
					catch (Exception ex)
					{
						throw new ApplicationException("ex during toNode = toParentNode.AppendChild(: " + ex.Message);
					}
				}

				//Copy element content if any
				XmlNode textEl = GetTextElement(node);
				if (textEl != null)
				{
					toNode.InnerText = textEl.InnerText;
				}

				//Copy attribs if any
				foreach (XmlAttribute attrib in node.Attributes)
				{
					XmlAttribute toAttrib = toNode.Attributes[attrib.Name];
					if (toAttrib == null)
					{
						Debug.WriteLine("attr: " + toNode.Name + "@" + attrib.Name);
						toAttrib = toNode.Attributes.Append(toNode.OwnerDocument.CreateAttribute(attrib.Name));
					}
					toAttrib.InnerText = attrib.InnerText;
				}
				((XmlElement)toNode).IsEmpty = !toNode.HasChildNodes; //Ensure no endtag when not needed
				UpdateExistingElementsAndAttribsRecurse(node.ChildNodes, toNode);
			}
		}

		private static XmlNode GetTextElement(XmlNode node)
		{
			foreach (XmlNode subNode in node.ChildNodes)
			{
				if (subNode.NodeType == XmlNodeType.Text)
				{
					return subNode;
				}
			}

			return null;
		}

		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName, int iSameElement)
		{
			return SelectSingleNodeMatchingNamespaceURI(node, nodeName, null, iSameElement);
		}

		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName, XmlAttribute keyAttrib)
		{
			return SelectSingleNodeMatchingNamespaceURI(node, nodeName, keyAttrib, 0); //, null);
		}

		private static Regex _typeParsePattern = new Regex(@"([^,]+),");

		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName, XmlAttribute keyAttrib, int iSameElement)
		{
			XmlNode matchNode = null;
			int iNodeNameElements = 0 - 1;
			foreach (XmlNode subNode in node.ChildNodes)
			{
				if (subNode.Name != nodeName.Name || subNode.NamespaceURI != nodeName.NamespaceURI)
				{
					continue;
				}

				iNodeNameElements++;

				if (keyAttrib == null)
				{
					if (iNodeNameElements == iSameElement)
					{
						return subNode;
					}
					else
					{
						continue;
					}
				}

				if (subNode.Attributes[keyAttrib.Name] != null &&
						subNode.Attributes[keyAttrib.Name].InnerText == keyAttrib.InnerText)
				{
					matchNode = subNode;
				}
				else if (keyAttrib != null
					  && keyAttrib.Name == "type")
				{
					Match subNodeMatch = _typeParsePattern.Match(subNode.Attributes[keyAttrib.Name].InnerText);
					Match keyAttribMatch = _typeParsePattern.Match(keyAttrib.InnerText);
					if (subNodeMatch.Success && keyAttribMatch.Success
							&& subNodeMatch.Result("$1") == keyAttribMatch.Result("$1"))
					{
						matchNode = subNode; //Have type class match (ignoring assembly-name suffix)
					}
				}
			}

			return matchNode; //return last match if > 1
		}

		#endregion private methods

	}
}



