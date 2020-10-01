using System.Xml;

namespace RogueModLoader
{
	public static class XmlReaderExtensions
	{
		public static bool ReadNonEmptyElement(this XmlReader reader)
		{
			bool isEmpty = reader.IsEmptyElement;
			reader.ReadStartElement();
			return !isEmpty;
		}

	}
}
