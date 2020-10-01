using Octokit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace RogueModLoader
{
	[XmlRoot("release")]
	public class RogueRelease : IXmlSerializable
	{
		public RogueRelease() { }
		public RogueRelease(RogueMod mod) => Mod = mod;

		public void From(Release release)
		{
			Tag = release.TagName;
			Prerelease = release.Draft || release.Prerelease;
			title = release.Name;
			description = release.Body;
			DownloadURL = new Uri(release.Assets[0].BrowserDownloadUrl);
			FileName = release.Assets[0].Name;
		}

		public RogueMod Mod { get; set; }

		public string Tag { get; set; }
		public bool Prerelease { get; set; }

		private string title;
		public string Title
		{
			get => title ?? Mod.Title;
			set => title = value;
		}
		private string description;
		public string Description
		{
			get => description ?? Mod.Description;
			set => description = value;
		}

		public Uri DownloadURL { get; set; }
		public string FileName { get; set; }

		public void WriteXml(XmlWriter xml)
		{
			xml.WriteAttributeString("tag", Tag);
			if (Prerelease) xml.WriteAttributeString("prerelease", "true");

			if (title != null)
				xml.WriteElementString("title", title);
			if (description != null)
				xml.WriteElementString("description", description);

			xml.WriteElementString("filename", FileName);

			xml.WriteElementString("url", DownloadURL.ToString());
		}
		public void ReadXml(XmlReader xml)
		{
			Tag = xml.GetAttribute("tag");
			string prerelease = xml.GetAttribute("prerelease");
			if (bool.TryParse(prerelease, out bool pre))
				Prerelease = pre;

			if (xml.ReadNonEmptyElement())
			{
				xml.MoveToContent();
				while (xml.NodeType != XmlNodeType.EndElement)
				{
					if (xml.Name == "title")
						Title = xml.ReadElementContentAsString();
					else if (xml.Name == "description")
						Description = xml.ReadElementContentAsString();
					else if (xml.Name == "url")
						DownloadURL = new Uri(xml.ReadElementContentAsString());
					else if (xml.Name == "filename")
						FileName = xml.ReadElementContentAsString();
					else
						xml.Skip();
					xml.MoveToContent();
				}
				xml.ReadEndElement();
			}
		}

		public System.Xml.Schema.XmlSchema GetSchema() => null;

		public override string ToString() => Tag;

	}

}
